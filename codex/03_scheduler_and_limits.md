Ouvre ce fichier, résume, puis implémente un planificateur simple et des garde-fous.

Objectif
Envoyer de façon régulière et sûre:
- Limite journalière par tenant (ex: 100 messages/jour, configurable).
- Quiet hours (22:00–08:00 locale du contact): on décale à 10:30.
- WhatsApp: respecter fenêtre 24h (si hors fenêtre → basculer en "template" ou bloquer).
- STOP/opt-out SMS/email: ne jamais envoyer si contact opt-out.

Tâches
1) Config par tenant: DAILY_SEND_CAP=100, QUIET_START=22:00, QUIET_END=08:00.
2) Quartz job "SendDueMessages":
   - Cherche les envois “due” (à envoyer maintenant) par ordre FIFO, sans dépasser DAILY_SEND_CAP.
   - Si l’horaire tombe dans quiet hours → décale à 10:30 locale.
   - Marque “Sent” si provider OK, sinon “Failed” + erreur.
3) Règle WhatsApp 24h:
   - Si lastInboundAt > 24h et message non-template → bloquer et journaliser.
4) STOP/Opt-out:
   - Si contact.optedOutEmail/Sms → bloquer (409).
5) Compteurs & logs:
   - Incrémenter compteur du jour.
   - Log structuré: tenantId, contactId, channel, scheduledAt, sentAt, status.

Tests
- 1: cap 100/jour respecté.
- 2: envoi planifié 23:00 → décale à 10:30.
- 3: WhatsApp texte hors 24h est bloqué.
- 4: opt-out bloque l’envoi.
