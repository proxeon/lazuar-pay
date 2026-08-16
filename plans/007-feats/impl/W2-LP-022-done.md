# W2-LP-022 — done

Hosted checkout collects **buyer company name + TIN** when a product has **Require Company Name & Tax ID**. The live product editor sends a real `requires_tax_id` flag. CRM stores `CompanyName` + `Tin` (never company name in `IdValue`). Gateway metadata stamps `is_b2b_required=true` when TIN is present so Billing books **B2B** (no `RCPT-`, no Official Receipt). Copy is buyer identity, not a validated e-invoice.

## Files

- CRM `ClientProfile.CompanyName` + migration `20260818120000_AddClientProfileCompanyName`
- `ResolveClientProfileCommand` + handler enrich/create
- `InitiateCheckoutCommandHandler` CRM arity + metadata stamp
- Ops `ProductForm` / both `CreateProductForm` checkboxes
- Portal `CheckoutForm` + EN/BM copy
- Tests: `ClientProfileCompanyNameTests`, `CheckoutB2bIdentityTests`, `GatewayPaymentCompletedHandlerTests`

## Tests run

- `ClientProfileCompanyNameTests|CheckoutB2bIdentityTests|GatewayPaymentCompletedHandlerTests|CommerceCheckoutMetadataTests|PublicWorkspaceBrandingTests|TenantLegalProfileTests` — **22 passed**
- `npx tsc --noEmit -p apps/lazuar-portal` and `lazuar-ops` — clean
- portal checkout i18n — **14 passed**

Not committed. Not pushed.

Tracker `LP-022` can move **B → P**. Tax invoice issuance from this TIN remains LP-103 / LP-110. Buyer BRN/NRIC fields remain LP-112.
