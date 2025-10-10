import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';

@Component({
    standalone: true,
    selector: 'pl-kpi-card',
    imports: [CommonModule, MatCardModule],
    template: `
    <mat-card class="kpi-card">
      <div class="label">{{label}}</div>
      <div class="value">{{value | number}}</div>
      <div class="delta" [class.up]="delta >= 0" [class.down]="delta < 0" *ngIf="delta !== undefined">
        <span class="material-icons">{{ delta >= 0 ? 'trending_up' : 'trending_down' }}</span>
        {{ delta | number:'1.0-1' }}%
      </div>
    </mat-card>
  `,
    styles: [`
    .kpi-card{ padding: var(--space-5); display:flex; flex-direction:column; gap:.25rem }
    .label{ color: var(--color-muted); font-size:.9rem }
    .value{ font-size:1.8rem; font-weight:700 }
    .delta{ display:flex; align-items:center; gap:.25rem; font-weight:600 }
    .delta.up{ color:#10b981 }
    .delta.down{ color:#ef4444 }
  `],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class KpiCardComponent {
  @Input() label = '';
  @Input() value = 0;
  @Input() delta?: number;
}
