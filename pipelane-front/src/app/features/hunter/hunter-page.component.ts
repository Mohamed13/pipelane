import { SelectionModel } from '@angular/cdk/collections';
import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  ViewChild,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, PartialObserver, Subscription, finalize } from 'rxjs';

import { HunterMapComponent, type HunterResultVm } from './hunter-map.component';
import { ApiService } from '../../core/api.service';
import { MAPBOX_TOKEN } from '../../core/env.generated';
import { environment } from '../../core/environment';
import {
  AddToListPayload,
  HunterFilters,
  HunterResult,
  HunterSearchCriteria,
  ListSummary,
} from '../../core/models';
import { SubscriptionStore } from '../../core/subscription-store';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

interface HunterFormValue {
  industry: string;
  city: string;
  radiusKm: number;
  source: string;
  ratingMin: number;
  reviewsMin: number;
  hasSite: boolean;
  booking: boolean;
  socialActive: boolean;
  siteIssuesOnly: boolean;
}

interface PersonaShortcut {
  key: string;
  label: string;
  industry: string;
  filters?: Partial<
    Pick<HunterFormValue, 'hasSite' | 'booking' | 'socialActive' | 'siteIssuesOnly'>
  >;
  hint?: string;
}

interface HeatmapSlot {
  hour: string;
  label: string;
  intensity: number;
}

const HEATMAP_HOURS = ['10h', '11h', '14h', '15h', '16h'] as const;

const CITY_COORDINATES: Record<string, { lat: number; lng: number }> = {
  paris: { lat: 48.8566, lng: 2.3522 },
  lyon: { lat: 45.764, lng: 4.8357 },
  marseille: { lat: 43.2965, lng: 5.3698 },
  lille: { lat: 50.6292, lng: 3.0573 },
  toulouse: { lat: 43.6045, lng: 1.4442 },
  nantes: { lat: 47.2184, lng: -1.5536 },
  bordeaux: { lat: 44.8378, lng: -0.5792 },
  nice: { lat: 43.7102, lng: 7.262 },
  montpellier: { lat: 43.6108, lng: 3.8767 },
  strasbourg: { lat: 48.5734, lng: 7.7521 },
};

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function jitter(coord: { lat: number; lng: number }, seed: number): { lat: number; lng: number } {
  const deltaLat = ((seed % 7) - 3) * 0.02;
  const deltaLng = ((seed % 11) - 5) * 0.02;
  return {
    lat: clamp(coord.lat + deltaLat, 42, 51),
    lng: clamp(coord.lng + deltaLng, -1, 8),
  };
}

function approximateCoordinates(
  city: string | null | undefined,
  seed: number,
): { lat: number; lng: number } {
  if (!city) {
    return jitter({ lat: 46.6, lng: 2.3 }, seed);
  }

  const key = city.trim().toLowerCase();
  const known = CITY_COORDINATES[key];
  if (known) {
    return jitter(known, seed);
  }

  const hash =
    Array.from(key).reduce((acc, char) => acc + char.charCodeAt(0), 0) +
    seed * 17 +
    key.length * 13;
  const lat = 42 + ((hash % 1000) / 1000) * (51 - 42);
  const lng = -1 + ((Math.floor(hash / 3) % 1000) / 1000) * (8 - -1);
  return jitter({ lat, lng }, seed);
}

function scoreToColor(score: number): string {
  const bounded = clamp(score, 0, 100);
  const hue = (bounded / 100) * 120; // 0 red → 120 green
  return `hsl(${hue.toFixed(0)}, 80%, 55%)`;
}

function titleCase(input: string): string {
  return input
    .split(/[\s-]/)
    .filter(Boolean)
    .map((word) => word[0]?.toUpperCase() + word.slice(1))
    .join(' ');
}

@Component({
  standalone: true,
  selector: 'app-hunter-page',
  templateUrl: './hunter-page.component.html',
  styleUrls: ['./hunter-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ScrollingModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatCheckboxModule,
    MatChipsModule,
    MatSnackBarModule,
    MatDividerModule,
    MatSidenavModule,
    PageHeaderComponent,
    HunterMapComponent,
  ],
})
export class HunterPageComponent implements OnDestroy {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackbar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly subscriptions = new SubscriptionStore();

  private subscribe<T>(
    source: Observable<T>,
    observer: PartialObserver<T>,
    key?: string,
  ): Subscription {
    return this.subscriptions.subscribe(source, observer, key);
  }

  readonly personas: PersonaShortcut[] = [
    {
      key: 'web',
      label: 'Sites web',
      industry: 'Agences web',
      filters: { hasSite: true, socialActive: true },
      hint: 'Agences digitales cherchant des PME pour refonte de site',
    },
    {
      key: 'restaurants',
      label: 'Restaurants',
      industry: 'Restaurants',
      filters: { booking: true, socialActive: true },
      hint: 'Restaurants à Paris avec au moins 4★ et 80 avis',
    },
    {
      key: 'plumbing',
      label: 'Plomberie',
      industry: 'Plomberie',
      filters: { hasSite: false, socialActive: false, siteIssuesOnly: true },
      hint: 'Artisans plomberie sans site ou site obsolète en Île-de-France',
    },
    {
      key: 'training',
      label: 'Formation',
      industry: 'Formation professionnelle',
      filters: { hasSite: true, booking: false, socialActive: true },
      hint: 'Organismes de formation B2B autour de Lyon',
    },
    {
      key: 'custom',
      label: 'Autre',
      industry: 'PME services',
    },
  ];

  private readonly defaultPersona: PersonaShortcut = this.personas[1] ??
    this.personas[0] ?? { key: 'default', label: 'Général', industry: '' };
  readonly activePersona = signal<PersonaShortcut>(this.defaultPersona);
  readonly naturalLanguage = signal('');
  readonly uploadedCsvId = signal<string | null>(null);
  readonly csvFileName = signal<string | null>(null);
  readonly demoMode = environment.DEMO_MODE;

  readonly filtersForm: FormGroup = this.fb.group({
    industry: ['Restaurants'],
    city: ['Paris'],
    radiusKm: [8, [Validators.min(1), Validators.max(50)]],
    source: ['mapsStub'],
    ratingMin: [3],
    reviewsMin: [10],
    hasSite: [false],
    booking: [false],
    socialActive: [false],
    siteIssuesOnly: [false],
  });

  readonly loading = signal(false);
  readonly results = signal<HunterResult[]>([]);
  readonly total = signal(0);
  readonly duplicates = signal(0);
  readonly lists = signal<ListSummary[]>([]);
  readonly lastRunMode = signal<'live' | 'dry' | null>(null);
  @ViewChild(CdkVirtualScrollViewport) private viewport?: CdkVirtualScrollViewport;

  readonly excluded = signal<Set<string>>(new Set());
  readonly favorites = signal<Set<string>>(new Set());
  readonly siteIssuesOnly = signal(false);

  readonly visibleResults = computed(() => {
    const excluded = this.excluded();
    const issuesOnly = this.siteIssuesOnly();
    return this.results().filter((result) => {
      if (excluded.has(result.prospectId)) {
        return false;
      }

      if (!issuesOnly) {
        return true;
      }

      const features = result.features;
      const hasIssue =
        features.pixelPresent === false ||
        features.mobileOk === false ||
        features.lcpSlow === true ||
        features.hasSite === false;
      return hasIssue;
    });
  });

  readonly mapboxToken = MAPBOX_TOKEN;
  readonly mapItems = computed<HunterResultVm[]>(() => {
    const results = this.visibleResults();
    return results.map((result, index) => {
      const coords = this.extractCoordinates(result, index);
      return {
        ...result,
        lat: coords.lat,
        lng: coords.lng,
        why: result.why ?? [],
      };
    });
  });
  readonly mapSelectedId = computed(() => this.activeProspect()?.prospectId ?? null);
  readonly legendItems = [
    {
      icon: 'check_circle',
      label:
        'Icône verte = signal présent (site, réservation, réseaux). Icône rouge = opportunité à corriger.',
    },
    {
      icon: 'sell',
      label: 'Pastilles bleues = raisons principales identifiées par Hunter.',
    },
    {
      icon: 'speed',
      label: 'Score 0–100 = priorité : plus il est élevé, plus le prospect est chaud.',
    },
    {
      icon: 'auto_awesome',
      label: 'Magic Pick répartit automatiquement une sélection équilibrée par zones et scores.',
    },
  ] as const;

  readonly selection = new SelectionModel<string>(true);
  readonly selectionCount = computed(() => this.selection.selected.length);
  readonly selectedListId = signal<string | null>(null);
  readonly activeProspect = signal<HunterResult | null>(null);

  constructor() {
    effect(() => {
      this.refreshLists();
    });

    this.siteIssuesOnly.set(this.filtersForm.get('siteIssuesOnly')?.value ?? false);
    const siteIssuesControl = this.filtersForm.get('siteIssuesOnly');
    if (siteIssuesControl) {
      this.subscribe(siteIssuesControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)), {
        next: (value) => this.siteIssuesOnly.set(!!value),
      });
    }

    const sourceControl = this.filtersForm.get('source');
    if (sourceControl) {
      this.subscribe(sourceControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)), {
        next: (value) => {
          if (value !== 'csv') {
            this.uploadedCsvId.set(null);
            this.csvFileName.set(null);
          }
        },
      });
    }
  }

  seedDemo(): void {
    this.loading.set(true);
    this.subscribe(
      this.api.seedHunterDemo().pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      ),
      {
        next: (response) => {
          this.results.set(response.items ?? []);
          this.total.set(response.total);
          this.duplicates.set(response.duplicates);
          this.selection.clear();
          this.excluded.set(new Set());
          this.favorites.set(new Set());
          this.activeProspect.set(null);
          this.lastRunMode.set('live');
          this.snackbar.open('50 prospects de démo ajoutés.', 'Fermer', { duration: 2800 });
          this.refreshLists();
        },
        error: () =>
          this.snackbar.open('Impossible de charger les prospects de démo.', 'Fermer', {
            duration: 3200,
          }),
      },
      'seed-demo',
    );
  }

  selectPersona(persona: PersonaShortcut): void {
    this.activePersona.set(persona);
    const current = this.filtersForm.value as HunterFormValue;
    const patch: Partial<HunterFormValue> = {
      industry: persona.industry,
      hasSite: persona.filters?.hasSite ?? current.hasSite,
      booking: persona.filters?.booking ?? current.booking,
      socialActive: persona.filters?.socialActive ?? current.socialActive,
      siteIssuesOnly: persona.filters?.siteIssuesOnly ?? current.siteIssuesOnly,
    };

    this.filtersForm.patchValue(patch, { emitEvent: false });
    this.siteIssuesOnly.set(patch.siteIssuesOnly ?? current.siteIssuesOnly);
    if (persona.hint) {
      this.naturalLanguage.set(persona.hint);
    }
  }

  onDescriptionInput(event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;
    this.naturalLanguage.set(value);
  }

  interpretDescription(): void {
    const text = this.naturalLanguage().trim();
    if (!text) {
      this.snackbar.open('Ajoutez une description à interpréter.', 'Fermer', { duration: 2500 });
      return;
    }

    const lower = text.toLowerCase();
    const updates: Partial<HunterFormValue> = {};

    if (/restaurant|bistro|brasserie/.test(lower)) {
      updates.industry = 'Restaurants';
    } else if (/plomb|chauffage/.test(lower)) {
      updates.industry = 'Plomberie';
    } else if (/formation|bootcamp|cfa/.test(lower)) {
      updates.industry = 'Formation professionnelle';
    } else if (/site web|agence|digitale|marketing/.test(lower)) {
      updates.industry = 'Agences web';
    }

    const cityMatch = lower.match(/(?:à|a|sur)\s+([a-zàâçéèêëîïôûùüÿñ\- ]{2,})/);
    const city = cityMatch?.[1];
    if (city) {
      updates.city = titleCase(city.trim());
    }

    const radiusMatch = lower.match(/(\d{1,2})\s*(?:km|kilom)/);
    const radiusValue = radiusMatch?.[1];
    if (radiusValue) {
      updates.radiusKm = Number(radiusValue);
    }

    const ratingMatch = lower.match(/(?:note|min(?:imale)?)\s*(?:de)?\s*(\d(?:[.,]\d)?)/);
    const ratingValue = ratingMatch?.[1];
    if (ratingValue) {
      updates.ratingMin = Number(ratingValue.replace(',', '.'));
    }

    const reviewsMatch = lower.match(/(?:au moins|min(?:imum)?)\s*(\d{1,4})\s*(?:avis)/);
    const reviewsValue = reviewsMatch?.[1];
    if (reviewsValue) {
      updates.reviewsMin = Number(reviewsValue);
    }

    if (/sans site|pas de site/.test(lower)) {
      updates.hasSite = false;
      updates.siteIssuesOnly = true;
    } else if (/site moderne|refonte|avec site/.test(lower)) {
      updates.hasSite = true;
    }

    if (/réseaux|instagram|social/.test(lower)) {
      updates.socialActive = true;
    }

    if (/réservation|booking|module de resa/.test(lower)) {
      updates.booking = true;
    }

    if (/probl[èe]me|lent|mobile|pixel/.test(lower)) {
      updates.siteIssuesOnly = true;
    }

    this.filtersForm.patchValue(updates, { emitEvent: false });
    if (updates.siteIssuesOnly !== undefined) {
      this.siteIssuesOnly.set(updates.siteIssuesOnly);
    }
    this.snackbar.open('Description interprétée.', 'Fermer', { duration: 2500 });
  }

  search(dryRun = false): void {
    if (this.filtersForm.invalid) {
      this.filtersForm.markAllAsTouched();
      return;
    }

    const form = this.filtersForm.value as HunterFormValue;
    if (form.source === 'csv' && !this.uploadedCsvId()) {
      this.snackbar.open('Importez un CSV avant de lancer la recherche.', 'Fermer', {
        duration: 3000,
      });
      return;
    }

    const criteria = this.buildCriteria(form);
    this.loading.set(true);
    this.subscribe(
      this.api.hunterSearch(criteria, { dryRun }).pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      ),
      {
        next: (response) => {
          this.results.set(response.items ?? []);
          this.total.set(response.total);
          this.duplicates.set(response.duplicates);
          this.selection.clear();
          this.excluded.set(new Set());
          this.favorites.set(new Set());
          this.activeProspect.set(null);
          this.lastRunMode.set(dryRun ? 'dry' : 'live');
          if (!dryRun) {
            this.refreshLists();
          }
        },
        error: () => {
          this.snackbar.open('Recherche Hunter impossible pour le moment.', 'Fermer', {
            duration: 3000,
          });
        },
      },
      'hunter-search',
    );
  }

  toggleAll(): void {
    const current = this.visibleResults();
    if (current.length === 0) {
      this.selection.clear();
      return;
    }

    if (this.selection.selected.length === current.length) {
      this.selection.clear();
    } else {
      this.selection.clear();
      current.forEach((result) => {
        if (result.prospectId) {
          this.selection.select(result.prospectId);
        }
      });
    }
  }

  toggleRow(row: HunterResult): void {
    if (!row.prospectId) return;
    this.selection.toggle(row.prospectId);
  }

  handleMapSelect(prospectId: string): void {
    if (!prospectId) {
      return;
    }

    const match = this.visibleResults().find((item) => item.prospectId === prospectId);
    if (!match) {
      return;
    }

    if (!this.selection.isSelected(prospectId)) {
      this.selection.select(prospectId);
    }

    this.openProspect(match);
    this.scrollToProspect(prospectId);
  }

  handleMapAddToList(prospectId: string): void {
    if (!prospectId) {
      return;
    }
    const match = this.visibleResults().find((item) => item.prospectId === prospectId);
    if (match) {
      this.addProspectToCurrentList(match);
    }
  }

  trackRow = (_: number, item: HunterResult): string => item.prospectId;
  openProspect(row: HunterResult): void {
    this.activeProspect.set(row);
    if (row.prospectId) {
      this.selection.select(row.prospectId);
    }
  }

  closeProspect(): void {
    this.activeProspect.set(null);
  }

  heatmapFor(result: HunterResult): HeatmapSlot[] {
    const base = clamp(result.score / 100, 0.2, 1);
    return HEATMAP_HOURS.map((hour, index) => {
      let intensity = base;
      if (index < 2 && result.features.booking) intensity += 0.1;
      if (result.features.socialActive) intensity += 0.05;
      if (index >= 2 && result.features.mobileOk === false) intensity -= 0.15;
      if (result.features.lcpSlow) intensity -= 0.1;
      intensity = clamp(intensity, 0.1, 1);
      const label = intensity >= 0.75 ? 'Excellent' : intensity >= 0.5 ? 'Bon' : 'À tester';
      return { hour, label, intensity };
    });
  }

  scoreColor(score: number): string {
    return scoreToColor(score);
  }

  magicPick(limit = 30): void {
    const visible = this.visibleResults();
    if (visible.length === 0) {
      this.snackbar.open('Lancez une recherche avant Magic Pick.', 'Fermer', { duration: 2500 });
      return;
    }

    const grouped = new Map<string, HunterResult[]>();
    visible
      .slice()
      .sort((a, b) => b.score - a.score)
      .forEach((item) => {
        const key = (item.prospect.city ?? 'Autres').toLowerCase();
        const existing = grouped.get(key);
        if (existing) {
          existing.push(item);
        } else {
          grouped.set(key, [item]);
        }
      });

    const picks: string[] = [];
    const max = Math.min(limit, visible.length);

    while (picks.length < max) {
      let added = false;
      for (const bucket of grouped.values()) {
        const candidate = bucket.shift();
        if (!candidate) {
          continue;
        }

        if (candidate.prospectId && !picks.includes(candidate.prospectId)) {
          picks.push(candidate.prospectId);
          added = true;
          if (picks.length >= max) {
            break;
          }
        }
      }

      if (!added) {
        break;
      }
    }

    this.selection.clear();
    picks.forEach((id) => this.selection.select(id));
    this.snackbar.open(`Magic Pick sélectionne ${picks.length} prospects`, 'Fermer', {
      duration: 2800,
    });
  }

  createListFromSelection(): void {
    const ids = this.selection.selected.filter((id) => !!id);
    if (ids.length === 0) {
      this.snackbar.open('Sélectionnez au moins un prospect.', 'Fermer', { duration: 2500 });
      return;
    }

    const name = prompt('Nom de la nouvelle liste :', `Liste ${new Date().toLocaleDateString()}`);
    if (!name?.trim()) {
      return;
    }

    this.loading.set(true);
    this.subscribe(
      this.api.createList({ name: name.trim() }).pipe(takeUntilDestroyed(this.destroyRef)),
      {
        next: (res) => {
          this.addProspectsToList(res.id, ids, () => {
            this.snackbar.open('Liste créée et prospects ajoutés.', 'Fermer', { duration: 3000 });
            this.selection.clear();
            this.refreshLists();
            this.selectedListId.set(res.id);
          });
        },
        error: () => {
          this.loading.set(false);
          this.snackbar.open('Impossible de créer la liste.', 'Fermer', { duration: 3000 });
        },
      },
      'create-list',
    );
  }

  addSelectionTo(listId: string): void {
    const ids = this.selection.selected.filter((id) => !!id);
    if (ids.length === 0) {
      this.snackbar.open('Sélectionnez au moins un prospect.', 'Fermer', { duration: 2500 });
      return;
    }
    this.addProspectsToList(listId, ids, () => {
      this.snackbar.open('Prospects ajoutés à la liste.', 'Fermer', { duration: 3000 });
      this.selection.clear();
      this.refreshLists();
    });
  }

  addProspectToCurrentList(row: HunterResult): void {
    const listId = this.selectedListId();
    if (!listId) {
      this.snackbar.open('Choisissez une liste cible dans la barre d’action.', 'Fermer', {
        duration: 2800,
      });
      return;
    }

    if (!row.prospectId) return;
    this.addProspectsToList(listId, [row.prospectId], () =>
      this.snackbar.open('Prospect ajouté à la liste.', 'Fermer', { duration: 2500 }),
    );
  }

  createCadenceFromSelection(): void {
    const listId = this.selectedListId();
    if (!listId) {
      this.snackbar.open('Sélectionnez ou créez une liste avant de lancer la cadence.', 'Fermer', {
        duration: 3000,
      });
      return;
    }

    this.loading.set(true);
    this.subscribe(
      this.api.createCadenceFromList({ listId }).pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      ),
      {
        next: () => {
          this.snackbar.open('Cadence créée pour la sélection.', 'Fermer', { duration: 3200 });
        },
        error: () =>
          this.snackbar.open('Création de cadence impossible pour le moment.', 'Fermer', {
            duration: 3200,
          }),
      },
      'create-cadence',
    );
  }

  importCsv(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0);
    if (!file) {
      return;
    }
    this.loading.set(true);
    this.subscribe(
      this.api.uploadHunterCsv(file).pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      ),
      {
        next: (res) => {
          this.uploadedCsvId.set(res.csvId);
          this.csvFileName.set(file.name);
          this.filtersForm.patchValue({ source: 'csv' }, { emitEvent: false });
          this.snackbar.open('CSV importé, prêt à être exploré.', 'Fermer', { duration: 2800 });
        },
        error: () =>
          this.snackbar.open("Impossible d'importer le fichier CSV.", 'Fermer', { duration: 3200 }),
      },
      'upload-csv',
    );

    input.value = '';
  }

  clearCsv(): void {
    this.uploadedCsvId.set(null);
    this.csvFileName.set(null);
  }

  excludeProspect(row: HunterResult): void {
    if (!row.prospectId) return;
    this.selection.deselect(row.prospectId);
    this.excluded.update((current) => {
      const next = new Set(current);
      next.add(row.prospectId);
      return next;
    });
    this.snackbar.open('Prospect exclu de la sélection locale.', 'Fermer', { duration: 2200 });
  }

  toggleFavorite(row: HunterResult): void {
    if (!row.prospectId) return;
    this.favorites.update((current) => {
      const next = new Set(current);
      if (next.has(row.prospectId)) {
        next.delete(row.prospectId);
      } else {
        next.add(row.prospectId);
      }
      return next;
    });
  }

  isFavorite(row: HunterResult): boolean {
    if (!row.prospectId) return false;
    return this.favorites().has(row.prospectId);
  }

  refreshLists(): void {
    this.subscribe(
      this.api.listSummaries().pipe(takeUntilDestroyed(this.destroyRef)),
      {
        next: (lists) => {
          this.lists.set(lists);
          if (lists.every((list) => list.id !== this.selectedListId())) {
            this.selectedListId.set(null);
          }
          if (!this.selectedListId() && lists.length > 0) {
            const first = lists[0];
            if (first) {
              this.selectedListId.set(first.id);
            }
          }
        },
        error: () =>
          this.snackbar.open('Impossible de charger les listes.', 'Fermer', { duration: 2500 }),
      },
      'list-summaries',
    );
  }

  private addProspectsToList(listId: string, prospectIds: string[], onSuccess: () => void): void {
    const payload: AddToListPayload = { prospectIds };
    this.loading.set(true);
    this.subscribe(
      this.api.addToList(listId, payload).pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      ),
      {
        next: () => onSuccess(),
        error: () =>
          this.snackbar.open("Impossible d'ajouter les prospects à la liste.", 'Fermer', {
            duration: 3000,
          }),
      },
      `add-to-list-${listId}`,
    );
  }

  private scrollToProspect(prospectId: string): void {
    if (!prospectId) {
      return;
    }

    const index = this.visibleResults().findIndex((item) => item.prospectId === prospectId);
    if (index >= 0) {
      this.viewport?.scrollToIndex(index, 'smooth');
    }

    if (!this.viewport) {
      const element = document.getElementById(`prospect-row-${prospectId}`);
      element?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }

  private extractCoordinates(result: HunterResult, seed: number): { lat?: number; lng?: number } {
    const candidates: Array<{ lat?: unknown; lng?: unknown }> = [];
    const anyResult = result as unknown as Record<string, unknown>;

    if (anyResult['lat'] !== undefined || anyResult['lng'] !== undefined) {
      candidates.push({ lat: anyResult['lat'], lng: anyResult['lng'] });
    }

    if (typeof anyResult['geo'] === 'object' && anyResult['geo'] !== null) {
      candidates.push(anyResult['geo'] as { lat?: unknown; lng?: unknown });
    }

    const prospectGeo = (result.prospect as unknown as Record<string, unknown>)?.['geo'];
    if (typeof prospectGeo === 'object' && prospectGeo !== null) {
      candidates.push(prospectGeo as { lat?: unknown; lng?: unknown });
    }

    const featureGeo = (result.features as unknown as Record<string, unknown>)?.['geo'];
    if (typeof featureGeo === 'object' && featureGeo !== null) {
      candidates.push(featureGeo as { lat?: unknown; lng?: unknown });
    }

    const featureLat = (result.features as unknown as Record<string, unknown>)?.['lat'];
    const featureLng = (result.features as unknown as Record<string, unknown>)?.['lng'];
    if (featureLat !== undefined || featureLng !== undefined) {
      candidates.push({ lat: featureLat, lng: featureLng });
    }

    for (const candidate of candidates) {
      const lat = this.numberOrUndefined(candidate?.lat);
      const lng = this.numberOrUndefined(candidate?.lng);
      if (lat !== undefined && lng !== undefined) {
        return { lat, lng };
      }
    }

    const city = result.prospect.city;
    if (!city) {
      return {};
    }

    return approximateCoordinates(city, seed);
  }

  private numberOrUndefined(value: unknown): number | undefined {
    if (typeof value === 'number' && Number.isFinite(value)) {
      return value;
    }
    if (typeof value === 'string') {
      const parsed = Number.parseFloat(value);
      if (Number.isFinite(parsed)) {
        return parsed;
      }
    }
    return undefined;
  }

  private buildCriteria(form: HunterFormValue): HunterSearchCriteria {
    const filters: HunterFilters = {
      ratingMin: form.ratingMin,
      reviewsMin: form.reviewsMin,
      hasSite: form.hasSite || undefined,
      booking: form.booking || undefined,
      socialActive: form.socialActive || undefined,
    };

    const criteria: HunterSearchCriteria = {
      industry: form.industry,
      source: form.source as HunterSearchCriteria['source'],
      filters,
    };

    if (form.city) {
      const coords = approximateCoordinates(form.city, 0);
      criteria.geo = {
        lat: coords.lat,
        lng: coords.lng,
        radiusKm: form.radiusKm ?? 5,
      };
      criteria.textQuery = form.city;
    }

    const csvId = this.uploadedCsvId();
    if (csvId) {
      criteria.csvId = csvId;
    }

    return criteria;
  }

  ngOnDestroy(): void {
    this.subscriptions.clear();
  }
}
