---
number: "330"
id: B10-X28
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 330 — B10-X28 — Honesty / docs residuals after `cbe17c2`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X28 — P2 — Honesty / docs residuals after `cbe17c2`

- Scraper unresolved `MapPreview` / `MapReason` (noise).
- `docs/contracts/openapi-vs-minimal-api.md` §“Intentional frontend dark matter” still says ops invoicing / BillingProfile are **unrouted** (ADR 023). Ops `App.tsx` routes them. The contracts doc is a second SSoT that 023-erased itself.
- Combined spec now has M2M commerce (fixed). Product-scoped Scalar already had it. Clients committed in `cbe17c2` grew ~2k lines.
- Superadmin `/platform/*` TypeSpec still thin (doc residual; not a new bug).
- `CommerceWebhookEnvelope.event_type` union is still the five subscription names; cannot describe `order.completed` / `payment_link.paid`. Schema island.

## Evaluation (current tree, 2026-08-18)

### What the bug is
After `cbe17c2` the machine honesty gate is green (OpenAPI ⊆ Minimal ∪ allowlist). What remains is human SSoT drift: the contracts doc still says ops invoicing / BillingProfile are unrouted (ADR 023); the honesty scraper still warns on `MapPreview` / `MapReason` as if they were HTTP maps; Superadmin `/platform/*` TypeSpec is still auth + payment-config only; `CommerceWebhookEnvelope.event_type` is still the five subscription names while `docs-commerce.tsp` tells integrators they also get `order.completed` / `payment_link.paid`. Combined spec M2M commerce is already fixed — do not re-do that.

### Still present?
**DOCS / HONESTY ONLY**

Stale dark-matter paragraph is still the contracts doc:

```73:82:docs/contracts/openapi-vs-minimal-api.md
## Intentional frontend “dark matter” (not deleted)
...
| `lazuar-ops` invoicing module (quotes / tax invoices / credit notes) | Code present; **no routes** in `App.tsx` | Uncomment `[MVP-HIDE]` routes + sidebar (Phase D.3) |
| `lazuar-ops` `BillingProfilePage` | Unrouted | Same |
| `lazuar-ops` Ops chat (`OpsChatWorkspace`, stream client) | Unrouted; API + OpenAPI exist | Mount `/ops/chat` when productizing |
```

Ops `App.tsx` **does** mount those pages today:

```296:308:apps/lazuar-ops/src/App.tsx
        <Route path="/workspace/billing-profile" element={<BillingProfilePage />} />
        ...
        <Route path="/invoicing/quotes" element={<QuotesPage />} />
        <Route path="/invoicing/tax-invoices" element={<TaxInvoicesPage />} />
        <Route path="/invoicing/credit-notes" element={<CreditNotesPage />} />

        {/* [MVP-HIDE] ADR 023 — ops chat remains disconnected
        <Route path="/ops/chat" element={<OpsChatWorkspace />} />
        */}
```

Chat is the only remaining unrouted island (issue 321). The contracts doc is a second SSoT that 023-erased itself.

Scraper noise: `scripts/check-openapi-minimal-honesty.mjs` 397–405 still `console.warn`s `unresolved call receiver '${c.receiver}.${c.method}'` when a `Map*` helper is not an HTTP map. `SubscriberEndpoints.MapPreview` (316) and `ResendWebhookParser.MapReason` are those collisions. Soft, `--verbose` only, exit stays 0.

Envelope island:

```8:26:packages/api-spec/modules/commerce/models/webhooks.tsp
union CommerceSubscriptionEventType {
  "subscription.activated",
  "subscription.resumed",
  "subscription.past_due",
  "subscription.canceled",
  "subscription.suspended",
}
...
model CommerceWebhookEnvelope {
  event_type: CommerceSubscriptionEventType;
  data: SubscriptionWebhookData;
}
```

`packages/api-spec/docs-commerce.tsp` 26–27 still says “plus order.completed and payment_link.paid.” The generated client cannot describe those two on this model. Frozen P09: do **not** add `subscription.updated`.

Superadmin TypeSpec is still thin: `packages/api-spec/modules/platform/routes.tsp` is login/logout/me + GET/PUT `/platform/payment-config` (11–46). Residual note in the contracts doc (96) is accurate.

M2M commerce in combined spec: already closed by `cbe17c2` / `main.tsp` import of `integration-routes.tsp`. Not a new bug.

### Related files
- `docs/contracts/openapi-vs-minimal-api.md` — the lying dark-matter table.
- `apps/lazuar-ops/src/App.tsx` — live route map.
- `scripts/check-openapi-minimal-honesty.mjs` — `MapPreview`/`MapReason` warnings.
- `packages/api-spec/modules/commerce/models/webhooks.tsp` and `docs-commerce.tsp` — envelope vs prose.
- `packages/api-spec/modules/platform/routes.tsp` — thin superadmin surface.
- `packages/api-spec/honesty-allowlist.yaml` — machine allowlist (8 impl_only; not this issue).
- Issues 173 (TRIALING catalog, resolved), 321 (chat hide), 326 (buttons on those now-routed pages).

### Tests
- Existing tests that touch this path: CI `contracts` job runs `node scripts/check-openapi-minimal-honesty.mjs` (path honesty only). No test reads the markdown table against `App.tsx`. No test that `CommerceWebhookEnvelope.event_type` includes runtime `order.completed`.
- Whether any test would fail if the bug is still there: **no**. Honesty stays exit 0. The stale sentence is documentation.
- First regression test: a small node/markdown assertion that if `App.tsx` contains `<Route path="/invoicing/quotes"` the dark-matter table must not say “no routes”; or a comment-only allowlist that `MapPreview`/`MapReason` are not HTTP maps so verbose does not page people. Do **not** TypeSpec-regen to “fix” the envelope unless product opens a contract change.

### Reproduction today
Arrange: open `docs/contracts/openapi-vs-minimal-api.md` §dark matter and `apps/lazuar-ops/src/App.tsx`. Act: load `/invoicing/quotes` and `/workspace/billing-profile` on a logged-in ops session. Assert: pages render (not 404). Run `node scripts/check-openapi-minimal-honesty.mjs --verbose` and see `unresolved call receiver 'SubscriberEndpoints.MapPreview'` / `ResendWebhookParser.MapReason`. Generate a client from `CommerceWebhookEnvelope` and try to type `event_type: "order.completed"` — it does not typecheck.

### Blast radius
Frontend and integrator authors, not runtime money. A new hire following the contracts doc will think invoicing is still lobotomized and may hide working UI or re-add phantom `[MVP-HIDE]`. An ERP generated from `CommerceWebhookEnvelope` cannot bind `order.completed` / `payment_link.paid`. Frequency: every onboarding / every webhook SDK. PII/money: none unless someone “fixes” the envelope by inventing `subscription.updated`.

### Suggested fix
Edit the dark-matter table: invoicing + BillingProfile = **routed**; chat = still unrouted. Mention `MapPreview`/`MapReason` as known verbose noise (or rename those helpers so they do not start with `Map`). Leave `/platform/*` TypeSpec thin until admin grows — do not `task gen`. For the envelope: either document that `CommerceWebhookEnvelope` is **subscription-only** and point at a separate runtime shape for order/payment_link (sample `examples/hub-cashier-next` is the honest client), or add a second union **without** `subscription.updated`. Wrap-rails: no TypeSpec regen unless product explicitly opens it; this issue can be docs-only.

### Evaluation notes
Honesty-only. Still P2. Not a duplicate of 173 (TRIALING in the human catalog — resolved). Combined-spec M2M is done; do not reopen `cbe17c2`. 326’s buttons live on the pages this doc still calls unrouted. Do not mark resolved while the dark-matter table contradicts `App.tsx`.


