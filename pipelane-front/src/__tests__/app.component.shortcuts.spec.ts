import { BreakpointObserver } from '@angular/cdk/layout';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AppComponent } from '../app/app.component';
import { ApiService } from '../app/core/api.service';
import { I18nService } from '../app/core/i18n.service';
import { ThemeService } from '../app/core/theme.service';
import { TourService } from '../app/core/tour.service';

class BreakpointObserverStub {
  observe() {
    return of({ matches: false, breakpoints: {} });
  }
}

class I18nStub {
  lang = signal<'en' | 'fr'>('en');
  dict = signal<Record<string, string>>({});
  setLang = jest.fn();
  t(key: string) {
    return key;
  }
}

class ThemeStub {
  theme = signal<'dark' | 'light'>('dark');
  toggle = () => undefined;
}

class TourStub {
  initialize = jest.fn();
  replay = jest.fn();
}

describe('AppComponent keyboard shortcuts', () => {
  beforeEach(async () => {
    const apiStub = { runDemo: jest.fn(() => of({ triggeredAtUtc: '', messages: [] })) };
    const snackbarStub = { open: jest.fn(() => ({ onAction: () => of(void 0) })) };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: BreakpointObserver, useClass: BreakpointObserverStub },
        { provide: I18nService, useClass: I18nStub },
        { provide: ThemeService, useClass: ThemeStub },
        { provide: TourService, useClass: TourStub },
        { provide: ApiService, useValue: apiStub },
        { provide: MatSnackBar, useValue: snackbarStub },
        { provide: MatDialog, useValue: { open: jest.fn() } },
        provideNoopAnimations(),
      ],
    }).compileComponents();
  });

  it('resets buffer when keyboard event key is undefined', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const component = fixture.componentInstance;
    const shortcuts = component as unknown as { shortcutBuffer: string };

    shortcuts.shortcutBuffer = 'g';
    expect(() => component.handleShortcut({ key: undefined } as unknown as KeyboardEvent)).not.toThrow();
    expect(shortcuts.shortcutBuffer).toBeNull();
  });

  it('ignores shortcuts originating from inputs', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const component = fixture.componentInstance;
    const shortcuts = component as unknown as { shortcutBuffer: string };
    const focusSpy = jest.spyOn(component as unknown as { focusGlobalSearch: () => void }, 'focusGlobalSearch');

    const input = document.createElement('input');
    document.body.appendChild(input);
    shortcuts.shortcutBuffer = '';

    const keyboardEvent = new KeyboardEvent('keydown', { key: 'g', bubbles: true });
    Object.defineProperty(keyboardEvent, 'target', { value: input, writable: false });

    component.handleShortcut(keyboardEvent);

    expect(shortcuts.shortcutBuffer).toBeNull();
    expect(focusSpy).not.toHaveBeenCalled();

    input.remove();
  });
});
