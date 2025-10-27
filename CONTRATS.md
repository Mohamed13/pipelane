# Contrats techniques principaux

Ce document récapitule les principales API exposées par l'instance Pipelane ainsi que les comportements attendus. Toutes les routes exigent l'en-tête `X-Tenant-Id` sauf mention contraire.

## Authentification
- `POST /api/auth/login` – authentifie un opérateur et retourne un JWT. Corps : `{ email, password }`. Codes : `200`, `401`.
- `GET /api/auth/me` – retourne le profil courant (JWT obligatoire). Codes : `200`, `401`.

## Hunter & listes
- `POST /api/hunter/upload-csv` – multipart form (`file`). Réponse `{ csvId }`. Si `?dryRun=true`, n’enregistre pas les prospects. Codes : `200`, `400`.
- `POST /api/hunter/search` – corps `HunterSearchCriteria` (industry, geo.lat/lng/radiusKm, filters...). Réponse `HunterSearchResponse { total, duplicates, items[] }`. Codes : `200`.
- `POST /api/hunter/seed-demo` – nécessite `DEMO_MODE=true`. Injecte 50 prospects démo. Codes : `200`.
- `POST /api/lists` – crée une liste `{ name }` → `{ id }`. Codes : `200`, `400`.
- `GET /api/lists` – renvoie les listes du tenant. Codes : `200`.
- `GET /api/lists/{id}` – détail de liste (prospects, score, why). Codes : `200`, `404`.
- `POST /api/lists/{id}/add` – `{ prospectIds: Guid[] }` → `{ added, skipped }`. Codes : `200`, `404`.
- `POST /api/cadences/from-list` – crée une cadence depuis une liste `{ listId, name?, dailyCap?, window?, steps? }` → `{ cadenceId }`. Respecte les caps/quiet hours. Codes : `200`, `404`.

## Suivi & relance
- `GET /api/followups/preview?conversationId=` – prévisualise une relance (retourne Why + proposition). Codes : `200`, `404`.
- `POST /api/followups/preview` – soit `conversationId`, soit `segmentJson` pour compter les contacts ciblés. Codes : `200`, `400`, `404`.
- `POST /api/followups/validate` – valide une proposition `{ conversationId, proposalId, sendNow? }` → enfile dans l’outbox. Codes : `200`, `400`, `404`.

## Webhooks e-mail
- `POST /api/webhooks/email/resend` – payload Resend signé. Valide la signature puis mappe vers `MessageEvent`. Réponses `200`, `400`, `401` (signature invalide). Idempotent via couple provider/event.

## Santé & observabilité
- `GET /health` – health-check JSON (statut global + checks). Ouvert (pas de JWT requis).
- `GET /health/metrics` – métriques agrégées `{ queueDepth, avgSendLatencyMs, deadWebhookBacklog, timestamp }`. Ouvert.
- Export OTLP activable via `OTEL_EXPORTER_OTLP_ENDPOINT` (sinon console exporter par défaut). Activités suivies : outbox send, webhooks Resend, preview/validate follow-up.

## Contraintes générales
- Quiet hours + caps journaliers appliqués lors de `Validate` (24h glissantes).
- Tous les endpoints retournent `ProblemDetails` FR en cas d’erreur (cf. middleware `ExceptionHandlingMiddleware`). Les erreurs incluent `traceId` et `correlationId` si disponibles.
