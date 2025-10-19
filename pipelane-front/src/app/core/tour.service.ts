import { Injectable, NgZone, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ShepherdService } from 'angular-shepherd';

interface TourStep {
  id: string;
  element: string;
  title: string;
  text: string;
  placement: 'top' | 'bottom' | 'left' | 'right';
  route?: string;
}

@Injectable({ providedIn: 'root' })
export class TourService {
  private readonly shepherd = inject(ShepherdService);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);

  private readonly storageKey = 'pipelane_tour_done';
  private configured = false;

  constructor() {
    window.addEventListener('pipelane:replay-tour', () => this.start(true));
  }

  initialize(): void {
    if (!this.configured) {
      this.configure();
      this.configured = true;
    }
    if (!this.isCompleted()) {
      this.zone.runOutsideAngular(() => setTimeout(() => this.start(), 600));
    }
  }

  replay(): void {
    this.start(true);
  }

  private start(force = false): void {
    if (!force && this.isCompleted()) {
      return;
    }
    if (!this.configured) {
      this.configure();
      this.configured = true;
    }
    this.shepherd.start();
  }

  private configure(): void {
    this.shepherd.defaultStepOptions = {
      classes: 'glass shepherd-theme',
      cancelIcon: { enabled: true },
      scrollTo: true,
    };
    this.shepherd.modal = true;

    const steps: TourStep[] = [
      {
        id: 'channels',
        element: '[data-tour="nav-onboarding"]',
        title: 'Connect your channels',
        text: 'Begin in Onboarding to plug in WhatsApp, email, and SMS credentials. Each card shows connection status and quick tests.',
        placement: 'right',
      },
      {
        id: 'templates',
        element: '[data-tour="nav-templates"]',
        title: 'Add templates',
        text: 'Create rich omnichannel templates to personalise outreach and unlock automated follow-ups.',
        placement: 'right',
      },
      {
        id: 'contacts',
        element: '[data-tour="nav-contacts"]',
        title: 'Import contacts',
        text: 'Bring your audience with CSV imports or API syncs, then segment them with tags and recency filters.',
        placement: 'right',
      },
      {
        id: 'send-test',
        element: '[data-tour="quick-action-send-test"]',
        title: 'Send yourself a test',
        text: 'Use the quick action to trigger a test message and verify channel delivery before full send.',
        placement: 'left',
      },
      {
        id: 'create-campaign',
        element: '[data-tour="quick-action-create-campaign"]',
        title: 'Build campaigns',
        text: 'Open the campaign builder to orchestrate multi-step journeys with fallbacks, throttling and scheduling.',
        placement: 'left',
      },
      {
        id: 'follow-ups',
        element: '[data-tour="nav-campaigns"]',
        title: 'Enable follow-ups',
        text: 'Manage automated follow-ups and nurture flows inside Campaigns. Configure segments, templates and cadence.',
        placement: 'right',
      },
      {
        id: 'analytics',
        element: '[data-tour="nav-analytics"]',
        title: 'View analytics',
        text: 'Track delivery, engagement and channel performance in the analytics dashboard with live Apex charts.',
        placement: 'right',
      },
    ];

    this.shepherd.addSteps(
      steps.map((step) => ({
        id: step.id,
        title: step.title,
        text: step.text,
        attachTo: {
          element: step.element,
          on: step.placement,
        },
        when: {
          show: async () => {
            if (step.route) {
              await this.router.navigateByUrl(step.route);
            }
          },
        },
        buttons: [
          {
            classes: 'shepherd-button-secondary',
            text: 'Skip tour',
            action: () => {
              this.complete();
              this.shepherd.cancel();
            },
          },
          {
            classes: 'shepherd-button-primary',
            text: 'Next',
            action: () => this.shepherd.next(),
          },
        ],
      })),
    );

    const tour = this.shepherd.tourObject;
    tour?.on('complete', () => this.complete());
    tour?.on('cancel', () => this.complete());
  }

  private isCompleted(): boolean {
    return localStorage.getItem(this.storageKey) === 'true';
  }

  private complete(): void {
    localStorage.setItem(this.storageKey, 'true');
  }
}
