# 14 — How Google, AWS, and Microsoft decide split vs together

**Date:** 20 August 2026  
**Question:** How do Google, AWS, and Microsoft decide monolith vs several services?  
**Maps to us:** [13-monolith-vs-services.md](./13-monolith-vs-services.md). Door vs room: [08-bezos-door.md](./08-bezos-door.md).

They do **not** pick “monolith vs microservices” as a style. They pick **who owns a failure, who gets the bill, and whether a stranger can buy it**. Repo shape, process shape, and product shape are three different knobs. All three companies turn them independently.

Copying “Google is a monolith” or “AWS is microservices” without that distinction is how you get four empty compose files.

---

## Three knobs (or you copy the wrong thing)

| Knob | Google | AWS / Amazon | Microsoft |
|------|--------|--------------|-----------|
| **Source** | One giant repo (Piper, since ~1999) | Many repos; teams own their code | Windows/Office: huge monorepos. Azure / GitHub: product repos |
| **Process** | Thousands of Borg jobs talking Stubby/gRPC | Thousands of services; two-pizza ownership | Windows is one OS. Azure is hundreds of billed services |
| **What a customer buys** | Search, Gmail, YouTube, Cloud APIs — separate products | S3, EC2, IAM — the **split is the product** | Windows license, M365, Azure Blob, Entra — separate SKUs |

A Google engineer “calls a function” **in the repo** (shared library, atomic commit). At runtime that still becomes **another job** if Search and Ads cannot share a crash. Amazon banned **back doors between teams**, not `foo()` inside one program. Microsoft kept Windows as one tree because a kernel is one machine; they split Azure because customers buy **storage ≠ VM ≠ identity**.

---

## How Amazon / AWS decide

**Trigger was org pain, not fashion.** Early 2000s Amazon was a retailer with tangled internal calls (teams reading each other’s databases). Around **2002** Bezos mandated: teams expose **service interfaces** that could be shown to the outside world; no linking internals, no shared-memory back doors. Yegge’s later write-up is the usual source; Amazon never published the memo as a PDF.

That mandate did two things:

1. **Retail** became a service-oriented mess internally (hops, fan-out, “who owns this?”).
2. **AWS** became possible: S3 (2006) and EC2 were doors that already existed *inside*, then got a bill and an SLA.

**The decision rule they actually use:**

- A **two-pizza team** owns a thing end-to-end (build, run, page). If nobody owns it, it is not a service; it is a library or a feature.
- If another team (or a customer) must use it **without compiling your code**, it is an **API**. That is Bezos.
- If it has its own **meter, IAM policy, and failure domain**, it can become an **AWS product**. That is why S3 is not a folder in EC2.
- If it is a **tight data loop on one team**, they will **collapse it**. Prime Video’s video-quality monitor (2023) moved off Step Functions + Lambda + S3 hops back into **one process** and cut that path’s cost ~90%. Even Amazon will un-split a workflow that was distributed for no ownership reason.

**They do not** start a new company as IAM + S3 + SES + CloudWatch with zero storefront. The bookstore existed first.

For Lazuar: One is already an Amazon-shaped extract (another team’s IdP). Pay’s ledger + receipt is a Prime-Video-shaped loop — keep it one process. Notify/Media become services when a **team or a customer** owns them, not when the slide has a box.

---

## How Google decide

**They split runtime, not the tree.**

In 1999 they moved to **one repository** (later Piper). The 2016 CACM paper (*Why Google Stores Billions of Lines of Code in a Single Repository*, Potvin & Levenberg) is explicit: early engineers thought **one tree was strictly better** than splitting the codebase. They later spent a fortune on tooling (Piper, Bazel, Code Search, TAP) so tens of thousands of people could still make **atomic** changes across Search, Ads, YouTube, infrastructure.

At runtime Google is not one binary. Jobs run on **Borg**. They talk over **Stubby** (internal RPC; gRPC is the public descendant). A “service” is a **job with an SLO**. SRE’s production-readiness review asks: is this a thing we would page for, with clear boundaries, not “the whole company”?

**The decision rule:**

- **Default in source:** put it in the monorepo; prefer a **library** so the next change is one commit.
- **New binary / new Borg job** when it needs its own **release, language, scaling curve, or SLO** (Gmail’s mailstore is not Search ranking; YouTube transcode is not Ads bidding).
- **New customer product** (Cloud Storage, IAM, Gmail) gets an **external API** because the buyer is not on Piper.
- Shared platforms (Borg, Colossus, Bigtable, Chubby) are **internal monopolies** — one implementation everyone calls. That is Linux-shaped *infrastructure*, many *products*.

Google Cloud looks like AWS because **Cloud’s customer is a stranger**. Search did not start as twenty billed microservices.

For Lazuar: a Google-like move is **one Pay repo/binary** with packages, plus One as an already-external job. It is **not** “five GitHub orgs on day one.” Google would rather you share a library than invent Notify-as-a-fleet before anyone sends mail.

---

## How Microsoft decide

**They split by product P&L and by “is this one machine?”**

- **Windows** (and a wide brush of Xbox/HoloLens/server) is developed as **one enormous tree**. Brian Harry (2017): some code *is* separable (microservices, isolated repos); **Windows core is not** and must be treated as a single repo. They spent years making Git survive a ~300GB / millions-of-files enlistment. That is a **kernel + OS** decision: one address space, one ship vehicle.
- **Office / Microsoft 365** is a suite: shared code, many apps, still basically “one company product,” not a mesh of checkout services.
- **Azure** is the AWS copy, on purpose, years later. A new Azure capability becomes a **service** when it has a portal blade, an ARM resource type, a meter, an SLA, and a team that will get the Sev-1. Blob Storage is not a DLL you link into Windows. **Entra ID** is not a table inside Azure SQL.
- **Org charts move boxes** (2018 Windows split into Experiences & Devices vs Cloud + AI). That is Conway: they reorganize *people* more than they turn `ntoskrnl` into microservices.

**The decision rule:**

- Ships on a **PC / console / phone** → stay a product monolith (or OS modules, still one device).
- Sells as **cloud** → service if a customer can provision it without Microsoft compiling their app into yours.
- **Identity** (Entra) and **storage** are separate because **different compliance, different bills, different customers**.

For Lazuar: Microsoft would keep Pay as one service (like a business app). They would keep One separate if it is Entra-shaped (it is). They would not invent Azure Media Services before you have video.

---

## The shared rule (stripped of brand)

All three, in practice:

1. **One team, one loop, one transaction** → one process (Amazon collapsed Prime Video monitoring; Google uses a library; Microsoft ships Windows).
2. **Another team must not `SELECT` your tables** → an API (Bezos; Google Stubby; Azure ARM).
3. **A customer pays for a failure domain** → a product/service (S3, GCS, Blob; IAM/Entra; not “the receipt mailer”).
4. **Source layout is a tooling choice.** Google and Windows bet on monorepos at insane cost. That does not mean one HTTP server.

They **do not** decide by “platforms.md has four headings.” They decide by **ownership + blast radius + can a stranger buy this**.

None of them started as four empty platform services with zero products. Google did not start with “identity service, pay service, notify service.” Amazon started as a bookstore (monolith), then mandated APIs when they had many teams and were blocked. Microsoft started as Windows/Office monoliths; Azure came decades later copying AWS.

---

## Mapped onto Lazuar

| Name | What they would do | Closest analogue |
|------|--------------------|------------------|
| **One** | Keep as a **service**. Identity is Entra / IAM / Google identity — own SLO, own secrets, own pages | Already extracted |
| **Pay** | One **process** for checkout, ledger, receipt, refund. Public `/v1` so a second app does not join your DB | Stripe-like product, not “Billing + Ledger + Receipt” as three fleets |
| **Notify** | Library/package **inside Pay** until a second product shares a sending domain or SES-scale is a P&L | Google: library. AWS: SES only once email *was* a product |
| **Media** | Do not exist until blobs/VOD are a product. Then it is S3/Blob **because the machine is different** | GCS / S3 / Azure Blob — extracted when storage is the job |
| **Audit** | Table in the same commit as the write. “CloudTrail” only when many teams and a compliance buyer need an independent log | CloudTrail / Azure Activity Log — after there is an estate |

Google’s lesson: **one tree / one Pay binary**, not “merge Zitadel into checkout.”  
AWS’s lesson: **`/v1` as a door** and **don’t split a tight loop**.  
Microsoft’s lesson: **OS-shaped things stay together; cloud SKUs split when they bill**.

None of them would start **lazuar-one + pay + notify + media + audit** as five greenfield services with one founder team. They grew the splits **after** a storefront, **after** a second team, **after** a customer could pay for a boundary.

Development stays: **new Pay as one binary, talk to existing One, Notify/Audit inside Pay, Media later.** That is the boring subset of how all three actually decide.
