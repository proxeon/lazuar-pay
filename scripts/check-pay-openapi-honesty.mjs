#!/usr/bin/env node
/**
 * Pay OpenAPI ↔ Minimal API path honesty (packages/pay-spec, not Hub api-spec).
 *
 * Asserts:
 *   1. OpenAPI paths ⊆ MapGet|Post|Put under apps/lazuar-pay/src
 *   2. Map* ⊆ OpenAPI ∪ host-only allowlist (unversioned /health /ready)
 *
 * Usage (after `task pay:spec` / `pnpm --filter @repo/pay-spec exec tsp compile .`):
 *   node scripts/check-pay-openapi-honesty.mjs
 *
 * Exit 0 = honest; 1 = drift or missing OpenAPI.
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const OPENAPI_PATH = path.join(ROOT, "packages/pay-spec/dist/openapi.yaml");
const SCAN_ROOT = path.join(ROOT, "apps/lazuar-pay/src");
const HTTP_VERBS = new Set(["get", "post", "put", "delete", "patch"]);

/** Unversioned process probes. Keep host-only; do not grow pay-spec for them. */
const IMPL_ONLY = new Set(["GET /health", "GET /ready"]);

function normalizeRoute(p) {
  let s = String(p).trim().replace(/\/+/g, "/");
  if (!s.startsWith("/")) s = `/${s}`;
  if (s.length > 1 && s.endsWith("/")) s = s.slice(0, -1);
  return s;
}

function routeKey(method, routePath) {
  return `${method.toUpperCase()} ${normalizeRoute(routePath)}`;
}

function walkCsFiles(dir, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) {
      if (["bin", "obj"].includes(ent.name)) continue;
      walkCsFiles(p, acc);
    } else if (ent.isFile() && ent.name.endsWith(".cs")) {
      acc.push(p);
    }
  }
  return acc;
}

function loadMapRoutes() {
  const routes = new Map();
  const mapRe = /\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]+)"/g;
  for (const file of walkCsFiles(SCAN_ROOT)) {
    const text = fs.readFileSync(file, "utf8");
    let m;
    while ((m = mapRe.exec(text)) !== null) {
      const key = routeKey(m[1], m[2]);
      routes.set(key, { method: m[1].toUpperCase(), path: normalizeRoute(m[2]), file });
    }
  }
  return routes;
}

function loadOpenApiPaths(filePath) {
  if (!fs.existsSync(filePath)) {
    console.error(
      `Missing OpenAPI file: ${path.relative(ROOT, filePath)}\n` +
        `Run 'task pay:spec' or 'pnpm --filter @repo/pay-spec exec tsp compile .' first.`,
    );
    process.exit(1);
  }
  const text = fs.readFileSync(filePath, "utf8");
  const paths = new Map();
  let inPaths = false;
  let currentPath = null;
  for (const line of text.split(/\n/)) {
    if (/^paths:\s*$/.test(line)) {
      inPaths = true;
      continue;
    }
    if (!inPaths) continue;
    if (/^[A-Za-z]/.test(line)) break;
    const pathLine = line.match(/^  (\/[^:]*):\s*$/);
    if (pathLine) {
      currentPath = pathLine[1];
      continue;
    }
    if (!currentPath) continue;
    const verbLine = line.match(/^    ([a-z]+):\s*$/i);
    if (verbLine && HTTP_VERBS.has(verbLine[1].toLowerCase())) {
      const method = verbLine[1].toUpperCase();
      paths.set(routeKey(method, currentPath), {
        method,
        path: normalizeRoute(currentPath),
      });
    }
  }
  return { paths, text };
}

function fail(lines) {
  for (const line of lines) console.error(line);
  process.exit(1);
}

const maps = loadMapRoutes();
const { paths: spec, text: yaml } = loadOpenApiPaths(OPENAPI_PATH);

const extraSpec = [...spec.keys()].filter((k) => !maps.has(k)).sort();
const missingSpec = [...maps.keys()]
  .filter((k) => !spec.has(k) && !IMPL_ONLY.has(k))
  .sort();
const allowlistedButMappedInSpec = [...IMPL_ONLY].filter((k) => spec.has(k)).sort();

const errors = [];
if (extraSpec.length) {
  errors.push("OpenAPI paths not mapped on the Pay host:");
  for (const k of extraSpec) errors.push(`  + ${k}`);
}
if (missingSpec.length) {
  errors.push("Pay Map* paths missing from OpenAPI (not in host-only allowlist):");
  for (const k of missingSpec) errors.push(`  - ${k}`);
}
if (allowlistedButMappedInSpec.length) {
  errors.push("Host-only probes should stay out of pay-spec:");
  for (const k of allowlistedButMappedInSpec) errors.push(`  ${k}`);
}

function schemaBlock(text, name) {
  const start = text.indexOf(`\n    ${name}:\n`);
  if (start < 0) return null;
  const rest = text.slice(start + 1);
  const next = rest.search(/\n    [A-Za-z]/);
  return next < 0 ? rest : rest.slice(0, next);
}

function pascalToSnake(name) {
  return name
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1_$2")
    .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
    .toLowerCase();
}

function csharpPublicProperties(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const names = [];
  const re = /public\s+(?:required\s+)?[\w.?<>]+\s+(\w+)\s*\{\s*get;/g;
  let m;
  while ((m = re.exec(text)) !== null) names.push(m[1]);
  return names;
}

function openApiSchemaProperties(yamlText, schemaName) {
  const block = schemaBlock(yamlText, schemaName);
  if (!block) return null;
  const names = new Set();
  for (const line of block.split(/\n/)) {
    const m = line.match(/^        ([a-z][a-z0-9_]*):\s*$/);
    if (m) names.add(m[1]);
  }
  return names;
}

/** TypeSpec schema name → C# DTO file (relative to apps/lazuar-pay/src). */
const DTO_MAP = [
  ["CreateCheckoutRequest", "Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs"],
  ["CreatePaymentLinkRequest", "Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs"],
  ["CreateProductRequest", "Lazuar.Pay/Catalog/CatalogEndpoints.cs"],
  ["StartPayRequest", "Lazuar.Pay/PublicPay/PublicPayEndpoints.cs"],
  ["ConfirmPayRequest", "Lazuar.Pay/PublicPay/PublicPayEndpoints.cs"],
  ["CreateRefundRequest", "Lazuar.Pay/Money/RefundEndpoints.cs"],
  ["PaymentLink", "Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs"],
  ["WhoamiResponse", "Lazuar.Pay/Identity/WhoamiResponse.cs"],
];

const csharpRoot = path.join(ROOT, "apps/lazuar-pay/src");
for (const [schema, rel] of DTO_MAP) {
  const specProps = openApiSchemaProperties(yaml, schema);
  if (!specProps) {
    errors.push(`OpenAPI missing schema ${schema}`);
    continue;
  }
  const csPath = path.join(csharpRoot, rel);
  if (!fs.existsSync(csPath)) {
    errors.push(`C# DTO missing for ${schema}: ${rel}`);
    continue;
  }
  const csProps = csharpPublicProperties(csPath)
    .filter((n) => {
      if (schema === "PaymentLink") return ["Id", "OrgId", "Provider", "Amount", "Currency", "Status", "PublicToken", "PayUrl", "CreatedAt", "MaxPayers", "Unlimited", "PaidCount", "TakenCount", "Remaining", "Label"].includes(n);
      if (schema === "WhoamiResponse") return ["UserId", "Email", "Name", "IsPlatformAdmin", "ActiveOrgId", "Tenants"].includes(n);
      if (schema === "StartPayRequest") return ["Name", "Email", "SlotKey"].includes(n);
      if (schema === "ConfirmPayRequest") return ["Signature"].includes(n);
      if (schema === "CreateProductRequest") return ["Name", "Description", "Amount", "Currency", "Interval"].includes(n);
      return true;
    })
    .map(pascalToSnake);
  const missing = csProps.filter((p) => !specProps.has(p));
  if (missing.length) {
    errors.push(`OpenAPI ${schema} missing C# fields: ${missing.join(", ")}`);
  }
}

const fieldChecks = [
  [schemaBlock(yaml, "CreateCheckoutRequest")?.includes("        provider:"), "CreateCheckoutRequest.provider"],
  [schemaBlock(yaml, "StartPayRequest")?.includes("        slot_key:"), "StartPayRequest.slot_key"],
  [schemaBlock(yaml, "WhoamiResponse")?.includes("        name:"), "WhoamiResponse.name"],
  [schemaBlock(yaml, "CreateProductRequest") != null, "CreateProductRequest"],
  [schemaBlock(yaml, "WebhookDuplicate")?.includes("        duplicate:"), "WebhookDuplicate.duplicate"],
  [schemaBlock(yaml, "WebhookIgnored")?.includes("        ignored:"), "WebhookIgnored.ignored"],
  [yaml.includes("'201':"), "201 mint status"],
];
for (const [ok, label] of fieldChecks) {
  if (!ok) {
    errors.push(`OpenAPI missing live field/status: ${label}`);
  }
}

if (errors.length) {
  fail(["Pay OpenAPI ↔ Map* honesty failed.", "", ...errors]);
}

console.log(
  `Pay OpenAPI honesty: ${spec.size} spec ops, ${maps.size} Map* (${IMPL_ONLY.size} host-only probes).`,
);
