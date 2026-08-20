# 01 — Product: focused Lazuar Pay

**Date:** 20 August 2026  
**Job:** take money correctly, once, and leave a receipt and access behind.

This is the **second cut** (One already exists). An earlier list put email/password, workspace slug, and membership *inside* Pay. That is superseded: merchants live in lazuar-one. Why we left the old tree: [00-why-leave.md](./00-why-leave.md). How bad the old tree is: [09-old-pay.md](./09-old-pay.md).

It is not Compliance CaaS, not WorkOS, not MyInvois. ADR 021’s tax moat stays **out**. ADR 023’s “just checkout” is the product.

Thin mail and audit for **Pay writes** live next to money (same Pay process / same Pay database). Merchant login, orgs, staff invites, and `lzr_sk_` live in **lazuar-one** — see [02-one-integration.md](./02-one-integration.md).

---

## Must have (v1)

### Catalog and checkout

- Product: name, prices (monthly / yearly), currency (start **MYR**), quantity/seats.
- Checkout session: amount, success/cancel URLs, idempotency key, open → paid / expired.
- Hosted buyer page (cash register). Merchant ops: products, gateway keys, payments, subscribers.

### Gateways (wrap-rails)

- BYOK keys per workspace (encrypted).
- **Stripe** (cards; off-session only if a real PM/token exists — never treat setup as paid).
- **One Malaysian rail** you will actually dogfood (**CHIP** or **Billplz**), not five adapters on day one.
- Webhook: verify signature, empty body = 400, idempotent on `(tenant, provider, event_id)`.
- Honest matrix: Stripe/CHIP can auto-charge if vaulted; Billplz/Xendit/Razorpay-class = **reminder + hosted link**, never silent debit.
- No homemade FPX e-mandate. No Stripe Billing `subscription.updated` as source of truth.

### Fulfillment (same process as the webhook)

- First successful pay creates the subscription (or marks a one-off complete) **and** writes the ledger **in the same handler**.
- Buyer access is Pay’s subscription / session row. Merchant staff access is One membership — do not grant buyer access in One.
- Renew: billing job mints checkout or off-session charge; decline does not invent PAST_DUE on a healthy seat without a real failed charge.
- Buyer portal: magic link to the **payer** mailbox, update-payment, download **receipt**.

### Money truth

- Double-entry journal: cash, revenue, tax, fee when the gateway **actually** sent a fee (`unknown` ≠ 0).
- **SST:** exclusive on the **unit**, then × seats; if you cannot know whether the merchant is SST-registered, **fail closed** (do not undercharge).
- Refunds: call gateway, then reverse the journal **once**. Disputes: do not double-reverse.

### Documents (commercial, not tax)

- Official Receipt / payment receipt (`RCPT-…`). Number is never a UUID; missing number is `PENDING`.
- Do not title it Tax Invoice. Do not print MyInvois VALID.

### Buyer plane (not One)

- Payer email/name on the checkout session.
- Magic link / receipts for **that** mailbox.
- Do not create a Zitadel human per cardholder. Keep a small payer profile inside Pay (old CRM/client-profile job, stripped).

### Mail and audit (Pay-owned, not a Notify/Audit service in v1)

- Transactional email: receipt, dunning, failed pay, buyer magic link.
- Staff invite **copy-link** stays One’s (keep a non-email accept path).
- Audit row on Pay writes (charge, refund, gateway-key change) in the **same DB transaction** as the write.

### Public door

- `POST /v1/checkouts`, provider webhook URL, `GET` payment status.
- Merchant ops is a client of that door (user JWT from One, or `lzr_sk_` for workers).
- No second app reading Pay tables.

---

## Should have soon (still Pay)

- Custom amount / quote (proforma PDF, not a tax invoice). SST on the quote matches hop-2.
- PAST_DUE + email dunning + cached update-payment link (one completion does not skip a cycle).
- Partial refunds that match the gateway.
- M2M checkout for a second of *your* apps (same `/v1`).
- Second gateway only after the first two are boring in production.

---

## Later (not v1)

- Tax **provider** (someone else’s MyInvois). You send amount + buyer; they return VALID + QR. Pay never owns UBL, consolidation, types 03–14, or XAdES.
- More rails (Razorpay, Xendit) as reminder-only, labelled as such.
- Entitlement grant for a **second** Lazuar app — HTTP or a function if in-process; not an in-process event catalog talking to yourself.

---

## Do not build in this product

| Leave out | Why |
|-----------|-----|
| Homemade LHDN / XML / consolidation job / TIN-at-checkout as a legal feature | Sandbox VALID still **not captured** in the old tree |
| WhatsApp dunning, Xero, Web3, escrow, 15 apps, CMS | Never shipping / vitamin |
| Zitadel, OpenFGA, SCIM, password store, dual JWT vs membership roles | **lazuar-one** (or a vendor) |
| Per-module schemas / inbox as the way Pay talks to itself | Already paid that tax |
| Debit notes, self-billed 11–14, “Credit & Debit Notes” | Strategy-only lies in the old tree |

Steal **judgment** from the old repo, not folders: exclusive SST on the unit then × seats; fail closed if SST is unknown; never print a UUID as a document number; do not say VALID unless a tax *provider* said so; wrap-rails only.

---

## Dogfood test (focused enough)

A merchant signs in through **One**, opens Pay, pastes CHIP or Stripe keys, a **buyer pays on the hosted page without a One account**, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, a One-invited MEMBER can see ops and a VIEWER cannot charge.

If a feature is not on that path, it is not v1 lazuar-pay.
