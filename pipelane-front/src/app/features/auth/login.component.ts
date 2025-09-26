import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth.service';

@Component({
  standalone: true,
  selector: 'pl-login',
  imports: [FormsModule],
  template: `
  <h2>Login</h2>
  <form (ngSubmit)="login()">
    <input placeholder="Email" [(ngModel)]="email" name="email" />
    <input placeholder="Password" [(ngModel)]="password" name="password" type="password" />
    <button type="submit">Login</button>
  </form>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginComponent {
  private auth = inject(AuthService);
  email = 'demo@example.com';
  password = 'password';
  login(){ this.auth.login(this.email, this.password); }
}

