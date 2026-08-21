# D25 — `subscriptions`

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-FUL-002. Buyer access is a Pay row. Not Stripe Billing SoT. Not Hub dunning.

---

## D25.1 Table

- [x] `subscriptions`: `org_id`, `payer_id` **nullable**, `status`, period fields **minimal**
- [x] Buyer access = this row (or checkout complete for one-off). Not a One membership
- [x] Nullable provider refs OK (`provider_subscription_id` etc.) — **not** source of truth

## D25.2 SoT

- [x] **Not** Stripe `subscription.updated` / Stripe subscription id as SoT
- [x] Pay’s later billing job (Bar C) mints a checkout or off-session charge
- [x] No `client_profile_id` into Hub CRM. No One / Zitadel user id on this row

## D25.3 Refuse

- [x] Not Hub dunning engine (`DunningCampaigns`, PAST_DUE JSON snapshots)
- [x] Do not invent PAST_DUE without a real failed charge
- [x] No `HasDefaultSchema("commerce")`

## D25.4 Exit

- [x] Table exists; provider refs are nullable and not SoT
- [x] Unblocked for D26
