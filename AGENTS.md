# Agent Handbook

Guidance for agents working in the Pipelane monorepo. Keep changes scoped, documented, and validated locally before handing work back.

## Repository Map
- `pipelane-api/`: .NET 8 multi-tenant API (SQL Server). Key pieces: background services (`OutboxProcessor`, `CampaignRunner`, Quartz `FollowupScheduler`), channel adapters (WhatsApp/Email/SMS), webhook plumbing, xUnit tests in `tests/`.
- `pipelane-front/`: Angular 20 operator console (standalone components, Angular Material, chart.js). Helpers live in `tools/` (`inject-env.mjs`, `fetch-swagger.mjs`).
- `pipelane-marketing/`: Astro + Tailwind marketing site with pa11y/Lighthouse scripts and `/api/demo-request` endpoint.
- `.github/workflows/`: CI definitions that must stay aligned with script and tooling changes.

## Getting Started
- Backend
  - `dotnet restore` inside `pipelane-api`.
  - Build via `./scripts/build.ps1` (or `.sh`), run tests with `./scripts/test.ps1`.
  - Local DB: `docker compose up sqlserver` at repo root; the API auto-applies migrations/seeding on startup.
  - Required env vars: `DB_CONNECTION`, `ENCRYPTION_KEY`, `JWT_KEY`, `RESEND_API_KEY`, `RESEND_WEBHOOK_SECRET`.
- Frontend
  - `npm ci` inside `pipelane-front`.
  - `npm start`/`npm run build`; `tools/inject-env.mjs` writes `src/app/core/env.generated.ts` (do not commit it).
  - Quality commands: `npm test`, `npm run lint`, `npm run e2e`, `npm run gen:api` (Swagger fetch + TS types; API must be running).
- Marketing
  - `npm install` or `pnpm install` then `npm run dev|build|test:a11y|test:lighthouse`.
- Always execute the relevant build/test commands before pushing or opening a PR.

## Backend Highlights
- Multi-tenancy enforced via `HttpTenantProvider`; every request and test needs `X-Tenant-Id`.
- Messaging flow: `MessagingService` routes text vs template, enqueues templates into the `Outbox`, and honours the WhatsApp 24h session rule via `ChannelRulesService`.
- Background workers:
  - `OutboxProcessor` locks batches, retries with exponential backoff, persists `Message` + `MessageEvent`.
  - `CampaignRunner` promotes due campaigns and enqueues up to 100 contacts (segment JSON still rudimentary).
  - `FollowupScheduler` (Quartz) creates `FollowupTask` items after 24h without reply and schedules `nudge-1` template nudges at 48h.
- Integrations: email uses Resend (`EmailChannel`, dedicated HTTP client). `ResendWebhookVerifier` (HMAC) and `ResendWebhookProcessor` (idempotent status mapping) are already covered by unit tests. WhatsApp/SMS adapters are stubbed for now.
- Analytics: `AnalyticsService.GetDeliveryAsync` powers `/analytics/delivery` with totals, per-channel, and per-template breakdowns.
- Auth/security: `AuthController` supports register/login/me, JWT signing with configurable key, PBKDF2 password hashing, AES-GCM encryption service, Serilog + OpenTelemetry (console exporter).
- Extend channels or analytics? Update the swagger snapshot (`pipelane-api/swagger.json`) and document any new env vars.

## Frontend Highlights
- Standalone Angular 20 setup with Angular Material theming (`ThemeService`) and route guards in `app.routes.ts`.
- `ApiService` centralises HTTP calls, injects `X-Tenant-Id`, and surfaces toast errors.
- Design tokens & composants partagés : `src/theme/_tokens.scss` centralise couleurs/gradients/motion; `kpi-card` et `chart-card` enveloppent Material + ng-apexcharts.
- `AnalyticsOverviewComponent` s'appuie sur ng-apexcharts (area/donut/bar), KPI sparklines et MatTable reliés à `/analytics/delivery`.
- `ConversationThreadComponent` affiche bulles vitrées, badges provider/statut, panneau insight repliable et composer texte/template avec helpers variables.
- `CampaignBuilderComponent` devient un wizard 4 étapes (audience, message, schedule, review) avec preview dynamique (`/api/followups/preview`).
- `TourService` embarque ngx-shepherd pour un onboarding guidé (flag localStorage + replay via menu Aide).
- Tests front : nouvelles specs Jest (`app.component` tooltips, `tour.service`, `analytics` mapping) et specs Cypress (`analytics`, `campaign_builder`, `onboarding`, `tour`). Lancer `npm run ui:e2e` une fois le front servi (`npm start` ou serveur statique sur `dist/`).
- Regenerate API types (`npm run gen:api`) whenever backend DTOs evolve.

## Marketing Site Notes
- Astro components compose the landing page sections (hero, product tour, pricing, FAQ, CTA, etc.) with Tailwind design tokens and glassmorphism utilities.
- Dark/light toggle honours `prefers-reduced-motion`; contrast checking runs in dev via `src/scripts/contrast-check.ts`.
- `/api/demo-request` validates submissions and logs them (no persistence yet). Coordinate with growth/CRM before wiring storage.
- Quality tooling: `npm run test:a11y` (pa11y-ci) and `npm run test:lighthouse` (fails <95). Keep assets optimised when adding visuals.

## Quality, Docs, CI
- Use the provided scripts for build/test/lint/format per project; report results when handing off work.
- OpenTelemetry currently exports to console; plan exporters if observability requirements grow.
- Reference `tasks.md`, `TODO-net8.md`, and `TODO-angular.md` for backlog context before introducing new tasks.
- Keep `AGENTS.md`, `COMPTE_RENDU.md`, and swagger/types docs accurate when behaviour changes.
- The repo may contain unrelated local changes—never revert work you did not author; sync with maintainers if conflicts arise.

## Practical Tips
- Stick to ASCII in new files/diffs unless the existing file relies on Unicode.
- Update `.env.example` files whenever new configuration is required.
- When modifying channels/back-office flows, extend existing xUnit coverage (`Pipelane.Tests`) and document secrets/scripts.
- Before merging: run backend tests (`./scripts/test`), frontend checks (`npm test`, `npm run lint`), and marketing audits where relevant; mention outcomes in review notes.
