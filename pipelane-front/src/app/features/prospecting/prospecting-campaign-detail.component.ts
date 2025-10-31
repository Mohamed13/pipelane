import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute } from '@angular/router';
import { EMPTY, forkJoin } from 'rxjs';
import { switchMap, tap } from 'rxjs/operators';

import { ApiService } from '../../core/api.service';
import { ProspectingCampaign, ProspectingCampaignPreview } from '../../core/models';
import { SubscriptionStore } from '../../core/subscription-store';

@Component({
  selector: 'pl-prospecting-campaign-detail',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatTooltipModule,
    MatSnackBarModule,
  ],
  templateUrl: './prospecting-campaign-detail.component.html',
  styleUrls: ['./prospecting-campaign-detail.component.scss'],
})
export class ProspectingCampaignDetailComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly snackbar = inject(MatSnackBar);
  private readonly subscriptions = new SubscriptionStore();

  readonly campaign = signal<ProspectingCampaign | null>(null);
  readonly preview = signal<ProspectingCampaignPreview | null>(null);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.subscriptions.track(
      this.route.paramMap
        .pipe(
          tap(() => this.loading.set(true)),
          switchMap((params) => {
            const id = params.get('id');
            if (!id) {
              return EMPTY;
            }
            return forkJoin({
              campaign: this.api.getProspectingCampaign(id),
              preview: this.api.previewProspectingCampaign(id),
            });
          }),
        )
        .subscribe({
          next: (result) => {
            if (!result) {
              return;
            }
            this.campaign.set(result.campaign);
            this.preview.set(result.preview);
            this.loading.set(false);
          },
          error: () => {
            this.snackbar?.open('Unable to load campaign', 'Dismiss', { duration: 5000 });
            this.loading.set(false);
          },
        }),
    );
  }

  start(): void {
    const campaign = this.campaign();
    if (!campaign) {
      return;
    }
    this.subscriptions.subscribe(
      this.api.startProspectingCampaign(campaign.id),
      {
        next: (updated) => {
          this.campaign.set(updated);
          this.snackbar?.open('Campaign started', 'Dismiss', { duration: 3000 });
        },
        error: () => this.snackbar?.open('Failed to start campaign', 'Dismiss', { duration: 5000 }),
      },
      'start-campaign',
    );
  }

  pause(): void {
    const campaign = this.campaign();
    if (!campaign) {
      return;
    }
    this.subscriptions.subscribe(
      this.api.pauseProspectingCampaign(campaign.id),
      {
        next: (updated) => {
          this.campaign.set(updated);
          this.snackbar?.open('Campaign paused', 'Dismiss', { duration: 3000 });
        },
        error: () => this.snackbar?.open('Failed to pause campaign', 'Dismiss', { duration: 5000 }),
      },
      'pause-campaign',
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.clear();
  }
}
