import type { APIRoute } from "astro";
import { randomUUID } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";

const emailRegex = /^[\w.!#$%&'*+/=?`{|}~-]+@[\w-]+(?:\.[\w-]+)+$/i;
const STORAGE_DIR = path.join(process.cwd(), "storage");
const LEADS_FILE = path.join(STORAGE_DIR, "demo-leads.json");
const INTERNAL_FALLBACK = "demo@pipelane.app";

type DemoPayload = Record<string, string | undefined>;

interface LeadEntry {
  id: string;
  name: string;
  email: string;
  company: string;
  volume: string;
  notes?: string;
  submittedAt: string;
  ip?: string;
  meta?: Record<string, string>;
}

const parsePayload = async (request: Request): Promise<DemoPayload> => {
  const contentType = request.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    const body = await request.json();
    return typeof body === "object" && body ? (body as DemoPayload) : {};
  }

  const form = await request.formData();
  const payload: DemoPayload = {};
  for (const key of form.keys()) {
    const value = form.get(key);
    if (typeof value === "string") {
      payload[key] = value.trim();
    }
  }
  return payload;
};

const badRequest = (message: string) =>
  new Response(JSON.stringify({ ok: false, error: message }), {
    status: 400,
    headers: { "Content-Type": "application/json" }
  });

const ensureStorage = async () => {
  await mkdir(STORAGE_DIR, { recursive: true });
};

const persistLead = async (entry: LeadEntry) => {
  try {
    await ensureStorage();
    let buffer: unknown = [];
    try {
      const value = await readFile(LEADS_FILE, "utf8");
      buffer = JSON.parse(value);
      if (!Array.isArray(buffer)) {
        buffer = [];
      }
    } catch {
      buffer = [];
    }
    (buffer as LeadEntry[]).push(entry);
    await writeFile(LEADS_FILE, JSON.stringify(buffer, null, 2), "utf8");
  } catch (error) {
    console.error("[demo-request] Failed to persist lead", error);
  }
};

const sendEmail = async (message: { to: string; subject: string; text: string }) => {
  const apiKey = process.env.RESEND_API_KEY;
  const from = process.env.RESEND_FROM ?? "Pipelane Demo <demo@pipelane.app>";
  if (!apiKey) {
    console.warn("[demo-request] RESEND_API_KEY missing, email skipped");
    return false;
  }

  try {
    const response = await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${apiKey}`,
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        from,
        to: message.to,
        subject: message.subject,
        text: message.text
      })
    });

    if (!response.ok) {
      const detail = await response.text();
      console.error("[demo-request] Resend error", detail);
      return false;
    }
    return true;
  } catch (error) {
    console.error("[demo-request] Resend exception", error);
    return false;
  }
};

const notifyTeam = async (entry: LeadEntry) => {
  const to = process.env.RESEND_TO ?? INTERNAL_FALLBACK;
  const lines = [
    `Nouvelle demande de démo`,
    ``,
    `Nom       : ${entry.name}`,
    `Email     : ${entry.email}`,
    `Société   : ${entry.company}`,
    `Volume    : ${entry.volume}`,
    entry.notes ? `Notes     : ${entry.notes}` : null,
    entry.meta && Object.keys(entry.meta).length ? `Meta      : ${JSON.stringify(entry.meta)}` : null,
    entry.ip ? `IP        : ${entry.ip}` : null,
    `Horodatage : ${entry.submittedAt}`
  ].filter(Boolean);

  return sendEmail({
    to,
    subject: `Demande de démo – ${entry.company}`,
    text: lines.join("\n")
  });
};

const acknowledgeRequester = async (entry: LeadEntry) => {
  const lines = [
    `Bonjour ${entry.name.split(" ")[0] ?? ""},`,
    ``,
    `Merci pour votre demande de démo. L'équipe revient vers vous très vite (moins d'une journée ouvrée)`,
    `avec un créneau et un accès à la sandbox.`,
    ``,
    `Vous pouvez répondre directement à cet email pour ajouter des précisions.`,
    ``,
    `— L'équipe Pipelane`
  ];

  return sendEmail({
    to: entry.email,
    subject: "Merci pour votre demande de démo Pipelane",
    text: lines.join("\n")
  });
};

export const post: APIRoute = async ({ request }) => {
  const payload = await parsePayload(request);
  const name = String(payload.name ?? "").trim();
  const email = String(payload.email ?? "").trim();
  const company = String(payload.company ?? "").trim();
  const volume = String(payload.volume ?? "").trim();
  const notes = String(payload.notes ?? "").trim();

  if (!name || !email || !company || !volume) {
    return badRequest("Champs requis manquants.");
  }

  if (!emailRegex.test(email)) {
    return badRequest("Adresse email invalide.");
  }

  const meta: Record<string, string> = {};
  for (const key of ["utm_source", "utm_medium", "utm_campaign", "gclid", "fbclid"]) {
    const value = String(payload[key] ?? "").trim();
    if (value) {
      meta[key] = value;
    }
  }

  const entry: LeadEntry = {
    id: randomUUID(),
    name,
    email,
    company,
    volume,
    notes: notes || undefined,
    submittedAt: new Date().toISOString(),
    ip: request.headers.get("x-forwarded-for") ?? request.headers.get("cf-connecting-ip") ?? undefined,
    meta: Object.keys(meta).length ? meta : undefined
  };

  console.info("[demo-request] Nouvelle demande", entry);
  await persistLead(entry);

  const [teamNotified, ackSent] = await Promise.all([notifyTeam(entry), acknowledgeRequester(entry)]);

  return new Response(
    JSON.stringify({
      ok: true,
      leadId: entry.id,
      teamNotified,
      acknowledgmentSent: ackSent
    }),
    {
      status: 200,
      headers: { "Content-Type": "application/json" }
    }
  );
};

export const ALL: APIRoute = () =>
  new Response(JSON.stringify({ ok: false, error: "Méthode non autorisée" }), {
    status: 405,
    headers: { Allow: "POST", "Content-Type": "application/json" }
  });
