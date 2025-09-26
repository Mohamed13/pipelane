import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  standalone: true,
  selector: 'pl-settings',
  template: `
  <h2>Settings</h2>
  <p class="muted">Tenant settings, quiet hours, default fallback policy (placeholder).</p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingsComponent {}

