# W4-LP-036 — ShopeePay / Boost (wrap only)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-036`. Tracker: *ShopeePay / Boost* — Lazuar **N**.  
**Not this ID:** SPayLater / Boost credit (BNPL refuse). Separate adapters per wallet. Stripe (neither method is a Stripe MY type).

**Invariant:** One ticket, two **hosted** methods. Billplz codes historically include `boost`; ShopeePay appears on HitPay/CHIP/Xendit/Billplz collections. We pass codes if documented; otherwise disclose “if enabled on your collection.” No first-party wallets.

---

## 0. Scope lock

Same wrap kit as [W4-LP-033](./W4-LP-033-analysis.md) / [034](./W4-LP-034-analysis.md): flags `SupportsShopeePay` / `SupportsBoost`, hop-1 copy, optional allow-list.

Out of scope: two checkout buttons on hop 1; wallet mandates; Shopee Open Platform.

---

## 1. Verdict

Stripe will not save this row. CHIP/Xendit/Billplz hosted pages might already show both. Product work is honesty + optional request, not SDKs.

---

## 2. Current files

No method filters. No hop-1 wallet list.

---

## 3. Exact gaps

No flags; no copy; no Xendit adapter to request ASEAN wallets in one API (LP-045).

---

## 4. Recommended model

Treat as two bits in `HostedMethods`. Recurring = reminder-only. Do not split IDs.

---

## 5. Minimal code changes

Shared capability helper + hop-1 (“ShopeePay / Boost may appear on the next page”). Adapter whitelist only with official codes.

---

## 6. Tests

Stripe → both flags false. Billplz/CHIP payload includes codes only when configured.

---

## 7. Acceptance

No Shopee/Boost SDK in the repo. Tracker **W** or **P**. Never **Y**.

---

## 8. Order

Same PR as 033–035 once method codes are confirmed.

Do **not** implement from this file.
