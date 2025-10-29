import { HttpErrorResponse } from '@angular/common/http';
import { convertToParamMap, ActivatedRoute, Router } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { AuthService } from '../app/core/auth.service';
import { LoginPageComponent } from '../app/features/auth/login-page.component';
import { LanguageService, LangCode } from '../app/core/i18n/language.service';

class LanguageStub {
  private readonly dict: Record<string, string> = {
    'login.brand.tagline': 'Reprenez le contrôle de vos conversations.',
    'login.title': 'Connexion',
    'login.subtitle': 'Connectez-vous pour piloter vos campagnes multicanales.',
    'login.email': 'Email professionnel',
    'login.password': 'Mot de passe',
    'login.showPassword': 'Afficher le mot de passe',
    'login.hidePassword': 'Masquer le mot de passe',
    'login.remember': 'Se souvenir de moi',
    'login.forgot': 'Mot de passe oublié ?',
    'login.submit': 'Se connecter',
    'login.noAccount': 'Pas de compte ?',
    'login.createAccount': 'Créer un compte',
    'login.sso.google': 'Continuer avec Google',
    'login.sso.microsoft': 'Continuer avec Microsoft',
    'login.hero.title': 'Insights en temps réel',
    'login.hero.copy':
      'Visualisez vos indicateurs clés, automatisez les relances et concentrez-vous sur les conversations qui comptent.',
    'errors.emailRequired': "L'email est requis.",
    'errors.emailInvalid': 'Saisissez un email valide.',
    'errors.passwordRequired': 'Le mot de passe est requis.',
    'errors.invalidCredentials': 'Identifiants invalides.',
    'errors.invalidCredentials.detail':
      'Vérifiez votre email et votre mot de passe avant de réessayer.',
    'errors.serviceUnavailable': 'Service indisponible.',
    'errors.serviceUnavailable.detail': 'Réessayez dans quelques instants.',
    'language.switch': 'Changer de langue',
    'language.en': 'EN',
    'language.fr': 'FR',
    'common.or': 'ou',
    'common.close': 'Fermer',
  };

  current = signal<LangCode>('fr');
  dictionary$ = of(this.dict);
  set = jest.fn((lang: LangCode) => this.current.set(lang));
  translate = jest.fn((key: string) => this.dict[key] ?? key);
}

describe('LoginPageComponent', () => {
  let authMock: { login: jest.Mock };
  let router: Router;
  let navigateSpy: jest.SpyInstance;
  let routeStub: { snapshot: { queryParamMap: ReturnType<typeof convertToParamMap> } };
  let languageStub: LanguageStub;

  beforeEach(async () => {
    authMock = {
      login: jest.fn().mockReturnValue(
        of({
          token: 'abc.def.ghi',
          tenantId: 'tenant-1',
          role: 'admin',
        }),
      ),
    };
    routeStub = {
      snapshot: {
        queryParamMap: convertToParamMap({}),
      },
    };
    languageStub = new LanguageStub();

    await TestBed.configureTestingModule({
      imports: [LoginPageComponent, RouterTestingModule],
      providers: [
        { provide: AuthService, useValue: authMock },
        { provide: ActivatedRoute, useValue: routeStub },
        { provide: LanguageService, useValue: languageStub },
        provideNoopAnimations(),
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    navigateSpy = jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  it('shows validation errors when submitting empty form', () => {
    const fixture = TestBed.createComponent(LoginPageComponent);
    const component = fixture.componentInstance;

    fixture.detectChanges();
    component.submit();
    fixture.detectChanges();

    const errors = fixture.nativeElement.querySelectorAll('mat-error');
    expect(errors.length).toBeGreaterThan(0);
    expect(errors[0].textContent.trim()).toContain("L'email est requis");
    expect(authMock.login).not.toHaveBeenCalled();
  });

  it('calls AuthService.login and redirects to provided query param', async () => {
    routeStub.snapshot.queryParamMap = convertToParamMap({ redirect: '/inbox' });
    const fixture = TestBed.createComponent(LoginPageComponent);
    const component = fixture.componentInstance;

    component.form.setValue({
      email: 'user@example.com',
      password: 'Secret123!',
      remember: true,
    });

    fixture.detectChanges();
    component.submit();

    expect(authMock.login).toHaveBeenCalledWith('user@example.com', 'Secret123!', true);
    expect(navigateSpy).toHaveBeenCalledWith('/inbox');
  });

  it('surfaces invalid banner on 401 responses', () => {
    authMock.login.mockReturnValueOnce(
      throwError(() => new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })),
    );
    const fixture = TestBed.createComponent(LoginPageComponent);
    const component = fixture.componentInstance;

    component.form.setValue({
      email: 'bad@example.com',
      password: 'wrong',
      remember: false,
    });

    fixture.detectChanges();
    component.submit();
    fixture.detectChanges();

    const banner = fixture.nativeElement.querySelector('.alert-card.alert-error');
    expect(banner).toBeTruthy();
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
