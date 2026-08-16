# W4-LP-033 — DuitNow QR (wrap only)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-033`. Tracker: *DuitNow QR* — Lazuar **N**. Alias reserved `LP-PAY-013`.  
**Not this ID:** CHIP mini in-person. PayNet QR generation. MAE as a named rail. Apple Pay (`LP-037`).

**Invariant:** DuitNow QR appears because **CHIP, Billplz, Xendit, or HitPay-class hosted checkout already shows it**. Lazuar does not render a QR. If the active gateway cannot request or default that method, **do not add a button**.

---

## 0. Scope lock

In scope:

- Adapter method allow-list **only if** the processor documents a code (`duitnow_qr` / CHIP payment method / Billplz collection method)  
- Hop 1 one-liner when the gateway advertises QR  
- Capability `SupportsDuitNowQr(gateway)`

Out of scope:

- `IPaymentGatewayAdapter.GenerateQrAsync`  
- Polling DuitNow status ourselves  
- Making QR a subscription vault (A2A has **no** card-like token)

**Depends on:** existing CHIP/Billplz hosted defaults, or [W4-LP-045](./W4-LP-045-analysis.md).

---

## 1. Verdict

Today we send no `payment_methods` filter. A CHIP/Billplz collection that includes DuitNow QR already shows it on hop 2. Tracker **N** is the **product** (we don’t name or request it). First-class = request + disclose, not a new adapter.

---

## 2. Current files

| Path | Role |
|------|------|
| `BillplzGatewayAdapter` | No method filter |
| `ChipCollectGatewayAdapter` | No method whitelist on purchase |
| Stripe | No DuitNow (N) |
| Hop 1 | No rail list |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No allow-list field on generate checkout |
| G2 | Hop 1 silent |
| G3 | No capability helper |

---

## 4. Recommended model

Shared with LP-034–036:

```
PaymentGatewayCapabilities.HostedMethods(gateway) → flags
GenerateCheckoutAsync: optional metadata payment_method_whitelist
  CHIP/Xendit: pass if API supports
  Billplz: usually collection-default only — document, don’t fake
Hop 1: if SupportsDuitNowQr: "You can scan DuitNow QR on the next page."
```

One-time products only for QR as a **featured** rail. Recurring + QR = reminder-only (no mandate). Do not combine with e-mandate copy.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| Capabilities | `SupportsDuitNowQr` |
| CHIP (and Xendit if present) | Pass method if documented |
| `OrderSummaryCard` | One line when flag true |
| Docs | “QR is the processor’s page” |

Must not: QRCoder for DuitNow (QRCoder is for LHDN invoice QR).

---

## 6. Tests

| Case | Expect |
|------|--------|
| Stripe product | Flag false; no hop-1 QR claim |
| CHIP + whitelist support | Payload contains documented key (unit) |
| Billplz | Flag **P**: “if your collection has QR” copy only |

---

## 7. Acceptance

1. No Lazuar-generated DuitNow QR.  
2. When CHIP/Xendit can request it, hop 2 shows QR without us drawing pixels.  
3. Subscriptions do not claim silent QR debit.

Tracker **N → W** if requested; **P** if we only disclose collection defaults.

---

## 8. Order

After confirming CHIP/Xendit method codes. Same PR pattern as 034–036.

Do **not** implement from this file.
