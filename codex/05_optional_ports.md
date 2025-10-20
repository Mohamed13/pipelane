Ouvre ce fichier, résume, puis ajoute 2 routes optionnelles (désactivées par défaut).

Objectif
Préparer une ouverture vers l’extérieur (plus tard n8n ou autre), sans impacter le cœur.

Tâches
1) Sortie (events) — désactivée par défaut
- Config: AUTOMATIONS_EVENTS_ENABLED=false, AUTOMATIONS_TOKEN=
- Quand un événement utile arrive (contact.created, message.sent, message.status.changed):
  - Si activé: POST vers AUTOMATIONS_EVENTS_URL avec header X-Automations-Token.
  - Retry x3, logs.

2) Entrée (actions) — désactivée par défaut
- POST /api/automations/actions (header X-Automations-Token)
  - Body: { type: "send_message|create_task|schedule_followup", data: {...} }
  - Effectuer l’action si token valide et données complètes.
- Retourner {ok:true} ou un 400 clair.

3) Sécurité
- Token obligatoire; 401 si absent/mauvais.
- Rate limit 300/min.

Acceptance
- Par défaut, rien ne part nulle part.
- Si on active, un ping de test fonctionne et se log bien.
