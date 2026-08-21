# G16 — Create PSP hosted session

**Track:** Rails · **Depends:** G12, D17  
**Analysis:** [06](../06-money-rails.md) §2.3 / §2.4 / §6.3  
**IDs:** NP-GW-002 XOR NP-GW-003  
**Goal:** Pay mints a hosted URL with decrypted BYOK. One live rail. No Stripe Billing.

---

## G16.1 Create (G10 rail only)

- [ ] Decrypt BYOK in process. Call the PSP. Return a hosted URL
- [ ] If Stripe: Checkout **`mode=payment`**, cards. **Not** `mode=subscription`. **Not** `mode=setup` as paid
- [ ] If CHIP: purchases API (`POST …/api/v1/purchases/`) + hosted `checkout_url`
- [ ] Amount `> 0`. No keys → 400/409 `payments_not_configured` — **not** a surprise Billplz bill

## G16.2 Steal HTTP judgment, not the module

- [ ] Steal from Hub `StripeGatewayAdapter` / `ChipCollectGatewayAdapter` **HTTP** (mode, cards, purchases)
- [ ] Do **not** copy `Modules/Payments`, MediatR, `IPaymentGatewayAdapter`, or the factory of five
- [ ] No Stripe Billing Portal. No `customer.subscription.*` as paid (G23)

## G16.3 Isolation

- [ ] IsolationTests still ban `MediatR` / `Modules.` / `BuildingBlocks` / `lazuar-api`
- [ ] No `ProjectReference` to `apps/lazuar-api`

## G16.4 Exit

- [ ] Flip **only** `NP-GW-002` (Stripe) **or** `NP-GW-003` (CHIP) — XOR from G10
- [ ] Unblocked for G17 and G24
