# 00 — Why leave this tree

**Date:** 20 August 2026  
**Type:** Reflection. Not an argument that the user needs permission to rewrite.

The instinct matches what this repo actually did to us. Waves 241–260 were often not “pay is hard.” They were **seams**.

---

## What the modular monolith actually cost

A lot of the last harvest was pretending each folder is a service while everything still shares one process, one deploy, and one operator:

- `TaxInvoiceId` was a dumping ground because Billing, LHDN, and consolidation could not share one document model.
- `InvoiceIssued` was subscribed in two modules and constructed in none.
- `ManualPaymentRecorded` was a contract that looked like cash settlement.
- Hub SaaS PDF sliced a Guid because that handler did not use the merchant numbering helper sitting one folder over.
- Portal tokens were subscription-shaped, so a paid quote with no `Subscriptions` row could not open its own documents.
- Register said `ADMIN` in JSON and stamped `CLIENT` on the cookie.
- Workers needed `IgnoreQueryFilters` because the “module” ran with an empty tenant.

Those are not product ideas. They are the tax of module walls plus leftover multi-app plus migrations that cannot see across schemas.

Once a module has a schema, a DbContext, a migration set, an outbox, a README, an architecture test, and a parked event on an allowlist, **deleting it feels like deleting a product**. So dead writers stay “parked.” Strategy-only debit notes stay in the factory. JSON 1.1 signing exists and sandbox VALID does not. Honesty files and allowlists become a second product: documenting the lie so the next person does not sell it.

That is how modular monoliths rot in practice. The boundary that was supposed to make deletion easy makes deletion look like a cross-team breaking change.

Modular monolith sounded like discipline: extract later by lifting a folder. In this codebase a module is a schema + events other modules subscribe to. “Lift Commerce into a service” means rewriting those contracts, not moving a folder. Linux never paid that tax — see [04-linux-shape.md](./04-linux-shape.md).

---

## Start monolith, extract when a reason appears

Checkout, ledger, tax document, and gateway webhook are **one money story**. Splitting them into Commerce / Billing / Payments / Lhdn **before** the story is stable does not isolate risk. It hides the story.

The right sequence for early pay:

1. One app, one database, `recordPayment()` updates ledger and receipt in the same transaction.
2. Extract a service later if something actually needs its own process.

A tax **provider** already is that extract. A second product that cannot compile with Pay is another. Four named platforms as four deploys on day one is not — see [06-platforms.md](./06-platforms.md).

---

## Stop feature work; keep the tree as reference

Using this tree as a reference and stopping feature work on it is the honest conclusion of the last few months.

Two months of construction and then a long bug harvest is the usual ratio for this architecture. Another year on this path would not mostly invent new pay features. It would keep producing seam bugs: dual-use fields, workers with empty tenant, tokens whose subject is the wrong aggregate, READMEs that outrun publishers. 001–260 already showed that pattern. 261–334 are still open on paper.

A leaner pay monolith will still have **hard bugs worth hunting from zero**: SST rounding, 72-hour cancel (when a tax provider exists), webhook idempotency, tenant isolation.

The bugs to **refuse to recreate** are the ones we kept marking resolved as honesty: unused events, dual-use columns, README that outruns the publisher, two role vocabularies, a token subject that is not the thing the buyer paid for.

**Useful inheritance is judgment, not folders:**

- Exclusive SST on the **unit**, then × seats.
- Fail closed when you cannot decide SST.
- A document number that is never a UUID; missing number is `PENDING`.
- VALID means a tax system said VALID — not a badge we printed.
- One role story.
- One write path for cash.
- One database you can migrate without negotiating with a module README.
- Wrap-rails only (no Stripe Billing `subscription.updated` as source of truth).

Leave LHDN, WhatsApp, Xero, and homemade e-mandate out until a provider or a later extract has a real reason.

Stopping now is early, not waste. The expensive lesson is already paid for.

---

## Tax in this codebase was the wrong extract

MyInvois was treated like another module: strategies, types `01`–`14`, a signer, consolidation, 72-hour cancel, TIN collect, a VALID badge. Most of that never became a product.

What shipped was a **second ledger**: document numbers versus UUIDs, receipts that must not be called tax invoices, a park status with no collector, a signer test that is not ACCEPT. Malaysian e-invoice is a regulated network. Rebuilding the network inside a two-month pay app is how you get a museum of honesty files.

The right split: Pay keeps **money and commercial documents** (receipt / proforma). A **provider** already files, cancels, and returns a real VALID. Pay never owns UBL, consolidation, types 03–14, or XAdES in v1.

---

## How this reads after One already exists

[01-product.md](./01-product.md) and [02-one-integration.md](./02-one-integration.md) assume **lazuar-one stays a sibling process**. That is the justified extract (identity platform, not a `users` table). New Pay still leaves as one money kernel — it does not become four more services.

See [09-old-pay.md](./09-old-pay.md) for how problematic this tree is as a year-two core.
