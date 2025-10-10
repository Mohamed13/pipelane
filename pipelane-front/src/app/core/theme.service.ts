import { Injectable, signal } from '@angular/core';

type ThemeMode = 'classic' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly storageKey = 'theme';
  theme = signal<ThemeMode>(this.readInitialTheme());

  constructor() {
    this.apply();
  }

  toggle(): void {
    const next: ThemeMode = this.theme() === 'dark' ? 'classic' : 'dark';
    this.theme.set(next);
    this.apply();
  }

  private apply(): void {
    const mode = this.theme();
    localStorage.setItem(this.storageKey, mode);
    document.documentElement.classList.toggle('theme-dark', mode === 'dark');
  }

  private readInitialTheme(): ThemeMode {
    const stored = localStorage.getItem(this.storageKey);
    return stored === 'dark' ? 'dark' : 'classic';
  }
}
