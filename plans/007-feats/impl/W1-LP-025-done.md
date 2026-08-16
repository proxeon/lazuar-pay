# W1-LP-025 — done

Hosted checkout branding is a cash-register skin: workspace **name + optional https logo + optional `#RRGGBB` accent**. Stored on `one.Organizations`, edited on General Settings, published as `GET /public/one/{slug}/branding` (no TIN / legal profile). Hop-1 header shows logo or name; CTA uses `--brand` when set; legal copy names the workspace. “Powered by Lazuar” stays.

## Files

- `Organization.UpdateBranding` + One migration `20260818100000_AddOrganizationCheckoutBranding`
- `GET/PUT /one/workspaces/{id}` + public branding GET
- Ops `GeneralSettingsPage` logo upload + color
- Portal tenant layout `--brand`, checkout header, form CTA/copy, update-payment mark
- Tests: `OrganizationBrandingTests`, `PublicWorkspaceBrandingTests`

## Tests run

- `OrganizationBrandingTests|PublicWorkspaceBrandingTests` — **passed** (with Wave 1 filter **103 passed**)
- `npx tsc --noEmit -p apps/lazuar-portal` and `lazuar-ops` — clean

Not committed. Not pushed.

Tracker `LP-025` can move **P → Y**. PDF branding remains LP-107.
