import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';

@Component({
  standalone: true,
  selector: 'pl-onboarding',
  imports: [FormsModule],
  template: `
  <h2>Onboarding</h2>
  <p class="muted">Configure channel credentials and run quick checks.</p>
  <section>
    <h3>WhatsApp</h3>
    <form (ngSubmit)="save('whatsapp')">
      <input placeholder="PhoneNumberId" [(ngModel)]="wa.phone_number_id" name="phone" />
      <input placeholder="AccessToken" [(ngModel)]="wa.access_token" name="token" />
      <input placeholder="VerifyToken" [(ngModel)]="wa.verify_token" name="verify" />
      <button type="submit">Save</button>
    </form>
  </section>
  <section>
    <h3>Email ESP</h3>
    <form (ngSubmit)="save('email')">
      <input placeholder="API key" [(ngModel)]="email.apiKey" name="apikey" />
      <input placeholder="Domain" [(ngModel)]="email.domain" name="domain" />
      <button type="submit">Save</button>
    </form>
  </section>
  <section>
    <h3>SMS</h3>
    <form (ngSubmit)="save('sms')">
      <input placeholder="API key" [(ngModel)]="sms.apiKey" name="smsapikey" />
      <button type="submit">Save</button>
    </form>
  </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OnboardingComponent {
  private api = inject(ApiService);
  wa: any = {}; email: any = {}; sms: any = {};
  tenantId?: string;
  save(channel: 'whatsapp'|'email'|'sms') {
    const body = { channel, settings: channel==='whatsapp'?this.wa: channel==='email'?this.email:this.sms };
    this.api.saveChannelSettings(body, this.tenantId).subscribe();
  }
}

