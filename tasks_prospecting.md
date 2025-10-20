You are a senior full-stack engineer working inside a monorepo:
- pipelane-api (.NET 8, Clean Architecture)
- pipelane-front (Angular 20, Material)
- pipelane-marketing (Astro)

Open this file, summarize the tasks, then execute them one by one with small, reviewable commits.

GOAL
Add a new “Prospecting Agent” module that autonomously generates, sends and follows up B2B cold emails, classifies replies, and books demos; integrated with n8n and our CRM connectors. Ship a 2-week MVP.

### 1) Database & Domain (pipelane-api)
Create entities + EF migrations:
- Prospect, Sequence, SequenceStep, Campaign, EmailGeneration, SendLog, Reply, LeadScore (see fields in the spec above).
- Indexes: per-tenant, unique Prospect(email).
Seed demo data (50 prospects, 1 sequence, 1 campaign).

### 2) Providers & Orchestration
- Email providers: Gmail/Outlook SMTP + SendGrid API (choose SendGrid as default).
- Outbox + Quartz jobs for scheduling steps (J0/J+3/J+7), throttle & quiet hours.
- Webhooks: POST /api/email/webhooks/sendgrid (events + inbound parse). Map statuses to SendLog.
- n8n endpoints/hooks to trigger enrich, send, wait, follow-up.

### 3) AI Services
- Add OpenAI client service.
- Endpoints:
  - POST /api/ai/generate-email {prospectId, stepId} -> EmailGeneration (HTML + subject, A/B variants).
  - POST /api/ai/classify-reply {replyId} -> intent, confidence, extracted dates.
  - POST /api/ai/auto-reply {replyId} -> draft HTML.
- Apply moderation/guardrails; log tokens/cost.

### 4) REST API for Prospecting
- Import: POST /api/prospects/import (CSV/JSON + mapping, dedupe).
- Sequences CRUD; Campaigns CRUD + /preview + /start|/pause.
- Analytics: GET /api/prospecting/analytics?from=&to= (openRate, replyRate, booked, series by day, by step).

### 5) Angular 20 UI
- Add a “Prospection” section with routes:
  - /prospecting/onboarding (wizard 5 steps)
  - /prospecting/sequences (builder with live AI preview)
  - /prospecting/campaigns/:id (monitor)
  - /prospecting/inbox (replies triage)
- Use Angular Material + ng-apexcharts for nicer charts; add MatTooltips.
- Add a guided tour (ngx-shepherd) describing the flow (connect email → define pitch → import → launch → follow-ups → inbox → analytics). Persist flag in localStorage.

### 6) n8n Workflows (export JSON + docs)
- Prospect-Enrich: On import -> Clearbit/Dropcontact -> PATCH Prospect.enrichedJson.
- Send-Step: Cron or webhook -> fetch due prospects -> call /api/ai/generate-email -> send via SendGrid -> log SendLog.
- Listen-Replies: SendGrid event webhook -> POST /api/replies -> /api/ai/classify-reply -> decide action (auto-reply or assign).
- Book-Demo: If intent=Interested -> craft reply with Calendly link -> send -> update CRM (HubSpot/Pipedrive).

### 7) Compliance & Deliverability
- Mandatory footer with opt-out link and company address.
- Implement /api/optout?email=… to mark Prospect.optedOut.
- Throttle defaults: 100 emails/day/tenant, randomize send windows, respect quiet hours.
- Store secrets encrypted; document SPF/DKIM in README.

### 8) Tests & Quality
- Unit tests: throttling, classification mapping, generation service, webhook parsing.
- Integration test: campaign run (J0 send + J+3 follow-up) on in-memory clock.
- Front tests: wizard starts on first visit; chart renders; inbox filters by intent.
- Add scripts: api:test, front:test, e2e smoke.

### 9) Docs & Demo
- README: how to connect SendGrid/Gmail, import CSV, build a sequence, launch, and read analytics.
- Seed command to populate demo data; add a demo walkthrough GIF.

Deliver incrementally; keep commits atomic with messages:
feat(api/prospecting): …
feat(front/prospecting): …
chore(n8n): …
docs: …

At the end, run smoke tests and output a summary of endpoints, UI routes, and how to run the demo.
