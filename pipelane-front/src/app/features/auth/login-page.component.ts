import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../core/auth.service';
import { LanguageService, LangCode } from '../../core/i18n/language.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PipelaneLogoComponent } from '../../shared/ui/pipelane-logo/pipelane-logo.component';

type LoginErrorState = 'invalid' | 'server' | null;

@Component({
  standalone: true,
  selector: 'pl-login-page',
  templateUrl: './login-page.component.html',
  styleUrls: ['./login-page.component.scss'],
  imports: [
    ReactiveFormsModule,
    RouterModule,
    CommonModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatCardModule,
    MatDividerModule,
    MatProgressSpinnerModule,
    PipelaneLogoComponent,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly language = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly error = signal<LoginErrorState>(null);
  readonly showPassword = signal(false);
  readonly lang = this.language.current;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
    remember: [false],
  });

  togglePasswordVisibility(): void {
    this.showPassword.update((value) => !value);
  }

  setLanguage(lang: LangCode): void {
    this.language.set(lang);
  }

  submit(): void {
    this.error.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { email, password, remember } = this.form.getRawValue();
    this.loading.set(true);
    const _loginSubscription = this.auth
      .login(email, password, remember)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: () => {
          const redirect = this.sanitiseRedirect(this.route.snapshot.queryParamMap.get('redirect'));
          this.router.navigateByUrl(redirect ?? '/analytics');
        },
        error: (err: unknown) => {
          const httpError = err instanceof HttpErrorResponse ? err : null;
          if (httpError?.status === 401 || httpError?.status === 403) {
            this.error.set('invalid');
            return;
          }
          this.error.set('server');
        },
      });
  }

  get email(): FormControl<string> {
    return this.form.controls.email;
  }

  get password(): FormControl<string> {
    return this.form.controls.password;
  }

  private sanitiseRedirect(raw: string | null): string | null {
    if (!raw) {
      return null;
    }
    return raw.startsWith('/') ? raw : null;
  }
}
