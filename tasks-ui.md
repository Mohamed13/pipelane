Tu travailles dans pipelane-front (Angular 20 standalone + Angular Material + ng-apexcharts).
Objectif: corriger marges/paddings/occupation d’espace, améliorer la lisibilité et la praticité, harmoniser la densité et moderniser le visuel (dark-only) — sans casser la logique existante.

RÈGLES
- Procède SECTION par SECTION. Pour chaque section:
  1) ouvre les fichiers concernés, 2) résume les tâches, 3) fais des commits courts et explicites,
  4) vérifie `npm run build` + `npm run ui:test`, 5) exécute `/compact`.
- Ne modifie pas les contrats d’API. Respecte a11y (focus visible), performances (OnPush, virtual scroll), dark-only.
- Cible surtout: pages **Hunter**, **Cadences** (builder), **Inbox**, **Analytics**, **Shell**.

========================================================
SECTION A — Système d’espacement & thèmes (tokens + utilities)
========================================================
Fichiers: `src/theme/_tokens.scss`, `src/styles.scss` (ou global styles).

Tâches:
1) Crée un **système d’espacement** cohérent (8-pt):
   $space-0: 0; $space-1: 4px; $space-2: 8px; $space-3: 12px; $space-4: 16px; $space-5: 20px; $space-6: 24px; $space-7: 32px; $space-8: 40px; $space-9: 56px;
   :root { --gap-xs: #{$space-2}; --gap-sm:#{$space-3}; --gap:#{$space-4}; --gap-lg:#{$space-6}; --section-v:#{$space-9}; }

2) Ajoute **utilitaires**:
   .container-page { max-width: 1360px; margin-inline:auto; padding-inline: clamp(16px, 3vw, 32px); }
   .section   { padding-block: var(--section-v); }
   .glass     { background: rgba(255,255,255,.06); border: 1px solid rgba(255,255,255,.08); backdrop-filter: blur(10px); border-radius: 16px; }
   .chip      { padding: 4px 8px; border-radius: 999px; background: rgba(255,255,255,.08); }
   .sr        { position:absolute; width:1px; height:1px; overflow:hidden; clip:rect(0 0 0 0); }

3) Typo responsive (clamp) + titres:
   h1{ font-size:clamp(24px,3.6vw,36px); line-height:1.15; letter-spacing:-.012em}
   h2{ font-size:clamp(20px,2.6vw,28px); line-height:1.2;}
   .text-muted{color:rgba(230,234,242,.64)} .on-surface{color:rgba(230,234,242,.92)} .on-surface-strong{color:#fff}

4) Safe areas & scroll-margin:
   [id]{ scroll-margin-top: 96px; }
   :root{ --z-appbar: 40; --z-dialog: 60; }

Acceptance: build OK; utilitaires disponibles.
 /compact

========================================================
SECTION B — App Shell (Topbar, Sidenav, Header de page)
========================================================
Fichiers: `src/app/app.component.*`, layout shell components.

Tâches:
1) **Top bar** compacte: hauteur 56px desktop, 64px mobile; actions à droite (Search Ctrl+K, Dark, Lang, Help).
   - `mat-toolbar` avec classes `.glass sticky top-0 z-[var(--z-appbar)]` (via styles globaux).
   - Champ recherche en width clamp (min 240px / max 420px).

2) **Sidenav**:
   - Largeur 240px desktop; 72px en “rail” compact.
   - Groupes “Hunter / Cadences / Inbox / Contacts / Analytics / Settings”.
   - Items: icon + label; padding vertical 8px; focus visible.

3) **Header de page** standard:
   - Titre (h1) + sous-titre (option) + action chips (Send test, Create campaign, Import, Docs).
   - Marge sous-header: var(--gap-lg).
   - Ajoute un composant `PageHeaderComponent` réutilisable (inputs: title, subtitle, actions[]).

Acceptance: aucune superposition, header constant sur toutes pages.
 /compact

========================================================
SECTION C — Hunter: grilles, paddings, panneau critères, carte, liste
========================================================
Fichiers: `src/app/features/hunter/*` (page + composants).

Tâches:
1) **Grille principale**:
   - Wrapper `.container-page section`.
   - `grid-template-columns`: 360px (panneau) + 1fr (carte+liste) (>=1280px); 1col stacked en <1280px.
   - Gap: var(--gap-lg).

2) **Panneau critères** (à gauche):
   - Carte `.glass p-5` (mobile p-4); titres `h2` compacts; chips persona en `display:flex; flex-wrap:wrap; gap:8px`.
   - Inputs en 2 colonnes si largeur >= 480px; spacing vertical 12–16px.

3) **Carte des prospects**:
   - Hauteur fixe responsive: `min-height: 240px; height: clamp(220px, 32vh, 320px);`
   - Placeholder gradient + label centré. Bouton “Magic Pick” en `position:absolute; top:12px; right:12px;`

4) **Liste des résultats**:
   - Utilise `cdk-virtual-scroll-viewport` avec itemHeight calculé (56–64px).
   - Colonnes: PROSPECT | VILLE | NOTES | SITE | BOOKING | SCORE | ACTIONS.
   - Densité `mat-density(-2)`; cellules avec `padding-inline:12px`.
   - Ligne d’état vide (illustration + lien import CSV) centrée verticalement.

5) **Barre d’action inférieure** (sélection):
   - Collée en bas du viewport, `.glass` + `backdrop-filter`; contenu: “N sélectionnés” + boutons “Créer liste”, “Ajouter à liste”, “Créer cadence →”.
   - Hauteur 56px; padding 8–12px; z-index > appbar si besoin.

Acceptance: aucune zone “vide” énorme, défilement fluide liste, panneau critères lisible.
 /compact

========================================================
SECTION D — Cadences (Builder): densité, grilles, steps
========================================================
Fichiers: `src/app/features/campaigns/*`

Tâches:
1) **Stepper**:
   - Barre de progression visible; steps espacés (gap var(--gap-lg)); titres h2.
   - Cards `.glass p-5` pour “Audience / Message / Schedule & throttle / Review”.

2) **Segment builder**:
   - MatChips pour tags + “Preferred channels” alignés; toggles en lignes; dernier bloc “Respect consent” décalé à droite (row end) en >= 1024px.
   - **Preview JSON**: panel à droite (min-width 320px) sticky sur viewport (position: sticky; top: 88px).

3) **Footer wizard**:
   - Barre action fixed bas (comme Hunter): “Back / Next” + “Save draft”.
   - Empêcher “bande vide” sous le footer (padding-bottom sur .page équivalent à hauteur footer).

Acceptance: aucune alerte chevauchée, preview toujours visible en desktop, navigation claire.
 /compact

========================================================
SECTION E — Inbox: cadrage, filtres, empty state
========================================================
Fichiers: `src/app/features/inbox/*`

Tâches:
1) **Container**: `.container-page section`.
2) **Panneau principal**:
   - Largeur max 980px centré; `.glass p-5`; tabs filtres (All/Interested/Meetings/Unsub/No).
   - Empty state: illustration légère + texte `text-muted` + CTA “Create campaign”.

3) **Thread futur** (si présent plus tard): réserver `min-height: 40vh`.

Acceptance: la zone centrale ne paraît pas “perdue”, filtres lisibles.
 /compact

========================================================
SECTION F — Analytics: KPIs, graphs, export
========================================================
Fichiers: `src/app/features/analytics/*`

Tâches:
1) **KPI strip**:
   - Grille 2/3/5 col selon breakpoints; cartes `.glass p-4` densité -2.
   - Numbers avec `font-variant-numeric: tabular-nums`.

2) **Charts**:
   - Area (series), Donut (channel), Bar (top template/subject) dans une grille `grid gap var(--gap-lg)`; mêmes marges verticales.
   - Légendes cliquables; tooltips clairs.

3) **Export**:
   - Bouton “Exporter PDF” dans header; snackbar résultat + loader.

Acceptance: charts alignés, pas de scroll horizontal, export OK.
 /compact

========================================================
SECTION G — États transverses (toasts, modals, focus, raccourcis)
========================================================
Fichiers: services UI + styles globaux.

Tâches:
1) **Toasts**: position bottom-right par défaut, margin-bottom égale à la barre d’action si visible (détection via CSS variable).
2) **Dialogs**: largeur clamp (min 360 / max 720), paddings réguliers.
3) **Focus**: ajoute une classe `.focus-ring` (outline 2px #75F0FF/60) sur `:focus-visible`; applique sur boutons/lien/inputs; désactive outline:none hérités.
4) **Raccourcis**: Ctrl+K (recherche), g+h (Hunter), g+a (Analytics), n+c (New cadence), ? (Help). Affiche un petit dialog “Raccourcis” accessible depuis le header.

Acceptance: navigation clavier agréable; pas de toast caché.
 /compact

========================================================
SECTION H — Performance & a11y
========================================================
Tâches:
1) Active `ChangeDetectionStrategy.OnPush` sur listes/containers volumineux (Hunter Results, Contacts).
2) Vérifie que toutes les listes > 100 items utilisent `cdk-virtual-scroll`.
3) Ajoute labels ARIA explicites (buttons d’action liste, Magic Pick) + `aria-live="polite"` pour compte sélection.
4) Vérifie Lighthouse local: éviter CLS (réserve hauteurs; images/frames ratio).

Tests:
- Jest: snapshots de composants clés; tests sur utilitaires (MagicPick diversifie par city + score).
- Lint a11y si présent.

 /compact

========================================================
SECTION I — Theme density & overrides Material
========================================================
Fichiers: `src/theme/material-theme.scss` (ou theme builder).

Tâches:
1) Définit densité par défaut `-1`, avec exceptions `-2` pour tables, chips, toolbars secondaires.
2) Override paddings MatTable cell: `padding: 12px 12px;` (responsive 8px sur <600px).
3) Stepper: réduire `mat-step-header` height et ajouter `gap: var(--gap-sm)`.

Acceptance: densité homogène, pas d’éléments “trop espacés”.
 /compact

========================================================
SECTION J — Nettoyage & tests finaux
========================================================
Tâches:
1) Passe un coup de style: retire marges/paddings inline redondants dans composants (remplace par utils).
2) `npm run build`, `npm run ui:test` doivent passer.
3) Vérifie visuellement:
   - Hunter: panneau gauche lisible, carte hauteur correcte, liste dense, barre action visible.
   - Cadences: preview sticky, footer wizard visible, aucun overlap.
   - Inbox: bloc centré, filtres clairs.
   - Analytics: grilles nettes, export OK.

Commit final: 
`feat(front): layout polish — spacing system, page headers, grids (Hunter/Cadences/Inbox/Analytics), action footers, density, focus ring; smoother UX`

/compact
