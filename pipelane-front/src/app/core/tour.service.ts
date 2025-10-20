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
        id: 'connect-email',
        element: '[data-tour="onboarding-email"]',
        title: 'Connecter ton email',
        text: 'Commence par relier ta boîte d’envoi dans Onboarding. Le statut et un test rapide sont disponibles sur chaque carte.',
        placement: 'right',
        route: '/onboarding',
      },
      {
        id: 'write-pitch',
        element: '[data-tour="conversation-pitch"]',
        title: 'Écrire ton pitch',
        text: 'Renseigne un pitch clair ici : il sera utilisé par le copilote IA pour personnaliser chaque approche.',
        placement: 'left',
        route: '/contacts',
      },
      {
        id: 'import-contacts',
        element: '[data-tour="quick-action-import-contacts"]',
        title: 'Importer des contacts',
        text: 'Depuis n’importe où, utilise ce raccourci pour lancer l’import CSV ou connecter ton CRM.',
        placement: 'left',
      },
      {
        id: 'generate-message',
        element: '[data-tour="conversation-ai-actions"]',
        title: 'Générer un message IA',
        text: 'Clique sur “Générer un message (IA)” pour obtenir un brouillon contextualisé en quelques secondes.',
        placement: 'left',
        route: '/contacts',
      },
      {
        id: 'smart-followup',
        element: '[data-tour="conversation-ai-actions"] mat-slide-toggle',
        title: 'Activer la relance intelligente',
        text: 'Passe le switch en mode ON : la plateforme proposera automatiquement la prochaine relance au bon moment.',
        placement: 'left',
        route: '/contacts',
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
