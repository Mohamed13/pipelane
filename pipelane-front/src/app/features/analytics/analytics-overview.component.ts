import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AsyncPipe, NgFor } from '@angular/common';
import { ApiService } from '../../core/api.service';

@Component({
  standalone: true,
  selector: 'pl-analytics-overview',
  imports: [AsyncPipe, NgFor],
  template: `
  <h2>Analytics</h2>
  <div *ngIf="data | async as d">
    <div>Total messages: <strong>{{ d.total }}</strong></div>
    <ul>
      <li *ngFor="let ch of d.byChannel">{{ch.channel}} — {{ch.count}}</li>
    </ul>
  </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnalyticsOverviewComponent {
  private api = inject(ApiService);
  data = this.api.getAnalyticsOverview();
}

