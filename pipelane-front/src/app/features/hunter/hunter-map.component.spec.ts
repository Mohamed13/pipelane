import { SimpleChange } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HunterMapComponent, type HunterResultVm } from './hunter-map.component';
import { MapboxService } from '../../core/mapbox.service';

type FeatureCollection = {
  type: 'FeatureCollection';
  features: Array<{
    properties?: Record<string, unknown>;
  }>;
};

type ProspectStub = {
  company: string;
  city?: string | null;
};

class MapStub {
  private loadHandlers: Array<() => void> = [];

  on(event: string, handler: () => void): this {
    if (event === 'load') {
      this.loadHandlers.push(handler);
    }
    return this;
  }

  emitLoad(): void {
    for (const handler of this.loadHandlers) {
      handler();
    }
  }

  remove(): void {
    // noop
  }

  queryRenderedFeatures(): unknown[] {
    return [];
  }

  getCanvas(): { style: Record<string, string> } {
    return { style: { cursor: '' } };
  }

  getSource(): {
    getClusterExpansionZoom: (clusterId: number, cb: (err: unknown, zoom: number) => void) => void;
  } {
    return {
      getClusterExpansionZoom: (_clusterId, cb) => cb(null, 12),
    };
  }

  easeTo(): void {
    // noop
  }

  querySourceFeatures(): unknown[] {
    return [];
  }
}

class MapboxServiceStub {
  init = jest.fn();
  addClusteredSource = jest.fn();
  addLayersForClusters = jest.fn();
  fitTo = jest.fn();

  constructor(private readonly tokenPresent: boolean) {}

  hasToken(): boolean {
    return this.tokenPresent;
  }
}

describe('HunterMapComponent', () => {
  afterEach(() => {
    jest.clearAllMocks();
  });

  it('renders fallback and skips map initialisation without token', () => {
    const service = new MapboxServiceStub(false);
    TestBed.configureTestingModule({
      imports: [HunterMapComponent],
      providers: [{ provide: MapboxService, useValue: service }],
    });

    const fixture = TestBed.createComponent(HunterMapComponent);
    fixture.detectChanges();

    expect(service.init).not.toHaveBeenCalled();
    const fallback = fixture.nativeElement.querySelector('.map-fallback');
    expect(fallback).toBeTruthy();
  });

  it('pushes GeoJSON data to Mapbox when token available', () => {
    const mapStub = new MapStub();
    const service = new MapboxServiceStub(true);
    service.init.mockReturnValue(mapStub);

    TestBed.configureTestingModule({
      imports: [HunterMapComponent],
      providers: [{ provide: MapboxService, useValue: service }],
    });

    const fixture: ComponentFixture<HunterMapComponent> =
      TestBed.createComponent(HunterMapComponent);
    const component = fixture.componentInstance;

    const prospect: ProspectStub = {
      company: 'Brasserie Demo',
      city: 'Paris',
    };
    const features: Record<string, unknown> = {};
    const item: HunterResultVm = {
      prospectId: '11111111-1111-1111-1111-111111111111',
      prospect,
      features,
      score: 78,
      why: ['Très bien noté', 'Actif sur Instagram'],
      lat: 48.8566,
      lng: 2.3522,
    };

    component.items = [item];
    fixture.detectChanges();

    mapStub.emitLoad();

    expect(service.init).toHaveBeenCalledTimes(1);
    expect(service.addClusteredSource).toHaveBeenCalledTimes(1);
    const [, , data] = service.addClusteredSource.mock.calls[0] as [
      unknown,
      string,
      FeatureCollection,
    ];
    expect(data.features).toHaveLength(1);
    expect(data.features[0]?.properties?.['score']).toBe(78);
  });

  it('refits the viewport when items update after initial load', () => {
    const mapStub = new MapStub();
    const service = new MapboxServiceStub(true);
    service.init.mockReturnValue(mapStub);

    TestBed.configureTestingModule({
      imports: [HunterMapComponent],
      providers: [{ provide: MapboxService, useValue: service }],
    });

    const fixture: ComponentFixture<HunterMapComponent> =
      TestBed.createComponent(HunterMapComponent);
    const component = fixture.componentInstance;

    const firstProspect: ProspectStub = { company: 'Alpha', city: 'Paris' };
    const firstFeatures: Record<string, unknown> = {};
    const firstItem: HunterResultVm = {
      prospectId: 'aaa',
      prospect: firstProspect,
      features: firstFeatures,
      score: 60,
      lat: 48.85,
      lng: 2.35,
      why: ['Raison initiale'],
    };

    component.items = [firstItem];
    fixture.detectChanges();
    mapStub.emitLoad();

    expect(service.fitTo).toHaveBeenCalledTimes(1);

    service.fitTo.mockClear();

    const nextProspect: ProspectStub = { company: 'Beta', city: 'Lyon' };
    const nextFeatures: Record<string, unknown> = {};
    const nextItem: HunterResultVm = {
      prospectId: 'bbb',
      prospect: nextProspect,
      features: nextFeatures,
      score: 82,
      lat: 45.76,
      lng: 4.83,
      why: ['Second raison'],
    };

    const previousItems = component.items;
    component.items = [nextItem];
    component.ngOnChanges({
      items: new SimpleChange(previousItems, component.items, false),
    });

    expect(service.fitTo).toHaveBeenCalledTimes(1);
  });
});
