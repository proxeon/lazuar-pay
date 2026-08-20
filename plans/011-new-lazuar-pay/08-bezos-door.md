# 08 — Bezos is the door, Linux is the room

**Date:** 20 August 2026

The worry: “We are building a system others can integrate. Linux as a reference is wrong. Remember the Bezos mandate? Their codebase is ugly because they call the function directly instead of through an API.”

Those are **two different problems**. Both references are useful. Neither says “four services on day one.”

---

## What Bezos actually banned

The 2002 mandate (as Yegge later told it): teams do not read each other’s databases, do not link each other’s internals, do not share memory. Everything goes through a **service interface** that could be shown to the outside world.

The sin was **back doors** — Team Retail `SELECT` from Team Payments’ tables. That is how you cannot sell S3: nothing was a product.

He did **not** say a single team must split Pay, Notify, and Audit into processes before they have a second caller. Amazon already had many teams and many codebases. The mandate made AWS possible. It also produced the famous internal spaghetti: hops, fan-out, “who owns this?” That ugliness is **many services**, not `foo()` inside one program.

---

## What Linux is about

One address space, one linker, call the function. That is how **one team** changes one money story without an outbox. It is a bad metaphor for **strangers integrating**. A merchant cannot `import your/internal/pay`. Reject “Linux” as the *whole* story if you want a platform others use. Keep it as the story of **Pay’s own process**.

---

## The contradiction

| If you only call functions | If you only copy Bezos on week one |
|----------------------------|-------------------------------------|
| Fast, one transaction, no parked events | Externalizable in theory |
| Second app / customer cannot integrate without your repo | Four processes, four DBs, start order — Twitter clone |
| Not a sold platform | Platform-shaped waste before anyone integrates |

---

## How both are true at once

**Bezos is the door. Linux is the room.**

- Anything you will sell or let another app use is a **versioned HTTP API** (`/v1/pay`, …). No “just join the `ledger` table.” That *is* the mandate.
- Inside **one** Pay binary **you** own, Pay may call `notify.Enqueue` and `audit.Append` as functions — or, stricter and better for selling: those functions are exactly what the HTTP handlers call, and **your own UI is a client of `/v1`**, not of `internal/`. Then you have no back door, and you still have one deploy and one transaction *if* the handler runs in-process (same process serving `/v1` can still do one DB transaction in the handler).

The handler is allowed to touch several packages in one request. That is not “calling another team’s binary.” That is one program implementing one HTTP use case. Bezos objected to **Team B compiling against Team A’s internals**. He did not object to one service implementing “charge and receipt” behind one API.

**Amazon’s “ugly”** is closer to the last two years than to a function call: thousands of interfaces, every noun a fleet, integration is archaeology. AWS was the payoff **because** they already had huge internal demand. We do not have that demand yet.

**One is already the other team.** Pay must not read One’s tables. Pay calls One’s `/api/v1`. That is Bezos *between products*. Pay must not also Bezos *inside* Pay (Commerce HTTP to Billing).

---

## Practical rule

1. One Pay binary, one Pay database.
2. Public `/v1` from day one — that is Bezos.
3. No second product and no customer reads Pay tables or `internal/`.
4. The first app uses `/v1` (or the same handler).
5. Pay talks to One over HTTP — One is a different product.
6. A **new process** appears when a **new team or a customer** cannot run inside that binary — not when the slide has four names.

Linux without an API is a product nobody can integrate. Bezos without a single kernel is four services talking to yourself. You want the door *and* the room: **one Pay service, every integration through the API, no back door.** That is the mandate at this scale. It is not the Twitter clone, and it is not “only `foo()` forever.”

---

## If AWS had stayed a Linux-shaped monolith

AWS could not have stayed a Linux-shaped monolith **as a product**. Linux is one machine’s kernel. AWS is **many machines, many tenants, many bills**. The thing they sell *is* the service boundary.

Customers do not “run Amazon.” They call S3, EC2, IAM, STS, each with its own API, IAM policy, meter, and failure domain. That was not a Twitter clone. S3 (2006) was “put/get object” as a **network product**. The Bezos mandate made those doors *externalizable*. The kernel analogy would be: everyone gets one giant Amazon process and you `import s3`.

**If they had stayed one binary / one “Amazon OS”:**

- **You could not buy just disk.** S3’s whole point is: you do not run their computers. A monolith you deploy yourself is hosting, not AWS.
- **One blast radius.** A leak in “the store” could take down compute, IAM, and billing. Today S3 can hurt the internet and EC2 still boots. Linux can panic one box. AWS cannot panic the planet as one process.
- **One scaling story.** Object storage, VMs, and IAM have nothing in common at us-east-1 scale.
- **One compliance blob.** PCI, FedRAMP, and “public bucket” cannot share one release train forever.
- **No two-pizza ownership.** S3 and EC2 are different teams, different SLAs, different pages at 3 a.m. Linux maintainers still share one tree; they do not owe you 99.99% on `ext4` separately from `tcp`.
- **No marketplace of primitives.** Lambda, SQS, and RDS exist because the door was already a service.

**What would have been better about a monolith.** Fewer hops, fewer “IAM is down so nothing works” in a *different* way (everything is one deploy), cheaper internal calls, less Yegge-spaghetti. Amazon *retail* might have been happier longer as a fatter app. That is not the AWS business.

| | Linux | AWS | Lazuar |
|--|--------|-----|--------|
| Unit of sale | A kernel you run | APIs you call | Not sure yet — checkout *and* “platforms” |
| Failure domain | One machine | One service / one region | One product, one team (Pay); One already separate |
| Why many processes | Userspace, not the kernel | Isolation, billing, scale | Not yet for Notify/Audit |

Amazon’s ugliness is **too many doors between their own teams**. AWS’s success is **doors that customers pay for**. Those are the same technical pattern (HTTP APIs) at different scales.

**If AWS had been Linux:** there would be no S3 product. There would be Amazon.com, maybe a giant internal library, maybe a hosted “Amazon appliance.” Someone else would have sold object storage as an API.

**For us:** Bezos applies to **what a stranger may call** (`/v1/pay`) and to **Pay ↔ One**. Linux applies to **what Pay runs this year** (one binary). Pretending we are AWS — four fleets, four SLAs — before a second team or a paying integrator is the Twitter clone. Pretending we are Linux **forever**, with no HTTP door, means we never have an AWS-shaped product to sell.

AWS did not stay a monolith because **the product is the split**. Pay should stay a monolith until **someone else** needs a split they can buy. One already had that someone (Pay, as Consumer-0) — that is why One is a process.

How Google and Microsoft turn the same knobs (repo ≠ process ≠ SKU): [14-google-aws-microsoft.md](./14-google-aws-microsoft.md). Before-development choice: [13-monolith-vs-services.md](./13-monolith-vs-services.md).
