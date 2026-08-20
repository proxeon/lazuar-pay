# 10 — Tracker schema (new Lazuar Pay)

**Date:** 20 August 2026  
**Living companion:** [11-checklist.md](./11-checklist.md)  
**Slice map:** [12-first-slice-tracker.md](./12-first-slice-tracker.md)  
**Product law:** [01-product.md](./01-product.md), [02-one-integration.md](./02-one-integration.md), [03-first-slice.md](./03-first-slice.md)  
**Type:** Tracker only. Not an implementation order. Not a rewrite of the old C# tree.

This schema is how we track **what to build in new Pay**. It is not the 007 competitor matrix (`LP-*`). Do not reuse `LP-` IDs here.

---

## Rows and columns

**A row** is one merchant, buyer, or integrator job (or one explicit refuse). One job = one ID.

**A column** is a field on that job. The living tables in [11](./11-checklist.md) always have:

| Column | Meaning |
|--------|---------|
| **ID** | Stable `NP-FAM-NNN`. Never recycle. |
| **Feature** | The job in one breath. |
| **Wave** | When it may be built. See below. |
| **Owner** | Who implements or is waited on. |
| **Dogfood** | `Y` if the [01](./01-product.md) dogfood test fails without it. |
| **Status** | Build state. Flip only here (or in 12 for slice steps). |
| **Notes** | Constraint, fail mode, or pointer. Not a second status. |

Do **not** add competitor columns. That is `plans/007-feats`. Do **not** add implementation tasks (“create `charges` table”) as rows. Those belong in a later impl program.

A row is allowed only if at least one of:

1. It is on the v1 / soon / later / never lists in [01](./01-product.md).
2. It is a One call or non-call in [02](./02-one-integration.md).
3. It is a pass/fail in [03](./03-first-slice.md).
4. It is a binding decision in [README.md](./README.md) that would otherwise be rebuilt by accident (refuse rows).

---

## ID families

Prefix **`NP-`** (new Pay). Family is the grep key.

| Family | Domain | Paper |
|--------|--------|-------|
| `NP-ONE` | Merchant identity via lazuar-one | [02](./02-one-integration.md) |
| `NP-CAT` | Catalog / products / prices | [01](./01-product.md) |
| `NP-CHK` | Checkout session / hosted page / pay link | [01](./01-product.md) |
| `NP-GW` | BYOK, Stripe, one MY rail, webhooks | [01](./01-product.md) |
| `NP-FUL` | Subscription / one-off fulfillment | [01](./01-product.md) |
| `NP-MON` | Journal, SST, refunds, disputes | [01](./01-product.md) |
| `NP-DOC` | Official receipt (not tax invoice) | [01](./01-product.md) |
| `NP-BUY` | Buyer / payer plane (not Zitadel) | [01](./01-product.md) |
| `NP-MAIL` | Transactional email inside Pay | [01](./01-product.md) |
| `NP-AUD` | Audit row on Pay writes | [01](./01-product.md) |
| `NP-API` | Public `/v1` door | [01](./01-product.md), [08](./08-bezos-door.md) |
| `NP-OPS` | Merchant ops UI as a client of `/v1` | [01](./01-product.md) |
| `NP-SOON` | Should-have, still Pay | [01](./01-product.md) |
| `NP-LAT` | Later, not v1 | [01](./01-product.md) |
| `NP-XX` | Refuse / never | [01](./01-product.md), [README](./README.md) |

If a later paper names a missed job, **add the next free number in that family**. Do not invent a second taxonomy. Prefer `NP-GW-010` over turning `NP-GW-006` into a folder.

---

## Wave

| Wave | Meaning | Build? |
|------|---------|--------|
| **S0** | One façade: SPA, `/me`, tenant, invite, key, `authz`, One webhooks | Yes — first. May be **blocked** on One. |
| **S1** | Money loop: BYOK → product → hosted pay → webhook → journal + `RCPT-` in one txn | Yes — dogfood. |
| **V1** | Rest of must-have (renew, refund, buyer portal, SST fail-closed, remaining mail/audit) | Yes — after S1 is boring. |
| **soon** | Quote, PAST_DUE dunning, partial refund, M2M, second gateway | After v1 dogfood. Still Pay. |
| **later** | Tax **provider**, extra rails, second-app entitlement, Notify extract | Not v1. |
| **refuse** | Never in this product | Do not build. Keep the row so we do not rebuild the museum. |

S0 + S1 = [03-first-slice.md](./03-first-slice.md). If a feature is not on that path, it is not S0/S1.

---

## Owner

| Owner | Meaning |
|-------|---------|
| **Pay** | New Pay implements it. |
| **One** | Lazuar One owns it. Pay must not reimplement. Tracker row exists so Pay does not grow a copy. |
| **both** | Pay implements by **calling** One HTTP (OIDC, `/me`, invites, `authz`, One webhooks). |
| **vendor** | A provider (MyInvois, a second rail) — not homemade. |

---

## Status (flip only when true)

| Status | Meaning |
|--------|---------|
| **todo** | Not started in new Pay. |
| **doing** | In progress. |
| **done** | Proven: test, or the dogfood path actually ran. Not “README says so.” |
| **blocked** | Waiting on One (staging proof, SMTP, copy-link, …) or a vendor. Name the blocker in Notes. |
| **refuse** | Will not build. Wave is also `refuse`. |
| **n/a** | Not Pay’s job. Owner is One or vendor. Leave the row so we do not grow a copy. |

New Pay does not exist yet. Seed **Status = `todo`** (or `refuse` / `n/a`). Do not mark `done` from the old C# tree. Steal **judgment** from that tree, not checkboxes.

---

## Dogfood

**Y** if this must be true for:

> A merchant signs in through One, opens Pay, pastes CHIP or Stripe keys, a buyer pays on the hosted page without a One account, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, a One-invited MEMBER can see ops and a VIEWER cannot charge.

Everything else is `—`. If you add a v1 row that is not `Y`, it does not belong in S0/S1.

---

## What not to track here

| Leave out | Where it lives |
|-----------|----------------|
| Old-tree bugs 261–334 | `issues/` on this repo — do not implement |
| Competitor cells (Stripe has X) | `plans/007-feats` |
| Go vs C# vs Linux vs Bezos | [04](./04-linux-shape.md)–[08](./08-bezos-door.md) — locked, not rows |
| One’s own staging proof / SCIM / npm publish | One `plans/017-evals` |
| SQL migrations, table names, package layout | Later impl program |

---

## Locked (not rows)

These are already decided. Do not add tracker rows that reopen them.

1. New Pay is a **separate origin** from One. One tenant id **is** Pay `org_id` unless Pay writes a reason not to.
2. Buyers are **not** Zitadel humans.
3. One Pay process, one Pay database. Notify/audit for Pay writes stay **in Pay** in v1.
4. Public `/v1` from day one. No second app reading Pay tables.
5. Homemade MyInvois is out. Tax later = a provider.
6. Wrap-rails only. No Stripe Billing `subscription.updated` as source of truth.
7. Language for a rewrite, if we rewrite: **Go** ([05](./05-language.md)). The tracker does not care which repo path you open first; it cares the job exists.

---

## How to flip a cell

1. Change **Status** (and Notes if blocked).
2. If S1 dogfood just passed, flip every **Dogfood = Y** row that was actually proven — not the ones you skipped.
3. Update the count table at the top of [11](./11-checklist.md).
4. Do not delete refuse rows.
