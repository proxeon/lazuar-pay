#!/usr/bin/env node
// Honesty: every C# public path we care about is a match arm in apps/lazuar-api/src/app.rs.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const app = readFileSync(join(root, "apps/lazuar-api/src/app.rs"), "utf8");
const required = [
  '["health"]',
  '["v1", "health"]',
  '["ready"]',
  '["v1", "whoami"]',
  '["v1", "webhooks"',
  '["v1", "orgs", org_id, "refunds"]',
  '["v1", "payment-links"]',
  '["v1", "orgs", org_id, "payment-links"]',
  '["v1", "pay", token]',
  '["v1", "pay", token, "start"]',
  '["v1", "pay", token, "confirm"]',
  '["v1", "orgs", org_id, "ready"]',
  '["v1", "checkouts"]',
  '["v1", "checkouts", id]',
  '["v1", "orgs", org_id, "checkouts"]',
  '["v1", "orgs", org_id, "products"]',
  '["v1", "orgs", org_id, "gateway"]',
  '["v1", "orgs", org_id, "gateways"]',
  '["v1", "orgs", org_id, "payments"]',
  '["v1", "orgs", org_id, "receipts"]',
  '["v1", "orgs", org_id, "receipts", id]',
  '["v1", "orgs", org_id, "subscriptions"]',
  '["v1", "one", "webhooks"]',
  '["v1", "orgs", org_id, "one-webhook"]',
  '["v1", "orgs", org_id, "webhooks"]',
  '["v1", "orgs", org_id, "webhooks", "rotate"]',
  '["v1", "orgs", org_id, "webhooks", "test"]',
];
const missing = required.filter((p) => !app.includes(p));
if (app.includes('["v1", "public", "pay"')) {
  console.error("buyer surface still has /v1/public/pay — adopt C# /v1/pay paths");
  process.exit(1);
}
if (missing.length) {
  console.error("Rust router missing C# paths:\n" + missing.map((m) => "  " + m).join("\n"));
  process.exit(1);
}
console.log(`ok: ${required.length} C# paths present in app.rs router`);
