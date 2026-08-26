# 018 — How strong is Lazuar Pay?

**Date:** 25 August 2026  
**Type:** Evaluation. Not an implementation.

---

## Question

How strong is our ideas for lazuar-pay? Thoughts, evaluations, demand analysis, painkiller or vitamin.

Beliefs on the table:

- Being a **kernel** where other apps can integrate to a payment gateway easily is a real opportunity. A lot of apps use Stripe by default because the API is clean. They swallow Stripe’s extra charge because integrating a local PSP (or another global) is plumbing that can take months to get right. We can also **reuse Lazuar Pay** for our other apps.
- **Escrow** in our apps is a real advantage.
- Many business owners cannot integrate a payment provider, or have no integration at all. They give away card numbers, QR, etc. Cards get reported to banks/BNM; scammers spam RM 0.01; BNM freezes accounts. Another angle: **set up payment integration commercially** for business owners.

---

## Verdict

The **kernel idea is the strongest thing in Lazuar Pay.** Escrow and “stop pasting your CIMB QR” are real pains, but they are **different products and different buyers.** Mixing them into one Processor page will make Pay a weaker Stripe *and* a weaker HitPay.

Pay today is a **hosted cashier** (merchant pastes keys, buyer pays on a link, Official Receipt). It is **not** yet a kernel other apps can swallow in an afternoon: there is no machine key (`lzr_sk_`) and no outbound `payment.completed` on the new host. The idea is ahead of the door.

---

## Painkiller vs vitamin

| Idea | For whom | Class | Why |
|---|---|---|---|
| One clean API → CHIP / Billplz / Xendit / Stripe | Your apps, then other SaaS | **Painkiller** *if* the API is Stripe-boring | Months of PSP plumbing is a real invoice. Developers already pay Stripe extra to avoid it. |
| Reuse Pay inside your other apps | You | **Painkiller** | You control the first two customers. This is how the kernel gets true. |
| Escrow.com / Tazapay in the app | Brokers, exporters, classifieds | **Painkiller there, vitamin here** | Acute if you sell a domain/machine. Irrelevant to an RM 80 deposit. |
| Stop WhatsApp card/QR / BNM freeze | Offline SMEs | **Painkiller**, crowded | The pain is real in MY. HitPay/Billplz already sell “send a link.” |

A painkiller is something they would pay for **this week** because the current path is hurting. A vitamin is better later.

- Stripe won as a painkiller for **developers**.
- HitPay won as a painkiller for **aunties with WhatsApp**.
- Escrow.com won as a painkiller for **people who do not trust the other party**.

Those are three GTM motions.

---

## 1. Kernel: “Stripe-clean, local rails”

The problem is correctly named. Apps default to Stripe because:

- Checkout + webhooks + test keys + docs fit in a day
- CHIP PEM, Billplz callbacks, Xendit invoice tokens, Razorpay links are each a **month of edge cases** (idempotency, amount mismatch, success URL ≠ paid, sandbox vs live)

They swallow 2.9%+ and weak FPX because **engineering time is more expensive than MDR** until volume hurts. In Malaysia that tax is worse: Stripe is the wrong method mix, FPX is what buyers have.

**What you would sell:** not acquiring. A **narrow kernel**:

```
POST /v1/checkouts { amount, currency, provider? }
→ hosted URL
webhook: payment.completed | payment.failed
receipt number, journal, same handler
```

One object, one signature, BYOK so money never sits with you. The app does not learn Billplz. That is ADR 019, and it is still the right bet.

**Demand (developer side)** is real and **not unique**:

| Already doing this job | Why someone still picks Stripe |
|---|---|
| Xendit | Closest SEA “Stripe” for APIs |
| HitPay API | SMB + methods; DX is secondary |
| Rapyd / dLocal / 2C2P | One API, many countries, sales-led |
| Primer / Spreedly | Orchestration for people who already have five PSPs |
| Stripe anyway | Docs, SDKs, status page, CLI, muscle memory |

You win only if **time-to-first-paid is Stripe-like** *and* the rail is CHIP/Billplz so MY buyers actually complete. “We wrap five PSPs” is not the pitch. “Your app speaks one Pay; the merchant picks the pipe” is.

**Honesty on strength:** the idea is **strong**. The **implementation is not there yet**. Until a second app (not the merchant Vite) can `POST` a checkout with a machine token and get a signed `payment.completed`, you do not have a kernel. You have a dashboard that mints links.

**First-party reuse is the demand you actually have.** Aura, the next Lazuar app, a sample Next.js. That is not a TAM slide. That is a paid (or captive) integration that forces the API to stay small. Stripe became Stripe because they were the default inside their own customers’ stacks. If Pay is not the default inside **your** apps, no outsider will trust it.

---

## 2. Escrow in the app

Covered last turn; short version for this frame:

- **Painkiller** for “I will not wire US$20k to a stranger.”
- **Vitamin** for the person minting RM pay links on `:5178`.
- Advantage vs escrow.com **dashboard** is ops + books + one API, not better escrow.
- Do not put it on the Processor card next to Test. It is a second surface (`funded → inspect → released`).

It does **not** strengthen the kernel unless brokers are a named ICP. For the kernel customer (an app), escrow is an optional `capability: escrow_hold` later — same way Stripe did not launch with Connect.

Your own 011 product note even parked escrow as later/vitamin. That was right for **v1 cashier**. It can still be right as a **v2 slice** if you sell trade/classifieds. It is wrong as the headline next to “we are Stripe for MY PSPs.”

---

## 3. SMEs pasting account numbers, QR, card PAN

This pain is **Malaysian and acute**:

- Business on a **personal** account → bank/BNM treats it like mule/scam traffic
- QR / account number in WhatsApp/IG → no reconciliation, wrong amount, “I already paid”
- **RM 0.01** probes (card testing / harassment) → reports → freeze
- Giving **card number** to a customer is not a PSP problem; it is a “we have no checkout” problem

**That is a painkiller.** HitPay’s whole company is “send a WhatsApp payment link this afternoon.” Billplz bills, CHIP pages, even a DuitNow QR from the bank app are partial substitutes. You would not be first. You would be **another hosted link** unless:

- They already have Billplz/CHIP keys and you are the **brain** (receipt, list, one URL, capacity), or
- You sell **setup as a service**: we open CHIP, paste keys, send the first link, teach them not to post the CIMB QR — a **services** business that uses Pay, not a kernel.

Those buyers do **not** care that your API is clean. They care that their cousin can tap Pay on the phone. DX is vitamin to them. WhatsApp share, Malay copy, FPX on the hosted page are the painkiller.

The freeze story is a **marketing truth**, not a product differentiator, unless you also do: named payers, no PAN collection, receipt, and “this is a business bill, not a personal transfer.” HitPay/Xendit already imply that. Your Official Receipt + “buyer has no login” is honest. It is not enough alone.

---

## Three ideas, two customers (the real risk)

```
                    ┌─ developers / your other apps
 KERNEL  ───────────┤  Stripe-shaped API, machine keys, webhooks
                    └─ pain: months of PSP plumbing

                    ┌─ WhatsApp SMEs
 LINK + SETUP ──────┤  hosted URL, FPX, don’t paste CIMB
                    └─ pain: freeze, no reconciliation

                    ┌─ brokers / exporters
 ESCROW ────────────┤  hold until accept
                    └─ pain: counterparty trust
```

You can **share the wrap-rails** (CHIP still CHIP). You cannot share the **homepage, onboarding, or success metric**.

| If you optimize for… | You build… | You look like… |
|---|---|---|
| Kernel | Keys, idempotency, events, sandbox, second-app sample | Thin Stripe / thin Xendit |
| SME WhatsApp | Share, QR, Malay, 30-second create | HitPay |
| Escrow | funded/inspect/release, USD KYC wait | Escrow.com with a Lazuar skin |

Trying to be all three in one merchant shell is how Hub became a cathedral. Pay already left that tree.

---

## How strong is the overall idea?

**Thesis (kernel + BYOK + MY rails): strong.**  
The world really does overpay Stripe and under-integrate Billplz. A focused host that makes local PSP as easy as Stripe is a real company — **if** the door is as boring as Stripe’s.

**Position vs incumbents:**

- **vs Stripe:** you win on MY methods and price of the *pipe* (their CHIP MDR, not yours). You lose on DX until you copy the boring parts (test keys, webhook retries, one event).
- **vs Xendit / HitPay:** they **are** the acquirer. You are software on the merchant’s existing account. That is either “why would I add you?” or “I keep my Billplz, I don’t re-KYC.” BYOK is the only reason you are not a worse Xendit.
- **vs doing nothing / CIMB QR:** SME painkiller, but HitPay is already there. You win as **setup + receipt + their existing PSP**, not as a new brand on WhatsApp.

**Demand you can actually touch this year:**

1. **Your other apps** (captive) — strongest, smallest TAM, makes the kernel real
2. **MY merchants who already have Billplz/CHIP** and hate the dashboard — medium, vitamin until share/receipt is nicer than HitPay
3. **Greenfield SMEs who paste QR** — large TAM, expensive GTM, HitPay’s home
4. **Escrow** — small TAM, high ACV, different sales

**Not a painkiller yet** for outsiders: they cannot integrate Pay like Stripe. Merchant UI and Test rail are dogfood. That is necessary. It is not the kernel.

---

## What I would treat as the strategy

1. **Finish the kernel for one second app you own.** Machine auth, `POST` checkout, signed `payment.completed`, idempotency, one sample repo. If Aura (or the next app) still talks to CHIP directly, the idea is a slide.
2. **Keep the merchant link** as the human proof of the same API — not a second product. Capacity, receipts, “don’t paste your account number” is the **SME skin** on the kernel, not the company.
3. **Sell setup commercially** if you want the freeze story: onboarding CHIP/Billplz + first pay link as a **service SKU**, kernel underneath. Do not pretend that is a developer platform.
4. **Escrow later**, named ICP, separate create-offer. Catalog yes; Processor card no.
5. **Do not become an orchestrator or an acquirer.** Primer and Xendit have more money. Your wedge is **one checkout object + BYOK + MY receipt**, reused by your apps.

**Bottom line:** the opportunity you believe in — **kernel so apps don’t swallow Stripe or spend months on local PSPs** — is the right one, and it is a painkiller **for developers and for your own suite**. Escrow and QR-freeze are real, but they are **other** painkillers. Pay gets strong by making the Stripe-shaped door true (starting with reuse in your apps), then letting SMEs and escrow sit on that door — not by building three companies in one dashboard.
