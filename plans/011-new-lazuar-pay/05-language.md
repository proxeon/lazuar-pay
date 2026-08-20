# 05 — Language: Go, not C#, not Java (for a new Pay)

**Date:** 20 August 2026  
**Verdict:** **Go** for a new lazuar-pay that is one process, one database, pay in one transaction, and “I need that? call it.”

C# and Java can do the same job. They will keep offering the old building.

---

## Why not C# this time

C# is genuinely good at the *paper* job: async, tooling, LINQ, a single Windows-to-Linux deploy story, Stripe and QuestPDF and EF. The language did not force `TaxInvoiceId` to be a dumping ground.

**The culture around C# in business apps often does.** The default unit is a class, an interface, a `DbContext`, a MediatR handler, an integration event, a test that locks the allowlist. That toolkit is how you get a two-month product with per-module migrations and parked events. C# is not “bad.” It is **easy to look finished while the money path is still a rumor.**

A rewrite in C# is the highest risk of rebuilding the museum with cleaner names. C# is fine if you write it like a 2008 ASP.NET app: one project, one schema, SQL or a single EF context, handlers as functions, no event bus. That requires fighting the ecosystem every week. We just spent months losing that fight.

---

## Why not Java

Same gravity, Spring-flavored. Excellent HTTP, JDBC, boring ops on the JVM. Decades of research does not stop someone from creating `billing` and `commerce` packages that cannot share a transaction.

Java is the right pick if you are hiring a Java shop or you already think in JDBC. We are not. We are one product, early, allergic to walls.

The cautionary clone: [merikbest/twitter-spring-reactjs](https://github.com/merikbest/twitter-spring-reactjs) — tweet/user/list/tag/chat plus Eureka, config-server, API gateway, **seven Postgres databases**, start services in order. None of that is required to post a tweet. It is required to *look like* Twitter’s org chart. Real Twitter did not start that way. We already lived the C# version: One / Commerce / Billing / Notify as schemas and events. See [06-platforms.md](./06-platforms.md).

---

## Why Go

For a new Pay that is one process, one database, and “I need that? call it,” Go makes that the **default**.

- One binary. `net/http` in the standard library.
- Postgres via `pgx` or `sqlc`.
- Stripe and CHIP are HTTP JSON.
- SST is arithmetic in a function, not a module.
- Pay and ledger can be one `BEGIN`/`COMMIT`.
- Dead code is a file you delete.
- There is no respectable “parked integration event” culture.
- The annoying parts (`if err != nil`, fewer fancy types) are the same refusal C gave Linux: complexity has to be written out loud.

**What you give up.** Less ceremony for domain models. You will not get EF migrations-as-identity; use one migration tool (`golang-migrate`, Atlas) and one folder of SQL. Fewer “enterprise” tutorials that match the stack — that is the point.

**What Go will not do for you.** It will not stop `cmd/pay`, `cmd/one`, `cmd/notify` on week one. That would be the Twitter clone in a smaller language. One `main`. Packages named after the platforms. Same database for **Pay’s** money, mail, and audit.

**If you later hate Go**, you can still extract a service. You cannot later extract your way out of another two-month modular cathedral. Start in Go, one `cmd/pay`, one `internal/`, one database.

Judge the language by whether a new hire can follow pay → ledger → receipt in one stack without opening an event catalog. C# can do that if you refuse MediatR-as-architecture. Go will nag you until you do that. Java will let you do either. The last codebase failed the stack-trace test long before it failed a benchmark.

---

## What to steal from this repo, in any language

- Exclusive SST on the unit then × seats.
- Fail closed if you cannot decide tax.
- Never print a UUID as a document number.
- Do not say VALID unless a tax **provider** said so.
- One role vocabulary (on One for merchants; not two cookies).
- Wrap rails only (no Stripe Billing `subscription.updated`).
- Leave MyInvois to a vendor.

---

## After One already exists

Go is still the kernel language for **new Pay**. It is not an order to rewrite One in Go, and it is not an order to merge One into Pay’s binary. One is already a process. Pay calls it over HTTP — [02-one-integration.md](./02-one-integration.md). Notify/audit for Pay writes still belong in Pay’s process — [07-separate-vs-one-binary.md](./07-separate-vs-one-binary.md).
