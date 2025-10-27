Tu travailles dans un monorepo :
- pipelane-api (ASP.NET 8, multi-tenant, EF Core)
- pipelane-front (Angular 20 + Angular Material + ng-apexcharts)
- pipelane-marketing (Astro + Tailwind)

Objectif
Mettre le repo au niveau “propre et robuste” :
- Nettoyage : code mort, usings/imports inutiles, TODO obsolètes, logs verbeux.
- Factorisation : utilitaires partagés, mappers, guard clauses, types et constantes centralisés.
- Null-safety : nullable enable (.NET), TS strict, garde-fous dans les templates Angular.
- Commentaires/documentation : FR clair (/// XML côté API, /** */ côté TS, README).
- Linters/formatters : ESLint/Prettier (front/marketing), analyzers Roslyn/StyleCop (.NET).
- Tests : seuils minimaux, smoke scripts, e2e de base.
- Hooks & CI : husky + lint-staged, commitlint, GitHub Actions (build/test/lint/QA).

RÈGLES
- Procède SECTION par SECTION. Pour chaque section :
  1) Ouvre les fichiers concernés, 2) résume ce que tu fais, 3) fais des commits courts et explicites,
  4) vérifie : dotnet build/test (API), npm run build && npm run ui:test (Front), npm run build && test:a11y && test:lighthouse (Marketing),
  5) exécute /compact.
- N’altère pas les contrats API existants (ajoute des champs optionnels si nécessaire, ne supprime pas).
- Tous les commentaires/captions/docstrings en **français** (clairs et concis).
- Ajoute ACCEPTANCE à la fin de chaque section.

========================================================
SECTION A — Hygiène globale du repo
========================================================
Tâches
1) .editorconfig (racine) : indentation 2 (web), 4 (.NET), UTF-8-BOM off, fin de ligne LF, trailing spaces trim, newline EOF.
2) .gitattributes : normaliser fins de lignes, traiter *.md comme text.
3) .gitignore : node_modules, dist, build, coverage, .angular, .vs, bin/obj, .env*, env.generated.ts, storage/*.json.
4) LICENCE (MIT) + README racine : “Qualité & CI”, comment lancer, conventions de commit.
5) CODEOWNERS (option) : définir les chemins et propriétaires.

ACCEPTANCE
- Lint de base ne remonte pas d’avertissements sur l’édition (EOL, BOM).
- README met à jour “Qualité & CI”.
/compact

========================================================
SECTION B — API (.NET 8) : Nullables, analyzers, factorisation
========================================================
Tâches
1) Nullables & Warnings-as-errors
- Directory.Build.props : `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- Exclure ponctuellement des règles trop bruyantes via `NoWarn` documenté.

2) Analyzers & Style
- Ajouter `Microsoft.CodeAnalysis.NetAnalyzers`, `StyleCop.Analyzers` (règles pragmatiques).
- stylecop.json : règles de noms, documentation requise pour **public**.

3) Guard Clauses & null-safety
- Ajoute une classe util `Guard` (ex. `NotNull`, `NotDefault`, `NotEmpty`) + usages dans services/ctrl.
- Remplacer `if (x == null) throw new ...` par `ArgumentNullException.ThrowIfNull`.
- DTO -> Domain : mappers explicites (records immutables).

4) EF Core bonnes pratiques
- Lecture : `.AsNoTracking()`, projections (`Select`) ; pas d’entités traquées inutilement.
- Indexes & contraintes vérifiées dans `OnModelCreating`.
- DbContext Scoped, CancellationToken partout, `await` suffixe Async cohérent.

5) ProblemDetails & logs FR
- Middleware unifié d’erreurs → ProblemDetails (code, titre FR, détail FR).
- Logs Serilog enrichis (tenantId, userId, correlationId, provider, messageId).

6) Commentaires FR
- XML `///` sur contrôleurs publics, endpoints (objectif, paramètres, codes).
- Résumer les invariants métiers en tête de service (FR).

Tests
- `dotnet test` passe ; au moins 1 test par couche modifiée.
/compact

========================================================
SECTION C — Front Angular 20 : ESLint, Prettier, TS strict, patterns
========================================================
Tâches
1) ESLint + Angular ESLint + Prettier
- Installer `@angular-eslint/*`, `eslint`, `eslint-config-prettier`, `eslint-plugin-import`, `eslint-plugin-rxjs`, `eslint-plugin-jsdoc`.
- .eslintrc.json : règles strictes utiles : no-unused-vars (ts), no-implicit-any, eqeqeq, import/order, rxjs/no-ignored-subscription, jsdoc/check-tag-names.
- Prettier : .prettierrc (printWidth 100, tabWidth 2, semi true, singleQuote true, trailingComma all).

2) TypeScript strict
- tsconfig : `"strict": true`, `"noImplicitAny": true`, `"noUncheckedIndexedAccess": true`, `"exactOptionalPropertyTypes": true`, `"noFallthroughCasesInSwitch": true`.

3) Patterns Angular
- `ChangeDetectionStrategy.OnPush` sur listes, tables, pages volumineuses.
- `trackBy` systématique sur *ngFor.
- `takeUntil` + `Subject` destroy pour subscriptions ; `AsyncPipe` de préférence.
- Intercepteur d’erreurs Http : toasts + mapping ProblemDetails.
- Null-safety templates : `?.` + fallbacks, safe pipes pour dates/nums.

4) Structure & factorisation
- Dossier `shared/` : `components`, `pipes`, `directives` (ex: autofocus, skeleton), `models` (VM types), `utils` (guards).
- ApiService : méthodes typées, `HttpParams` centralisés, headers `X-Tenant-Id` injectés.
- KeyboardShortcutsService : ignore inputs & contenteditable, normalise `.key?.toLowerCase()`.

5) Commentaires FR
- JSDoc succinct en FR pour services publics et composants pages (but, inputs/outputs, effets de bord).

Tests
- `npm run ui:test` OK ; ajouter 2–3 tests ciblés (null-safety rendering, interceptors).
/compact

========================================================
SECTION D — Marketing (Astro) : ESLint, Prettier, a11y, budgets perf
========================================================
Tâches
1) ESLint + Prettier sur Astro
- Config eslint astro (`eslint-plugin-astro`) + `eslint-config-prettier`.
- Règles a11y : `jsx-a11y` (adaptées Astro), alt obligatoire, landmark roles.

2) Budgets perf Lighthouse
- Ajoute `lighthouserc.json` avec budgets (total-byte-weight, LCP, CLS).
- Script `npm run ci:qa` → pa11y-ci + lighthouse-ci.

3) Commentaires FR
- En-tête de chaque page : but de la section, ancre d’UX (FR).

Tests
- `npm run test:a11y` et `npm run test:lighthouse` ≥ 95 passent.
/compact

========================================================
SECTION E — Nettoyage automatique & code mort
========================================================
Tâches
1) .NET : `dotnet format` (whitespace/usings), suppression usings morts, fichiers .cs non référencés.
2) TS : `ts-prune` + `depcheck` (scripts npm) pour repérer imports morts et dépendances inutilisées ; supprime et commit.
3) Recherches TODO/FIXME : ouvrir issues GitHub si garde, sinon supprimer.
4) Logs dev trop verbeux : derrière `if (IsDevelopment)` ou niveau Debug.

ACCEPTANCE
- `dotnet format` ne modifie plus rien ; `depcheck` “unused” vide (ou justifié).
/compact

========================================================
SECTION F — Tests & seuils minimaux
========================================================
Tâches
1) API : cibles de base
- xUnit + coverlet ; seuil global statements ≥ 60% (initial).
- Tests “happy-path” pour endpoints sensibles (lists, preview, webhooks mapper).

2) Front : Jest
- Seuils : lines ≥ 60%.
- Tests pour ApiService error mapping, templates avec champs manquants, shortcuts guard.

3) E2E (option)
- Cypress : un smoke “Hunter → créer liste → créer cadence” + “Relance preview → validate”.

ACCEPTANCE
- Rapport couverture généré, seuils passés en CI.
/compact

========================================================
SECTION G — Hooks Git & Conventions de commit
========================================================
Tâches
1) Husky + lint-staged
- Pre-commit : `eslint --fix` + `prettier --write` (front/marketing), `dotnet format` (API), `tsc -p` typecheck.
- Pre-push : `dotnet test`, `npm run ui:test`, (option) `npm run test:a11y` si changements marketing.

2) commitlint
- Conventional Commits (feat, fix, chore, docs, refactor, test, ci, perf).
- Ajoute un fichier `commitlint.config.js`.

ACCEPTANCE
- Impossible de pousser si tests cassés ou lint KO.
/compact

========================================================
SECTION H — CI GitHub Actions (durcissement)
========================================================
Tâches
1) workflow `api-ci.yml`
- matrix .NET 8, `dotnet restore/build/test`, publie rapport couverture (artifacts).
2) workflow `front-ci.yml`
- Node LTS, `npm ci`, `npm run ui:check`, `npm run ui:test`, `npm run build`.
3) workflow `marketing-ci.yml` (existant)
- Garantir `npm run build`, `npm run test:a11y`, `npm run test:lighthouse`.
4) Badges README (build status, coverage approximative).

ACCEPTANCE
- Les 3 pipelines verts sur une branche de test.
/compact

========================================================
SECTION I — Observabilité & résilience (minima)
========================================================
Tâches
1) OpenTelemetry (API) : tracer Outbox.Send, Webhooks.Handle, Followups.Preview/Validate ; exporter OTLP optionnel via env (désactivé par défaut).
2) Serilog enrichisseurs : correlationId middleware, userId/tenantId, provider/messageId.
3) Polly (API) : transient HTTP (Resend/Meta/Twilio) avec retry/backoff + circuit-breaker doux.
4) Health & metrics : `/health` + `/health/metrics` (queueDepth, avgSendLatencyMs, deadWebhookBacklog) documentés.

ACCEPTANCE
- Traces/logs structurés visibles en dev ; métriques OK.
/compact

========================================================
SECTION J — Documentation FR & CONTRATS
========================================================
Tâches
1) `CONTRATS.md` (racine) : lister endpoints clés (paths, verbes, payloads), DTO minimaux, codes d’erreur ProblemDetails.
2) `QUALITY.md` : résumer linters, formatters, seuils de tests, comment corriger les échecs CI, scripts utiles.
3) Commentaires FR ajoutés sur classes/services publics (résumé, invariants, exceptions).

ACCEPTANCE
- `CONTRATS.md` et `QUALITY.md` présents, clairs et à jour.
/compact

========================================================
SECTION K — Récapitulatif final & scripts
========================================================
Tâches
1) Ajoute scripts racine (package.json ou scripts/*.sh|ps1):
   - `quality:all` → format + lint + test (API/Front/Marketing)
   - `smoke:api` → curl endpoints clé + vérif codes
   - `smoke:front` → démarrage et grep absence d’erreurs connues
   - `ci:local` → exécuter localement l’équivalent CI
2) Affiche un RÉCAP FINAL :
   - Ce qui a été nettoyé/factorisé,
   - Linters/formatters activés,
   - Nullables/TS strict activés,
   - Hooks Git et CI en place,
   - Où lire la doc (CONTRATS.md, QUALITY.md), comment corriger un échec CI.

ACCEPTANCE
- `npm run quality:all` (ou scripts équivalents) passe sans erreur.
- README mis à jour avec la section “Qualité & CI”.
/compact
