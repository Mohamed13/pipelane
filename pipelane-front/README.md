# Pipelane Front (Angular)

Operator console for the Omni-Channel Revenue Engine.

## Run
- Install deps: `npm install`
- Configure API base: copy `.env.example` to `.env` and set `API_BASE_URL`
- Dev server: `npm start` (http://localhost:4200) — env injected automatically
- Unit tests: `npm test`
- E2E: `npm run e2e`

Env injection writes `src/app/core/env.generated.ts` at build/start time.
Send tenant with `X-Tenant-Id` (ApiService supports header injection when provided).

## Routes
- `/onboarding`, `/templates`, `/contacts`, `/conversations/:contactId`, `/campaigns`, `/analytics`, `/settings`

## I18n & Theme
- Language: EN/FR via `assets/i18n/*.json` (selector in header)
- Theme: Classic/Dark toggle (header)
