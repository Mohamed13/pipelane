import { CommonModule, DatePipe, NgFor, NgIf } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { ApiService } from '../../core/api.service';
import { PolicyService } from '../../core/policy.service';
import { ChannelLabels, ConversationResponse, Message, MessageStatus } from '../../core/models';

@Component({
  standalone: true,
  selector: 'pl-conversation-thread',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgIf,
    NgFor,
    DatePipe,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatDividerModule,
    MatChipsModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <mat-card class="surface-card" *ngIf="conversation(); else loading">
      <header class="header" *ngIf="conversation() as conv">
        <div>
          <h2>Conversation</h2>
          <p class="body-text-muted">Channel: {{ primaryChannelLabel(conv) }}</p>
        </div>
        <mat-chip-set>
          <mat-chip color="primary" selected *ngIf="canText()">WhatsApp session active</mat-chip>
          <mat-chip color="warn" selected *ngIf="!canText()">Template required</mat-chip>
        </mat-chip-set>
      </header>

      <section class="messages" *ngIf="conversation()?.messages?.length; else empty">
        <div
          *ngFor="let message of conversation()?.messages"
          class="message"
          [class.message--outgoing]="message.direction === 'out'"
          [class.message--incoming]="message.direction === 'in'"
        >
          <div class="meta">
            <span class="meta__channel">{{ ChannelLabels[message.channel] }}</span>
            <span class="meta__time">{{ message.createdAt | date: 'short' }}</span>
          </div>
          <div class="bubble">
            <div class="bubble__type" *ngIf="message.type !== 'text'">{{ message.type | uppercase }}</div>
            <div class="bubble__body">{{ renderPayload(message) }}</div>
            <div class="bubble__chips">
              <mat-chip-set>
                <mat-chip [color]="statusColor(message.status)" selected>{{ statusLabel(message.status) }}</mat-chip>
                <mat-chip *ngIf="message.provider" color="accent" selected>
                  {{ formatProvider(message.provider) }}
                </mat-chip>
              </mat-chip-set>
              <div class="bubble__error" *ngIf="message.errorReason">
                <mat-icon>error_outline</mat-icon>
                {{ message.errorReason }}
              </div>
            </div>
          </div>
        </div>
      </section>

      <mat-divider></mat-divider>

      <section class="composer" *ngIf="conversation() as conv">
        <form class="message-form" (ngSubmit)="sendText()" *ngIf="canText(); else templateMode">
          <mat-form-field appearance="outline" class="w-100">
            <mat-label>Message</mat-label>
            <textarea matInput rows="2" [formControl]="textControl" placeholder="Type a message..."></textarea>
          </mat-form-field>
          <button mat-raised-button color="primary" type="submit" [disabled]="textControl.invalid || sending()">
            <mat-icon>send</mat-icon>
            Send
          </button>
        </form>

        <ng-template #templateMode>
          <form class="message-form" (ngSubmit)="sendTemplate()">
            <mat-form-field appearance="outline" class="w-100">
              <mat-label>Template name</mat-label>
              <input matInput [formControl]="templateControl" placeholder="e.g. welcome_new_user" />
            </mat-form-field>
            <p class="body-text-muted">WhatsApp text is disabled outside the 24h session. Send an approved template.</p>
            <button mat-raised-button color="primary" type="submit" [disabled]="templateControl.invalid || sending()">
              <mat-icon>send</mat-icon>
              Send template
            </button>
          </form>
        </ng-template>
      </section>
    </mat-card>

    <ng-template #loading>
      <div class="loading">
        <mat-progress-spinner diameter="48" mode="indeterminate"></mat-progress-spinner>
        <p>Loading conversation…</p>
      </div>
    </ng-template>

    <ng-template #empty>
      <div class="empty">
        <mat-icon>chat_bubble_outline</mat-icon>
        <p>No messages yet.</p>
      </div>
    </ng-template>
  `,
  styles: [
    `
      .header { display:flex; justify-content:space-between; align-items:center; margin-bottom: var(--space-4); }
      .messages { display:flex; flex-direction:column; gap:var(--space-3); margin-bottom: var(--space-4); }
      .message { display:flex; flex-direction:column; max-width:70%; }
      .message--outgoing { margin-left:auto; align-items:flex-end; }
      .message--incoming { align-items:flex-start; }
      .meta { display:flex; gap:var(--space-2); font-size:0.8rem; color:var(--color-text-muted); margin-bottom:var(--space-1); }
      .bubble { padding:var(--space-3); border-radius:var(--radius-md); background:var(--color-surface-alt); box-shadow:var(--elevation-1); max-width: 100%; }
      .message--outgoing .bubble { background: rgba(99,102,241,0.15); }
      .bubble__type { font-size:0.75rem; text-transform:uppercase; letter-spacing:.05em; color:var(--color-text-muted); margin-bottom:var(--space-1); }
      .bubble__body { white-space:pre-wrap; word-break:break-word; }
      .bubble__chips { display:flex; flex-direction:column; gap:var(--space-2); margin-top:var(--space-2); }
      .bubble__error { display:flex; align-items:center; gap:var(--space-1); font-size:0.8rem; color:var(--color-warn); }
      .bubble__error mat-icon { font-size: 1rem; height: 1rem; width: 1rem; }
      .composer { margin-top: var(--space-4); }
      .message-form { display:flex; gap:var(--space-3); align-items:flex-end; flex-wrap:wrap; }
      .w-100 { flex:1 1 240px; }
      .loading, .empty { display:flex; flex-direction:column; align-items:center; gap:var(--space-3); padding:var(--space-6); color:var(--color-text-muted); }
      .empty mat-icon { font-size:3rem; height:auto; width:auto; }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConversationThreadComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);
  private readonly policy = inject(PolicyService);
  private readonly destroyRef = inject(DestroyRef);

  readonly ChannelLabels = ChannelLabels;

  private readonly contactId = this.route.snapshot.paramMap.get('contactId')!;

  conversation = signal<ConversationResponse | null>(null);
  sending = signal(false);

  private pollingTimer: ReturnType<typeof setInterval> | null = null;
  private pollingMessageId: string | null = null;
  private awaitingTerminal = false;
  private pendingAutoPoll = false;
  private pendingFetchTimeout: ReturnType<typeof setTimeout> | null = null;

  readonly textControl = new FormControl<string>('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(1)],
  });
  readonly templateControl = new FormControl<string>('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(3)],
  });

  canText = computed(() => {
    const convo = this.conversation();
    if (!convo?.messages?.length) return false;
    const lastInbound = [...convo.messages]
      .filter((m) => m.direction === 'in')
      .slice(-1)[0]?.createdAt;
    return this.policy.isWhatsAppTextAllowed(lastInbound ?? null);
  });

  constructor() {
    this.destroyRef.onDestroy(() => this.stopPolling());
    this.fetchConversation();
  }

  primaryChannelLabel(conversation: ConversationResponse): string {
    const first = conversation.messages[0];
    return first ? ChannelLabels[first.channel] : 'Unknown channel';
  }

  sendText(): void {
    if (this.textControl.invalid) return;
    this.awaitingTerminal = true;
    this.pendingAutoPoll = true;
    this.sending.set(true);
    this.api
      .sendMessage({
        contactId: this.contactId,
        channel: 'whatsapp',
        type: 'text',
        text: this.textControl.value.trim(),
      })
      .subscribe({
        next: () => {
          this.textControl.reset('');
          this.fetchConversation();
        },
        error: () => this.stopPolling(),
      });
  }

  sendTemplate(): void {
    if (this.templateControl.invalid) return;
    this.awaitingTerminal = true;
    this.pendingAutoPoll = true;
    this.sending.set(true);
    this.api
      .sendMessage({
        contactId: this.contactId,
        channel: 'whatsapp',
        type: 'template',
        templateName: this.templateControl.value.trim(),
      })
      .subscribe({
        next: () => {
          this.templateControl.reset('');
          this.fetchConversation();
        },
        error: () => this.stopPolling(),
      });
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

  statusColor(status: MessageStatus): 'primary' | 'accent' | 'warn' {
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

  formatProvider(provider: string): string {
    return provider.replace(/Provider$/i, '').replace(/Channel$/i, '').trim() || provider;
  }

  renderPayload(message: Message): string {
    if (!message.payloadJson) return '';
    try {
      const parsed = JSON.parse(message.payloadJson);
      return typeof parsed === 'string' ? parsed : JSON.stringify(parsed, null, 2);
    } catch {
      return message.payloadJson;
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

        const latestOutbound = this.findLatestOutbound(messages);
        if (this.pendingAutoPoll && latestOutbound) {
          this.pendingAutoPoll = false;
          if (this.isTerminal(latestOutbound.status)) {
            this.stopPolling();
          } else {
            this.startPolling(latestOutbound.id);
          }
        } else if (this.pendingAutoPoll && !latestOutbound) {
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
    if (this.pollingMessageId === messageId) {
      return;
    }
    this.stopPolling();
    this.pollingMessageId = messageId;
    this.pollingTimer = setInterval(() => this.fetchConversation({ skipSendingReset: true }), 5000);
  }

  private schedulePendingFetch(): void {
    if (this.pendingFetchTimeout) {
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
    this.sending.set(false);
  }

  private findLatestOutbound(messages: Message[]): Message | undefined {
    return [...messages].filter((m) => m.direction === 'out').slice(-1)[0];
  }

  private isTerminal(status: MessageStatus): boolean {
    return status === 'delivered' || status === 'opened' || status === 'failed' || status === 'bounced';
  }
}

