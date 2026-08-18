---
number: "239"
id: B05-L35
severity: P2
status: resolved
resolved_branch: fix/239-billplz-fee-honesty
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 239 — B05-L35 — Billplz fee is always 0 in the journal

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/239-billplz-fee-honesty`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L35 — P2 — Billplz fee is always 0 in the journal

Adapter formula uses `estimatedFeePercentage` / `fixedFee`. Webhook handler always passes 0, 0, 0 (`ProcessGatewayWebhookCommandHandler:74-76`). Cash = full paid. Payout CSV will not match. Same class of honesty hole as B05-L28.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Billplz’s webhook parser still computes `gatewayFee = (paidAmountMyr * (estimatedFeePercentage / 100)) + fixedFee`. The only caller, `ProcessGatewayWebhookCommandHandler`, always passes `0, 0, 0` and the comments now say those knobs were “removed from config”. So every Billplz `PAYMENT_COMPLETED` has `GatewayFee = 0`, `NetAmount = paidAmountMyr`. `GatewayPaymentCompletedHandler` only posts `EXPENSE_GATEWAY_FEE` when `GatewayFee > 0`, so cash is booked as the full paid amount. Billplz’s payout CSV / net settlement will not match the ledger. Same honesty class as **232** (`RefundedFee` always 0): the formula exists, the wire never supplies the inputs.

### Still present?
**STILL BROKEN**

```74:76:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
            0, // estimatedFeePercentage - removed from config
            0, // fixedFee - removed from config
            0); // taxRate - removed from config
```

```226:230:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
            decimal gatewayFee = (paidAmountMyr * (estimatedFeePercentage / 100m)) + fixedFee;
            if (gatewayFee < 0) gatewayFee = 0;
            
            decimal taxAmount = 0; 
            decimal netAmount = paidAmountMyr - gatewayFee;
```

Sale journal only books a fee when the event has one (`GatewayPaymentCompletedHandler.cs:84-87`). Completed event copies `parsedResult.GatewayFee` (`ProcessGatewayWebhookCommandHandler.cs:263`). Billplz tests call `ParseWebhookAsync` with default fee args (0) and never assert a non-zero `GatewayFee`. Webhook handler tests stub `ParseWebhookAsync(..., Arg.Any<decimal>(), ...)` and do not lock the 0,0,0 literals.

### Related files
- [`apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs`](apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs) — always 0,0,0.
- [`apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs`](apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs) — fee formula still live.
- [`apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs`](apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs) — `estimatedFeePercentage` / `fixedFee` still on the port.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs) — fee line only if `GatewayFee > 0`.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzGatewayAdapterTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzGatewayAdapterTests.cs) — signature / metadata; not fee.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ProcessGatewayWebhookCommandHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ProcessGatewayWebhookCommandHandlerTests.cs) — does not assert the 0,0,0 pass-through.

### Tests
- Existing: `BillplzGatewayAdapterTests.ParseWebhook_*`; `ProcessGatewayWebhookCommandHandlerTests` (many); `LedgerBalanceMatrixTests` books a Stripe-like fee of 3 via the event, not via Billplz parse.
- No test fails while Billplz fee is 0. A test that called `ParseWebhookAsync(..., estimatedFeePercentage: 1.5m, fixedFee: 1m)` and expected a non-zero fee would pass today — the adapter math works; the handler never uses it.
- First regression: either (a) lock the policy: Billplz `GatewayFee` is always 0 and README says so, or (b) if we restore config, `ProcessGatewayWebhookCommandHandler` must pass the tenant’s Billplz % / fixed fee and a module test must assert `GatewayPaymentCompleted.GatewayFee` matches the formula for a 100.00 paid bill.

### Reproduction today
Arrange a Billplz paid callback `paid_amount=10000` (RM 100). Act: hit the tenant webhook. Assert: `GatewayPaymentCompleted.GatewayFee == 0`, `NetAmount == 100`; ledger `ASSET_CASH = 100`, no `EXPENSE_GATEWAY_FEE`. Compare to Billplz’s settlement (typically ~RM 1 + %).

### Blast radius
Billplz merchants looking at net revenue / “exact gateway fees”. Cash on the ledger is overstated vs payout. Not PII. Frequency: every Billplz capture. Stripe can extract a real fee from Balance Transaction (`StripeGatewayAdapter` expand path) — Billplz cannot without config or a later payout file. Same honesty hole as **232**.

### Suggested fix
Do not resurrect a global “estimated fee %” on webhook config unless product owns it. Smallest honest change: document that Billplz journals are gross-only (README golden-rule sentence already claims “exact Gateway Fees (Stripe/Billplz)” — that sentence is the lie to fix). If product requires Billplz MDR: store per-tenant % / fixed on `TenantPaymentConfiguration` and pass those into `ParseWebhookAsync` instead of 0,0,0. Do not scrape a homemade fee from the payout CSV in this issue. No TypeSpec. No Stripe Billing.

### Evaluation notes
Comments now say the knobs were “removed from config” — more honest than 009, same result. Still P2. Sibling **232**. Not blocked. 161–200 did not restore fee config. Distinct from **075** (skip $0 GMV).

