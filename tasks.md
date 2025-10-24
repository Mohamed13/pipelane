RÈGLES
- Pour chaque Section (API, Front, Marketing) : ouvrir les fichiers concernés, résumer les tâches, exécuter, petits commits, build/tests OK, puis /compact.
- Si une étape échoue : corriger et relancer jusqu’au succès.

SECTION API
1) Créer GET /api/followups/preview?conversationId=… qui retourne {historySnippet,lastInteractionAt,read,timezone} + une proposition IA (sans enqueue).
2) Persister les compteurs de rate-limit par tenant (table kv) + exposer /health/metrics (queueDepth, avgSendLatency, webhookErrorRate).
3) Dead-letter pour webhooks (table FailedWebhooks) + job Quartz de retry.
4) Finaliser /api/reports/summary(.pdf). Tests xUnit pour preview, limites persistantes, dead-letter.

SECTION FRONT
1) ConversationThread: brancher la carte “Prochaine relance” sur le nouvel endpoint preview ; actions Valider/Modifier/Reporter/Stop.
2) Analytics: ajouter “Top sujets/templates” (bar chart) + bouton Exporter PDF.
3) Démo: bouton “Lancer la démo” (si DEMO_MODE) → POST /api/demo/run → redirection et toasts. Tests Jest sur preview/validate & charts.

SECTION MARKETING
1) Repasser pa11y & Lighthouse, corriger jusqu’à score ≥ 95.
2) Page Prix: ajouter “1er RDV qualifié garanti ou prolongation gratuite”.
3) Form démo: POST /api/demo-request crée un Lead interne + email auto (Resend). Ajouter vidéo “Démo 2 min”.

/compact
- API ✅ préview follow-up, limites persistantes + /health/metrics, dead-letter webhooks, reports summary/pdf couverts par tests.
- Front ✅ carte follow-up branchée, analytics top messages + export PDF, bouton démo conditionnel, Jest à jour.
- Marketing ✅ pa11y + Lighthouse ≥ 95, promesse prix ajoutée, formulaire démo stock + email + vidéo 2 min.
