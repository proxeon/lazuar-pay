# H22 — No Stripe Connect application_fee

**Track:** Harden · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) standing law BYOK; Hub `StripeGatewayAdapterTests`  
**IDs:** NP-XX (MoR / Connect refuse)  
**Goal:** Pay is software. Money settles on the merchant’s Stripe account.

---

## H22.1 Grep

- [x] `apps/lazuar-pay/src/Lazuar.Pay/Gateways` must not contain `ApplicationFeeAmount`, `application_fee`, `TransferData`, `transfer_data`
- [x] Add Isolation-style or source grep test (Hub already has `PaymentAdapters_DoNotSetConnectApplicationFeeOrTransfer` — steal the idea, not the project)

## H22.2 StripeHosted

- [x] Live `SessionCreateOptions` already has no Connect fields — keep it that way when the file grows

## H22.3 Exit

- [x] Grep test green
