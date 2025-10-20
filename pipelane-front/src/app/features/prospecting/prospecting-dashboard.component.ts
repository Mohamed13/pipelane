import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { RouterModule } from '@angular/router';
import {
  ApexAxisChartSeries,
  ApexChart,
  ApexDataLabels,
  ApexStroke,
  ApexTooltip,
  ApexXAxis,
  NgApexchartsModule,
} from 'ng-apexcharts';
import { forkJoin } from 'rxjs';
import { finalize } from 'rxjs/operators';

import { ApiService } from '../../core/api.service';
import {
  ProspectingAnalyticsResponse,
  ProspectingCampaign,
  ProspectingSequence,
} from '../../core/models';

@Component({
  selector: 'pl-prospecting-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatSnackBarModule,
    NgApexchartsModule,
  ],
  templateUrl: './prospecting-dashboard.component.html',
  styleUrls: ['./prospecting-dashboard.component.scss'],
})
export class ProspectingDashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly snackbar = inject(MatSnackBar);

  readonly loading = signal(false);
  readonly analytics = signal<ProspectingAnalyticsResponse | null>(null);
  readonly campaigns = signal<ProspectingCampaign[]>([]);
  readonly sequences = signal<ProspectingSequence[]>([]);

  chartSeries: ApexAxisChartSeries = [];
  chartOptions: Partial<{
    chart: ApexChart;
    xaxis: ApexXAxis;
    dataLabels: ApexDataLabels;
    stroke: ApexStroke;
    tooltip: ApexTooltip;
  }> = {
    chart: {
      type: 'area',
      height: 260,
      toolbar: { show: false },
    },
    dataLabels: { enabled: false },
    stroke: { curve: 'smooth' },
    tooltip: { shared: true },
    xaxis: { type: 'datetime' },
  };

  ngOnInit(): void {
    this.load();
  }

  refresh(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    forkJoin({
      analytics: this.api.getProspectingAnalytics(),
      campaigns: this.api.getProspectingCampaigns(),
      sequences: this.api.getProspectingSequences(),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ analytics, campaigns, sequences }) => {
          this.analytics.set(analytics);
          this.campaigns.set(campaigns);
          this.sequences.set(sequences);
          this.updateChart(analytics);
        },
        error: () => {
          this.snackbar?.open('Unable to load prospecting analytics', 'Dismiss', {
            duration: 6000,
          });
        },
      });
  }

  private updateChart(analytics: ProspectingAnalyticsResponse): void {
    const series = analytics.dailySeries ?? [];
    this.chartSeries = [
      {
        name: 'Sent',
        data: series.map((point) => ({ x: point.date, y: point.sent })),
      },
      {
        name: 'Opened',
        data: series.map((point) => ({ x: point.date, y: point.opened })),
      },
      {
        name: 'Replies',
        data: series.map((point) => ({ x: point.date, y: point.replies })),
      },
    ];
  }
}

