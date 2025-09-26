import { Injectable, signal } from '@angular/core';

type Lang = 'en' | 'fr';

@Injectable({ providedIn: 'root' })
export class I18nService {
  lang = signal<Lang>((localStorage.getItem('lang') as Lang) || 'en');
  dict = signal<Record<string, string>>({});

  constructor() { this.load(this.lang()); }

  setLang(l: Lang) {
    localStorage.setItem('lang', l);
    this.lang.set(l);
    this.load(l);
  }

  async load(l: Lang) {
    const res = await fetch(`/assets/i18n/${l}.json`);
    this.dict.set(await res.json());
  }

  t(key: string) { return this.dict()[key] ?? key; }
}

