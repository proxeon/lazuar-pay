# R23 — Billing signed PDF honesty

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** TypeSpec · Wave B  
**Checklist:** `checklists/r23-tsp-billing-pdf-honesty.md`  
**Analysis:** `08-typespec-wave-b.md` §3.1  
**Scope this pass:** Decide product vs internal for public final signed PDF; implement allowlist path (B-PDF-Allowlist). No TypeSpec add, no `task gen`.

---

## Decision

| Question | Answer |
|----------|--------|
| Final signed PDF is public/admin **product** API surface? | **No** |
| Option chosen | **B-PDF-Allowlist** (analysis §3.1) |
| Product OpenAPI change | None — keep route out of TypeSpec / product docs |
| R25 allowlist | Entry added in `packages/api-spec/honesty-allowlist.yaml` |

### Why not B-PDF-Add

| Signal | Finding |
|--------|---------|
| Runtime | `GET /public/billing/{tenantSlug}/documents/{ledgerEntryId}?sig&exp` → HMAC validate → **302 Redirect** to R2 presign (`PublicBillingEndpoints.cs`) |
| Consumers | Email only: `DocumentPublishedIntegrationEventHandler` builds `document_link` for Quotation/Receipt templates |
| Typed clients | No openapi-fetch / SDK caller for final path |
| Developers hub | Billing product docs already cover admin document URL + public **draft** PDF |
| TSP modeling cost | Success is redirect (no body); claiming `bytes` would be dishonest (draft streams PDF; final does not) |
| Contrast — draft | Product: checkout returns signed `draft_pdf_url`; TSP `getDraftDocument` → `bytes` ✅ |
| Contrast — admin | Product: `GET /admin/billing/ledger/{id}/document` → `DocumentDownloadUrlDto` JSON ✅ |

Allowlist is the honest, cheaper path when links are email/HMAC-only and response shape is redirect.

---

## Route inventory (post-R23)

| Route | Auth | Response | TypeSpec | Notes |
|-------|------|----------|----------|-------|
| `GET /admin/billing/ledger/{id}/document` | OrgAdmin Bearer | `200` + `{ url }` | **Yes** (`downloadLedgerDocument`) | Product admin download |
| `GET /public/billing/{tenantSlug}/documents/draft/{sessionId}?sig&exp` | HMAC | `200` PDF bytes | **Yes** (`getDraftDocument`) | Product checkout proforma |
| `GET /public/billing/{tenantSlug}/documents/{ledgerEntryId}?sig&exp` | HMAC | **302** → R2 | **No** (allowlisted) | Email receipt/quotation link |

HMAC payload (final): `tenantSlug:final:ledgerEntryId:exp` via `DocumentLinkSigner.FinalDocumentPayload`.

---

## Implement

1. **`packages/api-spec/honesty-allowlist.yaml`** (new) — `impl_only` row for final PDF + seed of routes already prose-allowlisted in contracts doc (R25 CI input).
2. **`docs/contracts/openapi-vs-minimal-api.md`** — surface map clarified; allowlist table row + pointer to YAML.
3. **`apps/lazuar-api/Modules/Billing/README.md`** — § public/admin document download honesty note.
4. No endpoint/runtime change; no generated-types bind (route not exposed).

---

## Verification

| Check | Result |
|-------|--------|
| Final path in `packages/api-spec/modules/billing/routes.tsp` | Absent (intentional) |
| Draft + admin document in TSP | Present |
| Product OpenAPI advertises final? | No |
| Allowlist YAML entry | Present with reason |
| Runtime endpoint retained | Yes (`PublicBillingEndpoints`) |

---

## Residual

| Item | Owner | Note |
|------|-------|------|
| R25 path-honesty CI | R25 | Consume `honesty-allowlist.yaml`; expand rows for comms HTML/Svix if still impl-only |
| Promote final PDF to TSP later | Product | Only if integrators need typed client or Scalar; model 302 honestly, not as `bytes` |
| Admin document existence check | Billing debt | Pre-existing gap (gap doc); out of R23 |

---

## Files

| Action | Path |
|--------|------|
| Created | `packages/api-spec/honesty-allowlist.yaml` |
| Edited | `docs/contracts/openapi-vs-minimal-api.md` |
| Edited | `apps/lazuar-api/Modules/Billing/README.md` |
| Notes | `plans/005-remaining/r23-notes.md` |
| Checklist | `plans/005-remaining/checklists/r23-tsp-billing-pdf-honesty.md` |
| FULL-CHECKLIST | R23 section checked |
