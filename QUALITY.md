# Qualité et automatisation

## Linters & formatters
- **.NET** : `dotnet format Pipelane.sln` (lancé par Husky + lint-staged, vérification `--verify-no-changes` en pré-commit).
- **Angular** : `npm run lint` + `prettier --write` (déclenchés sur les fichiers modifiés via lint-staged). TypeScript strict (`strict`, `noImplicitAny`, `noUncheckedIndexedAccess`, `noFallthroughCasesInSwitch`).
- **Astro** : `npm run lint` (`eslint-plugin-astro` + a11y) + `npm run format` (Prettier) via lint-staged.
- **Détections code mort** : `npx ts-prune`, `npx depcheck` côté front (scripts manuels) ; `dotnet format` côté API.

## Tests & seuils de couverture
- **Backend** : `dotnet test Pipelane.sln` (coverlet intégré). Seuil minimal actuel : 15 % lignes (à augmenter via nouvelles specs). Rapport Cobertura/LCOV disponible dans `pipelane-api/tests/coverage/` et exporté en CI.
- **Frontend Angular** : `npm run ui:test` (Jest). Seuil global : 25 % lignes & statements. Couverture exportée dans `pipelane-front/coverage/` et jointe en CI.
- **Marketing** : `npm run build`, `npm run test:a11y`, `npm run test:lighthouse` (budgets performance, LCP ≤ 2 500 ms, CLS ≤ 0.1). CI = `marketing-ci`.
- **E2E** : Cypress dispo via `npm run ui:e2e` (non bloquant dans la CI actuelle).

## Hooks Git (Husky)
- `pre-commit` : `lint-staged` (format + lint ciblé), `dotnet format --verify-no-changes`, `npx tsc --noEmit` (Angular). Tout échec bloque le commit.
- `commit-msg` : `@commitlint/cli` (Conventional Commits).
- `pre-push` : `dotnet test`, `npm run ui:test` (Angular), `npm run test:a11y` (marketing).

## Intégration continue
- `api-ci.yml` : restore/build/test (.NET 8), artefact couverture (`api-coverage`).
- `front-ci.yml` : `npm ci`, lint (`ui:check`), tests (`ui:test`), build, artefact Jest couverture (`front-coverage`). Node 20, cache npm.
- `marketing-ci.yml` : `npm ci`, build, pa11y, Lighthouse (≥0.95). 
- Les badges CI sont disponibles dans le README (`API CI`, `Front CI`, `Marketing CI`).

## Résolution d’échecs fréquents
- **Lint .NET** : lancer `dotnet format Pipelane.sln`. Pour ignorer temporairement une règle StyleCop, documenter dans `Directory.Build.props` (`NoWarn`). 
- **Lint Angular/Astro** : exécuter `npm run lint` puis corriger les erreurs. Prettier se charge du formatage (`npm run format` si besoin).
- **Tests backend** : `dotnet test` (vérifie aussi la couverture). Poubelle `pipelane-api/tests/coverage/` si besoin après inspection.
- **Tests front** : `npm run ui:test`. Penser à supprimer `src/app/core/env.generated.ts` après exécution (généré par `inject-env`).
- **Tests marketing** : `npm run test:a11y`, `npm run test:lighthouse`. Vérifier que le port 4321 est libre.

## Observabilité & résilience
- Traces OTel publiées sur `Pipelane.Messaging`, `Pipelane.Webhooks`, `Pipelane.Followups`. Export OTLP activable via `OTEL_EXPORTER_OTLP_ENDPOINT`. Console exporter actif par défaut.
- Clients HTTP (`Resend`, `OpenAI`, `Automations`) protégés par `WaitAndRetry` exponentiel et `CircuitBreaker` (Polly).
- Endpoints santé : `GET /health` (JSON complet) + `GET /health/metrics` (queue outbox, latence moyenne, backlog webhooks).
