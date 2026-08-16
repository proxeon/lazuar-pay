# W4-LP-034 — Touch ’n Go eWallet (wrap only)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-034`. Tracker: *Touch ’n Go eWallet* — Lazuar **N**.  
**Not this ID:** TnG merchant SDK. DuitNow QR (`LP-033`). GrabPay (`LP-035`). BNPL.

**Invariant:** TnG is a **hosted method** on Billplz (`touchngo`), CHIP, HitPay, or Xendit. We do not open TnG Digital APIs. Recurring TnG is **not** a card vault unless the processor says the token is reusable — default **reminder-only**.

---

## 0. Scope lock

Same wrap pattern as [W4-LP-033](./W4-LP-033-analysis.md): capability flag, optional method allow-list, hop-1 disclose.

Out of scope: in-app TnG buttons on hop 1; wallet mandate product without processor docs.

---

## 1. Verdict

Likely already visible on some Billplz collections. Cell **N** means we don’t name it. Do not add a TnG adapter.

---

## 2. Current files

Billplz/CHIP generate checkout with **no** method filter. Stripe has no TnG.

---

## 3. Exact gaps

No `SupportsTng` flag; hop 1 silent; no documented method code in adapters.

---

## 4. Recommended model

`HostedMethods.Tng`. Pass Billplz/CHIP/Xendit code **only if** official. Hop 1: “Touch ’n Go may appear on the next page if your merchant enabled it.” Recurring: do not set `setupFutureUsage` for a TnG-only intent.

---

## 5. Minimal code changes

Capabilities + hop-1 line + adapter whitelist hook (shared with 033–036). No new project.

---

## 6. Tests

Stripe product → flag false. Adapter unit: whitelist key only when set.

---

## 7. Acceptance

No first-party TnG SDK. Copy matches the active gateway. Tracker **W** or **P** (collection-default).

---

## 8. Order

Bundle with 033/035/036 after Xendit or CHIP method codes are confirmed.

Do **not** implement from this file.
