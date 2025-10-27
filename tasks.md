Tu travailles dans pipelane-marketing (Astro + Tailwind, dark-only).
Objectif: corriger les bugs visuels (nav en “pilule”, titres qui dépassent, chevauchements, bannière cookies, contrastes) et stabiliser le responsive (desktop ↔ mobile) sans casser le design.

RÈGLES
- Procède SECTION par SECTION. Pour chaque section:
  1) ouvre les fichiers concernés, 2) résume les tâches, 3) applique les changements par petits commits,
  4) vérifie `npm run build`, `npm run test:a11y`, `npm run test:lighthouse`, 5) exécute `/compact`.
- Dark-only, contraste AA, rythme vertical 56px (48–72 ok).

========================================================
SECTION A — Globals (containers, typographie, rythme, safe-area)
========================================================
Fichiers: `src/styles/globals.css`, `src/theme/tokens.css` (ou `_tokens.css`), `src/layouts/Base.astro`.

Tâches:
1) Containers & gutters
- Ajoute des utilitaires pour un conteneur cohérent:
  .container-page { max-width: 1200px; margin-inline:auto; padding-inline: clamp(16px, 4vw, 32px); }

2) Typographie responsive (clamp)
- Applique un scale pour h1/h2:
  h1 { font-size: clamp(28px, 5vw, 56px); line-height: 1.05; letter-spacing: -0.01em; }
  h2 { font-size: clamp(22px, 3.6vw, 36px); line-height: 1.12; }

3) Rythme vertical
- Ajoute util `.section`:
  .section { padding-block: 56px; }
  @media (min-width: 1024px){ .section { padding-block: 72px; } }

4) Safe-area & scroll-margin
- :root { --safe-bottom: env(safe-area-inset-bottom); }
- body { padding-bottom: max(0px, var(--safe-bottom)); }
- [id] { scroll-margin-top: 96px; } /* pour éviter titres sous la nav */

5) Z-index & stacking context
- Définir couches:
  :root { --z-nav: 50; --z-banner: 40; --z-modal: 60; }

6) Réduire le blur coûteux
- Crée la classe `.glass` performante:
  .glass { background: rgba(255,255,255,.06); border: 1px solid rgba(255,255,255,.08); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); border-radius: 16px; }

Acceptance: build OK, classes disponibles. `/compact`

========================================================
SECTION B — Navbar “pilule” (overflow, wrap, contraste, hover)
========================================================
Fichier: `src/components/Navbar.astro`

Tâches:
1) Empêcher le débordement horizontal
- Sur le conteneur des items, utiliser `overflow-x-auto` + `scrollbar-gutter: stable;` + `snap-x`.
- Autoriser le wrap sur desktop moyen: `flex-wrap md:flex-nowrap`.

2) Padding & rayons stables
- Appliquer `.glass` sur la capsule; arrondis uniformes `rounded-2xl`; `px-3 md:px-4 py-2`.

3) État actif/hover lisibles
- Actif: texte `on-surface-strong`, légère lueur: `ring-1 ring-white/10`.
- Hover: underlines animées (via `after:` pseudo avec `scale-x`).

4) Z-index
- Nav: `z-[var(--z-nav)]` et `sticky top-0` (si sticky).
- Ajoute un fond `backdrop-blur` sur scroll (util `data-scrolled` sur window, classe `backdrop-blur-md` + `bg-white/5`).

Pseudo-code dans le composant (extraits):
- wrapper class: `glass sticky top-4 z-[var(--z-nav)] w-full overflow-x-auto snap-x`
- item class: `inline-flex items-center gap-2 px-3 md:px-4 py-2 rounded-xl hover:after:scale-x-100`

Acceptance: nav ne coupe plus, pas de chevauchement. `/compact`

========================================================
SECTION C — Hero (grille, ratios, CTA, badges)
========================================================
Fichier: `src/pages/index.astro` (et sections connexes).

Tâches:
1) Grille stable
- Utilise `grid md:grid-cols-2 gap-8 md:gap-12` dans le hero.
- Forcer les médias de droite avec aspect ratio: `aspect-[16/10] md:aspect-[4/3]` + `object-cover rounded-2xl glass`.

2) Titres & sous-titres
- Appliquer h1/h2 clamp (SECTION A), éviter `<br>` non nécessaires.
- Limiter largeur du texte: `max-w-[42ch]`.

3) CTA
- Boutons `min-h-[44px] px-5 rounded-xl` ; focus visible: `ring-2 ring-primary/60`.
- Stack mobile: `flex-col sm:flex-row gap-3`.

4) Badges
- Chips sous le H1 (`+ de réponses / zéro oubli / tout au même endroit`) en `flex flex-wrap gap-2` avec `rounded-full px-3 py-1 bg-white/8`.

Acceptance: pas de chevauchement, visuel droit bien cadré. `/compact`

========================================================
SECTION D — Bannières & Consentement (ne plus masquer le contenu)
========================================================
Fichier: `src/components/ConsentManager.astro`

Tâches:
1) Éviter le recouvrement
- Quand la bannière apparaît, ajoute `document.body.style.paddingBottom = 'calc(16px + var(--safe-bottom))'`; à la fermeture, remets `0`.
- Place la bannière avec `position: fixed; left: clamp(8px,3vw,24px); right: clamp(8px,3vw,24px); bottom: max(8px, var(--safe-bottom));`

2) Z-index
- `z-[var(--z-banner)]`.

3) A11y
- `role="dialog"` + `aria-live="polite"` + tab focus sur boutons.

Acceptance: la bannière n’écrase plus les CTA et remonte la page. `/compact`

========================================================
SECTION E — Sections internes (Relance, Prospection IA, Prix)
========================================================
Fichiers: `src/pages/relance-intelligente.astro`, `src/pages/prospection-ia.astro`, `src/pages/prix.astro`

Tâches:
1) Uniformiser le rythme
- Envelopper chaque grande section avec `.section container-page`.
- Supprimer padding inline redondant; ne pas doubler les marges.

2) Encarts “carte”
- Appliquer `.glass p-5 md:p-6 rounded-2xl border border-white/10`.
- Titre `text-on-surface-strong`, texte `text-on-surface`.

3) Listes à puces
- Utiliser `grid md:grid-cols-2 gap-6` pour “Pourquoi ce choix ?”.
- Empêcher les longues lignes: `max-w-[60ch]`.

4) Page prix
- Large titre centré avec clamp; tableaux en `grid md:grid-cols-3 gap-6`.
- Ajoute `scroll-margin-top` sur les ancres (si présentes).

Acceptance: pas de débordement de titres, pas d’espace “bizarre”. `/compact`

========================================================
SECTION F — Contrastes & classes “on-surface”
========================================================
Fichiers: `src/theme/tokens.css`, `globals.css`, composants divers.

Tâches:
1) Définir variables:
- --on-surface: hsla(220 20% 90% / 0.92); --on-surface-strong: #fff;
- Classes: `.on-surface{color:var(--on-surface)} .on-surface-strong{color:var(--on-surface-strong)}`

2) Appliquer aux textes de cartes, nav, badges; éviter `text-white/50` sur fond clair.

Acceptance: pa11y ne remonte plus d’alertes contraste. `/compact`

========================================================
SECTION G — Petits bugs récurrents (overflow, CLS, images)
========================================================
Tâches:
1) Empêcher les débordements
- Ajoute `overflow-hidden` sur grands wrappers de cards avec gros rayons (pour ne pas couper la lueur).
2) CLS
- Réserve la hauteur pour images (`aspect-[16/9]`) + `loading="lazy"` + `decoding="async"`.
3) Ancre cookie/CTA
- Assure-toi que le premier CTA hero est au-dessus de la bannière: `scroll-margin-bottom` n/a (géré via padding body, SECTION D).

Acceptance: Lighthouse CLS stable. `/compact`

========================================================
SECTION H — Tests & CI
========================================================
Tâches:
1) Relance la QA
- `npm run build`
- `npm run test:a11y`
- `npm run test:lighthouse`

2) Si score <95
- Réduire blur (`blur(10px)` max), limiter box-shadow lourds, compresser SVG bruyants (subset).

3) Commit final + changelog:
- “fix(marketing): stable navbar pill, hero grid clamp, consent banner safe-area, vertical rhythm, on-surface contrasts; QA ≥95”

/compact
