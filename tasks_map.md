Tu travailles dans pipelane-front (Angular 20 + Material + ng-apexcharts).
But: remplacer le placeholder carte du module **Hunter** par une **carte Mapbox** réactive, avec marqueurs scorés, clustering et popup “Why this lead?”, synchronisée avec la liste. Ne JAMAIS hardcoder de token dans le code: utiliser l’env.

TOKEN FOURNI PAR L’UTILISATEUR (à mettre en env, pas en dur):
MAPBOX_TOKEN=pk.eyJ1IjoibW9oYW1tZWRiMTMiLCJhIjoiY21oOW41ZXJzMDAwazJrc2R1MXEzbWtqNCJ9.gKipo0Rd1y9tBI6WP9ht7Q

RÈGLES
- Commits atomiques, build+tests OK à chaque section, puis /compact.
- Dark-only; performance (virtual scroll, OnPush); null-safety.
- Fallback propre si token manquant: afficher un bandeau “Carte désactivée (token manquant)” et garder les résultats utilisables.

========================================================
SECTION A — Dépendances & styles
========================================================
Tâches:
1) Installer Mapbox GL JS:
   npm i mapbox-gl
   npm i --save-dev @types/mapbox-gl

2) Styles globaux (styles.scss ou angular.json globalStyles):
   @import 'mapbox-gl/dist/mapbox-gl.css';
   .mapboxgl-popup { color: #E6EAF2; } /* lisible sur dark */
   .mapboxgl-ctrl-logo, .mapboxgl-ctrl-attrib { filter: invert(0.85); opacity:.8; }

3) Token via env:
   - Ajouter `MAPBOX_TOKEN` dans `pipelane-front/.env.example` et `.env` (ne pas commit le réel).
   - Étendre le script `tools/inject-env.mjs` pour générer `env.generated.ts` avec `export const MAPBOX_TOKEN = process.env.MAPBOX_TOKEN ?? '';`

Vérifie `npm run build` → /compact.

========================================================
SECTION B — Service Map & utilitaires (OnPush-friendly)
========================================================
Fichiers: src/app/core/mapbox.service.ts, src/app/core/env.generated.ts

Tâches:
1) Créer `MapboxService`:
   - `init(container: HTMLElement, center:[lng,lat], zoom:number)` → renvoie l’instance `map: mapboxgl.Map`.
   - `addClusteredSource(map, sourceId:string, data:GeoJSON.FeatureCollection)` avec options cluster `{cluster:true, clusterRadius:40, clusterMaxZoom:14}`
   - `addLayersForClusters(map, sourceId)`:
       - cercles cluster (étendue par pointCount) et `symbol` pour `pointCount`.
       - cercles individuels colorés par **score** (0–100) → palette: rouge (≤40) / ambre (41–69) / vert (≥70).
   - `fitTo(data)` pour adapter les bounds.
   - `setToken(MAPBOX_TOKEN)` dans le constructeur (mapboxgl.accessToken = …).

2) Helpers:
   - `scoreToColor(score:number): string` → '#F87171' | '#F59E0B' | '#60F7A3'
   - `toGeoJSON(results: HunterResult[]): FeatureCollection` avec `properties:{id, company, city, score, why[]}` et `geometry:{type:'Point', coordinates:[lng,lat]}` (ignorer items sans coords)

/compact.

========================================================
SECTION C — HunterMapComponent (nouveau composant)
========================================================
Fichiers: src/app/features/hunter/hunter-map.component.{ts,html,scss}

Tâches:
1) Composant standalone, `ChangeDetectionStrategy.OnPush`.
2) Inputs:
   - `@Input() items: HunterResultVm[] = []` (doit contenir `lng`, `lat`, `score`, `why`, `company`, `city`, `id`)
   - `@Input() selectedId?: string`
3) DOM:
   - container `.glass` avec `#mapEl` (height: clamp(220px,32vh,320px); border-radius:16px; overflow:hidden)
   - bandeau si `MAPBOX_TOKEN === ''`: “Carte désactivée (token manquant)” avec un lien doc.
4) Init:
   - si token vide → ne pas initialiser la carte.
   - sinon `map = mapboxService.init(mapEl.nativeElement, defaultCenter, 11)`
   - `sourceId='hunter'` ; `data=toGeoJSON(items.filter(hasCoords))`; `addClusteredSource` + `addLayersForClusters`
   - `fitTo(data)` si >0 features.
5) Interactions:
   - **click cluster** → `map.easeTo` sur le cluster ou `map.zoomTo(map.getZoom()+2)`.
   - **click point** → popup custom: `company (score)` + 2–3 bullets de `why` + 2 boutons:
      - `[Sélectionner]` → émet `select.emit(feature.properties.id)`
      - `[Ajouter à la liste]` → émet `addToList.emit(id)`
6) Sync avec liste:
   - Input `selectedId` → si modifié, surligner le point (layer outline temporaire ou `setFeatureState({selected:true})`).
   - EventEmitter:
      - `@Output() select = new EventEmitter<string>();`
      - `@Output() addToList = new EventEmitter<string>();`

Tests unitaires minimaux: instanciation sans token; avec token + items → add source ok.
/compact.

========================================================
SECTION D — Brancher la carte à HunterPage
========================================================
Fichiers: src/app/features/hunter/hunter-page.component.{ts,html}

Tâches:
1) Mapper les résultats existants (`HunterResult` → `HunterResultVm`) en ajoutant `lng/lat`:
   - Si l’API renvoie déjà `geo` ou `lng/lat`: utiliser directement.
   - Sinon, tenter une **heuristique** en attendant le provider: ignorer sans coords (la liste reste fonctionnelle).
2) Injection du token:
   - `import { MAPBOX_TOKEN } from '@/app/core/env.generated';`
   - Transmettre via `*ngIf="MAPBOX_TOKEN; else MapDisabled"`
3) Disposition:
   - Grille 2 colonnes (panneau critères à gauche, carte + liste à droite).
   - Place la **carte au-dessus de la liste** avec marge `var(--gap-lg)`.
4) Événements:
   - `(select)` de la carte → sélectionner la ligne correspondante dans la table (scrollTo + focus).
   - `(addToList)` → ouvrir le dialogue de sélection de liste existant.

Tests Jest:
- Render sans token → affiche le bandeau désactivé.
- Avec token + 1 item coordonné → carte init et 1 feature présente.
/compact.

========================================================
SECTION E — Popups & accessibilité
========================================================
Tâches:
1) Popup HTML sobre, dark:
   - `<strong>{{company}}</strong> — <span class="chip">{{score}}/100</span>`
   - `<ul>` bullets 2–3 raisons `why`
   - 2 boutons `<button>` avec `aria-label` clairs (Sélectionner / Ajouter)
2) Focus trap: donner le focus au 1er bouton à l’ouverture (listener `popup.on('open', ...)`)
3) Mobile: désactiver double-tap zoom sur boutons (stopPropagation).

/compact.

========================================================
SECTION F — Clustering & perfs (beaucoup de points)
========================================================
Tâches:
1) Layers:
   - `cluster-circles` (couleur par `point_count` quantiles)
   - `cluster-count` (symbol `text-field: ['get','point_count_abbreviated']`)
   - `unclustered-points` (cercle `circle-color` par `score`)
2) Pour 2000+ points:
   - `clusterRadius: 60`, `clusterMaxZoom: 13`
   - Simplifier popup: charge “why” paresseuse depuis `items` si nécessaire.

Profilage rapide: vérifier que la navigation reste fluide.
/compact.

========================================================
SECTION G — Fallback & sécurité
========================================================
Tâches:
1) Si `MAPBOX_TOKEN === ''`:
   - bannière `.glass` “Carte désactivée (token manquant)” + lien vers doc “Paramétrer MAPBOX_TOKEN”.
   - ne pas tenter d’importer Mapbox.
2) Ne pas logguer le token dans la console; ne pas commit `.env` contenant la valeur réelle.

Commit: `feat(hunter): mapbox integration with token env, clustering, score markers, popups, list sync, safe fallback`  
/compact.

========================================================
SECTION H — Option (Astro marketing): bloc carte démo (si souhaité)
========================================================
(Optionnel — ignorer si hors scope)
- Si `PUBLIC_MAPBOX_TOKEN` existe, afficher une mini-carte sur /prospection-ia avec 6–10 points factices (couleurs par score), sinon fallback texte.

/compact.

========================================================
SECTION I — Vérifications & scripts
========================================================
Tâches:
1) `npm run build` + `npm run ui:test`
2) Test manuel:
   - Avec token: recherche Hunter → carte s’affiche, clusters, clic → popup, sélection ligne OK.
   - Sans token: bannière désactivée, liste utilisable.
3) Script README (Hunter):
   - Ajouter une section “Activer la carte Mapbox” (mettre MAPBOX_TOKEN dans `.env`, relancer `npm start`).

/compact
