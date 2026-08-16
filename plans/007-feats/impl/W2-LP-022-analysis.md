# W2-LP-022 — Company + TIN fields on checkout (unhide MVP-HIDE)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-022`. Tracker: *Company + TIN fields on checkout* — Lazuar **B**. Sequencing alias `LP-TAX-003`. Checkout report `CK` / inventory INV-023 adjacent.  
**Not this ID:** Merchant legal profile (`LP-122`). Live MyInvois TIN lookup UX (`LP-112`). Quotes `/pay/{id}` (`LP-102`). Checkout branding (`LP-025`). Address collection (`LP-023`, already live). SST rate math (`LP-118`).

**Invariant:** When a product (or quote) requires B2B tax details, the **hosted hop-1 form** collects company name + buyer TIN, persists them on the CRM profile, and stamps payment metadata so Billing/LHDN can treat the sale as B2B. Un-commenting JSX without a write path is still **B**.

---

## 0. Scope lock

In scope:

- Ops product toggle **Require Company Name & Tax ID (LHDN B2B)**
- Portal `CheckoutForm` company / TIN block
- Persist `company_name` + `tax_id` on `crm.ClientProfiles`
- Stamp `is_b2b_required=true` on gateway metadata when TIN is present
- Honest copy: this is **buyer identity**, not a validated e-invoice

Out of scope:

- Calling `POST /lhdn/taxpayer/validate` (LP-112)
- Un-hiding quotes or billing profile
- Publishing `InvoiceIssued` / submitting type `01` (LP-103 / LP-110)
- Collecting `id_type` + `id_value` (BRN/NRIC) beyond TIN — required for a real LHDN submit, owned by LP-112
- Showing supplier TIN on the product checkout header (wrong plane)

---

## 1. Verdict

Tracker **B** is correct. The form existed and was **lobotomized** (ADR 023). The API still accepts the fields. The product create/edit path **forces the flag off**. Even if you uncomment the form, the B2B ledger path never fires because **nothing writes `is_b2b_required` onto payment metadata**.

| Layer | Today |
|-------|--------|
| TypeSpec `PublicCheckoutRequestDto` | `company_name?`, `tax_id?` |
| `InitiateCheckoutCommandHandler.EnforceCheckoutConfiguration` | Requires `TaxId` when `product.CheckoutConfiguration.RequiresTaxId` |
| CRM `ClientProfile.Tin` | Stored. **No `CompanyName` column.** Handler maps `request.CompanyName` into **`IdValue`** |
| Product create / update (live `ProductForm.tsx`) | `requires_tax_id: false` hardcoded |
| `CreateProductForm.tsx` (chat registry + older form) | Toggle commented `[MVP-HIDE]`, submit `false` |
| `CheckoutForm.tsx` | State + JSX commented; payload **hard-sets** both fields `undefined` |
| Gateway metadata | `CommerceCheckoutMetadata.MergeClientIntoGateway` never stamps `is_b2b_required` |
| Billing `GatewayPaymentCompletedHandler` | B2B only if metadata `is_b2b_required == "true"` — **never true from product checkout** |

Un-hide without the persist + metadata stamp = merchants collect TIN into the void and still get a B2C Official Receipt (or nothing for a “B2B” quote).

---

## 2. Current files

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | Hidden company checkbox + TIN inputs; submit ignores them |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` | **Live** create/edit (modal + detail). Always sends `requires_tax_id: false` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/components/CreateProductForm.tsx` | Commented toggle. Not the modal path |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/forms/CreateProductForm.tsx` | Chat `FormRegistry` twin; also forced `false` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/CheckoutConfiguration.cs` | `RequiresTaxId` first-class |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | Resolves CRM; enforces tax id; **does not** set B2B metadata |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Contracts/ResolveClientProfileCommand.cs` | `(… Tin, IdType, IdValue, BillingAddress)` — no company name |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Domain/ClientProfileEntity.cs` | `Tin`, `IdType`, `IdValue`. No trading/company name |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/product.tsp` | `requires_tax_id` already on create/update/public DTO |

Public product GET already returns `checkout_configuration.requires_tax_id`. Portal can show the block as soon as a product has the flag.

---

## 3. Exact gaps

### G1 — Product flag is dead in the live editor

`ProductForm.tsx` (the form merchants actually use) never exposes the checkbox and always writes `false`. Existing products cannot require TIN. Un-hiding only `CheckoutForm` does nothing.

### G2 — Checkout payload stripped

Even with `requires_tax_id` true in the API, the portal sets `company_name` / `tax_id` to `undefined`. Server then 400s “This product requires a tax ID” **or** (if flag stays false) accepts the sale with no TIN.

### G3 — Company name stored in the wrong CRM slot

```csharp
new ResolveClientProfileCommand(
    tenantId, name, email, phone,
    request.TaxId,   // Tin
    null,            // IdType
    request.CompanyName, // IdValue  ← wrong
    billingAddress);
```

Company name is written to `IdValue` (the BRN/NRIC slot). There is no company-name column. Later LHDN submit would treat “Acme Sdn Bhd” as a BRN.

### G4 — B2B never reaches the ledger

`GatewayPaymentCompletedHandler` keys off metadata `is_b2b_required`. Product initiate and custom initiate **do not set it**. Custom sessions store `CheckoutSession.IsB2bRequired` but the hop-2 metadata dict is only `type`, `subscription_id`, `tenant_id`.

So a filled TIN still books **B2C**, assigns `RCPT-`, and consolidates. The flag on the quote checkbox is a landmine (LP-102), not a working path.

### G5 — No buyer ID type

LHDN validate/submit need `id_type` + `id_value` (BRN / NRIC / PASSPORT / ARMY). The hidden form only has TIN. Do **not** invent a fake BRN. LP-112 owns the extra fields; this ticket can store TIN + company name only.

**Not gaps for this ticket**

| Observation | Owner |
|-------------|--------|
| No live TIN API on blur | LP-112 |
| Billing profile hidden | LP-122 |
| Quote pay 404 | LP-102 |
| `InvoiceIssued` unpublished | LP-103 / LP-110 |

---

## 4. Recommended model

```
ops ProductForm: requires_tax_id checkbox
  → Product.CheckoutConfiguration.RequiresTaxId
portal CheckoutForm (when flag true)
  → POST /public/commerce/checkout { company_name, tax_id }
  → CRM: Tin + CompanyName (new column)
  → gateway metadata is_b2b_required=true when tax_id present
Billing
  → customer_type B2B, MarkConsolidationNotRequired
```

Rules:

1. Show the block **only** when `requires_tax_id` (product) or when paying a custom session with `is_b2b_required` (LP-102). Do not put TIN on every FPX consumer checkout.
2. Optional “I am buying as a company” is fine **if** the product requires tax id: checked → company + TIN required; unchecked → 400 is honest (“this product requires a company TIN”). Prefer **always required** when the flag is on — simpler, matches `EnforceCheckoutConfiguration`.
3. Add `ClientProfile.CompanyName` (`varchar(200)` null). Stop writing company name to `IdValue`.
4. Stamp `is_b2b_required=true` in `MergeClientIntoGateway` / custom initiate when TIN is non-blank **or** session `IsB2bRequired`.
5. Copy: “Required for a Malaysian tax invoice. We will validate this number in a later step.” Do not say “LHDN validated” until LP-112.

---

## 5. Minimal code changes

### Must

| File | Change |
|------|--------|
| `ProductForm.tsx` | Checkbox; send real `requires_tax_id` |
| `CreateProductForm.tsx` (commerce + chat twin) | Same, so they cannot fight |
| `CheckoutForm.tsx` | Restore state + JSX; send `company_name` / `tax_id` when flag on |
| `ClientProfileEntity` + CRM migration | `CompanyName` |
| `ResolveClientProfileCommand` | Add `CompanyName`; stop using the 7th positional as `IdValue` |
| `InitiateCheckoutCommandHandler` | Pass company to CRM; stamp `is_b2b_required` on gateway metadata |
| Custom initiate branch (same handler) | Copy `session.IsB2bRequired` into metadata |

### Must not

- Call TIN validate
- Publish `InvoiceIssued`
- Un-hide billing profile or quotes
- Put supplier TIN on hop-1

---

## 6. Tests

| Case | Expect |
|------|--------|
| Product `RequiresTaxId`, missing `tax_id` | 400 existing message |
| Product flag on, TIN + company sent | CRM `Tin` + `CompanyName` set; `IdValue` **null** |
| Gateway metadata | `is_b2b_required=true` |
| Billing handler with that metadata | `CustomerType=B2B`, no `RCPT-`, no Official Receipt |
| Product flag off | Form omits block; sale stays B2C |
| Existing profile enrich | Blank TIN filled; existing TIN not overwritten (today’s enrich rule) |

---

## 7. Acceptance

Close LP-022 when:

1. Merchant can turn **Require Company Name & Tax ID** on a checkout link and save it (`GET` product returns `requires_tax_id: true`).
2. Hop-1 shows company + TIN; submit without them fails; submit with them stores both on CRM.
3. Paid webhook books the sale as **B2B** (no consolidation, no silent B2C receipt).
4. Company name is **not** in `IdValue`.
5. No MyInvois submit and no “validated TIN” claim.

Tracker **B → P** after 1–4 (B2B invoice still Wave 2 siblings). **Y** only when LP-103 can issue a tax invoice from this TIN.

---

## 8. Suggested implement order

1. CRM `CompanyName` + fix `ResolveClientProfile` arity  
2. Stamp `is_b2b_required` on initiate  
3. ProductForm checkbox  
4. Un-hide `CheckoutForm`  
5. Tests §6  

Do **not** implement from this file.
