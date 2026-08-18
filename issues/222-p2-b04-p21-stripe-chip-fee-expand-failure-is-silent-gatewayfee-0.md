---
number: "222"
id: B04-P21
severity: P2
status: resolved
resolved_branch: fix/222-fee-expand-unknown
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 222 — B04-P21 — Stripe / CHIP fee expand failure is silent `GatewayFee=0`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/222-fee-expand-unknown`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P21 — P2 — Stripe / CHIP fee expand failure is silent `GatewayFee=0`

**Where.** Stripe `99-102`, `182-186`; CHIP missing `payment` node leaves fee 0 (`185-192`); Billplz always 0 (B04-P — 008 leftover, still true).

**What.** Ledger net = gross. Honesty, not fulfillment.

## Evaluation (current tree, 2026-08-18)

### What the bug is
When Stripe `checkout.session.completed` / `payment_intent.succeeded` is parsed, the adapter tries to `GetAsync` the PaymentIntent with `Expand = latest_charge.balance_transaction` and copy `Fee / 100` into `GatewayFee` (and FX / base currency from the balance transaction). If that expand throws, or the charge/BT is missing, `gatewayFee` stays `0` and the event is still `PAYMENT_COMPLETED`. CHIP `purchase.paid` (and now token-bearing `purchase.preauthorized`) only fills fee when a root `payment` object has `fee_amount`; a missing `payment` node leaves `GatewayFee=0` and `NetAmount=amountPaid`. Billplz **can** compute `(paid * estimatedFeePercentage / 100) + fixedFee`, but `ProcessGatewayWebhookCommandHandler` always passes `0, 0, 0` (“removed from config”), so production Billplz `GatewayFee` is always 0. Billing `GatewayPaymentCompletedHandler` books `ASSET_CASH = NetAmount` and only adds `EXPENSE_GATEWAY_FEE` when `GatewayFee > 0`. So a silent 0 fee journals **cash = gross** (minus tax if any). The buyer is fulfilled either way. The lie is the ledger / “Net Cash in Bank” / fee KPI, not capture.

### Still present?
**STILL BROKEN**

Stripe still warns and continues with fee 0:

```163:203:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
                    // Mirror checkout.session.completed: expand latest_charge.balance_transaction for real fee.
                    // If expand fails, leave GatewayFee=0 (gross-only) rather than blocking fulfillment.
                    // ...
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to expand Stripe PaymentIntent {PaymentIntentId} for fee extraction; GatewayFee=0.", pi.Id);
                        gatewayFee = 0;
                    }
```

Session path is the same swallow at `StripeGatewayAdapter.cs:109-112` (“Failed to fetch Stripe balance transaction for fee extraction.”). CHIP still defaults fee to 0 unless `payment.fee_amount` is present:

```196:203:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
            decimal gatewayFee = 0m;
            decimal netAmount = amountPaid;

            if (root.TryGetProperty("payment", out var paymentNode) && paymentNode.ValueKind == JsonValueKind.Object)
            {
                gatewayFee = paymentNode.TryGetProperty("fee_amount", out var faProp) ? faProp.GetDecimal() / 100m : 0m;
                netAmount = paymentNode.TryGetProperty("net_amount", out var naProp) ? naProp.GetDecimal() / 100m : amountPaid;
            }
```

Handler still hard-codes estimated fees to 0 (`ProcessGatewayWebhookCommandHandler.cs:74-76`), so Billplz’s formula at `BillplzGatewayAdapter.cs:226-230` is dead in production. Billing still omits the fee line when 0:

```82:87:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
            entry.AddLine(AccountTypes.AssetCash, @event.NetAmount, @event.Currency, @event.NetAmount * fxRate, baseCurrency);

            if (@event.GatewayFee > 0)
            {
                entry.AddLine(AccountTypes.ExpenseGatewayFee, @event.GatewayFee, @event.Currency, @event.GatewayFee * fxRate, baseCurrency);
            }
```

`ValidateBalanced` still passes because cash + revenue (+ tax) balance without a fee line. Issue 239 (`B05-L35`) is the Billplz-journal twin of this.

### Related files
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — expand + swallow.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` — fee only from `payment` node.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` — formula unused because caller passes 0.
- `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` — `estimatedFeePercentage/fixedFee/taxRate` always 0.
- `apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` — fee args still on the port.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` — books fee only if > 0; cash = `NetAmount`.
- `issues/239-p2-b05-l35-billplz-fee-is-always-0-in-the-journal.md` — leftover 008 Billplz fee.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs` — no expand-failure fee test.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/LedgerBalanceMatrixTests.cs` — happy-path fee line when the event carries a fee.

### Tests
- Existing tests: Stripe parse tests assert identities / setup vault / refunds, **not** `GatewayFee` after a failed expand. Handler tests stub `GatewayFee: 0`. `LedgerBalanceMatrixTests.Payment_PostsBalancedSale_AndIsIdempotent` books whatever fee the stub event has. Billplz unpaid/paid tests do not assert fee. CHIP paid tests do not require a `payment` node.
- Whether any test would fail if the bug is still there: **no**. Several fixtures **lock** `GatewayFee: 0` as the stub.
- What a first regression test should assert: Stripe `payment_intent.succeeded` whose expand throws still returns `PAYMENT_COMPLETED` (fulfillment) **and** either (a) a structured `fee_unknown` metadata flag / non-zero retry, or (b) after a chosen fail-closed fix, `Verified=false` so Stripe retries until BT exists. CHIP paid **without** `payment` should not be documented as “exact Stripe-like fee”. Billplz: if we restore estimates, handler must pass non-zero args or the adapter must stop pretending.

### Reproduction today
Arrange: Stripe test PaymentIntent with a real `balance_transaction` fee (e.g. RM 10 card). Act: (1) deliver `payment_intent.succeeded` with a tenant Stripe key that cannot retrieve the PI (wrong sk, or stub expand to throw) — published `GatewayFee` is 0, Billing `ASSET_CASH` = 10, no `EXPENSE_GATEWAY_FEE`. (2) CHIP `purchase.paid` body with `purchase.total=1000` and no `payment` node — `GatewayFee=0`. (3) Billplz paid callback — `GatewayFee=0` regardless of real Billplz cut. Assert ledger net = gross (minus tax only).

### Blast radius
Every Stripe expand miss, every CHIP payload without `payment`, **every** Billplz paid event. Ledger overstates cash / understates fees. SST liability is unaffected. Fulfillment (Commerce activate, M2M `payment.completed`) is unaffected. Ops dashboard “Net Cash in Bank” (issue 230) inherits the lie. Frequency: Billplz = always; CHIP = whenever CHIP omits `payment` (common on preauthorized `$0`); Stripe = transient API errors or unexpanded events. Not PII. Not a double-charge.

### Suggested fix
Keep fulfillment non-blocking (do not 500 a paid webhook just to learn the fee — that *would* become a P0). Smallest honesty fix: stamp metadata `gateway_fee_status=unknown` when expand fails / `payment` missing, and do not treat `NetAmount == AmountPaid` as “we know the fee is zero”. Optionally retry Stripe fee expand from a worker without republishing completed. Billplz: either delete the dead estimated-fee parameters (issue 225) or restore a real source; do not invent a percent in the adapter. Do not reverse `EXPENSE_GATEWAY_FEE` on refund here (that is 232). No TypeSpec regen. No Stripe Billing `subscription.updated`.

### Evaluation notes
Duplicates: B04-P24 / 225 (fee args unused); B05-L28 / 232 (`RefundedFee` always 0); B05-L35 / 239 (Billplz journal fee 0). Severity still **P2** (honesty, not lost capture). Not blocked. Residual after 161-200 / 072 currency fail-closed: Stripe now refuses to invent `myr` when currency is missing, but fee expand is still fail-**open**. Payments README still claims Stripe “extracts exact fees from the `balance_transaction` object” (`Modules/Payments/README.md:30`) without mentioning the swallow.


