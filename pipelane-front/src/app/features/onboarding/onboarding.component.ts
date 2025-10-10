import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';

import { ApiService } from '../../core/api.service';
import { Channel, ChannelLabels, ChannelSettingsPayload } from '../../core/models';

@Component({
  standalone: true,
  selector: 'pl-onboarding',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    MatDividerModule,
  ],
  template: `
    <div class="grid-responsive">
      <mat-card class="surface-card">
        <header>
          <h2>WhatsApp</h2>
          <p class="body-text-muted">Configure Meta WhatsApp Cloud credentials.</p>
        </header>
        <form [formGroup]="whatsappForm" (ngSubmit)="save('whatsapp')" class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Phone number ID</mat-label>
            <input matInput formControlName="phone_number_id" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Access token</mat-label>
            <input matInput formControlName="access_token" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Verify token</mat-label>
            <input matInput formControlName="verify_token" />
          </mat-form-field>
          <button mat-raised-button color="primary" type="submit" [disabled]="whatsappForm.invalid || saving()">
            <mat-icon>save</mat-icon>
            Save WhatsApp settings
          </button>
        </form>
      </mat-card>

      <mat-card class="surface-card">
        <header>
          <h2>Email ESP</h2>
          <p class="body-text-muted">Setup your transactional email provider.</p>
        </header>
        <form [formGroup]="emailForm" (ngSubmit)="save('email')" class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>API key</mat-label>
            <input matInput formControlName="apiKey" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Domain</mat-label>
            <input matInput formControlName="domain" />
          </mat-form-field>
          <button mat-raised-button color="primary" type="submit" [disabled]="emailForm.invalid || saving()">
            <mat-icon>save</mat-icon>
            Save email settings
          </button>
        </form>
        <mat-divider></mat-divider>
        <section class="test-block">
          <h3>Test delivery</h3>
          <p class="body-text-muted">Send a quick test message to an existing contact.</p>
          <form [formGroup]="emailTestForm" (ngSubmit)="sendTestEmail()" class="form-grid">
            <mat-form-field appearance="outline">
              <mat-label>Contact ID</mat-label>
              <input matInput formControlName="contactId" placeholder="Contact GUID" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Message</mat-label>
              <textarea matInput rows="3" formControlName="message"></textarea>
            </mat-form-field>
            <button mat-raised-button color="accent" type="submit" [disabled]="emailTestForm.invalid || testingEmail()">
              <mat-icon>email</mat-icon>
              Send test email
            </button>
          </form>
        </section>
      </mat-card>

      <mat-card class="surface-card">
        <header>
          <h2>SMS</h2>
          <p class="body-text-muted">Link your SMS provider credentials.</p>
        </header>
        <form [formGroup]="smsForm" (ngSubmit)="save('sms')" class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>API key</mat-label>
            <input matInput formControlName="apiKey" />
          </mat-form-field>
          <button mat-raised-button color="primary" type="submit" [disabled]="smsForm.invalid || saving()">
            <mat-icon>save</mat-icon>
            Save SMS settings
          </button>
        </form>
      </mat-card>
    </div>
  `,
  styles: [
    `
      header { margin-bottom: var(--space-3); }
      .form-grid { display:flex; flex-direction:column; gap:var(--space-3); }
      mat-card { display:flex; flex-direction:column; gap:var(--space-3); }
      .test-block { display:flex; flex-direction:column; gap:var(--space-3); }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OnboardingComponent {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackbar = inject(MatSnackBar);

  saving = signal(false);
  testingEmail = signal(false);

  whatsappForm: FormGroup = this.fb.group({
    phone_number_id: this.fb.control<string>(''),
    access_token: this.fb.control<string>(''),
    verify_token: this.fb.control<string>(''),
  });

  emailForm: FormGroup = this.fb.group({
    apiKey: this.fb.control<string>('', Validators.required),
    domain: this.fb.control<string>('', Validators.required),
  });

  emailTestForm: FormGroup = this.fb.group({
    contactId: this.fb.control<string>('', Validators.required),
    message: this.fb.control<string>('This is a test email from Pipelane.'),
  });

  smsForm: FormGroup = this.fb.group({
    apiKey: this.fb.control<string>('', Validators.required),
  });

  save(channel: Channel): void {
    const form = this.getForm(channel);
    if (form.invalid) {
      form.markAllAsTouched();
      return;
    }

    const value = form.value as Record<string, unknown>;
    const payload: ChannelSettingsPayload = {
      channel,
      settings: this.toSettingsRecord(value),
    };

    this.saving.set(true);
    this.api.saveChannelSettings(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackbar.open(`${ChannelLabels[channel]} settings saved`, 'Close', { duration: 3000 });
      },
      error: () => {
        this.saving.set(false);
        this.snackbar.open(`Failed to save ${ChannelLabels[channel]} settings`, 'Dismiss', { duration: 4000 });
      },
    });
  }

  sendTestEmail(): void {
    if (this.emailTestForm.invalid) {
      this.emailTestForm.markAllAsTouched();
      return;
    }

    const { contactId, message } = this.emailTestForm.value as { contactId: string; message: string };
    if (!contactId)
      return;

    this.testingEmail.set(true);
    this.api
      .sendMessage({
        contactId: contactId.trim(),
        channel: 'email',
        type: 'text',
        text: (message ?? 'This is a test email from Pipelane.').trim(),
      })
      .subscribe({
        next: () => {
          this.testingEmail.set(false);
          this.snackbar.open('Test email sent', 'Close', { duration: 4000 });
        },
        error: () => {
          this.testingEmail.set(false);
          this.snackbar.open('Unable to send test email', 'Dismiss', { duration: 4000 });
        },
      });
  }

  private getForm(channel: Channel): FormGroup {
    switch (channel) {
      case 'whatsapp':
        return this.whatsappForm;
      case 'email':
        return this.emailForm;
      case 'sms':
      default:
        return this.smsForm;
    }
  }

  private toSettingsRecord(value: Record<string, unknown>): Record<string, string> {
    return Object.entries(value)
      .filter(([, val]) => typeof val === 'string' && val !== '')
      .reduce<Record<string, string>>((acc, [key, val]) => {
        acc[key] = val as string;
        return acc;
      }, {});
  }
}
