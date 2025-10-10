import { CommonModule } from '@angular/common';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { animate, style, transition, trigger } from '@angular/animations';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { I18nService } from './core/i18n.service';
import { ThemeService } from './core/theme.service';

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
        MatSlideToggleModule,
        MatFormFieldModule,
        MatInputModule,
    ],
    animations: [
        trigger('routeAnimations', [
            transition('* <=> *', [
                style({ opacity: 0, transform: 'translateY(8px)' }),
                animate('220ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
            ])
        ])
    ],
    template: `
    <mat-sidenav-container class="shell-container">
      <mat-sidenav #snav [mode]="sidenavMode()" [opened]="opened()" (openedChange)="onOpenedChange($event)">
        <div class="brand">Pipelane</div>
        <mat-nav-list>
          <a mat-list-item routerLink="/analytics" routerLinkActive="active"><mat-icon>analytics</mat-icon><span>Analytics</span></a>
          <a mat-list-item routerLink="/onboarding" routerLinkActive="active"><mat-icon>settings</mat-icon><span>Onboarding</span></a>
          <a mat-list-item routerLink="/templates" routerLinkActive="active"><mat-icon>view_list</mat-icon><span>Templates</span></a>
          <a mat-list-item routerLink="/contacts" routerLinkActive="active"><mat-icon>people</mat-icon><span>Contacts</span></a>
          <a mat-list-item routerLink="/campaigns" routerLinkActive="active"><mat-icon>flag</mat-icon><span>Campaigns</span></a>
          <a mat-list-item routerLink="/settings" routerLinkActive="active"><mat-icon>tune</mat-icon><span>Settings</span></a>
        </mat-nav-list>
      </mat-sidenav>

      <mat-sidenav-content>
        <mat-toolbar class="toolbar">
          <button mat-icon-button (click)="toggleSidenav()" aria-label="Toggle sidenav"><mat-icon>menu</mat-icon></button>
          <span class="title">Pipelane Console</span>
          <span class="spacer"></span>
          <mat-form-field appearance="outline" class="search" floatLabel="never">
            <mat-icon matPrefix>search</mat-icon>
            <input matInput placeholder="Search" [(ngModel)]="search" />
          </mat-form-field>
          <mat-slide-toggle
            [checked]="theme() === 'dark'"
            (change)="toggleTheme()"
            aria-label="Toggle dark mode">
          </mat-slide-toggle>
          <select class="lang" [value]="lang()" (change)="changeLang($event)">
            <option value="en">EN</option>
            <option value="fr">FR</option>
          </select>
        </mat-toolbar>

        <main class="content" [@routeAnimations]="outlet.activatedRoute?.routeConfig?.path">
          <router-outlet #outlet="outlet"></router-outlet>
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {
  private readonly i18n = inject(I18nService);
  private readonly themeSvc = inject(ThemeService);
  private readonly bp = inject(BreakpointObserver);

  @ViewChild('snav') private sidenav?: MatSidenav;

  lang = this.i18n.lang;
  theme = this.themeSvc.theme;
  search = '';
  opened = signal<boolean>(JSON.parse(localStorage.getItem('snav_opened') ?? 'true'));
  sidenavMode = signal<'side' | 'over'>('side');

  constructor() {
    this.bp
      .observe([Breakpoints.Medium, Breakpoints.Small, Breakpoints.Handset])
      .pipe(takeUntilDestroyed())
      .subscribe((state) => {
        const isHandset = state.breakpoints[Breakpoints.Handset] || state.breakpoints[Breakpoints.Small];
        this.sidenavMode.set(isHandset ? 'over' : 'side');
        if (isHandset) {
          this.opened.set(false);
        } else {
          const stored = JSON.parse(localStorage.getItem('snav_opened') ?? 'true');
          this.opened.set(stored);
        }
      });
  }

  setLang(l: string) {
    this.i18n.setLang(l as 'en' | 'fr');
  }

  toggleTheme() {
    this.themeSvc.toggle();
  }

  changeLang(event: Event) {
    const value = (event.target as HTMLSelectElement).value;
    this.setLang(value);
  }

  toggleSidenav() {
    if (this.sidenavMode() === 'over') {
      this.sidenav?.toggle();
    } else {
      this.onOpenedChange(!this.opened());
    }
  }

  onOpenedChange(val: boolean) {
    this.opened.set(val);
    localStorage.setItem('snav_opened', JSON.stringify(val));
  }
}


