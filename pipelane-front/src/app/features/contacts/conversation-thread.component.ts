import { CommonModule, DatePipe, NgIf } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime } from 'rxjs/operators';

import { ApiService } from '../../core/api.service';
import {
  Channel,
  ChannelLabels,
  ConversationResponse,
  Message,
  MessageStatus,
  SendMessageResponse,
  AiGenerateMessageRequest,
  AiGenerateMessageResponse,
  AiClassifyReplyResponse,
  FollowupProposalPreview,
} from '../../core/models';
import { PolicyService } from '../../core/policy.service';

type ComposerMode = 'text' | 'template';
type QuickActionKey = 'send-test' | 'import-contacts' | 'open-onboarding';

interface MessageStatSummary {
  total: number;
  incoming: number;
  outgoing: number;
  delivered: number;
  failed: number;
  lastInboundAt?: string;
  lastOutboundAt?: string;
}

interface RecentEvent {
  id: string;
  status: MessageStatus;
  channel: Channel;
  createdAt: string;
  direction: 'in' | 'out';
  provider?: string | null;
}

interface ProviderBadge {
  label: string;
  className: string;
}

interface PendingMessageTracking {
  providerMessageId: string | null;
  sentAtIso: string;
  messageId?: string;
}

@Component({
  standalone: true,
  selector: 'pl-conversation-thread',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DatePipe,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatDividerModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatButtonToggleModule,
    MatMenuModule,
    MatSlideToggleModule,
    MatSnackBarModule,
    NgIf,
  ],
  templateUrl: './conversation-thread.component.html',
  styleUrls: ['./conversation-thread.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConversationThreadComponent {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly policy = inject(PolicyService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly snackbar = inject(MatSnackBar);

  @ViewChild('textComposer') private textComposer?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('templateComposer') private templateComposer?: ElementRef<
    HTMLInputElement | HTMLTextAreaElement
  >;

  private readonly contactIdParam = this.route.snapshot.paramMap.get('contactId');
  readonly contactId = this.contactIdParam ?? '';

  conversation = signal<ConversationResponse | null>(null);
  sending = signal(false);
  composerMode = signal<ComposerMode>('text');
  infoPanelOpen = signal(true);

  textControl = new FormControl<string>('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(2)],
  });

  templateControl = new FormControl<string>('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(3)],
  });

  private readonly pitchStorageKey = 'pipelane_pitch';
  private readonly followupStorageKey = `pipelane_relance_${this.contactId}`;
  private readonly initialFollowupState = this.readFollowupState();

  pitchControl = new FormControl<string>(this.readPitch(), {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(5)],
  });

  aiMessagePreview = signal<AiGenerateMessageResponse | null>(null);
  aiGenerating = signal(false);
  classifying = signal(false);
  classification = signal<AiClassifyReplyResponse | null>(null);
  smartFollowupEnabled = signal(this.initialFollowupState.enabled);
  followupPreview = signal<FollowupProposalPreview | null>(this.initialFollowupState.preview);
  followupHistorySnippet = signal<string>(this.initialFollowupState.history);
  followupLoading = signal(false);

  private readonly followupWatcher = effect(
    () => {
      const enabled = this.smartFollowupEnabled();
      const preview = this.followupPreview();
      const snippet = this.followupHistorySnippet();
      if (!enabled) {
        this.persistFollowupState(false, null, '');
        return;
      }
      if (!preview && !this.followupLoading()) {
        this.requestSmartFollowup();
        return;
      }
      if (preview) {
        this.persistFollowupState(true, preview, snippet);
      }
    },
    { allowSignalWrites: true },
  );
  readonly ChannelLabels = ChannelLabels;
  readonly templateVariables = [
    '{{firstName}}',
    '{{company}}',
    '{{campaignName}}',
    '{{unsubscribeUrl}}',
  ];

  private pollingTimer: ReturnType<typeof setInterval> | null = null;
  private pollingMessageId: string | null = null;
  private pendingFetchTimeout: ReturnType<typeof setTimeout> | null = null;
  private awaitingTerminal = false;
  private pendingAutoPoll = false;
  private pendingTracking: PendingMessageTracking | null = null;

  onGenerateAiMessage(): void {
    if (this.aiGenerating()) {
      return;
    }
    const payload = this.buildAiMessagePayload();
    if (!payload) {
      return;
    }
    this.aiGenerating.set(true);
    this.api.generateAiMessage(payload).subscribe({
      next: (response) => {
        this.aiGenerating.set(false);
        this.aiMessagePreview.set(response);
      },
      error: () => {
        this.aiGenerating.set(false);
        this.aiMessagePreview.set(null);
      },
    });
  }

  onClassifyReply(): void {
    if (this.classifying()) {
      return;
    }
    const latestInbound = this.latestInboundMessage();
    if (!latestInbound) {
      return;
    }
    const text = this.renderPayload(latestInbound);
    this.classifying.set(true);
    this.api
      .classifyAiReply({ text, language: this.primaryChannel() === 'email' ? 'fr' : 'en' })
      .subscribe({
        next: (res) => {
          this.classification.set(res);
          this.classifying.set(false);
        },
        error: () => {
          this.classifying.set(false);
        },
      });
  }

  onToggleSmartFollowup(checked: boolean): void {
    this.smartFollowupEnabled.set(checked);
    if (!checked) {
      this.followupPreview.set(null);
    }
  }

  onValidateFollowup(): void {
    const preview = this.followupPreview();
    const conversationId = this.conversation()?.conversationId;
    if (!preview || !preview.proposalId || !conversationId) {
      this.followupPreview.set(null);
      this.followupHistorySnippet.set('');
      this.requestSmartFollowup();
      return;
    }
    this.followupLoading.set(true);
    this.api
      .validateFollowup({ conversationId, proposalId: preview.proposalId })
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: () => {
          this.followupLoading.set(false);
          this.smartFollowupEnabled.set(false);
          this.followupPreview.set(null);
          this.followupHistorySnippet.set('');
          this.snackbar.open('Relance programmée', 'Fermer', { duration: 4000 });
          this.fetchConversation({ skipSendingReset: true });
        },
        error: () => {
          this.followupLoading.set(false);
        },
      });
  }

  onModifyFollowup(): void {
    const preview = this.followupPreview();
    if (!preview) {
      return;
    }
    this.composerMode.set('text');
    this.textControl.setValue(preview.previewText);
  }

  onDeferFollowup(): void {
    const preview = this.followupPreview();
    if (!preview) {
      return;
    }
    const next = new Date(preview.scheduledAtIso);
    next.setDate(next.getDate() + 1);
    const updated: FollowupProposalPreview = {
      ...preview,
      scheduledAtIso: next.toISOString(),
    };
    this.followupPreview.set(updated);
  }

  onStopFollowup(): void {
    this.smartFollowupEnabled.set(false);
    this.followupPreview.set(null);
    this.followupHistorySnippet.set('');
  }

  sendGeneratedMessage(): void {
    const preview = this.aiMessagePreview();
    if (!preview) {
      return;
    }
    this.textControl.setValue(preview.text);
    this.composerMode.set('text');
    this.aiMessagePreview.set(null);
  }

  clearGeneratedMessage(): void {
    this.aiMessagePreview.set(null);
  }

  canText = computed(() => {
    const convo = this.conversation();
    if (!convo?.messages?.length) {
      return false;
    }
    const lastInbound = [...convo.messages]
      .filter((m) => m.direction === 'in')
      .slice(-1)[0]?.createdAt;
    return this.policy.isWhatsAppTextAllowed(lastInbound ?? null);
  });

  primaryChannel = computed<Channel | null>(() => {
    const first = this.conversation()?.messages?.[0];
    return first ? first.channel : null;
  });

  primaryChannelLabel = computed(() => {
    const channel = this.primaryChannel();
    return channel ? ChannelLabels[channel] : 'Unknown channel';
  });

  stats = computed<MessageStatSummary>(() => {
    const messages = this.conversation()?.messages ?? [];
    if (!messages.length) {
      return { total: 0, incoming: 0, outgoing: 0, delivered: 0, failed: 0 };
    }
    return messages.reduce<MessageStatSummary>(
      (acc, message) => {
        acc.total += 1;
        if (message.direction === 'in') {
          acc.incoming += 1;
          acc.lastInboundAt = message.createdAt;
        } else {
          acc.outgoing += 1;
          acc.lastOutboundAt = message.createdAt;
        }
        if (message.status === 'delivered' || message.status === 'opened') {
          acc.delivered += 1;
        }
        if (message.status === 'failed' || message.status === 'bounced') {
          acc.failed += 1;
        }
        return acc;
      },
      { total: 0, incoming: 0, outgoing: 0, delivered: 0, failed: 0 },
    );
  });

  hasInboundMessages = computed(() => {
    const messages = this.conversation()?.messages ?? [];
    return messages.some((message) => message.direction === 'in');
  });

  providersUsed = computed<ProviderBadge[]>(() => {
    const providers = new Set(
      (this.conversation()?.messages ?? []).map((m) => m.provider ?? '').filter(Boolean),
    );
    return Array.from(providers).map((provider) => this.toProviderBadge(provider));
  });

  recentEvents = computed<RecentEvent[]>(() => {
    return [...(this.conversation()?.messages ?? [])]
      .slice(-6)
      .reverse()
      .map((message) => ({
        id: message.id,
        status: message.status,
        channel: message.channel,
        createdAt: message.createdAt,
        direction: message.direction,
        provider: message.provider,
      }));
  });

  quickActions = [
    {
      key: 'send-test' as QuickActionKey,
      icon: 'send',
      label: 'Send test',
      tooltip: 'Send yourself a template to verify delivery',
    },
    {
      key: 'import-contacts' as QuickActionKey,
      icon: 'file_upload',
      label: 'Import contacts',
      tooltip: 'Jump to the contacts importer',
    },
    {
      key: 'open-onboarding' as QuickActionKey,
      icon: 'settings_account_box',
      label: 'Channel settings',
      tooltip: 'Review your channel credentials',
    },
  ];

  constructor() {
    this.destroyRef.onDestroy(() => this.stopPolling());
    if (!this.contactId) {
      Promise.resolve().then(() => this.router.navigate(['/contacts']));
      return;
    }
    this.fetchConversation();

    effect(
      () => {
        const canSendText = this.canText();
        if (!canSendText && this.composerMode() === 'text') {
          this.composerMode.set('template');
        }
      },
      { allowSignalWrites: true },
    );

    this.textControl.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      if (this.composerMode() !== 'text') {
        this.composerMode.set('text');
      }
    });

    this.pitchControl.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.savePitch(value));
  }

  onComposerModeChange(mode: ComposerMode): void {
    if (mode === 'text' && !this.canText()) {
      this.composerMode.set('template');
      return;
    }
    this.composerMode.set(mode);
    setTimeout(() => {
      if (mode === 'text') {
        this.textComposer?.nativeElement.focus();
      } else {
        this.templateComposer?.nativeElement.focus();
      }
    });
  }

  toggleInfoPanel(): void {
    this.infoPanelOpen.update((open) => !open);
  }

  triggerQuickAction(action: QuickActionKey): void {
    switch (action) {
      case 'send-test':
        this.onComposerModeChange('template');
        if (!this.templateControl.value) {
          this.templateControl.setValue('sample_followup');
        }
        break;
      case 'import-contacts':
        this.router.navigate(['/contacts'], { queryParams: { view: 'import' } });
        break;
      case 'open-onboarding':
        this.router.navigate(['/onboarding']);
        break;
    }
  }

  sendText(): void {
    if (this.textControl.invalid) {
      this.textControl.markAsTouched();
      return;
    }
    if (!this.contactId) {
      return;
    }
    const tracking = this.beginMessageSend();
    this.api
      .sendMessage({
        contactId: this.contactId,
        channel: 'whatsapp',
        type: 'text',
        text: this.textControl.value.trim(),
      })
      .subscribe({
        next: (result) => {
          this.applySendResult(tracking, result);
          this.textControl.reset('');
          this.fetchConversation();
        },
        error: () => this.stopPolling(),
      });
  }

  sendTemplate(): void {
    if (this.templateControl.invalid) {
      this.templateControl.markAsTouched();
      return;
    }
    if (!this.contactId) {
      return;
    }
    const tracking = this.beginMessageSend();
    this.api
      .sendMessage({
        contactId: this.contactId,
        channel: 'whatsapp',
        type: 'template',
        templateName: this.templateControl.value.trim(),
      })
      .subscribe({
        next: (result) => {
          this.applySendResult(tracking, result);
          this.templateControl.reset('');
          this.fetchConversation();
        },
        error: () => this.stopPolling(),
      });
  }

  insertTemplateVariable(variable: string): void {
    const current = this.templateControl.value;
    const nextValue = current.includes(variable) ? current : `${current} ${variable}`.trim();
    this.templateControl.setValue(nextValue);
    setTimeout(() => this.templateComposer?.nativeElement.focus());
  }

  statusLabel(status: MessageStatus): string {
    const labels: Record<MessageStatus, string> = {
      queued: 'Queued',
      sent: 'Sent',
      delivered: 'Delivered',
      opened: 'Opened',
      failed: 'Failed',
      bounced: 'Bounced',
    };
    return labels[status];
  }

  statusIcon(status: MessageStatus): string {
    switch (status) {
      case 'queued':
        return 'pending';
      case 'sent':
        return 'outgoing_mail';
      case 'delivered':
        return 'task_alt';
      case 'opened':
        return 'visibility';
      case 'failed':
        return 'error';
      case 'bounced':
        return 'unpublished';
      default:
        return 'info';
    }
  }

  statusAccent(status: MessageStatus): 'primary' | 'accent' | 'warn' {
    switch (status) {
      case 'delivered':
      case 'opened':
        return 'primary';
      case 'failed':
      case 'bounced':
        return 'warn';
      default:
        return 'accent';
    }
  }

  messageTooltip(message: Message): string {
    const status = this.statusLabel(message.status);
    const time = new Date(message.createdAt).toLocaleString();
    return `${status} | ${time}`;
  }

  providerBadge(provider?: string | null): ProviderBadge | null {
    if (!provider) {
      return null;
    }
    return this.toProviderBadge(provider);
  }

  renderPayload(message: Message): string {
    if (!message.payloadJson) {
      return '';
    }
    try {
      const parsed = JSON.parse(message.payloadJson);
      return typeof parsed === 'string' ? parsed : JSON.stringify(parsed, null, 2);
    } catch {
      return message.payloadJson;
    }
  }

  private beginMessageSend(): PendingMessageTracking {
    const tracking: PendingMessageTracking = {
      providerMessageId: null,
      sentAtIso: new Date().toISOString(),
    };
    this.awaitingTerminal = true;
    this.pendingAutoPoll = true;
    this.sending.set(true);
    this.pendingTracking = tracking;
    return tracking;
  }

  private applySendResult(
    tracking: PendingMessageTracking,
    result: SendMessageResponse | undefined,
  ): void {
    if (this.pendingTracking !== tracking) {
      return;
    }
    if (result?.providerMessageId) {
      this.pendingTracking.providerMessageId = result.providerMessageId;
    }
  }

  private fetchConversation(options: { skipSendingReset?: boolean } = {}): void {
    if (!this.contactId) {
      if (!options.skipSendingReset) {
        this.sending.set(false);
      }
      this.conversation.set({ conversationId: undefined, messages: [] });
      return;
    }
    this.api.getConversation(this.contactId).subscribe({
      next: (response) => {
        const messages = response.messages ?? [];
        this.conversation.set({
          conversationId: response.conversationId,
          messages,
        });

        const trackedMessage = this.findTrackedOutbound(messages);
        if (this.pendingAutoPoll) {
          if (trackedMessage) {
            this.pendingAutoPoll = false;
            if (this.isTerminal(trackedMessage.status)) {
              this.stopPolling();
            } else {
              this.startPolling(trackedMessage.id);
            }
          } else {
            this.schedulePendingFetch();
          }
        } else if (this.awaitingTerminal && !trackedMessage) {
          this.schedulePendingFetch();
        }

        if (this.pollingMessageId) {
          const tracked = messages.find((m) => m.id === this.pollingMessageId);
          if (tracked && this.isTerminal(tracked.status)) {
            this.stopPolling();
          }
        }

        if (!this.awaitingTerminal && !options.skipSendingReset) {
          this.sending.set(false);
        }
      },
      error: () => {
        if (!options.skipSendingReset) {
          this.sending.set(false);
        }
        this.stopPolling();
        this.conversation.set({ conversationId: undefined, messages: [] });
      },
    });
  }

  private startPolling(messageId: string): void {
    if (this.pollingMessageId === messageId && this.pollingTimer) {
      return;
    }
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
    }
    this.pollingMessageId = messageId;
    this.pollingTimer = setInterval(() => this.fetchConversation({ skipSendingReset: true }), 5000);
  }

  private schedulePendingFetch(): void {
    if (this.pendingFetchTimeout || this.pollingTimer) {
      return;
    }
    this.pendingFetchTimeout = setTimeout(() => {
      this.pendingFetchTimeout = null;
      this.fetchConversation({ skipSendingReset: true });
    }, 3000);
  }

  private stopPolling(): void {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = null;
    }
    if (this.pendingFetchTimeout) {
      clearTimeout(this.pendingFetchTimeout);
      this.pendingFetchTimeout = null;
    }
    this.pollingMessageId = null;
    this.awaitingTerminal = false;
    this.pendingAutoPoll = false;
    this.pendingTracking = null;
    this.sending.set(false);
  }

  private findTrackedOutbound(messages: Message[]): Message | undefined {
    if (!this.pendingTracking) {
      return undefined;
    }

    if (this.pendingTracking.messageId) {
      const existing = messages.find(
        (m) => m.id === this.pendingTracking?.messageId && m.direction === 'out',
      );
      if (existing) {
        return existing;
      }
    }

    const outbound = messages.filter((m) => m.direction === 'out');
    if (!outbound.length) {
      return undefined;
    }

    if (this.pendingTracking.providerMessageId) {
      const byProvider = outbound.find(
        (m) => m.providerMessageId === this.pendingTracking?.providerMessageId,
      );
      if (byProvider) {
        this.pendingTracking.messageId = byProvider.id;
        return byProvider;
      }
    }

    const sentThreshold = Date.parse(this.pendingTracking.sentAtIso);
    if (!Number.isNaN(sentThreshold)) {
      const cutoff = sentThreshold - 1000; // allow minimal clock drift
      const recent = outbound
        .filter((m) => {
          const created = Date.parse(m.createdAt);
          return !Number.isNaN(created) && created >= cutoff;
        })
        .sort((a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt));
      const pick = recent.at(-1);
      if (pick) {
        this.pendingTracking.messageId = pick.id;
        return pick;
      }
    }

    return undefined;
  }

  private isTerminal(status: MessageStatus): boolean {
    return (
      status === 'delivered' || status === 'opened' || status === 'failed' || status === 'bounced'
    );
  }

  private toProviderBadge(provider: string): ProviderBadge {
    const normalized = provider.toLowerCase();
    if (normalized.includes('whatsapp')) {
      return { label: 'WhatsApp Cloud', className: 'provider-whatsapp' };
    }
    if (normalized.includes('resend')) {
      return { label: 'Resend', className: 'provider-resend' };
    }
    if (normalized.includes('twilio')) {
      return { label: 'Twilio SMS', className: 'provider-twilio' };
    }
    return { label: provider, className: 'provider-generic' };
  }

  private buildAiMessagePayload(): AiGenerateMessageRequest | null {
    if (!this.contactId) {
      return null;
    }
    const convo = this.conversation();
    if (!convo?.messages?.length) {
      return null;
    }
    const channel = this.primaryChannel() ?? 'email';
    const historySnippet = this.buildHistorySnippet();
    const context = {
      firstName: undefined,
      lastName: undefined,
      company: undefined,
      role: undefined,
      painPoints: [] as string[],
      pitch: this.pitchControl.value,
      calendlyUrl: undefined,
      lastMessageSnippet: historySnippet || undefined,
    };
    return {
      contactId: this.contactId,
      channel,
      language: channel === 'email' ? 'fr' : 'en',
      context,
    };
  }

  private latestInboundMessage(): Message | undefined {
    const convo = this.conversation();
    if (!convo?.messages?.length) {
      return undefined;
    }
    return [...convo.messages].reverse().find((m) => m.direction === 'in');
  }

  private requestSmartFollowup(): void {
    if (this.followupLoading()) {
      return;
    }
    const conversationId = this.conversation()?.conversationId;
    if (!conversationId) {
      return;
    }
    this.followupLoading.set(true);
    this.api
      .getFollowupConversationPreview(conversationId)
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (res) => {
          this.followupLoading.set(false);
          this.followupHistorySnippet.set(res.historySnippet ?? '');
          this.followupPreview.set(res.proposal);
        },
        error: () => {
          this.followupLoading.set(false);
        },
      });
  }

  private buildHistorySnippet(limit = 4): string {
    const convo = this.conversation();
    const messages = convo?.messages ?? [];
    if (!messages.length) {
      return '';
    }
    const recent = messages.slice(-limit);
    return recent
      .map((message) => {
        const who = message.direction === 'in' ? 'Client' : 'Vous';
        const text = this.renderPayload(message).replace(/\s+/g, ' ').trim();
        return `${who}: ${text}`;
      })
      .join('\n');
  }

  private readPitch(): string {
    if (typeof window === 'undefined') {
      return 'Nous aidons les équipes à accélérer leur prospection avec un copilote IA.';
    }
    try {
      return (
        window.localStorage.getItem(this.pitchStorageKey) ??
        'Nous aidons les équipes à accélérer leur prospection avec un copilote IA.'
      );
    } catch {
      return 'Nous aidons les équipes à accélérer leur prospection avec un copilote IA.';
    }
  }

  private savePitch(value: string): void {
    if (typeof window === 'undefined') {
      return;
    }
    try {
      window.localStorage.setItem(this.pitchStorageKey, value);
    } catch {
      // ignore storage errors
    }
  }

  private readFollowupState(): {
    enabled: boolean;
    preview: FollowupProposalPreview | null;
    history: string;
  } {
    if (typeof window === 'undefined') {
      return { enabled: true, preview: null, history: '' };
    }
    try {
      const raw = window.localStorage.getItem(this.followupStorageKey);
      if (!raw) {
        return { enabled: true, preview: null, history: '' };
      }
      const parsed = JSON.parse(raw) as {
        enabled?: boolean;
        preview?: FollowupProposalPreview | null;
        history?: string;
      };
      const preview =
        parsed.preview && typeof parsed.preview.proposalId === 'string' ? parsed.preview : null;
      return {
        enabled: parsed.enabled ?? true,
        preview,
        history: typeof parsed.history === 'string' ? parsed.history : '',
      };
    } catch {
      return { enabled: true, preview: null, history: '' };
    }
  }

  private persistFollowupState(
    enabled: boolean,
    preview: FollowupProposalPreview | null,
    history: string,
  ): void {
    if (typeof window === 'undefined') {
      return;
    }
    try {
      window.localStorage.setItem(
        this.followupStorageKey,
        JSON.stringify({ enabled, preview, history }),
      );
    } catch {
      // ignore storage errors
    }
  }

  channelIcon(channel: Channel | null): string {
    switch (channel) {
      case 'whatsapp':
        return 'chat_bubble';
      case 'email':
        return 'mail';
      case 'sms':
        return 'sms';
      default:
        return 'chat';
    }
  }
}
