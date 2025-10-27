import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { isDevMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { AppComponent } from './app/app.component';
import { routes } from './app/app.routes';
import { authInterceptor } from './app/core/auth.interceptor';

if (isDevMode()) {
  const blockedFragment = 'chrome-extension://';
  const originalError = console.error.bind(console);
  const originalWarn = console.warn.bind(console);

  console.error = (...args: unknown[]) => {
    if (args.some((arg) => typeof arg === 'string' && arg.includes(blockedFragment))) {
      return;
    }
    originalError(...args);
  };

  console.warn = (...args: unknown[]) => {
    if (args.some((arg) => typeof arg === 'string' && arg.includes(blockedFragment))) {
      return;
    }
    originalWarn(...args);
  };
}

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes, withComponentInputBinding()),
    provideAnimations(),
  ],
}).catch((err) => console.error(err));
