TASK: Implement Pipelane logo across marketing site (Astro) and app (Angular). Keep existing CSS/theme.

Inputs

SVG (fond sombre + wordmark) :
<svg width="680" height="140" viewBox="0 0 680 140" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="Pipelane logo">
  <defs><linearGradient id="plFlow" x1="0%" y1="0%" x2="100%" y2="0%">
    <stop offset="0%" stop-color="#28C0B0"/><stop offset="100%" stop-color="#7CE4D9"/></linearGradient></defs>
  <rect width="680" height="140" rx="16" fill="#0D0F12"/>
  <g transform="translate(20,28)">
    <path d="M10 40 C60 10, 90 70, 140 40 S220 10, 260 40" stroke="url(#plFlow)" stroke-width="6" fill="none" stroke-linecap="round"/>
    <circle cx="60" cy="25" r="5" fill="#28C0B0"/><circle cx="140" cy="40" r="5" fill="#28C0B0"/><circle cx="220" cy="25" r="5" fill="#28C0B0"/>
    <path d="M260 40 l14 -8 v16 z" fill="#7CE4D9"/>
  </g>
  <g transform="translate(320,85)">
    <text x="0" y="0" fill="#E8FCF8" font-family="Space Grotesk, Inter, system-ui" font-weight="700" font-size="44">Pipelane</text>
    <text x="0" y="26" fill="#6B7280" font-family="Inter, system-ui" font-weight="600" font-size="16" letter-spacing="1.4">QUALIFY • RELAUNCH • BOOK</text>
  </g>
</svg>


Create a compact variant (no <rect>, wordmark in dark text) for light backgrounds.

1) Astro (marketing site)

Files

src/assets/pipelane-logo.svg (with <rect>)

src/assets/pipelane-compact.svg (no <rect>)

Header component (src/components/Header.astro)

Inline the full SVG for proper gradients & a11y; fallback to <img> allowed.

<header class="site-header">
  <div class="logo" role="img" aria-label="Pipelane logo">
    <!-- inline the full SVG here -->
  </div>
</header>

<style>
.site-header { display:flex; align-items:center; padding:12px 20px; }
.logo { width:min(280px, 40vw); height:auto; }
@media (max-width:640px){ .logo { width:200px; } }
</style>


Head (OG/Favicons)

Add in layout or _head.astro:

<meta property="og:title" content="Pipelane">
<meta property="og:description" content="Qualify • Relaunch • Book">
<meta property="og:image" content="/og-pipelane.png">
<meta name="twitter:card" content="summary_large_image">
<link rel="icon" href="/favicon.svg" type="image/svg+xml">
<link rel="alternate icon" href="/favicon.ico">
<link rel="apple-touch-icon" href="/apple-touch-icon.png">
<link rel="manifest" href="/site.webmanifest">


Export og-pipelane.png (1200×630), apple-touch-icon.png, favicon.svg, site.webmanifest with theme_color/background_color = #0D0F12.

2) Angular (app front)

Files

src/assets/brand/pipelane-logo.svg

src/assets/brand/pipelane-compact.svg

Favicons/manifest: assets/brand/{favicon.svg, favicon.ico, apple-touch-icon.png, maskable-192.png, maskable-512.png}

Reusable logo component
src/app/shared/ui/pipelane-logo/pipelane-logo.component.ts

import { Component, Input } from '@angular/core';
@Component({
  selector: 'app-pipelane-logo',
  standalone: true,
  template: `
    <div class="pl-logo" [attr.aria-label]="ariaLabel" role="img">
      <ng-container *ngIf="inline; else imgTag">
        <!-- paste full SVG here for 'default'; use compact variant if [variant]="compact" -->
      </ng-container>
      <ng-template #imgTag>
        <img [src]="variant==='compact' ? 'assets/brand/pipelane-compact.svg' : 'assets/brand/pipelane-logo.svg'"
             [alt]="ariaLabel" decoding="async" loading="eager">
      </ng-template>
    </div>`,
  styles: [`.pl-logo{display:inline-block;width:clamp(180px,22vw,320px)} img{width:100%;height:auto;display:block}`]
})
export class PipelaneLogoComponent {
  @Input() variant: 'default'|'compact' = 'default';
  @Input() inline = true;
  @Input() ariaLabel = 'Pipelane logo';
}


Use in header

<header class="app-header">
  <app-pipelane-logo class="header" variant="default" [inline]="true"></app-pipelane-logo>
  <!-- nav ... -->
</header>


Favicons/manifest
src/index.html

<link rel="icon" type="image/svg+xml" href="assets/brand/favicon.svg">
<link rel="alternate icon" href="assets/brand/favicon.ico">
<link rel="apple-touch-icon" href="assets/brand/apple-touch-icon.png">
<link rel="manifest" href="manifest.webmanifest">


src/manifest.webmanifest

{
  "name":"Pipelane","short_name":"Pipelane",
  "icons":[
    {"src":"assets/brand/maskable-192.png","sizes":"192x192","type":"image/png","purpose":"any maskable"},
    {"src":"assets/brand/maskable-512.png","sizes":"512x512","type":"image/png","purpose":"any maskable"}
  ],
  "theme_color":"#0D0F12","background_color":"#0D0F12","display":"standalone"
}

3) Theming & a11y

Keep role="img" + aria-label on containers.

Use full SVG inline where gradient fidelity is required (hero/header).

On light backgrounds, use compact variant (no <rect>).

If dark-mode: swap variant by theme class (optional).

Deliverables

Astro: logo integrated in header (inline SVG), OG/Twitter meta, favicons/manifest.

Angular: shared logo component, header usage, favicons/manifest.

Assets exported (SVG + PNGs).

No visual regressions; responsive widths applied.

Acceptance

Logo renders crisp in both apps (desktop/mobile).

OG/Twitter image resolves (validator OK).

Favicons show across platforms.

Gradient and wordmark colors match design on dark/light backgrounds.