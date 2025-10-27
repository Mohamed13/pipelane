import { CommonModule, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  SimpleChanges,
  computed,
  signal,
} from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  NgApexchartsModule,
  ApexAxisChartSeries,
  ApexChart,
  ApexFill,
  ApexStroke,
} from 'ng-apexcharts';

export interface KpiSparklineConfig {
  data: number[];
  categories?: string[];
  color?: string;
}

@Component({
  standalone: true,
  selector: 'pl-kpi-card',
  imports: [
    CommonModule,
    DecimalPipe,
    MatCardModule,
    MatTooltipModule,
    MatIconModule,
    NgApexchartsModule,
  ],
  template: `
    <mat-card
      class="kpi-card glass"
      [ngClass]="{ 'kpi-card--loading': loading }"
      [matTooltip]="tooltip || ''"
    >
      <div class="kpi-card__top">
        <div class="kpi-card__icon material-symbols-rounded" aria-hidden="true">{{ icon }}</div>
        <div class="kpi-card__meta">
          <span class="kpi-card__label">{{ label }}</span>
          <span class="kpi-card__caption" *ngIf="caption">{{ caption }}</span>
        </div>
      </div>
      <div class="kpi-card__value" [attr.aria-live]="loading ? 'polite' : 'off'">
        <span class="kpi-card__prefix" *ngIf="prefix">{{ prefix }}</span>
        <span>{{ formattedValue() }}</span>
        <span class="kpi-card__suffix" *ngIf="suffix">{{ suffix }}</span>
      </div>
      <div class="kpi-card__delta" *ngIf="delta !== undefined">
        <mat-icon aria-hidden="true" [ngClass]="{ up: delta >= 0, down: delta < 0 }">
          {{ delta >= 0 ? 'arrow_upward' : 'arrow_downward' }}
        </mat-icon>
        <span [ngClass]="{ up: delta >= 0, down: delta < 0 }">{{ delta | number: '1.0-1' }}%</span>
        <span class="kpi-card__delta-label" *ngIf="deltaLabel">{{ deltaLabel }}</span>
      </div>
      <div class="kpi-card__sparkline" *ngIf="sparkline">
        <apx-chart
          [series]="sparklineSeries()"
          [chart]="sparklineChart"
          [stroke]="sparklineStroke"
          [fill]="sparklineFill"
          [colors]="[sparkline?.color ?? 'var(--color-primary)']"
          [xaxis]="{ categories: sparkline?.categories ?? [] }"
        ></apx-chart>
      </div>
    </mat-card>
  `,
  styles: [
    `
      .kpi-card {
        position: relative;
        display: flex;
        flex-direction: column;
        gap: var(--space-3);
        padding: var(--space-4);
        border-radius: var(--radius-lg);
        border: 1px solid rgba(117, 240, 255, 0.12);
        min-height: 180px;
      }

      .kpi-card__top {
        display: flex;
        align-items: center;
        gap: var(--space-3);
      }

      .kpi-card__icon {
        width: 44px;
        height: 44px;
        display: grid;
        place-items: center;
        border-radius: var(--radius-md);
        background: rgba(117, 240, 255, 0.12);
        color: var(--color-primary);
        font-size: 24px;
      }

      .kpi-card__meta {
        display: flex;
        flex-direction: column;
        gap: 0.2rem;
      }

      .kpi-card__label {
        font-size: 0.9rem;
        letter-spacing: 0.04em;
        text-transform: uppercase;
        color: var(--color-text-muted);
      }

      .kpi-card__caption {
        font-size: 0.85rem;
        color: var(--color-text-muted);
      }

      .kpi-card__value {
        font-size: clamp(1.8rem, 1.4rem + 1vw, 2.4rem);
        font-weight: 600;
        display: flex;
        align-items: baseline;
        gap: 0.35rem;
        letter-spacing: 0.01em;
      }

      .kpi-card__prefix,
      .kpi-card__suffix {
        font-size: 0.95rem;
        color: var(--color-text-muted);
      }

      .kpi-card__delta {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        font-weight: 600;
        font-size: 0.95rem;
      }

      .kpi-card__delta mat-icon {
        font-size: 18px;
      }

      .kpi-card__delta mat-icon.up,
      .kpi-card__delta span.up {
        color: #4ade80;
      }

      .kpi-card__delta mat-icon.down,
      .kpi-card__delta span.down {
        color: #f87171;
      }

      .kpi-card__delta-label {
        color: var(--color-text-muted);
        font-weight: 400;
      }

      .kpi-card__sparkline {
        height: 60px;
        width: 100%;
      }

      .kpi-card--loading {
        opacity: 0.6;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiCardComponent implements OnChanges {
  @Input() label = '';
  @Input() caption?: string;
  @Input() value: number | null = null;
  @Input() prefix?: string;
  @Input() suffix?: string;
  @Input() valueFormat = '1.0-0';
  @Input() icon = 'insights';
  @Input() tooltip?: string;
  @Input() delta?: number;
  @Input() deltaLabel?: string;
  @Input() sparkline?: KpiSparklineConfig | null;
  @Input() loading = false;

  private readonly decimal = new DecimalPipe('en-US');

  readonly sparklineChart: ApexChart = {
    type: 'area',
    sparkline: { enabled: true },
    animations: { enabled: true },
  };

  readonly sparklineStroke: ApexStroke = {
    width: 2,
    curve: 'smooth',
  };

  readonly sparklineFill: ApexFill = {
    type: 'gradient',
    gradient: {
      opacityFrom: 0.35,
      opacityTo: 0.05,
    },
  };

  formattedValue = computed(() => {
    if (this.value === null || this.value === undefined) {
      return '—';
    }
    return this.decimal.transform(this.value, this.valueFormat) ?? `${this.value}`;
  });

  sparklineSeries = signal<ApexAxisChartSeries>([{ name: 'trend', data: [] }]);

  ngOnChanges(_changes: SimpleChanges): void {
    if (this.sparkline) {
      this.sparklineSeries.set([{ name: 'trend', data: this.sparkline.data }]);
    }
  }
}
