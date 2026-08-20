# 011 — New Lazuar Pay (focused reimplementation)

**Date:** 20 August 2026  
**Type:** Reference plan. **Not** an implementation order for the current .NET modular monolith. **Not** a commit to start a Go rewrite.  
**Repos:** this tree is the *old* Pay (judgment only). Sibling identity: `/Users/akmalfirdaus/Code/lazuar/lazuar-one`.  
**Source:** 20 August conversation after waves 001–260 landed on `main` (`e7bb07b0`). Written so we can refer later.

New Pay is **money + catalog + buyer checkout**. Merchant identity lives in **lazuar-one**. Buyers (cardholders) are **not** Zitadel humans. Tax later = a **provider**, not homemade MyInvois.

---

## How to read this folder

| File | Question it answers |
|------|---------------------|
| [00-why-leave.md](./00-why-leave.md) | Why stop this C# cathedral; why modular monolith hurt; why tax is a provider |
| [01-product.md](./01-product.md) | What focused Pay v1 must / should / later / never ship |
| [02-one-integration.md](./02-one-integration.md) | What Pay calls on One; what Pay must never hold |
| [03-first-slice.md](./03-first-slice.md) | Dogfood sequence: One login → keys → charge → receipt |
| [04-linux-shape.md](./04-linux-shape.md) | “Linux never paid that tax”: one tree, one linker, call the function |
| [05-language.md](./05-language.md) | C# vs Java vs Go for a new Pay |
| [06-platforms.md](./06-platforms.md) | Pay / One / Notify / Audit: map vs deploy; sell + reuse; 中台; Twitter clone |
| [07-separate-vs-one-binary.md](./07-separate-vs-one-binary.md) | If we still want four services; advantages of one Go binary |
| [08-bezos-door.md](./08-bezos-door.md) | Bezos mandate vs Linux; door vs room; AWS counterfactual |
| [09-old-pay.md](./09-old-pay.md) | How problematic this codebase is |
| [10-tracker-schema.md](./10-tracker-schema.md) | How to read the build tracker (rows, columns, `NP-*` IDs, waves) |
| [11-checklist.md](./11-checklist.md) | Living feature × status matrix — flip Status here |
| [12-first-slice-tracker.md](./12-first-slice-tracker.md) | Ordered S0/S1 dogfood steps mapped to IDs |

One’s own program for this sibling is `lazuar-one/plans/017-evals/08-dogfood-then-serve.md` §6 (Pay as Consumer-0). [02](./02-one-integration.md) restates that contract from Pay’s side.

Platform map (catalog, not day-one deploys): `/Users/akmalfirdaus/Code/dump/lazuars/docs/platforms`.

---

## Binding decisions

1. Do not grow this C# cathedral. Treat it as **reference** (SST unit × seats, wrap-rails, receipt ≠ tax invoice). No more feature work on it.
2. Do not rebuild `Modules/One` inside Pay. Merchants are One humans + One tenants.
3. Homemade MyInvois / UBL / consolidation is out of v1. Tax later = a **provider**.
4. One membership plane: a Pay merchant org **is** a One tenant unless Pay writes a reason not to.
5. One’s staging proof is **NOT PASSED**. Integrate the HTTP façade anyway; do not pretend Okta/SCIM is the next Pay ticket.
6. **Bezos is the door, Linux is the room.** Public `/v1` from day one. One Pay process. Function calls (or the same handler) inside Pay. HTTP to One because One already exists.
7. Notify and audit for **Pay writes** stay in Pay v1 (same process / same DB transaction). Do not stand up `lazuar-notify` / `lazuar-audit` as processes until a second caller exists.
8. Language for a new kernel, if we rewrite: **Go**. Not because C# cannot do it — because C#’s default toolkit rebuilt the museum.
9. Do not copy per-module schemas, in-process event bus as the way Pay talks to itself, or `twitter-spring-reactjs` (seven DBs, Eureka, start-order).

---

## Evolution of the cut (read this or 06/08 will contradict 01/02)

The 20 August thread changed the kernel once, on purpose.

**First cut (before treating One as already built):** one Go binary, packages `internal/one`, `internal/pay`, `internal/notify`, `internal/audit`, one Postgres, public `/v1`. Thin identity *inside* Pay. Zitadel / OpenFGA / SCIM left out.

**Second cut (One already exists as a sibling repo):** One is the **justified extract** — a different product (WorkOS-shaped), already a process. New Pay does **not** absorb it and does **not** reimplement `Modules/One`. Pay is Consumer-0 over HTTP. Buyers stay in Pay (checkout profile / magic link). Mail and audit for Pay’s own writes still live **in Pay**, not as two more services.

Both cuts agree on: one Pay process, `/v1` as the sold door, no homemade LHDN, no four-service day one for Notify/Audit.

---

## What this folder is not

- Not an order to rewrite Pay this week.
- Not an order to FF-merge or commit these files.
- Not a revert of waves 001–260 on `main`.
- Not a plan to implement issues 261–334 on the old tree.

Build tracking is [11-checklist.md](./11-checklist.md). All Status cells are `todo` or `refuse` until new Pay exists. The old C# tree does not count as `done`.
