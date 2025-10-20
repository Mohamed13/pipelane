# Compte rendu du projet Pipelane

## Vue d'ensemble
- Monorepo articule autour de trois applications : `pipelane-api` (.NET 8), `pipelane-front` (Angular 20) et `pipelane-marketing` (Astro + Tailwind).
- Positionnement produit : plateforme omni-canal multi-tenant pour orchestrer contacts, conversations, campagnes et analytics avec adaptateurs WhatsApp, Email et SMS.
- Infrastructure locale : SQL Server (docker-compose) + API + front. L'API applique migrations/seeding au demarrage et expose Swagger /health.
- Le code a recemment evolue sur l'orchestration (Outbox, Followups), l'analytics (`/analytics/delivery`) et l'integration email Resend.

## Backend (.NET 8)
### Realisations
- Architecture clean (Api/Application/Infrastructure/Domain) avec enregistrement Serilog + OpenTelemetry console. `Program.cs` configure JWT, CORS `localhost:4200/8080`, migrations auto et seeding demo.
- `MessagingService` gere l'envoi texte vs template, applique la regle WhatsApp 24h (`ChannelRulesService`) et enfile les templates dans l'`Outbox`.
- `OutboxProcessor` traite les jobs par lot (lock 30 s), tente jusqu'a 5 envois avec backoff exponentiel, puis cree `Message` + `MessageEvent` avec statut, provider, horodatages.
- `CampaignRunner` promeut les campagnes `pending/running`, prend jusqu'a 100 contacts et insere les messages dans l'Outbox; `CampaignStatus` passe a `Done` en fin de lot.
- `FollowupScheduler` (Quartz toutes 15 min) genere des `FollowupTask` 24 h apres lecture sans reponse et re-enfile un template `nudge-1` apres 48 h sans inbound.
- Canal email complet : `EmailChannel` s'appuie sur Resend (HTTP client dedie), `ResendWebhookVerifier` (HMAC) et `ResendWebhookProcessor` (mapping events -> statuts, idempotence). Tests xUnit couvrent verifier et processor.
- `AnalyticsController` expose `/analytics/overview` et `/analytics/delivery`; `AnalyticsService.GetDeliveryAsync` agrege totaux, breakdown par canal et par template.
- Authentification : `AuthController` offre register/login/me (JWT HMAC, PBKDF2), `HttpTenantProvider` extrait `X-Tenant-Id`, encryption AES-GCM pour secrets.
- Tests existants (`Pipelane.Tests`) couvrent MessagingService, regles WhatsApp et pipelines Resend.

### Points a surveiller
- Adaptateurs WhatsApp et SMS restent largement stubs (pas d'appel providers, pas de verification signature) -> a finaliser avant production.
- `CampaignRunner`/`Outbox` n'appliquent pas de segmentation avancee, pas de dedupe par contact, pas de rate limiting multi-tenant.
- `FollowupScheduler` et `AnalyticsService` n'ont pas de tests automatises; risque de regressions sur les requetes SQL complexes.
- Securite : clefs par defaut dans docker-compose, RBAC limite a une chaine `role`, validations DTO a elargir.
- Observabilite : OTel exporte sur la console uniquement, pas de pipeline centralise pour traces/metrics.

## Frontend (Angular 20)
### Realisations
- Application standalone Angular 20 avec Angular Material, theming geres par `ThemeService`, routes protegees (`authGuard`) definies dans `app.routes.ts`.
- `ApiService` centralise tous les appels, injecte `X-Tenant-Id`, gere les toasts d'erreur et reconstruit les messages a partir de `HttpErrorResponse`.
- `src/theme/_tokens.scss` sert de design tokens (dark glass, gradients, motions) et nourrit les composants partages `kpi-card` (sparkline) et `chart-card` (ng-apexcharts).
- `AnalyticsOverviewComponent` exploite désormais ng-apexcharts (area/donut/bar) + KPI sparklines et MatTable sur `/analytics/delivery`.
- `ConversationThreadComponent` propose bulles vitrées, badges provider/statut, insight panel repliable et composer texte/template avec helpers variables.
- `CampaignBuilderComponent` devient un wizard 4 étapes (audience, message, schedule, review) avec preview dynamique via `/api/followups/preview`.
- Scripts : `npm run ui:check`, `npm run ui:test`, `npm run ui:e2e` complètent lint/tests (Cypress nécessite le front servi localement).
- `TourService` (ngx-shepherd) déclenche un onboarding guidé (flag `localStorage`, replay dans le menu Aide).

### Points a surveiller
- Tests front renforcés : Jest couvre `ApiService`, `tour.service`, mapping analytics, tooltips. Cypress dispose d'un socle (tour, analytics, campaign builder, onboarding) — prévoir de lancer `npm run ui:e2e` avec `npm start` actif.
- Plusieurs vues dependent de comportements backend encore basiques (followups preview, conversation status) -> prevoir garde-fous UI et etats de chargement.
- Gestion erreurs/offline dispersee; uniformiser les toasts/snackbars et la detection tenant manquant.
- Verifier que `env.generated.ts` reste hors VCS et documenter toute nouvelle cle `.env`.

## Site marketing (Astro)
### Realisations
- Landing page riche couvrant hero, social proof, parcours produit, features, timeline, integrations, pricing, FAQ, success stories et CTA final, avec dark/light toggle et effets parallax soft.
- Design system Tailwind (`tokens`, utilitaires `glass`/`scrim`), composants modulaire (`Section`, `FeatureCard`, `PricingCard`, `CTA`, etc.).
- Endpoint `/api/demo-request` valide les champs (name/email/company/volume), journalise et repond JSON `{ ok: true }`. Scripts `npm run test:a11y` (pa11y-ci) et `npm run test:lighthouse`.

### Points a surveiller
- Pas de persistence pour les demandes demo -> prevoir integration CRM ou file queue.
- Plusieurs assets lourds (SVG) + animations : surveiller le score Lighthouse (>95) et optimiser si nouveaux visuels.
- Les fichiers contiennent des caracteres `?` a la place d'accents (encodage a verifier/normaliser en UTF-8 ou ASCII propre).

## Qualite et operations
- Scripts standards par couche (`scripts/*.ps1|sh`, `npm run lint/test`, `pnpm test:a11y`...). A executer avant toute PR.
- Couverture tests backend partielle : Outbox/Followup/Analytics manquent encore de specs; Angular quasi vide; marketing sans e2e.
- `docker-compose.yml` lance SQL Server + API + front; ajuster secrets avant prod.
- Swagger (`pipelane-api/swagger.json`) est versionne : le regenerer quand de nouveaux endpoints sont exposes et relancer `npm run gen:api`.
- OpenTelemetry et Serilog sont en place mais aucun exporter/collecteur distant configure.

## Priorites recommandees
1. Finaliser les integrations providers (WhatsApp Cloud, SMS) avec verifications signatures, gestion erreurs reseau et secrets separes par tenant.
2. Durcir le pipeline campagne/outbox : segmentation avancee, dedupe contact, throttling, tests sur CampaignRunner/FollowupScheduler/AnalyticsService.
3. Etendre la couverture de tests : xUnit (analytics, followups, adapters), Jest/Cypress (ApiService, analytics, conversation, campagnes), pa11y/Lighthouse dans la CI marketing.
4. Normaliser la documentation/encodage (marketing), mettre a jour swagger/types, documenter les nouvelles variables dans `.env.example`.
5. Renforcer l'Ops : secrets non par defaut, workflows CI actifs pour build/test, exporter OTel vers une stack d'observabilite et surveiller les jobs background.

## RECAP FINAL
- **Endpoints IA**  
  - `POST /api/ai/generate-message` — Exemple : `{"channel":"email","language":"fr","context":{"firstName":"Alex","company":"NeonCorp","pitch":"Nous aidons...","calendlyUrl":"https://cal.com/demo","lastMessageSnippet":"On se tient au courant ?"}}` → `{ "subject": "...", "text": "...", "html": "...", "languageDetected": "fr" }`.  
  - `POST /api/ai/classify-reply` — Exemple : `{"text":"Sounds good, book me Tuesday."}` → `{ "intent": "Interested", "confidence": 0.91 }`.  
  - `POST /api/ai/suggest-followup` — Exemple : `{"channel":"email","timezone":"Europe/Paris","lastInteractionAt":"2025-01-08T15:00:00Z","read":true,"historySnippet":"Merci pour la demo","performanceHints":{"goodHours":[10,11,14]}}` → `{ "scheduledAtIso": "2025-01-10T09:30:00Z", "angle": "value", "previewText": "..." }`.
- **Ports automations (optionnels)**  
  - Webhook sortant : `AutomationEventPublisher` pousse `message.sent`, `message.status.changed`, `contact.created` vers `AUTOMATIONS_EVENTS_URL` quand `AUTOMATIONS_EVENTS_ENABLED=true` + token valide.  
  - Webhook entrant : `POST /api/automations/actions` (header `X-Automations-Token`) gère `send_message`, `create_task`, `schedule_followup`.
- **Ecrans Front**  
  - `Conversations` : boutons “Générer un message (IA)”, “Classer la réponse (IA)”, switch “Relance intelligente” et carte “Prochaine relance” (`pipelane-front/src/app/features/contacts/conversation-thread.component.*`).  
  - `Campaign Builder` : commutateur “Relance intelligente par défaut” dans l’étape planification (`pipelane-front/src/app/features/campaigns/campaign-builder.component.html`).  
  - Tutoriel ngx-shepherd (5 étapes) rejouable via menu Aide (`pipelane-front/src/app/core/tour.service.ts`).
- **Scénarios smoke**  
  1. Générer → envoyer : `POST /api/ai/generate-message`, insérer l’aperçu dans le composeur, envoyer via `/messages/send`.  
  2. Classer une réponse : `POST /api/ai/classify-reply`, vérifier le badge d’intention dans la conversation.  
  3. Relance intelligente : activer le switch, appeler `POST /api/ai/suggest-followup`, valider la proposition (planifie un job dans l’outbox).  
  Ces parcours sont couverts par `SmokeScenarioTests` (`pipelane-api/tests/Pipelane.Tests/SmokeScenarioTests.cs`).
- **Variables d’environnement clés**  
  - Backend (`pipelane-api/.env.example`) : `OPENAI_API_KEY`, `OPENAI_MODEL`, `AI_DAILY_BUDGET_EUR`, `DAILY_SEND_CAP`, `QUIET_START`, `QUIET_END`, `AUTOMATIONS_*`.  
  - Front (`pipelane-front/.env`) : `API_BASE_URL`.  
  - Prévoir `RESEND_API_KEY`, secrets DB/JWT, et tokens automations.
- **Commandes utiles**
  - Backend build/tests : `dotnet restore`, `dotnet build`, `dotnet test` (ou scripts `./scripts/build.ps1`, `./scripts/test.ps1`).  
  - Front : `npm install`, `npm start`, `npm run ui:test`, `npm run ui:check`, `npm run ui:e2e`.  
  - Seeder exécuté au démarrage de l’API (Quartz + migrations) pour préparer la démo multi-canal.
