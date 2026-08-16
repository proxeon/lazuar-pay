# W2-LP-175 — Portal invoice / receipt history

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-175`. Tracker: *Invoice / receipt history* — Lazuar **B**. Alias `SL-069`.  
**Not this ID:** Single download href (`LP-106`). Subscription list (`LP-171` already **Y**). Paddle customer portal. AR aging (`LP-105`).

**Invariant:** A magic-link buyer can see a **list of their documents** (official receipts, tax invoices, credit notes) for this tenant and open each via HMAC. History is **not** the subscription card. One-time buyers without a subscription still need a path (email remains the fallback).

---

## 0. Scope lock

In scope:

- New section on `/{tenant}/portal` (or `/portal/documents`)
- `GET /public/commerce/{slug}/portal/documents?token=` (or billing public, token-bound)
- Rows: date, number (`RCPT-`/`INV-`/`CN-`/`QT-`), type, amount, status, `download_url`
- Magic token same as portal (email-bound)

Out of scope:

- Guest enumeration by session GUID list
- Staff impersonation
- Xero-style statement PDF
- Un-hiding tax invoice on the **subscription** row as the only UI (that is a one-link hack for LP-106)

---

## 1. Verdict

Portal is **subscriptions + cancel**. There is no document list API. Email HMAC is a deep link, not history. Hidden subscription download is a dead URL.

Stripe/Paddle/HitPay portals are a document inbox. Ours cannot flip to **Y** by uncommenting.

---

## 2. Current files

| Path | Role |
|------|------|
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | Subs only |
| Commerce `GET /public/commerce/{tenantSlug}/portal` | `subscriptions[]` |
| `PublicBillingEndpoints` | Single-id HMAC GET, no list |
| `DocumentPublishedIntegrationEvent` | Email one link |
| CRM `ClientProfile` | Email key for the buyer |

No TypeSpec for portal documents. Ledger query is OrgAdmin-only.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No list endpoint scoped to buyer email |
| G2 | Portal UI has no history section |
| G3 | Cross-module: Commerce token vs Billing ledger (need a billing query by customer email / profile id) |
| G4 | One-time `Order` buyers may have a profile but no subscription — list must include them |
| G5 | Quotes: optional `QT-` drafts if session email matches |

Join: `DocumentPublished` does not persist a “documents” table. Source of truth = `billing.LedgerEntries` where a PDF key `vault/{org}/documents/{ledgerId}.pdf` may exist **or** `CustomerDocumentNumber` is set. Do not list every fee line.

---

## 4. Recommended model

```
Magic token → email → CRM profiles for tenant
  → ledger entries for those profile references
     (via CommerceDocumentLookup / ReferenceId / stored customer email on generate)

GET …/portal/documents
  { items: [{ id, document_number, type, issued_at, amount, currency, lhdn_status, download_url }] }

portal: table under subscriptions
```

`download_url` = same 30-day HMAC as email (or shorter). Type from PDF title / reference type: Official Receipt, Tax Invoice, Credit Note.

Auth: same token rules as portal GET. No token + no session → existing magic-link form.

Do not use `X-Tenant-Id`. Do not return other buyers’ rows (test IDOR).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| TypeSpec commerce public | `PortalDocumentDto` + GET |
| New query in Billing (called from Commerce public handler) **or** Commerce lookup + Billing query service | Email/profile scoped |
| `portal/page.tsx` | History section |
| Tests | IDOR + empty list + HMAC present when PDF exists |

Must not: admin ledger GET from the browser; `/api/billing/invoice?subscription=`.

LP-106 can consume the same DTO (`items[0].download_url`) instead of a one-off field on the subscription.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Token email A | Only A’s documents |
| Token email B | Empty if no sales |
| Foreign tenant slug | 404 |
| After B2C receipt | One row `RCPT-` + working URL |
| B2B VALID | Type Tax Invoice; number `INV-` |
| No PDF yet | Row allowed with null URL **or** omit — pick one and test |

---

## 7. Acceptance

1. Magic-link portal shows a document history (not only Cancel Plan).  
2. Each row’s download opens the correct PDF.  
3. Another buyer’s token cannot see those rows.  
4. One-time purchasers who have a portal token (or request link by the same email) see the receipt.

Tracker **B → Y** after 1–3. Subscription-button-only (LP-106) is **P** at best.

---

## 8. Wave 2 program order (this ID last among documents)

1. **LP-122** legal profile  
2. **LP-022** + **LP-112** buyer TIN  
3. **LP-101** numbers  
4. **LP-102** quotes un-hide  
5. **LP-107** PDF fields  
6. **LP-103** / **LP-110** / **LP-111** / **LP-113** invoice loop  
7. **LP-104** / **LP-116** notes + cancel  
8. **LP-114** / **LP-118** consolidation + SST  
9. **LP-117** signing flag  
10. **LP-106** + **LP-175** buyer access  

Un-commenting `[MVP-HIDE]` without the joins (metadata `is_b2b_required`, VALID→ledger key, cancel internal id, stub TIN) is not Wave 2.

---

Do **not** implement from this file.
