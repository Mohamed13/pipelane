Tu travailles dans un monorepo :
- pipelane-api  (.NET 8, Clean Architecture)
- pipelane-front (Angular 20 + Angular Material)
- pipelane-marketing (Astro + Tailwind)

OBJECTIF
- Revue et corrections globales : ajouter les TESTS manquants (unit/int/e2e), supprimer le code mort, fiabiliser l’implémentation, sécuriser les parcours, automatiser les vérifications (CI).
- Exigence recette : **tout fonctionne**, perf/a11y correctes, basiques sécurité validées.

RÈGLES
- Commits atomiques (“feat/fix/chore/test/docs”), message clair.
- Pas de modification de contrats d’API ; si breaking, créer un flag temporaire ou compat rétro.
- Ajoute des scripts “smoke” reproductibles.
- S’appuyer sur les guides de tests officiels : Angular Testing (components/services), .NET xUnit, Astro + Playwright/Pa11y. (Réfs à suivre.)

================================================================
SECTION A — INVENTAIRE & CODE MORT (front + marketing + api)
================================================================
1) Détecte code mort / imports non utilisés :
   - Front : `depcheck`, `ts-prune`, `eslint --report-unused-disable-directives`.
   - Marketing : `depcheck`, revue manuelle des composants non référencés.
   - API : warnings analyzers .NET, fichiers/projs non référencés.
2) Supprime/retire tout code/asset mort. Évite la régression (tests à venir).
Commit: `chore(repo): purge code mort (depcheck/ts-prune/analyzers)`

/compact

================================================================
SECTION B — TESTS · pipelane-front (Angular 20)
================================================================
1) **Unit tests** (Jasmine/Karma ou Jest adapter) — cf. Angular testing guide :
   - Components clés : HunterPage, CampaignBuilder, ConversationThread, Analytics.
   - Services : ApiService (headers, erreurs), Rule/Policy services, TourService.
   - Pipes/Directives utilisés.
   (Angular docs: https://angular.dev/guide/testing) 
2) **Harness & TestBed** pour interagir avec Material proprement (inputs, table, stepper).
3) **Integration / shallow** :
   - Hunter flow minimal (search → table binds), Campaign preview (segment JSON) avec stub API.
4) **E2E léger** (Cypress/Playwright au choix) :
   - smoke: login (si présent) → Hunter → “Magic Pick” → créer liste → créer cadence.
   - analytics: changement de période → maj graphiques.
   - conversation: preview relance → valider/snooze.
Critères d’acceptation :
   - `npm run ui:test` vert; scénarios E2E “smoke” passent localement.
Commits:
   - `test(front): units components/services + harness`
   - `test(front): e2e smoke flows (hunter/campaign/analytics)`

(Refs : Angular testing components/scenarios) :contentReference[oaicite:1]{index=1}
/compact

================================================================
SECTION C — TESTS · pipelane-api (.NET 8)
================================================================
1) **Unit** (xUnit) — logiques pures :
   - Rule engine/Followups (planif créneaux, angle).
   - AnalyticsService (agrégations).
   - Mappers/DTOs (null-safety, fallback).
   (Best practices xUnit + .NET testing) :contentReference[oaicite:2]{index=2}
2) **Intégration** :
   - Controller /api/followups/preview (GET/POST) → 200/400 ; /api/lists → 200 [] ; /api/demo/run si DEMO_MODE.
   - EF Core : tests en mémoire **ou** Testcontainers selon infra (facultatif).
3) **Contract smoke** :
   - S’assurer que /health et /health/metrics renvoient OK avec infos provider DB.
Critères :
   - `dotnet test` vert ; scénarios preview/lists passent.
Commits:
   - `test(api): xUnit units (rules/analytics/mappers)`
   - `test(api): integration controllers (preview/lists/health)`

(Refs : .NET xUnit getting started & best practices) :contentReference[oaicite:3]{index=3}
/compact

================================================================
SECTION D — TESTS · pipelane-marketing (Astro)
================================================================
1) **E2E Playwright** (Astro docs) :
   - home rendering, nav links, CTA visibles, LCP asset présent.
2) **A11y automatisée** :
   - Pa11y CI (ou axe) sur pages clés : /, /prospection-ia, /relance-intelligente, /prix, /securite-rgpd.
3) **Lighthouse CI** (GitHub Action) :
   - Vérifier perf/accessibility/best-practices/SEO avec budgets min (exigence recette).
Critères :
   - `npm run test:a11y` OK ; Lighthouse CI ≥ 95 sur mobile.
Commits:
   - `test(marketing): playwright smoke`
   - `chore(a11y): pa11y-ci config`
   - `chore(perf): lighthouse-ci action`

(Refs : Astro testing & Pa11y CI; Lighthouse CI / GitHub Action) :contentReference[oaicite:4]{index=4}
/compact

================================================================
SECTION E — PERF · Core Web Vitals & Budgets
================================================================
1) Budgets **Lighthouse CI** (web.dev) :
   - LCP image unique (hero) eager ; autres lazy.
   - Poids JS/CSS max par page (définir budgets réalistes).
2) Mesure Web Vitals en prod (web-vitals lib) — envoi console/logs basiques.
3) Réductions rapides :
   - Angular : OnPush là où sûr, `source-map-explorer` pour gros bundles, suppression d’icônes non utilisées (tree-shake).
   - Astro : réserver ratios (CLS), éviter filtres lourds sur LCP.
Critères :
   - Budgets respectés en CI ; rapport LHCI stocké.
Commits:
   - `chore(perf): LHCI budgets + web-vitals ping`
   - `chore(front): bundle check + tree-shake icons`
(Refs : Core Web Vitals guidance & improvements) :contentReference[oaicite:5]{index=5}
/compact

================================================================
SECTION F — A11y · checks rapides
================================================================
1) Pa11y/axe sur pages clés ; contrastes AA ; focus visibles ; labels formulaires ; alt text non décoratifs.
2) Astro : vérifier via “Accessible Astro” checklist.
Commits:
   - `fix(a11y): labels/focus/contrast`
   - `test(a11y): pa11y-ci sitemap`
(Refs : Accessible Astro) :contentReference[oaicite:6]{index=6}
/compact

================================================================
SECTION G — SÉCURITÉ · ASVS/Top 10 (basiques)
================================================================
1) **Headers** côté edge/platform (Vercel/Netlify/Render) :
   - HSTS, X-Content-Type-Options, Referrer-Policy, CORS strict (domains front).
2) **Form & API** :
   - Validation côté serveur (null/longueur/types).
   - Désactivation de tout endpoint debug/DEMO en prod.
3) **Secrets** :
   - Envs uniquement (Render/Netlify/Vercel), jamais en repo ; rotation clé JWT.
4) **Check-lists OWASP** :
   - S’inspirer de **ASVS** pour vérif technique, et Top 10 pour sensibilisation.
Commits:
   - `chore(security): headers + env checks + demo off`
   - `docs(security): ASVS/Top10 checklist appliquée`
(Refs : OWASP ASVS & OWASP Top 10) :contentReference[oaicite:7]{index=7}
/compact

================================================================
SECTION H — CI · Qualité · Hooks
================================================================
1) **GitHub Actions** :
   - API: `dotnet build/test`
   - Front: `npm ci && npm run ui:check && npm run ui:test`
   - Marketing: `npm run build && npm run test:a11y && lhci autorun`
2) **Qualité** :
   - ESLint + Prettier (front/marketing) ; .editorconfig repo.
   - .NET analyzers, nullable enabled ; warnings as errors pour projets critiques.
3) **Pre-commit hooks** (Husky) : lint-staged TS/SCSS/MD.
Commits:
   - `ci: gh-actions (api/front/marketing + lhci + pa11y)`
   - `chore(quality): eslint+prettier+analyzers + hooks`
(Refs : Lighthouse CI GH Action, Angular testing guide) :contentReference[oaicite:8]{index=8}
/compact

================================================================
SECTION I — SMOKE SCRIPTS & RÉCAP
================================================================
1) Ajoute `/scripts` :
   - `smoke-api.sh` : /health, /analytics/overview, preview GET/POST (retour 200/400 attendu).
   - `smoke-front.sh` : ouvre app, vérifie absence d’erreurs console majeures (grep).
   - `smoke-marketing.sh` : build + pa11y + lhci local.
2) Génère un **RÉCAP FINAL** (dans la sortie CI) :
   - Nombre de tests ajoutés par module ; scénarios e2e passants.
   - LHCI scores & budgets ; a11y violations=0 ; en-têtes de sécurité actifs.
   - Liste des fichiers supprimés (code mort).
Commit final:
   - `chore(scripts): smoke + recap final`
/compact
