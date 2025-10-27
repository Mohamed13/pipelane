Tu travailles dans un monorepo :
- pipelane-api (ASP.NET 8)
- pipelane-front (Angular 20 + Material)

Objectif: corriger les erreurs suivantes observées en console:
1) 500 sur GET https://localhost:56667/api/lists
2) 400 sur POST https://localhost:56667/api/followups/preview
3) Shepherd: "The element for this step was not found [data-tour="onboarding-email"]"
4) TypeError: Cannot read properties of undefined (reading 'toLowerCase') dans handleShortcut
5) TypeError: Cannot read properties of undefined (reading 'label') (plusieurs occurrences)
6) NG0203 & erreurs liées au cycle de vie (après view init)
7) Messages "chrome-extension://…" (charger un module d’extension) → à ignorer côté app

RÈGLES
- Pour chaque SECTION: ouvre les fichiers, résume ce que tu fais, corrige par petits commits clairs, vérifie `dotnet build/test` et `npm run build && npm run ui:test`, puis `/compact`.
- Ne change pas la forme des DTO côté front: en cas de doute, fais une compat ascendante (ajoute champs optionnels/overload API).
- Ajoute des logs clairs (Serilog) côté API pour 500/400.

========================================================
SECTION A — API: /api/lists (500) + logs + garde-fous
========================================================
Fichiers: pipelane-api/Pipelane.Api/Controllers/ListsController.cs, Services/ListsService*, Infrastructure/DbContext*, Program.cs (logging)

Tâches:
1) Reproduire la 500 localement (appel direct GET /api/lists). Ajouter logs:
   - log tenantId, userId, sql provider, total lists trouvées, event d’erreur (exception + stack).
2) Garde-fous:
   - Si tenant inexistant ou non résolu → 400 ProblemDetails "Tenant header (X-Tenant-Id) manquant ou invalide".
   - Si DbContext migration manquante → retourner 500 avec code d’erreur "DB_MIGRATION_PENDING" et log explicite.
3) Retour API:
   - Toujours renvoyer 200 avec une liste vide si aucune liste (pas de null).
   - Map DTO côté contrôleur (évite les proxy EF).
4) Health:
   - GET /health: inclure un check "db" ; si KO, message clair dans logs.

Tests xUnit rapides:
- Lists_returns_empty_array_when_none
- Lists_returns_400_when_missing_tenant

Build+test OK → /compact.

========================================================
SECTION B — API: /api/followups/preview (400) compat GET/POST
========================================================
Fichiers: Pipelane.Api/Controllers/FollowupsController.cs, FollowupProposalStore*, AiController/FollowupService*

Tâches:
1) Compatibilité:
   - Supporter **GET** `/api/followups/preview?conversationId=GUID`
   - Supporter **POST** `/api/followups/preview` avec body `{ conversationId: "GUID" }`
   - Si manquant → 400 ProblemDetails avec détail `"conversationId required"`.
2) Validation:
   - Vérifier appartenance tenant, existence conversation, droits.
3) Logs:
   - Loggger contexte: conversationId, hasHistory, computedScheduledAt (UTC & local).

Tests xUnit:
- Preview_get_and_post_compat
- Preview_400_on_missing_conversationId

Build+test → /compact.

========================================================
SECTION C — FRONT: ApiService: endpoints, erreurs, toasts
========================================================
Fichiers: pipelane-front/src/app/core/api.service.ts, error-interceptor, toasts/ui-feedback

Tâches:
1) Corriger appel preview:
   - Utiliser **GET** `/api/followups/preview?conversationId=` si conversationId dispo.
   - Sinon POST avec body {conversationId}.
   - Sur 400: afficher toast "Sélectionnez une conversation pour prévisualiser la relance."
2) Corriger listes:
   - `getLists()`: gérer 200 [] (liste vide) et 400 tenant manquant → toast "Sélectionnez un espace de travail / reconnectez-vous."
3) Logger côté front:
   - `[API]` prefix dans console uniquement en dev; ne spammez pas (1 console.warn par type d’erreur).

Tests Jest:
- ApiService.preview GET/POST fallback
- ApiService.getLists returns []

/compact.

========================================================
SECTION D — FRONT: Shepherd tour — skips si sélecteur absent
========================================================
Fichiers: src/app/core/tour.service.ts (ou équivalent), composants d’onboarding concernés

Tâches:
1) Ajouter un helper `elementReady(selector: string, timeout=3000)` qui résout si l’élément apparaît (MutationObserver + fallback setTimeout), sinon rejette.
2) Avant d’attacher une étape Shepherd sur `attachTo.el`, vérifier:
   - Si l’élément n’existe pas dans le délai, **sauter cette étape** proprement (next step) au lieu de planter (log courte).
3) Empêcher lancement auto du tour si la page courante ne contient aucun des sélecteurs requis:
   - Ex: `[data-tour="onboarding-email"]` non présent → démarre à l’étape suivante ou ne lance pas le tour.

Tests Jest:
- TourService skips missing selector without throw

/compact.

========================================================
SECTION E — FRONT: Raccourcis clavier — null safety & filtres
========================================================
Fichiers: src/app/core/shortcuts.service.ts (ou où se trouve handleShortcut)

Tâches:
1) `handleShortcut(evt: KeyboardEvent)`:
   - Guard: `if (!evt || !evt.key) return;`
   - Normalisation: `const key = (evt.key || '').toString().toLowerCase();`
   - Ignorer si cible est `input, textarea, [contenteditable="true"]` (ne pas écouter dans les champs).
   - Mappe seulement les combinaisons déclarées (Ctrl+K, g+h, g+a, n+c, ?).
2) Ajouter tests Jest:
- returns early when evt.key undefined
- ignores when target is input/textarea

/compact.

========================================================
SECTION F — FRONT: "Cannot read 'label'" — mappers & safe render
========================================================
Fichiers: composants où l’erreur remonte (stack `221.js` → identifie le composant: liste, menu, tabs…)

Tâches:
1) Repérer tous les `.label` utilisés dans le template/TS.
2) Dans mappers (depuis API → ViewModel), **garantir** un fallback:
   - `label = api.label ?? api.name ?? api.title ?? '(sans libellé)'`
   - Pour les options/tabs/menus: contrôler que le tableau n’est pas `null`/`undefined` (`?? []`).
3) Dans les templates:
   - Utiliser l’opérateur `?.` et fallback `|| '(—)'`.
   - Ajouter `trackBy` sur *ngFor pour réduire re-renders.
4) Ajout test Jest par composant:
   - Rendu avec élément dont `label` est absent → **pas d’exception** et fallback affiché.

Commit: `fix(front): null-safety on label fields and array options`  
/compact.

========================================================
SECTION G — FRONT: NG0203 & AfterViewInit robustesse
========================================================
Fichiers: composants mentionnés dans les traces (ex: `loadRange`/`ngAfterViewInit`)

Tâches:
1) Si le composant lit `ViewChild`/`Mat*` dans `ngAfterViewInit`, **vérifier la présence** avant usage:
   - `if (!this.table) { return; }`
2) Déplacer la première exécution lourde asynchrone hors du cycle (`setTimeout(0)` ou `ngZone.runOutsideAngular` + `markForCheck`).
3) Ajouter guards sur inputs @Input optionnels (initialiser valeurs par défaut).
4) Utiliser `ChangeDetectionStrategy.OnPush` si le composant liste est volumineux, avec `cdr.markForCheck()` après maj async.

Tests: smoke rendering du composant avec inputs minimaux.  
/compact.

========================================================
SECTION H — Ignorer bruits "chrome-extension://…" proprement
========================================================
Fichiers: aucun correctif applicatif requis.

Tâches:
- Ne rien faire côté app (ces erreurs viennent d’extensions Chrome injectées).
- Option: filtrer ces logs dans `environment.ts` en dev: wrapper console.error pour ignorer messages qui contiennent "chrome-extension://".

Commit: `chore(dev): filter noisy extension logs in dev`  
/compact.

========================================================
SECTION I — Vérifications finales & scripts smoke
========================================================
Tâches:
1) `dotnet build && dotnet test`
2) `npm run build && npm run ui:test`
3) Ajouter 3 scripts smoke (PowerShell/Bash) dans /scripts:
   - `smoke-lists`: curl GET /api/lists avec et sans X-Tenant-Id → attendre 200 [] ou 400 ProblemDetails.
   - `smoke-preview`: curl GET /api/followups/preview?conversationId=<seeded> → 200 ; POST body → 200 ; sans id → 400 clair.
   - `smoke-front`: ouvre l’app, vérifie qu’aucune erreur `label`/`toLowerCase` n’apparaît dans 10s (simple grep sur logs dev si possible).

4) Afficher un récapitulatif dans la sortie:
   - Erreurs corrigées, fichiers modifiés, endpoints compatibles, how-to reproduce OK.
   
/compact
