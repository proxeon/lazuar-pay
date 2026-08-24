# 02 — Merchant frontend (`:5178`) vs Pay gateway APIs

**Date:** 24 August 2026  
**Slice:** `apps/lazuar-pay-merchant` connection to the focused Pay host on **8081**. What `WorkspacePage` actually PUTs versus what `GatewayEndpoints.Put` / `PutGatewayRequest` requires. Role chrome. Wrap-copy honesty. Secrets not in Vite.  
**Not:** an implementation. Live files below are the authority; 015 U10–U21 checklists are a map and were spot-checked against that live code.

## Provenance

Recorded from the repo on 24 August 2026:

| | |
|---|---|
| Branch | `feat/015-four-adapters` (`.git/HEAD` → `ref: refs/heads/feat/015-four-adapters`) |
| HEAD | `c621ceba7fc7b79f16954d0819200cb21db6f22b` |
| Subject | `docs(015): check off implemented T–Q phases` (`.git/COMMIT_EDITMSG`) |
| Body | `A99.1 lived dogfood stays open. Parked files stay parked. Hermetic task pay:test is 58 green.` |
| Parent index | [README.md](./README.md) recorded the same HEAD at analysis start as `c621ceba` |

Opened for this file (entire, or the cited regions):

- `apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx` (entire, 310 lines)
- `apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `apps/lazuar-pay-merchant/src/lib/roles.ts`
- `apps/lazuar-pay-merchant/src/locks.test.ts`
- `apps/lazuar-pay-merchant/package.json`
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` including `PutGatewayRequest`
- `plans/015-four-adapters/checklists/u10-provider-select.md` through `u21-active-provider-shown.md`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` (`Vite_apps_do_not_use_hub_types`)
- `plans/016-adapters-check/README.md`

Also opened because the PUT body, GET shape, error mapping, role chrome, wrap copy, and Vite env cannot be judged from `WorkspacePage` alone: `PayProviders.cs`, `MemberGate.cs`, `PayErrors.cs`, `Program.cs` JSON options, `CatalogEndpoints.cs`, `CheckoutEndpoints.cs` + `CreateCheckoutRequest.cs` + `CheckoutSession.cs`, `PaymentQueryEndpoints.cs`, `WhoamiEndpoints.cs` + `WhoamiResponse.cs`, `RazorpayHosted.TrySplit`, `ChipWebhook.Parse` (PEM), `BillplzHosted` environment/callback, `GatewayTests.cs`, `Rows.cs` `GatewayCredentialRow`, merchant `oidcConfig.ts`, `.env.example`, `oneApi.ts`, `HomePage.tsx`, `CreateWorkspacePage.tsx`, `LoginPage.tsx`, `RequireAuth.tsx`, `bearerToken.ts`, `App.tsx`, `vite.config.ts`, `vitest.config.ts`, `scripts/register-spa.sh`, `README.md`, host `.env.example` (`Pay__PublicBaseUrl`), and 015 P11–P16 / C11 / B11 / R11 / X11 / S12 / S18 / T15 / T18 / C28 / B15 / H17 / H18.

---

## 1. How `:5178` reaches `:8081`

`payApi.ts` pins the host origin to one Vite public env, with a localhost default. It is **not** Hub `:8080`.

```1:53:apps/lazuar-pay-merchant/src/lib/payApi.ts
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

export type WhoamiTenant = {
  id: string
  slug?: string
  name?: string
  role?: string
  status?: string
}

export type Whoami = {
  user_id: string
  email?: string
  is_platform_admin: boolean
  active_org_id?: string
  tenants: WhoamiTenant[]
}

/** credentials omitted on purpose: localhost cookies are not port-scoped. */
export async function getWhoami(
  accessToken: string,
  orgHint?: string | null,
): Promise<Whoami> {
  const headers: Record<string, string> = {
    Authorization: `Bearer ${accessToken}`,
    Accept: 'application/json',
  }
  if (orgHint) {
    headers['X-Lazuar-Tenant-Id'] = orgHint
  }
  const response = await fetch(`${payApi}/v1/whoami`, { headers })
  if (response.status === 401) {
    throw new Error('unauthorized')
  }
  if (!response.ok) {
    throw new Error(`whoami ${response.status}`)
  }
  return (await response.json()) as Whoami
}

export async function payFetch(
  accessToken: string,
  path: string,
  init?: RequestInit & { orgHint?: string },
): Promise<Response> {
  const headers = new Headers(init?.headers)
  headers.set('Authorization', `Bearer ${accessToken}`)
  headers.set('Accept', 'application/json')
  if (init?.orgHint) headers.set('X-Lazuar-Tenant-Id', init.orgHint)
  return fetch(`${payApi}${path}`, { ...init, headers })
}

export { payApi }
```

Facts locked by this file:

- Base URL is `VITE_PAY_API_URL` or `http://localhost:8081`. That string is also interpolated into the webhook-URL hint on `WorkspacePage` (see §8 and §11).
- Every Pay call from this SPA is `fetch` with an explicit `Authorization: Bearer …` header. `credentials` is left at the fetch default (`same-origin`). The comment is the design: localhost cookies are not port-scoped, so this SPA must not ride Hub / One cookies.
- `X-Lazuar-Tenant-Id` is set when `orgHint` is passed. `WorkspacePage` always passes `orgHint: orgId` on org-scoped Pay calls. `HomePage` calls `getWhoami(token)` **without** a hint.
- `getWhoami` maps HTTP 401 to the thrown string `'unauthorized'` and every other non-OK to `` `whoami ${status}` ``. It never reads `PayErrors` JSON (`status` / `title` / `detail`). `payFetch` does **not** interpret status at all; callers do.

Bearer selection is `pickApiBearerToken` — JWT `access_token` only, never `id_token`, never an opaque access token:

```10:18:apps/lazuar-pay-merchant/src/auth/bearerToken.ts
/**
 * Pick a Bearer token for Pay / One APIs.
 * Send only a JWT access_token. Never send id_token (not an API credential).
 */
export function pickApiBearerToken(user: User | null | undefined): string | undefined {
  if (!user) return undefined
  if (isJwtLike(user.access_token)) return user.access_token
  return undefined
}
```

If Zitadel/One ever issued an opaque access token, `WorkspacePage` would see `token === undefined`, skip the `useEffect` whoami/refresh, leave `tenant` null, and render the member chrome with role `…`. That is not a gateway-field bug, but it is how this SPA can look “empty” without an error banner.

Host JSON is snake_case, case-insensitive — so merchant `webhook_secret` binds to `PutGatewayRequest.WebhookSecret`:

```14:18:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
```

CORS on the host allows the merchant origin explicitly (`http://localhost:5178` and `http://127.0.0.1:5178`). Ops `:3003` is not on that list.

`vite.config.ts` dual-pins `port: 5178` with `strictPort: true`, matching `package.json` `"dev": "vite --port=5178 --host=0.0.0.0 --strictPort"`. Preview is 4178. The merchant SPA cannot silently steal One login `:5175` or checkout `:5179`.

---

## 2. Complete API-call inventory from `lazuar-pay-merchant`

There is **no** generated client and **no** `@repo/api-types-ts`. Every Pay HTTP call is a string path in a page or `payApi.ts`.

### 2.1 `WorkspacePage` — the gateway surface

Route: `/o/:orgId` (`App.tsx`). All of the following fire only after `pickApiBearerToken` yields a JWT.

| # | When | Method | Path | Headers beyond Bearer/Accept | Body | Response used? |
|---|------|--------|------|------------------------------|------|----------------|
| W1 | `useEffect` on `orgId`/`token` | `GET` | `/v1/whoami` | `X-Lazuar-Tenant-Id: {orgId}` | none | `tenants[]` to find membership + `role` |
| W2 | `refresh()` after whoami match, after successful PUT, after successful pay-link | `GET` | `/v1/orgs/{orgId}/products` | `X-Lazuar-Tenant-Id` | none | if `ok`, `setProducts`. **If not ok, silent.** |
| W3 | same | `GET` | `/v1/orgs/{orgId}/payments` | `X-Lazuar-Tenant-Id` | none | if `ok`, `setPayments`. **If not ok, silent.** |
| W4 | same | `GET` | `/v1/orgs/{orgId}/receipts` | `X-Lazuar-Tenant-Id` | none | if `ok`, `setReceipts`. **If not ok, silent.** |
| W5 | same | `GET` | `/v1/orgs/{orgId}/gateway` | `X-Lazuar-Tenant-Id` | none | if `ok`, `setGateway` and maybe `setProvider`. **No `?provider=`.** |
| W6 | writer clicks Save key | `PUT` | `/v1/orgs/{orgId}/gateway` | `X-Lazuar-Tenant-Id`, `Content-Type: application/json` | see §4 | status only; body discarded |
| W7 | writer clicks Create pay link | `POST` | `/v1/orgs/{orgId}/products` | `X-Lazuar-Tenant-Id`, `Content-Type: application/json` | `{ name, amount, currency: "MYR" }` | status only; **created `id` unused** |
| W8 | after W7 ok | `POST` | `/v1/checkouts` | `X-Lazuar-Tenant-Id`, `Content-Type: application/json` | `{ org_id, amount, currency: "MYR" }` | `public_token` → hardcoded `http://localhost:5179/c/{token}` |

W5–W6 are the gateway contract this slice is for. W7–W8 are the only other writes from this page; they are not the PUT, but they are how a saved rail is supposed to become a buyer URL, and they have their own mismatches with the host (§14).

The page also **prints** a webhook URL. That is not an HTTP call:

```249:254:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
          <p>
            Webhook URL:{' '}
            <code>
              {payApi}/v1/webhooks/{provider}/{orgId}
            </code>
          </p>
```

`POST /v1/webhooks/{provider}/{orgId}` is a PSP callback into the host. The merchant SPA never POSTs it.

### 2.2 Other merchant pages (Pay / One, not gateway PUT)

| Page | Call | Host | Notes |
|------|------|------|-------|
| `HomePage` | `GET {payApi}/v1/whoami` | Pay 8081 | no tenant hint; 401 → `signinRedirect`; other errors render “Whoami failed” |
| `CreateWorkspacePage` | `POST {oneApi}/tenants` `{ name, slug }` | **One** `VITE_ONE_API_URL` default `http://localhost:8080/api/v1` | not Pay; tenant id becomes Pay `org_id`; error `` `create tenant ${status}` `` |
| `LoginPage` | none | — | OIDC redirect; refuses to start if `VITE_ZITADEL_CLIENT_ID` is empty |
| `CallbackPage` | none | — | OIDC callback; `takeReturnTo()` |

`oneApi.ts` is the only non-8081 write in the SPA. It is workspace create, not keys.

### 2.3 Pay routes the merchant SPA never calls

These exist on 8081 and are relevant to the same staff job, but `:5178` does not touch them:

| Host route | Why it matters | Merchant use |
|------------|----------------|--------------|
| `GET /v1/orgs/{orgId}/gateway?provider={name}` | P15: inspect a **non-active** credential row without switching `active_provider` | **unused.** Default GET only. |
| `GET /v1/checkouts/{id}` | member can read a minted session | unused (W8 only reads the create JSON) |
| `GET /v1/orgs/{orgId}/receipts/{id}` | receipt detail | unused (list only) |
| `GET /v1/orgs/{orgId}/ready` | org-ready probe | unused |
| `GET /v1/pay/{token}` / `POST /v1/pay/{token}/start` | buyer start | checkout `:5179`, not merchant |
| `PUT` anything except gateway | — | no tax PUT (T15). no “activate rail” endpoint (P13: last PUT wins) |

---

## 3. Host PUT contract (what `PutGatewayRequest` actually is)

```203:212:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
public sealed class PutGatewayRequest
{
    public string? Provider { get; set; }
    public string? Secret { get; set; }
    public string? WebhookSecret { get; set; }
    public string? PublicMerchantId { get; set; }
    public string? Environment { get; set; }
    public string? KeyId { get; set; }
    public string? KeySecret { get; set; }
}
```

Snake_case JSON keys the host will bind:

`provider`, `secret`, `webhook_secret`, `public_merchant_id`, `environment`, `key_id`, `key_secret`.

P11.1 documented the first five. `key_id` / `key_secret` are extra host fields added so Razorpay can be sent unjoined (R11: “Accept either `secret` = `key_id:key_secret` **or** two fields `key_id` + `key_secret` joined with `:` before Protect”). The merchant SPA uses the first form only. It never serializes `key_id` or `key_secret` as JSON keys.

`Put` in full, because every merchant JSON choice is judged against this method, not against the checklist tick boxes:

```16:145:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
    static async Task<IResult> Put(
        string orgId,
        PutGatewayRequest? body,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        SecretBox box,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        if (!PayProviders.TryNormalize(body?.Provider, out var provider))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        var secret = body?.Secret?.Trim();
        if (string.IsNullOrWhiteSpace(secret)
            && !string.IsNullOrWhiteSpace(body?.KeyId)
            && !string.IsNullOrWhiteSpace(body?.KeySecret))
        {
            secret = body.KeyId.Trim() + ":" + body.KeySecret.Trim();
        }

        var webhookSecret = body?.WebhookSecret?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return PayErrors.Status(400, "Bad Request", "secret is required");
        }

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return PayErrors.Status(400, "Bad Request", "webhook_secret is required");
        }

        var publicId = body?.PublicMerchantId?.Trim();
        if (PayProviders.RequiresPublicMerchantId(provider) && string.IsNullOrWhiteSpace(publicId))
        {
            return PayErrors.Status(400, "Bad Request", "public_merchant_id is required");
        }

        if (!PayProviders.AllowsPublicMerchantId(provider) && !string.IsNullOrWhiteSpace(publicId))
        {
            return PayErrors.Status(400, "Bad Request", "public_merchant_id is not used for this provider");
        }

        var environment = string.IsNullOrWhiteSpace(body?.Environment) ? "test" : body.Environment.Trim().ToLowerInvariant();
        if (environment is not ("test" or "live"))
        {
            return PayErrors.Status(400, "Bad Request", "environment must be test or live");
        }

        if (provider == PayProviders.Billplz && string.IsNullOrWhiteSpace(body?.Environment))
        {
            return PayErrors.Status(400, "Bad Request", "environment is required");
        }

        if (provider == PayProviders.Razorpay && !RazorpayHosted.TrySplit(secret, out _, out _))
        {
            return PayErrors.Status(400, "Bad Request", "secret must be key_id:key_secret");
        }
        // ... Protect both secrets, upsert GatewayCredentialRow, set OrgSettings.ActiveProvider,
        // audit gateway.credentials.upsert, return GatewayJson ...
```

`PayProviders` rules the PUT uses:

```21:29:apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs
    public static bool RequiresPublicMerchantId(string provider) =>
        provider is Chip or Billplz;

    public static bool RequiresEmail(string provider) =>
        provider is not Stripe;

    public static bool AllowsPublicMerchantId(string provider) =>
        RequiresPublicMerchantId(provider);
```

`RequiresEmail` is a **start** rule (`POST /v1/pay/{token}/start`), not a PUT rule. The merchant form never collects buyer email. That is checkout `:5179`.

Allow-list: `stripe | chip | billplz | xendit | razorpay`, trimmed + lowercased. Unknown → 400 `"unknown provider"`. The merchant `<select>` only offers those five, so a normal click cannot send Hub `STRIPE` / `gatewayType`.

Writer gate (H18), independent of the SPA chrome:

```42:68:apps/lazuar-pay/src/Lazuar.Pay/One/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync(...)
    {
        var denied = await RequireMemberAsync(...);
        // ...
        var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
        if (role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }
        return null;
    }
```

Hermetic lock: `GatewayTests.Member_cannot_put_gateway` PUTs stripe keys as role `member` and asserts 403. `GatewayTests.Put_requires_webhook_secret` PUTs `{"provider":"stripe","secret":"sk_test_dummy"}` **without** `webhook_secret` and asserts 400. That second test is the host-side fact the UI must not paper over: **webhook_secret is required on every PUT, for every rail, including Stripe.** Omit and empty both fail `IsNullOrWhiteSpace`.

Error JSON the SPA never parses:

```1:7:apps/lazuar-pay/src/Lazuar.Pay/One/PayErrors.cs
internal static class PayErrors
{
    public static IResult Status(int status, string title, string detail) =>
        Results.Json(new { status, title, detail }, statusCode: status);
}
```

PUT 400 bodies the merchant can provoke, with `detail` strings:

| `detail` | When |
|----------|------|
| `unknown provider` | `provider` missing / not in the five |
| `secret is required` | no `secret` and no joinable `key_id`+`key_secret` |
| `webhook_secret is required` | missing or whitespace. **Always. All five rails.** |
| `public_merchant_id is required` | `chip` or `billplz` and Brand/Collection blank |
| `public_merchant_id is not used for this provider` | stripe / xendit / razorpay with a non-empty `public_merchant_id` |
| `environment must be test or live` | any other string |
| `environment is required` | **billplz only**, when `environment` is omitted (empty-object vs default) |
| `secret must be key_id:key_secret` | razorpay secret does not split on a middle `:` |
| `Writer role required` | 403, member curl |
| `Not a member of this org` | 403, membership check |
| `Missing bearer token` | 401 |

`environment` defaulting is easy to misread. For non-billplz, omitted `environment` becomes `"test"`. For billplz, omitted `environment` is 400 **even though** the next line would have defaulted it to `"test"` if the billplz-specific check were absent. Sending `"test"` explicitly is required for billplz. The merchant select always has a value, so a click on Save with Billplz selected does send it — **if** they have not navigated away from the billplz branch of the form. See §6.

---

## 4. What `WorkspacePage.pasteKey` actually serializes

```89:120:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
  async function pasteKey() {
    if (!token || !write) return
    const payload: Record<string, string> = {
      provider,
      webhook_secret: webhookSecret,
    }
    if (provider === 'razorpay') {
      payload.secret = `${keyId}:${keySecret}`
    } else {
      payload.secret = secret
    }
    if (provider === 'chip' || provider === 'billplz') {
      payload.public_merchant_id = publicMerchantId
    }
    if (provider === 'billplz') {
      payload.environment = environment
    }
    const response = await payFetch(token, `/v1/orgs/${orgId}/gateway`, {
      method: 'PUT',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })
    if (!response.ok) setError(`keys ${response.status}`)
    else {
      setError(null)
      setSecret('')
      setWebhookSecret('')
      setKeySecret('')
      await refresh(token)
    }
  }
```

Client-side gates before the wire:

- No JWT or `canWriteMoney` false → **return with no error and no PUT.** The member chrome already hid the button (U16). A writer with a dropped token also fails closed silently.
- **No trim. No required-field check. No PEM shape check. No `sk_` / `whsec_` prefix check. No razorpay colon check.** Empty strings go on the wire.

On success the SPA clears `secret`, `webhookSecret`, and `keySecret`. It does **not** clear `keyId`, `publicMerchantId`, or `environment`. `keyId` lingering after a razorpay save is a small UX leak (the id is not a vault secret, but it is still a credential identifier sitting in a text box). `publicMerchantId` lingering is convenient if they re-save, and dangerous if they switch chip → billplz and save without noticing the same box is now “Collection ID”.

On failure: `` setError(`keys ${response.status}`) ``. The host’s `detail` is thrown away. A missing webhook secret, a missing Brand ID, a razorpay join failure, and a member 403 all look like `keys 400` or `keys 403`.

`JSON.stringify` on `Record<string, string>` emits only keys that were assigned. That is how the SPA avoids the host 400 `"public_merchant_id is not used for this provider"`: leftover React state for Brand ID is **not** attached when provider is stripe/xendit/razorpay.

---

## 5. JSON body per provider (what Save key sends)

Assume the React state names below. Quotes are the exact keys. Values are whatever is in the box, including `""`.

### 5.1 Stripe (`provider === 'stripe'`)

Form visible: one `<input>` (placeholder `sk_test_…`) bound to `secret`; one `<input>` (placeholder `whsec_… (endpoint signing secret)`) bound to `webhookSecret`. No Brand ID. No environment select. No key_id.

```json
{
  "provider": "stripe",
  "webhook_secret": "<webhookSecret>",
  "secret": "<secret>"
}
```

Not sent: `public_merchant_id`, `environment`, `key_id`, `key_secret`.

Host after this body:

- `webhook_secret` empty → 400 `"webhook_secret is required"` (P12 / `GatewayTests.Put_requires_webhook_secret`). The UI always includes the key, so this is empty-string, not omit — same 400.
- `environment` omitted → stored as `"test"` even if the pasted key is `sk_live_…`. S12 says Stripe live vs test is the key prefix; the column is still written `test`. GET will return `"environment":"test"` for a live key. The SPA never shows it, so the lie stays in the database rather than on screen.
- `last4` = last four of the **API key**, not of `whsec_`.
- `active_provider` becomes `stripe` (P13).
- Response `capability` is always `"hosted_link"`.

U11.1 said: “Save calls PUT `{ provider: "stripe", secret, webhook_secret }`.” Live matches that object shape.

U11.1 also said: “Inputs: API key (`sk_test_` / `sk_live_`)” and “Labels say Dashboard **endpoint** signing secret”. Live placeholder is only `sk_test_…`. There is **no** `<label>` on either secret box; the only `<label>` on the page is “Provider”. The webhook placeholder does contain `(endpoint signing secret)`, which is the closest live match to the “label” requirement. Checklist U11 is ticked `[x]` anyway.

### 5.2 CHIP (`provider === 'chip'`)

Form visible: API-secret `<input>` (placeholder `API secret`); webhook `<input>` (placeholder `PEM from CHIP dashboard`); Brand ID `<input>` (placeholder `Brand ID`). No environment select.

```json
{
  "provider": "chip",
  "webhook_secret": "<webhookSecret>",
  "secret": "<secret>",
  "public_merchant_id": "<publicMerchantId>"
}
```

Not sent: `environment` (host defaults `"test"`), `key_id`, `key_secret`.

Host after this body:

- Blank Brand ID → 400 `"public_merchant_id is required"`. Locked hermetically by `GatewayTests.Chip_put_requires_brand_id`, which sends PEM **without** `public_merchant_id` and expects 400.
- Blank PEM → 400 `"webhook_secret is required"`. Same as stripe. Host does **not** parse PEM on PUT; it `Protect`s whatever string arrived. Verify is later: `ChipWebhook.Parse` does `rsa.ImportFromPem(pem)` on the unwrapped ciphertext. A one-line truncated PEM stores “successfully” and then fails every CHIP webhook with `"invalid signature"`.
- `environment` omitted → `"test"`. CHIP test vs live is a dashboard toggle (S12); Pay does not switch CHIP hosts from this column.

**PEM control is `<input>`, not `<textarea>`.** U12.1 is explicit: “Webhook public key PEM (textarea)”. Live:

```213:229:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
          <p>
            <input
              value={webhookSecret}
              onChange={(e) => setWebhookSecret(e.target.value)}
              autoComplete="off"
              placeholder={
                provider === 'stripe'
                  ? 'whsec_… (endpoint signing secret)'
                  : provider === 'chip'
                    ? 'PEM from CHIP dashboard'
                    : provider === 'billplz'
                      ? 'X-Signature secret'
                      : provider === 'xendit'
                        ? 'x-callback-token'
                        : 'webhook secret'
              }
            />
          </p>
```

There is **zero** `<textarea>` in `apps/lazuar-pay-merchant/src`. A `type=text` (the default) input cannot hold U+000A. Pasting a PEM with `-----BEGIN PUBLIC KEY-----` + newlines + body + `-----END PUBLIC KEY-----` keeps the first line or collapses newlines, depending on the browser. `RSA.ImportFromPem` in `ChipWebhook` needs a real PEM:

```25:44:apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipWebhook.cs
        var pem = box.Unprotect(cred.WebhookCiphertext);
        // ...
        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (Exception)
        {
            throw new PspVerifyException("invalid signature");
        }
```

This is the sharpest merchant↔host mismatch in the CHIP field set. The 015 checklist U12 is ticked `[x]` for textarea. Live code does not have it. A staff member can click Save, see the error banner clear, see `Active rail: chip · last4 … · hosted_link`, and still have a webhook secret that cannot verify.

Webhook URL hint for CHIP is `{payApi}/v1/webhooks/chip/{orgId}` i.e. `http://localhost:8081/v1/webhooks/chip/{orgId}` in default env. U12.1 asked for `https://{public}/v1/webhooks/chip/{orgId}` using the Pay public origin, not Hub. Live uses the Vite API origin, which is the loopback host, not `Pay:PublicBaseUrl`. CHIP’s dashboard cannot POST loopback any more than Billplz can. Billplz copy warns; CHIP copy does not (see §11).

### 5.3 Billplz (`provider === 'billplz'`)

Form visible: API-secret `<input>`; X-Signature `<input>`; Collection ID `<input>` (placeholder `Collection ID`); environment `<select>` `test (sandbox)` | `live`.

```json
{
  "provider": "billplz",
  "webhook_secret": "<webhookSecret>",
  "secret": "<secret>",
  "public_merchant_id": "<publicMerchantId>",
  "environment": "test"
}
```

or `"environment": "live"`.

This is the only rail where the SPA sends `environment`. That matches B11 / P11: billplz **requires** `environment`; the host 400s if the key is omitted. Because the select always has a value, a Save click from this field set satisfies the host **for the current React state**, which is initialized to `'test'` and is **never written from GET** (see §6 and §7).

Host uses `environment` to pick the Billplz API host at start time, not at PUT time:

```35:38:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs
        var host = string.Equals(cred.Environment, "live", StringComparison.OrdinalIgnoreCase)
            ? "https://www.billplz.com/api/v3/"
            : "https://www.billplz-sandbox.com/api/v3/";
        var callback = $"{publicBase}/v1/webhooks/billplz/{checkout.OrgId}?checkout_id={Uri.EscapeDataString(checkout.Id)}";
```

Default local `Pay:PublicBaseUrl` is loopback; B15 fail-closes start with a “callback base not public” style error. The merchant copy for billplz says “Callback must be public https (localhost will fail).” That sentence is honest for **start**, not for PUT (PUT will 200 with localhost keys). The printed webhook URL is still `{payApi}/v1/webhooks/billplz/{orgId}` = `http://localhost:8081/...`, which is exactly the URL Billplz will refuse. The **actual** callback the host sends to Billplz is `Pay:PublicBaseUrl` plus a `checkout_id` query. Staff who copy the hint into the Billplz dashboard are copying a different origin than start will register, and they are copying a URL **without** `?checkout_id=`, which B16 cares about on the webhook side. The hint is a path template, not the callback start emits.

### 5.4 Xendit (`provider === 'xendit'`)

Form visible: API-secret `<input>`; callback-token `<input>` (placeholder `x-callback-token`). No Brand ID (X11: reject `public_merchant_id`). No environment select.

```json
{
  "provider": "xendit",
  "webhook_secret": "<webhookSecret>",
  "secret": "<secret>"
}
```

Host: `webhook_secret` is the `x-callback-token` value (X11 / X14). Empty → 400. Non-empty `public_merchant_id` would 400; the SPA does not send it. `environment` omitted → `"test"` and ignored at HTTP time (Xendit host is not switched from this column).

### 5.5 Razorpay (`provider === 'razorpay'`)

Form visible: two `<input>`s (`placeholder="key_id"`, `placeholder="key_secret"`) **instead of** the generic secret box; plus the shared webhook `<input>` (placeholder `webhook secret`).

```json
{
  "provider": "razorpay",
  "webhook_secret": "<webhookSecret>",
  "secret": "<keyId>:<keySecret>"
}
```

**Not sent:**

```json
{
  "key_id": "<keyId>",
  "key_secret": "<keySecret>"
}
```

Join is entirely client-side: `` payload.secret = `${keyId}:${keySecret}` ``. U15.1 “PUT joins key_id:key_secret (R11)” is true **in the SPA**. The host’s alternate join (empty `secret` + both `KeyId` and `KeySecret`) is dead code from this UI.

Host then splits on the **first** colon:

```80:93:apps/lazuar-pay/src/Lazuar.Pay/Gateways/RazorpayHosted.cs
    internal static bool TrySplit(string secret, out string keyId, out string keySecret)
    {
        keyId = "";
        keySecret = "";
        var i = secret.IndexOf(':');
        if (i <= 0 || i == secret.Length - 1)
        {
            return false;
        }

        keyId = secret[..i];
        keySecret = secret[(i + 1)..];
        return true;
    }
```

Failure modes the SPA will turn into `keys 400`:

| State | `secret` sent | Host |
|-------|---------------|------|
| both boxes empty | `":"` | `TrySplit` sees `i == 0` → 400 `"secret must be key_id:key_secret"` (secret is not whitespace, so the `"secret is required"` branch is skipped) |
| only `key_id` | `"rzp_test_xxx:"` | `i == length-1` → same 400 |
| only `key_secret` | `":sk_…"` | `i == 0` → same 400 |
| both filled, no extra colon | `"rzp_test_xxx:sk_live_yyy"` | 200; `last4` = last four of **key_id**, not of key_secret (GatewayEndpoints special case) |
| `key_id` itself contains `:` | join still uses one `:`; split takes the first | key_id truncated, key_secret includes the rest |

The SPA does not trim. Host trims the **whole** `secret` then splits, so leading/trailing spaces on the joined string are stripped, but spaces around the colon inside the boxes survive.

Razorpay tests on the host seed `{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}` — the joined form, which is what the SPA sends. There is no hermetic test that POSTs `key_id` + `key_secret` without `secret`.

---

## 6. Missing / unused PUT fields (the three named holes)

### 6.1 `key_id` / `key_secret` JSON keys

`PutGatewayRequest` has them. Merchant never sends them. Functionally OK **if** the client-side join is non-empty on both sides. The hole is:

- Empty join `" : "` / `":"` still sends `secret`, so the host’s “if secret blank, join KeyId+KeySecret” path never runs.
- Staff cannot paste a pre-joined `key_id:key_secret` into one box: the razorpay branch **hides** the generic secret input. A Dashboard “key” copy that is already joined has to be split by the human into two boxes, then the SPA re-joins. That is workable, not documented on screen.
- Host last4 for razorpay is key_id last4. After save, the SPA shows GET `last4`. That is correct **after refresh**. During the same click, it does not locally compute last4.

### 6.2 `environment`

| Rail | SPA sends `environment`? | Host if omitted | SPA shows stored value? |
|------|--------------------------|-----------------|-------------------------|
| stripe | no | default `test` | no |
| chip | no | default `test` | no |
| billplz | **yes**, from `<select>` defaulting to `'test'` | 400 if omitted | **no — select is not hydrated from GET** |
| xendit | no | default `test` | no |
| razorpay | no | default `test` | no |

The GET JSON **always** includes `environment` (`GatewayJson`). The `Gateway` TypeScript type includes `environment?: string`. `refresh()` never calls `setEnvironment(body.environment)`. Full page load therefore always shows Billplz as `test (sandbox)` even when the row is `live`.

Re-save bug, concrete:

1. Writer selects billplz, environment `live`, pastes secrets + collection, Save. Host stores `environment=live`, `active_provider=billplz`.
2. Reload `/o/{orgId}`. `useState('test')` runs. GET returns `environment: "live"`. UI select shows **test (sandbox)**. Active rail label shows `billplz` + last4, not environment.
3. Writer pastes rotated secrets (required on every PUT — P12 prefers require-both) and clicks Save without touching the select.
4. PUT body includes `"environment":"test"`. Host overwrites live → test. Subsequent start hits `billplz-sandbox.com` with whatever key they just pasted.

That is a real host/UI mismatch, not a checklist nit. S12’s whole point is “Do not infer live from hostname; send test|live.” The SPA has the control and then forgets the stored value.

For non-billplz, never sending `environment` is aligned with “optional, default test.” Showing a live Stripe org as environment test in GET is a host-column quirk the SPA currently hides.

### 6.3 PEM textarea vs `<input>`

Covered in §5.2. Restated because the task named it: U12 required a textarea; live is the same single-line `<input>` used for `whsec_` and `x-callback-token`. `locks.test.ts` does not assert `textarea`. IsolationTests does not look at merchant TSX. The 015 box is checked. Live is not a textarea.

---

## 7. GET `/v1/orgs/{orgId}/gateway` — called, mostly unused

Host GET:

```147:200:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
    static async Task<IResult> Get(
        string orgId,
        string? provider,
        ...)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        // empty query → OrgSettings.ActiveProvider
        // ?provider= → normalize, 400 unknown, do not change active
        // no name → { org_id, configured: false }
        // name but no row → { org_id, provider, configured: false }
        // row → GatewayJson
    }

    static object GatewayJson(string orgId, GatewayCredentialRow row, bool configured) => new
    {
        org_id = orgId,
        provider = row.Provider,
        last4 = row.Last4,
        configured,
        capability = PayProviders.Capability,
        public_merchant_id = row.PublicMerchantId,
        environment = row.Environment,
        webhook_configured = !string.IsNullOrWhiteSpace(row.WebhookCiphertext)
    };
```

S18: this JSON must never contain `secret`, `ciphertext`, `webhook_secret`, PEM, `sk_`. `GatewayTests.Put_and_get_does_not_echo_secret` asserts the PUT/GET bodies do not contain the plaintext. Member GET is `RequireMemberAsync` (200 for member). Writer-only is PUT.

Merchant call:

```55:71:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
  async function refresh(access: string) {
    const [plist, pay, rec, gw] = await Promise.all([
      payFetch(access, `/v1/orgs/${orgId}/products`, { orgHint: orgId }),
      payFetch(access, `/v1/orgs/${orgId}/payments`, { orgHint: orgId }),
      payFetch(access, `/v1/orgs/${orgId}/receipts`, { orgHint: orgId }),
      payFetch(access, `/v1/orgs/${orgId}/gateway`, { orgHint: orgId }),
    ])
    // ...
    if (gw.ok) {
      const body = (await gw.json()) as Gateway
      setGateway(body)
      if (body.provider && rails.includes(body.provider as (typeof rails)[number])) {
        setProvider(body.provider as (typeof rails)[number])
      }
    }
  }
```

### 7.1 Query `?provider=` (P15) — unused

The SPA never appends `?provider=chip` (or any name). Support cannot inspect a non-active row from this UI. After a chip→stripe switch, the chip row may still exist (P13: old row remains) but GET-without-query returns stripe. The select snaps to GET `provider` (the active one). There is no UI to GET the parked chip row. P15 is a host feature with zero merchant consumer. Host tests also have **no** `gateway?provider=` case (grep of `apps/lazuar-pay/tests` is empty for that query).

### 7.2 Response fields the SPA types but does not render / does not hydrate

`Gateway` type:

```12:20:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
type Gateway = {
  provider?: string
  last4?: string
  configured?: boolean
  capability?: string
  public_merchant_id?: string
  environment?: string
  webhook_configured?: boolean
}
```

Rendered:

```161:166:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
      <p>
        Active rail:{' '}
        <code>{gateway?.configured ? gateway.provider : 'none'}</code>
        {gateway?.last4 ? ` · last4 ${gateway.last4}` : ''}
        {gateway?.capability ? ` · ${gateway.capability}` : ''}
      </p>
```

| JSON field | In `Gateway` type? | Written into form state? | Shown on screen? |
|------------|--------------------|--------------------------|------------------|
| `org_id` | **no** (dropped) | no | no |
| `provider` | yes | `setProvider` if in `rails` | yes, **only if** `configured` |
| `last4` | yes | no | yes, if truthy |
| `configured` | yes | no | drives `'none'` vs name |
| `capability` | yes | no | yes, if truthy (`hosted_link`) |
| `public_merchant_id` | yes | **no `setPublicMerchantId`** | **no** (C11.1 “GET may show Brand ID” is unused) |
| `environment` | yes | **no `setEnvironment`** | **no** |
| `webhook_configured` | yes | no | **no** |

So a member (U17) sees provider + last4 + capability when configured, and `none` otherwise. They cannot see whether a webhook secret is stored, whether the Brand/Collection id is present, or whether Billplz is live. U17.1 listed only “provider, last4, configured, capability” — live matches that **minimum**. The host sends more; the SPA discards it.

`configured: false` with a `provider` (active setting, missing row) still runs `setProvider`. The label shows `none` (because `configured` is false) while the writer select jumps to that rail. Two different stories on one page.

GET failure (`gw.ok === false`) is silent: previous `gateway` state remains, or stays `null` (label `none`). No `keys 403` / `whoami` style banner. A member who is 403 on GET (should not happen if whoami matched) would see an empty rail with no explanation.

PUT success body is also `GatewayJson`. The SPA ignores it and re-GETs via `refresh`. Extra round-trip; not a contract bug.

---

## 8. Error mapping (SPA vs `PayErrors`)

There is no shared error type. Mapping is status interpolation plus a few special strings.

| Call | Non-OK handling | Host `detail` shown? |
|------|-----------------|----------------------|
| `getWhoami` 401 | throw `'unauthorized'` | no |
| `getWhoami` other | throw `` `whoami ${status}` `` | no |
| Workspace whoami catch | `setError(err.message)` or `'whoami failed'` | n/a |
| HomePage whoami 401 | `signinRedirect()` | no |
| Workspace whoami 401 | **no redirect**; banner `unauthorized` | no |
| GET products/payments/receipts/gateway | **ignored** if `!ok` | no |
| PUT gateway | `` `keys ${status}` `` | **no** — this is the painful one |
| POST product | `` `product ${status}` `` | no |
| POST checkout | `` `checkout ${status}` `` | no |
| createTenant | `` `create tenant ${status}` `` | no (One, not PayErrors) |

Worked examples for Save key:

- Stripe, empty webhook box → host 400 `{ status:400, title:"Bad Request", detail:"webhook_secret is required" }` → banner **`keys 400`**.
- CHIP, empty Brand ID → `detail:"public_merchant_id is required"` → **`keys 400`**.
- CHIP, empty PEM → `detail:"webhook_secret is required"` → **`keys 400`**. Same banner as Stripe missing `whsec_`. Staff cannot tell which box.
- Billplz, if someone later stopped sending `environment` → `detail:"environment is required"` → **`keys 400`**.
- Razorpay empty boxes → `detail:"secret must be key_id:key_secret"` → **`keys 400`**.
- Member curling PUT → 403 `Writer role required` → if they somehow clicked, chrome already hid the button; curl is the real H18 path.
- One down → 503 `Identity provider unreachable` / `Identity provider failed` → **`keys 503`**.

`pasteKey` does not `response.json()`. Even a future richer Problem Details payload would be ignored until this line changes.

Whoami mismatch with HomePage: on the workspace route, 401 is a stuck banner rather than a login redirect. `RequireAuth` only checks `auth.isAuthenticated`, not API 401.

---

## 9. Role chrome

```1:3:apps/lazuar-pay-merchant/src/lib/roles.ts
/** One tenant roles. Pay: owner/admin write money; member is read-only. */
export function canWriteMoney(role: string | undefined | null): boolean {
  return role === 'owner' || role === 'admin'
}
```

`WorkspacePage`:

```53:53:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
  const write = canWriteMoney(tenant?.role)
```

```157:159:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
      <p>
        Role <code>{tenant?.role ?? '…'}</code>. Path org id is authorization SoT.
      </p>
```

Writer branch (`write === true`): Provider select, rail copy, secret fields, webhook URL, Save key, Product + pay link.

Member branch:

```275:277:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
      ) : (
        <p>Member can see payments. Cannot paste keys or create charges.</p>
      )}
```

Members still see: kicker, org name, role, error banner, **Active rail / last4 / capability**, SST/e-invoice sentence, Products list, Payments list, Receipts list, Pay API origin, link home.

This matches U16 (hide paste unless owner/admin) and U17 (member sees metadata, not inputs) and H18 (API still 403 if they curl). `is_platform_admin` on whoami is typed in `payApi.ts` and **never consulted** for `write`. A platform admin who is not a tenant member hits `Not a member of this org` and member chrome. Host `RequireWriterAsync` also uses tenant role, not platform admin. Chrome and API agree: One tenant `owner`/`admin` only. README: “One has no VIEWER role.”

`HomePage` lists each tenant with `<code>{t.role}</code> {t.status}`. Workspace does not show `status`.

`canWriteMoney` is **not unit-tested**. `locks.test.ts` does not grep `owner`/`admin`. Host `GatewayTests.Member_cannot_put_gateway` is the 403 lock; the SPA lock is “do not render the form.”

H17: `POST /v1/checkouts` is `RequireWriterAsync`. The SPA hides “Create pay link” behind the same `write` flag. Chrome matches API for minting, not only for keys.

If `who.tenants` has no row for `orgId`, `setError('Not a member of this org')` and `tenant` is null → `write` false. The path param is still used as `orgHint` on the GET fan-out **only after** a match (`refresh` is inside `if (!match) … else await refresh`). Non-members do not GET gateway. Good.

---

## 10. Wrap-copy honesty (U18, U19, T18, T15)

Live copy table, quoted from the page (this is the entire `copy` map):

```24:30:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
const copy: Record<(typeof rails)[number], string> = {
  stripe: 'Hosted Checkout on Stripe. Cards on Stripe’s page. Official Receipt, not an e-invoice.',
  chip: 'Hosted CHIP page (FPX/wallets if enabled on the brand). Auto-debit later, not this program. Paste PEM from the CHIP dashboard — Pay does not register webhooks.',
  billplz: 'Reminder + hosted bill. We do not auto-debit. Callback must be public https (localhost will fail).',
  xendit: 'Hosted invoice. Wallets on Xendit’s page if you enabled them there. We do not auto-debit.',
  razorpay: 'Hosted payment link. Not e-mandate. We do not auto-debit.',
}
```

Plus two page-level sentences always visible (writers and members):

- `Pay does not file SST or MyInvois. Receipts are Official Receipts.`
- Active rail line with `gateway.capability` which is host `hosted_link`.

U19.1 vs live:

| U19 sentence | Live | Honest? |
|--------------|------|---------|
| Stripe: hosted Checkout, cards on Stripe’s page, capability hosted_link | Copy says hosted Checkout + cards on Stripe’s page + Official Receipt. The words `hosted_link` appear via GET `capability`, not in the stripe sentence. | **Mostly.** Capability is on the Active rail line, not “next to the field set” as a word, but it is on the page. |
| CHIP: hosted CHIP page (FPX/wallets if enabled on the brand). Auto-debit later, not this program | Exact. Plus PEM / no-register sentence (U12/C28). | **Yes**, with the PEM-input hole underneath the honest sentence. |
| Billplz: reminder + hosted bill. We do not auto-debit | Exact, plus localhost callback warning (B15). | **Yes** for capability. Webhook URL hint still shows loopback (see §5.3 / §11). |
| Xendit: hosted invoice. Wallets on Xendit’s page. We do not auto-debit | Live adds “if you enabled them there. We do not auto-debit.” Matches U14 “Pay does not draw them.” | **Yes.** |
| Razorpay: hosted payment link. Not e-mandate. We do not auto-debit | Exact. | **Yes.** U15.2 “Do not label the rail e-mandate” — the only “e-mandate” substring is the negation. |
| All: Official Receipt, not an e-invoice (T18) | Stripe sentence + global line. Receipt **list** is `{number} — {title}` with no per-row “Official Receipt” and **no receipt detail page** (GET by id unused). | **Page-level yes; list/detail T18.1 is thin.** |

U18 five-logo wall: `WorkspacePage` has a `<select>` of five **processor names**, no `<img>`, no GrabPay/TnG/Boost/DuitNow logo row. `App.css` is typography/layout only. Visual grep clean. A `<select>` is explicitly allowed by U18.1.

T15 SST field: no SST checkbox, no `PUT /v1/orgs/{orgId}/tax`, no `sst` in merchant `src`. Global copy says Pay does not file SST.

Honesty nits that are **not** in U19’s sentence list but sit on the same page:

1. Stripe API placeholder `sk_test_…` still trains test keys (U11’s original goal: stop training that `sk_test_` is the only secret). The webhook box exists, which is the bigger 014 hole; the placeholder is leftover training.
2. CHIP copy tells you to paste PEM; the control cannot hold a PEM. The sentence is honest; the widget is not.
3. Webhook URL uses `payApi` (Vite origin), not `Pay:PublicBaseUrl`. For local dogfood it prints `http://localhost:8081/v1/webhooks/{provider}/{orgId}`. Billplz copy says localhost will fail; the URL you would copy is localhost. CHIP/Xendit/Razorpay/Stripe have the same loopback hint without a warning (Stripe CLI can forward; the others cannot).
4. “Create pay link” does not mention that chip/billplz/xendit/razorpay start will 400 without buyer email — that email is collected on `:5179`, not here. Not a lie; a missing cross-link.
5. Pay link is hardcoded `http://localhost:5179/c/{public_token}` (00 §6.1 asked for exactly that). It does **not** append `?status=verifying`. Rails default `SuccessUrl` to `http://localhost:5179/c/{token}?status=verifying` when checkout create omitted `success_url`. Staff-copied URL and PSP-return URL differ by query. Checkout slice owns the poll; merchant owns the mint.

---

## 11. Secrets not in Vite (U20) and IsolationTests Vite ban

### 11.1 What Vite actually exposes

`.env.example` (the committed template):

```
# Focused Pay host. Never Hub :8080. Never point lazuar-ops here.
VITE_PAY_API_URL=http://localhost:8081

# Public SPA OIDC (PKCE). No client_secret. Never ZITADEL_PAT.
VITE_ZITADEL_AUTHORITY=http://localhost:8085
VITE_ZITADEL_CLIENT_ID=
VITE_ZITADEL_REDIRECT_URI=http://localhost:5178/callback
VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=http://localhost:5178/
VITE_ZITADEL_SCOPE=openid profile email offline_access

# One HTTP for workspace create (Ada Bearer). Not Pay org CRUD.
VITE_ONE_API_URL=http://localhost:8080/api/v1
```

`oidcConfig.ts` reads only the `VITE_ZITADEL_*` public SPA fields. `client_id` default is `''`. There is no `client_secret` in Vite. `register-spa.sh` refuses a response that includes `client_secret` (public PKCE). `WRITE_ENV=1` writes **only** `VITE_ZITADEL_CLIENT_ID` into `.env` and comments “(gitignored)”.

Repo root `.gitignore`:

```
.env
.env.local
.env.development.local
.env.test.local
.env.production.local
!.env.example
```

Merchant app `.gitignore` does **not** list `.env` itself; it relies on the repo root. That is enough for git. The merchant-local `.gitignore` would not protect a copy of this app extracted alone.

Grep of merchant `src`, `package.json`, `.env.example`, README, `register-spa.sh` for processor secrets as **defaults**:

- No `VITE_STRIPE_SECRET`, no `VITE_CHIP`, no PEM blob, no `sk_live` env, no `whsec_` env.
- `WorkspacePage` placeholders `sk_test_…` and `whsec_… (endpoint signing secret)` are UI chrome, not env. U20.1 said “none as defaults” for those greps — placeholders still contain the substrings `sk_test` / `whsec_` in source. They are not secrets. A naive grep of the app for `whsec_` hits the placeholder.

Secrets that **do** exist in this SPA exist only as React state sent in the PUT body (`secret`, `webhookSecret`, `keySecret`). After success they are cleared (except `keyId`). They are not written to `localStorage`. OIDC tokens live in `sessionStorage` (`WebStorageStateStore`). That is a token store, not a Stripe key store.

Processor wrap keys (`Pay__WrapKey`) and `Pay__StripeWebhookSecret` live in **host** env (`apps/lazuar-pay/.env.example`), never in the merchant Vite bundle. Correct split: BYOK paste from the browser; wrap on 8081.

### 11.2 IsolationTests Vite ban — what it actually bans

U20.1: “IsolationTests already ban Hub types.” Live test:

```55:72:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    [Test]
    public void Vite_apps_do_not_use_hub_types()
    {
        var repo = FindPayRoot();
        while (repo is not null && !Directory.Exists(Path.Combine(repo, "apps", "lazuar-pay-merchant")))
        {
            repo = Directory.GetParent(repo)?.FullName;
        }

        Assert.That(repo, Is.Not.Null);
        foreach (var name in new[] { "lazuar-pay-merchant", "lazuar-pay-checkout" })
        {
            var pkg = Path.Combine(repo, "apps", name, "package.json");
            Assert.That(File.Exists(pkg), Is.True, pkg);
            var text = File.ReadAllText(pkg);
            Assert.That(text, Does.Not.Contain("@repo/api-types-ts"), pkg);
        }
    }
```

This is **not** a Vite-secret ban. It is a `package.json` substring ban on `@repo/api-types-ts` for both Vite apps. It does not open `src/`. It does not grep `VITE_STRIPE_SECRET`, `sk_live`, PEM, or `whsec_`. H21 extends IsolationTests for **C# host** adapter type names (`IPaymentGatewayAdapter`, factory, …), also not merchant env.

Merchant `package.json` live dependencies: `oidc-client-ts`, `react`, `react-dom`, `react-oidc-context`, `react-router-dom`. No `@repo/api-types-ts`. No `lazuar-ops`. IsolationTests green on this axis is expected.

### 11.3 `locks.test.ts` — merchant honesty locks, also not U20

```22:37:apps/lazuar-pay-merchant/src/locks.test.ts
describe('merchant honesty locks', () => {
  it('has no password form or Hub login', () => {
    const blob = walkSrc()
      .map((p) => readFileSync(p, 'utf8'))
      .join('\n')
    expect(blob).not.toMatch(/type=["']password["']/)
    expect(blob).not.toContain('/one/auth/login')
    expect(blob).not.toContain('lazuar_auth')
  })

  it('package.json does not depend on Hub types', () => {
    const pkg = readFileSync(join(root, 'package.json'), 'utf8')
    expect(pkg).not.toContain('@repo/api-types-ts')
    expect(pkg).not.toContain('lazuar-ops')
  })
})
```

`walkSrc` is `src/**/*.ts,tsx,css` excluding `*.test.*`. It forbids a password **form** (so key boxes stay `type=text` on purpose — keys are visible on screen; that is the anti-Hub-login lock, not a secret-masking lock). It does not:

- require a CHIP `<textarea>`
- require `webhook_secret` in the PUT body
- forbid `VITE_STRIPE_SECRET`
- lock wrap copy strings
- lock `canWriteMoney`
- lock the five-name select

`vitest.config.ts` is `environment: 'node'`, `include: ['src/**/*.test.ts']`. There is no React Testing Library in `package.json`. `WorkspacePage` has **zero** component tests. U10–U21 are checklist ticks plus this source file; they are not hermetic.

`package.json` `"test": "vitest run"` is those two describes plus `bearerToken.test.ts` (JWT vs opaque vs id_token). That is the entire merchant test surface.

---

## 12. U10–U21 spot-check vs live (checklist is not proof)

015 marked every U10–U21 box `[x]`. Live vs each exit criterion:

### U10 — Staff provider select on `:5178`

| Criterion | Live |
|-----------|------|
| Select `stripe \| chip \| billplz \| xendit \| razorpay` | **Yes.** `const rails = ['stripe', 'chip', 'billplz', 'xendit', 'razorpay']` rendered as `<option>`. |
| Changing select shows that rail’s field set | **Partial.** Shared webhook `<input>` always. Extra Brand/Collection for chip/billplz. Environment only billplz. Razorpay swaps the secret box for two boxes. PEM is not a distinct control type. |
| Submit PUT with `provider` + fields | **Yes**, see §5. |
| Do not import `@repo/api-types-ts` | **Yes.** IsolationTests + locks + package.json. |
| Do not copy `lazuar-ops` modules | **Yes.** No ops routes, no logo wall. |
| Do not put this select on `:5179` | Out of this slice; merchant only. |
| Do not show five logos as “we take all wallets” | **Yes** (U18). |

### U11 — Stripe fields

| Criterion | Live |
|-----------|------|
| Two fields: sk_ + whsec_ | **Yes**, two `<input>`s when provider is stripe (plus the always-on provider select). |
| Labels say Dashboard endpoint signing secret | **Weak.** Placeholder only: `whsec_… (endpoint signing secret)`. No `<label>`. |
| `autoComplete="off"` | **Yes** on secret inputs. |
| PUT `{ provider:"stripe", secret, webhook_secret }` | **Yes.** |
| Stop training `sk_test_` is the only secret | **Partial.** Webhook box exists (the real fix). API placeholder is still `sk_test_…` only, not `sk_live_`. |

### U12 — CHIP fields

| Criterion | Live |
|-----------|------|
| Secret key (Bearer) | **Yes**, generic secret input. |
| Brand ID (`public_merchant_id`) | **Yes**, sent for chip. |
| Webhook public key PEM **(textarea)** | **No. `<input>`. Checklist is wrong.** |
| Copy: paste PEM; Pay does not auto-register | **Yes**, in `copy.chip`. |
| Webhook URL `https://{public}/v1/webhooks/chip/{orgId}` | **No.** `{payApi}/v1/webhooks/chip/{orgId}` with `payApi` default `http://localhost:8081`. Not `Pay:PublicBaseUrl`. Not https-public. |
| Exit: three fields + URL hint | Three **controls** + a URL **string**. Types of controls do not match U12.1. |

### U13 — Billplz fields

| Criterion | Live |
|-----------|------|
| API secret | **Yes.** |
| Collection ID | **Yes**, as `public_merchant_id`. |
| X-Signature secret | **Yes**, as `webhook_secret`. |
| Environment select `test \| live` | **Yes.** Values `test` / `live`. |
| Copy: callback public https; localhost will fail | **Yes.** |
| Webhook URL hint `/v1/webhooks/billplz/{orgId}` | **Path yes; origin is Vite payApi, and host start uses PublicBaseUrl + `checkout_id` query.** |
| Fields match B11 | **Shape yes.** Hydration of `environment` / collection from GET **no.** |

### U14 — Xendit fields

| Criterion | Live |
|-----------|------|
| Secret key | **Yes.** |
| Callback token (`x-callback-token`) | **Yes**, placeholder + `webhook_secret`. |
| Webhook URL hint `/v1/webhooks/xendit/{orgId}` | Path yes; origin as above. |
| Wallets on Xendit’s page; Pay does not draw them | **Yes.** |
| We do not auto-debit | **Yes.** |

### U15 — Razorpay fields

| Criterion | Live |
|-----------|------|
| Key ID, key secret, webhook secret | **Yes**, three boxes. |
| PUT joins `key_id:key_secret` | **Yes, client-side into `secret`.** Does not send host `key_id`/`key_secret` properties. |
| Copy: hosted payment link; **not** e-mandate | **Yes.** |
| Webhook URL hint `/v1/webhooks/razorpay/{orgId}` | Path yes. |
| Do not label the rail “e-mandate” | **Yes** (negation only). |

### U16 — Hide paste unless owner/admin

| Criterion | Live |
|-----------|------|
| `canWriteMoney` owner\|admin | **Yes.** |
| Member sees U17 metadata, not inputs | **Yes.** |
| API still 403 if they curl (H18) | **Host yes** (`RequireWriterAsync` + `GatewayTests.Member_cannot_put_gateway`). SPA cannot prove this; it only hides chrome. |

### U17 — Member sees metadata only

| Criterion | Live |
|-----------|------|
| GET gateway: provider, last4, configured, capability | **Fetched.** Shown if `configured`. |
| No secret fields | **Yes** (member branch has no inputs). |
| Member can still see payments/receipts | **Yes**, lists are outside the `write` branch. |
| GET extra: `public_merchant_id`, `environment`, `webhook_configured` | **Fetched into state, not shown.** |

### U18 — No five-logo wall

**Pass.** Select of names only.

### U19 — Honest hosted_link / reminder copy

**Pass on sentences** (see §10). Fail adjacent: PEM widget, webhook origin, stripe `sk_test_` placeholder, receipts list not labeled per row.

### U20 — No secrets in `VITE_*`

**Pass** for processor secrets. Vite env is Pay URL + public OIDC client_id + One URL. IsolationTests does **not** enforce this; it only bans Hub types in `package.json`. `locks.test.ts` does not grep `VITE_STRIPE`. The guarantee is grep-of-the-tree, not a test.

### U21 — Show which rail is active

| Criterion | Live |
|-----------|------|
| After GET, show `Active: chip` (or stripe/…) and last4 | Shows `Active rail:` + `<code>{provider}</code>` + ` · last4 …`. Wording differs (`Active rail` vs `Active:`). |
| `configured: false` empty state | Shows `none`. |
| Saving a different rail updates the label (P13) | **Via GET refresh after 200 PUT**, not via PUT body. If PUT 200 and GET then fails, label would lag; GET fail is silent. |

---

## 13. Host requires `webhook_secret` always — UI alignment

P11 / P12 / C11 / B11 / X11 / R11 / `GatewayEndpoints.Put` / `GatewayTests.Put_requires_webhook_secret` all agree: **there is no rail where webhook_secret is optional.** Empty or omit → 400.

Merchant alignment:

- Always renders a webhook `<input>`.
- Always puts `webhook_secret` on the JSON object, even when the box is empty (so the SPA never “omits” the key; it sends `""`, which is the same 400).
- Placeholders differ per rail (`whsec_…`, PEM, X-Signature, `x-callback-token`, `webhook secret`). That is the only per-rail teaching besides CHIP/Billplz extra boxes.
- Does not client-validate emptiness, so the first time a writer learns the host rule is `keys 400`.
- Does not show GET `webhook_configured`, so after a successful save the only evidence a webhook secret is stored is that the banner cleared. `last4` is the **API key** last4, not webhook last4. A CHIP PEM that truncated in an `<input>` still yields `webhook_configured: true` on GET (non-empty ciphertext). The boolean cannot mean “valid PEM.”

---

## 14. Adjacent writes: product + checkout (same page, same `write` flag)

```122:151:apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
  async function createProductAndLink() {
    if (!token || !write) return
    const created = await payFetch(token, `/v1/orgs/${orgId}/products`, {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: productName, amount: Number(amount), currency: 'MYR' }),
    })
    if (!created.ok) {
      setError(`product ${created.status}`)
      return
    }
    const checkout = await payFetch(token, '/v1/checkouts', {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        org_id: orgId,
        amount: Number(amount),
        currency: 'MYR',
      }),
    })
    if (!checkout.ok) {
      setError(`checkout ${checkout.status}`)
      return
    }
    const body = (await checkout.json()) as { public_token?: string }
    if (body.public_token) setPayUrl(`http://localhost:5179/c/${body.public_token}`)
    await refresh(token)
  }
```

Host `CreateProductRequest`: `name`, `description`, `amount`, `currency`, `interval`. SPA sends name/amount/`MYR`. Currency hard-coded matches CatalogEndpoints Bar B (`currency != "MYR"` → 400 `"Bar B currency is MYR"`). `amount` from `Number(amount)` on a string input; `""` becomes `0` → 400 `"amount must be greater than 0"`. Product `id` / `price_id` in the 201 body are discarded. Catalog `interval` defaults `one_off`.

Host `CreateCheckoutRequest`: `org_id`, `amount`, `currency`, `success_url`, `cancel_url`, `idempotency_key`. SPA sends the first three. No `Idempotency-Key` header (host then uses body key, also empty). No success/cancel URLs — rails fill verifying URLs themselves.

**Product is not linked to checkout.** There is no `product_id` on `CreateCheckoutRequest`. The catalog row is a side effect of the same button. If checkout 400/403 after product 201, the product remains and `refresh` is skipped (`return` after `checkout ${status}`).

Checkout JSON is snake_case via `OneClient.Json`; `public_token` matches `CheckoutSession.PublicToken`. Other session fields (`id`, `status`, `amount`) unused.

GET products returns `prices[]`; the list UI prints `p.name` only. GET payments returns `id, org_id, checkout_id, amount, currency, status`; UI prints amount, currency, status (`checkout_id` unused). GET receipts returns `id, org_id, number, title, checkout_id`; UI prints number + title.

---

## 15. Field-set matrix (SPA form vs host PUT vs GET back)

| Host JSON | stripe UI | chip UI | billplz UI | xendit UI | razorpay UI | PUT sent? | GET shown? |
|-----------|-----------|---------|------------|-----------|-------------|-----------|------------|
| `provider` | select | select | select | select | select | always | yes if configured |
| `secret` | one box | one box | one box | one box | **joined from two boxes** | always (razorpay via join) | never (S18) |
| `webhook_secret` | `whsec_` box | PEM **input** | X-Signature box | callback-token box | webhook box | always, may be `""` | never; only `webhook_configured` exists and is unused |
| `public_merchant_id` | hidden | Brand ID | Collection ID | hidden | hidden | only chip/billplz | typed, not shown, not hydrated |
| `environment` | hidden | hidden | select | hidden | hidden | **only billplz** | typed, not shown, not hydrated |
| `key_id` | — | — | — | — | box, **not a JSON key** | no | no |
| `key_secret` | — | — | — | — | box, **not a JSON key** | no | no |
| `last4` | — | — | — | — | — | n/a (computed on host) | yes |
| `configured` | — | — | — | — | — | n/a | yes (as none vs name) |
| `capability` | — | — | — | — | — | n/a | yes (`hosted_link`) |
| `org_id` | path param | path | path | path | path | URL, not body (PUT) | dropped |

---

## 16. `package.json` and isolation, quoted

```1:32:apps/lazuar-pay-merchant/package.json
{
  "name": "lazuar-pay-merchant",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite --port=5178 --host=0.0.0.0 --strictPort",
    "build": "tsc -b && vite build",
    "lint": "oxlint",
    "test": "vitest run",
    "check-types": "tsc -b",
    "preview": "vite preview --port=4178 --strictPort",
    "clean": "rm -rf dist"
  },
  "dependencies": {
    "oidc-client-ts": "^3.4.1",
    "react": "^19.2.8",
    "react-dom": "^19.2.8",
    "react-oidc-context": "^3.3.0",
    "react-router-dom": "^7.15.0"
  },
  ...
}
```

Port 5178, no Hub types package, tests are vitest node greps + bearer unit tests. That is the whole lock around this UI besides host `GatewayTests` / `IsolationTests.Vite_apps_do_not_use_hub_types`.

---

## 17. Ranked mismatches (merchant PUT/GET vs host, this slice)

These are live-code facts, not a rewrite list.

1. **CHIP PEM is a single-line `<input>`; U12 and `ChipWebhook.ImportFromPem` need a multi-line PEM.** PUT can 200; webhooks then `"invalid signature"`. Checklist U12 `[x]` is false against live.
2. **Host always requires `webhook_secret`; SPA always sends the key but maps every 400 to `keys ${status}`.** Staff cannot distinguish missing PEM, missing Brand ID, missing `whsec_`, bad razorpay join, or missing billplz environment.
3. **GET `environment` / `public_merchant_id` / `webhook_configured` are unused.** Billplz live vs test is not hydrated; re-save can flip `live` → `test`. Brand/Collection must be retyped every session even though GET returns them. `webhook_configured: true` cannot be seen, and would be true for a truncated PEM anyway.
4. **GET `?provider=` is unused.** Parked rails after a switch are invisible. P15 has no merchant consumer and no host test hit.
5. **Razorpay `key_id`/`key_secret` exist on `PutGatewayRequest` and are never sent.** Client join is the only path; empty boxes send `secret:":"` which 400s on `TrySplit`, not on the host join branch.
6. **Webhook URL hint uses `VITE_PAY_API_URL`, not `Pay:PublicBaseUrl`.** Default print is `http://localhost:8081/v1/webhooks/{provider}/{orgId}`. Billplz start registers a different base and appends `checkout_id`. U12’s `https://{public}` is not implemented.
7. **Stripe placeholder still says `sk_test_…` only.** Webhook box exists (U11’s real requirement). Label-as-placeholder. Environment column will say `test` for an `sk_live_` key.
8. **Error mapping never reads `PayErrors.detail`.** Whoami on this page also does not redirect on 401 (HomePage does). List GETs fail closed-silent.
9. **Role chrome matches H17/H18/U16** (`owner`/`admin` vs `member`). That part is aligned. `is_platform_admin` unused on both sides for gateway write.
10. **Wrap copy sentences are honest** (no e-mandate, no auto-debit, Official Receipt, no registrar, no SST field, no five-logo wall). The dishonest adjacent widget is the PEM input, not the CHIP sentence.
11. **Secrets are not in Vite processor env.** IsolationTests Vite ban does not prove that; it only bans `@repo/api-types-ts` in `package.json`. U20 grep is clean for `VITE_STRIPE_SECRET` / PEM defaults. Placeholders still contain `sk_test` / `whsec_` as strings.
12. **Product POST and checkout POST share a button but not a foreign key.** Catalog 201 `id` unused. Checkout omits success/cancel/idempotency. Currency locked MYR in the SPA, which matches the host Bar B rule.

---

## 18. Files this slice treats as closed evidence

Merchant:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/roles.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/locks.test.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/package.json`

Host:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` (`PutGatewayRequest`, PUT/GET)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` (`Vite_apps_do_not_use_hub_types`)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/GatewayTests.cs` (member 403, webhook_secret required, no echo, chip Brand ID)

015 map (ticked, not proof): `u10`–`u21` plus the P11–P16 / C11 / B11 / R11 / X11 / S18 / T18 items cited above.

Nothing in this file was implemented. The merchant PUT is a hand-built `Record<string, string>` that almost matches `PutGatewayRequest`, except where it joins Razorpay in `secret`, skips `environment` off Billplz, never sends `key_id`/`key_secret` JSON, never hydrates GET extras, and puts CHIP’s PEM in a control that cannot hold a PEM.
