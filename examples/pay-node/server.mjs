import { createHmac, timingSafeEqual } from "node:crypto";
import { createServer } from "node:http";

const payApi = (process.env.PAY_API_URL ?? "http://localhost:8081").replace(/\/$/, "");
const orgId = process.env.PAY_ORG_ID ?? "";
const apiKey = process.env.PAY_API_KEY ?? "";
const webhookSecret = process.env.PAY_WEBHOOK_SECRET ?? "";
const mintToken = process.env.PAY_MINT_TOKEN ?? "";
const port = Number(process.env.PORT ?? 3021);
const unlocked = new Map();

function verify(raw, signatureHeader, timestampHeader) {
  const v1 = String(signatureHeader ?? "")
    .split(",")
    .map((p) => p.trim())
    .find((p) => p.startsWith("v1="))
    ?.slice(3);
  const ts = String(timestampHeader ?? "").trim();
  if (!v1 || !ts || !webhookSecret) return false;
  const tsNum = Number(ts);
  if (!Number.isFinite(tsNum) || Math.abs(Date.now() / 1000 - tsNum) > 300) return false;
  const expected = createHmac("sha256", webhookSecret).update(`${ts}.${raw}`).digest("hex");
  const a = Buffer.from(v1, "utf8");
  const b = Buffer.from(expected, "utf8");
  return a.length === b.length && timingSafeEqual(a, b);
}

const server = createServer(async (req, res) => {
  try {
    const url = new URL(req.url ?? "/", `http://127.0.0.1:${port}`);
    if (req.method === "POST" && url.pathname === "/hook") {
      const chunks = [];
      for await (const c of req) chunks.push(c);
      const raw = Buffer.concat(chunks).toString("utf8");
      if (!verify(raw, req.headers["x-lazuar-signature"], req.headers["x-lazuar-timestamp"])) {
        res.writeHead(401).end("invalid hmac");
        return;
      }
      let body;
      try {
        body = JSON.parse(raw);
      } catch {
        res.writeHead(400).end("invalid json");
        return;
      }
      if (body.type === "payment.completed" && body.data?.checkout_id) {
        unlocked.set(body.data.checkout_id, true);
      }
      res.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify({ ok: true }));
      return;
    }
    if (req.method === "GET" && url.pathname.startsWith("/unlocked/")) {
      const id = url.pathname.slice("/unlocked/".length);
      res.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify({ unlocked: unlocked.get(id) === true }));
      return;
    }
    if (req.method === "POST" && url.pathname === "/mint") {
      if (mintToken && req.headers["x-pay-mint-token"] !== mintToken) {
        res.writeHead(401).end("unauthorized");
        return;
      }
      const created = await fetch(`${payApi}/v1/checkouts`, {
        method: "POST",
        headers: {
          authorization: `Bearer ${apiKey}`,
          "content-type": "application/json",
        },
        body: JSON.stringify({
          org_id: orgId,
          amount: 10,
          currency: process.env.PAY_CURRENCY ?? (process.env.PAY_PROVIDER === "solana" ? "USDC" : "MYR"),
          provider: process.env.PAY_PROVIDER ?? "test",
        }),
      });
      const json = await created.json();
      res.writeHead(created.status, { "content-type": "application/json" }).end(JSON.stringify(json));
      return;
    }
    res.writeHead(200, { "content-type": "text/plain" }).end("pay-node: POST /mint, POST /hook, GET /unlocked/:checkoutId\n");
  } catch {
    res.writeHead(500).end("error");
  }
});

server.listen(port, "127.0.0.1", () => {
  console.log(`pay-node ${port} org=${orgId || "(set PAY_ORG_ID)"}`);
});
