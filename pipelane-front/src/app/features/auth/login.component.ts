import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';

@Component({
  standalone: true,
  selector: 'pl-login',
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <div class="login-wrap">
      <h2>Login</h2>
      <form (ngSubmit)="login()" class="login-form">
        <mat-form-field appearance="outline" class="w-100">
          <mat-label>Email</mat-label>
          <input matInput [(ngModel)]="email" name="email" type="email" required />
        </mat-form-field>
        <mat-form-field appearance="outline" class="w-100">
          <mat-label>Password</mat-label>
          <input matInput [(ngModel)]="password" name="password" type="password" required />
        </mat-form-field>
        <button mat-raised-button color="primary" type="submit">Login</button>
      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private auth = inject(AuthService);
  email = 'demo@pipelane.local';
  password = 'Demo123!';
  login() {
    this.auth.login(this.email, this.password);
  }
}
