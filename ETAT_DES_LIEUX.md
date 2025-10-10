# Etat des lieux du projet Pipelane

## Vue generale
- Monorepo organise en backend .NET 8 (`pipelane-api`) et frontend Angular 17 (`pipelane-front`), avec scripts et docker-compose pour lancer SQL Server, l API et le front.
- L objectif global est une plateforme omni-canal avec tenants multiples: stockage contacts/messages/campagnes, envoi via adaptateurs (WhatsApp, email, SMS), webhooks entrants, analytics et console operateur.
- La documentation de reference (`TODO-net8.md`, `TODO-angular.md`) decrit une ambition plus large que l etat actuel: plusieurs composants sont en place mais encore partiellement implantes ou relies a des stubs.

## Backend (.NET 8)

### Architecture et infrastructure
- Solution `Pipelane.sln` composee des projets `Pipelane.Api`, `Pipelane.Application`, `Pipelane.Infrastructure`, `Pipelane.Domain`, plus les tests `Pipelane.Tests`.
- `Program.cs` configure Serilog, EF Core, OpenTelemetry (traces console), authentication JWT, CORS, FluentValidation et enregistrement des services applicatifs et adaptateurs de canal.
- Multi-tenant: `HttpTenantProvider` lit `X-Tenant-Id`; `AppDbContext` applique un filtre global sur `TenantId` pour toutes les entites derives de `BaseEntity`.
- Connexion base par defaut cible SQL Server (chaine `Server=localhost\\SQLEXPRESS...`) et docker-compose depose un conteneur MSSQL; la spec initiale mentionnait PostgreSQL, ecart a noter.

### Modele et persistence
- Entites couvrent contacts, consents, conversations, messages, templates, campagnes, events, conversions, lead scores, channel settings, outbox, users (toutes avec `TenantId`).
- `AppDbContext` ajoute indexes (messages, contacts, templates, campaigns, consents, etc.) et precision pour `Conversion.Amount`.
- `DataSeeder` cree un tenant demo, un utilisateur owner et des contacts/messages exemples + template `welcome`.
- Scripts `scripts/*.sh|ps1` pour build/test/lint/format/dev/migrate; `migrate` applique les migrations (assume `dotnet-ef` ou fallback en lancant l API).

### Services, canaux et traitements
- `IMessageChannel` abstrait l envoi et les webhooks; trois implementations (WhatsApp/Email/Sms) existent mais restent des stubs qui loggent et retournent un `SendResult` fictif.
- `MessagingService` gere validation contact, regles WhatsApp 24h via `ChannelRulesService`, envoi texte direct ou enqueue template dans la table Outbox.
- `OutboxService` ajoute des jobs en base; `OutboxProcessor` (BackgroundService) execute en boucle: prend les jobs, appelle les canaux (avec retry Polly), cree/consolide conversations et messages sortants.
- `CampaignRunner` selectionne les campagnes Pending/Running, charge jusqu a 100 contacts et enfile des jobs Outbox; la segmentation reste rudimentaire (`SegmentJson` reutilise en payload). `FollowupScheduler` est un squelette (Delay + TODO logique).

### API exposee
- Controleurs Auth (login/register/me), Users (listing CRUD basique par tenant), Onboarding (stockage chiffree des credentials via `AesGcmEncryptionService`), Contacts (import JSON/CSV + recherche paginatee), Conversations (dernier thread), Messages (envoi via `IMessagingService`), Templates (listing + refresh stub), Campaigns (creation + consultation), Analytics (compte total et repartition par canal), Conversions (enregistrement), Webhooks (WhatsApp/Email/SMS, verifications de signature non implementees).
- Startup applique migrations + seed en dev, expose Swagger et healthcheck `/health`.

### Tests et qualite
- Tests unitaires (`MessagingServiceTests`, `ChannelRulesServiceTests`) couvrent la logique WhatsApp 24h, enqueue Outbox et blocage des textes hors fenetre; pas de tests integration ou API.
- Analyzers/lint et formatage relies aux scripts fournis; pas d integration CI visible dans le repo (workflow GitHub absent).

### Points a surveiller
- Adaptateurs canaux, webhooks et orchestrations sont encore mock/stub (pas d appels providers, pas de gestion STOP, unsubscribe, quiet hours, etc.).
- Campagnes et follow-ups ne font pas encore d expansion de segments ni de planification J+N; `FollowupScheduler` est vide.
- Analytics retourne seulement total/byChannel; le frontend attend plus (delivered/read/failed) et retombe sur donnees demo.
- Securite basique (JWT dev key) et absence de validations avancees (rate limit, RBAC fin, audit) a renforcer avant prod.
- Base par defaut SQL Server: adapter scripts/secrets si PostgreSQL reste l objectif cible.

## Frontend (Angular 17)

### Shell, theme et UX
- Application bootstrap via `main.ts` avec Angular standalone API, routeur et intercepteur auth. Shell Material (sidenav + toolbar) dans `AppComponent` avec persistence localStorage, recherche, toggle theme, selection langue.
- Theme tokens SCSS (`src/theme/_tokens.scss`) definissent couleurs, espacement, rayons, elevations, et supportent mode sombre via classe `theme-dark`. Styles globaux ajoutent animations et utilities.
- Directive `RevealOnScroll` et composants `KpiCardComponent`, `ChartCardComponent` fournissent micro-animations et charts (Chart.js via ng2-charts).

### Fonctionnalites ecrans
- `AnalyticsOverviewComponent` affiche KPI, charts line/bar/donut, table MAT avec tri/pagination; consomme `ApiService.getAnalyticsOverview` mais garde demos (DEMO_ROWS/DEMO_TOTAL) si `USE_DEMO_DATA` ou API incomplere.
- Onboarding: formulaires WhatsApp/Email/SMS avec validations minimales, envoie `ChannelSettingsPayload` via ApiService.
- Templates: listing Material (tri/table) avec refresh (stub backend).
- Contacts: table paginatee avec recherche, navigation vers `ConversationThreadComponent`.
- Conversation thread: affiche chronologie, gere composer texte vs template selon regle WhatsApp (PolicyService), appelle `/messages/send` et recharge conversation.
- Campaign builder: stepper Material (details + template/schedule), chips fallback, preview JSON, appel POST `/campaigns`, Snackbar feedback. D autres routes (`settings`, `analytics`) existent, certains contenus restent placeholders.

### Services, outils et tests
- `ApiService` centralise appels HTTP et ajoute `X-Tenant-Id` optionnel, `auth.interceptor` ajoute JWT + tenant automatique.
- `AuthService` gere stockage token (localStorage), decode `tid`, redirect login -> analytics; `AuthGuard` bloque routes protegees.
- Environnement: `tools/inject-env.mjs` genere `env.generated.ts`, `fetch-swagger.mjs` peut lancer l API pour recuperer Swagger puis `openapi-typescript` genere types.
- Tests Jest limites a `auth.interceptor.spec.ts` (verification headers) et `policy.service.spec.ts` (regle 24h). Pas de tests UI e2e consommes (Cypress configure mais aucun test committe).

### Points d attention
- Plusieurs ecrans dependent de reponses API plus riches que l implementation actuelle (analytics detaillee, templates refresh, segmentation). Risque de mismatch tant que le backend reste stub.
- Feedback utilisateur minimal (pas encore de gestion d erreurs detaillee, toasts limites) et pas de gestion d etat global (NgRx, query) pour caches ou rafraichissements.
- Auth UI simple (pas de creation de compte, reinitialisation, roles) et les routes supposent un token valide.

## Operations et dev
- `docker-compose.yml` orchestre SQL Server, API (port 5000) et front Nginx (port 8080) avec build Dockerfiles dedies.
- README backend/front donnent instructions run/test. Scripts dotnet et npm couvrent build/test/lint/format. Aucun pipeline CI present dans le repo.

## Suite suggeree (haut niveau)
- Remplacer les stubs de canaux par integrations concretes (Meta Cloud, ESP, SMS) avec gestion erreurs, opt-out et webhooks entrants reels; renforcer securite (verifications signatures, chiffrement secrets).
- Consolider orchestrations: segment expansion, quotas, batch sizing, FollowupScheduler, analytics completes (status, conversions, KPIs) et alignement front/back.
- Ajouter couverture tests (integration API, tests unitaires supplementaires, tests frontend e2e) et automatiser via pipeline CI.
- Preparer configuration production (secrets, logging structure, healthchecks, observabilite, support Postgres si requis par cahier des charges).
