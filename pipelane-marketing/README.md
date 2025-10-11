# Pipelane Marketing Site

Landing page futuriste construite avec Astro et Tailwind CSS pour présenter la plateforme d'automations omni-canales Pipelane.

## Caractéristiques principales
- Thème sombre clair respectant WCAG 2.2 AA (tokens foreground/surface dédiés).
- Animations légères (IntersectionObserver, parallax CSS) respectant `prefers-reduced-motion`.
- Sections héro, social proof, tour produit, bénéfices, marche à suivre, features, intégrations, pricing, FAQ, récits clients et CTA final avec médias illustratifs.
- Formulaires de demande de démo (hero + CTA) envoyés vers `/api/demo-request` avec validation serveur.
- Métadonnées SEO complètes (OpenGraph, Twitter), favicon, sitemap et robots.

## Prérequis
- Node.js 20.11 ou supérieur (recommandé Node 20.14).
- Gestionnaire de paquets : `pnpm` (recommandé) ou `npm`/`yarn`.

## Installation
```bash
pnpm install
# ou
npm install
```

## Scripts
```bash
pnpm dev            # démarrage du serveur Astro en mode développement
pnpm build          # build de production
pnpm preview        # prévisualisation du build
pnpm preview:ci     # preview en mode CI (127.0.0.1:4321)
pnpm lint           # linting ESLint
pnpm format         # formatage Prettier
pnpm test:a11y      # audit contrastes / a11y via pa11y-ci (nécessite un port libre 4321)
pnpm test:lighthouse# Lighthouse accessibilité (échoue si score < 95)
```

## Structure
```
pipelane-marketing/
├── public/              # assets statiques (favicon, OG image, sitemap)
├── src/
│   ├── assets/img/      # médias illustratifs (mockups, analytics, intégrations)
│   ├── components/      # UI réutilisable (Section, FeatureCard, CTA…)
│   ├── layouts/         # layout de base + scripts globaux
│   ├── pages/           # routes Astro (`/` et `/api/demo-request`)
│   ├── scripts/         # vérifications front (contrast-check)
│   └── styles/          # tokens + global styles
├── pa11yci.config.cjs   # configuration pa11y-ci
├── tailwind.config.cjs
├── astro.config.ts
└── package.json
```

## Qualité & accessibilité
- Palette AA pilotée par `src/styles/tokens.css` (surfaces/foregrounds pour sombre & clair).
- Utilitaires Tailwind personnalisés : `.on-surface`, `.on-surface-strong`, `.scrim`, variantes `glass`.
- Pa11y CI + Lighthouse automatisés (`test:a11y`, `test:lighthouse`).
- Script `src/scripts/contrast-check.ts` déclenché en dev : logge les éléments < AA en console.
- Images responsive via `<Image />` (`astro:assets`), lazy-load et scrim overlay pour garantir la lisibilité.

## Endpoint `/api/demo-request`
- Accepte `application/json` ou `x-www-form-urlencoded`.
- Valide `name`, `email`, `company`, `volume` et journalise les demandes côté serveur.
- Réponse JSON `{ ok: true }` ou `{ ok: false, error }` avec codes HTTP appropriés.

## Aller plus loin
- Brancher l'endpoint vers un CRM ou une file d'attente.
- Ajouter des tests de rendu (`@astrojs/check`) pour vérifier les sections critiques.
- Intégrer un service d'analytics (Fathom, Plausible) lors du déploiement.
