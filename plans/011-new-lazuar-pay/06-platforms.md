# 06 — Platform map vs deploy

**Date:** 20 August 2026  
**Map (catalog):** `/Users/akmalfirdaus/Code/dump/lazuars/docs/platforms` — One, Pay, Notify, Media. Audit was named in conversation; there is no `lazuar-audit.md` in that folder.

Four names on paper is a **platform map**. Four processes on day one is the expensive shape again.

The platform README already says the right sequence: prove One with **one app**, extract when pain is real. Principle 4 (“separate codebases”) is the target *catalog*, not the first deploy. Pay → event → One for entitlements is exactly the seam that produced “paid but no access” and “access without a second look at money” in the old tree.

---

## Two trees today (Pay and One)

You are not building two modules of one product. You are building **two products**, and old Pay already contains a **third** identity stack. That is why it looks nice and still feels wrong.

| Tree | What it actually is |
|------|---------------------|
| `lazuar-pay` | Checkout-as-a-service: gateways, subscriptions, ledger, dunning, receipts. Inside it, `Modules/One` is a homemade IdP (`GlobalUser`, BCrypt, cookie JWT, workspace slug, invites). That module is how you got `CLIENT` on the cookie and `ADMIN` in JSON. |
| `lazuar-one` | Identity *platform*: Zitadel humans, OpenFGA tuples, SCIM, SSO, OIDC apps, API keys, webhook fan-out. Closer to WorkOS than to a `users` table. Its defects are a different catalog from Pay’s money bugs. |

So today: Pay’s money, Pay’s toy identity, and a second repo that wants to be real identity. Two services “for cleanliness” hid a three-body problem.

**After the second cut:** new Pay does not reimplement `Modules/One`. Merchants live in lazuar-one. Buyers do not. See [02-one-integration.md](./02-one-integration.md).

Zitadel, OpenFGA, SCIM, enterprise SSO, IdP-initiated login do **not** belong in Pay’s kernel. They are why One already has ~90 identity defects and CI that fakes the IdP. They are a *provider* extract — like MyInvois. Do not start a Pay rewrite as Clerk-plus-Stripe.

---

## What each name actually is

| Name | Hard problem | Own process on day one? |
|------|----------------|-------------------------|
| **Pay** | Charge once, webhook once, ledger true | No. This *is* the product. One Pay binary. |
| **One** | Who / which org / may they open the app | **Already extracted** (sibling repo). Justified: different product, already a process. New Pay does not absorb it. Until that existed, the advice was “thin One inside Pay, not Zitadel+FGA+SCIM.” |
| **Notify** | Deliverability, bounces, OTP vs marketing | Not until a **second** app shares a sending domain, or OTP must not share fate with receipts. Pay v1 keeps transactional mail **in Pay**. |
| **Audit** | Tamper-evident “who did what” | Almost never a service first. If it is a network hop, you get “business commit succeeded, audit lost.” Treat as a **table + function** in Pay (and in One, for One writes) until a regulator wants an independent log. |
| **Media** | Files / stream | Do not build. |

There is no `lazuar-audit.md` in `docs/platforms`. Do not invent a fifth platform to complete a slide.

---

## Sell and reuse are the same code path

You can sell platforms **and** reuse them. You cannot do both by starting as four services. A sold platform is a **contract other people can call**. A service mesh is an implementation you do not need until a stranger is on the other side of that contract.

A platform exists when:

1. A second consumer integrates from **docs + SDK**, not by opening your database.
2. That consumer can be **your** next product **or** an external team.
3. Money, identity, and mail have **one** implementation both of you hit.

Your own checkout, invites, and receipts must go through that same surface. Dogfood is the product. A private `Modules.Pay` that only the monolith uses, plus a future “public API,” is how you get two Pay’s.

**Shape:**

```text
internal/pay/     internal/notify/     internal/audit/     (Pay binary)
        \                |                 /
                    functions you call
                           |
                    http/v1/   (the thing you sell)
                           |
              your ops UI  ·  later: customer app / second Lazuar app

lazuar-one  (already a process)  ← HTTP, HMAC webhooks
```

- **Reuse:** `pay.MarkPaid` writes ledger + receipt + audit in one transaction; enqueue mail. Merchant staff from One membership.
- **Sell:** operations hang off `/v1/...` with API keys (`lzr_sk_`) and tenant isolation.
- First product (ops + hosted checkout) is a client of **http/v1**, or of the same handlers. Do not give the first app a back door into tables.

| You need in order to sell | You do not need yet |
|---------------------------|---------------------|
| Stable HTTP + version (`/v1`) | Four repos, four deploys for Pay’s own nouns |
| Tenant isolation on every route | Zitadel + OpenFGA + SCIM inside Pay |
| Idempotency on money and OTP | Event bus between Pay’s own packages |
| Docs a stranger can finish | Media, audit-as-a-service |
| One SDK generated from that HTTP | “Apps never talk to vendors” as a hard rule on day one |

Pay → One **events** in the platform README are for *external* apps you do not compile with, and for **One → Pay** membership/suspend. Inside Pay, that link is a function call. When a customer’s app cannot share your process, *they* consume `payment.succeeded`. You do not consume your own events to talk to yourself.

**Order if you want something to sell**

1. **Pay** — BYOK checkout, webhook, ledger. First customer: your own CaaS UI. Second: another of your apps, or an external merchant.
2. **One** — already started. Keep it thin for Pay: user, org, session, API key, membership. Zitadel/FGA when a **buyer** asks for SSO, not so the diagram is complete. Staging proof is NOT PASSED; still integrate the façade.
3. **Notify** — extract or sell only when a second product shares a sending domain, or you sell “transactional email.” Until then it is `internal/notify` behind Pay’s `/v1`.
4. **Audit** — a table written in the same transaction. Sell an audit *API* later if someone wants a feed.

**The failure mode to refuse:** building `lazuar-one`, `lazuar-pay`, `lazuar-notify`, `lazuar-audit` as four services so the platform map looks shippable — while the only app is still you. Selling requires a **caller**. Reuse requires that caller to be you first, through the public door.

---

## Is this how 中台 was built? One service?

**No.** Zhongtai was not one service. It was also not “draw four platforms, then invent the first app.”

**What 中台 was.** Alibaba’s 2015–2018 line was **大中台、小前台**: many existing front businesses (Taobao, Tmall, 1688, …) share **capability centers** — trade, commodity, user, payment, marketing. Those centers were **many services and many teams**, already at massive scale. Shared *ideas* (one checkout, one user, one item model), not one `main()`.

**How it was built.** Fronts existed first. Pain was real: every BU reimplemented pay and membership. Then they **extracted** shared capabilities. That is “start as a monolith (or as many products), extract when a second caller exists.”

**What happened next.** By ~2019–2023 the “big middle platform” got a reputation for **slow fronts**. Shared teams became a queue. Alibaba thinned 中台, gave BUs more autonomy, and the industry joked that everyone was still *building* 中台 while Ali was *拆* it. The lesson from the source is not “one Linux binary.” It is **do not make the shared layer thicker than the products that feed it.**

| Zhongtai (Ali) | You, if you copy the slide | You, if you copy the *history* |
|----------------|----------------------------|--------------------------------|
| Many live 前台 | One founder, no second app | Pay kernel + existing One |
| Many 中台 services after scale | Four repos on day one | `/v1` you can sell later |
| Shared pay after Taobao already charged | Pay as a service before a merchant | Charge in-process, sell HTTP when a stranger calls |
| Later: 中台 too fat | Same fat, no GMV | Keep the middle **thin** |

Selling a platform is 中台 as a **product**. Ali sold that story to the industry *after* internal reuse was proven. They did not bootstrap Alibaba as “identity service + pay service + notify service” with zero storefront.

Zhongtai ≠ one service. Zhongtai ≠ start with four services either. Zhongtai = **reuse after you have more than one front**. A Linux-shaped Pay binary with public `/v1`, calling the One that already exists, is the small-company version of that history. Four named platforms as four deploys is the 2018 slide, which even Ali walked back.

---

## The Twitter clone (why not four services that look like a platform)

[merikbest/twitter-spring-reactjs](https://github.com/merikbest/twitter-spring-reactjs) is the same mistake with a nicer README. Install asks you to create **seven Postgres databases** and start services **in order**. Architecture was the feature.

Those four Lazuar names are **one user journey**: sign in (One) → belong to an org → pay → write a ledger row → send a receipt → append “who did what.” In the clone, “like a tweet” hops user-service → tweet-service → notification-service → maybe email-service, each with its own DB. A retry can notify twice or notify without a like. We already saw that as Pay event → One entitlement, inbox with empty tenant, parked events.

Go fights that instinct because the default unit is a **function in a package**, not a Spring Boot app per noun. Go will not stop you from opening four `cmd/`s — the monolith *choice* is what stops the clone. See [05-language.md](./05-language.md) and [07-separate-vs-one-binary.md](./07-separate-vs-one-binary.md).

---

## Practical order (Pay-side)

Ship **one Pay process** that can: take a BYOK payment (merchant signed in via One), write a ledger row, send one email, append an audit row. Keep the **names** as packages so the docs stay true. Extract Notify only when a second app or a real sending-domain requirement shows up.

Separate services are how you *sell* platforms to strangers who cannot share your process. One Pay monolith is how you *prove* Pay. You are still in the prove step for Pay. One is already in its own prove step (staging **NOT PASSED**).
