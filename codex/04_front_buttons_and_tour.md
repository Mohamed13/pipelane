Ouvre ce fichier, résume, puis mets à jour pipelane-front (Angular 20).

Objectif côté UI
- Ajouter 2 boutons sur la conversation:
  (A) "Générer un message (IA)" → appelle /api/ai/generate-message → affiche aperçu → "Envoyer".
  (B) "Classer la réponse (IA)" → appelle /api/ai/classify-reply → badge d’intention + action proposée.
- Ajouter un interrupteur "Relance intelligente ON/OFF" (conversation + campagne).
- Afficher une carte "Prochaine relance" : date/heure, angle, aperçu, boutons "Valider", "Modifier", "Reporter", "Stop".
- Ajouter des tooltips (MatTooltip) explicatifs sur chaque bouton/option.
- Un mini tutoriel (ngx-shepherd) de 5 étapes la première fois:
  1) Connecter l’email d’envoi,
  2) Écrire le pitch,
  3) Importer des contacts,
  4) Générer un message test,
  5) Activer la relance intelligente.

Tâches
1) Services
- ApiService: méthodes pour POST /api/ai/generate-message, /api/ai/classify-reply, /api/ai/suggest-followup.
- Gestion erreurs: toasts clairs.

2) Composants
- ConversationThread: 
  - boutons IA,
  - panneau “Relance intelligente” (switch + carte suivante relance).
- CampaignBuilder:
  - switch “Relance intelligente par défaut” pour la campagne.

3) Tooltips (texte concis)
- "L’IA propose un message court et pro. Relis et envoie en un clic."
- "Classe la réponse: intéressé, à revoir plus tard, etc."
- "La relance intelligente choisit le bon moment et un angle adapté."

4) Tutoriel (ngx-shepherd)
- Drapeau localStorage `pipelane_tour_done=false` au départ.
- Bouton "Aide → Revoir le tutoriel".

Acceptance
- Les 3 appels IA fonctionnent (mock si besoin).
- Le switch relance intelligente persiste par conversation/campagne.
- Tooltips visibles; tutoriel se lance la première fois.
