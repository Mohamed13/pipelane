Ouvre ce fichier, résume, puis ajoute un seed et 3 smoke tests.

Seed
- 20 contacts démo (FR/EN), 1 mini séquence (J0/J+3), 1 campagne.
- 3 messages existants pour 3 contacts, avec 1 réponse entrante simulée.

Smoke tests (manuel ou script minimal)
1) Générer un message (IA) sur un contact → voir l’aperçu → envoyer.
2) Classer une réponse (IA) → voir le badge "Interested" et l’action proposée.
3) Relance intelligente ON → suggérer "mardi 10:30" angle "value" → valider → vérifier l’envoi planifié.

Acceptance
- Les 3 scénarios passent sans erreur.
