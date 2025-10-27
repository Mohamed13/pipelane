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
        id: 'connect-channels',
        element: '[data-tour="onboarding-email"]',
        title: 'Connecter tes canaux',
        text: 'Commence par relier email, WhatsApp ou SMS dans Onboarding. Chaque carte te permet de tester l’intégration en direct.',
        placement: 'right',
        route: '/onboarding',
      },
      {
        id: 'write-pitch',
        element: '[data-tour="conversation-pitch"]',
        title: 'Écrire ton pitch',
        text: 'Définis un pitch concis : il sera injecté par le copilote IA dans chaque message pour conserver ton ton.',
        placement: 'left',
        route: '/contacts',
      },
      {
        id: 'hunter-search',
        element: '[data-tour="hunter-search"]',
        title: 'Trouver des prospects',
        text: 'Depuis Hunter, précise ta cible ou colle une phrase en français. Lance la recherche pour enrichir, scorer et visualiser la carte.',
        placement: 'right',
        route: '/hunter',
      },
      {
        id: 'hunter-cadence',
        element: '[data-tour="hunter-action-bar"]',
        title: 'Créer liste & cadence',
        text: 'Sélectionne les prospects (ou Magic Pick) puis clique sur “Créer une cadence” pour générer la séquence prête à l’emploi.',
        placement: 'top',
        route: '/hunter',
      },
      {
        id: 'generate-message',
        element: '[data-tour="conversation-ai-actions"]',
        title: 'Générer et envoyer',
        text: 'Utilise “Générer un message (IA)” puis “Envoyer” pour laisser le copilote personnaliser et déclencher les relances intelligentes.',
        placement: 'left',
        route: '/contacts',
      },
      {
        id: 'analytics-export',
        element: '[data-tour="analytics-export"]',
        title: 'Analyser & exporter',
        text: 'Dans Analytics, filtre par période, ouvre le top templates et exporte un PDF pour ton reporting commercial.',
        placement: 'left',
        route: '/analytics',
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
