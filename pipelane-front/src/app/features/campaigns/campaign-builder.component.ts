import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatStepperModule } from '@angular/material/stepper';
import { MatTooltipModule } from '@angular/material/tooltip';
import { startWith } from 'rxjs';
import { debounceTime } from 'rxjs/operators';

import { ApiService } from '../../core/api.service';
import {
  AiSuggestFollowupRequest,
  AiSuggestFollowupResponse,
  CampaignCreatePayload,
  Channel,
  ChannelLabels,
  TemplateSummary,
} from '../../core/models';
import { SubscriptionStore } from '../../core/subscription-store';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

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
    PageHeaderComponent,
  ],
  templateUrl: './campaign-builder.component.html',
  styleUrls: ['./campaign-builder.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignBuilderComponent implements OnDestroy {
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
  followupPreview = signal<AiSuggestFollowupResponse | null>(null);
  followupLoading = signal(false);
  private readonly subscriptions = new SubscriptionStore();

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
      smartFollowupDefault: this.fb.control<boolean>(true),
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

  private get smartFollowupControl(): FormControl<boolean> {
    return this.messageGroup.get('smartFollowupDefault') as FormControl<boolean>;
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

  private loadFollowupPreview(force = false): void {
    if (!this.smartFollowupControl.value) {
      return;
    }

    if (this.followupLoading() && !force) {
      return;
    }

    const payload = this.buildFollowupPreviewRequest();

    this.followupPreview.set(null);
    this.followupLoading.set(true);
    this.subscriptions.set(
      'followup-preview',
      this.api
        .suggestSmartFollowup(payload)
        .pipe(takeUntilDestroyed())
        .subscribe({
          next: (preview) => {
            this.followupPreview.set(preview);
            this.followupLoading.set(false);
          },
          error: () => {
            this.followupPreview.set(null);
            this.followupLoading.set(false);
          },
        }),
    );
  }

  private buildFollowupPreviewRequest(): AiSuggestFollowupRequest {
    const template = this.selectedTemplate();
    const timezone =
      typeof Intl !== 'undefined' && Intl.DateTimeFormat
        ? (Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'UTC')
        : 'UTC';

    return {
      channel: this.primaryChannelSelection(),
      timezone,
      lastInteractionAt: new Date().toISOString(),
      read: true,
      language: template?.lang ?? 'fr',
      historySnippet: this.sampleHistorySnippet(template?.name ?? null),
    };
  }

  private sampleHistorySnippet(templateName: string | null): string {
    const label = templateName ?? 'votre message';
    return `Client: Merci pour votre dernier message !\nVous (${label}): Ravi d'avoir de vos nouvelles, je vous prépare un rappel personnalisé.`;
  }

  constructor() {
    this.subscriptions.track(
      this.api.getTemplates().subscribe({
        next: (templates) => this.templates.set(templates ?? []),
      }),
    );

    this.subscriptions.track(
      this.audienceGroup.valueChanges
        .pipe(debounceTime(120), takeUntilDestroyed())
        .subscribe(() => this.generateSegmentJson()),
    );

    const primaryControl = this.messageGroup.get('primaryChannel');
    if (primaryControl) {
      this.subscriptions.track(
        primaryControl.valueChanges.pipe(takeUntilDestroyed()).subscribe((primary) => {
          this.pruneFallback(primary as Channel);
          if (this.smartFollowupControl.value) {
            this.loadFollowupPreview(true);
          }
        }),
      );
    }

    const templateControl = this.messageGroup.get('templateId');
    if (templateControl) {
      this.subscriptions.track(
        templateControl.valueChanges.pipe(debounceTime(120), takeUntilDestroyed()).subscribe(() => {
          if (this.smartFollowupControl.value) {
            this.loadFollowupPreview();
          }
        }),
      );
    }

    this.subscriptions.track(
      this.smartFollowupControl.valueChanges
        .pipe(startWith(this.smartFollowupControl.value), takeUntilDestroyed())
        .subscribe((enabled) => {
          if (enabled) {
            this.loadFollowupPreview(true);
          } else {
            this.followupPreview.set(null);
          }
        }),
    );

    this.generateSegmentJson();
    const segmentControl = this.audienceGroup.get('segmentJson');
    const initialSegment =
      typeof segmentControl?.value === 'string' ? segmentControl.value.trim() : '';
    this.updatePreview(initialSegment && initialSegment.length ? initialSegment : '{}');
  }

  toggleTag(tag: string): void {
    const control = this.audienceGroup.get('tags');
    if (!control) {
      return;
    }
    const current = new Set(control.value ?? []);
    current.has(tag) ? current.delete(tag) : current.add(tag);
    control.setValue(Array.from(current));
    this.generateSegmentJson();
  }

  tagSelected(tag: string): boolean {
    return (this.audienceGroup.get('tags')?.value ?? []).includes(tag);
  }

  toggleChannel(channel: Channel): void {
    const control = this.audienceGroup.get('channels');
    if (!control) {
      return;
    }
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
    this.subscriptions.set(
      'create-campaign',
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
      }),
    );
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
    const control = this.audienceGroup.get('segmentJson');
    if (!control) {
      return;
    }
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
    this.subscriptions.set(
      'preview-followups',
      this.api.previewFollowups(segmentJson).subscribe({
        next: (res) => {
          this.previewLoading.set(false);
          this.previewCount.set(res?.count ?? 0);
        },
        error: () => {
          this.previewLoading.set(false);
          this.previewCount.set(null);
        },
      }),
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.clear();
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
