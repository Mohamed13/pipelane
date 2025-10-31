import { Injectable, signal } from '@angular/core';

type ThemeMode = 'dark' | 'light';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly storageKey = 'theme';
  theme = signal<ThemeMode>(this.readInitialTheme());

  constructor() {
    this.apply();
  }

  toggle(): void {
    const next: ThemeMode = this.theme() === 'dark' ? 'light' : 'dark';
    this.theme.set(next);
    this.apply();
  }

  set(mode: ThemeMode): void {
    this.theme.set(mode);
    this.apply();
  }

  private apply(): void {
    const mode = this.theme();
    localStorage.setItem(this.storageKey, mode);
    document.documentElement.classList.toggle('theme-light', mode === 'light');
    document.documentElement.classList.toggle('theme-dark', mode === 'dark');
  }

  private readInitialTheme(): ThemeMode {
    const stored = localStorage.getItem(this.storageKey) as ThemeMode | null;
    if (stored === 'light' || stored === 'dark') {
      return stored;
    }
    const prefersDark = window.matchMedia?.('(prefers-color-scheme: dark)').matches;
    return prefersDark ? 'dark' : 'light';
  }
}
