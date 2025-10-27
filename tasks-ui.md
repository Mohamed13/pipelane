Tu travailles dans pipelane-marketing (Astro + Tailwind, dark-only).
Objectif: refondre la page d’accueil (/) pour expliquer, en 1 lecture, le flux complet:
Lead Hunter (recherche intelligente) → Cadences multi-canales → Relance intelligente → Qualification & réservation (2 créneaux).

RÈGLES
- N’écrase pas le design system: tokens, .glass, on-surface, .scrim, contraste AA.
- Commits atomiques par section; après chaque section: npm run build, npm run test:a11y, npm run test:lighthouse, puis /compact.
- FR par défaut; ajoute EN en alt entre parenthèses dans le contenu.

========================================
SECTION 0 — Préparation & composants
========================================
Fichiers: src/components/{Hero.astro, FlowStep.astro, FeatureCard.astro, StatCard.astro, CTA.astro}, src/layouts/Base.astro, src/pages/index.astro

Tâches:
1) Vérifie/ajoute utilitaires:
- .container-page { max-width: 1200px; margin-inline:auto; padding-inline: clamp(16px, 4vw, 32px); }
- .section { padding-block: 56px; } @media (min-width:1024px){ .section{ padding-block:72px; } }
- h1 { font-size: clamp(28px, 5vw, 56px); line-height:1.05; letter-spacing:-0.01em }
- h2 { font-size: clamp(22px, 3.4vw, 36px); line-height:1.12 }

2) Crée/valide composants:
- <FlowStep title desc icon> (petite carte horizontale)
- <FeatureCard title desc bullets[] image?> (carte .glass)
- <StatCard label value hint?>
- <CTA title primary{label,href} secondary?>

========================================
SECTION 1 — Hero “Trouver & Remplir l’agenda”
========================================
Fichier: src/pages/index.astro

Remplacer le hero par une grille 2 colonnes (texte à gauche, visuel à droite):

Titre:
"Votre agent qui trouve des clients et remplit votre agenda" 
(EN: The agent that finds clients and fills your calendar)

Sous-titre:
"Pipelane repère les bons prospects avec une recherche intelligente, lance des séquences multi-canales (Email/WhatsApp/SMS), relance au bon moment et propose 2 créneaux — vous ne gérez que les RDV chauds."
(EN: Smart lead hunting + multi-channel cadences + smart follow-ups + 2-slot booking.)

Chips (3):
"+ de réponses" • "Zéro oubli" • "Tout au même endroit"

CTA:
- primaire: Demander une démo → "/#demo" (ou page dédiée)
- secondaire: Voir comment ça marche → ancre vers la section “De A à Z”

Visuel (droite):
- Card .glass avec mock “Console + Carte+Liste scorée + Carte relance” (placeholder image ou bloc gradient avec badges).

========================================
SECTION 2 — Bloc Lead Hunter IA (Trouver des clients)
========================================
Fichier: src/pages/index.astro (ajoute une <section> après le hero)

Contenu (colonne gauche):
Titre: "Trouvez les bons prospects en 60 secondes"
Texte:
"Décrivez votre cible ou choisissez un secteur. L’agent analyse cartes/annuaires/données publiques, normalise, enrichit (site/email/réseaux), applique un scoring 0–100 et affiche des raisons claires 'Pourquoi ce lead ?'."
(EN alt fourni sur les mêmes lignes)

Bullets:
- "Critères simples (secteur, zone, avis, site, réseaux)" (Simple criteria)
- "Liste scorée + raison claire" (Scored list + clear reasons)
- "1-clic : créer la liste → créer la cadence" (1-click list → cadence)

Colonne droite (FeatureCard):
- Titre: "Why this lead?"
- bullets: "Pas de réservation en ligne", "Site lent mobile", "Instagram actif"
- Badge "Score 0–100"
- CTA secondaire: "Voir l’agent de prospection" → "/prospection-ia"

========================================
SECTION 3 — Cadences multi-canales (Prospecter)
========================================
Grille 3 colonnes “Email / WhatsApp / SMS”, chaque carte .glass liste:
- "Caps/jour protégés", "Fenêtres horaires", "Variantes A/B légères"
Note conformité (petit encart en dessous):
"Conformité: Email (désinscription), SMS (STOP), WhatsApp 24h (HSM si hors fenêtre)."

Court paragraphe:
"L’IA rédige des messages courts et utiles (≤120 mots), FR/EN."

========================================
SECTION 4 — Qualification & Booking (Book)
========================================
Deux colonnes:
Gauche:
Titre: "Qualifiez en 3–5 questions, proposez 2 créneaux"
Texte:
"Sur réponse, l’agent pose 3–5 questions, score l’intention, puis propose 2 créneaux (ou un lien) et confirme le RDV."
Bullets: "Intent 0–100", "2-slot offer", "Ajout auto au calendrier/CRM"

Droite: carte .glass "Offre 2 créneaux"
- "Mar 10:00–10:30" • "Mer 15:30–16:00" • bouton "Confirmer"

========================================
SECTION 5 — Relance intelligente (Nurture)
========================================
Grille 2 colonnes:
- Col gauche: Exemple carte relance .glass
  Titre ligne: "Relance mardi 10:30 · angle valeur"
  Message (3–6 lignes):
  « Bonjour Camille, vous m’indiquiez chercher un moyen d’automatiser vos relances.
  Voici comment NovaOps a doublé ses RDV en 5 semaines, je vous envoie le cas ? »
  Provenance (petit): "Dernier échange lu · Pas de réponse depuis 3 jours"
- Col droite: "Pourquoi ce choix ?" (liste)
  • ancienneté de l’échange • heure locale qui performe • angle valeur pour ce secteur

Note bas de bloc (petit): "Respect opt-out/STOP et policy WhatsApp 24h (HSM si hors fenêtre)."

========================================
SECTION 6 — Timeline “De A à Z”
========================================
Titre centré: "De la recherche au RDV, en 4 étapes"
Sous-ligne (EN alt): "(Find → Prospect → Nurture → Book)"
Liste horizontale (4 <FlowStep>):
1) Trouver — "Lead Hunter IA → liste scorée"
2) Prospecter — "Cadence Email/WA/SMS"
3) Relancer — "Moment + angle + message court"
4) Booker — "2 créneaux → confirmation → CRM"

========================================
SECTION 7 — Preuve & réassurance
========================================
3 <StatCard>:
- "TTR" → "26 s" • hint "réponse inbound moyenne"
- "Reply rate" → "12 %" • hint "sur 30 jours"
- "RDV / 100" → "8" • hint "dépend du secteur"
Encart lien: "Sécurité & RGPD" → "/securite-rgpd"

========================================
SECTION 8 — CTA final
========================================
<CTA
  title="Remplissez votre pipe sans écrire un seul email."
  primary={ {label:"Demander une démo", href:"/#demo"} }
  secondary={ {label:"Voir l’agent de prospection", href:"/prospection-ia"} } />

========================================
SECTION 9 — QA & Performance
========================================
- Vérifier contrasts AA (classes .on-surface / .on-surface-strong), éviter text-white/50 sur fond clair.
- Héros: aspect-ratio sur visuels, `loading="lazy" decoding="async"` et éviter CLS (réserver les hauteurs).
- Safe-area cookie banner: s’assurer que ConsentManager ajoute padding-bottom dynamique (déjà implémenté).

Commandes:
- npm run build
- npm run test:a11y
- npm run test:lighthouse

Critères d’acceptation (doivent être vrais):
- Hero clair et lisible 320–1440px, boutons 44px min.
- Les 4 blocs (Trouver/Prospecter/Relancer/Booker) sont visibles sans ambiguïté et reliés par la section Timeline.
- Lighthouse mobile ≥ 95, pa11y 0 erreurs critiques.
- Aucune superposition avec la bannière cookies; nav sticky ne cache pas les ancres.

Commit final:
`feat(marketing): home refactor — Lead Hunter + cadences + smart follow-up + 2-slot booking; end-to-end flow explained`
/compact
