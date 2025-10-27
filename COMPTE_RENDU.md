Tu travailles dans pipelane-front (Angular 20 standalone + Angular Material + ng-apexcharts).
Objectif: surélever l’ergonomie, rendre l’app futuriste, simple et agréable, avec interactions, tutoriel mis à jour, accessibilité et performances au vert.

RÈGLES GÉNÉRALES
- Procède SECTION par SECTION. Pour chaque section:
  1) ouvre les fichiers concernés, 2) résume les tâches, 3) exécute-les par petits commits explicites,
  4) vérifie build/tests (npm run build, npm run ui:test), 5) exécute /compact.
- Ne casse pas les features existantes. Utilise Angular Material (MatToolbar, MatSidenav, MatButton, MatMenu, MatTooltip, MatDialog, MatTabs, MatSnackBar, MatStepper).
- Respecte dark-only; contraste AA; responsive; performances (lazy, CDK Virtual Scroll pour longues listes).

========================================================
SECTION A — Design System & Theme (dark-only futuriste)
========================================================
Tâches
1) Tokens SCSS: enrichir `src/theme/_tokens.scss`:
   - Couleurs: bg:#0b0f17, surface:#101726, surface-strong:#0e1524, primary:#75F0FF, secondary:#9B8CFF, accent:#60F7A3, text:#E6EAF2, text-muted:#A6B0C3, success:#60F7A3, warn:#F59E0B, error:#F87171.
   - Effets: glass (blur(12px) + border 1px rgba(255,255,255,.08)), neon ring focus (outline offset).
   - Gradients: `--grad-main: linear-gradient(135deg,#75F0FF 0%,#9B8CFF 45%,#60F7A3 100%)`.
   - Radii (12/16/24), spacers (4/8/12/16/24/32), elevations (soft shadows).
2) Material Theming: définir palette dark + typography (Inter/Outfit) + density comfortable.
3) Utilitaires SCSS:
   - .glass, .scrim (overlay pour contraste sur images/gradients),
   - .on-surface, .on-surface-strong, .chip, .badge (score/status/provider),
   - Animations CSS: .fade-up, .scale-in, .shimmer (skeleton).

Acceptance
- Build OK, variables accessibles dans composants, story-exemple dans un playground (demo-page vite-fait).
/compact

========================================================
SECTION B — App Shell & Navigation (simplicité + repères)
========================================================
Tâches
1) App Shell:
   - Top App Bar “glass” avec: logo, champ recherche globale (Ctrl+K), actions rapides (Créer campagne, Importer, Lancer démo), avatar menu (Profil, Aide, Rejouer tutoriel).
   - Left rail (sidenav) large sur desktop, icon-only en compact; items: Hunter, Cadences, Inbox, Contacts, Analytics, Settings.
   - Breadcrumb sous la barre pour pages profondes.
2) États responsives:
   - Mobile: sidenav over + bottom action bar contextuelle pour listes (selection).
3) Micro-interactions:
   - Hover underline animée, press states, transitions 150–200ms, disable ripple agressif.

Acceptance
- Navigation cohérente desktop/mobile; shortcuts Ctrl+K ouvre la recherche.
/compact

========================================================
SECTION C — Patterns UI transverses
========================================================
Tâches
1) Tooltips intelligents (MatTooltip) partout où c’est utile: boutons principaux, colonnes obscures, icônes statut.
2) États vides utiles:
   - Hunter vide: illustration + “Commencer une recherche” + lien doc; bouton Importer CSV.
   - Inbox vide: “Aucun message encore — lancez une cadence”.
3) Skeletons:
   - kpi-card, chart-card, tables (cdk-virtual-scroll). Ajouter shimmer.
4) Erreurs compréhensibles:
   - Toast + “Réessayer / Voir détails” (MatDialog pour stack).
5) Raccourcis clavier:
   - Ctrl+K (recherche), g+h (Hunter), g+a (Analytics), n+c (Nouvelle cadence), ? (ouvrir “Raccourcis”).
6) Accessibilité:
   - Focus visible (neon ring); roles ARIA pour nav et tables; labels sur inputs; support tab/esc dans modals; `prefers-reduced-motion`.

Acceptance
- Lint a11y OK (si présent), parcours uniquement clavier possible pour actions clés.
/compact

========================================================
SECTION D — Pages clés (ergonomie & beauté)
========================================================
D1) Hunter (/hunter)
- Layout split: Panneau critères sticky (gauche) + Carte+Liste (droite).
- Critères: objectifs chips (Sites web/Restaurants/Plomberie/Formation), adresse+rayon, filtres (rating, avis, site, booking, social actif, “problèmes site”).
- ResultList: table virtualisée; colonnes: Company, City, ★rating(reviews), Site ✓/✗, Booking ✓/✗, IG ✓/✗, Score (badge dégradé).
- Drawer “Fiche prospect”: coordonnées, tags features, **Why this lead?** (3 bullets), **Heatmap heures** mini (ng-apexcharts bar simple 10–12/14–16).
- Barre d’action inférieure (glass): “Magic Pick” (selection auto équilibrée), “Créer une liste”, “Ajouter à une liste”, “Créer une cadence →”.
- Tooltips clairs (“Score = priorité 0–100”, “Magic Pick = sélection auto diversifiée”).

D2) Cadence Builder (/campaigns/new)
- Stepper 4 étapes: Cibles → Canaux → Séquence → Review.
- UI simple: preview messages (cards), fenêtres horaires, caps/jour, variantes A/B minimalistes.
- Résumé final compact avec warning compliance (unsubscribe/STOP/WA24h).

D3) Inbox (/inbox)
- Thread à bulles verre, badges provider & statut, composer simplifié (texte / template).
- Side panel contact: tags, score, dernières actions.
- Boutons rapides: Proposer 2 créneaux, Classer la réponse (IA), Relance intelligente.

D4) Analytics (/analytics)
- KPI strip + area (series jour), donut (par canal), bar (top sujets/templates), selectors de période; boutons Export (PDF).
- Interactions: hover tooltips clairs, légendes cliquables.

D5) Settings (/settings)
- Cartes canaux (Email/WA/SMS) avec chip Connected/Not connected + “Envoyer un test”.
- Section IA (plafond budget), Caps & Quiet Hours visuels (slider + preview).

Acceptance
- Navigation entre pages sans charges inutiles, no layout shift visible; mobile OK.
/compact

========================================================
SECTION E — Didacticiel & Aide (ngx-shepherd + Help Center)
========================================================
Tâches
1) Tutoriel (onboarding) remis à jour:
   - 6 étapes: 1) Connecter canaux 2) Écrire pitch 3) Hunter: lancer recherche 4) Créer liste & cadence 5) Générer un message IA & envoyer 6) Voir analytics & exporter PDF.
   - Bouton “Rejouer le tutoriel” dans Aide.
   - Stockage localStorage `pipelane_tour_done`.
2) Panneau “?” (Help Center):
   - Raccourcis clavier (liste), liens docs (marketing /prospection-ia), “Contacter le support”.
3) Micro-coach:
   - Avant envoi: bulle “Coach 30s” (MatTooltip large ou mini-dialog): “+ propose 2 créneaux précis”, “– 1 phrase”, “évite jargon”.

Acceptance
- Tutoriel fonctionne, accessible clavier; Help Center ouvert via “?” ou Shift+/.
/compact

========================================================
SECTION F — Performance & charge
========================================================
Tâches
1) Virtual scroll par défaut pour listes >100 items.
2) Lazy loading de modules lourds (charts/tour).
3) ChangeDetection OnPush sur pages liste & charts.
4) Mémoïsation sélecteurs (signals/rx) pour éviter rerenders.

Acceptance
- Lighthouse (front served) perf non régressée; scroll fluide gros volumes.
/compact

========================================================
SECTION G — Tests & Qualité
========================================================
Tâches
1) Jest
   - Tooltips présents sur actions clés (Hunter, Inbox).
   - Magic Pick: sélection équilibrée (répartition par city + top scores).
   - Tutoriel: flag localStorage; “Rejouer” démarre le tour.
   - Analytics: mappers vers apexcharts options corrects; export PDF bouton appelle API.
2) (Option) Cypress smoke
   - Hunter → recherche → Magic Pick → Créer liste → Créer cadence.
   - Inbox → Classer la réponse → Proposer 2 créneaux.
   - Analytics → changer période → Export PDF.

Acceptance
- npm run ui:test OK; (option) e2e vert; /compact final.
/compact
