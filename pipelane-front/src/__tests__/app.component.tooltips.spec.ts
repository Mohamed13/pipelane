import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { BreakpointObserver } from '@angular/cdk/layout';
import { signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { MatTooltip } from '@angular/material/tooltip';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { AppComponent } from '../app/app.component';
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
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: BreakpointObserver, useClass: BreakpointObserverStub },
        { provide: I18nService, useClass: I18nStub },
        { provide: ThemeService, useClass: ThemeStub },
        { provide: TourService, useClass: TourStub },
        provideNoopAnimations(),
      ],
    }).compileComponents();
  });

  it('exposes tooltip for quick action button', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.componentInstance['breadcrumbs'].set([{ label: 'Analytics', url: '/analytics' }]);
    fixture.detectChanges();

    const quickAction = fixture.debugElement.query(
      By.css('button[data-tour="quick-action-send-test"]'),
    );
    const tooltip = quickAction.injector.get(MatTooltip);
    expect(tooltip.message).toBe('Send yourself a test message');
  });

  it('exposes tooltip for onboarding navigation item', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const navItem = fixture.debugElement.query(By.css('a[data-tour="nav-onboarding"]'));
    const tooltip = navItem.injector.get(MatTooltip);
    expect(tooltip.message).toBe('Connect your channels');
  });
});
