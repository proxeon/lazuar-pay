# 07 — Separate services vs one Pay binary

**Date:** 20 August 2026

Separate services are a **product and ops choice**, not a language choice. Go will not make four binaries cheaper. You will re-buy the tax: contracts, dual writes, and “start this before that.”

**Already decided for One:** lazuar-one exists. That split is taken. This paper is about Pay / Notify / Audit, and about not splitting Pay into Commerce-shaped processes again.

---

## What “separate” actually means

Each name is its own process, its own database, its own deploy. Pay cannot `BEGIN` a ledger row and One’s entitlement in the same transaction. Notify cannot append audit in the same commit as “email accepted.” Every cross-cut becomes:

1. Write locally.
2. Outbox (or you lose the message).
3. Other service consumes **at least once**.
4. Idempotency on the consumer, or you double-grant / double-email.
5. A versioned event (`payment.succeeded` v1) you cannot casually rename.

That is the honest implementation of the arrows in the platform docs. It is also how Pay’s parked events and One’s “audit outbox completed on fail” happened.

---

## What each extra split costs

| Split | You gain | You pay immediately |
|-------|----------|---------------------|
| **Pay ↔ One** | Money and identity can scale/fail apart; you can sell Pay without shipping your IdP | Paid but no access, or access without pay, until the consumer is perfect. Token/org ids must be stable across repos. **We accept this** because One already exists; money stays true in Pay even if membership webhooks lag. Do **not** put buyer entitlement in One. |
| **Pay ↔ Notify** | Mail outage does not take checkout (if designed that way) | Receipt after pay is eventual. OTP/magic-link for the **buyer** is now a distributed critical path — worse than in-process. |
| **Anything ↔ Audit** | A log you can claim is independent | Every write is two systems. If audit is down, you either block the business write (audit is not separate) or lose the trail (audit is a lie). |

Audit as a **service** is the weakest of the four. A sold audit *API* (query a feed) can sit on a table in Pay. A sold audit *process* only pays off if a regulator wants a different operator or WORM store.

---

## If you still want more services, do not start them all

Build **Pay first**, talking to existing One. When a **second product** shares a sending domain, **extract Notify**. Audit stays a table until someone buys a feed.

That is Zhongtai’s history and the opposite of the Twitter clone (user-service + tweet-service + notification-service + email-service on day one).

---

## Rules that keep a split from becoming that repo

1. **No shared database** across processes. If they share Postgres schemas “for convenience,” you have a distributed monolith with extra latency.
2. **No sync HTTP for the money path as the only grant.** Pay must not `POST One/grant-buyer-access` in the webhook as the only fulfillment — One down = webhook retries = mess. Buyer access is **Pay’s subscription/session row**. Staff access may lag on One webhooks.
3. **One writer per fact.** Pay owns “paid.” One owns “may use merchant ops.” Notify owns “accepted by SES” when it exists. Do not let both store entitlement.
4. **Idempotency keys on every cross-service command.** Same `payment_id` → same email, same journal.
5. **You are the first client of the public `/v1`.** No back-door SQL from the next app.
6. **Same language optional.** Four Go services still need Eureka-equivalents: compose order, health, tracing. Budget that as product work.

---

## Why not four services first

You want to **sell** platforms. Buyers buy a working checkout and a login, not a compose file with four health checks. Separate services help when a **second consumer or a second team** exists. Until then they are four Twitter clones of yourself: gigantic, and the feature is the wiring.

If you want separate services **on week one** (Pay + Notify + Audit + a second One), expect the same waste as `twitter-spring-reactjs` — gateway, many DBs, start order — only now the domain is money, so the bugs charge twice.

---

## Advantages of one Go Pay binary (with One already out)

**Advantage: Pay / Notify / Audit stay products, not wiring.** You still sell `/v1/pay`. You do not sell Eureka, start-order, and seven databases.

**One transaction for the money journey.** Charge → ledger → receipt number → audit row can share one `BEGIN`/`COMMIT`. Paid-but-no-receipt-number stops being a distributed race. (Staff membership is One’s; that lag is accepted.)

**One migration timeline for money.** One Postgres for Pay, one folder of SQL. You do not justify a Billing migration because Commerce cannot join.

**Dead code can die.** Notify is a package. If a template is unused, delete the file. You do not park an integration event so the bus does not treat “no handlers” as success.

**Debugging is a stack.** `MarkPaid` → `EnqueueReceipt` → `AppendAudit`. No inbox job with empty tenant.

**The compiler is the contract inside Pay.** Rename `RecordPayment` and every caller breaks in one build. External apps still use versioned HTTP. You do not version events to talk to yourself.

**OTP/magic-link for buyers does not need a second network to succeed.** Receipt and checkout live in the same binary. Notify extract later if marketing volume can take down OTP.

**Audit is honest.** `audit.Append` in the same transaction as the write. A down “audit service” cannot silently drop the trail or block pay. When someone buys an audit *feed*, you expose a query API on that table.

**Sell and reuse are the same door.** Ops/portal is the first client of `/v1`. A later merchant or a second app uses the same routes.

**Ops is one Pay binary + One’s existing compose.** No config-server for Pay’s own nouns.

**Extract stays possible.** When a second product shares a sending domain, Notify can become a process and consume an outbox you already have. When a stranger must not compile with Pay, they already speak `/v1`. You split because a **caller** exists, not because the slide had four boxes.

**What you are not giving up:** Zhongtai-as-catalog (shared pay, identity, mail). A public platform story. BYOK Stripe/CHIP. One as the identity platform without dragging Zitadel into Pay.

**What you are giving up on purpose:** four deploys for Pay’s own journey, and the feeling that the architecture is the product. That feeling is what made the C# modular Pay and that Twitter repo gigantic.

One Pay kernel. Package names for notify/audit. HTTP for customers **and** for One. Function calls for Pay talking to itself.
