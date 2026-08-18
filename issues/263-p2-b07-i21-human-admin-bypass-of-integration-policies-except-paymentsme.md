---
number: "263"
id: B07-I21
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 263 — B07-I21 — Human ADMIN bypass of Integration* policies (except PaymentsMe)

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I21 — P2 — Human ADMIN bypass of Integration* policies (except PaymentsMe)

**Where.** `AuthAndCorsExtensions.cs:96–182`.

**What.** Intentional per W1-LP-137. Still a scope hole relative to “machine keys are the only M2M.” Not a steal of another tenant.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`Integration*` authorization policies (LHDN documents, payments checkouts, webhooks manage, commerce subscriptions) succeed for **either** a scoped `API_CLIENT` **or** a human `ADMIN` / `SUPER_ADMIN`. Only `IntegrationPaymentsMe` is machine-only. A staff cookie plus `X-Tenant-Id` therefore calls the same M2M write routes as a scoped key, without holding `lhdn.documents:write` / `payments.checkouts:write` / etc. W1-LP-137 documented this as the console bypass so Hub humans are not forced through a key. Relative to the sentence “machine keys are the only M2M,” it is a scope hole. It is not cross-tenant: middleware still 403s if the human has no membership (or is not system admin).

### Still present?
**STILL BROKEN**

Host policies are unchanged. Example write policy:

```96:105:apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs
            options.AddPolicy("IntegrationLhdnDocumentsWrite", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && ctx.User.HasClaim("scope", PlatformApiScopes.LhdnDocumentsWrite)));
            });
```

Same `ADMIN`/`SUPER_ADMIN` short-circuit on checkouts write/read, webhooks manage, commerce subscriptions write/read (`AuthAndCorsExtensions.cs:119–182`). PaymentsMe still excludes humans:

```153:161:apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs
            options.AddPolicy("IntegrationPaymentsMe", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("API_CLIENT")
                    && (ctx.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsWrite)
                        || ctx.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsRead)));
            });
```

`Payments_Me_Policy_Denies_Human_Admin` documents the exception. There is **no** test that human ADMIN is allowed (or denied) on the other Integration* policies. `ApiKeyAuthenticationTests.BuildAuthorizationService` **mirrors** the host file (`:506–611`); changing production without the copy would not fail those tests.

### Related files
- `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs:72–183` — policy catalog.
- `apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` — injects membership `ADMIN` when `X-Tenant-Id` matches.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/ApiKeyAuthenticationTests.cs` — scope isolation + PaymentsMe denies human; mirrored policies.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnEndpointsAuthorizationTests.cs` — routes require Integration* names, not who may pass.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/IntegrationCheckoutEndpointsAuthorizationTests.cs` — same.
- `plans/007-feats/impl/W1-LP-137-done.md` — M2M subscriptions; humans were left on the policy.

### Tests
- Existing: `IntegrationWrite_Policy_Allows_ApiClient_With_Write_Scope` and the read/write/cross-product isolation set; `Payments_Me_Policy_Denies_Human_Admin`; `OrgAdmin_Policy_Allows_Human_Admin`.
- None fail if ADMIN is removed from IntegrationLhdnDocumentsWrite (the hole closing) **or** if ADMIN is added to PaymentsMe (a new hole). Endpoint tests only check policy **names**.
- First regression depends on product choice: if the hole stays intentional, add `Integration*_Policy_Allows_Human_Admin` and `Payments_Me_Policy_Denies_Human_Admin` (already there) against **host** `AddLazuarAuthorizationPolicies`, not a pasted copy. If the hole should close, assert ADMIN **fails** Integration* unless `OrgAdmin` routes are the console path.

### Reproduction today
Arrange: workspace ADMIN cookie (JWT role `CLIENT`, membership `ADMIN`), `X-Tenant-Id` set. Act: `POST /api/v1/lhdn/documents` or a checkout Integration write **without** an `sk_*` key. Assert: policy allows (not 403 from Integration*). Repeat as `API_CLIENT` with only `payments.checkouts:read` on an LHDN write → deny (tests already cover). `GET` payments me as the human → deny.

### Blast radius
A compromised staff session can drive M2M write surfaces (LHDN submit/cancel, checkout create, subscription admin, webhook endpoint manage) without a scoped key. Still confined to tenants the human can access. Not a steal of another org. Frequency: every Hub admin who uses ops, by design. Machine-key least-privilege story is weaker than the docs sentence.

### Suggested fix
Do **not** silently delete ADMIN from these policies — ops/LHDN console likely depends on them. Smallest honest change: document in `Modules/One/README.md` / api-keys guide that Integration* (except PaymentsMe) allow workspace ADMIN as a human bypass (W1-LP-137), and point policy tests at `AddLazuarAuthorizationPolicies`. If product wants “keys only” later, split console onto `OrgAdmin` routes and leave Integration* as `API_CLIENT` + scope. No TypeSpec regen. No Wave 5 / WhatsApp / Xero work.

### Evaluation notes
Intentional, still present, still P2. Honesty issue as much as a bug; I did not mark `DOCS / HONESTY ONLY` because the bypass is executable. **259** (JWT vs membership) is how ADMIN appears on the cookie principal. **123** is a different SUPER_ADMIN injection hole. Stay P2.

