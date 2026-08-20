# 09 — How problematic is this codebase

**Date:** 20 August 2026  
**HEAD considered:** `main` @ `e7bb07b0` (waves 001–260 merged). Issues 261–334 still open on paper.

**Problematic as a place to keep building. Not as a pile of unused files.** You can run it, charge a card, book a ledger row, and send a receipt. You should not spend another year *inside* this shape, and you should not sell it as a tax or identity platform.

---

## How bad, in one line

The money path was hunted hard (001–260). The **building** is the problem: nine modules, nine migration trains, events between folders that share one process. That produced a 334-bug audit from one HEAD, and 261–334 are still open (identity oracles, cookie vs Bearer, genesis password rotate, storage presign, more). Fixing 260 did not make the next 74 cheap. The architecture keeps minting them.

---

## What is actually broken vs what is heavy

| Layer | Severity | What it means |
|-------|----------|----------------|
| **Shape** | High | Commerce / Billing / Payments / One / Lhdn / CRM / Communications / Messaging / Ops each have a schema and workers. A one-line product change is a cross-module case. This is the tax in [00-why-leave.md](./00-why-leave.md). |
| **Scope** | High | Homemade MyInvois, homemade identity, messaging, credits wallet, TypeSpec honesty allowlists. Two months of “platform.” LHDN sandbox VALID is still **not captured**. Selling “e-invoice at pay” is a lie. |
| **Remaining defects** | Medium–high if you ship this binary | 74 P2s still filed: reset-password email oracle, API key hash vs prefix, ADMIN vs Integration policies, TOS as a checkbox, forwarded-for rate limit, cookie wins over JWT. Not theoretical. |
| **Money math (after the harvest)** | Medium, contained | SST per-unit × seats, fail-closed if billing is missing, wrap-rails, Guid not printed as invoice number — those were *earned*. They are judgment, not a reason to keep the cathedral. |
| **Tests** | Mixed | Lots of green module tests. Several important paths are honesty locks or `[Ignore]` sandbox. Green CI ≠ MyInvois VALID ≠ “no 261.” |

---

## Compared to the Twitter clone

Same disease, more dangerous domain. That repo wastes time on Eureka. This one can **double-book cash or file the wrong tax shape** when a seam slips. We already closed the worst of those; the *next* year would be more seams, not more checkout.

---

## What is not the problem

QuestPDF receipts, CHIP/Stripe adapters as HTTP, `SstTaxMath`, document series years in MYT, “don’t call it Tax Invoice until VALID.” Those are keep-as-notes.

C# itself is fine. The **C# enterprise default** (MediatR, per-module `DbContext`, in-process bus) is how it got here. Language: [05-language.md](./05-language.md).

---

## If you kept this codebase

You would ship a CaaS MVP only with a tight mouth: BYOK pay, subscriptions, ledger, email dunning, **no** LHDN as a product, **no** second identity story. You would still fight migrations and parked events on every change. That is “operate a museum,” not “build Pay.”

---

## If you treat it as reference

That matches how problematic it is: **too expensive to extend, too specific to ignore.** Read the handlers that take money. Do not copy the module walls, the Lhdn factory, or One-the-module.

Steal judgment into [01-product.md](./01-product.md). Integrate real One via [02-one-integration.md](./02-one-integration.md).

---

## Score

| As | Score |
|----|--------|
| Learning artifact | High value |
| Year-two product core | Poor |
| Something to sell as “platforms + Malaysia tax” | Unsafe |

The 260 fixes made it *honest enough to leave*. They did not make it a kernel you should keep growing.
