# W2-LP-122 — done

Ops **Legal & Billing** is remounted at `/workspace/billing-profile` (sidebar under Workspace). Card 1 writes `TenantBillingProfile` (legal name, TIN, SSM, SST, logo, address + MY state codes). Saving stationery copies name/TIN/address onto existing `LhdnTenantConfig` without wiping the MyInvois secret. Card 2 is MyInvois (TIN/BRN/MSIC/env/creds/cert). `StandardInvoice.xml` / `SelfBilledInvoice.xml` bind supplier address from config — no Bangunan Merdeka. Product checkout still uses LP-025 branding only (no public billing TIN).

## Files

- Ops `App.tsx` + `Sidebar.tsx` + `BillingProfilePage.tsx`
- `SyncSupplierStationeryCommand` + handler; billing profile save publishes it
- `LhdnTenantConfig.SyncStationeryIdentity`
- LHDN `StandardInvoice.xml` + `SelfBilledInvoice.xml` Scriban supplier address
- Tests: `TenantLegalProfileTests`, `PublicWorkspaceBrandingTests` (no TIN on branding DTO)

## Tests run

- `TenantLegalProfileTests` (PUT persist, sync preserves secret, GET flags, XML no Merdeka) + branding TIN assertion — included in the **22 passed** LP-022/122 filter
- `npx tsc --noEmit -p apps/lazuar-ops` — clean

Not committed. Not pushed.

Tracker `LP-122` can move **B → Y** (editor + UBL supplier address bound). PDF stationery mapping remains LP-107.
