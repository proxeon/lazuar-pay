# 13 — One monolith vs several services (before development)

**Date:** 20 August 2026  
**Question:** Can we build lazuar-one, lazuar-pay, lazuar-media, lazuar-notify, etc. as **one monolith** instead of several services? Advantages, cons, which has more?

**Verdict:** one process has more advantages **for new Pay, now**. Several services have more advantages **later**, when a second product or a stranger is actually calling. Given One already exists: **do not mega-merge, do not five-deploy.** Ship **existing One + one Pay binary**.

Related: [06-platforms.md](./06-platforms.md), [07-separate-vs-one-binary.md](./07-separate-vs-one-binary.md), [08-bezos-door.md](./08-bezos-door.md), [14-google-aws-microsoft.md](./14-google-aws-microsoft.md).

The platform **names** (One, Pay, Notify, Media) can stay. The **deploys** should not multiply until a name has a second runtime.

---

## Verdict

| Choice | When it wins |
|--------|----------------|
| **One process** (Pay + mail + audit + files-if-you-must, public `/v1`) | One team, no paying integrator yet, money path must commit in one transaction |
| **Several services** | A second app cannot compile with you, a second team pages at 3 a.m., or a sold SKU must fail/scale/bill alone (S3, not “Notify package”) |
| **Hybrid you already have** | **Keep lazuar-one as its own process.** It is already the justified extract. Do **not** stand up Pay, Notify, Media, Audit as four more processes |

**Do not merge One back into Pay** to “make a Linux kernel.” One is Zitadel + OpenFGA + SCIM + OIDC apps — a different product and a different defect catalog. Absorbing it re-creates `Modules/One` inside money.

**Do not start Media.** There is no second product waiting on blobs or VOD.

**Put Notify and Audit inside new Pay** until a second sending domain or a regulator wants a separate log.

So: **one new Pay monolith + existing One**. That has more advantages than either “one giant mega-repo of everything” or “five services on week one.”

---

## What you are comparing

```text
A. Mega-monolith          One binary: identity + pay + mail + files + stream
B. Several services       one / pay / notify / media / audit  — five processes, five DBs
C. What you should ship   lazuar-one (exists)  +  one Pay binary (money, mail, audit, /v1)
```

C is not a compromise slogan. One already exists; Pay does not. Media has no customer. Notify for receipts is not a company.

---

## Advantages of one monolith (Pay’s nouns in one process)

**One transaction for the money journey.** Charge, ledger, receipt number, audit row, “enqueue receipt email” can share `BEGIN`/`COMMIT`. Paid-but-no-receipt and access-without-pay stop being distributed races *inside Pay*.

**One migration timeline.** One Postgres for Pay. No Commerce-vs-Billing dual-use columns. Dead tables drop in one commit.

**Dead code can die.** A template is a file. You do not park an integration event so the bus treats “no handlers” as success.

**Debugging is a stack.** `MarkPaid` → `EnqueueReceipt` → `AppendAudit`. No inbox job with an empty tenant.

**The compiler is the contract inside Pay.** Rename `RecordPayment` and every caller breaks in one build. External people still use `/v1`.

**Ops is one binary + One’s existing compose.** No “start Eureka first,” no seven databases to post a tweet (the Spring Twitter clone).

**Sell and reuse can still be the same door.** Your ops UI is the first client of `/v1`. A later merchant uses the same routes. Bezos is the **door**, not “four health checks.”

**Extract stays possible.** When a second product shares a sending domain, Notify can become a process and consume an outbox you already have. You split because a **caller** exists.

---

## Cons of one monolith

**A stranger cannot `import internal/pay`.** Without `/v1` from day one, you have a product nobody else can integrate. That is the Bezos objection, and it is real. Fix: public HTTP, no back-door SQL — still one process.

**One blast radius inside Pay.** A runaway mail loop can take checkout if you let it. Mitigation: queues *inside* the process, timeouts, later extract Notify if marketing volume can starve OTP.

**Identity and money in one binary is a bad merge *if identity is already Zitadel-shaped*.** That is why One stays out. A *thin* `users` table inside Pay would have been fine **before** One existed. It exists.

**Media (encode, large blobs) will dominate the process** if you put Stream in the same binary as checkout. That is a real extract *when you have VOD*. You do not.

**Harder to sell “just Notify” or “just Media” as a SKU** until they are processes. You are not selling those SKUs this year.

**Team contention later.** Two-pizza teams want separate release trains. You are one team.

---

## Advantages of several services

**Failure domains.** Mail outage need not take checkout — *if* you designed the outbox so pay still commits. IAM/S3/EC2 at AWS scale is this. You do not have us-east-1.

**A stranger can buy one door.** S3 is not “import Amazon.” That is how you sell a platform *to other companies*.

**Independent scale and compliance.** Object storage, PCI checkout, and a video encoder have nothing in common. Relevant **after** those loads exist.

**Ownership.** Different SLAs, different pages at 3 a.m. Relevant **after** a second team.

**You already did this once for One.** Pay should not hold a Zitadel PAT. That split is earned.

---

## Cons of several services (the expensive shape)

Every arrow becomes: local write → outbox → at-least-once consumer → idempotency → a versioned event you cannot casually rename.

| Split | You pay immediately |
|-------|---------------------|
| Pay ↔ One | Paid but no staff access (acceptable lag). **Do not** put buyer entitlement in One or you get paid-but-no-access |
| Pay ↔ Notify | Receipt is eventual; buyer magic-link is a **distributed critical path** |
| Pay ↔ Audit | Business commit succeeded, trail lost — or pay blocked because audit is down |
| Pay ↔ Media | Checkout waits on S3/ffmpeg, or you invent a second consistency story for “paid, file missing” |

You also pay: five deploys, five health checks, start order, tracing, “which DB has the truth.” That is the Twitter clone with a nicer README. Architecture becomes the feature. In money, a seam slip **charges twice**.

Zhongtai was **not** “draw four platforms, then invent the first storefront.” Fronts existed; they extracted when a second BU reimplemented pay. Ali later thinned 中台 because the shared layer got fatter than the products.

AWS could not stay a Linux kernel **as a product** — customers buy S3, not `import amazon`. You are not AWS. Pretending you are is four fleets before a second caller. See [14-google-aws-microsoft.md](./14-google-aws-microsoft.md).

---

## Per name (keep the map, delay the process)

| Name | Own process now? | Why |
|------|------------------|-----|
| **One** | **Yes (already)** | Different product. Pay calls HTTP. Merchants are One tenants. Buyers are not. |
| **Pay** | **This is the new binary** | Charge once, webhook once, ledger true |
| **Notify** | **No** | Receipts/dunning/magic-link live next to money. Extract when a **second app** shares a sending domain, or marketing can take down OTP |
| **Audit** | **No** | Table + `Append` in the same transaction. A feed API later if someone buys a feed |
| **Media / Files** | **No** | No multi-app blob problem yet. When you have one, it is often the next real extract (object store, not a schema in Pay) |
| **Media / Stream** | **No** | Encoder + CDN is a different machine. Only when VOD is a product |

---

## Which has *more* advantages?

**One monolith has more advantages for new Pay** (mail, audit, catalog, `/v1` in one process).

**Several services have more advantages as a *catalog* and as *sold doors*** — One already, Notify/Media only when a second consumer or a second runtime is real.

**Mega-monolith of One+Pay+Media+Notify has fewer advantages than C**, because you would drag Zitadel/FGA into the money binary and rebuild identity while trying to take a card.

**Five services on day one has the fewest advantages.** You already lived that as a modular monolith (same tax, one process) and as two C# trees that still duplicated org.

Score, for a founder team before a second product:

| | Advantages | Cons |
|--|------------|------|
| Mega-monolith (absorb One + Media) | One clone to run | Mixes IdP and money; Media starves checkout; months rewriting One |
| Several services now | Slide looks like a platform | Outbox, dual-write, start-order, paid-but-no-X; no second caller |
| **One + Pay-monolith** | Earned identity split; money is one transaction; `/v1` to sell | Staff access can lag One webhooks; cannot sell Notify/Media as SKUs yet |

The last row wins.

---

## Practical rule

1. **Bezos is the door:** `/v1` from day one; no app reads Pay tables; Pay does not read One tables.
2. **Linux is the room:** one Pay process; function calls (or the same handler) for ledger, receipt, mail, audit.
3. **One stays a sibling.** Consumer-0 over HTTP. Staging proof is still NOT PASSED; integrate the façade anyway.
4. **A new process appears when a new team or customer cannot live in that binary** — not when `docs/platforms` has another heading.

When development starts: **new Pay as one binary** that talks to **existing One**. Do not scaffold `lazuar-notify` or `lazuar-media` as services.
