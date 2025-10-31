import { animate, animation, style, transition, trigger, useAnimation } from '@angular/animations';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ActivatedRoute,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { NavigationEnd } from '@angular/router';
import { EMPTY, catchError, filter, switchMap, take, tap } from 'rxjs';

import { ApiService } from './core/api.service';
import { environment } from './core/environment';
import { HelpCenterComponent } from './core/help-center/help-center.component';
import { LanguageService } from './core/i18n/language.service';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { IconService } from './core/icon.service';
import { CommandPaletteComponent } from './core/search/command-palette.component';
import { ThemeService } from './core/theme.service';
import { TourService } from './core/tour.service';
import { PipelaneLogoComponent } from './shared/ui/pipelane-logo/pipelane-logo.component';

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
type QuickActionKey = 'send-test' | 'create-campaign' | 'import-contacts' | 'launch-demo';

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
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    MatSnackBarModule,
    MatDialogModule,
    PipelaneLogoComponent,
    TranslatePipe,
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
        [class.nav-shell--collapsed]="collapsed() && !isHandset()"
        [mode]="sidenavMode()"
        [opened]="opened()"
        (openedChange)="onOpenedChange($event)"
      >
        <div class="rail-head">
          <div class="brand">
            <app-pipelane-logo variant="compact" [inline]="true"></app-pipelane-logo>
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
            [disableRipple]="true"
          >
            <mat-icon aria-hidden="true" class="nav-icon">{{ item.icon }}</mat-icon>
            <span class="nav-label">{{ item.label }}</span>
          </a>
        </mat-nav-list>
        <div class="rail-footer">
          <button
            *ngIf="!demoMode"
            mat-stroked-button
            color="primary"
            (click)="onQuickAction('create-campaign')"
            class="launch-button"
          >
            <mat-icon aria-hidden="true">rocket_launch</mat-icon>
            <span>Launch campaign</span>
          </button>
          <button
            *ngIf="demoMode"
            mat-stroked-button
            color="primary"
            (click)="triggerDemoRun()"
            [disabled]="demoRunning()"
            class="launch-button launch-button--demo"
          >
            <mat-icon aria-hidden="true">{{ demoRunning() ? 'hourglass_top' : 'bolt' }}</mat-icon>
            <span>{{ demoRunning() ? 'Launching...' : 'Launch demo' }}</span>
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
          <div class="demo-mode-chip" *ngIf="demoMode">
            <mat-icon aria-hidden="true">bolt</mat-icon>
            <span>Demo mode</span>
          </div>
          <span class="spacer"></span>
          <button
            mat-icon-button
            class="search-trigger neon-outline"
            aria-label="Ouvrir la palette de commande"
            matTooltip="Rechercher (Ctrl+K)"
            (click)="openCommandPalette()"
          >
            <mat-icon aria-hidden="true">search</mat-icon>
          </button>
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
            [attr.aria-label]="'language.switch' | translate"
            [matTooltip]="'language.switch' | translate"
          >
            <mat-icon aria-hidden="true">translate</mat-icon>
          </button>
          <mat-menu #langMenu="matMenu">
            <button mat-menu-item (click)="setLang('en')">
              <span>{{ 'language.label.en' | translate }}</span>
              <mat-icon *ngIf="lang() === 'en'">check</mat-icon>
            </button>
            <button mat-menu-item (click)="setLang('fr')">
              <span>{{ 'language.label.fr' | translate }}</span>
              <mat-icon *ngIf="lang() === 'fr'">check</mat-icon>
            </button>
          </mat-menu>
          <button
            mat-icon-button
            aria-label="Open help center"
            matTooltip="Help & shortcuts (Shift + ?)"
            class="neon-outline"
            (click)="openHelpCenter()"
          >
            <mat-icon aria-hidden="true">help</mat-icon>
          </button>
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
        width: 240px;
        border-right: 1px solid rgba(117, 240, 255, 0.12);
        background: color-mix(in srgb, var(--md-sys-color-surface-container-low) 92%, transparent);
        backdrop-filter: blur(18px);
        display: flex;
        flex-direction: column;
        transition: width 220ms ease;
      }

      .nav-shell--collapsed {
        width: 76px;
      }

      .rail-head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: var(--gap);
      }

      .brand {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding-inline: var(--gap-sm);
      }

      .brand app-pipelane-logo,
      .brand .pl-logo {
        width: clamp(132px, 18vw, 200px);
      }

      .nav-rail {
        display: flex;
        flex-direction: column;
        gap: var(--gap-sm);
        padding: 0 var(--gap);
        overflow-y: auto;
      }

      .nav-rail--collapsed {
        align-items: center;
        padding-inline: var(--gap-sm);
      }

      .nav-rail--collapsed .nav-label {
        display: none;
      }

      .nav-rail--collapsed .mat-mdc-list-item {
        justify-content: center;
      }

      .nav-rail .mat-mdc-list-item {
        border-radius: var(--radius-lg);
        transition:
          background 180ms ease,
          transform 180ms ease;
      }

      .nav-rail .mat-mdc-list-item:hover {
        background: rgba(117, 240, 255, 0.08);
      }

      .nav-rail .mat-mdc-list-item.is-active {
        background: rgba(117, 240, 255, 0.18);
        border: 1px solid rgba(117, 240, 255, 0.32);
        transform: translateX(4px);
      }

      .nav-icon {
        margin-right: var(--gap);
        font-size: 1.4rem;
      }

      .nav-rail--collapsed .nav-icon {
        margin-right: 0;
      }

      .rail-footer {
        margin-top: auto;
        padding: var(--gap);
        display: flex;
        flex-direction: column;
        gap: var(--gap);
      }

      .launch-button {
        width: 100%;
        border-radius: var(--radius-pill);
        border-color: rgba(117, 240, 255, 0.28) !important;
        backdrop-filter: blur(8px);
      }

      .launch-button--demo {
        border-color: rgba(96, 247, 163, 0.6) !important;
        color: var(--md-sys-color-tertiary);
      }

      .launch-button--demo[disabled] {
        opacity: 0.65;
      }

      .shell-container {
        height: 100vh;
        background:
          radial-gradient(circle at top, rgba(117, 240, 255, 0.08), transparent 55%),
          var(--color-bg);
        color: var(--color-text);
      }

      .toolbar {
        position: sticky;
        top: 0;
        z-index: 14;
        min-height: 64px;
        display: flex;
        align-items: center;
        gap: var(--gap);
        padding-inline: clamp(16px, 4vw, 32px);
        background: color-mix(in srgb, var(--md-sys-color-surface) 88%, transparent);
        border-bottom: 1px solid rgba(117, 240, 255, 0.14);
        backdrop-filter: blur(12px);
      }

      .route-title {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }

      .title {
        font-weight: 600;
        letter-spacing: 0.02em;
      }

      .demo-mode-chip {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        border-radius: var(--radius-pill);
        padding: 0.35rem 0.75rem;
        border: 1px solid rgba(96, 247, 163, 0.35);
        background: rgba(96, 247, 163, 0.12);
        font-size: 0.75rem;
        text-transform: uppercase;
        letter-spacing: 0.12em;
        color: var(--md-sys-color-on-surface);
      }

      .search-trigger {
        margin-right: var(--gap-sm);
        color: var(--md-sys-color-on-surface-variant);
      }

      .search-trigger:hover,
      .search-trigger:focus-visible {
        color: var(--md-sys-color-on-surface);
      }

      .theme-toggle {
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
        border-radius: var(--radius-pill);
        padding: 0.35rem 0.75rem;
        border: 1px solid rgba(117, 240, 255, 0.16);
        cursor: pointer;
      }

      .skip-link {
        position: absolute;
        top: -100px;
        left: 0;
        background: var(--md-sys-color-primary);
        color: var(--md-sys-color-on-primary);
        padding: 0.75rem 1rem;
        border-radius: var(--radius-pill);
        transition: top 0.2s ease;
        z-index: 999;
      }

      .skip-link:focus-visible {
        top: 16px;
      }

      .header-band {
        display: flex;
        align-items: center;
        gap: var(--gap);
        margin: var(--gap-lg) clamp(16px, 4vw, 32px) 0;
        padding: var(--gap) clamp(16px, 4vw, 32px);
        border-radius: var(--radius-lg);
        border: 1px solid rgba(117, 240, 255, 0.12);
        background: color-mix(in srgb, var(--md-sys-color-surface-container) 90%, transparent);
      }

      .breadcrumb-trail {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        color: var(--md-sys-color-on-surface-variant);
        font-size: 0.9rem;
        letter-spacing: 0.02em;
      }

      .breadcrumb-trail mat-icon {
        font-size: 18px;
        opacity: 0.6;
      }

      .content {
        padding: clamp(24px, 5vw, 48px);
        min-height: calc(100vh - 64px);
        background:
          radial-gradient(circle at 20% 10%, rgba(117, 240, 255, 0.12), transparent 45%),
          radial-gradient(circle at 80% 0%, rgba(155, 140, 255, 0.16), transparent 55%),
          var(--color-bg);
      }

      .spacer {
        flex: 1;
      }

      @media (max-width: 960px) {
        .nav-shell {
          width: 220px;
        }

        .header-band {
          margin: var(--gap-lg) var(--gap);
          padding: var(--gap);
        }

        .toolbar {
          padding-inline: var(--gap);
        }
      }

      @media (max-width: 720px) {
        .nav-shell {
          width: 200px;
        }

        .nav-shell--collapsed {
          width: 76px;
        }

        .toolbar {
          gap: var(--gap-sm);
        }

        .content {
          padding: var(--gap-lg) var(--gap);
        }
      }

      @media (max-width: 540px) {
        .toolbar {
          padding-inline: var(--gap);
        }

        .content {
          padding: var(--gap-lg) var(--gap);
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  private readonly language = inject(LanguageService);
  private readonly themeSvc = inject(ThemeService);
  private readonly _icons = inject(IconService);
  private readonly bp = inject(BreakpointObserver);
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly tour = inject(TourService);
  private readonly api = inject(ApiService);
  private readonly snackbar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  @ViewChild('snav') private sidenav?: MatSidenav;

  readonly navItems: NavItem[] = [
    {
      label: 'Hunter',
      route: '/hunter',
      icon: 'travel_explore',
      tooltip: 'Lead Hunter AI',
      tourKey: 'nav-hunter',
      aliases: ['/lists'],
    },
    {
      label: 'Cadences',
      route: '/campaigns',
      icon: 'schema',
      tooltip: 'Design and launch cadences',
      tourKey: 'nav-cadences',
      aliases: ['/campaigns/new', '/campaigns'],
    },
    {
      label: 'Inbox',
      route: '/inbox',
      icon: 'forum',
      tooltip: 'Unified messaging inbox',
      tourKey: 'nav-inbox',
      aliases: ['/prospecting/inbox'],
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
      label: 'Analytics',
      route: '/analytics',
      icon: 'monitoring',
      tooltip: 'Analytics overview',
      tourKey: 'nav-analytics',
    },
    { label: 'Settings', route: '/settings', icon: 'tune', tooltip: 'Console settings' },
  ];

  readonly demoMode = environment.DEMO_MODE;
  lang = this.language.current;
  theme = this.themeSvc.theme;
  opened = signal<boolean>(this.readBool('snav_opened', true));
  collapsed = signal<boolean>(this.readBool('snav_collapsed', false));
  sidenavMode = signal<'side' | 'over'>('side');
  isHandset = signal<boolean>(false);
  currentUrl = signal<string>(this.router.url);
  breadcrumbs = signal<Breadcrumb[]>([]);
  demoRunning = signal(false);
  activeNavTitle = computed(() => {
    const url = this.currentUrl();
    const nav = this.navItems.find((item) => this.matchesRoute(url, item));
    return nav?.label ?? 'Pipelane Console';
  });
  private shortcutBuffer: string | null = null;
  private shortcutTimer: number | null = null;

  constructor() {
    const _breakpointSubscription = this.bp
      .observe([Breakpoints.Medium, Breakpoints.Small, Breakpoints.Handset])
      .pipe(takeUntilDestroyed())
      .subscribe((state) => {
        const handset =
          Boolean(state.breakpoints[Breakpoints.Handset]) ||
          Boolean(state.breakpoints[Breakpoints.Small]);
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

    const _routerSubscription = this.router.events
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

  setLang(language: 'en' | 'fr'): void {
    this.language.set(language);
  }

  toggleTheme(): void {
    this.themeSvc.toggle();
  }

  toggleSidenav(): void {
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

  onOpenedChange(val: boolean): void {
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
      case 'launch-demo':
        this.triggerDemoRun();
        break;
    }
  }

  triggerDemoRun(): void {
    if (!this.demoMode || this.demoRunning()) {
      return;
    }

    this.demoRunning.set(true);
    const _demoRunSubscription = this.api
      .runDemo()
      .pipe(
        tap((response) => {
          this.demoRunning.set(false);
          const primary = response.messages[0];
          if (primary?.contactId) {
            this.router.navigate(['/conversations', primary.contactId], {
              queryParams: { demo: 'true' },
            });
          }
        }),
        switchMap(() => {
          const ref = this.snackbar.open('Demo launched - analytics refreshed.', 'View analytics', {
            duration: 6000,
          });
          return ref.onAction().pipe(
            take(1),
            tap(() => {
              this.router.navigate(['/analytics']);
            }),
          );
        }),
        catchError(() => {
          this.demoRunning.set(false);
          return EMPTY;
        }),
        takeUntilDestroyed(),
      )
      .subscribe();
  }

  @HostListener('document:keydown', ['$event'])
  handleShortcut(event: KeyboardEvent): void {
    if (!event || typeof event.key !== 'string' || event.key.length === 0) {
      this.resetShortcutBuffer();
      return;
    }

    const target = event.target instanceof HTMLElement ? event.target : null;
    const tag = typeof target?.tagName === 'string' ? target.tagName.toLowerCase() : '';
    const isEditable = target?.isContentEditable ?? false;
    const isFormField =
      !!tag && (tag === 'input' || tag === 'textarea' || tag === 'select' || isEditable);

    const key = event.key.toLowerCase();

    if ((event.ctrlKey || event.metaKey) && key === 'k') {
      event.preventDefault();
      this.openCommandPalette();
      this.resetShortcutBuffer();
      return;
    }

    if (event.shiftKey && event.key === '?') {
      event.preventDefault();
      this.openHelpCenter();
      this.resetShortcutBuffer();
      return;
    }

    if (isFormField || event.altKey || event.ctrlKey || event.metaKey) {
      this.resetShortcutBuffer();
      return;
    }

    const setBuffer = (value: string): void => {
      this.shortcutBuffer = value;
      if (this.shortcutTimer !== null && typeof window !== 'undefined') {
        window.clearTimeout(this.shortcutTimer);
      }
      if (typeof window !== 'undefined') {
        this.shortcutTimer = window.setTimeout(() => this.resetShortcutBuffer(), 800);
      }
    };

    if (this.shortcutBuffer === 'g' && key === 'h') {
      event.preventDefault();
      this.router.navigate(['/hunter']);
      this.resetShortcutBuffer();
      return;
    }

    if (this.shortcutBuffer === 'g' && key === 'a') {
      event.preventDefault();
      this.router.navigate(['/analytics']);
      this.resetShortcutBuffer();
      return;
    }

    if (this.shortcutBuffer === 'n' && key === 'c') {
      event.preventDefault();
      this.onQuickAction('create-campaign');
      this.resetShortcutBuffer();
      return;
    }

    setBuffer(key);
  }

  private resetShortcutBuffer(): void {
    if (this.shortcutTimer !== null && typeof window !== 'undefined') {
      window.clearTimeout(this.shortcutTimer);
    }
    this.shortcutTimer = null;
    this.shortcutBuffer = null;
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

  openCommandPalette(): void {
    const alreadyOpen = this.dialog.openDialogs.some(
      (ref) => ref.componentInstance instanceof CommandPaletteComponent,
    );
    if (alreadyOpen) {
      return;
    }
    this.dialog.open(CommandPaletteComponent, {
      panelClass: 'command-palette-dialog',
      width: '720px',
      maxWidth: '92vw',
      autoFocus: false,
      restoreFocus: false,
    });
  }

  openHelpCenter(): void {
    this.dialog.open(HelpCenterComponent, {
      panelClass: 'help-center-dialog',
    });
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
          const crumbLabel =
            typeof crumb?.label === 'string' && crumb.label.trim().length
              ? crumb.label.trim()
              : this.toTitleCase(segment.replace(/-/g, ' '));
          const breadcrumb: Breadcrumb = { label: crumbLabel };
          if (!isLast) {
            breadcrumb.url = crumb.url ?? url;
          }
          breadcrumbs.push(breadcrumb);
        });
        continue;
      }

      const labelCandidate =
        (typeof labelFromData === 'string' && labelFromData.trim().length
          ? labelFromData.trim()
          : null) ??
        (typeof navItem?.label === 'string' && navItem.label.trim().length
          ? navItem.label.trim()
          : null);
      const label = labelCandidate ?? this.toTitleCase(segment.replace(/-/g, ' '));
      breadcrumbs.push({ label, url });
    }

    if (breadcrumbs.length) {
      const last = breadcrumbs[breadcrumbs.length - 1];
      if (last) {
        delete last.url;
      }
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
