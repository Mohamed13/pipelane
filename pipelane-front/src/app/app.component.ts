import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { I18nService } from './core/i18n.service';
import { ThemeService } from './core/theme.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
  <header class="header">
    <strong>Pipelane Console</strong>
    <a routerLink="/onboarding" routerLinkActive="active">Onboarding</a>
    <a routerLink="/templates" routerLinkActive="active">Templates</a>
    <a routerLink="/contacts" routerLinkActive="active">Contacts</a>
    <a routerLink="/campaigns" routerLinkActive="active">Campaigns</a>
    <a routerLink="/analytics" routerLinkActive="active">Analytics</a>
    <a routerLink="/settings" routerLinkActive="active">Settings</a>
    <span class="spacer"></span>
    <a routerLink="/login" routerLinkActive="active">Login</a>
    <select [value]="lang()" (change)="setLang($any($event.target).value)">
      <option value="en">EN</option>
      <option value="fr">FR</option>
    </select>
    <button (click)="toggleTheme()">{{ theme() === 'dark' ? 'Dark' : 'Classic' }}</button>
  </header>
  <main class="container">
    <router-outlet></router-outlet>
  </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {
  private i18n = inject(I18nService);
  private themeSvc = inject(ThemeService);
  lang = this.i18n.lang;
  theme = this.themeSvc.theme;
  setLang(l: string) { this.i18n.setLang(l as any); }
  toggleTheme() { this.themeSvc.toggle(); }
}
