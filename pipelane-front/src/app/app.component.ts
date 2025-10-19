import { CommonModule } from '@angular/common';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { animate, animation, style, transition, trigger, useAnimation } from '@angular/animations';
import {
  ChangeDetectionStrategy,
  Component,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import {
  ActivatedRoute,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { I18nService } from './core/i18n.service';
import { ThemeService } from './core/theme.service';
import { TourService } from './core/tour.service';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  tooltip: string;
  exact?: boolean;
  aliases?: string[];
  tourKey?: string;
}

interface Breadcrumb {
  label: string;
  url?: string;
}
type QuickActionKey = 'send-test' | 'create-campaign' | 'import-contacts';

const fadeIn = animation([
  style({ opacity: 0, transform: 'translateY(12px)' }),
  animate('240ms ease-out'),
]);

@Component({
  standalone: true,
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    CommonModule,
    FormsModule,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatMenuModule,
    MatTooltipModule,
  ],
  animations: [
    trigger('headerAnimate', [
      transition(':enter', [useAnimation(fadeIn)]),
      transition(':increment', [useAnimation(fadeIn)]),
    ]),
  ],
  template: `
    <mat-sidenav-container class="shell-container">
      <mat-sidenav
        #snav
        class="nav-shell"
        [mode]="sidenavMode()"
        [opened]="opened()"
        (openedChange)="onOpenedChange($event)"
      >
        <div class="rail-head">
          <div class="brand">
            <mat-icon aria-hidden="true">auto_awesome</mat-icon>
            <span>Pipelane</span>
          </div>
          <button
            mat-icon-button
            class="neon-outline"
            (click)="toggleRailCollapse()"
            aria-label="Collapse navigation"
            *ngIf="!isHandset()"
            [matTooltip]="collapsed() ? 'Expand menu' : 'Collapse menu'"
          >
            <mat-icon>{{ collapsed() ? 'chevron_right' : 'chevron_left' }}</mat-icon>
          </button>
        </div>
        <mat-nav-list class="nav-rail" [class.nav-rail--collapsed]="collapsed()">
          <a
            mat-list-item
            *ngFor="let item of navItems; trackBy: trackByRoute"
            [routerLink]="item.route"
            (click)="handleNavClick()"
            routerLinkActive="is-active"
            [routerLinkActiveOptions]="{ exact: item.exact ?? true }"
            [matTooltip]="item.tooltip"
            matTooltipPosition="right"
            [attr.data-tour]="item.tourKey || null"
          >
            <mat-icon aria-hidden="true" class="nav-icon">{{ item.icon }}</mat-icon>
            <span class="nav-label">{{ item.label }}</span>
          </a>
        </mat-nav-list>
        <div class="rail-footer">
          <button
            mat-stroked-button
            color="primary"
            (click)="onQuickAction('create-campaign')"
            class="launch-button"
          >
            <mat-icon aria-hidden="true">rocket_launch</mat-icon>
            <span>Launch campaign</span>
          </button>
        </div>
      </mat-sidenav>

      <mat-sidenav-content>
        <a class="skip-link" href="#main-content">Skip to content</a>
        <mat-toolbar class="toolbar glass">
          <button
            mat-icon-button
            class="neon-outline"
            (click)="toggleSidenav()"
            aria-label="Toggle navigation"
          >
            <mat-icon>{{ opened() ? 'menu_open' : 'menu' }}</mat-icon>
          </button>
          <div class="route-title">
            <span class="title route-underline is-active">{{ activeNavTitle() }}</span>
          </div>
          <span class="spacer"></span>
          <mat-form-field appearance="outline" class="search" floatLabel="never">
            <mat-icon matPrefix aria-hidden="true">search</mat-icon>
            <input matInput placeholder="Search journeys, contacts…" [(ngModel)]="search" />
          </mat-form-field>
          <div
            class="theme-toggle neon-outline"
            (click)="toggleTheme()"
            role="button"
            tabindex="0"
            (keyup.enter)="toggleTheme()"
            matTooltip="Toggle theme"
            aria-label="Toggle theme"
          >
            <mat-icon aria-hidden="true">{{
              theme() === 'dark' ? 'dark_mode' : 'light_mode'
            }}</mat-icon>
            <span>{{ theme() === 'dark' ? 'Dark' : 'Light' }}</span>
          </div>
          <button
            mat-icon-button
            [matMenuTriggerFor]="langMenu"
            aria-label="Change language"
            matTooltip="Language"
          >
            <mat-icon aria-hidden="true">translate</mat-icon>
          </button>
          <mat-menu #langMenu="matMenu">
            <button mat-menu-item (click)="setLang('en')">
              <span>English</span>
              <mat-icon *ngIf="lang() === 'en'">check</mat-icon>
            </button>
            <button mat-menu-item (click)="setLang('fr')">
              <span>Français</span>
              <mat-icon *ngIf="lang() === 'fr'">check</mat-icon>
            </button>
          </mat-menu>
          <button
            mat-icon-button
            [matMenuTriggerFor]="helpMenu"
            aria-label="Open help menu"
            matTooltip="Help & tutorial"
            class="neon-outline"
          >
            <mat-icon aria-hidden="true">help</mat-icon>
          </button>
          <mat-menu #helpMenu="matMenu">
            <button mat-menu-item (click)="replayTour()">
              <mat-icon>play_circle</mat-icon>
              <span>Replay tutorial</span>
            </button>
            <button mat-menu-item (click)="openDocs()">
              <mat-icon>description</mat-icon>
              <span>Docs</span>
            </button>
            <button mat-menu-item (click)="openSupport()">
              <mat-icon>support_agent</mat-icon>
              <span>Support</span>
            </button>
          </mat-menu>
        </mat-toolbar>

        <section
          class="header-band glass"
          *ngIf="breadcrumbs().length"
          [@headerAnimate]="breadcrumbs().length"
        >
          <nav class="breadcrumb-trail" aria-label="Breadcrumb">
            <ng-container *ngFor="let crumb of breadcrumbs(); let last = last">
              <a *ngIf="crumb.url && !last" [routerLink]="crumb.url">{{ crumb.label }}</a>
              <span *ngIf="!crumb.url || last">{{ crumb.label }}</span>
              <mat-icon aria-hidden="true" *ngIf="!last">chevron_right</mat-icon>
            </ng-container>
          </nav>
          <div class="quick-actions">
            <button
              mat-stroked-button
              color="primary"
              (click)="onQuickAction('send-test')"
              matTooltip="Send yourself a test message"
              data-tour="quick-action-send-test"
            >
              <mat-icon aria-hidden="true">send</mat-icon>
              <span>Send test</span>
            </button>
            <button
              mat-stroked-button
              color="accent"
              (click)="onQuickAction('create-campaign')"
              matTooltip="Create your next campaign"
              data-tour="quick-action-create-campaign"
            >
              <mat-icon aria-hidden="true">flag</mat-icon>
              <span>Create campaign</span>
            </button>
            <button
              mat-stroked-button
              color="accent"
              (click)="onQuickAction('import-contacts')"
              matTooltip="Import contacts via CSV or API"
              data-tour="quick-action-import-contacts"
            >
              <mat-icon aria-hidden="true">upload</mat-icon>
              <span>Import contacts</span>
            </button>
            <button
              mat-stroked-button
              color="warn"
              (click)="openDocs()"
              matTooltip="Read product documentation"
            >
              <mat-icon aria-hidden="true">menu_book</mat-icon>
              <span>Docs</span>
            </button>
          </div>
        </section>

        <main class="content" id="main-content">
          <router-outlet></router-outlet>
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [
    `
      .nav-shell {
        width: 288px;
        border-right: 1px solid rgba(117, 240, 255, 0.08);
        background: rgba(16, 23, 38, 0.92);
        backdrop-filter: blur(18px);
        display: flex;
        flex-direction: column;
      }

      .rail-head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: var(--space-4);
      }

      .brand {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        background: var(--grad-main);
        -webkit-background-clip: text;
        -webkit-text-fill-color: transparent;
      }

      .nav-rail {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        padding: 0 var(--space-3);
        overflow-y: auto;
      }

      .nav-rail--collapsed .nav-label {
        display: none;
      }

      .nav-rail--collapsed a.mat-mdc-list-item {
        justify-content: center;
      }

      .nav-rail .mat-mdc-list-item {
        border-radius: var(--radius-md);
        transition:
          background 220ms ease,
          transform 220ms ease;
      }

      .nav-rail .mat-mdc-list-item:hover {
        background: rgba(117, 240, 255, 0.08);
      }

      .nav-rail .mat-mdc-list-item.is-active {
        background: rgba(117, 240, 255, 0.12);
        border: 1px solid rgba(117, 240, 255, 0.25);
        transform: translateX(4px);
      }

      .nav-icon {
        margin-right: 1rem;
      }

      .nav-rail--collapsed .nav-icon {
        margin-right: 0;
      }

      .rail-footer {
        padding: var(--space-4);
        margin-top: auto;
      }

      .launch-button {
        width: 100%;
        border-radius: var(--radius-pill);
        border-color: rgba(117, 240, 255, 0.35) !important;
        backdrop-filter: blur(6px);
      }

      .toolbar.glass {
        border-bottom: none;
      }

      .quick-actions {
        display: flex;
        gap: var(--space-2);
        align-items: center;
      }

      .route-title {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }

      .header-band {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-4);
        margin: var(--space-4) var(--space-6) 0;
        padding: var(--space-3) var(--space-4);
        border-radius: var(--radius-lg);
        border: 1px solid rgba(117, 240, 255, 0.12);
      }

      .breadcrumb-trail mat-icon {
        font-size: 18px;
        vertical-align: middle;
        opacity: 0.6;
      }

      .quick-actions button {
        backdrop-filter: blur(6px);
      }

      @media (max-width: 1024px) {
        .quick-actions {
          flex-wrap: wrap;
          justify-content: flex-end;
        }
      }

      @media (max-width: 960px) {
        .nav-shell {
          width: 260px;
        }

        .header-band {
          margin: var(--space-4);
        }

        .search {
          display: none;
        }
      }

      @media (max-width: 720px) {
        .header-band {
          flex-direction: column;
          align-items: flex-start;
        }

        .quick-actions {
          width: 100%;
          justify-content: flex-start;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  private readonly i18n = inject(I18nService);
  private readonly themeSvc = inject(ThemeService);
  private readonly bp = inject(BreakpointObserver);
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly tour = inject(TourService);

  @ViewChild('snav') private sidenav?: MatSidenav;

  readonly navItems: NavItem[] = [
    {
      label: 'Analytics',
      route: '/analytics',
      icon: 'monitoring',
      tooltip: 'Analytics overview',
      tourKey: 'nav-analytics',
    },
    {
      label: 'Onboarding',
      route: '/onboarding',
      icon: 'hub',
      tooltip: 'Connect your channels',
      tourKey: 'nav-onboarding',
    },
    {
      label: 'Templates',
      route: '/templates',
      icon: 'space_dashboard',
      tooltip: 'Manage templates',
      tourKey: 'nav-templates',
    },
    {
      label: 'Contacts',
      route: '/contacts',
      icon: 'people_alt',
      tooltip: 'View contacts',
      aliases: ['/conversations'],
      tourKey: 'nav-contacts',
    },
    {
      label: 'Campaigns',
      route: '/campaigns',
      icon: 'flag',
      tooltip: 'Build campaigns',
      tourKey: 'nav-campaigns',
    },
    { label: 'Settings', route: '/settings', icon: 'tune', tooltip: 'Console settings' },
  ];

  lang = this.i18n.lang;
  theme = this.themeSvc.theme;
  search = '';
  opened = signal<boolean>(this.readBool('snav_opened', true));
  collapsed = signal<boolean>(this.readBool('snav_collapsed', false));
  sidenavMode = signal<'side' | 'over'>('side');
  isHandset = signal<boolean>(false);
  currentUrl = signal<string>(this.router.url);
  breadcrumbs = signal<Breadcrumb[]>([]);
  activeNavTitle = computed(() => {
    const url = this.currentUrl();
    const nav = this.navItems.find((item) => this.matchesRoute(url, item));
    return nav?.label ?? 'Pipelane Console';
  });

  constructor() {
    this.bp
      .observe([Breakpoints.Medium, Breakpoints.Small, Breakpoints.Handset])
      .pipe(takeUntilDestroyed())
      .subscribe((state) => {
        const handset =
          state.breakpoints[Breakpoints.Handset] || state.breakpoints[Breakpoints.Small];
        this.isHandset.set(handset);
        this.sidenavMode.set(handset ? 'over' : 'side');
        if (handset) {
          this.collapsed.set(false);
          this.opened.set(false);
        } else {
          this.opened.set(this.readBool('snav_opened', true));
          this.collapsed.set(this.readBool('snav_collapsed', false));
        }
      });

    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => {
        this.currentUrl.set(this.router.url);
        this.breadcrumbs.set(this.buildBreadcrumbs());
      });

    this.breadcrumbs.set(this.buildBreadcrumbs());
    this.tour.initialize();
  }

  setLang(language: 'en' | 'fr') {
    this.i18n.setLang(language);
  }

  toggleTheme() {
    this.themeSvc.toggle();
  }

  toggleSidenav() {
    if (this.sidenavMode() === 'over') {
      this.sidenav?.toggle();
    } else {
      this.onOpenedChange(!this.opened());
    }
  }

  toggleRailCollapse(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    localStorage.setItem('snav_collapsed', JSON.stringify(next));
  }

  handleNavClick(): void {
    if (this.sidenavMode() === 'over') {
      this.sidenav?.close();
    }
  }

  onOpenedChange(val: boolean) {
    this.opened.set(val);
    localStorage.setItem('snav_opened', JSON.stringify(val));
  }

  onQuickAction(action: QuickActionKey): void {
    switch (action) {
      case 'send-test':
        this.router.navigate(['/onboarding'], { queryParams: { action: 'send-test' } });
        break;
      case 'create-campaign':
        this.router.navigate(['/campaigns'], { queryParams: { new: 'true' } });
        break;
      case 'import-contacts':
        this.router.navigate(['/contacts'], { queryParams: { view: 'import' } });
        break;
    }
  }

  openDocs(): void {
    if (typeof window !== 'undefined') {
      window.open('https://docs.pipelane.dev', '_blank', 'noopener');
    }
  }

  openSupport(): void {
    if (typeof window !== 'undefined') {
      window.open('https://support.pipelane.dev', '_blank', 'noopener');
    }
  }

  replayTour(): void {
    this.tour.replay();
  }

  trackByRoute(_: number, item: NavItem): string {
    return item.route;
  }

  private readBool(key: string, fallback: boolean): boolean {
    try {
      const raw = localStorage.getItem(key);
      return raw === null ? fallback : JSON.parse(raw);
    } catch {
      return fallback;
    }
  }

  private buildBreadcrumbs(): Breadcrumb[] {
    const breadcrumbs: Breadcrumb[] = [];
    let route = this.activatedRoute.root;
    let url = '';

    while (route.firstChild) {
      route = route.firstChild;
      const snapshot = route.snapshot;
      const segment = snapshot.url.map((part) => part.path).join('/');

      if (!segment) {
        continue;
      }

      url += `/${segment}`;
      const navItem = this.navItems.find((item) => this.matchesRoute(url, item));
      const labelFromData = snapshot.data['breadcrumb'] ?? snapshot.data['title'];
      const trail = snapshot.data['breadcrumbTrail'] as Breadcrumb[] | undefined;

      if (Array.isArray(trail) && trail.length) {
        trail.forEach((crumb, index) => {
          const isLast = index === trail.length - 1;
          breadcrumbs.push({
            label: crumb.label,
            url: isLast ? undefined : (crumb.url ?? url),
          });
        });
        continue;
      }

      const label = labelFromData ?? navItem?.label ?? this.toTitleCase(segment.replace(/-/g, ' '));
      breadcrumbs.push({ label, url });
    }

    if (breadcrumbs.length) {
      breadcrumbs[breadcrumbs.length - 1].url = undefined;
    }

    return breadcrumbs;
  }

  private matchesRoute(url: string, item: NavItem): boolean {
    const normalized = url.endsWith('/') ? url.slice(0, -1) : url;
    return (
      normalized === item.route ||
      normalized.startsWith(`${item.route}/`) ||
      (item.aliases ?? []).some(
        (alias) => normalized === alias || normalized.startsWith(`${alias}/`),
      )
    );
  }

  private toTitleCase(value: string): string {
    return value
      .split(' ')
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  }
}
