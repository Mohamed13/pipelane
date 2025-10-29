import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

let instanceCounter = 0;

@Component({
  selector: 'app-pipelane-logo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="pl-logo" [attr.aria-label]="ariaLabel" role="img">
      <ng-container *ngIf="inline; else imgTag">
        <ng-container [ngSwitch]="variant">
          <svg
            *ngSwitchCase="'compact'"
            class="pl-logo__svg"
            width="640"
            height="120"
            viewBox="0 0 640 120"
            xmlns="http://www.w3.org/2000/svg"
            role="presentation"
            aria-hidden="true"
          >
            <defs>
              <linearGradient [attr.id]="compactGradientId" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stop-color="#28C0B0" />
                <stop offset="100%" stop-color="#7CE4D9" />
              </linearGradient>
            </defs>
            <g transform="translate(0,20)">
              <path
                d="M10 40 C60 10, 90 70, 140 40 S220 10, 260 40"
                stroke-linecap="round"
                stroke-width="6"
                fill="none"
                [attr.stroke]="'url(#' + compactGradientId + ')'"
              ></path>
              <circle cx="60" cy="25" r="5" fill="#28C0B0"></circle>
              <circle cx="140" cy="40" r="5" fill="#28C0B0"></circle>
              <circle cx="220" cy="25" r="5" fill="#28C0B0"></circle>
              <path d="M260 40 l14 -8 v16 z" fill="#7CE4D9"></path>
            </g>
            <g transform="translate(300,74)">
              <text
                x="0"
                y="0"
                fill="#0D0F12"
                font-family="'Space Grotesk', Inter, system-ui, sans-serif"
                font-weight="700"
                font-size="44"
              >
                Pipelane
              </text>
              <text
                x="0"
                y="24"
                fill="#4B5563"
                font-family="Inter, system-ui, sans-serif"
                font-weight="600"
                font-size="16"
                letter-spacing="1.4"
              >
                QUALIFY • RELAUNCH • BOOK
              </text>
            </g>
          </svg>
          <svg
            *ngSwitchDefault
            class="pl-logo__svg"
            width="680"
            height="140"
            viewBox="0 0 680 140"
            xmlns="http://www.w3.org/2000/svg"
            role="presentation"
            aria-hidden="true"
          >
            <defs>
              <linearGradient [attr.id]="defaultGradientId" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stop-color="#28C0B0" />
                <stop offset="100%" stop-color="#7CE4D9" />
              </linearGradient>
            </defs>
            <rect width="680" height="140" rx="16" fill="#0D0F12"></rect>
            <g transform="translate(20,28)">
              <path
                d="M10 40 C60 10, 90 70, 140 40 S220 10, 260 40"
                stroke-linecap="round"
                stroke-width="6"
                fill="none"
                [attr.stroke]="'url(#' + defaultGradientId + ')'"
              ></path>
              <circle cx="60" cy="25" r="5" fill="#28C0B0"></circle>
              <circle cx="140" cy="40" r="5" fill="#28C0B0"></circle>
              <circle cx="220" cy="25" r="5" fill="#28C0B0"></circle>
              <path d="M260 40 l14 -8 v16 z" fill="#7CE4D9"></path>
            </g>
            <g transform="translate(320,85)">
              <text
                x="0"
                y="0"
                fill="#E8FCF8"
                font-family="'Space Grotesk', Inter, system-ui, sans-serif"
                font-weight="700"
                font-size="44"
              >
                Pipelane
              </text>
              <text
                x="0"
                y="26"
                fill="#6B7280"
                font-family="Inter, system-ui, sans-serif"
                font-weight="600"
                font-size="16"
                letter-spacing="1.4"
              >
                QUALIFY • RELAUNCH • BOOK
              </text>
            </g>
          </svg>
        </ng-container>
      </ng-container>
      <ng-template #imgTag>
        <img
          [src]="
            variant === 'compact'
              ? 'assets/brand/pipelane-compact.svg'
              : 'assets/brand/pipelane-logo.svg'
          "
          [alt]="ariaLabel"
          decoding="async"
          loading="eager"
        />
      </ng-template>
    </div>
  `,
  styles: [
    `
      .pl-logo {
        display: inline-block;
        width: clamp(180px, 22vw, 320px);
      }
      .pl-logo__svg,
      .pl-logo img {
        width: 100%;
        height: auto;
        display: block;
      }
    `,
  ],
})
export class PipelaneLogoComponent {
  @Input() variant: 'default' | 'compact' = 'default';
  @Input() inline = true;
  @Input() ariaLabel = 'Pipelane logo';

  private readonly idSuffix = ++instanceCounter;
  readonly defaultGradientId = `plFlowDefault${this.idSuffix}`;
  readonly compactGradientId = `plFlowCompact${this.idSuffix}`;
}
