You are improving Pipelane’s follow-up feature. 
Goal: turn follow-ups into a smart, semi-autonomous system that proposes (and can send) the right follow-up at the right time, with a short personalized message.

Tasks:
1) UI
- Add a toggle “Relance intelligente” at campaign level and conversation level.
- Show the next follow-up card: {scheduledAt (local time), angle label, message preview, “Why this choice?”}, with actions: Validate, Edit, Snooze (choose hours/days), Stop follow-ups for this contact.
- In dashboards, add simple counters: proposed, sent, replies.

2) Planner (simple rules + AI suggestions)
- Compute a “relance score” per contact using: last activity age, read/unread, channel, local time window, best-performing hours/days (keep a small per-tenant memory).
- Pick a time within quiet hours and channel constraints (respect WhatsApp 24h window).
- Pick an angle among {reminder, value, social proof, question} based on the conversation; expose a short “Why this choice?” string.

3) Message generation (short & safe)
- Use the existing AI service to produce a 3–6 line follow-up in the conversation’s language (FR/EN), respectful tone.
- Enforce guardrails: no sensitive topics, no over-claims; include an opt-out line when email channel.
- Support A/B on subject or opening line (optional V1).

4) Safeties
- Enforce opt-out/STOP, per-tenant daily caps, quiet hours. Never send more than 1 follow-up per contact per 7 days; max 3 follow-ups per thread.
- Provide “Require approval” mode (default ON). Offer “Auto-send low-risk follow-ups” as an option.

5) Learning (light)
- Log outcomes (reply/no reply) with hour/day/angle. Prefer angles/time slots that historically performed for this tenant and similar contacts.

6) Telemetry
- Add logs: tenantId, channel, scheduledAt, chosen angle, sentAt, outcome.
- Dashboard: show sent → replies, best angle, best hour block.

Acceptance:
- On a sample campaign, the system proposes valid follow-ups with compliant timing and clear previews.
- Validation sends the follow-up and records it; snooze postpones; stop disables for that contact.
- No sends outside quiet hours or violating WhatsApp 24h rules.
