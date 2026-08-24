# 016 — Double-check new Pay gateway adapters, frontends, Hub HTTP, and tests

**Date:** 24 August 2026  
**Branch:** `feat/015-four-adapters`  
**HEAD at analysis start:** `c621ceba` — `docs(015): check off implemented T–Q phases`  
**Type:** Uncondensed evaluation. **Not** an implementation in the ten evidence files. **Not** a project reference into `apps/lazuar-api`.

Parent judgment: [00-evaluation.md](./00-evaluation.md) (written after `01`–`10`, SHA `c621ceba`). Evidence: the ten uncondensed reports. **Do not treat this index or the parent as a substitute for those reports.** Read the file.

**Problem:** 015 landed five hosted_link rails on 8081 plus merchant/checkout UIs. We must (1) verify the **new host** and how **`:5178` / `:5179`** actually call it, (2) cross-check each rail against Hub `Modules/Payments/Infrastructure/Gateways/` as **HTTP judgment**, (3) name which **tests exist vs must still be written**. Live code is authority. [015](../015-four-adapters/README.md) checklists are a map, not proof.

New stack:

| Path | Role |
|------|------|
| `apps/lazuar-pay` | Host **8081**. `Gateways/*`, `PUT/GET /v1/orgs/{orgId}/gateway`, `POST /v1/pay/{token}/start`, `POST /v1/webhooks/{provider}/{orgId}` |
| `apps/lazuar-pay-merchant` | Vite **5178**. Staff paste keys + mint pay link |
| `apps/lazuar-pay-checkout` | Vite **5179**. Buyer start + verifying poll. No One account |

Hub (steal HTTP; do not copy):

| Path | Role |
|------|------|
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/` | Stripe, CHIP, Billplz, Razorpay, Xendit + factory + DNS fallback + CHIP registrar |

| File | Subagent | Assigned slice |
|------|----------|----------------|
| [01-new-host-seams.md](./01-new-host-seams.md) | Host seams | PUT/GET, `IHostedRail`, start dispatch, webhook TX, secrets |
| [02-merchant-frontend.md](./02-merchant-frontend.md) | Merchant Vite | `:5178` fields vs host PUT; wrap copy |
| [03-checkout-frontend.md](./03-checkout-frontend.md) | Checkout Vite | `:5179` start, email_required, verifying poll |
| [04-stripe-crosscheck.md](./04-stripe-crosscheck.md) | Stripe | Hub adapter vs `StripeHosted` + `StripeWebhook` |
| [05-chip-crosscheck.md](./05-chip-crosscheck.md) | CHIP | Hub Collect vs `ChipHosted` + RSA webhook |
| [06-billplz-crosscheck.md](./06-billplz-crosscheck.md) | Billplz | Hub bills/HMAC vs `BillplzHosted` + form webhook |
| [07-xendit-crosscheck.md](./07-xendit-crosscheck.md) | Xendit | Hub invoices vs `XenditHosted` + callback token |
| [08-razorpay-crosscheck.md](./08-razorpay-crosscheck.md) | Razorpay | Hub payment links vs HTTP + HMAC |
| [09-tests-inventory.md](./09-tests-inventory.md) | Tests | What `WebhookTests`/`RailTests`/`GatewayTests` lock; gaps to write |
| [10-honesty-frontend-risks.md](./10-honesty-frontend-risks.md) | Honesty | Ranked bugs, frontend/host mismatches, refuse list |
| [11-hub-vs-pay-features.md](./11-hub-vs-pay-features.md) | Feature matrix | Hub cathedral vs Pay cashier after 016 harden (`69454123`) |

Write uncondensed. Do not summarize a report into a bullet list and delete the evidence. The feature matrix does **not** replace [04](./04-stripe-crosscheck.md)–[08](./08-razorpay-crosscheck.md).

**Implementation (after this eval):** many small phases in [`checklist/`](./checklist/README.md). Freeze [`checklist/decisions.md`](./checklist/decisions.md). Product money first, then tests, then SPA. Not a sixth rail.
