---
number: "265"
id: B07-I23
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 265 — B07-I23 — `accepted_terms` is a request-time gate; TOS is the buyer document

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I23 — P2 — `accepted_terms` is a request-time gate; TOS is the buyer document

**Where.** `AuthEndpoints.cs:47–48`; `LoginPage.tsx:9–10, 289–298` links `/portal/legal/terms` and `/privacy`.

**What.** 008 §2.3 still holds. No merchant MSA, no stored version, 99.9% sentence still on the buyer terms. Legal, not a crash.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Public register requires `accepted_terms == true` or throws. Ops signup checkbox links `/portal/legal/terms` and `/portal/legal/privacy` — buyer documents on `lazuar-portal`. Those pages address “you” the purchaser of a Creator product: Lazuar is not a party, 99.9% uptime with no SLA, magic-link buyer access, Creator-as-controller. The boolean is not written to `GlobalUser` or any terms-version column. We cannot prove which text a tenant accepted. 008-evals/05 §2.3 still describes this. It is a legal/honesty gap, not a 500.

### Still present?
**DOCS / HONESTY ONLY**

Request-time gate is still the only enforcement:

```49:50:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs
            if (req.Accepted_terms != true)
                throw new InvalidOperationException("You must accept the Terms of Service and Privacy Policy.");
```

I grepped `apps/lazuar-api` for `AcceptedTerms` / stored terms version — no domain field. DTO remains optional in the generated contract (`packages/api-types-dotnet/Lazuar.ApiContracts.cs:6774–6779`); the handler is the gate. Checkbox still points at buyer URLs (`LoginPage.tsx:9–10, 294–311`) and still admits the MSA gap:

```302:311:apps/lazuar-ops/src/components/LoginPage.tsx
                  <span className="text-[12px] text-[#71717a] leading-relaxed">
                    I agree to the{" "}
                    <a href={LEGAL_TERMS_HREF} target="_blank" rel="noreferrer" className="text-[#09090b] font-semibold hover:underline">
                      Terms of Service
                    </a>{" "}
                    and{" "}
                    <a href={LEGAL_PRIVACY_HREF} target="_blank" rel="noreferrer" className="text-[#09090b] font-semibold hover:underline">
                      Privacy Policy
                    </a>
                    . Platform use is covered by these pages until a merchant MSA exists.
```

Buyer terms still have the 99.9% sentence (`apps/lazuar-portal/src/app/legal/terms/page.tsx:32–34`) and “not a party” (`:20`). Privacy still names the Creator as controller (`privacy/page.tsx:18–24`).

### Related files
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs:49–50` — gate.
- `apps/lazuar-ops/src/components/LoginPage.tsx` — clickwrap + buyer hrefs.
- `apps/lazuar-portal/src/app/legal/terms/page.tsx` — buyer TOS, 99.9%.
- `apps/lazuar-portal/src/app/legal/privacy/page.tsx` — Creator controller.
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs:6756–6779` — optional `accepted_terms`.
- `plans/008-evals/05-identity-roles-keys-audit.md` §2.3 — still accurate.
- `issues/149-p1-b09-u20-legal-privacy-landing-still-sell-whatsapp-communities-courses.md` — adjacent legal copy.

### Tests
- Existing: none that POST register without `accepted_terms` or snapshot the TOS. `GetPublicPricingQueryHandlerTests` / `WorkspaceCreateAuthorizationTests` do not cover the boolean. No portal legal tests.
- Nothing would fail if the gate were removed or if a merchant MSA were linked.
- First regression (product, not legal): register with `accepted_terms: false` / omitted → 400 with the current detail. A legal review is out of band; do not add a test that cements the 99.9% sentence.

### Reproduction today
Arrange: `POST /one/public/register` with valid email/password/workspace and `accepted_terms: false` → 400 must-accept. Repeat with `true` → 200; inspect `one.GlobalUsers` — no terms timestamp/version. Open `/signup`, follow Terms → portal buyer page with “99.9% platform uptime” and “not a party to the transaction.”

### Blast radius
Every new Hub tenant. Procurement / bank questionnaire / PDPA story: wrong legal object, no acceptance ledger, 99.9% claim. Not a crash, not money movement, not a PII leak by itself. Frequency: 100% of public signups.

### Suggested fix
Do not invent a merchant MSA in this ticket (and do not TypeSpec-regen). Smallest engineering honesty: persist `accepted_terms_at` (UTC) + a static version string (e.g. `buyer-tos-2026-06`) on `GlobalUser` via migration only. Keep linking the existing pages and the “until a merchant MSA exists” sentence until legal supplies Hub terms. Do not add WhatsApp/Xero/e-mandate language. Do not put 99.9% on a new merchant page without an SLA.

### Evaluation notes
Still P2 legal. 008 §2.3 holds. Not blocked by **149** (sales copy) but do not collide. Not 161–200 fail-closed. YAML stays `open`.

