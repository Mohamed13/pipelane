# Pipelane Front (Angular)

Operator console for the Omni-Channel Revenue Engine.

## Run
- Install deps: `npm install`
- Configure API base: copy `.env.example` to `.env` and set `API_BASE_URL`
- Dev server: `npm start` (http://localhost:4200) — env injected automatically
- Lint + format check: `npm run ui:check`
- Unit tests: `npm run ui:test`
- E2E: `npm run ui:e2e` *(start the app separately with `npm start` or serve the `dist/` folder, e.g. `npx http-server dist/pipelane-front -p 4200 -c-1 --proxy http://localhost:4200?`)*

Env injection writes `src/app/core/env.generated.ts` at build/start time.
Send tenant with `X-Tenant-Id` (ApiService supports header injection when provided).

## Routes
- `/onboarding`, `/templates`, `/contacts`, `/conversations/:contactId`, `/campaigns`, `/analytics`, `/settings`
- Prospecting workspace: `/prospecting`, `/prospecting/onboarding`, `/prospecting/sequences`, `/prospecting/campaigns/:id`, `/prospecting/inbox`

## I18n & Theme
- Language: EN/FR via `assets/i18n/*.json` (selector in header)
- Theme tokens: `src/theme/_tokens.scss` centralises colours, gradients, radii, shadows and motion utilities; Material palettes derive from it
- Global components: `src/app/shared/ui/kpi-card.component.ts` (sparkline KPIs) + `chart-card.component.ts` (ng-apexcharts wrapper)
- Guided tour: `TourService` (ngx-shepherd) bootstrap on first visit, replay via Help menu
