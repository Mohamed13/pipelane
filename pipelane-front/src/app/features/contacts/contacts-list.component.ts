import { CommonModule, DatePipe } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginator, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

import { ApiService } from '../../core/api.service';
import { Contact, PagedContactsResponse } from '../../core/models';

@Component({
  standalone: true,
  selector: 'pl-contacts-list',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DatePipe,
    MatTableModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatSortModule,
    MatProgressBarModule,
  ],
  template: `
    <mat-card class="surface-card">
      <div class="header">
        <div>
          <h2>Contacts</h2>
          <p class="body-text-muted">Search and browse contacts across channels.</p>
        </div>
        <button mat-stroked-button color="primary" (click)="reload()">
          <mat-icon>refresh</mat-icon>
          Refresh
        </button>
      </div>

      <mat-form-field appearance="outline" class="search-field">
        <mat-icon matPrefix>search</mat-icon>
        <input
          matInput
          [formControl]="searchControl"
          placeholder="Search by name, phone or email"
        />
        <button mat-icon-button matSuffix (click)="clearSearch()" *ngIf="searchControl.value">
          <mat-icon>close</mat-icon>
        </button>
      </mat-form-field>

      <mat-progress-bar *ngIf="isLoading()" mode="indeterminate"></mat-progress-bar>

      <div class="table-wrapper" *ngIf="dataSource.data.length > 0; else emptyState">
        <table mat-table [dataSource]="dataSource" matSort class="mat-elevation-z1">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th>
            <td mat-cell *matCellDef="let contact">
              <div class="contact-name">{{ formatName(contact) || 'Unknown contact' }}</div>
              <div class="body-text-muted small">{{ contact.phone || contact.email }}</div>
            </td>
          </ng-container>

          <ng-container matColumnDef="lang">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Language</th>
            <td mat-cell *matCellDef="let contact">{{ contact.lang || 'n/a' }}</td>
          </ng-container>

          <ng-container matColumnDef="created">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>Created</th>
            <td mat-cell *matCellDef="let contact">{{ contact.createdAt | date: 'mediumDate' }}</td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let contact" class="actions">
              <button mat-icon-button color="primary" (click)="open(contact)">
                <mat-icon>chat</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns; sticky: true"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns" class="table-row"></tr>
        </table>
        <mat-paginator
          [length]="total()"
          [pageSize]="pageSize"
          [pageSizeOptions]="[10, 20, 50]"
          (page)="onPage($event)"
        >
        </mat-paginator>
      </div>
      <ng-template #emptyState>
        <div class="empty">
          <mat-icon>people_outline</mat-icon>
          <p>No contacts found. Try adjusting your search.</p>
        </div>
      </ng-template>
    </mat-card>
  `,
  styles: [
    `
      .header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-3);
        margin-bottom: var(--space-4);
      }
      .search-field {
        width: 100%;
        margin-bottom: var(--space-4);
      }
      .table-wrapper {
        overflow-x: auto;
      }
      .table-row {
        cursor: pointer;
        transition: background var(--transition-fast);
      }
      .table-row:hover {
        background: var(--color-surface-alt);
      }
      .actions {
        text-align: right;
      }
      .empty {
        text-align: center;
        padding: var(--space-6) 0;
        color: var(--color-text-muted);
        display: flex;
        flex-direction: column;
        gap: var(--space-3);
        align-items: center;
      }
      .empty mat-icon {
        font-size: 3rem;
        height: auto;
        width: auto;
      }
      .contact-name {
        font-weight: 600;
      }
      .small {
        font-size: 0.85rem;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactsListComponent implements AfterViewInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  searchControl = new FormControl<string>('');
  dataSource = new MatTableDataSource<Contact>([]);
  displayedColumns: (keyof Contact | 'actions' | 'created' | 'name')[] = [
    'name',
    'lang',
    'created',
    'actions',
  ];
  total = signal(0);
  isLoading = signal(false);
  pageIndex = 0;
  pageSize = 10;

  @ViewChild(MatPaginator) paginator?: MatPaginator;
  @ViewChild(MatSort) sort?: MatSort;

  constructor() {
    this.loadContacts('');
    this.searchControl.valueChanges
      ?.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((value) => {
        this.pageIndex = 0;
        this.loadContacts(value ?? '');
      });
  }

  ngAfterViewInit(): void {
    if (this.paginator) {
      this.dataSource.paginator = this.paginator;
    }
    if (this.sort) {
      this.dataSource.sort = this.sort;
    }
  }

  reload(): void {
    this.loadContacts(this.searchControl.value ?? '');
  }

  clearSearch(): void {
    this.searchControl.setValue('');
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadContacts(this.searchControl.value ?? '');
  }

  open(contact: Contact): void {
    this.router.navigate(['/conversations', contact.id]);
  }

  formatName(contact: Contact): string | undefined {
    const parts = [contact.firstName, contact.lastName].filter(Boolean);
    return parts.length ? parts.join(' ') : undefined;
  }

  private loadContacts(query: string): void {
    this.isLoading.set(true);
    this.api.searchContacts(query, this.pageIndex + 1, this.pageSize).subscribe({
      next: (response: PagedContactsResponse) => {
        this.dataSource.data = response.items;
        this.total.set(response.total);
        this.isLoading.set(false);
      },
      error: () => {
        this.dataSource.data = [];
        this.total.set(0);
        this.isLoading.set(false);
      },
    });
  }
}
