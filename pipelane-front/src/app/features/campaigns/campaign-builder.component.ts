import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { inject } from '@angular/core';

@Component({
  standalone: true,
  selector: 'pl-campaign-builder',
  imports: [FormsModule],
  template: `
  <h2>Campaign Builder</h2>
  <form (ngSubmit)="create()">
    <label>Primary channel</label>
    <select [(ngModel)]="primary" name="primary">
      <option value="whatsapp">WhatsApp</option>
      <option value="email">Email</option>
      <option value="sms">SMS</option>
    </select>
    <label>Template name</label>
    <input [(ngModel)]="templateName" name="templateName" />
    <label>Schedule (UTC)</label>
    <input type="datetime-local" [(ngModel)]="scheduled" name="scheduled" />
    <button type="submit">Create</button>
  </form>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CampaignBuilderComponent {
  private api = inject(ApiService);
  primary: 'whatsapp'|'email'|'sms' = 'whatsapp';
  templateName = '';
  scheduled = '';
  create(){
    const body = {
      name: 'New Campaign',
      primaryChannel: this.primary,
      templateId: '00000000-0000-0000-0000-000000000000',
      segmentJson: '{}',
      scheduledAtUtc: this.scheduled? new Date(this.scheduled).toISOString(): null
    };
    this.api.createCampaign(body).subscribe();
  }
}
