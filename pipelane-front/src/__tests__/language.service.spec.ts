import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { LanguageService } from '../app/core/i18n/language.service';

describe('LanguageService', () => {
  let service: LanguageService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), LanguageService],
    });
  });

  afterEach(() => {
    if (httpMock) {
      httpMock.verify();
    }
    localStorage.clear();
  });

  it('loads stored language and hydrates cache on init', () => {
    localStorage.setItem('pipelane_lang', 'en');
    localStorage.setItem('pipelane_lang_dict:en', JSON.stringify({ greeting: 'Cached Hello' }));

    service = TestBed.inject(LanguageService);
    httpMock = TestBed.inject(HttpTestingController);

    // Cached value is returned immediately
    expect(service.translate('greeting')).toBe('Cached Hello');

    const request = httpMock.expectOne('/assets/i18n/en.json');
    request.flush({ greeting: 'Hello' });

    expect(service.current()).toBe('en');
    expect(service.translate('greeting')).toBe('Hello');
    expect(JSON.parse(localStorage.getItem('pipelane_lang_dict:en') ?? '{}')).toEqual({
      greeting: 'Hello',
    });
  });

  it('updates dictionary, persists language and caches translations on success', () => {
    service = TestBed.inject(LanguageService);
    httpMock = TestBed.inject(HttpTestingController);

    const initRequest = httpMock.expectOne('/assets/i18n/fr.json');
    initRequest.flush({ greeting: 'Bonjour' });

    expect(service.translate('greeting')).toBe('Bonjour');

    service.set('en');

    const switchRequest = httpMock.expectOne('/assets/i18n/en.json');
    switchRequest.flush({ greeting: 'Hello' });

    expect(service.current()).toBe('en');
    expect(localStorage.getItem('pipelane_lang')).toBe('en');
    expect(JSON.parse(localStorage.getItem('pipelane_lang_dict:en') ?? '{}')).toEqual({
      greeting: 'Hello',
    });
    expect(service.translate('greeting')).toBe('Hello');
  });

  it('keeps previous language and copy when fetch fails', () => {
    service = TestBed.inject(LanguageService);
    httpMock = TestBed.inject(HttpTestingController);

    const initRequest = httpMock.expectOne('/assets/i18n/fr.json');
    initRequest.flush({ greeting: 'Bonjour' });

    service.set('en');

    const switchRequest = httpMock.expectOne('/assets/i18n/en.json');
    switchRequest.flush('error', { status: 500, statusText: 'Server Error' });

    expect(service.current()).toBe('fr');
    expect(service.translate('greeting')).toBe('Bonjour');
    expect(localStorage.getItem('pipelane_lang')).toBe('fr');
    expect(JSON.parse(localStorage.getItem('pipelane_lang_dict:fr') ?? '{}')).toEqual({
      greeting: 'Bonjour',
    });
  });
});
