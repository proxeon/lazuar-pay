#!/usr/bin/env node
/**
 * OpenAPI ↔ Minimal API path honesty gate (R25 / FW-6).
 *
 * Asserts:
 *   1. OpenAPI paths ⊆ Minimal API maps ∪ openapi_only_exceptions
 *   2. Minimal API maps ⊆ OpenAPI ∪ impl_only allowlist
 *
 * Inputs:
 *   - packages/api-spec/dist/openapi.yaml   (after `task gen` / `task gen:spec`)
 *   - packages/api-spec/honesty-allowlist.yaml
 *   - Static scrape of MapGroup/MapGet|Post|Put|Delete|Patch under apps/lazuar-api
 *
 * Paths are compared relative to /api/v1 (TypeSpec @route trees + openapi-fetch baseUrl).
 * Host infrastructure routes outside /api/v1 (e.g. /health) are not in scope.
 *
 * Usage:
 *   node scripts/check-openapi-minimal-honesty.mjs
 *   node scripts/check-openapi-minimal-honesty.mjs --verbose
 *
 * Exit 0 = honest; 1 = drift or missing inputs.
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");

const OPENAPI_PATH = path.join(ROOT, "packages/api-spec/dist/openapi.yaml");
const ALLOWLIST_PATH = path.join(ROOT, "packages/api-spec/honesty-allowlist.yaml");
const SCAN_ROOTS = [
  path.join(ROOT, "apps/lazuar-api/Modules"),
  path.join(ROOT, "apps/lazuar-api/src/Lazuar.Api/Composition"),
];

const HTTP_VERBS = new Set(["get", "post", "put", "delete", "patch", "head", "options"]);
const VERBOSE = process.argv.includes("--verbose");

// ─── path helpers ───────────────────────────────────────────────────────────

function stripConstraints(p) {
  return p.replace(/\{([^}:]+):[^}]+\}/g, "{$1}");
}

function joinRoute(base, segment) {
  if (segment == null || segment === "") return normalizeRoute(base ?? "");
  if (base == null || base === "") return normalizeRoute(segment);
  const a = String(base).replace(/\/+$/, "");
  const b = String(segment).replace(/^\/+/, "");
  return normalizeRoute(`${a}/${b}`);
}

function normalizeRoute(p) {
  if (!p) return "";
  let s = stripConstraints(String(p).trim());
  // Host maps under /api/v1/... → relative product path
  if (s === "/api/v1" || s.startsWith("/api/v1/")) {
    s = s.slice("/api/v1".length) || "";
  }
  s = s.replace(/\/+/g, "/");
  if (!s.startsWith("/") && s !== "") s = `/${s}`;
  if (s.length > 1 && s.endsWith("/")) s = s.slice(0, -1);
  return s;
}

function routeKey(method, routePath) {
  return `${method.toUpperCase()} ${normalizeRoute(routePath)}`;
}

// ─── minimal YAML (allowlist only) ──────────────────────────────────────────

/**
 * Parse the honesty-allowlist.yaml shape without a YAML dependency.
 * Supports: top-level keys, list items with method/path/reason (reason may be `>` folded).
 */
function parseAllowlistYaml(text) {
  const result = { impl_only: [], openapi_only_exceptions: [] };
  let section = null;
  let current = null;
  let inFolded = false;
  let foldedLines = [];

  const flushFolded = () => {
    if (current && inFolded) {
      current.reason = foldedLines.join(" ").replace(/\s+/g, " ").trim();
      inFolded = false;
      foldedLines = [];
    }
  };

  for (const raw of text.split(/\n/)) {
    const line = raw.replace(/\t/g, "  ");
    if (/^\s*#/.test(line) && !inFolded) continue;
    if (line.trim() === "" && !inFolded) continue;

    if (inFolded) {
      // folded block continues while indented more than list item fields
      const m = line.match(/^(\s+)(\S.*)$/);
      if (m && m[1].length >= 4) {
        foldedLines.push(m[2].trim());
        continue;
      }
      flushFolded();
      // fall through to re-process this line
    }

    if (/^impl_only:\s*$/.test(line)) {
      section = "impl_only";
      current = null;
      continue;
    }
    if (/^openapi_only_exceptions:\s*\[\s*\]\s*$/.test(line)) {
      section = "openapi_only_exceptions";
      continue;
    }
    if (/^openapi_only_exceptions:\s*$/.test(line)) {
      section = "openapi_only_exceptions";
      continue;
    }
    if (/^[a-zA-Z_][\w]*:/.test(line) && !/^\s/.test(line)) {
      // unknown top-level — ignore
      section = null;
      current = null;
      continue;
    }

    if (!section) continue;

    const itemStart = line.match(/^\s*-\s+method:\s*(\w+)\s*$/i);
    if (itemStart) {
      current = { method: itemStart[1].toUpperCase(), path: "", reason: "" };
      result[section].push(current);
      continue;
    }

    if (!current) continue;

    const pathMatch = line.match(/^\s+path:\s*(.+)\s*$/);
    if (pathMatch) {
      current.path = pathMatch[1].trim().replace(/^["']|["']$/g, "");
      continue;
    }

    const reasonFolded = line.match(/^\s+reason:\s*>\s*$/);
    if (reasonFolded) {
      inFolded = true;
      foldedLines = [];
      continue;
    }

    const reasonInline = line.match(/^\s+reason:\s*(.+)\s*$/);
    if (reasonInline) {
      current.reason = reasonInline[1].trim().replace(/^["']|["']$/g, "");
      continue;
    }
  }
  flushFolded();
  return result;
}

// ─── OpenAPI paths ──────────────────────────────────────────────────────────

function loadOpenApiPaths(filePath) {
  if (!fs.existsSync(filePath)) {
    console.error(
      `Missing OpenAPI file: ${path.relative(ROOT, filePath)}\n` +
        `Run 'task gen' or 'task gen:spec' first.`,
    );
    process.exit(1);
  }
  const text = fs.readFileSync(filePath, "utf8");
  const paths = new Map(); // key -> { method, path }
  let inPaths = false;
  let currentPath = null;

  for (const line of text.split(/\n/)) {
    if (/^paths:\s*$/.test(line)) {
      inPaths = true;
      continue;
    }
    if (!inPaths) continue;
    // next top-level key ends paths
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
      const key = routeKey(method, currentPath);
      paths.set(key, { method, path: normalizeRoute(currentPath) });
    }
  }
  return paths;
}

// ─── filesystem walk ────────────────────────────────────────────────────────

function walkCsFiles(dir, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) {
      if (["bin", "obj", "Migrations", "tests", "TestResults"].includes(ent.name)) continue;
      walkCsFiles(p, acc);
    } else if (
      ent.isFile() &&
      ent.name.endsWith(".cs") &&
      !ent.name.endsWith(".Designer.cs")
    ) {
      acc.push(p);
    }
  }
  return acc;
}

// ─── C# endpoint method scrape ──────────────────────────────────────────────

/**
 * @typedef {{
 *   name: string,
 *   thisType: 'RouteGroupBuilder'|'IEndpointRouteBuilder'|'WebApplication',
 *   thisName: string,
 *   file: string,
 *   routes: Array<{ method: string, pathExpr: string }>,
 *   groups: Array<{ dest: string, src: string, prefix: string }>,
 *   calls: Array<{ receiver: string, method: string }>,
 * }} EndpointMethod
 */

function extractMethodBodies(text) {
  /** @type {Array<{name:string,thisType:string,thisName:string,body:string,index:number}>} */
  const methods = [];
  const sigRe =
    /public\s+static\s+\w+\s+(Map\w+)\s*\(\s*this\s+(RouteGroupBuilder|IEndpointRouteBuilder|WebApplication)\s+(\w+)/g;
  let m;
  while ((m = sigRe.exec(text)) !== null) {
    const name = m[1];
    const thisType = m[2];
    const thisName = m[3];
    const braceStart = text.indexOf("{", m.index + m[0].length);
    if (braceStart < 0) continue;
    let depth = 0;
    let i = braceStart;
    for (; i < text.length; i++) {
      const ch = text[i];
      if (ch === "{") depth++;
      else if (ch === "}") {
        depth--;
        if (depth === 0) {
          i++;
          break;
        }
      }
    }
    const body = text.slice(braceStart, i);
    methods.push({ name, thisType, thisName, body, index: m.index });
  }
  return methods;
}

/**
 * Parse a method body for MapGroup / Map* / Map*Endpoints calls.
 * @returns {Omit<EndpointMethod,'file'>}
 */
function parseMethodBody(name, thisType, thisName, body) {
  const groups = [];
  const routes = [];
  const calls = [];

  // var x = y.MapGroup("prefix")  OR  x = y.MapGroup("prefix")
  const groupRe =
    /(?:(?:var|RouteGroupBuilder)\s+)?(\w+)\s*=\s*(\w+)\.MapGroup\(\s*"([^"]*)"\s*\)/g;
  let gm;
  while ((gm = groupRe.exec(body)) !== null) {
    groups.push({ dest: gm[1], src: gm[2], prefix: gm[3] });
  }

  // receiver.MapGet|Post|Put|Delete|Patch("path"
  const mapRe = /(\w+)\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]*)"/g;
  let mm;
  while ((mm = mapRe.exec(body)) !== null) {
    routes.push({
      method: mm[2].toUpperCase(),
      receiver: mm[1],
      pathExpr: mm[3],
    });
  }

  // receiver.MapSomethingEndpoints(
  const callRe = /(\w+)\.(Map\w+)\s*\(/g;
  let cm;
  while ((cm = callRe.exec(body)) !== null) {
    const recv = cm[1];
    const meth = cm[2];
    // skip MapGroup / MapGet etc already handled
    if (/^Map(Get|Post|Put|Delete|Patch|Group|Methods)$/.test(meth)) continue;
    calls.push({ receiver: recv, method: meth });
  }

  return { name, thisType, thisName, routes, groups, calls };
}

function loadEndpointMethods() {
  const files = SCAN_ROOTS.flatMap((r) => walkCsFiles(r));
  /** @type {Map<string, EndpointMethod[]>} */
  const byName = new Map();

  for (const file of files) {
    const text = fs.readFileSync(file, "utf8");
    const rel = path.relative(ROOT, file);
    for (const raw of extractMethodBodies(text)) {
      const parsed = parseMethodBody(raw.name, raw.thisType, raw.thisName, raw.body);
      /** @type {EndpointMethod} */
      const def = { ...parsed, file: rel };
      if (!byName.has(def.name)) byName.set(def.name, []);
      byName.get(def.name).push(def);
    }
  }
  return byName;
}

/**
 * Resolve all concrete routes reachable from MapAllModuleEndpoints (and peers).
 * @param {Map<string, EndpointMethod[]>} byName
 */
function resolveMinimalRoutes(byName) {
  /** @type {Map<string, { method: string, path: string, source: string }>} */
  const out = new Map();
  const visiting = new Set();

  /**
   * @param {string} methodName
   * @param {string} thisPrefix  concrete prefix for `this` parameter (relative to /api/v1)
   * @param {string} callChain
   */
  function invoke(methodName, thisPrefix, callChain) {
    const defs = byName.get(methodName);
    if (!defs || defs.length === 0) {
      if (VERBOSE) console.warn(`  (no definition for ${methodName})`);
      return;
    }
    // Prefer Infrastructure module definitions over tests (tests already excluded)
    for (const def of defs) {
      const visitKey = `${def.file}::${def.name}::${thisPrefix}`;
      if (visiting.has(visitKey)) continue;
      visiting.add(visitKey);

      /** @type {Map<string, string>} */
      const varPrefix = new Map();
      varPrefix.set(def.thisName, normalizeRoute(thisPrefix));

      // Apply MapGroup assignments in source order (may chain)
      // Iterate until stable for multi-hop groups in same method
      let changed = true;
      let guard = 0;
      while (changed && guard++ < 20) {
        changed = false;
        for (const g of def.groups) {
          if (!varPrefix.has(g.src)) continue;
          const next = joinRoute(varPrefix.get(g.src), g.prefix);
          if (varPrefix.get(g.dest) !== next) {
            varPrefix.set(g.dest, next);
            changed = true;
          }
        }
      }

      for (const r of def.routes) {
        const base = varPrefix.get(r.receiver);
        if (base === undefined) {
          if (VERBOSE) {
            console.warn(
              `  unresolved receiver '${r.receiver}' in ${def.name} (${def.file})`,
            );
          }
          continue;
        }
        const full = joinRoute(base, r.pathExpr);
        const key = routeKey(r.method, full);
        if (!out.has(key)) {
          out.set(key, {
            method: r.method,
            path: normalizeRoute(full),
            source: `${def.file} via ${callChain}`,
          });
        }
      }

      for (const c of def.calls) {
        const base = varPrefix.get(c.receiver);
        if (base === undefined) {
          // IEndpointRouteBuilder calls sometimes use `endpoints` / `app` already in map
          if (VERBOSE) {
            console.warn(
              `  unresolved call receiver '${c.receiver}.${c.method}' in ${def.name} (${def.file})`,
            );
          }
          continue;
        }
        invoke(c.method, base, `${callChain}→${c.method}`);
      }

      visiting.delete(visitKey);
    }
  }

  // Entry: MapAllModuleEndpoints
  const roots = byName.get("MapAllModuleEndpoints");
  if (!roots || roots.length === 0) {
    console.error(
      "Could not find MapAllModuleEndpoints — scrape roots may be wrong.",
    );
    process.exit(1);
  }
  invoke("MapAllModuleEndpoints", "", "MapAllModuleEndpoints");

  // Health endpoints are host-level (not under /api/v1) — intentionally omitted.
  return out;
}

// ─── compare ────────────────────────────────────────────────────────────────

function main() {
  const allowlist = parseAllowlistYaml(fs.readFileSync(ALLOWLIST_PATH, "utf8"));
  const openapi = loadOpenApiPaths(OPENAPI_PATH);
  const byName = loadEndpointMethods();
  const minimal = resolveMinimalRoutes(byName);

  /** @type {Set<string>} */
  const implOnly = new Set();
  for (const row of allowlist.impl_only) {
    if (!row.method || !row.path) {
      console.error("Invalid impl_only row (need method + path):", row);
      process.exit(1);
    }
    implOnly.add(routeKey(row.method, row.path));
  }

  /** @type {Set<string>} */
  const openapiOnlyEx = new Set();
  for (const row of allowlist.openapi_only_exceptions) {
    openapiOnlyEx.add(routeKey(row.method, row.path));
  }

  const phantoms = []; // in OpenAPI, not in Minimal, not exempted
  const undocumented = []; // in Minimal, not in OpenAPI, not allowlisted
  const allowlistUnused = [];

  for (const [key, info] of openapi) {
    if (minimal.has(key)) continue;
    if (openapiOnlyEx.has(key)) continue;
    phantoms.push(info);
  }

  for (const [key, info] of minimal) {
    if (openapi.has(key)) continue;
    if (implOnly.has(key)) continue;
    undocumented.push(info);
  }

  for (const key of implOnly) {
    if (!minimal.has(key)) allowlistUnused.push(key);
  }

  if (VERBOSE) {
    console.log(`OpenAPI operations:  ${openapi.size}`);
    console.log(`Minimal operations:  ${minimal.size}`);
    console.log(`impl_only allowlist: ${implOnly.size}`);
    console.log(`openapi_only_ex:     ${openapiOnlyEx.size}`);
  }

  let failed = false;

  if (phantoms.length) {
    failed = true;
    console.error("\nPHANTOM (in OpenAPI, not in Minimal, not exempted):");
    for (const p of phantoms.sort((a, b) =>
      `${a.method} ${a.path}`.localeCompare(`${b.method} ${b.path}`),
    )) {
      console.error(`  ${p.method} ${p.path}`);
    }
  }

  if (undocumented.length) {
    failed = true;
    console.error(
      "\nUNDOCUMENTED (in Minimal, not in OpenAPI, not allowlisted):",
    );
    for (const u of undocumented.sort((a, b) =>
      `${a.method} ${a.path}`.localeCompare(`${b.method} ${b.path}`),
    )) {
      console.error(`  ${u.method} ${u.path}`);
      if (VERBOSE) console.error(`    ← ${u.source}`);
    }
  }

  if (allowlistUnused.length) {
    // Soft warning: stale allowlist rows (do not fail CI — optional strict later)
    console.warn("\nALLOWLIST STALE (impl_only path not found in Minimal scrape):");
    for (const k of allowlistUnused.sort()) {
      console.warn(`  ${k}`);
    }
  }

  if (failed) {
    console.error(`\nAllowlist: ${path.relative(ROOT, ALLOWLIST_PATH)}`);
    console.error(
      "Fix: add missing TypeSpec routes + task gen, implement the Map, or add an allowlist row with reason.",
    );
    console.error("Doc: docs/contracts/openapi-vs-minimal-api.md");
    process.exit(1);
  }

  console.log(
    `OpenAPI ↔ Minimal path honesty OK (${openapi.size} OpenAPI, ${minimal.size} Minimal, ${implOnly.size} impl_only).`,
  );
}

main();
