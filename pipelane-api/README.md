# Pipelane Backend (.NET 8)

Omni-Channel Revenue Engine backend (ASP.NET Core + EF Core + SQL Server). Implements multi-tenant API, channel adapters (WhatsApp/Email/SMS), outbox processing, and basic campaign orchestration.

## Quickstart
- Start DB: `docker compose up -d sqlserver`
- Run API: `dotnet run --project src/Pipelane.Api`
- Health: `GET http://localhost:5000/health`

Set tenant per request via header: `X-Tenant-Id: <GUID>`.

## Key Endpoints
- POST `/onboarding/channel-settings`
- GET `/templates`, POST `/templates/refresh`
- POST `/contacts/import`, GET `/contacts?search=&page=&size=`
- GET `/conversations/{contactId}`
- POST `/messages/send`
- POST `/campaigns`, GET `/campaigns/{id}`
- GET `/analytics/overview?from&to`
- GET `/webhooks/whatsapp` (verify), POST `/webhooks/whatsapp|email|sms`
- POST `/conversions`
- Prospecting:
  - GET `/api/prospects`, POST `/api/prospects/import`, POST `/api/prospects/optout`
  - GET `/api/prospecting/sequences`, POST `/api/prospecting/sequences`
  - GET `/api/prospecting/campaigns`, `POST /api/prospecting/campaigns/{id}/start|pause|preview`
  - GET `/api/prospecting/replies`, POST `/api/ai/generate-email|classify-reply|auto-reply`
  - POST `/api/prospecting/hooks/{enrich|send-next|follow-up}`

## Notes
- Background services: OutboxProcessor, CampaignRunner, FollowupScheduler.
- Outbox uses DB table `Outbox` with retry and basic idempotency (provider id).
- Configure `DB_CONNECTION` and `ENCRYPTION_KEY` env vars for production.

## Prospecting Module
- Entities: `Prospect`, `ProspectingSequence`, `ProspectingSequenceStep`, `ProspectingCampaign`, `SendLog`, `ProspectReply`, `EmailGeneration`, `LeadScore` (extended for prospects).
- SQL Server migrations include per-tenant indexes and unique constraints on prospect email.
- AI integration hooks via `/api/ai/*` endpoints. Configure `OpenAI:ApiKey` (optional) for live completions; falls back to deterministic templates when absent.
- SendGrid webhook listens on `/api/email/webhooks/sendgrid` updating `ProspectingSendLogs` and creating `ProspectReply` records.
- Automation hooks (`/api/prospecting/hooks/enrich|send-next|follow-up`) provide a stateless surface for n8n flows to orchestrate enrichment, scheduling, and follow-ups.
