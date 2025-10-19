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
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatMenuModule } from '@angular/material/menu';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ApiService } from '../../core/api.service';
import { PolicyService } from '../../core/policy.service';
import {
  Channel,
  ChannelLabels,
  ConversationResponse,
  Message,
  MessageStatus,
  SendMessageResponse,
} from '../../core/models';

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

  @ViewChild('textComposer') private textComposer?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('templateComposer') private templateComposer?: ElementRef<
    HTMLInputElement | HTMLTextAreaElement
  >;

  readonly contactId = this.route.snapshot.paramMap.get('contactId')!;

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
