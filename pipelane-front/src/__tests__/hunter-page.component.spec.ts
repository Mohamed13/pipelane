import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';

import { ApiService } from '../app/core/api.service';
import { HunterResult } from '../app/core/models';
import { HunterPageComponent } from '../app/features/hunter/hunter-page.component';

const snackbarStub = { open: jest.fn() };

describe('HunterPageComponent', () => {
  let component: HunterPageComponent;
  let api: jest.Mocked<ApiService>;

  const createResult = (id: string, score: number, city: string): HunterResult => ({
    prospectId: id,
    prospect: {
      company: `Company ${id}`,
      email: `${id}@example.com`,
      phone: '+33600000000',
      city,
      country: 'France',
      website: `https://example-${id}.fr`,
      firstName: 'Alex',
      lastName: 'Martin',
      social: null,
      whatsAppMsisdn: null,
    },
    features: {
      rating: 4,
      reviews: 120,
      hasSite: true,
      booking: false,
      socialActive: true,
      cms: 'WordPress',
      mobileOk: true,
      pixelPresent: true,
      lcpSlow: false,
    },
    score,
    why: ['Bon profil', 'Site actif'],
  });

  beforeEach(async () => {
    api = {
      hunterSearch: jest.fn(),
      listSummaries: jest.fn().mockReturnValue(of([])),
      uploadHunterCsv: jest.fn(),
      createList: jest.fn(),
      addToList: jest.fn(),
      createCadenceFromList: jest.fn(),
      seedHunterDemo: jest.fn(),
    } as unknown as jest.Mocked<ApiService>;

    await TestBed.configureTestingModule({
      imports: [HunterPageComponent],
      providers: [
        { provide: ApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackbarStub },
      ],
    })
      .overrideComponent(HunterPageComponent, {
        set: { template: '' },
      })
      .compileComponents();

    const fixture = TestBed.createComponent(HunterPageComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  it('populates results when search succeeds', () => {
    api.hunterSearch.mockReturnValue(
      of({
        total: 2,
        duplicates: 1,
        items: [createResult('prospect-1', 82, 'Paris'), createResult('prospect-2', 74, 'Lyon')],
      }),
    );

    component.search(false);

    expect(api.hunterSearch).toHaveBeenCalledWith(
      expect.objectContaining({ industry: 'Restaurants', source: 'mapsStub' }),
      { dryRun: false },
    );
    expect(component.total()).toBe(2);
    expect(component.duplicates()).toBe(1);
    expect(component.visibleResults().length).toBe(2);
    expect(component.lastRunMode()).toBe('live');
    expect(component.loading()).toBe(false);
  });

  it('magicPick selects diversified prospects across cities', () => {
    const leads = [
      createResult('paris-1', 92, 'Paris'),
      createResult('paris-2', 85, 'Paris'),
      createResult('lyon-1', 88, 'Lyon'),
      createResult('lyon-2', 81, 'Lyon'),
      createResult('marseille-1', 79, 'Marseille'),
    ];
    component['results'].set(leads);

    component.magicPick(3);

    const selected = component.selection.selected;
    expect(selected.length).toBe(3);
    expect(selected.some((id) => id.startsWith('paris'))).toBe(true);
    expect(selected.some((id) => id.startsWith('lyon'))).toBe(true);
    expect(selected.some((id) => id.startsWith('marseille'))).toBe(true);
  });

  it('loads demo prospects when seedDemo is executed', () => {
    api.seedHunterDemo.mockReturnValue(
      of({
        total: 2,
        duplicates: 0,
        items: [createResult('demo-1', 80, 'Paris'), createResult('demo-2', 78, 'Lyon')],
      }),
    );

    component.seedDemo();

    expect(api.seedHunterDemo).toHaveBeenCalled();
    expect(component.visibleResults().length).toBe(2);
    expect(component.total()).toBe(2);
  });

  it('builds map items with heuristic coordinates when API lacks lat/lng', () => {
    const result = createResult('map-1', 82, 'Paris');
    component['results'].set([result]);

    const mapItems = component.mapItems();
    expect(mapItems).toHaveLength(1);
    expect(mapItems[0]?.lat).toBeDefined();
    expect(mapItems[0]?.lng).toBeDefined();
  });

  it('leaves map coordinates undefined when no location information is available', () => {
    const result = createResult('map-2', 70, '');
    result.prospect.city = null;
    component['results'].set([result]);

    const mapItems = component.mapItems();
    expect(mapItems[0]?.lat).toBeUndefined();
    expect(mapItems[0]?.lng).toBeUndefined();
  });
});
