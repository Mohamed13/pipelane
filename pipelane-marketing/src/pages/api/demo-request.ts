import type { APIRoute } from "astro";

const emailRegex = /^[\w.!#$%&'*+/=?`{|}~-]+@[\w-]+(?:\.[\w-]+)+$/i;

const parsePayload = async (request: Request) => {
  const contentType = request.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    const body = await request.json();
    return typeof body === "object" && body ? body : {};
  }

  const form = await request.formData();
  const payload: Record<string, string> = {};
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

  const entry = {
    name,
    email,
    company,
    volume,
    notes,
    submittedAt: new Date().toISOString(),
    ip: request.headers.get("x-forwarded-for") ?? request.headers.get("cf-connecting-ip") ?? undefined
  };

  console.info("[demo-request] Nouvelle demande", entry);

  return new Response(JSON.stringify({ ok: true }), {
    status: 200,
    headers: { "Content-Type": "application/json" }
  });
};

export const all: APIRoute = () =>
  new Response(JSON.stringify({ ok: false, error: "Méthode non autorisée" }), {
    status: 405,
    headers: { "Allow": "POST", "Content-Type": "application/json" }
  });
