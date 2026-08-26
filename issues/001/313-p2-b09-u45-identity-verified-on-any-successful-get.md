---
number: "313"
id: B09-U45
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 313 — B09-U45 — Identity Verified on any successful GET

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U45 — Identity Verified on any successful GET (P2)

Including a query-string token. Looks like a session. It is a 24h HMAC.

## Evaluation (current tree, 2026-08-18)

### What the bug is
The buyer portal, after a successful `GET /public/commerce/{tenantSlug}/portal?token=…`, paints a green “Identity Verified” banner with a shield. The token is not a login session. It is a 24-hour HMAC-SHA256 magic link (`Base64("{subscriptionId}:{expiryUnix}:{hmacHex}")` over `Jwt:Secret`). Anyone who has the URL — forwarded email, support transcript, browser history — sees the same verified chrome until expiry. There is still no cookie session on this GET (U02): missing token renders the magic-link form, not this banner. The banner therefore fires on *any* HMAC that the API accepts, including a query-string token the buyer did not type on this device.

### Still present?
**STILL BROKEN**

```66:97:apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx
  const accessToken = token;

  const { data: commerceData, error: commerceError } = await serverClient.GET("/public/commerce/{tenantSlug}/portal", {
    params: { path: { tenantSlug }, query: { token: token ?? "" } },
    next: { revalidate: 0 }
  });

  if (commerceError || !commerceData) {
    notFound();
  }
  ...
      <div className="flex flex-col sm:flex-row sm:items-center justify-between p-4 bg-emerald-50/50 border border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900 gap-2">
        <p className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500 flex items-center gap-1.5">
          <ShieldCheck size={14} /> Identity Verified
        </p>
        <p className="text-[11px] font-medium text-emerald-600 dark:text-emerald-500/80">
          Accessing resources for this workspace.
        </p>
      </div>
```

Token implementation:

```9:37:apps/lazuar-api/Modules/Commerce/Infrastructure/Security/MagicLinkTokenService.cs
/// HMAC-SHA256 portal tokens: Base64("{subscriptionId}:{expiryUnix}:{hmacHex}"), 24h TTL.
...
    public string GenerateToken(Guid subscriptionId)
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();
        var payload = $"{subscriptionId}:{expiry}";
        var hash = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_secret),
            Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
```

No-token path is the email form (`portal/page.tsx` 54–63), not a session. Header still says “Buyer Dashboard” / default name “Member” (`portal/layout.tsx` 15, 22–26).

### Related files
- `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` — the banner.
- `apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx` — “Member” / Buyer Dashboard chrome.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Security/MagicLinkTokenService.cs` — 24h HMAC.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicPortalEndpoints.cs` — GET is token-gated.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/MagicLinkTokenServiceTests.cs` — format, expiry, tamper; not UI copy.
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/PortalAccessEmailHandlers.cs` — mints the link.
- Issues 021 (success never mints a token), 022 (cookie portal 404) — how buyers arrive here.

### Tests
- Existing tests that touch this path: `MagicLinkTokenServiceTests` (`GenerateToken_WireFormat_IsBase64OfGuidExpiryHmacHex`, `ValidateToken_Expired_ReturnsNull`, tamper/wrong secret). `PortalAccessEmailHandlerTests.RequestMagicLink_MatchingEmail_Dispatches`. No portal page test for the banner.
- Whether any test would fail if the bug is still there: **No.** HMAC tests will stay green while the banner still says Verified.
- What a first regression test should assert: the painted string is not “Identity Verified” (e.g. “Link expires in 24 hours” / “Signed with email link”). A grep/fixture that `portal/page.tsx` does not contain `Identity Verified`.

### Reproduction today
Request a magic link (or copy `?token=` from a dunning/fulfillment mail). Open `/{slug}/portal?token=…` on a fresh browser with no `lazuar_auth` cookie. Assert: green “Identity Verified”. Decode the token (base64) and confirm `{guid}:{unix}:{64-hex}` with unix ≈ now+24h (`MagicLinkTokenServiceTests` documents this). Wait until expiry or truncate the token: `notFound()`, no banner.

### Blast radius
Every buyer who opens a portal magic link. Honesty / security theater: they may think they completed a login and leave the tab shared. The URL *is* the credential for 24h (cancel, plan change, documents). PII on the page: product names, amounts, document numbers. Frequency: every portal visit that works. Not a new auth bypass — it is lying chrome on the existing HMAC.

### Suggested fix
Replace the banner with time-bounded copy: “Signed in with an email link. This link expires 24 hours after it was sent.” Do not imply a password or cookie session. Do not mint a long-lived cookie here (that is U02, a different ticket, and must stay token-gated until that GET contract changes). No TypeSpec regen, no Stripe Billing.

### Evaluation notes
009 copy table already listed “Identity Verified” vs HMAC. Adjacent U01/U02/U03 still control whether the buyer *has* a token. Severity still P2 (copy). Not blocked. Do not “fix” it by showing the banner only when `GET /one/auth/me` succeeds — that would hide the banner from almost every real buyer.

