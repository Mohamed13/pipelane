# Prospecting Automation (n8n)

## Workflows

### 1. Prospect-Enrich
- **Trigger**: Manual or webhook (POST /api/prospecting/hooks/enrich)
- **Steps**:
  1. Fetch latest prospects (GET /api/prospects?size=200)
  2. Call Clearbit/Dropcontact enrichment; append response to Prospect.enrichedJson
  3. PATCH back to API (PATCH /api/prospects/{id} – not yet exposed, use manual SQL or extend service)
  4. Notify Slack/Teams once enrichment batch completes.

### 2. Send-Step
- **Trigger**: Cron (every hour) or webhook (POST /api/prospecting/hooks/send-next)
- **Steps**:
  1. Request due send logs (POST /api/prospecting/hooks/send-next)
  2. For each item, call /api/ai/generate-email when equiresApproval=false
  3. Send via SendGrid API; add sendLogId & 	enantId in custom args
  4. PATCH /api/prospecting/hooks/send-next response (already updates status to Sent).

### 3. Listen-Replies
- **Trigger**: SendGrid Inbound Parse → POST /api/email/webhooks/sendgrid
- **Steps**:
  1. The backend stores ProspectReply; call /api/ai/classify-reply
  2. If intent is interested or meetingRequested, create task in CRM.
  3. For quick responses, call /api/ai/auto-reply and send via SendGrid.

### 4. Book-Demo
- **Trigger**: n8n poll (e.g., every 15 minutes) on replies table via /api/prospecting/replies?intent=meetingRequested
- **Steps**:
  1. Compose email with Calendly link (/api/ai/auto-reply)
  2. Send via SendGrid; update CRM (HubSpot/Pipedrive) with activity
  3. POST to /api/prospecting/hooks/follow-up to keep pipeline warm.

## Webhook Secrets
- Configure SendGrid signature validation via environment variables.
- Keep OPENAI_API_KEY set for live AI prompts. Without it, the service falls back to deterministic templates.

## TODO
- Expose PATCH /api/prospects/{id} endpoint for enrichment
- Add durable queue for large send batches
- Instrument ProspectingAutomationController hooks with idempotency keys
