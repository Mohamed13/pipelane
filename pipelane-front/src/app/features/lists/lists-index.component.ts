import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import type { Observable, PartialObserver, Subscription } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { ListSummary } from '../../core/models';
import { SubscriptionStore } from '../../core/subscription-store';

@Component({
  standalone: true,
  selector: 'app-lists-index',
  templateUrl: './lists-index.component.html',
  styleUrls: ['./lists-index.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
})
export class ListsIndexComponent implements OnDestroy {
  private readonly api = inject(ApiService);
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

  readonly loading = signal(true);
  readonly lists = signal<ListSummary[]>([]);
  readonly displayedColumns = ['name', 'count', 'created', 'updated', 'actions'];

  constructor() {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.subscribe(
      this.api.listSummaries().pipe(takeUntilDestroyed(this.destroyRef)),
      {
        next: (res) => {
          this.lists.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.snackbar.open('Impossible de charger les listes.', 'Fermer', { duration: 3000 });
        },
      },
      'list-summaries',
    );
  }

  createList(): void {
    const name = prompt('Nom de la nouvelle liste :', `Liste ${new Date().toLocaleDateString()}`);
    if (!name?.trim()) {
      return;
    }

    this.loading.set(true);
    this.subscribe(
      this.api.createList({ name: name.trim() }).pipe(takeUntilDestroyed(this.destroyRef)),
      {
        next: () => {
          this.snackbar.open('Liste créée.', 'Fermer', { duration: 2500 });
          this.refresh();
        },
        error: () => {
          this.loading.set(false);
          this.snackbar.open('Impossible de créer la liste.', 'Fermer', { duration: 3000 });
        },
      },
      'create-list',
    );
  }

  renameList(list: ListSummary): void {
    const nextName = prompt('Nouveau nom de liste :', list.name ?? '');
    const trimmed = nextName?.trim();
    const current = list.name?.trim() ?? '';
    if (!trimmed || trimmed === current) {
      return;
    }

    this.loading.set(true);
    this.subscribe(
      this.api.renameList(list.id, { name: trimmed }).pipe(takeUntilDestroyed(this.destroyRef)),
      {
        next: () => {
          this.snackbar.open('Liste renommée.', 'Fermer', { duration: 2500 });
          this.refresh();
        },
        error: () => {
          this.loading.set(false);
          this.snackbar.open('Impossible de renommer la liste.', 'Fermer', { duration: 3000 });
        },
      },
      `rename-list-${list.id}`,
    );
  }

  deleteList(list: ListSummary): void {
    const confirmDelete = confirm(
      `Supprimer la liste "${list.name || 'Sans titre'}" ? Cette action retire les associations locales.`,
    );
    if (!confirmDelete) {
      return;
    }

    this.loading.set(true);
    this.subscribe(
      this.api.deleteList(list.id).pipe(takeUntilDestroyed(this.destroyRef)),
      {
        next: () => {
          this.snackbar.open('Liste supprimée.', 'Fermer', { duration: 2500 });
          this.refresh();
        },
        error: () => {
          this.loading.set(false);
          this.snackbar.open('Impossible de supprimer la liste.', 'Fermer', { duration: 3000 });
        },
      },
      `delete-list-${list.id}`,
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.clear();
  }
}
