You are a senior front-end developer. Build a production-ready marketing site using Astro + Tailwind CSS for the product “Pipelane”.

### Goals
- A futuristic, sleek, ergonomic landing page that sells a multi-tenant, omni-channel messaging & automation platform.
- Smooth micro-interactions, glassmorphism, soft neon accents, subtle parallax, GPU-accelerated transforms. No heavy libs.
- 95+ Lighthouse (Performance/Best Practices/SEO), responsive, dark by default with light mode toggle.
- Clean, accessible, semantic HTML; focus on copy clarity and visual hierarchy.

### Tech & Setup
- Framework: Astro latest + TypeScript.
- Styling: Tailwind + CSS variables for theme tokens. Add a tiny utility for glass panels & neon borders.
- Animations: Use small vanilla intersection observers + CSS transitions (no GSAP/Framer); parallax via CSS perspective + transform on scroll; staggered reveals.
- Assets: create simple SVG logos/placeholders where needed; use Iconify for icons (minimal).
- Integrations: 
  - Form CTA posts to `/api/demo-request` (Astro endpoint that logs to console for now, easily swappable to backend).
  - OpenGraph & Twitter meta, sitemap, robots, canonical.

### Design System (tokens)
- Base font: Inter.
- Colors: 
  - bg: #0b0f17 (dark), surface: rgba(255,255,255,0.06), lines: rgba(255,255,255,0.08)
  - primary neon: #75F0FF, secondary: #9B8CFF, accent: #60F7A3
  - gradient: linear-gradient(135deg, #75F0FF 0%, #9B8CFF 45%, #60F7A3 100%)
- Effects: glass blur(12px), soft shadow, neon border on focus/hover, subtle grid background with animated radial gradient.

### Site Map
- `/` single-page landing with sections: 
  1) Hero (value prop + product shots)
  2) Social proof (logos + short quotes)
  3) Product tour (cards + mini timeline of a message lifecycle)
  4) Benefits (why it matters, quantified)
  5) How it works (3 steps)
  6) Features grid (omni-channel, rules, analytics, follow-ups, webhooks, multi-tenant)
  7) Integrations (WhatsApp, Email, SMS, Resend, n8n, Meta Cloud, Slack)
  8) Pricing teaser (simple tiers)
  9) FAQ
  10) Final CTA
- `/api/demo-request` (Astro endpoint mock).

### Copy (FR) — use exactly but allow minor micro-edits for layout
HERO:
- Eyebrow: Plateforme d’automations omni-canales
- H1: Pipelane — centralisez, automatisez, convertissez.
- Sub: Une console unique pour capter, envoyer et suivre tous vos messages (WhatsApp, Email, SMS), avec règles intelligentes, analytics temps réel et suivi multi-tenant.
- Primary CTA: Demander une démo
- Secondary CTA: Voir le produit
- Bullets: Multi-tenant • Automations no-code • Analytics temps réel

SOCIAL PROOF:
- Title: Déjà adoptée par des équipes orientées résultats
- Logos: (placeholders) NovaTech, Velia, QuantifyX, Orbitline, Bluehawk
- Quotes (short): “Mise en prod en 48h”, “+22% de réponses”, “On a enfin une vision claire”

PRODUCT TOUR:
- Title: Votre pipeline de messages, de A à Z
- Steps (with mini timeline visuals):
  1) Capture & import (formulaires, webhooks, CSV)
  2) Orchestration & règles (fenêtre WhatsApp, séquences, quiet hours)
  3) Envoi & suivi (statuts Sent/Delivered/Open/Failed)
  4) Analytics & conversions (sources, templates, ROI)

BENEFITS (3 blocks, quantify):
- “+20–35% de taux de réponse” grâce aux séquences et relances intelligentes.
- “Jusqu’à 2h gagnées/jour” par opérateur via automatisations & templates.
- “Vue consolidée” pour prioriser ce qui convertit vraiment.

HOW IT WORKS (3 steps):
1) Connectez vos canaux (WhatsApp, Email, SMS)
2) Créez vos règles (si/alors) et séquences
3) Lancez vos campagnes et suivez les conversions

FEATURES GRID (6):
- Omni-canal: WhatsApp, Email, SMS dans une seule timeline.
- Règles & séquences: triggers, conditions, actions, fenêtres horaires.
- Analytics avancées: sent/delivered/open/failed, par template/canal.
- Follow-ups intelligents: relances auto si pas de réponse.
- Webhooks & intégrations: n8n, Resend, Slack, Meta Cloud.
- Multi-tenant & rôles: ségrégation stricte, audit, sécurité.

INTEGRATIONS:
- Petites cards icônes: WhatsApp Cloud, Resend, n8n, Slack, SMTP, Webhooks.

PRICING:
- Starter: 49€/mois — 1 tenant, 3 utilisateurs, 10k events
- Growth: 149€/mois — 5 tenants, 15 utilisateurs, 100k events
- Scale: Sur mesure — SSO, SLA, support dédié
- Note: “Aucun frais caché. Annulable à tout moment.”

FAQ (6):
- Puis-je utiliser uniquement l’email ? — Oui, chaque canal est optionnel.
- Gérez-vous la fenêtre WhatsApp 24h ? — Oui, règles automatiques intégrées.
- Puis-je brancher mes webhooks entrants ? — Oui, endpoints dédiés par canal.
- Données & sécurité ? — Multi-tenant, chiffrage des secrets, audit & RBAC.
- Essai gratuit ? — Démo guidée + sandbox possible.
- Migration depuis mon outil actuel ? — Import CSV + APIs.

FINAL CTA:
- Title: Prêt à centraliser vos messages et accélérer la conversion ?
- Primary: Demander une démo
- Secondary: Voir la documentation

FOOTER:
- Liens: Produit, Prix, Docs, Sécurité, Contact
- Mentions: © Pipelane. Tous droits réservés.

### Implementation details
- Create components under `src/components`: 
  - `NeonBadge.astro`, `Section.astro`, `FeatureCard.astro`, `IntegrationCard.astro`, `PricingCard.astro`, `FAQItem.astro`, `CTA.astro`.
- Create `src/layouts/Base.astro` with full SEO (title, description, og:image placeholder, theme-color).
- Background: animated radial gradient + subtle grid SVG; parallax layers for hero orbs (pure CSS).
- Add a floating “glass” nav with active underline hover effect; sticky; scroll spy for section links.
- Add a theme toggle (dark/light) storing preference in localStorage.
- Add IntersectionObserver utility to add `.reveal-in` class (translateY(12px) → 0, opacity 0 → 1).
- Ensure reduced motion preference is honored.
- Optimize images with Astro `<Image />` where applicable.

### Forms & Endpoint
- In hero & final CTA, add a simple form (name, email, company, message volume dropdown). POST to `/api/demo-request`.
- Implement `/src/pages/api/demo-request.ts` to validate payload server-side, log to console, and return 200 JSON `{ok:true}`.

### Tailwind config
- Enable JIT, add custom colors, shadows, neon ring utilities, container padding, and font Inter.
- Create utility classes: `.glass`, `.neon`, `.btn-primary`, `.btn-ghost`, `.chip`.

### Build & Quality
- Add ESLint + Prettier basic config.
- Add `pnpm` scripts: dev, build, preview.
- Provide a README with setup steps.

### Deliverables
- Full Astro project with the landing page live at `/`.
- Clean, commented code, easily customizable, no dead CSS.
- LCP under 1.8s on mid-range mobile.

Now generate the complete project files.
