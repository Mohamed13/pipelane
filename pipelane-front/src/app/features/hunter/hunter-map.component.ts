import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  NgZone,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
  inject,
} from '@angular/core';
import mapboxgl, {
  type GeoJSONSource,
  type Map as MapboxMap,
  type MapLayerMouseEvent,
  type MapboxGeoJSONFeature,
} from 'mapbox-gl';

import { MapboxService, toGeoJSON, type HunterFeatureCollection } from '../../core/mapbox.service';
import type { HunterResult } from '../../core/models';

const DEFAULT_CENTER: [number, number] = [2.3522, 48.8566];
type PointGeometry = {
  type: 'Point';
  coordinates: [number, number];
};

export interface HunterResultVm extends HunterResult {
  lat?: number | null;
  lng?: number | null;
}

@Component({
  selector: 'app-hunter-map',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './hunter-map.component.html',
  styleUrls: ['./hunter-map.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HunterMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() items: HunterResultVm[] = [];
  @Input() selectedId?: string | null;

  @Output() itemSelect = new EventEmitter<string>();
  @Output() addToList = new EventEmitter<string>();

  @ViewChild('mapEl', { static: false }) private mapElement?: ElementRef<HTMLDivElement>;

  private readonly mapbox = inject(MapboxService);
  private readonly zone = inject(NgZone);
  protected readonly hasToken = this.mapbox.hasToken();

  private map?: MapboxMap;
  private popup?: mapboxgl.Popup;
  private layersAttached = false;
  private readonly sourceId = 'hunter';
  private currentFeatureStateId?: number;
  private hasFitView = false;
  private readonly itemsById = new Map<string, HunterResultVm>();

  ngAfterViewInit(): void {
    if (!this.hasToken) {
      return;
    }
    const hostRef = this.mapElement;
    if (!hostRef) {
      return;
    }
    const host = hostRef.nativeElement;
    if (!host) {
      return;
    }

    this.zone.runOutsideAngular(() => {
      const map = this.mapbox.init(host, DEFAULT_CENTER, 11);
      this.map = map;

      map.on('load', () => {
        this.layersAttached = true;
        this.attachData(toGeoJSON(this.items));
        this.attachInteractions();
      });
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['items']) {
      this.hasFitView = false;
      this.rebuildIndex();
      this.zone.runOutsideAngular(() => this.updateData());
    }

    if (changes['selectedId'] && !changes['selectedId'].firstChange) {
      this.zone.runOutsideAngular(() => this.applySelection(true));
    }
  }

  ngOnDestroy(): void {
    this.popup?.remove();
    if (this.map) {
      this.map.remove();
    }
  }

  private updateData(): void {
    const data = toGeoJSON(this.items);
    if (!this.map || !this.layersAttached) {
      return;
    }

    this.attachData(data);
  }

  private attachData(data: HunterFeatureCollection): void {
    if (!this.map) {
      return;
    }

    this.mapbox.addClusteredSource(this.map, this.sourceId, data);
    this.mapbox.addLayersForClusters(this.map, this.sourceId);

    if (!this.hasFitView && data.features.length > 0) {
      this.mapbox.fitTo(this.map, data);
      this.hasFitView = true;
    }

    this.applySelection(true);
  }

  private attachInteractions(): void {
    const map = this.map;
    if (!map) {
      return;
    }

    const clusterLayerId = `${this.sourceId}-clusters`;
    const pointsLayerId = `${this.sourceId}-points`;

    map.on('click', clusterLayerId, (event: MapLayerMouseEvent) => {
      const features = map.queryRenderedFeatures(event.point, { layers: [clusterLayerId] });
      const feature = features[0] as MapboxGeoJSONFeature | undefined;
      if (!feature) {
        return;
      }

      const rawSource = map.getSource(this.sourceId);
      if (!rawSource || !(rawSource as GeoJSONSource).getClusterExpansionZoom) {
        return;
      }
      const source = rawSource as GeoJSONSource;
      const clusterId = feature.properties?.['cluster_id'];
      if (typeof clusterId !== 'number') {
        return;
      }

      source.getClusterExpansionZoom(clusterId, (error, zoom) => {
        if (error) {
          return;
        }
        if (typeof zoom !== 'number') {
          return;
        }
        map.easeTo({
          center: (feature.geometry as PointGeometry).coordinates,
          zoom,
        });
      });
    });

    map.on('click', pointsLayerId, (event: MapLayerMouseEvent) => {
      const feature = event.features?.[0] as MapboxGeoJSONFeature | undefined;
      if (!feature) {
        return;
      }
      this.openPopup(feature);
    });

    map.on('mouseenter', pointsLayerId, () => {
      map.getCanvas().style.cursor = 'pointer';
    });

    map.on('mouseleave', pointsLayerId, () => {
      map.getCanvas().style.cursor = '';
    });
  }

  private openPopup(feature: MapboxGeoJSONFeature): void {
    if (!this.map) {
      return;
    }

    const coords = feature.geometry.type === 'Point' ? feature.geometry.coordinates : null;
    const properties = feature.properties ?? {};
    const id = typeof properties['id'] === 'string' ? properties['id'] : undefined;
    const company = typeof properties['company'] === 'string' ? properties['company'] : 'Prospect';
    const city = typeof properties['city'] === 'string' ? properties['city'] : '';
    const scoreRaw = Number(properties['score']);
    const score = Number.isFinite(scoreRaw) ? Math.round(scoreRaw) : 0;

    if (!coords || !id) {
      return;
    }

    const [lng, lat] = coords as [number, number];
    const reasons = this.lookupReasons(id);
    const topReasons = reasons.slice(0, 3);
    const html = this.buildPopupHtml(company, city, score, topReasons);

    this.popup?.remove();
    this.popup = new mapboxgl.Popup({
      closeButton: true,
      closeOnMove: true,
      offset: 18,
      className: 'hunter-map-popup',
      maxWidth: '280px',
    })
      .setLngLat([lng, lat])
      .setHTML(html)
      .addTo(this.map);

    this.popup.on('open', () => {
      const popupEl = this.popup?.getElement();
      if (!popupEl) {
        return;
      }

      const selectBtn = popupEl.querySelector<HTMLButtonElement>('button[data-action="select"]');
      const addBtn = popupEl.querySelector<HTMLButtonElement>('button[data-action="add"]');

      const handleClick = (event: Event, emitter: EventEmitter<string>): void => {
        event.preventDefault();
        event.stopPropagation();
        this.zone.run(() => emitter.emit(id));
        this.popup?.remove();
      };

      const stopGesture = (event: Event): void => {
        event.stopPropagation();
      };

      if (selectBtn) {
        selectBtn.addEventListener('click', (event) => handleClick(event, this.itemSelect));
        selectBtn.addEventListener('touchstart', stopGesture, { passive: true });
        selectBtn.addEventListener('dblclick', stopGesture);
        requestAnimationFrame(() => selectBtn.focus());
      }

      if (addBtn) {
        addBtn.addEventListener('click', (event) => handleClick(event, this.addToList));
        addBtn.addEventListener('touchstart', stopGesture, { passive: true });
        addBtn.addEventListener('dblclick', stopGesture);
      }
    });
  }

  private applySelection(resetPrevious: boolean): void {
    if (!this.map || !this.layersAttached) {
      return;
    }

    if (resetPrevious && this.currentFeatureStateId !== undefined) {
      this.map.setFeatureState(
        { source: this.sourceId, id: this.currentFeatureStateId },
        { selected: false },
      );
      this.currentFeatureStateId = undefined;
    }

    if (!this.selectedId) {
      return;
    }

    const features = this.map.querySourceFeatures(this.sourceId, {
      filter: ['!', ['has', 'point_count']],
    }) as MapboxGeoJSONFeature[];
    const match = features.find((feature) => feature.properties?.['id'] === this.selectedId);

    if (match && typeof match.id === 'number') {
      this.currentFeatureStateId = match.id;
      this.map.setFeatureState({ source: this.sourceId, id: match.id }, { selected: true });
    }
  }

  private buildPopupHtml(company: string, city: string, score: number, reasons: string[]): string {
    const subtitle = city ? `<span class="city">${city}</span>` : '';
    const reasonsHtml =
      reasons.length > 0
        ? `<ul>${reasons.map((reason) => `<li>${this.escape(reason)}</li>`).join('')}</ul>`
        : '<p class="hint">Aucune raison détaillée fournie.</p>';

    return `
      <div class="popup-title">
        <div class="heading">
          <strong>${this.escape(company)}</strong>
          ${subtitle}
        </div>
        <span class="score-chip" aria-label="Score prospect">${score}/100</span>
      </div>
      ${reasonsHtml}
      <div class="popup-actions">
        <button type="button" class="primary" data-action="select" aria-label="Sélectionner ce prospect">Sélectionner</button>
        <button type="button" data-action="add" aria-label="Ajouter ce prospect à une liste">Ajouter</button>
      </div>
    `;
  }

  private rebuildIndex(): void {
    this.itemsById.clear();
    for (const item of this.items ?? []) {
      const id = item?.prospectId;
      if (typeof id !== 'string' || id.trim().length === 0) {
        continue;
      }
      this.itemsById.set(id, item);
    }
  }

  private lookupReasons(id: string): string[] {
    const match = this.itemsById.get(id);
    const source = match?.why ?? [];
    if (!Array.isArray(source)) {
      return [];
    }

    return source.filter((reason): reason is string => typeof reason === 'string');
  }

  private escape(value: string): string {
    return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }
}
