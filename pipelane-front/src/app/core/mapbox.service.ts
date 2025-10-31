import { Injectable } from '@angular/core';
import mapboxgl, { type GeoJSONSource, type Map as MapboxMap } from 'mapbox-gl';

import { MAPBOX_TOKEN } from './env.generated';
import type { HunterResult } from './models';

type HunterFeatureProperties = {
  id?: string;
  company?: string;
  city?: string;
  score?: number;
};

type HunterFeature = {
  type: 'Feature';
  geometry: {
    type: 'Point';
    coordinates: [number, number];
  };
  properties: HunterFeatureProperties;
};

export type HunterFeatureCollection = {
  type: 'FeatureCollection';
  features: HunterFeature[];
};

type HunterResultWithCoordinates = HunterResult & {
  lat?: number | null;
  lng?: number | null;
};

@Injectable({
  providedIn: 'root',
})
export class MapboxService {
  private readonly token = MAPBOX_TOKEN ?? '';
  private mapInstance?: MapboxMap;

  constructor() {
    if (this.token) {
      mapboxgl.accessToken = this.token;
    }
  }

  hasToken(): boolean {
    return this.token.length > 0;
  }

  init(container: HTMLElement, center: [number, number], zoom: number): MapboxMap {
    const map = new mapboxgl.Map({
      container,
      style: 'mapbox://styles/mapbox/dark-v11',
      center,
      zoom,
      attributionControl: false,
    });
    this.mapInstance = map;
    return map;
  }

  addClusteredSource(map: MapboxMap, sourceId: string, data: HunterFeatureCollection): void {
    const existing = map.getSource(sourceId) as GeoJSONSource | undefined;
    if (existing) {
      existing.setData(data);
      return;
    }

    map.addSource(sourceId, {
      type: 'geojson',
      data,
      cluster: true,
      clusterRadius: 60,
      clusterMaxZoom: 13,
      generateId: true,
    });
  }

  addLayersForClusters(map: MapboxMap, sourceId: string): void {
    const clusterLayerId = `${sourceId}-clusters`;
    if (!map.getLayer(clusterLayerId)) {
      map.addLayer({
        id: clusterLayerId,
        type: 'circle',
        source: sourceId,
        filter: ['has', 'point_count'],
        paint: {
          'circle-color': ['step', ['get', 'point_count'], '#3b82f6', 20, '#6366f1', 50, '#8b5cf6'],
          'circle-radius': ['step', ['get', 'point_count'], 16, 20, 24, 50, 32],
          'circle-opacity': 0.75,
        },
      });
    }

    const countLayerId = `${sourceId}-cluster-count`;
    if (!map.getLayer(countLayerId)) {
      map.addLayer({
        id: countLayerId,
        type: 'symbol',
        source: sourceId,
        filter: ['has', 'point_count'],
        layout: {
          'text-field': ['get', 'point_count_abbreviated'],
          'text-font': ['Inter', 'Arial Unicode MS Bold'],
          'text-size': 12,
        },
        paint: {
          'text-color': '#0b1622',
        },
      });
    }

    const pointsLayerId = `${sourceId}-points`;
    if (!map.getLayer(pointsLayerId)) {
      map.addLayer({
        id: pointsLayerId,
        type: 'circle',
        source: sourceId,
        filter: ['!', ['has', 'point_count']],
        paint: {
          'circle-radius': ['case', ['boolean', ['feature-state', 'selected'], false], 12, 10],
          'circle-color': [
            'case',
            ['<', ['get', 'score'], 41],
            '#F87171',
            ['<', ['get', 'score'], 70],
            '#F59E0B',
            '#60F7A3',
          ],
          'circle-opacity': 0.85,
          'circle-stroke-width': [
            'case',
            ['boolean', ['feature-state', 'selected'], false],
            3,
            1.5,
          ],
          'circle-stroke-color': [
            'case',
            ['boolean', ['feature-state', 'selected'], false],
            '#FFFFFF',
            '#031322',
          ],
        },
      });
    }
  }

  fitTo(data: HunterFeatureCollection): void;
  fitTo(map: MapboxMap, data: HunterFeatureCollection): void;
  fitTo(mapOrData: MapboxMap | HunterFeatureCollection, data?: HunterFeatureCollection): void {
    const map = mapOrData instanceof mapboxgl.Map ? mapOrData : this.mapInstance;
    const collection = mapOrData instanceof mapboxgl.Map ? data : mapOrData;

    if (!map || !collection || !collection.features.length) {
      return;
    }

    const bounds = new mapboxgl.LngLatBounds();
    for (const feature of collection.features) {
      if (feature.geometry?.type === 'Point') {
        const [lng, lat] = feature.geometry.coordinates as [number, number];
        bounds.extend([lng, lat]);
      }
    }

    if (!bounds.isEmpty()) {
      map.fitBounds(bounds, {
        padding: { top: 40, bottom: 40, left: 40, right: 40 },
        duration: 600,
        maxZoom: 14,
      });
    }
  }
}

export function toGeoJSON(
  results: HunterResultWithCoordinates[] | null | undefined,
): HunterFeatureCollection {
  const features: HunterFeature[] = [];
  if (!results) {
    return { type: 'FeatureCollection', features };
  }

  for (const item of results) {
    const lat = item.lat;
    const lng = item.lng;
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
      continue;
    }

    features.push({
      type: 'Feature',
      geometry: {
        type: 'Point',
        coordinates: [lng as number, lat as number],
      },
      properties: {
        id: item.prospectId,
        company: item.prospect?.company ?? 'Prospect',
        city: item.prospect?.city ?? '',
        score: item.score ?? 0,
      },
    });
  }

  return {
    type: 'FeatureCollection',
    features,
  };
}
