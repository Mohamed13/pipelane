import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  computed,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NgApexchartsModule } from 'ng-apexcharts';
import { ApexAxisChartSeries, ApexOptions, ApexNonAxisChartSeries } from 'ng-apexcharts';

interface ChartCardEmptyState {
  title: string;
  message?: string;
  ctaLabel?: string;
  ctaHref?: string;
}

export interface ChartCardConfig {
  title: string;
  subtitle?: string;
  helpText?: string;
  series: ApexAxisChartSeries | ApexNonAxisChartSeries;
  options: Partial<ApexOptions>;
  height?: number;
  loading?: boolean;
  emptyState?: ChartCardEmptyState | null;
  compact?: boolean;
}

@Component({
  standalone: true,
  selector: 'pl-chart-card',
  imports: [
    CommonModule,
    MatCardModule,
    NgApexchartsModule,
    MatButtonModule,
    MatTooltipModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <mat-card class="chart-card surface-card" [class.chart-card--compact]="config?.compact">
      <div class="chart-card__header">
        <div>
          <h3>{{ config?.title }}</h3>
          <p *ngIf="config?.subtitle" class="muted">{{ config?.subtitle }}</p>
        </div>
        <button
          *ngIf="config?.helpText"
          mat-icon-button
          matTooltip="{{ config?.helpText }}"
          aria-label="Chart help"
          class="chart-card__help"
        >
          <mat-icon aria-hidden="true">info</mat-icon>
        </button>
      </div>

      <div class="chart-card__body" [style.min-height.px]="computedHeight()">
        <ng-container *ngIf="config?.loading; else resolved">
          <div class="chart-card__placeholder glass" aria-busy="true">
            <mat-spinner diameter="48"></mat-spinner>
            <p>Fetching insights…</p>
          </div>
        </ng-container>
        <ng-template #resolved>
          <ng-container *ngIf="config as cfg; else emptyTpl">
            <ng-container *ngIf="!isEmpty(); else emptyTpl">
              <div class="chart-card__inner">
                <apx-chart
                  class="chart"
                  [series]="cfg.series"
                  [chart]="cfg.options?.chart"
                  [xaxis]="cfg.options?.xaxis"
                  [yaxis]="cfg.options?.yaxis"
                  [dataLabels]="cfg.options?.dataLabels"
                  [stroke]="cfg.options?.stroke"
                  [tooltip]="cfg.options?.tooltip"
                  [legend]="cfg.options?.legend"
                  [fill]="cfg.options?.fill"
                  [grid]="cfg.options?.grid"
                  [plotOptions]="cfg.options?.plotOptions"
                  [colors]="cfg.options?.colors"
                  [labels]="cfg.options?.labels"
                  [responsive]="cfg.options?.responsive"
                  [theme]="cfg.options?.theme"
                  [markers]="cfg.options?.markers"
                  [states]="cfg.options?.states"
                  [noData]="cfg.options?.noData"
                  [annotations]="cfg.options?.annotations"
                  [title]="cfg.options?.title"
                  [subtitle]="cfg.options?.subtitle"
                  [width]="cfg.options?.chart?.width"
                  [height]="cfg.height ?? computedHeight()"
                ></apx-chart>
              </div>
            </ng-container>
          </ng-container>
        </ng-template>
      </div>

      <ng-template #emptyTpl>
        <div class="chart-card__placeholder glass">
          <div class="empty-icon">
            <mat-icon aria-hidden="true">radar</mat-icon>
          </div>
          <h4>{{ config?.emptyState?.title ?? 'No data yet' }}</h4>
          <p *ngIf="config?.emptyState?.message" class="muted">{{ config?.emptyState?.message }}</p>
          <a
            *ngIf="config?.emptyState?.ctaHref && config?.emptyState?.ctaLabel"
            [href]="config?.emptyState?.ctaHref"
            target="_blank"
            rel="noopener"
            mat-stroked-button
            color="primary"
          >
            {{ config?.emptyState?.ctaLabel }}
          </a>
        </div>
      </ng-template>
    </mat-card>
  `,
  styles: [
    `
      .chart-card {
        display: flex;
        flex-direction: column;
        gap: var(--space-4);
        background: rgba(16, 24, 38, 0.72);
      }

      .chart-card--compact {
        gap: var(--space-3);
      }

      .chart-card__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-3);
      }

      .chart-card__header h3 {
        margin: 0;
        font-weight: 600;
        letter-spacing: 0.02em;
      }

      .muted {
        color: var(--color-text-muted);
        margin: 0;
      }

      .chart-card__body {
        position: relative;
        width: 100%;
      }

      .chart-card__inner {
        width: 100%;
      }

      .chart-card__placeholder {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: var(--space-3);
        padding: var(--space-5);
        border-radius: var(--radius-lg);
        text-align: center;
        color: var(--color-text-muted);
        min-height: 220px;
      }

      .chart-card__placeholder mat-icon {
        font-size: 32px;
        opacity: 0.7;
      }

      .chart-card__help {
        border-radius: var(--radius-pill);
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChartCardComponent implements OnChanges {
  private readonly defaultHeight = 320;

  @Input({ required: true }) config?: ChartCardConfig | null;

  private readonly empty = computed(() => {
    const cfg = this.config;
    if (!cfg) {
      return true;
    }
    if (cfg.loading) {
      return false;
    }
    const series = cfg.series as ApexAxisChartSeries | ApexNonAxisChartSeries;
    if (!Array.isArray(series) || series.length === 0) {
      return true;
    }
    // Non-axis charts provide number arrays
    if (typeof series[0] === 'number') {
      return (series as ApexNonAxisChartSeries).every((value) => value === 0);
    }
    return (series as ApexAxisChartSeries).every((entry) =>
      Array.isArray(entry.data) ? entry.data.length === 0 : !entry.data,
    );
  });

  computedHeight = signal(this.defaultHeight);

  ngOnChanges(): void {
    if (this.config?.height) {
      this.computedHeight.set(this.config.height);
    } else {
      this.computedHeight.set(this.defaultHeight);
    }
  }

  isEmpty(): boolean {
    return this.empty();
  }
}
