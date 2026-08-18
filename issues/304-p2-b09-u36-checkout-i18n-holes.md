---
number: "304"
id: B09-U36
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 304 — B09-U36 — Checkout i18n holes

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U36 — Checkout i18n holes (P2)

`CheckoutForm.tsx` 228–251 (“ID type”, “ID value”); `CheckoutView.tsx` 160 (“Yearly” / “Monthly”); portal, update-payment, QuoteView, legal: English only. The i18n test only checks dictionary key parity.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Product checkout has a real EN/BM dictionary and a `CheckoutI18nProvider`, but several buyer-visible strings never go through `t()`. On a B2B (`requires_tax_id`) checkout the ID-type and ID-value labels are hard-coded English, so a BM session still reads “ID type” / “ID value” / “SSM / NRIC / passport no.” next to otherwise-translated TIN fields. When a product has more than one price, the interval pills are hard-coded `"Yearly"` / `"Monthly"` (including for any non-`yr` interval, which is labeled Monthly even if it is `one_time`). Quote pay (`QuoteView`), the aggregated portal, update-payment, and the legal articles are entirely English; they are not inside `CheckoutI18nProvider`. The only portal test asserts `en`/`ms` key parity and therefore stays green while the painted UI is mixed-language.

### Still present?
**STILL BROKEN**

`CheckoutForm` still interpolates most labels via `t(...)` and then drops to English for the MyInvois ID pair:

```231:254:apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx
                <label htmlFor="id-type" className="text-sm font-semibold text-foreground">ID type</label>
                <select
                  id="id-type"
                  required
                  value={idType}
                  onChange={e => setIdType(e.target.value)}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                >
                  <option value="BRN">BRN</option>
                  <option value="NRIC">NRIC</option>
                  <option value="PASSPORT">PASSPORT</option>
                  <option value="ARMY">ARMY</option>
                </select>
              </div>
              <div className="space-y-2">
                <label htmlFor="id-value" className="text-sm font-semibold text-foreground">ID value</label>
                <input
                  id="id-value"
                  required
                  type="text"
                  value={idValue}
                  onChange={e => setIdValue(e.target.value)}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                  placeholder="SSM / NRIC / passport no."
```

Interval pills still ignore the dictionary (`summary.intervalYear` / `summary.intervalMonth` exist and are unused here):

```174:191:apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx
      {prices.length > 1 && (
        <div className="mb-6 flex gap-2">
          {prices.map((p) => (
            <button
              key={p.id}
              type="button"
              onClick={() => {
                setSelectedInterval(p.interval);
                handleRemoveCoupon();
              }}
              className={`h-9 px-4 text-[11px] font-bold uppercase tracking-widest border ${
                selectedInterval === p.interval
                  ? "bg-foreground text-background border-foreground"
                  : "bg-background text-foreground border-border"
              }`}
            >
              {p.interval === "yr" ? "Yearly" : "Monthly"}
            </button>
```

`messages.ts` has no `form.idType` / `form.idValue` / interval-pill keys. `i18n.test.mjs` only checks matching key sets. `QuoteView.tsx` 73, 135–158, 181, 311 and `update-payment/[subId]/page.tsx` 56–80 and `portal/page.tsx` 57–93 and `legal/terms/page.tsx` / `legal/privacy/page.tsx` are English JSX with no `t()`. Checkout chrome footer *is* localized (`layout.tsx` 50–58); that is the exception, not the rule.

### Related files
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` — hard-coded ID labels on the live B2B form.
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` — hard-coded Yearly/Monthly pills.
- `apps/lazuar-portal/src/modules/checkout/i18n/messages.ts` — EN/BM dictionary; missing the skipped keys.
- `apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` — key-parity test that cannot catch holes.
- `apps/lazuar-portal/src/modules/checkout/i18n/CheckoutI18n.tsx` — provider used only under product-checkout layouts.
- `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` — wraps product checkout only.
- `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` — English quote / proforma / ID-pair UI.
- `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` — English dashboard + “Identity Verified”.
- `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` — English arrears / update-card copy.
- `apps/lazuar-portal/src/app/legal/terms/page.tsx`, `apps/lazuar-portal/src/app/legal/privacy/page.tsx` — English-only legal articles.
- `apps/lazuar-portal/src/app/layout.tsx` — localized footer labels pointing at English legal pages.

### Tests
- Existing tests that touch this path: `apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` `messages` / `has matching en and ms keys`; `classifyCheckoutError`; `resolveCheckoutLocale`. `apps/lazuar-portal/package.json` `test` runs only `i18n.test.mjs` and `grossBreakdown.test.mjs`.
- Whether any test would fail if the bug is still there: **No.** Key parity stays green while JSX is English. There is no ops/admin frontend test suite (`lazuar-ops` / `lazuar-admin` `package.json` have `lint` only).
- What a first regression test should assert: `en`/`ms` contain `form.idType`, `form.idValue`, `form.idValuePlaceholder`, `interval.yearly`, `interval.monthly`; a grep/fixture test that `CheckoutForm.tsx` / `CheckoutView.tsx` do not contain the literal strings `"ID type"`, `"ID value"`, `"Yearly"`, `"Monthly"`; optional: QuoteView / portal strings go through the same dictionary or are explicitly documented as EN-only.

### Reproduction today
Arrange a workspace product with `requires_tax_id` and two prices (`mo` + `yr`). Open `/{slug}/checkout/{product}?lang=ms` (or a `ms-MY` Accept-Language browser; U55 still prefers any `ms` tag). Act: look at the interval pills and the TIN ID pair. Assert: surrounding fields are BM (`Butiran akaun`, `Nombor Pengenalan Cukai`) while pills stay “YEARLY”/“MONTHLY” and labels stay “ID type”/“ID value”. Repeat on `/{slug}/pay/{sessionId}` and `/{slug}/portal?token=…` and `/legal/terms`: entire page English.

### Blast radius
Malay-speaking B2B buyers at checkout (TIN + ID is the LHDN-required pair). Not money-wrong and not PII leakage; it is a compliance-adjacent UX hole on the only localized buyer surface. Hits every BM session on a tax-ID or multi-price product. Quote/portal/legal never offered BM, so those are honesty/scope rather than a regression of a promised translation.

### Suggested fix
Add the missing keys to `en`/`ms` and replace the four hard-coded CheckoutForm/CheckoutView strings with `t(...)`. Map non-`mo`/`yr` intervals honestly (`one_time` is not “Monthly”). Do not invent a second i18n stack. QuoteView / portal / update-payment / legal can stay English in this ticket if you explicitly scope U36 to product checkout; if the ticket is the whole portal, wrap those trees in the same provider and dictionary. No TypeSpec regen, no Stripe Billing, no WhatsApp.

### Evaluation notes
Duplicates the 008 “ID type + interval skipped i18n” row (still OPEN at 297ba98; still OPEN today). Adjacent: U55 (`i18n.test.mjs` 72–78 prefers any `ms` tag). Residual copy: `form.country` is still `"Country Code (e.g. MY)"` even after 102/190 defaulted the field to `MYS`. Severity still P2 — mixed-language checkout, not a charge bug. Not blocked.

