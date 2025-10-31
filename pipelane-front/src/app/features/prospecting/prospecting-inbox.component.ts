import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs/operators';

import { ApiService } from '../../core/api.service';
import { ProspectReplyRecord, ReplyIntent } from '../../core/models';
import { SubscriptionStore } from '../../core/subscription-store';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

@Component({
  selector: 'pl-prospecting-inbox',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonToggleModule,
    MatListModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatSnackBarModule,
    PageHeaderComponent,
  ],
  templateUrl: './prospecting-inbox.component.html',
  styleUrls: ['./prospecting-inbox.component.scss'],
})
export class ProspectingInboxComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly snackbar = inject(MatSnackBar);
  private readonly subscriptions = new SubscriptionStore();

  readonly replies = signal<ProspectReplyRecord[]>([]);
  readonly loading = signal(false);
  readonly filter = signal<ReplyIntent | 'all'>('all');

  ngOnInit(): void {
    this.load();
  }

  setFilter(intent: ReplyIntent | 'all'): void {
    this.filter.set(intent);
    this.load();
  }

  classify(reply: ProspectReplyRecord): void {
    this.subscriptions.subscribe(
      this.api.classifyProspectReply({ replyId: reply.id }),
      {
        next: (result) => {
          this.replies.update((items) =>
            items.map((item) =>
              item.id === reply.id
                ? {
                    ...item,
                    intent: result.intent,
                    intentConfidence: result.confidence,
                    processedAtUtc: new Date().toISOString(),
                  }
                : item,
            ),
          );
          this.snackbar?.open('Reply classified', 'Dismiss', { duration: 3000 });
        },
        error: () => this.snackbar?.open('Classification failed', 'Dismiss', { duration: 4000 }),
      },
      `classify-${reply.id}`,
    );
  }

  draftAutoReply(reply: ProspectReplyRecord): void {
    this.subscriptions.subscribe(
      this.api.autoReplyDraft({ replyId: reply.id, campaignId: reply.campaignId ?? undefined }),
      {
        next: () => {
          this.snackbar?.open('Draft generated via AI', 'Dismiss', { duration: 4000 });
        },
        error: () =>
          this.snackbar?.open('Unable to craft auto-reply', 'Dismiss', { duration: 5000 }),
      },
      `auto-reply-${reply.id}`,
    );
  }

  private load(): void {
    this.loading.set(true);
    const intent = this.filter();
    this.subscriptions.set(
      'load-replies',
      this.api
        .getProspectingReplies(intent === 'all' ? undefined : intent)
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({
          next: (replies) => this.replies.set(replies),
          error: () => this.snackbar?.open('Unable to load replies', 'Dismiss', { duration: 5000 }),
        }),
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.clear();
  }
}
