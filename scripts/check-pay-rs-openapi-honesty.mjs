#!/usr/bin/env node
/**
 * Pay OpenAPI ↔ Rust axum `.route` honesty (036/006 #35).
 *
 * Asserts:
 *   1. OpenAPI paths ⊆ axum routes in apps/lazuar-pay-rs/crates/api/src/lib.rs
 *   2. axum ⊆ OpenAPI ∪ host-only allowlist
 *
 * Path params are canonicalized (`{orgId}` / `{org_id}` → `{}`).
 *
 * Usage (after `task pay:spec`):
 *   node scripts/check-pay-rs-openapi-honesty.mjs
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const OPENAPI_PATH = path.join(ROOT, "packages/pay-spec/dist/openapi.yaml");
const ROUTER = path.join(ROOT, "apps/lazuar-pay-rs/crates/api/src/lib.rs");
const HTTP_VERBS = new Set(["get", "post", "put", "delete", "patch"]);

/** Process probes. Buyer/PSP doors and GET events live in pay-spec. */
const IMPL_ONLY = new Set(["GET /health", "GET /ready", "GET /metrics"]);

/** TypeSpec uses `/v1/webhooks/{provider}/{orgId}`; Rust names each rail. */
const SPEC_WEBHOOK_WILDCARD = "POST /v1/webhooks/{}/{}";

function normalizeRoute(p) {
  let s = String(p).trim().replace(/\/+/g, "/");
  if (!s.startsWith("/")) s = `/${s}`;
  if (s.length > 1 && s.endsWith("/")) s = s.slice(0, -1);
  return s.replace(/\{[^}]+\}/g, "{}");
}

function routeKey(method, routePath) {
  return `${method.toUpperCase()} ${normalizeRoute(routePath)}`;
}

function loadOpenApiPaths(filePath) {
  if (!fs.existsSync(filePath)) {
    console.error(
      `Missing OpenAPI file: ${path.relative(ROOT, filePath)}\n` +
        `Run 'task pay:spec' first.`,
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
  return paths;
}

function extractCall(text, start) {
  const open = text.indexOf("(", start);
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    if (text[i] === "(") depth++;
    else if (text[i] === ")") {
      depth--;
      if (depth === 0) return text.slice(start, i + 1);
    }
  }
  return text.slice(start, start + 200);
}

function loadAxumRoutes() {
  if (!fs.existsSync(ROUTER)) {
    console.error(`Missing router: ${path.relative(ROOT, ROUTER)}`);
    process.exit(1);
  }
  const text = fs.readFileSync(ROUTER, "utf8");
  const routes = new Map();
  const re = /\.route\(\s*"([^"]+)"/g;
  let m;
  while ((m = re.exec(text)) !== null) {
    const stmt = extractCall(text, m.index);
    for (const verb of HTTP_VERBS) {
      if (new RegExp(`\\b${verb}\\s*\\(`).test(stmt)) {
        const key = routeKey(verb, m[1]);
        routes.set(key, { method: verb.toUpperCase(), path: normalizeRoute(m[1]) });
      }
    }
  }
  return routes;
}

function rustCoversSpec(specKey, axum) {
  if (axum.has(specKey)) return true;
  if (specKey === SPEC_WEBHOOK_WILDCARD) {
    return [...axum.keys()].some((k) => /^POST \/v1\/webhooks\//.test(k));
  }
  return false;
}

function specCoversRust(rustKey, spec) {
  if (spec.has(rustKey)) return true;
  if (/^POST \/v1\/webhooks\//.test(rustKey) && spec.has(SPEC_WEBHOOK_WILDCARD)) {
    return true;
  }
  return false;
}

const spec = loadOpenApiPaths(OPENAPI_PATH);
const axum = loadAxumRoutes();

const extraSpec = [...spec.keys()].filter((k) => !rustCoversSpec(k, axum)).sort();
const missingSpec = [...axum.keys()]
  .filter((k) => !specCoversRust(k, spec) && !IMPL_ONLY.has(k))
  .sort();
const allowlistedButInSpec = [...IMPL_ONLY].filter((k) => spec.has(k)).sort();

const errors = [];
if (extraSpec.length) {
  errors.push("OpenAPI paths not routed on lazuar-pay-rs:");
  for (const k of extraSpec) errors.push(`  + ${k}`);
}
if (missingSpec.length) {
  errors.push("Rust axum routes missing from OpenAPI (not in host-only allowlist):");
  for (const k of missingSpec) errors.push(`  - ${k}`);
}
if (allowlistedButInSpec.length) {
  errors.push("Host-only routes should stay out of pay-spec (or drop from allowlist):");
  for (const k of allowlistedButInSpec) errors.push(`  ${k}`);
}

if (errors.length) {
  for (const line of errors) console.error(line);
  process.exit(1);
}

console.log(
  `pay-rs OpenAPI honesty: ${spec.size} spec paths, ${axum.size} axum routes, ok.`,
);
