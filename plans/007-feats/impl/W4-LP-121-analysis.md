# W4-LP-121 — Xero sync (not Xero replacement)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-121`. Tracker: *Xero / QuickBooks sync* — Lazuar **N**. Aliases `LP-TRU-006` / ADR 021 keep.  
**Not this ID:** Full GL (`LP-206` refuse). QuickBooks in the same ticket. Unparking `RevenueRecognitionJob` (00.3). LHDN submit (`LP-110`). Becoming the accountant.

**Invariant:** After **Wave 2 LHDN/invoice UI is live**, we push **already-issued** commercial documents (Official Receipt / Tax Invoice / payment) into Xero as invoices + payments via OAuth. Lazuar remains system of checkout truth; Xero remains the GL. Failure is visible; we do not silently skip.

---

## 0. Scope lock

In scope:

- Xero OAuth2 (tenant BYOK app or platform app + tenant connection)  
- Map `LedgerEntry` with `CustomerDocumentNumber` → Xero Invoice  
- Map payment (gateway complete) → Xero Payment  
- Ops: connect / disconnect / last error  
- Idempotent external id = ledger id

Out of scope:

- Bank feeds / payout recon (`LP-095` refuse)  
- Contacts sync beyond buyer name/email  
- Inventory  
- QuickBooks  
- Creating Xero invoices **before** we have a local document (Wave 2)

**Blocked on:** LP-103 / LP-102 un-hide and honest `RCPT-`/`INV-` (Wave 2). Do not sync “Lazuar Merchant / TIN N/A” PDFs as if they were books.

---

## 1. Verdict

Zero Xero code. README Phase 1 still lists cloud accounting. ADR 021 keep is a **delay**, not a refuse. Shipping Xero before merchants can see invoices is syncing a ghost.

---

## 2. Current files

| Path | Role |
|------|------|
| Billing ledger + QuestPDF | Source documents |
| `RevenueRecognitionJob` | Parked |
| README | “Xero, QuickBooks” wishlist |
| No `Xero*` sources | |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No OAuth / tokens table |
| G2 | No mapper |
| G3 | No worker on `DocumentPublished` / payment  
| G4 | Claims in README |

---

## 4. Recommended model

```
one or billing.XeroConnections (org, tenant_id, refresh encrypted)
On DocumentPublished (Official Receipt | Tax Invoice):
  upsert Xero Invoice (status AUTHORISED) + Payment if cash already in
Idempotency: reference = CustomerDocumentNumber
Errors: store LastError; ops banner; retry 3x
```

Do not push LHDN XML to Xero. Do not invent accounts beyond one “Lazuar Pay clearing” + one bank/clearing account the merchant picks at connect.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| New small integration (Billing or One) | OAuth + HTTP client |
| Token vault | Reuse `AesSecretVault` |
| Handler on `DocumentPublished` | Push |
| Ops Workspace page | Connect Xero |
| README | Remove until connected tenants exist |
| Tests | Mapper + idempotent replay |

Must not: new ERP module; QuickBooks dual-write.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Receipt RCPT-2026-00001 | One invoice create payload |
| Replay same ledger id | No second invoice |
| No connection | Handler no-op, no throw |
| 401 | LastError set |

No live Xero in CI.

---

## 7. Acceptance

1. A paid B2C sale with a real receipt number appears in the connected Xero org once.  
2. Disconnect stops pushes.  
3. README does not list Xero as shipping until this works.  
4. LHDN remains in Lazuar; Xero is a copy.

Tracker **N → Y** after 1–2 **and** Wave 2 documents exist. Else stay **N**.

---

## 8. Order

Wave 2 LP-103/107/122 first. Then OAuth → mapper → ops.

Do **not** implement from this file.
