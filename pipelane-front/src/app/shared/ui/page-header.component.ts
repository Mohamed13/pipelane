import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="page-header">
      <div class="page-header__meta">
        <span *ngIf="eyebrow" class="eyebrow">{{ eyebrow }}</span>
        <h1 class="page-header__title">{{ title }}</h1>
        <p *ngIf="subtitle" class="page-header__subtitle text-muted">{{ subtitle }}</p>
      </div>
      <div class="page-header__actions">
        <ng-content select="[page-actions]"></ng-content>
      </div>
    </section>
  `,
  styles: [
    `
      .page-header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: var(--gap-lg);
        padding-block: var(--gap-lg);
        margin-block-end: var(--gap-lg);
        border-block-end: 1px solid rgba(117, 240, 255, 0.12);
      }

      .page-header__meta {
        display: flex;
        flex-direction: column;
        gap: var(--gap-xs);
        max-width: 640px;
      }

      .page-header__title {
        margin: 0;
      }

      .page-header__subtitle {
        margin: 0;
        max-width: 720px;
      }

      .page-header__actions {
        display: flex;
        align-items: center;
        gap: var(--gap);
        flex-wrap: wrap;
      }

      .page-header__actions:empty {
        display: none;
      }

      @media (max-width: 960px) {
        .page-header {
          flex-direction: column;
          align-items: flex-start;
        }

        .page-header__actions {
          width: 100%;
          justify-content: flex-start;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageHeaderComponent {
  @Input({ required: true }) title!: string;
  @Input() subtitle?: string;
  @Input() eyebrow?: string;
}
