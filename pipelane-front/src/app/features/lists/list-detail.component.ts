import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ApiService } from '../../core/api.service';
import { ProspectListResponse } from '../../core/models';

@Component({
  standalone: true,
  selector: 'app-list-detail',
  templateUrl: './list-detail.component.html',
  styleUrls: ['./list-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
  ],
})
export class ListDetailComponent {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly snackbar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly list = signal<ProspectListResponse | null>(null);
  readonly displayedColumns = ['company', 'score', 'city', 'added'];

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) {
        this.load(id);
      }
    });
  }

  createCadence(): void {
    const current = this.list();
    if (!current) return;

    this.loading.set(true);
    this.api
      .createCadenceFromList({ listId: current.id })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loading.set(false);
          this.snackbar.open('Cadence créée depuis la liste.', 'Fermer', { duration: 3000 });
        },
        error: () => {
          this.loading.set(false);
          this.snackbar.open('Impossible de créer la cadence depuis cette liste.', 'Fermer', {
            duration: 3200,
          });
        },
      });
  }

  private load(id: string): void {
    this.loading.set(true);
    this.api
      .getList(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.list.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.snackbar.open('Impossible de charger la liste.', 'Fermer', { duration: 3000 });
        },
      });
  }
}
