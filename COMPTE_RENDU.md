# Compte rendu du projet Pipelane

## Vue d'ensemble
- Monorepo structure autour de trois axes :
  - `pipelane-api/` : backend .NET 8 (Api/Application/Infrastructure/Domain + tests).
  - `pipelane-front/` : console operateur Angular 20.
  - `pipelane-marketing/` : nouveau site marketing Astro + Tailwind.
- Objectif global : plateforme omni-canale multi-tenant (contacts, conversations, campagnes, analytics, suivis automatises) avec adaptateurs WhatsApp/Email/SMS.
- Codebase active mais non stabilisee : nombreuses evolutions recentes (migrations, canaux, analytics, marketing). Revue approfondie recommandee avant merge/pull request.

## Backend (.NET 8)
### Realisations marquantes
- Modele enrichi : `Message` integre provider/status detailles et horodatages, `MessageEvent` historise les evenements par provider (migrations SQL ajoutees et index uniques sur `TenantId` + `ProviderMessageId`).
- Pipelines asynchrones revisites :
  - `OutboxProcessor` applique desormais 5 tentatives maximum avec backoff exponentiel, mise a jour des statuts/timestamps et journalisation des erreurs.
  - `FollowupScheduler` (Quartz) planifie des taches de relance (task "Follow up" + envoi template `nudge-1`) selon absence d'activite 24 h / 48 h.
- Services : `AnalyticsService.GetDelivery(from,to)` execute des regroupements SQL pour totaux et ventilations par canal/template ; expose via `GET /api/analytics/delivery`.
- Email : canal Resend implemente (envoi template -> `ProviderMessageId`), webhook `POST /api/webhooks/email/resend` avec verification HMAC (`IProviderWebhookVerifier`) et idempotence ; stockage des evenements/statuts alimente `Message` et `MessageEvent`.
- Outillage : scripts lint/format/migrate, `Directory.Build.props`, `.editorconfig`, nouveaux controleurs (followups, webhook email, users).

### Points restants / risques
- Verifications incompletes :
  - Pas de tests couvrant `ResendWebhookVerifier`, `AnalyticsService` ou `FollowupScheduler` (TODO ouverts).
  - Scenarios d'echec Outbox partiellement couverts (tests unitaires/integration a ajouter sur les nouvelles branches de code).
- Integrations : adaptateurs WhatsApp/SMS toujours relies a des stubs (pas de calls providers, pas de verification signature Meta/Twilio).
- Donnees : segmentation campagne, preview contacts, regles quiet hours ou STOP SMS restent rudimentaires.
- Observabilite : Serilog/Otel en place mais pipeline CI et supervision non verifies.
- Securite : cles JWT/RESEND par defaut, RBAC partiel, validations d'entrees a approfondir.

## Frontend (Angular 20)
### Realisations marquantes
- Migration Angular 20 terminee (CLI/Material/CDK) avec build `npm run build` concluant. Charts et theming alignes.
- Fonctionnalites :
  - Dashboard analytics consomme `GET /api/analytics/delivery`, rend totaux + charts line/donut avec fallback demo.
  - ConversationThread : badges statut (Queued/Sent/Delivered/Opened/Failed), badge provider, polling 5 s jusqu'a statut terminal.
  - Onboarding : bouton "Envoyer un email de test" via `/api/messages/send`.
  - Campaign builder : champs `ScheduleAt` + `BatchSize`, appel `/api/followups/preview` affichant le volume cible.
  - `ApiService` ajoute `X-Tenant-Id` et publie des toasts d'erreur detailles.
- Tooling : script `inject-env.mjs` mis a jour, build Angular propre.

### Points restants / risques
- Tests unitaires absents sur `ApiService`, preview campagne et polling conversation.
- UX : gestion erreurs/offline a ameliorer, consolidation des toasts.
- Alignement API : certaines vues reposent encore sur des reponses stub (templates, analytics avancees) -> prevoir garde-fous UI si backend incomplet.

## Site marketing (Astro + Tailwind)
### Realisations marquantes
- Nouveau projet `pipelane-marketing/` : landing page complete (hero, social proof, produit, benefices, how-it-works, features, integrations, pricing, FAQ, CTA final) avec animations scroll/parallax et dark mode toggle persistant.
- Composants reutilisables (`Section`, `FeatureCard`, `PricingCard`, `CTA`, `Icon`, etc.) et design system Tailwind (tokens, utilities glass/neon/chip).
- Endpoint `/api/demo-request` valide le formulaire (name/email/company/volume), journalise et renvoie JSON `{ ok: true }`; assets SEO (favicon, OG, sitemap, robots) ajoutes.
- Build `npm run build` OK, README documente l'installation et les scripts.

### Points restants / risques
- Objectif Lighthouse 95+ non verifie (tests performance/accessibilite a lancer).
- Formulaire demo : aucune persistence/CRM -> integration a planifier.
- QA : pas de tests e2e ou verifications accessibilite automatisee.

## DevOps et qualite
- Builds locaux verifies :
  - `pipelane-marketing`: `npm run build` -> OK.
  - `pipelane-front`: `npm run build` -> OK (env genere automatiquement).
  - `pipelane-api`: scripts build/test non executes durant cette passe (a rejouer pour valider les changements importants et migrations).
- CI : workflows GitHub presents (`.github/workflows/ci-backend.yml`, etc.) mais non executes/valides recemment.
- Couverture de tests insuffisante (backend et frontend) au regard des nouvelles fonctionnalites.

## Priorites recommandees
1. Stabiliser le backend : rejouer build/tests, ajouter tests unitaires/integration pour Outbox, FollowupScheduler, webhook Resend, analytics. Valider les migrations (indexes uniques) sur une base locale.
2. Finaliser les integrations providers : WhatsApp/SMS reels, verification signatures, gestion STOP/unsubscribe, quiet hours, erreurs reseau.
3. Renforcer les tests frontend : unitaire (ApiService, campaign preview) et e2e (Cypress) pour scenarios critiques.
4. Verifier l'alignement front/back : garantir que les DTOs exposes par l'API correspondent aux attentes du front (analytics delivery, followups preview, statuts messages).
5. Relancer la chaine Ops : workflows CI, audit securite (dependances, secrets), observabilite (Serilog/Otel), readiness pour deployment (containers, variables d'env).
6. Site marketing : connecter le formulaire demo a une persistence externe et executer tests performance/accessibilite.

## Notes complementaires
- Arbre Git tres modifie : coordonner avec les autres contributeurs avant merge (nombreuses modifs preexistantes non liees).
- Documenter davantage (schemas BDD, flux Slack/Email/SMS, contrats API) pour faciliter l'onboarding equipe.
