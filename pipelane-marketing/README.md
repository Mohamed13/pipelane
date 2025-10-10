# Pipelane Marketing Site

Landing page futuriste construite avec Astro et Tailwind CSS pour présenter la plateforme d'automations omni-canales Pipelane.

## Caractéristiques principales
- Dark mode par défaut avec bascule light/dark stockée dans `localStorage`.
- Animations légères (IntersectionObserver, parallax CSS) respectant `prefers-reduced-motion`.
- Sections héro, social proof, tour produit, bénéfices, marche à suivre, grille de fonctionnalités, intégrations, pricing, FAQ et CTA final.
- Formulaires de demande de démo (hero et CTA) envoyés vers `/api/demo-request` avec validation serveur.
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
pnpm dev      # démarrage du serveur Astro en mode développement
pnpm build    # build de production
pnpm preview  # prévisualisation du build
pnpm lint     # linting ESLint
pnpm format   # formatage Prettier
```

## Structure
```
pipelane-marketing/
├── public/              # assets statiques (favicon, OG image, sitemap)
├── src/
│   ├── components/      # composants Astro réutilisables (UI, cartes, CTA)
│   ├── layouts/         # layout de base avec SEO + scripts globaux
│   ├── pages/           # routes Astro (`/` et `/api/demo-request`)
│   ├── styles/          # styles globaux & tokens
│   └── utils/           # (réservé pour extensions)
├── tailwind.config.cjs
├── astro.config.ts
├── tsconfig.json
└── package.json
```

## Qualité & accessibilité
- Tailwind configuré avec tokens couleur, dégradés et helpers `glass`, `neon`, `chip`.
- ESLint + Prettier préconfigurés (`pnpm lint`, `pnpm format`).
- Animations basées sur IntersectionObserver + transform GPU (aucune dépendance lourde).
- Layout et contenu optimisés pour Lighthouse 95+ (Performance / Best Practices / SEO).

## Endpoint `/api/demo-request`
- Accepte `application/json` ou `x-www-form-urlencoded`.
- Valide `name`, `email`, `company`, `volume` et loggue les demandes côté serveur.
- Réponse JSON `{ ok: true }` ou `{ ok: false, error }` avec codes HTTP appropriés.

## Aller plus loin
- Brancher l'endpoint vers un CRM ou une file d'attente.
- Ajouter des tests de rendu (`@astrojs/check`) pour vérifier les sections critiques.
- Intégrer un service d'analytics (Fathom, Plausible) lors du déploiement.
