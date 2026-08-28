# W15 — GET endpoint metadata, no secret

**Track:** W · **Depends:** W14  
**Goal:** Member can see configured; secret never listed.

**Why:** Same as vault GET `webhook_configured`. Listing the `whsec_` would let a member steal the signing secret.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` | GET metadata |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` | GET one-webhook configured |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs` | `Put_and_get_does_not_echo_secret` |

**Current (`6d730d15`):** No Plane C GET.

---

## W15.1

- [x] `GET /v1/orgs/{orgId}/webhooks`
- [x] Member gate
- [x] JSON: `org_id`, `url` or null, `webhook_configured`, `secret_prefix` or null
- [x] Never `webhook_secret`
- [x] No row → `webhook_configured: false`

## W15.2 Tests

- [x] After PUT, GET has prefix, not full secret
- [x] 401 without Bearer

## W15.3 Exit

- [x] Unblocked for W16
