import { readFileSync } from 'fs';
import { join } from 'path';

describe('Global theming adjustments', () => {
  const styles = readFileSync(join(__dirname, '../styles.scss'), 'utf8');

  it('sets rounded Material form field tokens', () => {
    expect(styles).toContain('--mdc-outlined-text-field-container-shape: 12px;');
    expect(styles).toContain('--mdc-filled-text-field-container-shape: 12px;');
  });

  it('customises chip radius utilities', () => {
    expect(styles).toMatch(/\.mat-mdc-standard-chip\s*{\s*border-radius: var\(--radius-pill\)/);
  });
});

describe('Campaign builder layout', () => {
  const builderStyles = readFileSync(
    join(__dirname, '../app/features/campaigns/campaign-builder.component.scss'),
    'utf8',
  );

  it('clamps segment preview to four lines', () => {
    expect(builderStyles).toMatch(/-webkit-line-clamp:\s*4/);
    expect(builderStyles).toContain('max-height: 11rem;');
  });
});
