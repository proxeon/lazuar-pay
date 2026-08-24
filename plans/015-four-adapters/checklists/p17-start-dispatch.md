# P17 — Public start calls the active rail

**Track:** Provider door · **Depends:** P13, S13  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** NP-CHK-005  
**Goal:** `PublicPayEndpoints.Start` must not inject `StripeHosted` only.

---

## P17.1 Live today

- [x] `Start` takes `StripeHosted stripe` and always calls it

## P17.2 Change

- [x] If `checkout.Provider` already set (retry start) → use that rail (do not switch mid-flight)
- [x] Else load `org_settings.active_provider`
- [x] Null / missing creds → 503 `"rail not configured"` (keep)
- [x] Dispatch: `switch (provider)` to `StripeHosted` / `ChipHosted` / `BillplzHosted` / `XenditHosted` / `RazorpayHosted` as each class exists
- [x] Unknown active_provider → 400
- [x] ChargesPaused still 403 (existing)
- [x] paid/expired still 409 (existing)

## P17.3 Must not

- [x] Do not try Stripe then CHIP on failure
- [x] Do not call Hub factory
- [x] Do not require Bearer on start

## P17.4 Exit

- [x] Start compiles without a Stripe-only constructor dependency (or keeps Stripe as one case)
- [x] Unblocked for P18, C10
