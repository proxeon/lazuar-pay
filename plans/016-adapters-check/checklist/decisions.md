# 016 — Locked decisions (harden five wraps)

**Filled by:** [a00-align-freeze.md](./a00-align-freeze.md)  
**Evidence:** [`../00-evaluation.md`](../00-evaluation.md), [`../09-tests-inventory.md`](../09-tests-inventory.md), [`../10-honesty-frontend-risks.md`](../10-honesty-frontend-risks.md)  
**Do not change a row without amending A00.**

015 locks still bind: five lowercase names, `hosted_link`, one `active_provider`, same-handler Official Receipt, tax **out**, no factory, no registrar, no DNS fallback, hermetic `task pay:test`. This program **does not** add a rail.

| Topic | Lock |
|-------|------|
| Program | Harden the five wraps already on 8081. Product money first, then tests, then SPA honesty. |
| Start | Second `POST /v1/pay/{token}/start` on an **open** checkout with `PspRedirectUrl` set **returns that URL** and must **not** call the PSP again. Paid/expired still 409. Paused still 403. |
| Public GET | May expose `started` and `redirect_url` (buyer already has the link). Never secrets. |
| Stripe create belt | `Idempotency-Key = lazuar-checkout:{checkout.Id}` on Session create. Does not replace I10. |
| One HMAC | Standard Webhooks judgment: header `t={unix},v1={lowercase hex}` over `{unix}.{body}`, skew ~300s. Old body-only uppercase hex is **401**. Steal judgment from Hub One signer; **do not** copy `Modules.One`. |
| Org id on Plane A | Read JSON `tenant_id` **or** `org_id`. |
| Pause | `tenant.suspended` sets `ChargesPaused`. Fulfill **does not book**. Paid event id is **not** consumed (PSP can retry after unsuspend). New starts already 403. |
| Razorpay join | Prefer `payment.entity.notes.checkout_id`. Fallback: stored `checkout.ProviderSessionId` (`plink_`). Missing both → 400, no silent pay. `payment_link.paid` / `order.paid` are **not** cash. Cash remains `payment.captured` only. |
| Units | CHIP `total` cents; Stripe `AmountTotal` cents; Razorpay `amount` minor; Billplz `paid_amount` sen; Xendit `paid_amount` **major** then `ToMinor`. Pin in parser comments + FakePsp JSON. |
| Currency | Fail closed if PSP omits currency. **Do not default MYR on the webhook.** Billplz must stop hardcoding MYR. Stripe must not skip the compare when currency is null. Checkout create may still default MYR — that is not a webhook default. |
| Mismatch | Amount/currency mismatch → **400** and **no** `psp_webhook_events` insert. |
| Stripe process `whsec_` | Fallback **Testing only**. Development and Production with empty `WebhookCiphertext` → 503 even if `Pay:StripeWebhookSecret` is set. |
| Wrap key | Git-known `"lazuar-pay-dev-wrap-key"` **Testing only**. Production required. Development/Staging without `Pay:WrapKey` must throw (same as Production) **or** refuse to boot — A00 fills: **throw outside Testing**. |
| Checkout origin | `Pay:CheckoutBaseUrl` for hosted success/cancel defaults. Merchant `VITE_CHECKOUT_ORIGIN` for minted `/c/{token}` links. Billplz **callback** stays `Pay:PublicBaseUrl`. Do not mix the two. |
| Webhook rail bind | If `checkout.Provider` is set, path `{provider}` must equal it. Leftover credentials for a previous rail must not fulfill that checkout. |
| Xendit token | Hash-first compare (Hub 073 judgment). SETTLED still not paid. |
| Fulfill throw | One TX stays. Prove with a seam or a real-transaction store. InMemory is **not** proof. Do not SaveChanges-then-throw in production to make a test. |
| Tests | Hermetic. Fake One + FakePsp. Strengthen eight existing methods **before** cloning paid tests. Method names from 09 §10. |
| SPA errors | Buyer/staff show host problem `detail` (or a mapped known string). Do not map every 400 to Billplz callback copy. |
| Placeholder email | Host already 400s. SPA must block `customer@example.com` the same as empty. |
| Verifying | Query is not paid. After poll cap: not-paid-yet + manual refresh GET. Do not re-enable Pay if `started`. |
| CHIP PEM | `<textarea>`, not a single-line `<input>`. |
| Billplz env | Hydrate GET `environment` into the select so re-save cannot flip live→test. |
| Catalog | Either send `product_id` on checkout create **or** copy that the amount field is independent. Do not demo SKU. |
| Tracker | Flip 011/11 only from a lived phase Exit. Hermetic CHIP ≠ `NP-GW-003`. |
| Parked | Factory, registrar, DNS, SST/LHDN, e-mandate, off-session, refunds, Hub cutover, sixth rail. |

## Filled in A00 (must not be blank)

| Topic | Value | Notes |
|-------|-------|-------|
| Start second click | **return existing URL** | Not 409-only. Buyer must be able to continue. |
| Pause + paid webhook | **do not fulfill, do not consume paid event id** | 409/403 so PSP retries after unsuspend. |
| Razorpay missing notes | **join plink_ else 400** | Do not invent Guid. Do not fulfill `payment_link.paid`. |
| Wrap key outside Testing | **throw** | Same message as Production. |
| Stripe process `whsec_` | **Testing only** | Not every non-Production. |
| Checkout default origin | **`Pay:CheckoutBaseUrl`** | Laptop default may stay `http://localhost:5179` in `.env.example`. |
