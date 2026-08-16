# W2-LP-122 — Merchant legal profile (TIN, BRN, address)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-122`. Tracker: *Merchant legal profile (TIN, BRN, address)* — Lazuar **B**. Alias `LP-TAX-008`, `INV-010`.  
**Not this ID:** Workspace trading name / checkout logo (`LP-025`). Buyer TIN (`LP-022`). PDF field mapping (`LP-107`). MyInvois client id / `.p12` (same **page** is OK; signing is LP-117).

**Invariant:** One merchant-facing **Legal & Billing** screen writes the supplier identity used on PDFs **and** UBL. LHDN templates must not emit Bangunan Merdeka. Public product checkout must **not** fetch this DTO (TIN leak).

---

## 0. Scope lock

In scope:

- Remount `/workspace/billing-profile` + sidebar **Legal & Billing**
- Keep `TenantBillingProfile` as commercial stationery SSoT
- Keep `LhdnTenantConfig` as MyInvois SSoT (TIN, id type/value, MSIC, creds, cert, legal address)
- **Sync or single editor** so merchants do not type TIN twice
- Bind `StandardInvoice.xml` supplier address to config (shared with LP-110)
- Do not call public profile from product checkout

Out of scope:

- Checkout branding fields on Organization
- Intermediary appointment legal pages (EasyStore-class) as a marketing site
- Making public `GET /public/billing/{slug}/profile` the checkout brand API

---

## 1. Verdict

Two aggregates, one hidden editor, templates ignore the address mapper.

| Store | Fields | Editor | Consumers |
|-------|--------|--------|-----------|
| `billing.TenantBillingProfiles` | legal name, TIN, SSM, SST, logo, address | `BillingProfilePage` **unrouted** | QuestPDF, QuoteView, public GET |
| `lhdn.LhdnTenantConfigs` | supplier TIN, id type/value, MSIC, env, client id/secret, PFX, legal name/address | **No ops page** (`PUT /lhdn/workspaces/{id}/lhdn-config` exists) | UBL mapper / gateway |

`ViewModelMapper` fills supplier address from Lhdn config. `StandardInvoice.xml` **hardcodes** Lot 66 / Bangunan Merdeka / 50480 / state 14. A VALID e-invoice can still carry the SDK sample HQ.

Public `GET /public/billing/{tenantSlug}/profile` returns the **full** DTO including TIN. QuoteView would use it. Product checkout must not.

---

## 2. Current files

| Path | Role |
|------|------|
| `apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` | Full editor + R2 logo |
| `apps/lazuar-ops/src/App.tsx` | Route commented; **page not imported** |
| `Sidebar.tsx` | No Legal link |
| `AdminProfileEndpoints.cs` | `GET/PUT /admin/billing/profile` |
| `TenantBillingProfile.cs` | Aggregate |
| `TenantConfigEndpoints.cs` | OrgAdmin LHDN config + cert |
| `LhdnTenantConfig.cs` | MyInvois + legal address |
| `PublicBillingEndpoints.cs` | Unauthenticated full profile |
| `StandardInvoice.xml` | Hardcoded supplier address |

No ops UI for MyInvois client id, sandbox/prod, intermediary mode, MSIC, or certificate — all required before LP-110 is sellable. Wave 2 should put them on **this** page (second card), not a third hidden island.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Route + import + sidebar |
| G2 | Dual TIN (billing vs Lhdn) drift |
| G3 | UBL address hardcoded |
| G4 | No merchant MyInvois credential UI |
| G5 | Public profile is a TIN oracle for anyone who knows the slug |
| G6 | State code is a raw string (`14`) with no helper |

---

## 4. Recommended model

```
ops /workspace/legal
  Card 1 — Stationery (TenantBillingProfile)
  Card 2 — MyInvois (LhdnTenantConfig): TIN/BRN/MSIC/env/creds/cert

On save Card 1:
  upsert billing profile
  if Lhdn config exists, copy legal name, TIN, address (do not wipe client secret)

On save Card 2:
  Lhdn TIN is source for UBL supplier.tin
```

Alternatively **one TIN field** written to both. Prefer explicit copy with “Same as stationery” checkbox default on.

Public profile: either (a) stop returning TIN on the public GET (break QuoteView legal block — then pass only logo+legal name via a slimmer DTO), or (b) keep full GET for **B2B quote pages only** and never call it from product checkout (LP-025 already forbids this).

Templates: bind `supplier.city` etc. (LP-110 can land the XML change; this ticket owns having data in config).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `App.tsx` | Import + `/workspace/billing-profile` (or `/workspace/legal`) |
| `Sidebar.tsx` | Legal & Billing under Workspace |
| `BillingProfilePage.tsx` | Second card: LHDN GET/PUT + cert upload (base64) |
| Save handlers | Sync name/TIN/address billing → Lhdn |
| `StandardInvoice.xml` | Scriban supplier address (if not done in LP-110) |
| Optional public DTO | Split `PublicMerchantCardDto` vs full legal |

Must not: put TIN on `GET /public/one/{slug}/branding`; use billing profile as checkout theme.

---

## 6. Tests

| Case | Expect |
|------|--------|
| PUT billing profile | GET returns fields |
| Sync | Lhdn `LegalName` / `SupplierTin` / `AddressLine1` updated; secret preserved |
| GET Lhdn config | `has_client_secret`, no raw secret |
| XML generate after address save | No `Bangunan Merdeka` |
| Public branding (LP-025) | No TIN |

---

## 7. Acceptance

1. Merchant can save legal name, TIN, SSM, SST, address, logo without a deep URL.  
2. Same TIN/address appear on a draft PDF (LP-107) and in UBL supplier nodes.  
3. MyInvois client id + cert can be stored (`has_certificate` true).  
4. Product checkout does not load the public billing profile.

Tracker **B → Y** after 1–3. Editor-only without template bind stays **P**.

**Implement this before** LP-107 / LP-110 demos — otherwise PDFs still say Lazuar Merchant and XML still says Merdeka.

---

Do **not** implement from this file.
