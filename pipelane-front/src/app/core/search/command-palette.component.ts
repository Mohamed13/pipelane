import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { Subscription, catchError, combineLatest, finalize, of, startWith, switchMap } from 'rxjs';

import { CommandItem, CommandType, SearchFilters, SearchService } from './search.service';
import { TranslatePipe } from '../i18n/translate.pipe';

type PaletteRow =
  | { kind: 'header'; label: string; type: CommandType }
  | { kind: 'item'; item: CommandItem; index: number };

@Component({
  standalone: true,
  selector: 'pl-command-palette',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    ScrollingModule,
    TranslatePipe,
  ],
  templateUrl: './command-palette.component.html',
  styleUrls: ['./command-palette.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CommandPaletteComponent implements AfterViewInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly search = inject(SearchService);
  private readonly router = inject(Router);
  private readonly dialogRef = inject(MatDialogRef<CommandPaletteComponent>);
  private readonly searchSubscription: Subscription;

  @ViewChild('queryInput') private queryInput?: ElementRef<HTMLInputElement>;
  @ViewChild(CdkVirtualScrollViewport) private viewport?: CdkVirtualScrollViewport;

  readonly queryControl = new FormControl<string>('', { nonNullable: true });
  readonly filter = signal<'all' | CommandType>('all');
  readonly items = signal<CommandItem[]>([]);
  readonly activeIndex = signal<number>(-1);
  readonly loading = signal(false);
  readonly recents = toSignal(this.search.recent$, { initialValue: [] as string[] });
  readonly queryValue = toSignal(
    this.queryControl.valueChanges.pipe(startWith(this.queryControl.getRawValue())),
    { initialValue: this.queryControl.getRawValue() },
  );

  readonly filterOptions: Array<{ labelKey: string; value: 'all' | CommandType }> = [
    { labelKey: 'search.filters.all', value: 'all' },
    { labelKey: 'search.filters.prospects', value: 'prospect' },
    { labelKey: 'search.filters.conversations', value: 'conversation' },
    { labelKey: 'search.filters.campaigns', value: 'campaign' },
    { labelKey: 'search.filters.lists', value: 'list' },
  ];

  readonly grouped = computed(() => {
    const groups = new Map<CommandType, Array<CommandItem & { index: number }>>();
    this.items().forEach((item, index) => {
      const bucket = groups.get(item.type) ?? [];
      bucket.push({ ...item, index });
      groups.set(item.type, bucket);
    });
    return Array.from(groups.entries()).map(([type, collection]) => ({
      type,
      label: this.groupLabel(type),
      items: collection,
    }));
  });

  readonly viewRows = computed<PaletteRow[]>(() => {
    const rows: PaletteRow[] = [];
    const groups = this.grouped();
    groups.forEach((group) => {
      rows.push({ kind: 'header', label: group.label, type: group.type });
      group.items.forEach((item) => rows.push({ kind: 'item', item, index: item.index }));
    });
    return rows;
  });

  readonly hasQuery = computed(() => this.queryValue().trim().length > 0);

  constructor() {
    const term$ = this.queryControl.valueChanges.pipe(startWith(this.queryControl.getRawValue()));
    const filter$ = toObservable(this.filter);

    this.searchSubscription = combineLatest([term$, filter$])
      .pipe(
        switchMap(([term, filter]) => {
          const trimmed = term.trim();
          const filters = filter === 'all' ? undefined : ({ type: filter } as SearchFilters);
          if (!trimmed) {
            this.loading.set(false);
            this.items.set([]);
            this.activeIndex.set(-1);
            return of<CommandItem[]>([]);
          }
          this.loading.set(true);
          return this.search.search(trimmed, filters).pipe(
            catchError(() => of([])),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((results) => {
        this.items.set(results);
        this.activeIndex.set(results.length ? 0 : -1);
        if (results.length && this.viewport) {
          queueMicrotask(() => this.viewport?.scrollToIndex(0));
        }
      });
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => this.queryInput?.nativeElement.select());
  }

  iconFor(type: CommandType): string {
    switch (type) {
      case 'prospect':
        return 'person';
      case 'conversation':
        return 'forum';
      case 'campaign':
        return 'flag';
      case 'list':
        return 'list_alt';
      default:
        return 'search';
    }
  }

  optionId(index: number): string {
    return `command-option-${index}`;
  }

  activeOptionId(): string | null {
    const index = this.activeIndex();
    return index >= 0 ? this.optionId(index) : null;
  }

  setFilter(value: 'all' | CommandType): void {
    if (this.filter() === value) {
      return;
    }
    this.filter.set(value);
  }

  hoverIndex(index: number): void {
    this.activeIndex.set(index);
  }

  selectIndex(index: number): void {
    const items = this.items();
    if (index < 0 || index >= items.length) {
      return;
    }
    const item = items[index];
    if (!item) {
      return;
    }
    const term = this.queryControl.getRawValue().trim();
    if (term) {
      this.search.recordRecent(term);
    }
    if (item.route) {
      void this.router.navigateByUrl(item.route);
    }
    this.dialogRef.close(item);
  }

  selectActive(): void {
    const index = this.activeIndex();
    if (index >= 0) {
      this.selectIndex(index);
    }
  }

  onRecentSelect(term: string): void {
    this.queryControl.setValue(term);
    this.queryInput?.nativeElement.focus();
  }

  clearRecents(): void {
    this.search.clearRecents();
  }

  trackRow(_: number, row: PaletteRow): string {
    return row.kind === 'header' ? `header:${row.label}` : row.item.id;
  }

  trackFilter(_: number, option: { value: 'all' | CommandType }): string {
    return option.value;
  }

  trackRecent(_: number, value: string): string {
    return value;
  }

  @HostListener('keydown', ['$event'])
  handleKeydown(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.moveActive(1);
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.moveActive(-1);
      return;
    }
    if (event.key === 'Enter') {
      if (this.activeIndex() >= 0) {
        event.preventDefault();
        this.selectActive();
      }
      return;
    }
  }

  close(): void {
    this.dialogRef.close();
  }

  private moveActive(offset: number): void {
    const total = this.items().length;
    if (!total) {
      return;
    }
    const next = (this.activeIndex() + offset + total) % total;
    this.activeIndex.set(next);
    if (this.viewport) {
      queueMicrotask(() => this.viewport?.scrollToIndex(next));
    }
  }

  private groupLabel(type: CommandType): string {
    switch (type) {
      case 'prospect':
        return 'search.groups.prospects';
      case 'conversation':
        return 'search.groups.conversations';
      case 'campaign':
        return 'search.groups.campaigns';
      case 'list':
        return 'search.groups.lists';
      default:
        return 'search.groups.default';
    }
  }
}
