import { CommonModule } from '@angular/common';
import { AfterViewInit, ChangeDetectionStrategy, Component, effect, inject, signal, ViewChild } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { ChartConfiguration } from 'chart.js';
import { catchError, of } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

import { ApiService } from '../../core/api.service';
import { ThemeService } from '../../core/theme.service';
import { DeliveryAnalyticsResponse, DeliveryChannelBreakdown } from '../../core/models';
import { KpiCardComponent } from '../../shared/ui/kpi-card.component';
import { ChartCardComponent } from '../../shared/ui/chart-card.component';
import { RevealOnScrollDirective } from '../../shared/ui/reveal-on-scroll.directive';

interface ChannelRow {
  channel: string;
  queued: number;
  sent: number;
  delivered: number;
  opened: number;
  failed: number;
  bounced: number;
}

@Component({
  standalone: true,
  selector: 'pl-analytics-overview',
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    KpiCardComponent,
    ChartCardComponent,
    RevealOnScrollDirective,
  ],
  template: `
    <section class="grid-responsive" plRevealOnScroll>
      <pl-kpi-card label="Total Messages" [value]="totalMessages()"></pl-kpi-card>
      <pl-kpi-card label="Sent" [value]="sentCount()"></pl-kpi-card>
      <pl-kpi-card label="Delivered" [value]="deliveredCount()"></pl-kpi-card>
      <pl-kpi-card label="Opened" [value]="openedCount()"></pl-kpi-card>
      <pl-kpi-card label="Failed" [value]="failedCount()"></pl-kpi-card>
    </section>

    <section class="grid-responsive charts" plRevealOnScroll>
      <pl-chart-card title="Message Funnel" [type]="'line'" [data]="lineData" [options]="chartOptions"></pl-chart-card>
      <pl-chart-card title="Channel Performance" [type]="'bar'" [data]="barData" [options]="chartOptions"></pl-chart-card>
      <pl-chart-card title="Status Breakdown" [type]="'doughnut'" [data]="donutData" [options]="chartOptions"></pl-chart-card>
    </section>

    <section plRevealOnScroll>
      <mat-card class="surface-card">
        <div class="table-header">
          <h3>Channel Delivery Summary</h3>
        </div>
        <div class="table-wrapper">
          <table mat-table [dataSource]="table" matSort class="mat-elevation-z1">
            <ng-container matColumnDef="channel">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Channel</th>
              <td mat-cell *matCellDef="let row">{{ row.channel | titlecase }}</td>
            </ng-container>
            <ng-container matColumnDef="sent">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Sent</th>
              <td mat-cell *matCellDef="let row">{{ row.sent }}</td>
            </ng-container>
            <ng-container matColumnDef="delivered">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Delivered</th>
              <td mat-cell *matCellDef="let row">{{ row.delivered }}</td>
            </ng-container>
            <ng-container matColumnDef="opened">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Opened</th>
              <td mat-cell *matCellDef="let row">{{ row.opened }}</td>
            </ng-container>
            <ng-container matColumnDef="failed">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Failed/Bounced</th>
              <td mat-cell *matCellDef="let row">{{ row.failed + row.bounced }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="displayedColumns; sticky: true"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns" class="table-row"></tr>
          </table>
        </div>
        <mat-paginator [pageSize]="5" [pageSizeOptions]="[5, 10, 20]"></mat-paginator>
      </mat-card>
    </section>
  `,
  styles: [
    `
      .charts { margin-top: var(--space-5); }
      .table-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-3); }
      .table-wrapper { overflow-x: auto; }
      .table-row { cursor: pointer; transition: background var(--transition-fast); }
      .table-row:hover { background: var(--color-surface-alt); }
      :host ::ng-deep .mat-mdc-table .mat-mdc-row,
      :host ::ng-deep .mat-mdc-table .mat-mdc-header-row { height: 48px; }
      @media (max-width: 768px) {
        .charts { grid-template-columns: 1fr; }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnalyticsOverviewComponent implements AfterViewInit {
  private readonly api = inject(ApiService);
  private readonly theme = inject(ThemeService).theme;

  private readonly analytics$ = this.api
    .getDeliveryAnalytics()
    .pipe<DeliveryAnalyticsResponse | null>(catchError(() => of(null)));
  private readonly analytics = toSignal(this.analytics$, { initialValue: null });

  table = new MatTableDataSource<ChannelRow>([]);
  displayedColumns = ['channel', 'sent', 'delivered', 'opened', 'failed'];

  totalMessages = signal(0);
  sentCount = signal(0);
  deliveredCount = signal(0);
  openedCount = signal(0);
  failedCount = signal(0);

  lineData: ChartConfiguration['data'] = createLineData(emptyTotals());
  barData: ChartConfiguration['data'] = createBarData([]);
  donutData: ChartConfiguration['data'] = createDonutData(emptyTotals());
  chartOptions: ChartConfiguration['options'] = createChartOptions();
  private lastTotals = emptyTotals();

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor() {
    effect(() => {
      this.theme();
      this.refreshCharts();
    });

    effect(() => {
      const snapshot = this.analytics();
      if (!snapshot) {
        return;
      }
      this.applyAnalytics(snapshot);
    });
  }

  ngAfterViewInit(): void {
    this.table.paginator = this.paginator;
    this.table.sort = this.sort;
  }

  private applyAnalytics(snapshot: DeliveryAnalyticsResponse) {
    const totals = snapshot.totals ?? emptyTotals();
    this.lastTotals = totals;
    const total = Object.values(totals).reduce((acc, value) => acc + value, 0);
    this.totalMessages.set(total);
    this.sentCount.set(totals.sent);
    this.deliveredCount.set(totals.delivered);
    this.openedCount.set(totals.opened);
    this.failedCount.set(totals.failed + totals.bounced);

    const rows = (snapshot.byChannel ?? []).map(mapChannelRow);
    this.table.data = rows;
    this.refreshCharts();
  }

  private refreshCharts() {
    const totals = this.lastTotals;
    const rows = this.table.data;
    this.chartOptions = createChartOptions();
    this.lineData = createLineData(totals);
    this.barData = createBarData(rows);
    this.donutData = createDonutData(totals);
  }
}

function emptyTotals() {
  return { queued: 0, sent: 0, delivered: 0, opened: 0, failed: 0, bounced: 0 };
}

function mapChannelRow(entry: DeliveryChannelBreakdown): ChannelRow {
  return {
    channel: entry.channel,
    queued: entry.queued,
    sent: entry.sent,
    delivered: entry.delivered,
    opened: entry.opened,
    failed: entry.failed,
    bounced: entry.bounced,
  };
}

function cssVar(name: string) {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

function rgba(hex: string, alpha: number) {
  if (!hex.startsWith('#') || hex.length < 7) return hex;
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function createChartOptions(): ChartConfiguration['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        labels: { color: cssVar('--color-text-muted') },
      },
      tooltip: {
        backgroundColor: cssVar('--color-surface'),
        bodyColor: cssVar('--color-text'),
        titleColor: cssVar('--color-text'),
        borderColor: cssVar('--color-border'),
        borderWidth: 1,
        displayColors: true,
      },
    },
    scales: {
      x: { ticks: { color: cssVar('--color-text-muted') }, grid: { color: cssVar('--color-border') } },
      y: { ticks: { color: cssVar('--color-text-muted') }, grid: { color: cssVar('--color-border') } },
    },
  };
}

function createLineData(totals: { queued: number; sent: number; delivered: number; opened: number; failed: number; bounced: number }): ChartConfiguration['data'] {
  const primary = cssVar('--color-primary');
  const warn = cssVar('--color-warn');
  const labels = ['Queued', 'Sent', 'Delivered', 'Opened', 'Failed', 'Bounced'];
  const stages = [totals.queued, totals.sent, totals.delivered, totals.opened, totals.failed, totals.bounced];
  return {
    labels,
    datasets: [
      {
        data: stages,
        label: 'Pipeline',
        borderColor: primary,
        backgroundColor: rgba(primary, 0.2),
        fill: true,
        tension: 0.35,
      },
      {
        data: stages.map((value, idx) => (idx >= 4 ? value : 0)),
        label: 'Issues',
        borderColor: warn,
        backgroundColor: rgba(warn, 0.15),
        fill: true,
        tension: 0.35,
      },
    ],
  };
}

function createBarData(rows: ChannelRow[]): ChartConfiguration['data'] {
  const palette = [cssVar('--color-primary'), '#10b981', cssVar('--color-warn')];
  const labels = rows.map((r) => r.channel.toUpperCase());
  return {
    labels,
    datasets: [
      {
        data: rows.map((r) => r.sent),
        label: 'Sent',
        backgroundColor: palette[0],
      },
      {
        data: rows.map((r) => r.delivered),
        label: 'Delivered',
        backgroundColor: palette[1],
      },
      {
        data: rows.map((r) => r.failed + r.bounced),
        label: 'Failed/Bounced',
        backgroundColor: palette[2],
      },
    ],
  };
}

function createDonutData(totals: { queued: number; sent: number; delivered: number; opened: number; failed: number; bounced: number }): ChartConfiguration['data'] {
  const failedTotal = totals.failed + totals.bounced;
  return {
    labels: ['Queued', 'Sent', 'Delivered', 'Opened', 'Failed/Bounced'],
    datasets: [
      {
        data: [totals.queued, totals.sent, totals.delivered, totals.opened, failedTotal],
        backgroundColor: [
          cssVar('--color-surface-alt'),
          cssVar('--color-primary'),
          '#10b981',
          cssVar('--color-accent'),
          cssVar('--color-warn'),
        ],
        hoverOffset: 6,
      },
    ],
  };
}
