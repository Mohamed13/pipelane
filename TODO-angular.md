YOU ARE an expert Angular 20 engineer. Build a production-ready operator console for an
“Omni-Channel Revenue Engine” (rename with the product brand if provided).
Stack: Angular 20 (standalone components, Signals or NgRx), OnPush, RxJS, Chart.js, Jest, Cypress, i18n EN/FR, theme toggle (Classic/Dark).

GOAL
A fast, clean SPA that lets users:
- finish onboarding (per-channel credentials & checks),
- manage templates (import/preview per channel),
- search contacts & chat (read-only thread + send within policy),
- build/schedule campaigns with channel + fallback,
- view analytics by channel and overall.

ROUTES (lazy, standalone)
- `/onboarding`
  - Wizard steps per channel:
    1) WhatsApp (PhoneNumberId, AccessToken, VerifyToken) + “Verify webhook” + “Send test template”
    2) Email ESP (API key, domain) + “Send test email”
    3) SMS (API key) + “Send test sms”
  - Save to `POST /onboarding/channel-settings`, show status badges.
- `/templates`
  - List provider templates (group by channel/lang), refresh (`POST /templates/refresh`), preview (rendered).
- `/contacts`
  - Search bar (phone/email/name/tags), list with last activity + channel badges + opt-in chips.
  - Click → **Conversation view**: last N messages (inbound/outbound bubbles), composer:
    - Respect policy: WhatsApp text only within 24h session; else force template selection.
    - Email composer (subject + HTML preview) or SMS text when those channels are chosen.
- `/campaigns`
  - **Builder (wizard)**:
    - Step 1: **Channel & Fallback** (primary channel + ordered fallbacks)
    - Step 2: **Template** (choose per channel, variables mapping, lang)
    - Step 3: **Audience** (segment builder: tags, lastInboundAt, opt-in per channel)
    - Step 4: **Schedule** (now or date/time) → Review → Create (POST /campaigns)
  - Campaign detail: progress (queued/sent/delivered/read/failed), errors, per-channel breakdown.
- `/analytics`
  - KPI cards: leads captured, reply rate, avg first response time, qualified, meetings, revenue.
  - Charts (7/30d): sent/delivered/read per channel, replies, conversions.
- `/settings`
  - Channel credentials, language preferences, quiet hours, default fallback policy.
  - Account/tenant info.

CORE SERVICES
- `ApiService`
  - `saveChannelSettings()`, `getTemplates()`, `refreshTemplates()`
  - `searchContacts()`, `getConversation(contactId)`
  - `sendMessage({ contactId|phone, channel, type:'text'|'template', text?, templateName?, lang?, variables? })`
  - `createCampaign()`, `getCampaign(id)`
  - `getAnalyticsOverview(from,to)`
- `PolicyService`
  - Given contact + channel returns what’s allowed (WA text allowed if last inbound ≤24h; otherwise template).
- `I18nService` (EN/FR) with JSON dictionaries; language selector in the header.
- `ThemeService` (Classic/Dark) with CSS variables; persist in localStorage.

COMPONENTS (standalone, OnPush)
- OnboardingWizardComponent
- TemplatesListComponent + TemplatePreviewComponent
- ContactsListComponent + ConversationThreadComponent (virtual scroll) + MessageComposerComponent
- CampaignBuilderComponent (ChannelPicker, TemplatePicker, SegmentBuilder, Scheduler, Review)
- AnalyticsOverviewComponent (KPI cards + charts)
- Header with language + theme toggles; ToastService; ErrorBoundary

STATE
- Use Signals store or NgRx:
  - `settings`, `templates`, `contacts`, `conversation`, `campaignDraft`, `analytics`
- Sync URL query params for campaign builder (channel/fallback/lang/template) for shareable drafts.
- Cache templates and recent searches in memory with TTL.

UI/UX
- Responsive, keyboard accessible, ARIA roles/labels, focus outlines.
- Clear policy hints (why a channel or text is disabled).
- Loading skeletons, empty/error states.
- Chart.js with default styling; no heavy theming needed.

TESTS
- Jest: ApiService (mocks), PolicyService, CampaignBuilder validators, MessageComposer rules.
- Cypress:
  - Onboarding happy path (save WA creds, verify webhook, send test)
  - Contact search → open conversation → policy blocks WA text outside 24h → send template
  - Create campaign with WA primary + Email fallback → shows queued with counts
  - Analytics loads charts with mocked API

I18N & THEME
- Provide `src/assets/i18n/en.json` + `fr.json` with keys for navigation, onboarding, templates, contacts, conversation, campaigns, analytics, settings, policy messages, toasts.
- `styles.scss`: CSS variables for Classic/Dark; `.theme-dark` on `<html>` toggles dark.

DELIVERABLES
- Full code for routes/components/services (TS/HTML/SCSS), environment (`API_BASE_URL`), i18n files, theme variables, Jest tests and a Cypress spec.
- Short README: how to run (`npm start`), set env, switch language/theme, and connect to API.
Generate the complete Angular implementation now.