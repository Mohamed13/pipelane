import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ApiService } from '../../core/api.service';
import { Channel, ChannelLabels, ChannelSettingsPayload } from '../../core/models';
import { SubscriptionStore } from '../../core/subscription-store';

type ChannelStatus = 'connected' | 'pending';

@Component({
  standalone: true,
  selector: 'pl-onboarding',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    MatDividerModule,
    MatTooltipModule,
  ],
  templateUrl: './onboarding.component.html',
  styleUrls: ['./onboarding.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OnboardingComponent implements OnDestroy {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackbar = inject(MatSnackBar);
  private readonly subscriptions = new SubscriptionStore();

  readonly ChannelLabels = ChannelLabels;
  saving = signal(false);
  testingEmail = signal(false);
  whatsappSecretVisible = signal(false);
  emailSecretVisible = signal(false);
  smsSecretVisible = signal(false);

  whatsappForm: FormGroup = this.fb.group({
    phone_number_id: this.fb.control<string>('', Validators.required),
    access_token: this.fb.control<string>('', Validators.required),
    verify_token: this.fb.control<string>('', Validators.required),
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

  whatsappStatus = computed<ChannelStatus>(() =>
    this.isConnected(this.whatsappForm) ? 'connected' : 'pending',
  );
  emailStatus = computed<ChannelStatus>(() =>
    this.isConnected(this.emailForm) ? 'connected' : 'pending',
  );
  smsStatus = computed<ChannelStatus>(() =>
    this.isConnected(this.smsForm) ? 'connected' : 'pending',
  );

  save(channel: Channel): void {
    const form = this.getForm(channel);
    if (form.invalid) {
      form.markAllAsTouched();
      return;
    }

    const payload: ChannelSettingsPayload = {
      channel,
      settings: this.toSettingsRecord(form.value as Record<string, unknown>),
    };

    this.saving.set(true);
    this.subscriptions.subscribe(
      this.api.saveChannelSettings(payload),
      {
        next: () => {
          this.saving.set(false);
          this.snackbar.open(`${ChannelLabels[channel]} settings saved`, 'Close', {
            duration: 3000,
          });
        },
        error: () => {
          this.saving.set(false);
          this.snackbar.open(`Failed to save ${ChannelLabels[channel]} settings`, 'Dismiss', {
            duration: 4000,
          });
        },
      },
      `save-${channel}`,
    );
  }

  sendQuickTest(channel: Channel): void {
    if (channel === 'email') {
      this.sendTestEmail();
      return;
    }

    this.snackbar.open(`Test messages for ${ChannelLabels[channel]} coming soon.`, 'Close', {
      duration: 3000,
    });
  }

  toggleSecret(channel: Channel): void {
    switch (channel) {
      case 'whatsapp':
        this.whatsappSecretVisible.update((v) => !v);
        break;
      case 'email':
        this.emailSecretVisible.update((v) => !v);
        break;
      case 'sms':
        this.smsSecretVisible.update((v) => !v);
        break;
    }
  }

  private sendTestEmail(): void {
    if (this.emailTestForm.invalid) {
      this.emailTestForm.markAllAsTouched();
      return;
    }

    const { contactId, message } = this.emailTestForm.value as {
      contactId: string;
      message: string;
    };
    if (!contactId) {
      return;
    }

    this.testingEmail.set(true);
    this.subscriptions.subscribe(
      this.api.sendMessage({
        contactId: contactId.trim(),
        channel: 'email',
        type: 'text',
        text: (message ?? 'This is a test email from Pipelane.').trim(),
      }),
      {
        next: () => {
          this.testingEmail.set(false);
          this.snackbar.open('Test email sent', 'Close', { duration: 4000 });
        },
        error: () => {
          this.testingEmail.set(false);
          this.snackbar.open('Unable to send test email', 'Dismiss', { duration: 4000 });
        },
      },
      'send-test-email',
    );
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

  private isConnected(form: FormGroup): boolean {
    return Object.values(form.controls).every((control) => {
      const value = control.value;
      return typeof value === 'string' ? value.trim().length > 0 : !!value;
    });
  }

  private toSettingsRecord(value: Record<string, unknown>): Record<string, string> {
    return Object.entries(value)
      .filter(([, val]) => typeof val === 'string' && val.trim() !== '')
      .reduce<Record<string, string>>((acc, [key, val]) => {
        acc[key] = (val as string).trim();
        return acc;
      }, {});
  }

  ngOnDestroy(): void {
    this.subscriptions.clear();
  }
}
