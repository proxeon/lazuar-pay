# 04 — Linux shape: one tree, one linker, call the function

**Date:** 20 August 2026  
**Line this paper explains:** *Linux never paid that tax: one tree, one linker, call the function. You built the expensive shape first.*

This is about **how code is allowed to touch other code**, not “Linux is simple” and not “never extract.”

---

## What “the tax” is

The extra work a modular monolith adds to a change that is conceptually **one change**.

In this repo, “update the buyer’s document after pay” is not a function call. It is:

- write in Commerce
- publish an event
- hope Billing’s inbox runs
- hope Lhdn subscribed the live type and not the parked one
- hope the worker has no tenant so it uses `IgnoreQueryFilters`
- hope the migration that added `CustomerDocumentNumber` did not leave `TaxInvoiceId` as the search key

Crossing a module is a negotiation: new contract, maybe a new migration in *that* module’s DbContext, maybe an architecture test, maybe a README honesty line. Deleting unused code is the same negotiation in reverse. **That cost is the tax.**

---

## What Linux actually did (narrow sense)

The kernel is **one program**. Drivers, the scheduler, the filesystem, and the network stack live in **one tree**. They compile into **one image**. If ext4 needs to allocate memory, it **calls a function**.

There is no `Filesystem` schema and a `Memory` outbox. There is no “we must not import `mm/` so we publish `PageAllocatedIntegrationEvent`.” Linus’s long argument against microkernels was this: for a tightly coupled core, message-passing and artificial walls make the common path slower and the design dishonest.

Subsystems exist (folders, `EXPORT_SYMBOL`, loadable `.ko` files). Those are still **one address space**. A loadable driver is not a bounded context with its own migrations.

**You built the expensive shape first** means you started as if each folder were already a future service: separate schemas, separate EF migrations, no cross-schema joins, integration events as the official way to talk. That is the shape you pay the tax *on every commit*, including when you are still one deployable and one database.

The cheap shape for early pay: one app, one database, `recordPayment()` updates the ledger and the receipt in the same transaction, extract later if something needs its own process (a tax provider already is that extract).

So the line is: Linux kept “I need that? call it.” This codebase required “I need that? publish, subscribe, migrate the other module, and document why the old event is parked.” You bought the second world before the product was stable.

---

## How underrated this is (for us)

It is underrated in **product/SaaS talk**, not in systems work. Kernel people already treat “call the function” as the default. Startup architecture writing treated it as naive: first “microservices,” then “modular monolith so you can extract later.” This repo is what that second slogan costs when you are still one team and one deploy.

For an early pay product, the Linux-shaped app is the **high-discipline** option. Events, per-module schemas, and parked contracts look like discipline. They are a second product: the wiring. We spent more time proving the wiring was honest than making a buyer pay and get a receipt.

---

## Advantages of “I need that? call it.”

**One story, one transaction.** Pay, ledger line, receipt number, and “do not consolidate” can commit together or roll back together. You do not get Billing booked and Lhdn no-op, or a worker that cannot see the row because ambient tenant is empty. Money bugs become “this function is wrong,” not “which handler missed the event.”

**The compiler is the contract.** Rename `Charge` and every caller breaks in one build. In the modular shape, the old event still compiles, the README still names it, and the architecture test allowlists it. That is how `InvoiceIssued` and `ManualPaymentRecorded` survived.

**Dead code can die.** Grep, delete, one migration drop. You do not need a park list so the in-process bus does not treat “no handlers” as success. Linux throws unused functions out of the image. We kept unused modules because deleting a schema felt like retiring a service.

**One migration timeline.** Schema change is “the database at commit N.” Not Commerce migration 40 plus Billing migration 22 plus a dual-use column because the other module cannot join. `TaxInvoiceId` was that tax made visible.

**Debugging is a stack.** You see `Checkout.Complete` → `Ledger.Record` → `Document.Issue`. You do not reconstruct a night of inbox jobs, outbox retries, and “was this the live event or the parked one?”

**Refactors stay cheap while the model is wrong.** Early pay *will* be wrong: who owns TIN, what a quote is versus a subscription, what “VALID” means. In one tree you move a field. Across modules you version an integration event and leave the old field “for back-compat.” We bought that freeze on month two.

**Performance and ops are the bonus, not the point.** No serialize/deserialize, no “eventual” cash, one process to run. For Malaysia SMB checkout volume that is plenty. The real win is **cognitive**: the running path is the designed path.

---

## What this is not

It is not “one 20,000-line file” and not “never extract.” Linux has folders, `fs/`, `net/`, loadable `.ko` files. Those are **link-time** modules: still one address space, still a function call.

A Stripe or a MyInvois **provider** is a real extract — someone else’s process, someone else’s compliance. That is the opposite of inventing Lhdn as a sibling schema so you can “extract it later.”

**lazuar-one**, already built, is also a real extract: identity platform (Zitadel, OpenFGA, SCIM) is a different product. Pay calling One over HTTP is justified. Pay talking to *itself* through an event catalog is not.

People sold a **future org chart** (many services) as an engineering method. Linux sold a **present machine** (one program that works). For a two-month-old pay app, the machine is the scarce thing. The org chart can wait until a boundary has a reason: a different SLO, a different team, or a regulator you should not impersonate.

How this sits next to Bezos (sold door, no back door): [08-bezos-door.md](./08-bezos-door.md).

---

## C, and why the language and the shape reinforce each other

Linux succeeding in C is underrated in the same way “call the function” is underrated.

C does not make you virtuous. It **refuses to help you build a second world**. No exceptions as control flow, no templates that generate a type zoo, no inheritance trees, no “interface + three implementations + a DI registration” as the default unit of work. If you need another capability, you add a function and a `struct`. The kernel stays one program because the language makes a framework-shaped kernel painful. Linus rejected C++ in the 90s for that reason: it hides cost and encourages design that the caller cannot see.

Chromium is a weak one-to-one. A full browser would be a billion-dollar object in any language. C++ did not invent that bill. What C++ *does* do is make the expensive shape **feel like engineering**. You can grow a cathedral for a long time before anyone admits the nave is unused. The modular monolith was the C# version of that feeling: it compiled, it had modules, it looked extractable.

The underrated rule is not “use C.” It is: **pick a language that makes the cheap shape the path of least resistance.** For a pay kernel that is “one process, one database, call the function,” that is usually Go or a *deliberately thin* Java/C#. Language choice: [05-language.md](./05-language.md).
