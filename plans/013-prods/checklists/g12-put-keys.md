# G12 — Merchant PUT gateway keys

**Track:** Rails · **Depends:** G11, M24  
**Analysis:** [06](../06-money-rails.md) §4.3 / §4.6  
**IDs:** NP-GW-009  
**Goal:** Admin/owner pastes BYOK on `/v1`. Not Hub `/admin/commerce/payment-config`. `NP-GW-009`.

---

## G12.1 Route

- [x] `PUT /v1/orgs/{orgId}/gateway` or `PUT /v1/orgs/{orgId}/gateways/{provider}` — pick one, stay on `/v1`
- [x] Listen **8081**. Not Hub `/api/v1/…`. Not `/one/*`
- [x] Body: **provider** (if not in the path) + **secret**. G10 rail only (`stripe` or `chip`)
- [x] Unknown provider (razorpay / xendit / billplz if not G10) → **400**
- [x] Encrypt via G11. First-time requires a secret

## G12.2 Authz

- [x] Require `Authorization` Bearer; missing/blank → **401**
- [x] `authz/check` **admin** (owner has admin). Not `member` only
- [x] Caller who is only `member` → **403** (G14 owns the named test)
- [x] Not a member of `{orgId}` → **403**

## G12.3 Must not

- [x] No Hub `OrgAdmin` policy strings. No MediatR. No Vite secrets
- [x] Do not log the new or old secret. Audit provider + actor + `org_id` if D29 exists

## G12.4 Exit

- [x] PUT returns **200** or **204** for owner/admin
- [x] `NP-GW-001` / `NP-GW-009` may move when G13 GET is honest (prefer G12–G14 same commit if small)
- [x] Unblocked for G13, G14, G16
