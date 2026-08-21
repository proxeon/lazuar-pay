# G16 — Create PSP hosted session

**Track:** Rails · **Depends:** G12, D17  
**Analysis:** [06](../06-money-rails.md) §2.3 / §2.4 / §6.3  
**IDs:** NP-GW-002 XOR NP-GW-003  
**Goal:** Pay mints a hosted URL with decrypted BYOK. One live rail. No Stripe Billing.

---

## G16.1 Create (G10 rail only)

- [x] Decrypt BYOK in process. Call the PSP. Return a hosted URL
- [x] If Stripe: Checkout **`mode=payment`**, cards. **Not** `mode=subscription`. **Not** `mode=setup` as paid
- [x] If CHIP: purchases API (`POST …/api/v1/purchases/`) + hosted `checkout_url`
- [x] Amount `> 0`. No keys → 400/409 `payments_not_configured` — **not** a surprise Billplz bill

## G16.2 Steal HTTP judgment, not the module

- [x] Steal from Hub `StripeGatewayAdapter` / `ChipCollectGatewayAdapter` **HTTP** (mode, cards, purchases)
- [x] Do **not** copy `Modules/Payments`, MediatR, `IPaymentGatewayAdapter`, or the factory of five
- [x] No Stripe Billing Portal. No `customer.subscription.*` as paid (G23)

## G16.3 Isolation

- [x] IsolationTests still ban `MediatR` / `Modules.` / `BuildingBlocks` / `lazuar-api`
- [x] No `ProjectReference` to `apps/lazuar-api`

## G16.4 Exit

- [x] Flip **only** `NP-GW-002` (Stripe) **or** `NP-GW-003` (CHIP) — XOR from G10
- [x] Unblocked for G17 and G24
