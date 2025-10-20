Tu travailles dans le repo local. Suis STRICTEMENT ce plan, sans sauter d’étapes.

RÈGLES GÉNÉRALES
- Pour chaque fichier, commence par : ouvrir le fichier, résumer les tâches, puis exécuter TOUTES les tâches une par une.
- Fais des petits commits clairs (message explicite) pendant l’exécution.
- Quand toutes les tâches du fichier sont terminées et que ça build/test OK, exécute la commande : /compact
- Si une étape échoue, corrige et relance jusqu’à succès avant de passer au fichier suivant.
- À la fin du cycle 01→06, fournis un récapitulatif des changements et des commandes utiles pour lancer la démo.

Commence par lancer la commande : /compact

ORDRE D’EXÉCUTION

1) FICHIER 01
- Ouvre `codex/01_api_ai_endpoints.md`.
- Résume les étapes.
- Exécute toutes les tâches (implémentation, config, endpoints, tests).
- Vérifie : build OK, tests OK.
- Exécute : /compact

2) FICHIER 02
- Ouvre `codex/02_ai_prompts_text.md`.
- Résume les étapes.
- Exécute toutes les tâches (prompts IA réutilisables, intégration service).
- Vérifie : build OK, tests liés OK.
- Exécute : /compact

3) FICHIER 03
- Ouvre `codex/03_scheduler_and_limits.md`.
- Résume les étapes.
- Exécute toutes les tâches (planificateur, limites, quiet hours, règles canal).
- Vérifie : build OK, tests OK.
- Exécute : /compact

4) FICHIER 04
- Ouvre `codex/04_front_buttons_and_tour.md`.
- Résume les étapes.
- Exécute toutes les tâches (UI Angular : boutons IA, relance intelligente, tooltips, tutoriel).
- Vérifie : build front OK, tests UI/unitaires OK.
- Exécute : /compact

5) FICHIER 05
- Ouvre `codex/05_optional_ports.md`.
- Résume les étapes.
- Exécute toutes les tâches (ports optionnels entrée/sortie désactivés par défaut + sécurité).
- Vérifie : build OK, tests OK.
- Exécute : /compact

6) FICHIER 06
- Ouvre `codex/06_seed_and_smoke_tests.md`.
- Résume les étapes.
- Exécute toutes les tâches (seeds démo + 3 smoke tests).
- Vérifie : build OK, tests OK.
- Exécute : /compact

CONTRÔLES DE FIN
- Lancer les commandes locales pour vérifier que tout fonctionne (API + Front).
- Produire un RÉCAP FINAL contenant :
  - les endpoints créés (paths, payloads d’exemple),
  - les pages/écrans ajoutés côté front et comment les utiliser,
  - comment lancer les 3 scénarios “smoke” (générer → classer → relance intelligente),
  - variables d’environnement à renseigner (ex. OPENAI_API_KEY),
  - commandes npm/dotnet utiles pour build/test/run.

Commence maintenant par le fichier 01, et n’avance au 02 qu’une fois le 01 complètement exécuté et compacté, etc. jusqu’au 06.
