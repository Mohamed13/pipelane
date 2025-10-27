import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatStepperModule } from '@angular/material/stepper';
import { RouterModule } from '@angular/router';

import { ApiService } from '../../core/api.service';

@Component({
  selector: 'pl-prospecting-onboarding',
  standalone: true,
  imports: [
    CommonModule,
    MatStepperModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatSnackBarModule,
    RouterModule,
  ],
  templateUrl: './prospecting-onboarding.component.html',
  styleUrls: ['./prospecting-onboarding.component.scss'],
})
export class ProspectingOnboardingComponent {
  private readonly api = inject(ApiService);
  private readonly snackbar = inject(MatSnackBar);

  readonly busy = signal(false);

  runAutomation(slug: 'enrich' | 'send-next' | 'follow-up'): void {
    this.busy.set(true);
    this.api
      .triggerProspectingHook(slug)
      .subscribe({
        next: () => {
          this.snackbar?.open('Automation triggered in n8n', 'Dismiss', { duration: 4000 });
        },
        error: () => {
          this.snackbar?.open('Unable to trigger automation', 'Dismiss', { duration: 5000 });
        },
      })
      .add(() => this.busy.set(false));
  }
}
