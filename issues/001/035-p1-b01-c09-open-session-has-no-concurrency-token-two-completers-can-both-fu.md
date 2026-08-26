---
number: "035"
id: B01-C09
severity: P1
status: resolved
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
resolved_branch: fix/035-session-complete-cas
---

# 035 — B01-C09 — OPEN session has no concurrency token; two completers can both fulfill

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/035-session-complete-cas`

`TryComplete` is a no-op unless Status is OPEN. Status is an EF concurrency token so a second completer's SaveChanges loses.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C09 — OPEN session has no concurrency token; two completers can both fulfill

**Severity:** P1  
**One-sentence fault:** `Complete()` is an unguarded string write; two concurrent `HandleOpenCheckoutSessionAsync` or mark-paid vs webhook both see OPEN and both insert a Subscription or Order.

**Evidence.** No `IsConcurrencyToken` / `RowVersion` / `xmin` in Commerce mappings (grep empty). Session load in the webhook is a plain `FirstOrDefaultAsync`. Inbox SKIP LOCKED serialises **messages**, not **sessions**. Two API instances can process two inbox rows for the same session (different EventIds) at once. Mark-paid is a synchronous admin POST against the same OPEN row.

Sequential replay is safe and tested (`GatewayPaymentCompleted_SameEventTwice_DoesNotCreateSecondSubscription`): after the first save, status is COMPLETED, second call goes to `HandleSubscriptionPaymentAsync` and no-ops.

**Reproduction in words.** Stripe delivers `checkout.session.completed` twice with two EventIds before Payments’ business key can collapse them (or mark-paid races the first webhook). Two Commerce inbox messages, two scopes, two OPEN reads, two `AddSubscription`, two `subscription.activated`, two fulfillment lists. Buyer is provisioned twice.

**Speculation (labeled):** Payments’ `BuildBusinessKey(eventType, gatewayTransactionId)` is supposed to collapse `checkout.session.completed` + `payment_intent.succeeded`. Setup-mode trials only have a SetupIntent id; a single EventId is the common case. The race is real for mark-paid vs webhook and for any dual EventId leak; it is not proven daily.

**Blast radius.** Double entitlement, double outbound webhook, double ledger if Billing also consumes both. Money/access, not chrome.

**Why tests missed it.** InMemory + sequential `HandleAsync` twice. No parallel test, no row version.

**Fix direction.** `UPDATE … SET status='COMPLETED' WHERE id=@id AND status='OPEN' RETURNING *` (or EF concurrency token). Only the winner creates the Order/Subscription. Confirm coupon in the same transaction.

---

