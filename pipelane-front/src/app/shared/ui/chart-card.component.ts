import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';

@Component({
    standalone: true,
    selector: 'pl-chart-card',
    imports: [NgChartsModule, MatCardModule],
    template: `
    <mat-card class="chart-card">
      <div class="header">
        <h3>{{title}}</h3>
      </div>
      <div class="body">
        <canvas baseChart
          [type]="type"
          [data]="data"
          [options]="options">
        </canvas>
      </div>
    </mat-card>
  `,
    styles: [`
    .chart-card{ padding: var(--space-4); }
    .header{ display:flex; align-items:center; justify-content:space-between; margin-bottom: var(--space-3) }
    h3{ margin:0; font-weight:600 }
  `],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChartCardComponent {
  @Input() title = '';
  @Input() type: ChartConfiguration['type'] = 'line';
  @Input() data!: ChartConfiguration['data'];
  @Input() options: ChartConfiguration['options'] = { responsive: true, maintainAspectRatio: false };
}

