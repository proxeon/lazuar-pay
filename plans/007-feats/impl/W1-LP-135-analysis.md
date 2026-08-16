# W1-LP-135 — Versioned event catalog in docs (VitePress, not Scalar)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-135`. Tracker: *Versioned event catalog in docs* — Lazuar **P**.  
**Not this ID:** emit `payment.refunded` / `invoice.submitted` / `invoice.cancelled` (catalog honesty, not new product). Redrive (`LP-133` done). Silent-drop (`LP-132`). Do **not** grow the Scalar `/webhooks` page into the SSoT.

**Invariant:** Integrators learn **which events exist in v1**, the **envelope**, and **what to do** from VitePress `lazuar-docs`. The page only lists events the dispatcher actually posts today.

---

## 0. Scope lock

In scope:

- `apps/lazuar-docs/docs/reference/events.md` as the **v1 catalog**
- Cross-links from VitePress webhooks guide + Developers hub
- Honesty: shipped vs not shipped
- Version label aligned with `docs/api-versioning.md` (`1.0.0` / path `/api/v1`)

Out of scope:

- New outbound event types
- Fixing TypeSpec `PaymentWebhookPayloadDto` flat lie (note it; do not rewrite OpenAPI in this ticket unless one comment)
- Replacing Scalar product pages
- Mermaid diagrams

---

## 1. Verdict

Narrative webhooks (**how to verify**) already live in VitePress. The **catalog** is a stub. Developers Scalar hub has a richer table that **oversells**.

| Surface | What it is | Honesty |
|---------|------------|---------|
| `lazuar-docs/docs/reference/events.md` | Thin tables; Commerce uses `…`; `payment.refunded` “maturing” | Incomplete |
| `lazuar-docs/docs/integrations/webhooks.md` | How to verify Payments hop-2 | Good how-to; not a full catalog |
| `lazuar-developers/app/webhooks/page.tsx` | Tables for commerce / payment / LHDN | Lists `invoice.submitted` / `invoice.cancelled` as if real |
| TypeSpec `PaymentWebhookPayloadDto` | Flat body | **Not** the wire envelope |
| TypeSpec `commerce/models/webhooks.tsp` | Five `subscription.*` only | Omits `order.completed`, `payment_link.paid` |
| Runtime One dispatcher | `{ id, event_type, created_at, data }` | Truth |

Tracker **P** is right: a page exists; it is not a versioned SSoT.

---

## 2. Current files

| Path | Role |
|------|------|
| `apps/lazuar-docs/docs/reference/events.md` | Catalog stub |
| `apps/lazuar-docs/docs/.vitepress/config.ts` | Sidebar “Event catalog” → that file |
| `apps/lazuar-docs/docs/integrations/webhooks.md` | Payments verify + envelope example |
| `apps/lazuar-docs/docs/reference/openapi.md` | Points Scalar at contracts |
| `docs/api-versioning.md` | Additive events = non-breaking; filtered endpoints must opt in |
| `apps/lazuar-developers/app/webhooks/page.tsx` | Duplicate catalog + oversell |
| `apps/lazuar-developers/app/page.tsx` | “Event catalog” card → `/webhooks` (developers) |
| `packages/api-spec/modules/commerce/models/webhooks.tsp` | Frozen five subscription types |
| `packages/api-spec/modules/payments/models.tsp` | Flat `PaymentWebhookPayloadDto` |

**Shipped event types (runtime — list only these as v1):**

| Family | Type | Writer (evidence) |
|--------|------|-------------------|
| Payments M2M | `payment.completed` | `IntegrationCheckoutGatewayEventsHandler` |
| Payments M2M | `payment.failed` | same |
| Commerce | `subscription.activated` | lifecycle handlers |
| Commerce | `subscription.resumed` | recovery |
| Commerce | `subscription.past_due` | billing engine / fail handler |
| Commerce | `subscription.canceled` | admin / portal / dunning / PDPA |
| Commerce | `subscription.suspended` | dunning terminal |
| Commerce | `order.completed` | one-time product |
| Commerce | `payment_link.paid` | custom checkout |
| LHDN | `invoice.valid` | poller → One |
| LHDN | `invoice.invalid` | poller → One |

**Not v1 (do not list as available):** `payment.refunded`, `invoice.submitted`, `invoice.cancelled`, `subscription.updated` (explicitly forbidden).

Envelope (all One-dispatched families):

```json
{ "id": "<uuid v7>", "event_type": "payment.completed", "created_at": "<iso>", "data": { } }
```

Headers: `X-Lazuar-Signature` (`t=,v1=`), `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id`.

---

## 3. Gaps

### G1 — VitePress catalog is not usable as SSoT

Missing types, no envelope, no `data` field list, no version heading.

### G2 — Developers page is the accidental SSoT and it lies

`invoice.submitted` / `invoice.cancelled` “when emitted” — poller does not emit them (R40/R43).

### G3 — Three webhook body stories

TypeSpec flat ≠ VitePress envelope ≠ hub `data` example with `event_id` inside `data`.

**Not gaps**

- Signature algorithm (already documented).
- Implementing refund events.

---

## 4. Minimal changes

### 4.1 Must — rewrite `reference/events.md`

Structure (lock):

1. **Version:** `Catalog v1` · API `/api/v1` · OpenAPI `1.0.0`. Additive types only; filtered endpoints must add new names.  
2. **Delivery:** one envelope + headers; link to `/integrations/webhooks` for verify.  
3. **Tables per family** with columns: `event_type` | When | Your action | `data` keys (minimal).  
4. **Not in v1** table: the four names above + “do not subscribe expecting them”.  
5. **Do not mix families:** M2M cashier ≠ Commerce unlock.

Copy `data` keys from runtime builders, not from TypeSpec `PaymentWebhookPayloadDto`.

### 4.2 Must — demote Developers `/webhooks`

- Banner: “Normative catalog: VitePress Event catalog (v1).” Link `VITE_DOCS_URL` / `/docs/reference/events`.  
- Remove `invoice.submitted` / `invoice.cancelled` from the LHDN table **or** mark **Not emitted**.  
- Keep verify snippets if useful; do not add a third catalog.

### 4.3 Should

- `integrations/webhooks.md` “Events” section → link to `/reference/events` instead of repeating.  
- `index.md` Start-here row: “Which events exist” → catalog.  
- One sentence on `docs/api-versioning.md` pointing at the VitePress page as the human catalog.

### 4.4 Do not

- Rebuild Scalar as the catalog.  
- Emit new events to make the old tables true.  
- Fix all TypeSpec honesty in this ticket (optional one-line `@doc` on `PaymentWebhookPayloadDto`: “not the wire envelope; see VitePress”).

---

## 5. Tests

Docs-only. No API tests required.

Manual / CI-lite:

| Check | How |
|-------|-----|
| Sidebar opens `/reference/events` | `pnpm --filter lazuar-docs build` |
| Every **v1** type string appears exactly once as shipped | Grep page vs list in §2 |
| Forbidden types not in the shipped table | Grep |
| Developers page does not claim submitted/cancelled as live | Grep `lazuar-developers/app/webhooks/page.tsx` |

Optional: a tiny unit list in `OutboundWebhookTests` that the accepted event-name set equals the markdown table — **not** required if you do not want docs-in-code.

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Docs drift after a new event | `how-to-maintain.md` already says update guides with contracts; add “update events.md in the same PR” |
| Integrators still bookmark Scalar | Banner + homepage link |

---

## 7. Acceptance

1. VitePress Event catalog is labeled **v1** and lists only shipped types in §2.  
2. Envelope matches the dispatcher.  
3. `payment.refunded` / LHDN submitted+cancelled are **not** sold as live.  
4. Developers hub points here and does not contradict.  
5. Tracker **P → Y**.

---

## 8. Implement order

1. Rewrite `events.md`  
2. Trim / banner Developers `/webhooks`  
3. Cross-links  
