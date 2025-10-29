Ouvre ce fichier, résume toutes les étapes, puis exécute-les une par une.

Objectif simple
Mettre en place 3 endpoints IA dans pipelane-api (ASP.NET 8) :
1) Générer un message court et personnalisé.
2) Classer une réponse reçue (intéressé, plus tard, etc.).
3) Proposer une relance intelligente : quand relancer, avec quel angle, et un message de 3–6 lignes.

Tâches
1) Configuration
- Ajouter variables d’environnement (fichier .env.example et appsettings.json):
  OPEN_AI_KEY=
  OPENAI_MODEL=text-davinci-003-ou-GPT-actuel (placeholder, configurable)
  AI_DAILY_BUDGET_EUR=5
- Lire ces valeurs via IOptions. Si absent, renvoyer 503 sur les endpoints IA.
- Ajouter un RateLimit simple: max 120 requêtes IA / heure / tenant.

2) Client IA
- Créer ITextAiService avec 3 méthodes:
  - GenerateMessageAsync(input)
  - ClassifyReplyAsync(input)
  - SuggestFollowupAsync(input)
- Implémenter TextAiService (HTTP client vers OpenAI-compta):
  - Timeout 20s, retry simple x2.
  - Journaliser: tenantId, type requête, tokens estimés (si dispo), durée.

3) Endpoints
- POST /api/ai/generate-message
  Body: { contactId?, language?, channel, context: { firstName?, lastName?, company?, role?, painPoints?, pitch, calendlyUrl?, lastMessageSnippet? } }
  Retour: { subject?, text, html?, languageDetected }
- POST /api/ai/classify-reply
  Body: { text, language? }
  Retour JSON strict: { intent: "Interested|Maybe|NotNow|NotRelevant|OOO|AutoReply", confidence: 0..1 }
- POST /api/ai/suggest-followup
  Body: { channel, timezone, lastInteractionAt, read: bool, language?, historySnippet?, performanceHints?: { goodHours?: [10,11,14], badDays?: ["Fri"] } }
  Retour: { scheduledAtIso, angle: "reminder|value|social|question", previewText }

4) Garde-fous
- Toujours limiter la longueur des messages (max ~120 mots).
- Ton professionnel, respectueux, pas de sujets sensibles.
- Vérifier STOP/opt-out (si channel email/sms, refuser l’envoi et renvoyer 409).
- Respect heures: pas d’IA qui suggère une heure hors quiet hours (22h–8h locale). Si nécessaire, arrondir à 10:30.

5) Tests
- Unitaires pour: validation input, mapping outputs (intent), respect quiet hours.
- Un test d’intégration “happy path”: simulate generate → classify → suggest.

Acceptance
- Les 3 endpoints répondent en < 2 s avec données de test.
- Pas d’appel si OPEN_AI_KEY (ou legacy OPENAI_API_KEY) absent (503).
- Rate limit fonctionne (429 si dépassé).
