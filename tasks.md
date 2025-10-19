You are a senior Angular 20 UX engineer. 
Open this file, summarize all steps, then execute them one by one across the codebase `pipelane-front/`. 
Produce small, reviewable commits per section with clear messages.

GOAL
- Transform the current console into a **futuristic, modern, ergonomic** UI.
- Rich color palette, glass/neo gradients, smooth micro-interactions, accessible contrast.
- Nicer charts, contextual **tooltips** everywhere, and a **guided onboarding tutorial** (didacticiel) on first run.

LIBRARIES (install if missing)
- Angular Material (preferred) + CDK; (Bootstrap optional only for grid utilities if really needed).
- Charts: **ng-apexcharts** (preferred) or Chart.js with better theming.
- Tour/didacticiel: **ngx-shepherd** (Shepherd.js) or **driver.js** wrapper.
- Icons: **@angular/material/icon** + Material Symbols (fallback: lucide-angular).
- Tooltips: **MatTooltip**.
- Animations: Angular animations + CSS transforms (no heavy GSAP).

THEME & DESIGN SYSTEM
1) Create a design tokens file (SCSS): `src/theme/_tokens.scss`
   - Colors (dark default):
     - bg: #0b0f17
     - surface: #101726
     - surface-strong: #0e1524
     - primary: #75F0FF
     - secondary: #9B8CFF
     - accent: #60F7A3
     - text: #E6EAF2
     - text-muted: #A6B0C3
   - Gradients:
     - `--grad-main: linear-gradient(135deg,#75F0FF 0%,#9B8CFF 45%,#60F7A3 100%)`
   - Radii, spacing, shadows, elevation.
2) Material theming:
   - Define **Mat** theme (dark & light) mapping the tokens; enable density="comfortable".
   - Global glass utility: `.glass { backdrop-filter: blur(12px); background: rgba(255,255,255,.06); border: 1px solid rgba(255,255,255,.08); }`
   - Add `.scrim` overlay for text on images/gradients to guarantee contrast (WCAG AA).
3) Typography:
   - Inter or Outfit; set responsive scale (h1→h6, body-1/2).
4) Motion:
   - Build simple animation utilities: fade-up, scale-in, stagger reveal via IntersectionObserver directive.

LAYOUT & NAV
5) Refactor `AppComponent` shell:
   - Top app bar (glass), left rail for primary nav (icons + labels), responsive collapse.
   - Add quick actions: “Send test”, “Create campaign”, “Import contacts”, “Docs”.
   - Persistent theme toggle.
6) Route highlights with animated underline; breadcrumb under header area.

PAGES TO BEAUTIFY
7) **AnalyticsOverviewComponent**
   - Replace charts with **ng-apexcharts**:
     - KPI strip cards (glass) with micro-sparkline.
     - Area chart (Sent/Delivered/Opened/Failed by day) with gradient fill & smooth curve.
     - Donut “by channel”, bar “top templates”.
   - Add time range selector (Today / 7d / 30d / Custom) + MatDateRangePicker.
   - Tooltips on every KPI and axis (MatTooltip).
   - Empty states with nice illustrations.
8) **ConversationThreadComponent**
   - Chat bubbles with provider badges (WhatsAppCloud/Resend/TwilioSMS).
   - Status chips (Queued/Sent/Delivered/Opened/Failed) with icons + tooltips.
   - Composer: segmented control (Text / Template); template variable helper popover.
   - Right info panel (collapsible): contact profile, tags, last activity, consent flags.
9) **CampaignBuilderComponent**
   - Stepper with visual progress:
     1) Audience (segment builder mini-UI: tags, channels, last activity),
     2) Message (template/preview),
     3) Schedule & Throttle (BatchSize, ScheduleAt, Quiet hours),
     4) Review summary.
   - Live “Recipients preview count”; tooltips explaining each option.
10) **Onboarding/Settings**
    - Channel cards (WhatsApp/Email/SMS) with status chip (Connected/Not connected), “Send test” actions.
    - Secrets inputs with visibility toggle and helper texts.

TOOLTIPS & HELP
11) Add **MatTooltip** systematically:
    - Buttons, inputs, badges, filters, charts legends.
    - Use i18n keys (en/fr), file `src/i18n/ui.json`.
    - Provide concise helpful copy (what/why).

GUIDED TUTORIAL (DIDACTICIEL)
12) Integrate **ngx-shepherd**:
    - On first app launch (localStorage flag `pipelane_tour_done=false`), start a guided tour that walks through:
      Step 1: “Connect your channels” → highlight Settings/Onboarding.
      Step 2: “Add templates” → Templates page.
      Step 3: “Import contacts” → Contacts page (CSV or API).
      Step 4: “Send yourself a test” → Onboarding buttons.
      Step 5: “Create your first campaign” → Campaign builder start.
      Step 6: “Enable follow-ups” → Followups config section.
      Step 7: “View analytics” → Analytics dashboard with explanation of charts.
    - Each step: title, text, Next/Back/Finish, and a “Skip tour” option.
    - Add “Help → Replay tutorial” menu item.
    - Ensure focus-trap and accessibility (tab/esc) are OK.

ACCESSIBILITY & CONTRAST
13) Pass Lighthouse/axe for contrast AA:
    - Ensure text on surfaces meets 4.5:1 (use `.on-surface` / `.on-surface-strong` classes).
    - Add focus outlines consistent with brand (neon ring).
    - Respect prefers-reduced-motion.

DATA INTEGRATION
14) Ensure all charts load real data from `/api/analytics/delivery` with live DTOs.
    - Fallback skeletons/empty state when no data.
    - Poll minimal where needed; stop when terminal states reached.

TESTS & QUALITY
15) Unit tests (Jest):
    - Tooltips presence on key controls.
    - Tour service flag logic; start-on-first-run; replay.
    - Analytics service DTO mapping to ApexCharts options.
16) E2E (Cypress):
    - `tour.spec`: runs tutorial, checks step titles and highlights.
    - `analytics.spec`: change date range, charts update.
    - `campaign_builder.spec`: fill steps, preview recipients, review summary visible.
17) Add `npm scripts`:
    - `npm run ui:check` (lint + stylelint if present)
    - `npm run ui:test` (jest) 
    - `npm run ui:e2e` (cypress run headless)

IMPLEMENTATION NOTES
- Prefer Angular Material components; avoid mixing Bootstrap unless necessary for utilities.
- Encapsulate charts in `ChartCardComponent` with a single `@Input() config`.
- Create `TooltipDirective` helper if repeated tooltip text logic.
- Keep bundles light: lazy-load tour library and chart modules; import Apex modules selectively.
- Keep all colors via tokens; one source of truth for theming.

ACCEPTANCE CRITERIA
- New shell + navigation with futuristic style and smooth micro-interactions.
- Analytics page shows Apex area + donut + bar with legends and tooltips.
- Conversation thread uses styled bubbles, provider/status chips, and tooltips.
- Campaign builder 4 steps with preview; all controls have descriptive tooltips.
- Guided tour appears on first load (and is replayable), fully keyboard-accessible.
- Lighthouse Accessibility ≥ 95; Performance not regressed.
- 6–10 unit tests green; 3 Cypress e2e green.

DELIVERABLES
- All code changes in `pipelane-front/` with incremental commits.
- README snippet in `pipelane-front/README.md` describing theme tokens, how to run the tour, and how to update charts.
