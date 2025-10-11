You are a senior front-end engineer. We have an Astro + Tailwind marketing site in `pipelane-marketing/`. 
Do a global pass to FIX CONTRAST and ADD ILLUSTRATIVE MEDIA (images + icons) while preserving the futuristic aesthetic.

### Objectives
1) Enforce WCAG 2.2 AA contrast:
   - Body text ≥ 4.5:1, large text (≥ 18.66px/14pt or bold) ≥ 3:1.
   - No white text on light/low-opacity surfaces; no dark text on dark backgrounds.
2) Improve legibility on gradients, media, and glass panels with scrims/overlays.
3) Add tasteful product- and feature-related images and icons to visually support the copy.
4) Keep performance high: responsive images, lazy loading, minimal blocking.

### Tasks (exact)
- In `src/styles/tokens.css` or Tailwind config, define explicit **foreground** tokens for each surface:
  - `--bg`, `--surface`, `--surface-strong`, `--text`, `--text-muted`, `--primary`, `--primary-foreground`, `--on-surface`, `--on-surface-strong`.
  - Dark theme by default; also ensure proper mappings for light theme.
- Update `tailwind.config.*`:
  - Extend `colors` with `bg`, `surface`, `surfaceStrong`, `text`, `textMuted`, `primary`, `primaryFg`, and `onSurface`, `onSurfaceStrong`.
  - Provide utility classes `.on-surface`, `.on-surface-strong`, `.text-elevated` that map to the correct tokens.
- Create a reusable **scrim** utility:
  - CSS class `.scrim` applying a subtle linear-gradient overlay (e.g. `linear-gradient(180deg, rgba(0,0,0,.56), rgba(0,0,0,.24) 50%, rgba(0,0,0,0))`) for images/gradients to guarantee contrast under headings/buttons.
  - Use `.scrim` on hero, section headers with imagery, and any glass panel that has text on top of imagery/gradient.
- Replace semi-transparent “glass” panels that cause poor contrast:
  - Increase backdrop blur and reduce transparency: surface backgrounds at least `rgba(255,255,255,0.06)` dark theme, or `rgba(0,0,0,0.06)` in light theme.
  - Ensure text on those panels uses `.on-surface-strong`.
- Add **text shadow** only for large hero headers if still borderline; avoid for body text.
- Normalize headings and body:
  - Headings use `text-onSurfaceStrong` equivalent.
  - Body uses `text-onSurface`.
  - Muted text uses `text-textMuted` only on sufficiently strong surfaces (not directly on media).
- Buttons:
  - Primary button: background = `primary`, text = `primaryFg`. On hover, slightly brighten/darken but keep contrast ≥ 3:1 for large text.
  - Ghost/secondary buttons must sit on surfaces that guarantee ≥ 4.5:1.

### Images & Icons
- Introduce `src/assets/img/` with 6–8 illustrative images (placeholders acceptable): 
  - 2 “product/console” style mockups, 2 “automation/flow” illustrations, 2 “analytics/dashboards”, 1 “integrations” collage.
  - Use Astro `<Image />` for responsive sizes and lazy loading; provide `alt` text and `width/height`.
- Add an `Icon` component using Iconify (or built-in SVGs) with a consistent style (stroke=1.5, rounded caps).
  - Replace plain bullets with icons in Features, Benefits, Integrations, FAQ toggles.
- Where text sits on an image (hero, feature highlights), wrap in a container that includes `.scrim` and uses `.on-surface-strong`.

### Component updates (concrete)
- `src/components/Section.astro`: 
  - Accept props: `variant: 'plain'|'media'|'panel'`, `image?: string`, `icon?: string`.
  - If `variant==='media'`, render background image with `<Image />` + `.scrim`, then place content inside a padded container with `text-onSurfaceStrong`.
- `src/components/FeatureCard.astro`:
  - Add optional `icon` prop. Header row shows icon + title.
  - Card background uses `surfaceStrong` with `on-surface` text; hover raises elevation but preserves contrast.
- `src/components/PricingCard.astro`:
  - Ensure copy uses `on-surface-strong` for titles and `on-surface` for body. Verify price figures have ≥ 4.5:1.
- `src/components/CTA.astro`:
  - Background gradient gets an overlay `.scrim`. Heading/subtitle use `on-surface-strong`.

### Theming & Modes
- Dark as default, light mode toggle must remap tokens to keep AA contrast:
  - In light mode, avoid low-opacity text on light surfaces; ensure buttons switch to darker hues with adequate foreground.
- Respect `prefers-color-scheme`; store preference and recalc token classes.

### QA & Automation
- Add `npm script` `test:a11y` that runs `@axe-core/cli` or `pa11y-ci` against local preview for key routes (/, sections hash anchors).
- Add `npm script` `test:lighthouse` to check Lighthouse scores; fail if `accessibility < 95` or any contrast audit fails.
- Add a lightweight unit visual check: ensure all `h1/h2/p/button` elements compute to colors with compliant contrast vs their immediate background.
  (A simple DOM script in `src/scripts/contrast-check.ts` can measure computed styles & log offenders to console during dev.)

### Acceptance Criteria (must pass)
- No Lighthouse “Contrast” failures. Accessibility score ≥ 95.
- All headings/body/buttons meet AA thresholds.
- Hero, Product Tour, Features, Benefits, Integrations, Pricing, FAQ, Final CTA each includes at least one icon; at least 4 sections include an illustrative image with `.scrim`.
- Largest text on media has legible foreground in both dark & light modes.
- CLS unaffected; images lazy; total image payload under 350KB on initial viewport (use responsive sizes).

### Implementation Hints (Tailwind)
- Colors (example, adjust to project brand):
  - dark: 
    - bg `#0b0f17`, surface `#121826`, surface-strong `#0e1524`,
    - text `#E6EAF2`, textMuted `#A6B0C3`,
    - primary `#75F0FF`, primaryFg `#07141A`,
    - onSurface `#DDE3EE`, onSurfaceStrong `#FFFFFF`.
  - light:
    - bg `#F7FAFF`, surface `#FFFFFF`, surface-strong `#F2F6FF`,
    - text `#0E1A2B`, textMuted `#3C4B66`,
    - primary `#0EA5E9`, primaryFg `#FFFFFF`,
    - onSurface `#243142`, onSurfaceStrong `#0E1A2B`.
- Example utility classes:
  - `.on-surface { @apply text-onSurface; }`
  - `.on-surface-strong { @apply text-onSurfaceStrong; }`
  - `.scrim { background: linear-gradient(180deg, rgba(0,0,0,.56), rgba(0,0,0,.24) 50%, rgba(0,0,0,0)); }`

### Deliverables
- Updated Tailwind config & tokens with safe foreground/background pairs.
- Media-ready sections using `<Image />` + `.scrim`.
- Icons added across features/benefits/integrations/FAQ.
- A11y and Lighthouse scripts added and passing.

Refactor the code accordingly, update components, wire images/icons, and commit with message: 
"feat(marketing): enforce AA contrast + add images/icons with scrim overlays; add a11y checks"
