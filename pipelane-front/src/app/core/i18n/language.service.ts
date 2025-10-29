import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';

export type LangCode = 'fr' | 'en';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly http = inject(HttpClient);
  private readonly storageKey = 'pipelane_lang';
  private readonly dictionaryStoragePrefix = 'pipelane_lang_dict:';
  private readonly currentLang = signal<LangCode>(this.readInitial());
  private readonly dictionary = signal<Record<string, string>>(
    this.readDictionary(this.currentLang()),
  );
  private pendingRequest?: Subscription;

  readonly current = computed(() => this.currentLang());
  readonly current$ = toObservable(this.currentLang);
  readonly dictionary$ = toObservable(this.dictionary);

  constructor() {
    this.applyDocumentLang(this.currentLang());
    this.loadDictionary(this.currentLang());
  }

  set(lang: LangCode): void {
    if (lang === this.currentLang()) {
      return;
    }
    const previousLang = this.currentLang();
    const previousDictionary = this.dictionary();
    this.loadDictionary(lang, previousLang, previousDictionary);
  }

  translate(key: string): string {
    const dict = this.dictionary();
    return dict[key] ?? key;
  }

  private loadDictionary(
    lang: LangCode,
    rollbackLang?: LangCode,
    rollbackDictionary?: Record<string, string>,
  ): void {
    this.pendingRequest?.unsubscribe();
    const fallbackDict = rollbackDictionary ?? this.dictionary();

    this.pendingRequest = this.http
      .get<Record<string, string>>(`/assets/i18n/${lang}.json`)
      .subscribe({
        next: (dict) => {
          const resolved = dict ?? {};
          this.persist(lang);
          this.persistDictionary(lang, resolved);
          this.currentLang.set(lang);
          this.dictionary.set(resolved);
          this.applyDocumentLang(lang);
        },
        error: () => {
          if (rollbackLang) {
            this.currentLang.set(rollbackLang);
            this.applyDocumentLang(rollbackLang);
          }
          this.dictionary.set({ ...fallbackDict });
        },
      });
  }

  private readInitial(): LangCode {
    try {
      const stored = localStorage.getItem(this.storageKey);
      if (stored === 'en' || stored === 'fr') {
        return stored;
      }
    } catch {
      // ignore storage access errors (SSR, private mode)
    }
    return 'fr';
  }

  private persist(lang: LangCode): void {
    try {
      localStorage.setItem(this.storageKey, lang);
    } catch {
      // ignore storage limitations
    }
  }

  private readDictionary(lang: LangCode): Record<string, string> {
    try {
      const raw = localStorage.getItem(`${this.dictionaryStoragePrefix}${lang}`);
      if (!raw) {
        return {};
      }
      const parsed = JSON.parse(raw);
      return parsed && typeof parsed === 'object' ? parsed : {};
    } catch {
      return {};
    }
  }

  private persistDictionary(lang: LangCode, dictionary: Record<string, string>): void {
    try {
      localStorage.setItem(
        `${this.dictionaryStoragePrefix}${lang}`,
        JSON.stringify(dictionary ?? {}),
      );
    } catch {
      // ignore storage limitations
    }
  }

  private applyDocumentLang(lang: LangCode): void {
    if (typeof document !== 'undefined') {
      document.documentElement.lang = lang;
      document.documentElement.dir = 'ltr';
    }
  }
}
