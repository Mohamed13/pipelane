✅ Completed: Angular dashboard has been refreshed with Material shell, design tokens, responsive tables, KPI/Chart cards, interactions, and dark mode. The details below capture the original requirements for reference.

I have a working Angular app that looks plain. I want a modern, responsive dashboard UI with vibrant colors, cards, smooth micro-animations, and clean spacing. Keep existing business logic, routes, and API calls intact.

Primary choice: use Angular Material (preferred). If something is already using Bootstrap, you may keep it, but favor Material for new UI.

Objectives

Upgrade the UI to a polished dashboard look & feel.

Add consistent spacing (paddings/margins), readable typography, and an accessible color palette (vibrant but WCAG-compliant).

Use cards, grids, and charts. Add subtle animations and ho  ver effects.

Make everything responsive (desktop/tablet/mobile).

Keep all current features working; do not break APIs.

Tasks (end-to-end)

Install & Setup

Add Angular Material, CDK, Animations, and Material Icons.

Add a light/dark theme with a primary/accent/warn palette (vibrant style).

Create a global SCSS with spacing + elevation helpers.

Design System

Define theme in styles.scss (or a dedicated theme folder) with:

Color tokens: --color-primary, --color-accent, --color-bg, --color-surface, --color-text.

Spacing scale: --space-1 … --space-6.

Radius scale: --radius-sm, --radius-md, --radius-xl (use large rounded corners).

Shadow utilities for cards and buttons (soft shadows).

Ensure high contrast for text on colored backgrounds.

Layout & Navigation

Implement a responsive shell layout:

App toolbar with title, search input (optional), color-mode toggle.

Collapsible side nav with icons + labels, using Material mat-sidenav + mat-nav-list.

Content area uses a responsive mat-grid-list or CSS grid.

Persist side nav state (opened/collapsed) in localStorage.

Cards & Components

Replace plain blocks with mat-card components (rounded corners, elevation).

Add hover animations on cards (slight scale/elevation; 150–200ms).

For lists/tables:

Use MatTable with MatPaginator, MatSort, sticky header, and dense row option.

Add row hover feedback and clickable rows when relevant.

Add reusable “KPI Stat Card” component (title, value, delta %, small sparkline).

Charts

Add charts with Chart.js (ng2-charts) or ngx-charts (pick one and standardize).

Create a ChartsModule with:

Line chart (with gradient fill),

Bar chart,

Donut chart.

Provide a ChartCardComponent wrapping a mat-card + chart + header actions.

Animations

Enable Angular animations globally.

Add route-transition fade/slide (200–300ms).

Add hover transitions for buttons/cards/links.

Use IntersectionObserver or Angular animation triggers to gently reveal sections on scroll.

Responsive Rules

Desktop: multi-column grid (3–4 cols).

Tablet: 2 cols.

Mobile: 1 col; side nav becomes modal drawer.

Ensure charts resize; tables get horizontal scroll on small screens.

Forms & Buttons

Convert forms to mat-form-field with appearance="outline".

Add helpful hints, validation messages, and icons where useful.

Use mat-button/mat-raised-button with clear hierarchy (primary/secondary).

Accessibility

Minimum 4.5:1 contrast for body text; 3:1 for large text.

Keyboard focus states visible on all interactive elements.

aria-labels for icon-only buttons and nav items.

Theming

Implement dark mode toggle (persisted).

Make charts adapt to theme (text/grid/tooltip colors).

All custom components must consume CSS variables from the theme.

Performance & Hygiene

Remove unused CSS; prefer component-scoped styles.

Lazy-load heavy feature modules and chart libs if possible.

Keep lint passing; no TODOs or dead code.

Non-breaking

Do NOT change existing service logic or API contracts.

Preserve all data bindings, route params, and guard logic.

Deliverables

Updated layout (toolbar + responsive sidenav) and dashboard landing with example KPI stat cards + 3 chart cards + recent table.

A theme/ folder (or similar) with SCSS variables and mixins.

A shared/ui/ folder with reusable Card, ChartCard, KpiCard.

Dark mode working and persisted.

Demo data for charts (if no API yet), behind a USE_DEMO_DATA flag.

Concrete Implementation Steps

Install

@angular/material, @angular/cdk, @angular/animations

chart.js + ng2-charts (or @swimlane/ngx-charts)

Material Icons

App Shell

Create ShellComponent with mat-sidenav-container.

Toolbar: app name/logo, search, theme toggle, user menu.

Sidenav: icon list with active state; collapse on small screens.

Dashboard Page

Grid of KpiCardComponent (e.g., Revenue, Users, Conversion, Tickets).

Row with ChartCardComponent (line + bar + donut).

MatTable for recent items (with sort/paginate/filter).

Styling

Global SCSS variables; set border-radius: 1rem for cards/buttons.

Spacing utilities: m-1..m-6, p-1..p-6 classes mapped to CSS vars.

Hover: transform: translateY(-2px) scale(1.01) + box-shadow increase.

Animations

Route fade/slide on router-outlet.

:hover transitions on cards (200ms ease).

Reveal on scroll for sections.

Testing

Verify responsiveness with breakpoints (≥1200px, 992–1199, 768–991, ≤767).

Check light/dark contrast and chart readability.

Ensure no console errors; all existing features still work.

Acceptance Criteria

UI looks like a modern analytics dashboard (clean, vibrant, airy).

Consistent spacing, typography, and rounded cards with soft shadows.

Smooth micro-interactions; nothing janky.

Charts render responsively and match the theme (light/dark).

No functional regressions.

Notes

If any legacy Bootstrap exists, keep it only where refactoring would break things. Prefer Material for new components.

(Optional) Bootstrap Variant

If I say “use Bootstrap instead of Material”, then:

Use Bootstrap 5 + ng-bootstrap for components.

Replace mat-card by custom .card with rounded-4 and shadow utilities.

Use Chart.js via ng2-charts.

Use Bootstrap grid for responsiveness; offcanvas for mobile nav.

Keep the same theming approach via CSS variables and dark mode class on <body>.
