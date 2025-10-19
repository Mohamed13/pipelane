import { CommonModule, JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal, computed } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { ApiService } from '../../core/api.service';
import { TemplateSummary } from '../../core/models';

@Component({
  standalone: true,
  selector: 'pl-templates-list',
  imports: [
    CommonModule,
    JsonPipe,
    MatCardModule,
    MatExpansionModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <mat-card class="surface-card">
      <div class="header">
        <div>
          <h2>Templates</h2>
          <p class="body-text-muted">Manage messaging templates across channels.</p>
        </div>
        <button mat-stroked-button color="primary" (click)="refresh()" [disabled]="loading()">
          <mat-icon>refresh</mat-icon>
          Refresh
        </button>
      </div>

      <mat-progress-spinner
        *ngIf="loading()"
        diameter="40"
        mode="indeterminate"
      ></mat-progress-spinner>

      <ng-container *ngIf="!loading()">
        <mat-accordion *ngIf="templates().length; else empty">
          <mat-expansion-panel *ngFor="let template of templates()" class="template-panel">
            <mat-expansion-panel-header>
              <mat-panel-title>
                {{ template.name }}
              </mat-panel-title>
              <mat-panel-description>
                {{ template.channel | titlecase }} · {{ template.lang.toUpperCase() }}
              </mat-panel-description>
            </mat-expansion-panel-header>
            <div class="chip-row">
              <mat-chip color="primary" selected>{{ template.channel }}</mat-chip>
              <mat-chip>{{ template.lang }}</mat-chip>
              <mat-chip *ngIf="template.isActive" color="accent" selected>Active</mat-chip>
              <mat-chip *ngIf="template.category">{{ template.category }}</mat-chip>
            </div>
            <pre>{{ template.coreSchemaJson | json }}</pre>
            <p class="body-text-muted small">
              Updated {{ template.updatedAtUtc | date: 'medium' }}
            </p>
          </mat-expansion-panel>
        </mat-accordion>
      </ng-container>
    </mat-card>

    <ng-template #empty>
      <div class="empty">
        <mat-icon>inventory_2</mat-icon>
        <p>No templates found.</p>
      </div>
    </ng-template>
  `,
  styles: [
    `
      .header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: var(--space-4);
      }
      .template-panel {
        border-radius: var(--radius-md);
        margin-bottom: var(--space-3);
      }
      .chip-row {
        display: flex;
        gap: var(--space-2);
        margin-bottom: var(--space-3);
        flex-wrap: wrap;
      }
      pre {
        background: var(--color-surface-alt);
        padding: var(--space-3);
        border-radius: var(--radius-sm);
        overflow: auto;
      }
      .empty {
        text-align: center;
        color: var(--color-text-muted);
        padding: var(--space-6);
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
      mat-progress-spinner {
        margin: var(--space-5) auto;
        display: block;
      }
      .small {
        font-size: 0.85rem;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TemplatesListComponent {
  private readonly api = inject(ApiService);

  private readonly templatesSignal = signal<TemplateSummary[]>([]);
  loading = signal<boolean>(true);

  templates = computed(() => this.templatesSignal());

  constructor() {
    this.loadTemplates();
  }

  refresh(): void {
    this.loading.set(true);
    this.api.refreshTemplates().subscribe({
      next: () => this.loadTemplates(false),
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private loadTemplates(showSpinner = true): void {
    if (showSpinner) this.loading.set(true);
    this.api.getTemplates().subscribe({
      next: (templates) => {
        this.templatesSignal.set(templates ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.templatesSignal.set([]);
        this.loading.set(false);
      },
    });
  }
}
