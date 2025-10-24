# Compte rendu – Projet Pipelane

## Vue d’ensemble
- Monorepo articulé autour de trois applications : **pipelane-api** (ASP.NET 8 multi-tenant), **pipelane-front** (Angular 20, standalone + Angular Material) et **pipelane-marketing** (Astro + Tailwind).
- Positionnement produit : console omni-canal pilotant WhatsApp, Email et SMS, avec relances intelligentes assistées par IA, analytics consolidés et parcours marketing orientés conversion.
- Environnement local : SQL Server via `docker compose`, API sur `http://localhost:56667`, front sur `http://localhost:4200`, marketing sur `http://localhost:4321`. Les scripts `tools/inject-env.mjs` et `scripts/dev.*` synchronisent les bases d’URL et les env vars.

## Avancement global (tests au 24/10/2025)
- **Backend** : `dotnet test` (45 tests xUnit) OK, warnings NU1603 toujours bénins (QuestPDF auto-résolu en 2024.3.0).
- **Front Angular** : `npm run ui:test` (21 tests Jest) OK ; lint/format regroupés dans `npm run ui:check`.
- **Marketing** : `npm run test:a11y` + `npm run test:lighthouse` OK (accessibilité ≥ 0,95) et build Astro stable.
- Intégration continue : `.github/workflows/marketing-ci.yml` orchestre build + pa11y + Lighthouse ; backend/front workflows inchangés.

## Réalisations majeures

### Backend (.NET 8)
- **Canaux réels** : WhatsApp Cloud (`WhatsAppChannel`) et Twilio SMS (`SmsChannel`) gèrent envois, vérifications HMAC (`X-Hub-Signature-256`, `X-Twilio-Signature`), idempotence et stockage inbound/outbound (`WebhooksController`). Email s’appuie sur Resend avec vérif signature existante.
- **Relance intelligente** : `AiController` applique quiet hours, cap journalier, opt-out et enregistre les propositions dans `FollowupProposalStore`. `FollowupsController` valide les propositions (`proposalId`) en les enfilant dans l’Outbox. Nouveau rapport PDF via `ReportService` et endpoints `/api/reports/summary(.pdf)`.
- **Analytics enrichies** : `AnalyticsService.GetDeliveryAsync` retourne totaux, breakdowns et timeline ; `GetTopMessagesAsync` alimente `/api/analytics/top-messages`. Logs contextualisés (tenant, provider) + rate limiting (`MessageSendRateLimiter`, polices `RateLimiterOptions`).
- **Demo & Ports** : `DemoExperienceService` préseed 20 contacts et campagne de démo ; endpoint `POST /api/demo/run` protégé par `DEMO_MODE`. Ports automations (events sortants + actions entrantes) configurables via `AUTOMATIONS_*`.
- **Sécurité multi-tenant** : `TenantScopeMiddleware` refuse les `X-Tenant-Id` hors portée JWT (`tenant_ids`). Chiffrement AES-GCM pour secrets de canaux, PBKDF2 pour mots de passe.
- **Couverture ajoutée** : tests `FollowupsControllerTests` (validation, send-now, not-found) + stabilisation `AiControllerTests` (options messaging).

### Frontend (Angular 20)
- **Console opérateur** : navigation latérale responsive avec quick actions (campagne, import, démo). Onboarding guidé par ngx-shepherd (`TourService`). Mode démo distinct (badge + CTA).
- **Conversations** : `ConversationThreadComponent` expose carte “Prochaine relance” (angle, preview, actions) et maintenant valide via API (`proposalId`). Génération de message IA et classement IA accessibles, historique affiché avec statut provider.
- **Campaign Builder** : wizard 4 étapes avec réglage “Relance intelligente par défaut” + aperçu IA. Segment builder génère automatiquement le JSON et prévisualise la taille d’audience (`/api/followups/preview`).
- **Analytics** : vue enrichie avec filtres (Aujourd’hui / 7j / 30j / custom), graphes ng-apexcharts (area timeline, donut canal, bar top templates/sujets), KPI cards et états vides ; la barre “Top messages” hiérarchise désormais replies puis opens, avec fallback sur les templates livrés.
- **Infra front** : `ApiService` centralise headers (X-Tenant-Id), toasts snack-bar, endpoints demo/reports/followups ; tests Jest couvrent base URL, preview/validate follow-up, analytics mapping et toasts.

### Marketing (Astro)
- **Pages clés** : landing `/` refondue (hero, relance intelligente, workflow, preuves, CTA), `/prospection-ia`, `/relance-intelligente`, `/prix`, `/securite-rgpd`, blog structuré par `PostLayout`. Navbar adaptée (Produit, Prospection IA, Relance, Prix, Sécurité & RGPD, Ressources, Demander une démo).
- **Conversion & tracking** : `DemoForm` avec champs UTM cachés, message de succès, intégration `/api/demo-request`. `ConsentManager` charge GA4/LinkedIn uniquement après consentement (localStorage). Boutons “Lancer la démo” conditionnés par `PUBLIC_DEMO_MODE`.
- **Design system** : glassmorphism, tokens Tailwind on-surface/on-surface-strong, sections “Ce que vous gagnez”, “Comment ça marche”, carte relance intelligente, plan marketing (Good/Better/Best, essai 14 jours).

## Reste à faire / Points d’attention
1. **Preview réaliste côté Campaign Builder** : remplacer le `historySnippet` mocké par un extrait conversationnel réel dès que l’API exposera un endpoint de preview sécurisé.
2. **Monitoring & files d’attente** : `MessageSendRateLimiter` garde l’état en mémoire ; prévoir persistance et métriques (queue depth, temps d’attente) pour charges élevées.
3. **Analytics downstream** : vérifier exports, dashboards et API partenaires pour s’assurer qu’ils consomment le nouveau format (`timeline`, `TopMessagesResponse`) sans régression.
4. **Marketing QA continue** : surveiller les rapports `marketing-ci` (pa11y/Lighthouse) et corriger rapidement toute chute <95 ou contraste limite.
5. **Observabilité avancée** : brancher OpenTelemetry (traces/logs) vers un collecteur (OTLP/Seq) et envisager des dashboards (e.g. timeline d’erreurs webhooks).
6. **RBAC & secrets** : étendre les rôles (actuellement `role` unique) et s’assurer que les secrets de démo ne restent pas en clair dans `docker-compose.yml`.

## Commandes utiles
- **Backend** : `cd pipelane-api && dotnet restore && dotnet run` (ou `./scripts/dev.ps1|.sh`), tests `dotnet test`.
- **Front Angular** : `cd pipelane-front && npm ci && npm start`; qualité `npm run ui:check`, tests `npm run ui:test`, e2e `npm run ui:e2e` (serveur requis).
- **Marketing** : `cd pipelane-marketing && npm install && npm run dev`; QA `npm run test:a11y`, `npm run test:lighthouse`.
- **Demo** : activer `DEMO_MODE=true`, lancer `POST /api/demo/run` (via Postman/cURL) ou bouton “Launch demo” dans l’UI lorsque le flag est présent.

## Variables d’environnement (extraits)
- **Backend** (`pipelane-api/.env.example`) : `DB_CONNECTION`, `ENCRYPTION_KEY`, `JWT_KEY`, `OPENAI_API_KEY`, `OPENAI_MODEL`, `AI_DAILY_BUDGET_EUR`, `DAILY_SEND_CAP`, `QUIET_START`, `QUIET_END`, `AUTOMATIONS_*`, `DEMO_MODE`. Les credentials Meta/Twilio/Resend sont stockés chiffrés par tenant (`WA_*`, `TWILIO_*`, `RESEND_API_KEY`).
- **Front** (`pipelane-front/.env.example`) : `API_BASE_URL`, `DEMO_MODE`.
- **Marketing** (`pipelane-marketing/README.md`) : `PUBLIC_GA_ID`, `PUBLIC_LINKEDIN_ID`, `PUBLIC_DEMO_MODE`, `PUBLIC_CONSOLE_URL`.

## Santé du code & tests
- Tests unitaires et Jest passent systématiquement ; attention au fichier généré `pipelane-front/src/app/core/env.generated.ts` à ne pas valider en SCM.
- Warnings NU1603 (QuestPDF) sont bénins mais bruit : surveiller les mises à jour NuGet pour verrouiller la version désirée.
- Aucun e2e Cypress récent ; prévoir une passe “smart follow-up” & “analytics dashboards” avant livraison client.

## Prochaines étapes
1. **Brancher la preview de follow-up sur des conversations réelles** (attente d’un service backend dédié à la campagne).
2. **Instrumenter l’observabilité** : choisir une cible OTLP (Seq, Grafana Tempo…) et étendre `/health/metrics` si nécessaire.
3. **Élargir la couverture end-to-end** : lancer Cypress sur le flux “Smart follow-up”/analytics et vérifier que les exports PDF/top messages restent cohérents.
