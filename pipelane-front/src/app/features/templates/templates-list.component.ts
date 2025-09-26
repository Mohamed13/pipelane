import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AsyncPipe, JsonPipe, NgFor, NgIf } from '@angular/common';
import { ApiService } from '../../core/api.service';

@Component({
  standalone: true,
  selector: 'pl-templates-list',
  imports: [NgFor, NgIf, AsyncPipe, JsonPipe],
  template: `
  <h2>Templates</h2>
  <button (click)="refresh()">Refresh</button>
  <div *ngIf="templates | async as list">
    <div *ngFor="let t of list" style="border:1px solid #ddd; padding:.5rem; margin:.5rem 0;">
      <strong>{{t.name}}</strong> — {{t.channel}} / {{t.lang}}
      <pre class="muted">{{ t.coreSchemaJson }}</pre>
    </div>
  </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TemplatesListComponent {
  private api = inject(ApiService);
  templates = this.api.getTemplates();
  refresh(){ this.api.refreshTemplates().subscribe(()=> this.templates = this.api.getTemplates()); }
}

