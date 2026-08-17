# 009 — Bug and error audit

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement` (`297ba98`)  
**Product:** Lazuar Pay (Compliance CaaS / headless checkout)

These reports hunt **bugs and errors in the current tree**, after the P0/P1 fixes that landed on this branch (`911d358` … `297ba98`). They are not a rewrite of `plans/007-feats` or `plans/008-evals`.

008 evaluated the product after Waves 0–4 and named P0/P1s that this branch then fixed. 009 re-reads the **code as it is now**. A bug that 008 filed is closed only if this tree no longer contains it. A bug 008 missed must still be written up.

There is no parent condensation in this folder. The ten reports are the deliverable.

| File | Lines | Slice |
|------|------:|--------|
| [01](./01-commerce-checkout-activation.md) | 1198 | Commerce: hop-1/hop-2 checkout, coupons, $0/trial vault, SST on first charge, sessions, activation |
| [02](./02-commerce-subscriptions-billing-engine.md) | 1192 | Commerce: subscription state machine, cancel/pause/trial/plan/seats, BillingEngineJob |
| [03](./03-commerce-dunning-arrears-portal.md) | 875 | Commerce: dunning jobs, PAST_DUE, arrears, update-payment, magic-link tokens |
| [04](./04-payments-adapters-webhooks.md) | 1016 | Payments: five adapters, capabilities, inbound webhooks, EventId, off-session |
| [05](./05-billing-ledger-refunds-disputes.md) | 1200 | Billing: ledger, refunds, disputes, Hub SaaS fee, credits |
| [06](./06-lhdn-invoices-documents.md) | 1041 | LHDN + commercial paper: quotes, receipts, tax invoices, MyInvois |
| [07](./07-one-identity-invites-keys.md) | 1112 | One: auth, workspaces, invites, roles, API keys, audit |
| [08](./08-communications-messaging-crm.md) | 1085 | Communications, Messaging, CRM |
| [09](./09-frontends-ops-portal-admin.md) | 1100 | Ops, portal, admin: routes, client calls, chrome that lies |
| [10](./10-tenancy-workers-contracts-tests.md) | 1050 | Tenancy, workers, inbox/outbox, TypeSpec vs Minimal, tests that lie |

Write uncondensed. Do not summarize a report into a bullet list and delete the evidence. Total: **10,895** lines. There is no `00-evaluation.md` in this folder on purpose.
