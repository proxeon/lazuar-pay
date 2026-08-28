# 11 — What we do next (shared picture, not a checklist)

**Date:** 28 August 2026  
**After:** [00-evaluation.md](./00-evaluation.md) and the ten uncondensed reports.  
**Type:** Direction. Not an implementation order. Not tickets. Not code.

The 020 reports already argued the evidence. This file is the **same-page sentence**: what Pay is, what the next program is for, what “done” looks like, and what we will not pretend is the same job.

---

## 1. What we are, said plainly

Lazuar Pay today is a **hosted cashier for One workspaces**.

Staff sign in through One. They paste processor keys per rail. They mint a pay link. A buyer opens a public page, pays on the processor, and Pay writes an Official Receipt and a two-line journal. Buyers never become One humans. Hub on 8080 is a different product.

That cashier is **honest enough to dogfood** after the 002 occupancy and webhook work. It is **not** production-ready in the 013 sense (real public HTTPS, fail-closed config, a probe that means the database is up, a captured One pause, persist-before-PSP on the rail we actually charge). It is **not** a payments API another team can point at this afternoon.

If we say “platform,” “Stripe-shaped kernel,” “we have API keys,” or “we have merchant webhooks,” we are lying. If we say “hosted cashier for One shops, `/v1` is the door, second apps are the next program,” we are telling the truth.

---

## 2. Two jobs that look like one

“Production ready” and “other apps can integrate” are **not** the same queue.

| Job | Who it is for | What success feels like |
|-----|---------------|-------------------------|
| **A. Kernel door** | Lazuar’s next app, or a stranger, without cloning merchant Vite | Mint a checkout with a machine credential, send the buyer to pay, learn paid from Pay without polling a human session |
| **B. First-party go-live** | Us, charging a real test card on our own merchant + checkout | One Malaysian (or Stripe test) rail, public URLs, One pause actually stops charges, config that refuses to boot empty |

Occupancy lock is **off the front of both queues**. Refunds, subscriptions, SST, e-invoice, escrow, and Hub cutover are **on neither queue**. Mixing them is how we spend a month looking busy and still have no second caller **and** no live shop.

We can interleave **small** host honesty from B (a ready probe that fails, production that will not boot without a wrap key) while building A. We must not wait for B’s whole human loop before A, and we must not wait for A’s sample app before B’s ready probe.

The user’s original ask — secret key, webhooks, clean API, M2M — is **job A**. Treat A as the named next program unless we explicitly choose “charge ourselves first.”

---

## 3. Job A — make Pay callable by another app

A second app should not clone `:5178`. It should not hold a staff browser session. It should not poll forever. It should not talk to Hub.

The shape is already decided by older law and 020’s live read:

**Identity stays One.** Merchants (including machines) belong to a One workspace. `org_id` is that tenant id. We do **not** mint Pay-local `sk_live_` keys, we do **not** grow a Pay user table, we do **not** put one god-key in Pay’s env that speaks for every shop. One already mints `lzr_sk_`. Pay’s job is to **accept that Bearer as a merchant credential** on the mint and read doors — which it currently does not, because the membership check is shaped for a human JWT.

**`/v1` stays the only door.** Bezos is the door; Linux is the room. Merchant and checkout are already HTTP clients of `/v1`. A second app should be the same. We do not invent `/v2` to look finished. We do not generate a cathedral SDK. Plain HTTP, snake_case, the problem object we already return. When a mint succeeds, the stranger should get a **pay URL** they can hand a buyer — today they have to know our checkout origin.

**Three webhook planes stay three planes.**

- One → Pay (pause a shop) already exists. Other apps do not call this.
- Processor → Pay (money) already exists. Other apps do not call this.
- **Pay → the app** does not exist. That is the missing webhook. After Pay has actually fulfilled, it should POST a signed `payment.completed` to a URL that shop registered, with a secret shown once, retried at-least-once, idempotent on the charge. One event type is enough for the first hatch. Copy **judgment** from One and from Hub museum (HMAC, secret once, retry, SSRF). Do not copy their tables or dispatcher jobs.

**Docs and sample follow the door, they do not lead it.** Hub `examples/` teaches the museum. Root README hides Pay. When the machine credential and the outbound event exist, a small Node (or similar) sample against **8081** is the proof: provision a One key, mint, pay on Test or a sandbox rail, verify the signature, unlock a toy row. Until then, do not retarget the Hub sample by changing a base URL. The JSON, auth, and webhook dialect are different products.

**Done for job A** is a sentence a stranger can run: “I created a One workspace, minted a scoped key, called Pay, sent a buyer to a URL Pay gave me, and my server learned paid from a signed POST.” Polling remains as catch-up, not as the happy path. Spec grows when the maps grow. Honesty scrape stays green.

That is a **hatch**, not a billing engine. Pagination, refunds, subscription APIs, and a published client library wait until this sentence is boring.

---

## 4. Job B — make the cashier safe to run as ourselves

This is the production-ready work that does **not** require a second caller.

We already have the money story in hermetic tests. What we do not have is a process that tells the truth in production:

- A ready probe that is false when Postgres cannot be reached.
- Production that **refuses to start** without wrap key, CORS origins, and a real One base URL / connection string — not laptop defaults with a green health check.
- Images and compose that are not Development-with-empty-secrets. Root Hub compose stays museum.
- On the **one rail we actually dogfood**, do not create a second processor session if Pay fails after the processor already created one. Stripe has a story here; CHIP/Billplz/Xendit/Razorpay still “HTTP then persist.”
- Prove One pause on a **real** `tenant.suspended` against 8081, and write down how ops registers Pay’s public URL with One (loopback will not work). Per-org webhook secrets stay; the process-wide secret stays a one-shop fallback, not the multi-shop design.

Pick **one** live rail for dogfood. Five hosted names are not five production programs. Invite the second human through One, not through a Pay-made user.

**Refunds and expire-at-processor** belong here only if we will take real processor money with a 30-minute reservation. An abandoned start that expires in Pay while the buyer still pays at CHIP leaves cash at the processor and no receipt. That is a money hole for *us*, not a kernel door for *them*. Do not staff a refund product before we have chosen to take that cash.

**Done for job B** is: we charged a sandbox (or CHIP test) buyer on public HTTPS, Pay showed one receipt, a webhook retry did nothing, a suspended workspace could not start a new charge, and the process would not have booted with empty secrets.

Do not call that “platform.”

---

## 5. What we will not do in the next program

These keep showing up as “while we’re here.” They are how 020 fails.

- Rebuild identity inside Pay. No PAT, no OpenFGA admin, no members table, no homemade API keys.
- Import Hub as if it were Pay: MediatR, rail factory gravity, Hub generated types, Hub outbound dispatcher, retarget ops/portal at 8081.
- Tax, LHDN, SST computation, “Official Receipt means e-invoice.”
- Escrow, factory, registrar, WhatsApp, Xero, four new processes before a second caller exists.
- A published SDK or waiting on One’s npm client.
- Spec for doors we have not mapped. Spec lag was 019’s problem; inventing events in TypeSpec before the handler is how it comes back.
- Occupancy as the headline again. The lock is in. Leftovers (lazy TTL, client slot key, fulfill without parent lock) are not the reason another app cannot integrate.

---

## 6. Recommended default

Unless we say otherwise: **the next named program is job A** — other apps can integrate — because that is the gap 020 was asked to name, and because 019 already spent a cycle making the cashier honest.

Inside that program, the story in order is:

1. A machine credential that actually mints (One’s key, Pay’s gate).
2. A pay URL on the mint so the app does not reverse-engineer checkout.
3. One signed “this payment completed” POST so the app does not poll a staff JWT.
4. A sample and a README that talk about **this** Pay, not Hub.

Cheap B items that lie in production (`/ready`, empty Production config) can ride along because they are small and they keep us honest about the cashier we already have. The rest of B waits until we decide “we are going live for ourselves this month.”

If instead we decide we must take a real card **before** a second Lazuar app exists, we invert: job B first, README stays “hosted cashier,” kernel doors stay named-missing. That is allowed. Calling it integration is not.

---

## 7. How we stay on the same page

- Read this file for **intent**. Read `01`–`10` for **evidence**. Do not implement from a parent table.
- When a PR is proposed, ask: is this A, B, or refuse? If it is refunds-as-kernel, or keys-as-a-Pay-table, or Hub-as-sample, it is refuse.
- Success is two sentences, not a board of eighty tickets: a stranger learned paid from Pay; we can boot and charge ourselves without laptop-shaped lies.

When those two sentences are true, we can talk about pagination, refunds, subscriptions, and a typed client. Not before.

Implementation checklists (one intent per file): [`checklist/README.md`](./checklist/README.md). Freeze: [`checklist/decisions.md`](./checklist/decisions.md).
