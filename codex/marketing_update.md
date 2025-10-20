Ouvre ce fichier, résume toutes les étapes, puis exécute-les une par une avec de petits commits clairs.
Projet: pipelane-marketing (Astro + Tailwind). Objectif: mettre en avant l’Agent de Prospection B2B, la Relance intelligente, l’omni-canal et la simplicité (MVP sans n8n) avec 2 ports optionnels pour l’avenir.

-------------------------------------
1) Navigation & structure
-------------------------------------
- Mettre à jour la nav (header + footer) avec ces entrées:
  Header: Produit, Prospection IA, Relance intelligente, Prix, Sécurité & RGPD, Ressources (Blog), Demander une démo.
  Footer: Produit, Prospection IA, Relance intelligente, Prix, Sécurité & RGPD, Statut, Docs, Contact, Mentions légales/Privacy.
- Créer/mettre à jour les pages:
  / (Landing principale)
  /prospection-ia (nouvelle page dédiée)
  /relance-intelligente (nouvelle page dédiée)
  /prix (tiers Good/Better/Best)
  /securite-rgpd (nouvelle page)
  /blog (listing minimal + 3 brouillons)
  /changelog (simple timeline)
- Ajouter un “badge” BÊTA pour Prospection IA & Relance intelligente si utile.

-------------------------------------
2) Hero & promesse (page /)
-------------------------------------
- Remplacer le Hero avec un message simple:
  Eyebrow: Console omni-canale + Agent IA
  H1: Votre assistant commercial qui ne dort jamais
  Sub: Pipelane centralise WhatsApp, Email et SMS, rédige et envoie des messages personnalisés, relance intelligemment et vous amène des rendez-vous qualifiés — sans charge mentale.
  CTA primaire: Demander une démo
  CTA secondaire: Voir l’agent de prospection
- Ajouter 3 bullets: + de réponses • Zéro oubli • Tout au même endroit
- Visuel: mockup console + petite carte “Prochaine relance mardi 10:30 · angle valeur”.

-------------------------------------
3) Sections page d’accueil
-------------------------------------
A) “Ce que vous gagnez”
- Cartes 3 bénéfices:
  Gain de temps: fini les emails manuels et les relances oubliées.
  Régularité: l’agent travaille chaque jour, à la bonne heure.
  Résultats: plus de réponses, plus de rendez-vous qualifiés.

B) “Comment ça marche”
- Étapes simples (icônes):
  1) Connectez vos canaux (Email/WhatsApp/SMS)
  2) Importez vos contacts et votre pitch
  3) L’agent écrit, envoie, relance et lit les réponses
  4) Vous ne gérez que les rendez-vous intéressés

C) “Relance intelligente”
- Encadré avec capture ou illustration “carte prochaine relance”.
- Points: choisit le bon moment, adapte l’angle (rappel, valeur, preuve, question), reste modifiable.

D) “Tout en un (MVP sans n8n)”
- Texte court: Tout tourne dans Pipelane. Vous pouvez activer 2 ports optionnels (événements sortants, actions entrantes) si besoin d’intégrations plus tard.

E) “Tableau de bord”
- KPI line/donut (images statiques ou SVG) + texte: envoyés, délivrés, ouverts, réponses, RDV.

F) “Preuve & réassurance”
- 3 mini-témoignages (placeholders) + logos (placeholders).
- Encadré “Sécurité & RGPD”: données chiffrées, opt-out, hébergement UE possible.

G) CTA final
- Titre: Remplissez votre pipe sans écrire un seul email.
- Boutons: Demander une démo · Voir la prospection IA

-------------------------------------
4) Page /prospection-ia (nouvelle)
-------------------------------------
- H1: Agent de prospection IA
- Intro (copie FR à insérer):
  “Donnez-lui votre cible et votre pitch. Il écrit des emails courts et personnalisés, les envoie à la bonne cadence, relance intelligemment, lit les réponses et propose l’action suivante. Vous vous concentrez sur les rendez-vous.”
- Bloc “Ce qu’il fait”:
  • Rédige FR/EN, adapte le ton
  • Envoie étalé (horaires corrects)
  • Relance 2–3 fois si pas de réponse
  • Classe les réponses (intéressé, plus tard, etc.)
  • Répond aux demandes simples (infos, créneaux)
- Bloc “Démarrage guidé”:
  1) Connecter l’email d’envoi
  2) Écrire votre pitch
  3) Importer vos contacts (CSV)
  4) Aperçu d’un email personnalisé
  5) Lancer
- Encadré “Sans n8n”: tout est natif. Ports optionnels prêts si besoin d’intégrations spécifiques plus tard.
- CTA: Demander une démo

-------------------------------------
5) Page /relance-intelligente (nouvelle)
-------------------------------------
- H1: Relance intelligente
- Intro:
  “La relance qui ne saoule pas: bon moment, bon angle, 3–6 lignes utiles. Modifiable à tout moment, avec explication ‘Pourquoi ce choix ?’.”
- Points clés:
  • Analyse de l’historique (dernier échange, lu/non lu)
  • Moment optimal (jours/heures qui marchent)
  • Angles testés: rappel, valeur, preuve sociale, question
  • A/B léger (objet/phrase d’ouverture)
  • Garde-fous: opt-out, horaires, limites
- Carte “Prochaine relance” en exemple (mockup).
- CTA: Activer la relance intelligente

-------------------------------------
6) Page /prix (mettre à jour)
-------------------------------------
- Tiers clairs (texte FR prêt à insérer):
  Good — 99€/mois
    • 1 utilisateur • 1000 emails/mois • Prospection IA de base • Support email
  Better — 249€/mois
    • 3 utilisateurs • 5000 emails/mois • Classif réponses + relance intelligente • Intégration calendrier • Support prioritaire
  Best — 499€/mois
    • Utilisateurs illimités • Emails illimités* • Templates & modèles sur mesure • SSO & SLA • Success manager
  Astérisque: limites raisonnables anti-abus, détails sur la page.
- Encadrés:
  • Essai gratuit 14 jours
  • Pilot -50% pour 3 premiers clients en échange d’un témoignage
- FAQ billing: essai, facturation, limites, annulation.

-------------------------------------
7) Page /securite-rgpd (nouvelle)
-------------------------------------
- Titres & contenus simples:
  • Données & hébergement (UE possible)
  • Chiffrement au repos/en transit
  • Opt-out et gestion des désinscriptions
  • Export/suppression sur demande
  • Modération IA (ton respectueux, pas de sujets sensibles)
  • Bonnes pratiques délivrabilité (SPF/DKIM, cadence)
- CTA discret: Demander une démo

-------------------------------------
8) Formulaires & conversion
-------------------------------------
- Form “Demander une démo” (Hero et CTA final): name, email, company, volume (select), message libre.
- Champs cachés UTM (utm_source, utm_medium, utm_campaign, gclid, fbclid).
- Endpoint existant /api/demo-request: afficher un toast “Merci, on revient vers vous sous 24 h”.
- Ajouter micro-preuve: “Réponse sous 24 h” + “Zéro spam”.

-------------------------------------
9) SEO, a11y, perf
-------------------------------------
- SEO:
  • Titles/Descriptions uniques par page
  • Balises OpenGraph/Twitter
  • schema.org Product + FAQ sur /prix et / (FAQ JSON-LD)
  • sitemap.xml, robots.txt, canonical
- a11y:
  • Contrastes AA (utiliser .scrim et classes on-surface/on-surface-strong)
  • Focus visible (anneau “neon”)
  • Respect prefers-reduced-motion
- Perf:
  • Lazy images, images responsives via <Image />
  • Lighthouse ≥ 95 (mobile)
  • Pa11y/axe: 0 erreur contrast/labels

-------------------------------------
10) Copy FR — blocs prêts à coller
-------------------------------------
- Slogan court:
  “Remplissez votre pipe sans écrire un seul email.”
- Sous-titre prospection:
  “L’agent IA qui rédige, envoie, relance et répond — vous ne gérez que les rendez-vous intéressés.”
- Bullets:
  “+ de réponses”, “Zéro oubli”, “Tout au même endroit”
- Encadré MVP sans n8n:
  “Tout tourne dans Pipelane. Besoin d’intégrations spécifiques ? Activez nos deux ports optionnels (événements sortants, actions entrantes) quand vous le souhaitez.”
- Relance intelligente (phrase):
  “Bon moment, bon angle, message court. Modifiable à tout instant, avec ‘Pourquoi ce choix ?’ pour rester transparent.”

-------------------------------------
11) Blog & contenus (préparer l’amorçage)
-------------------------------------
- Créer 3 brouillons (MDX) dans /blog:
  1) “Comment obtenir ses 10 premiers rendez-vous B2B avec un agent IA (sans équipe sales)”
  2) “Relances intelligentes: 4 angles qui débloquent des réponses”
  3) “MVP sans n8n: pourquoi la simplicité gagne au début”
- Chaque article: 600–900 mots, ton simple, CTA final “Demander une démo”.

-------------------------------------
12) Tracking & consentement
-------------------------------------
- Ajouter GA4 + Pixel LinkedIn (chargés après consentement).
- Bandeau cookies simple: “Accepter / Refuser”. En cas de refus, ne pas charger les tags.
- Événements:
  cta_click {location:"hero|final|page-…"}
  demo_submit {company, volume, utm_source}
  pricing_view {plan:"Good|Better|Best"}

-------------------------------------
13) Tests & CI léger
-------------------------------------
- Scripts:
  npm run test:a11y  → pa11y-ci sur /, /prospection-ia, /relance-intelligente, /prix, /securite-rgpd
  npm run test:lighthouse → vérif mobile perf ≥95 & a11y ≥95
- Ajouter liens “Open in Gmail/Calendar” si l’endpoint de démo envoie un email (optionnel).
- À la fin: /compact

-------------------------------------
ACCEPTANCE (tout doit être vrai)
-------------------------------------
- Nouvelle nav et 5 pages dédiées opérationnelles.
- Hero, sections et CTA reflètent Prospection IA + Relance intelligente + MVP sans n8n.
- Formulaires envoient bien sur /api/demo-request (avec UTM).
- SEO/a11y/perf: Lighthouse mobile ≥95, pa11y 0 erreurs critiques.
- 3 brouillons de blog présents avec CTA.
- Page Sécurité & RGPD claire, rassurante.
- Texte FR simple, lisible, sans jargon.

Livrables finaux:
- Code Astro/Tailwind à jour, images/illustrations ajoutées (placeholders OK).
- README court “Comment lancer la démo marketing”.
- Journal des changements (changelog) mis à jour.
