---
number: "323"
id: B09-U55
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 323 — B09-U55 — Portal i18n Accept-Language prefers any `ms` tag even at low q

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U55 — Portal i18n Accept-Language prefers any `ms` tag even at low q (P2)

`i18n.test.mjs` 72–78 asserts this. A `en-US,en;q=0.9,ms-MY;q=0.8` browser gets BM. Product decision encoded as a test; easy to call a bug later.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Checkout locale resolution does not honor `Accept-Language` quality values. `parseAcceptLanguage` walks the header left to right and returns `ms` as soon as any tag’s primary subtag is `ms`. It never reads `;q=`. Chrome’s default for a Malaysian English UI is typically `en-US,en;q=0.9,ms-MY;q=0.8` (or similar): English is preferred, Bahasa Melayu is listed at a lower q. Those buyers get BM checkout unless they have `?lang=` / `?locale=` / `lazuar_locale` cookie. The audit’s complaint is not “BM exists”; it is that a **test locks the non-q-weighted behavior**, so a later engineer will “fix” it and fight CI, or will ship BM to English-first browsers and call it a bug.

### Still present?
**DOCS / HONESTY ONLY**

The implementation and the cementing test are unchanged:

```27:34:apps/lazuar-portal/src/modules/checkout/i18n/locales.ts
export function parseAcceptLanguage(header: string | null | undefined): Locale | null {
  if (!header) return null;
  for (const part of header.split(",")) {
    const tag = part.split(";")[0]?.trim();
    if (parseLocale(tag) === "ms") return "ms";
  }
  return null;
}
```

```72:78:apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs
  it("uses Accept-Language ms even when it is not the first tag", () => {
    assert.equal(
      resolveCheckoutLocale({
        acceptLanguage: "en-US,en;q=0.9,ms-MY;q=0.8",
      }),
      "ms",
    );
  });
```

Resolution order is still query → cookie → Accept-Language → `en` (`locales.ts:42–48`). `getCheckoutLocale.ts:18–23` passes `headers().get("accept-language")` into that helper. `id-ID` is still not treated as BM (`i18n.test.mjs:81–84`). There is no q-parser anywhere under `modules/checkout/i18n/`.

This is a documented product choice (prefer any `ms` tag so a bilingual MY header becomes BM) encoded as a unit test. It is only a “bug” if product now wants RFC 9110 q-weighted negotiation.

### Related files
- `apps/lazuar-portal/src/modules/checkout/i18n/locales.ts` — `parseAcceptLanguage` / `resolveCheckoutLocale`.
- `apps/lazuar-portal/src/modules/checkout/i18n/getCheckoutLocale.ts` — server wiring of the header.
- `apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` — cements low-q `ms`.
- `apps/lazuar-portal/src/modules/checkout/i18n/messages.ts` — `en` / `ms` catalogs (key parity still tested).
- `apps/lazuar-portal/src/modules/checkout/i18n/CheckoutI18n.tsx` — consumer (not re-read line-by-line; locale comes from `getCheckoutLocale`).

### Tests
- Existing: `resolveCheckoutLocale` / `parseLocale` / `parseAcceptLanguage` suite in `i18n.test.mjs` (`prefers ?lang=`, `accepts ?locale=`, `uses cookie`, **`uses Accept-Language ms even when it is not the first tag`**, `defaults to en, including for id-ID`).
- That last `ms` test would **fail** if you implemented q-weighted English-first behavior. Today the suite is green *because* the “bug” is the spec.
- First regression test *if product wants RFC negotiation*: `en-US,en;q=0.9,ms-MY;q=0.8` → `en`; `ms-MY,en;q=0.8` → `ms`; `en;q=0.2,ms;q=0.8` → `ms`. Invert the existing test rather than adding a second conflicting one. Keep `id` ≠ `ms`.

### Reproduction today
Arrange: clear `lazuar_locale`, no `?lang=` / `?locale=`. Act: `curl -H 'Accept-Language: en-US,en;q=0.9,ms-MY;q=0.8' http://localhost:3004/{slug}/checkout/{product}` (or DevTools on a fresh browser with that header). Assert: checkout chrome is BM (`ms` messages), not EN. Act: add `?lang=en` → EN wins. Act: `pnpm --filter lazuar-portal test` — the cited test passes.

### Blast radius
MY English-first buyers see Bahasa Melayu on first checkout visit until they flip the language control (if present) or get a cookie. Not money, not PII. Frequency: first visit from typical `en-US`+`ms-MY` headers. Support tickets: “why is checkout in BM?” Still P2 and only if product disagrees with the encoded decision.

### Suggested fix
Do **not** “fix” unless product asks. If they want English-first: parse `q`, pick the supported tag (`en`|`ms`) with the highest q, treat missing q as 1, then rewrite `i18n.test.mjs` so it no longer expects `ms` for `en-US,en;q=0.9,ms-MY;q=0.8`. If they want to keep BM-if-any-ms: add a comment on `parseAcceptLanguage` that q is ignored on purpose so the next person does not file this again. No TypeSpec. No locale segment in the path (breaks `App:ClientUrl`).

### Evaluation notes
Honesty / spec, not a regression. Easy to mis-file as a bug — that is why U55 exists. Unrelated U22 (`error.gatewayDown` for missing email) was **fixed** in 151; `i18n.test.mjs` now maps that string to `error.emailMissing` (`i18n.test.mjs:134–137`). Do not revert that while touching locale tests. Still P2 only as a product-decision foot-gun.

