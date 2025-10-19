YOU ARE a senior .NET 8 engineer. Build a production-grade, channel-agnostic backend for an
“Omni-Channel Revenue Engine” (rename to the project’s brand if provided: e.g., Relay).
Stack: ASP.NET Core (.NET 8), EF Core (SqlClient/SQL Server), BackgroundServices (Quartz or HostedServices),
Serilog, OpenTelemetry, FluentValidation, Polly. Clean architecture (Api/Application/Infrastructure).

GOAL
A secure, multi-tenant API that:
1) Stores contacts, consents (per channel), conversations, messages, templates, campaigns, events, conversions.
2) Sends messages via pluggable “Channel Adapters” (WhatsApp, Email, SMS), with rate-limit, retries, and fallback.
3) Handles inbound webhooks (WhatsApp Cloud API, ESP events, SMS gateway) to update statuses and conversations.
4) Orchestrates campaigns and follow-ups (J+0/J+1/J+3/J+7), enforces channel rules (WhatsApp 24h window, email unsubscribe, SMS STOP).
5) Exposes analytics (by channel and cross-channel).

ARCHITECTURE (projects)
- Project.Api           (controllers, DI, middlewares, health, Swagger)
- Project.Application   (DTOs, validators, services, rules/orchestrator, interfaces)
- Project.Infrastructure(DbContext, EF configs, repositories, SqlClient, Channel adapters, external clients, crypto)
- Project.Domain        (entities, value objects, enums)

DATA MODEL (schema "dbo")
Tables (with created_at/updated_at, tenant_id everywhere):
- tenants(id, name)
- contacts(id, tenant_id, phone, email, first_name, last_name, lang, tags_json, created_at, updated_at)
- consents(id, tenant_id, contact_id, channel enum('whatsapp','email','sms'), opt_in_at_utc, source, meta_json)
- conversations(id, tenant_id, contact_id, primary_channel, provider_thread_id, created_at)
- messages(id, tenant_id, conversation_id, channel, direction enum('in','out'), type enum('text','template','media'),
           template_name, lang, payload_json, status enum('queued','sent','delivered','read','failed'),
           provider_message_id, created_at)
- templates(id, tenant_id, name, channel enum('whatsapp','email','sms'), lang, category, core_schema_json, is_active, updated_at_utc)
- campaigns(id, tenant_id, name, primary_channel, fallback_order_json, template_id, segment_json, scheduled_at_utc,
            status enum('pending','running','done','failed'), created_at)
- events(id, tenant_id, source enum('whatsapp','email','sms','shop','crm'), payload_json, created_at)
- conversions(id, tenant_id, contact_id, campaign_id null, amount, currency, order_id, revenue_at_utc)
- lead_scores(id, tenant_id, contact_id, score, reasons_json, updated_at_utc)
- channel_settings(id, tenant_id, channel, settings_json) -- e.g., WhatsApp phone_number_id/access_token, ESP API key, SMS token

CRITICAL INDEXES
- IX_messages_conversation_created (conversation_id, created_at)
- IX_contacts_tenant_phone unique
- IX_templates_tenant_name_lang_channel unique
- IX_campaigns_tenant_scheduled
- IX_events_tenant_created
- IX_consents_contact_channel

CHANNEL ABSTRACTION
Define a common interface:
public interface IMessageChannel {
Task<SendResult> SendTextAsync(Contact c, string text, SendMeta meta, CancellationToken ct);
Task<SendResult> SendTemplateAsync(Contact c, Template t, IDictionary<string,string> vars, SendMeta meta, CancellationToken ct);
Task<WebhookResult> HandleWebhookAsync(HttpRequest request, CancellationToken ct); // parse provider event → Messages/Conversations
Task<bool> ValidateTemplateAsync(Template t, CancellationToken ct);
}

diff
Copier le code
Implement three adapters (initial):
- WhatsAppChannel (Meta WhatsApp Cloud API: session vs HSM outside 24h)
- EmailChannel    (ESP: SendGrid/Mailgun/Resend; HTML renderer; unsubscribe handling)
- SmsChannel      (Twilio/MessageBird; STOP opt-out)

ORCHESTRATOR & SEQUENCER
- Outbox queue (DB table or lightweight queue) + BackgroundService:
  - rate-limit, retry with Polly (exponential backoff), idempotency on provider_message_id.
- CampaignRunner:
  - selects due campaigns; expands segment_json to contact IDs; enqueues batches.
- FollowupScheduler:
  - plans J+1/J+3/J+7 based on last interaction & not converted.
- ChannelRules:
  - WhatsApp 24h window (session allowed if last inbound ≤ 24h; else require template)
  - Email: must include unsubscribe token; honor bounces/complaints.
  - SMS: honor opt-in + STOP; quiet hours window (config).

TEMPLATES
- Store a **core template** definition in `core_schema_json` (variables, i18n, structure).
- Provide renderers per channel:
  - WhatsApp → HSM (header/body/buttons)
  - Email → MJML/Handlebars → HTML
  - SMS → plain text + shortened links

ENDPOINTS (v1)
- GET  /health
- POST /onboarding/channel-settings   (save WhatsApp/ESP/SMS credentials per tenant)
- GET  /templates                      (list), POST /templates/refresh (pull provider list)
- POST /contacts/import                (CSV/JSON), GET /contacts?search=&page=&size=
- GET  /conversations/{contactId}      (last N messages)
- POST /messages/send                  ({contactId|phone, channel, type:'text'|'template', templateName?, lang?, variables?, meta?})
- POST /campaigns                      ({name, primary_channel, fallback_order_json, template_id, segment_json, scheduled_at_utc?})
- GET  /campaigns/{id}
- GET  /analytics/overview?from&to     (per channel + global KPIs)
- GET  /webhooks/whatsapp (verify), POST /webhooks/whatsapp
- POST /webhooks/email, POST /webhooks/sms
- POST /conversions                    ({contactId, amount, currency, orderId})

SECURITY & QUALITY
- Multi-tenant middleware (tenant header / subdomain).
- Auth: magic-link or OIDC (JWT). RBAC (owner/manager/viewer).
- Serilog request logging + OpenTelemetry traces (HTTP/SQL/outbound calls).
- CORS (Angular dev origin). ProblemDetails middleware.
- FluentValidation on POSTs; Swagger examples for providers’ payloads.
- Secrets via env/UserSecrets; AES-GCM to encrypt provider tokens in channel_settings.

TESTS (xUnit)
- Unit: channel rules (WhatsApp 24h), template rendering, orchestrator fallback.
- Integration: webhook → creates conversation/message; send template → queued → adapter called (mock).
- SQL migration test: ensure indexes exist.

DELIVERABLES
- Full solution (Api/Application/Infrastructure/Domain), EF Core migrations, DI setup,
- Channel adapters (WhatsApp/Email/SMS) with config stubs and real HTTP calls behind interfaces,
- Background services (Outbox, CampaignRunner, FollowupScheduler),
- Controllers/DTOs/Validators, Swagger/OpenAPI + README (docker-compose for SQL Server; how to set WhatsApp/ESP/SMS creds),
- Code compiles and first end-to-end “send template” works when credentials are valid.
Generate the complete implementation now.
