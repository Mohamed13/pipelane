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

## Notes
- Background services: OutboxProcessor, CampaignRunner, FollowupScheduler.
- Outbox uses DB table `Outbox` with retry and basic idempotency (provider id).
- Configure `DB_CONNECTION` and `ENCRYPTION_KEY` env vars for production.
