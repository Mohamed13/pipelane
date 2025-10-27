import { Injectable, NgZone, inject, isDevMode } from '@angular/core';
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
  private readonly isDev = isDevMode();

  private readonly steps: TourStep[] = [
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

  private readonly storageKey = 'pipelane_tour_done';
  private configured = false;
  private readonly missingSteps = new Set<string>();

  constructor() {
    window.addEventListener('pipelane:replay-tour', () => this.start(true));
  }

  initialize(): void {
    if (!this.configured) {
      this.configure();
      this.configured = true;
    }
    if (!this.isCompleted() && this.hasAnyTarget()) {
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
    this.shepherd.addSteps(
      this.steps.map((step) => ({
        id: step.id,
        title: step.title,
        text: step.text,
        attachTo: {
          element: step.element,
          on: step.placement,
        },
        showOn: () => {
          if (this.missingSteps.has(step.id)) {
            this.missingSteps.delete(step.id);
            return false;
          }
          return !!document.querySelector(step.element);
        },
        beforeShowPromise: async () => {
          if (step.route) {
            await this.router.navigateByUrl(step.route);
          }
          try {
            await this.elementReady(step.element);
          } catch {
            this.missingSteps.add(step.id);
            this.logStepSkip(step.id, step.element);
          }
        },
        when: {
          show: () => {
            const tour = this.shepherd.tourObject;
            if (this.missingSteps.has(step.id)) {
              this.missingSteps.delete(step.id);
              this.zone.run(() => setTimeout(() => tour?.next(), 0));
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

  private hasAnyTarget(): boolean {
    return this.steps.some((step) => !!document.querySelector(step.element));
  }

  private elementReady(selector: string, timeout = 3000): Promise<HTMLElement> {
    if (typeof document === 'undefined') {
      return Promise.reject(new Error('dom_unavailable'));
    }

    return new Promise((resolve, reject) => {
      const element = document.querySelector<HTMLElement>(selector);
      if (element) {
        resolve(element);
        return;
      }

      const body = document.body;
      if (!body) {
        reject(new Error('no_body'));
        return;
      }

      const observer = new MutationObserver(() => {
        const candidate = document.querySelector<HTMLElement>(selector);
        if (candidate) {
          observer.disconnect();
          window.clearTimeout(timer);
          resolve(candidate);
        }
      });

      observer.observe(body, { childList: true, subtree: true });

      const timer = window.setTimeout(() => {
        observer.disconnect();
        reject(new Error('timeout'));
      }, timeout);
    });
  }

  private logStepSkip(stepId: string, selector: string): void {
    if (!this.isDev || typeof console === 'undefined') {
      return;
    }
    console.warn(`[Tour] step skipped`, { stepId, selector });
  }
}
