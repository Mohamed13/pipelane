import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

interface TokenSwatch {
  name: string;
  variable: string;
}

@Component({
  standalone: true,
  selector: 'app-design-playground',
  imports: [CommonModule, MatCardModule, MatButtonModule, MatChipsModule, MatIconModule],
  templateUrl: './design-playground.component.html',
  styleUrls: ['./design-playground.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DesignPlaygroundComponent {
  readonly window = typeof window !== 'undefined' ? window : null;
  readonly colors: TokenSwatch[] = [
    { name: 'Background', variable: '--color-bg' },
    { name: 'Surface', variable: '--color-surface' },
    { name: 'Surface strong', variable: '--color-surface-strong' },
    { name: 'Primary', variable: '--color-primary' },
    { name: 'Secondary', variable: '--color-secondary' },
    { name: 'Accent', variable: '--color-accent' },
    { name: 'Success', variable: '--color-success' },
    { name: 'Warn', variable: '--color-warn' },
    { name: 'Error', variable: '--color-error' },
  ];

  readonly glassCards = [
    {
      title: 'Glass card',
      body: 'Blurred surface with neon focus ring. Great for hero metrics or summary cards.',
      icon: 'auto_awesome',
    },
    {
      title: 'Badge & chip',
      body: 'Tokens for inline status or KPI chips. Combine with gradients for emphasis.',
      icon: 'offline_bolt',
    },
  ];

  readonly animations = [
    { label: 'Fade up', className: 'fade-up is-visible' },
    { label: 'Scale in', className: 'scale-in is-visible' },
    { label: 'Shimmer', className: 'shimmer shimmer-block' },
  ];
}
