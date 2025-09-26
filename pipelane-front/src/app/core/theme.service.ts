import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  theme = signal<'classic'|'dark'>((localStorage.getItem('theme') as any) || 'classic');
  constructor() { this.apply(); }
  toggle() {
    this.theme.set(this.theme() === 'dark' ? 'classic' : 'dark');
    this.apply();
  }
  private apply() {
    localStorage.setItem('theme', this.theme());
    document.documentElement.classList.toggle('theme-dark', this.theme() === 'dark');
  }
}

