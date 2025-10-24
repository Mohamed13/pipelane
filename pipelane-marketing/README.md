# Pipelane Marketing

Site marketing Astro + Tailwind mettant en avant l’agent de prospection IA, la relance intelligente et la console omni-canale Pipelane.

## Lancer la démo marketing

`ash
cd pipelane-marketing
npm install       # ou pnpm install
define PUBLIC_GA_ID=              # optionnel GA4
set PUBLIC_LINKEDIN_ID=           # optionnel Pixel LinkedIn
set PUBLIC_DEMO_MODE=false         # active le bouton \ Lancer la démo\
set PUBLIC_CONSOLE_URL=http://localhost:4200  # URL console pour ouvrir la sandbox
npm run dev
`

- Aperçu live sur http://localhost:4321 (démarrage auto via 
pm run dev).
- 
pm run build && npm run preview pour vérifier le build statique.
- 
pm run test:a11y et 
pm run test:lighthouse (Lighthouse mobile ≥ 95, A11Y ≥ 95).

## Fonctionnalités

- Navigation mise à jour : Produit, Prospection IA (BÊTA), Relance intelligente (BÊTA), Prix, Sécurité & RGPD, Ressources (blog), Demander une démo.
- Nouvelles pages dédiées (/prospection-ia, /relance-intelligente, /prix, /securite-rgpd, /blog, /changelog).
- Hero refondu : promesse claire + formulaire « Demander une démo » avec pré-remplissage UTM.
- Sections d’accueil : gains, mode opératoire, relance intelligente, MVP sans n8n, tableau de bord, réassurance, CTA final.
- Formulaires centralisés (DemoForm) : champs requis + message libre, UTMs cachés, toast de confirmation, push demo_submit dans dataLayer.
- Consentement cookies : bannière acceptation/refus, GA4 + LinkedIn chargés uniquement après consentement.
- SEO : titles/descriptions uniques, schema.org Product + FAQ, sitemap/robots à jour.
- Blog en mode brouillon (3 articles 600–900 mots) avec layout dédié + CTA final.

## Structure

`
pipelane-marketing/
├── public/
│   ├── robots.txt
│   └── sitemap.xml
├── src/
│   ├── components/ (Navbar, Footer, DemoForm, ConsentManager…)
│   ├── layouts/ (Base, PostLayout)
│   ├── pages/ (landing + pages dédiées + API demo-request)
│   └── styles/scripts
`

## Scripts utiles

`ash
npm run dev            # serveur Astro développement
npm run build          # build production
npm run preview        # prévisualisation
npm run test:a11y      # pa11y-ci sur /, /prospection-ia, /relance-intelligente, /prix, /securite-rgpd
npm run test:lighthouse# Lighthouse mobile (accessibilité ≥ 95)
`

## Notes

- PUBLIC_GA_ID et PUBLIC_LINKEDIN_ID sont optionnels. Sans consentement ou sans valeur, aucun tag n’est chargé.
- Les ports optionnels (webhooks/action entrante) sont mentionnés mais désactivés par défaut.
- Le formulaire /api/demo-request journalise les demandes côté serveur (console) et répond en JSON.
- Les styles glass, on-surface, scrim assurent contraste AA/AAA et focus visibles.
- La CI (`.github/workflows/marketing-ci.yml`) exécute build + `npm run test:a11y` + `npm run test:lighthouse` (seuil accessibilité ≥ 0,95).
