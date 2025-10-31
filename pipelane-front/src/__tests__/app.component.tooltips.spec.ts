import { BreakpointObserver } from '@angular/cdk/layout';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltip } from '@angular/material/tooltip';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AppComponent } from '../app/app.component';
import { ApiService } from '../app/core/api.service';
import { LanguageService, LangCode } from '../app/core/i18n/language.service';
import { ThemeService } from '../app/core/theme.service';
import { TourService } from '../app/core/tour.service';

class BreakpointObserverStub {
  observe() {
    return of({ matches: false, breakpoints: {} });
  }
}

class LanguageStub {
  current = signal<LangCode>('en');
  dictionary$ = of<Record<string, string>>({});
  set = jest.fn((lang: LangCode) => this.current.set(lang));
  translate = jest.fn((key: string) => key);
}

class ThemeStub {
  theme = signal<'dark' | 'light'>('dark');

  toggle = () => {
    this.theme.set(this.theme() === 'dark' ? 'light' : 'dark');
  };
}

class TourStub {
  initialize = jest.fn();
  replay = jest.fn();
}

describe('AppComponent tooltips', () => {
  beforeEach(async () => {
    const apiStub = {
      runDemo: jest.fn(() => of({ triggeredAtUtc: new Date().toISOString(), messages: [] })),
    };
    const snackbarStub = {
      open: jest.fn(() => ({ onAction: () => of(void 0) })),
    };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: BreakpointObserver, useClass: BreakpointObserverStub },
        { provide: LanguageService, useClass: LanguageStub },
        { provide: ThemeService, useClass: ThemeStub },
        { provide: TourService, useClass: TourStub },
        { provide: ApiService, useValue: apiStub },
        { provide: MatSnackBar, useValue: snackbarStub },
        provideNoopAnimations(),
      ],
    }).compileComponents();
  });

  it('does not render legacy quick action buttons', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.componentInstance['breadcrumbs'].set([{ label: 'Analytics', url: '/analytics' }]);
    fixture.detectChanges();

    const quickAction = fixture.debugElement.query(
      By.css('button[data-tour="quick-action-send-test"]'),
    );
    expect(quickAction).toBeNull();
  });

  it('exposes tooltip for hunter navigation item', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const navItem = fixture.debugElement.query(By.css('a[data-tour="nav-hunter"]'));
    const tooltip = navItem.injector.get(MatTooltip);
    expect(tooltip.message).toBe('Lead Hunter AI');
  });
});
