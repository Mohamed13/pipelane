import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatStepperModule } from '@angular/material/stepper';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonToggleModule } from '@angular/material/button-toggle';

import { ApiService } from '../../core/api.service';
import { CampaignCreatePayload, Channel, ChannelLabels, TemplateSummary } from '../../core/models';

interface ActivityOption {
  label: string;
  value: number;
}

@Component({
  standalone: true,
  selector: 'pl-campaign-builder',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatStepperModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatChipsModule,
    MatSnackBarModule,
    MatDividerModule,
    MatSlideToggleModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatButtonToggleModule,
  ],
  templateUrl: './campaign-builder.component.html',
  styleUrls: ['./campaign-builder.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignBuilderComponent {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackbar = inject(MatSnackBar);

  readonly ChannelLabels = ChannelLabels;
  readonly channels: Channel[] = ['whatsapp', 'email', 'sms'];
  readonly tagLibrary = ['VIP', 'Trial', 'Newsletter', 'Churn risk', 'Welcome', 'Product update'];
  readonly activityOptions: ActivityOption[] = [
    { label: 'Last 7 days', value: 7 },
    { label: 'Last 30 days', value: 30 },
    { label: 'Last 90 days', value: 90 },
    { label: 'Last 180 days', value: 180 },
  ];

  templates = signal<TemplateSummary[]>([]);
  creating = signal(false);
  previewLoading = signal(false);
  previewCount = signal<number | null>(null);

  readonly form = this.fb.group({
    audience: this.fb.group({
      tags: this.fb.control<string[]>([]),
      channels: this.fb.control<Channel[]>(['whatsapp']),
      lastActivity: this.fb.control<number>(30),
      consentOnly: this.fb.control(true),
      segmentJson: this.fb.control<string>('{}', Validators.required),
    }),
    message: this.fb.group({
      name: this.fb.control<string>('Untitled campaign', [
        Validators.required,
        Validators.minLength(3),
      ]),
      templateId: this.fb.control<string>('', Validators.required),
      primaryChannel: this.fb.control<Channel>('whatsapp', Validators.required),
      fallbackOrder: this.fb.control<Channel[]>([]),
      smartFollowupDefault: this.fb.control<boolean>(false),
    }),
    schedule: this.fb.group({
      scheduledDate: this.fb.control<Date | null>(null),
      scheduledTime: this.fb.control<string>('09:00'),
      batchSize: this.fb.control<number | null>(null, Validators.min(1)),
      throttleEnabled: this.fb.control<boolean>(false),
      throttleRate: this.fb.control<number>(200),
      respectQuietHours: this.fb.control<boolean>(true),
      quietHoursStart: this.fb.control<string>('21:00'),
      quietHoursEnd: this.fb.control<string>('08:00'),
    }),
  });

  get audienceGroup(): FormGroup {
    return this.form.get('audience') as FormGroup;
  }

  get messageGroup(): FormGroup {
    return this.form.get('message') as FormGroup;
  }

  get scheduleGroup(): FormGroup {
    return this.form.get('schedule') as FormGroup;
  }

  selectedTemplate = computed<TemplateSummary | null>(() => {
    const id = this.messageGroup.get('templateId')?.value;
    if (!id) {
      return null;
    }
    return this.templates().find((template) => template.id === id) ?? null;
  });

  primaryChannelSelection = computed<Channel>(() => {
    const value = this.messageGroup.get('primaryChannel')?.value as Channel | null;
    return (value ?? 'whatsapp') as Channel;
  });

  summary = computed<CampaignCreatePayload | null>(() => {
    if (this.form.invalid) {
      return null;
    }
    return this.buildPayload();
  });

  constructor() {
    this.api.getTemplates().subscribe({
      next: (templates) => this.templates.set(templates ?? []),
    });

    this.audienceGroup.valueChanges
      .pipe(debounceTime(120), takeUntilDestroyed())
      .subscribe(() => this.generateSegmentJson());

    this.messageGroup
      .get('primaryChannel')
      ?.valueChanges.pipe(takeUntilDestroyed())
      .subscribe((primary) => this.pruneFallback(primary as Channel));

    this.generateSegmentJson();
    this.updatePreview(this.audienceGroup.get('segmentJson')!.value ?? '{}');
  }

  toggleTag(tag: string): void {
    const control = this.audienceGroup.get('tags')!;
    const current = new Set(control.value ?? []);
    current.has(tag) ? current.delete(tag) : current.add(tag);
    control.setValue(Array.from(current));
    this.generateSegmentJson();
  }

  tagSelected(tag: string): boolean {
    return (this.audienceGroup.get('tags')?.value ?? []).includes(tag);
  }

  toggleChannel(channel: Channel): void {
    const control = this.audienceGroup.get('channels')!;
    const current = new Set(control.value ?? []);
    if (current.has(channel)) {
      if (current.size > 1) {
        current.delete(channel);
      }
    } else {
      current.add(channel);
    }
    control.setValue(Array.from(current));
    this.generateSegmentJson();
  }

  channelSelected(channel: Channel): boolean {
    return (this.audienceGroup.get('channels')?.value ?? []).includes(channel);
  }

  setLastActivity(days: number): void {
    this.audienceGroup.get('lastActivity')?.setValue(days);
    this.generateSegmentJson();
  }

  toggleConsentOnly(checked: boolean): void {
    this.audienceGroup.get('consentOnly')?.setValue(checked);
    this.generateSegmentJson();
  }

  fallbackOptions(): Channel[] {
    const primary = this.messageGroup.get('primaryChannel')?.value as Channel;
    return this.channels.filter((channel) => channel !== primary);
  }

  onFallbackSelectionChange(channels: Channel[]): void {
    this.messageGroup.get('fallbackOrder')?.setValue(channels);
  }

  createCampaign(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const payload = this.buildPayload();
    if (!payload) {
      return;
    }
    this.creating.set(true);
    this.api.createCampaign(payload).subscribe({
      next: (res) => {
        this.creating.set(false);
        this.snackbar.open(`Campaign created (ID: ${res.id})`, 'Close', { duration: 4000 });
        this.form.reset({
          audience: {
            tags: [],
            channels: ['whatsapp'],
            lastActivity: 30,
            consentOnly: true,
            segmentJson: '{}',
          },
          message: {
            name: 'Untitled campaign',
            templateId: '',
            primaryChannel: 'whatsapp',
            fallbackOrder: [],
            smartFollowupDefault: false,
          },
          schedule: {
            scheduledDate: null,
            scheduledTime: '09:00',
            batchSize: null,
            throttleEnabled: false,
            throttleRate: 200,
            respectQuietHours: true,
            quietHoursStart: '21:00',
            quietHoursEnd: '08:00',
          },
        });
        this.previewCount.set(null);
        this.generateSegmentJson();
      },
      error: () => {
        this.creating.set(false);
        this.snackbar.open('Failed to create campaign', 'Dismiss', { duration: 4000 });
      },
    });
  }

  private generateSegmentJson(): void {
    const tags = this.audienceGroup.get('tags')?.value ?? [];
    const channels = this.audienceGroup.get('channels')?.value ?? ['whatsapp'];
    const lastActivity = this.audienceGroup.get('lastActivity')?.value ?? 30;
    const consentOnly = this.audienceGroup.get('consentOnly')?.value ?? true;

    const segment = {
      tags,
      channels,
      lastActivityDays: lastActivity,
      consentOnly,
    };

    const nextJson = JSON.stringify(segment);
    const control = this.audienceGroup.get('segmentJson')!;
    if (control.value !== nextJson) {
      control.setValue(nextJson, { emitEvent: false });
      this.updatePreview(nextJson);
    }
  }

  private pruneFallback(primary: Channel): void {
    const currentPrimary = (primary ?? 'whatsapp') as Channel;
    const fallback = (this.messageGroup.get('fallbackOrder')?.value ?? []) as Channel[];
    if (fallback.includes(currentPrimary)) {
      const filtered = fallback.filter((channel: Channel) => channel !== currentPrimary);
      this.messageGroup.get('fallbackOrder')?.setValue(filtered);
    }
  }

  private buildPayload(): CampaignCreatePayload | null {
    if (this.form.invalid) {
      return null;
    }
    const audience = this.audienceGroup.value;
    const message = this.messageGroup.value;
    const schedule = this.scheduleGroup.value;

    return {
      name: message.name!,
      primaryChannel: message.primaryChannel as Channel,
      fallbackOrderJson: (message.fallbackOrder ?? []).length
        ? JSON.stringify(message.fallbackOrder)
        : null,
      templateId: message.templateId!,
      segmentJson: audience.segmentJson ?? '{}',
      scheduledAtUtc: combineDateTime(
        schedule.scheduledDate ?? null,
        schedule.scheduledTime ?? null,
      ),
      batchSize: schedule.batchSize ? Number(schedule.batchSize) : null,
      smartFollowupDefault: message.smartFollowupDefault ?? false,
    };
  }

  channelLabel(channel: Channel | null): string {
    const value = (channel ?? 'whatsapp') as Channel;
    return ChannelLabels[value];
  }

  private updatePreview(segmentJson: string): void {
    this.previewLoading.set(true);
    this.api.previewFollowups(segmentJson).subscribe({
      next: (res) => {
        this.previewLoading.set(false);
        this.previewCount.set(res?.count ?? 0);
      },
      error: () => {
        this.previewLoading.set(false);
        this.previewCount.set(null);
      },
    });
  }
}

function combineDateTime(date: Date | null, time: string | null): string | null {
  if (!date) {
    return null;
  }
  const base = new Date(date);
  const [hours, minutes] = (time ?? '').split(':');
  const h = hours !== undefined && hours !== '' ? Number(hours) : 0;
  const m = minutes !== undefined && minutes !== '' ? Number(minutes) : 0;
  const iso = new Date(Date.UTC(base.getFullYear(), base.getMonth(), base.getDate(), h, m, 0, 0));
  return iso.toISOString();
}
