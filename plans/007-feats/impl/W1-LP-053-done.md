# W1-LP-053 — done

Reminder-only / send-link-each-cycle is a first-class collection mode, not a Billplz apology. Wave 0 still owns mint + `IsReminderOnly` + AUTO_CHARGE skip. This ticket makes the product claim match that physics: ops sells pay-link, hop 1 discloses it, and the due-cycle email CTA is the minted hosted bill.

`{{update_payment_link}}` stays the Lazuar interstitial. `{{renewal_link}}` (and `{{checkout_url}}`) prefer `CurrentRenewalCheckoutUrl` when it was minted for the current `NextBillingDate`. New-org default campaigns use pay-this-cycle copy. Existing tenant campaign HTML is not migrated.

`reminder.due` stays unpublished. Payment Failed catalog stays vaulted-only. No new billing job, table, or invoice object.

## Files changed

### Mail path

- `Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs` — payload `checkout_url` when URL + date match; empty otherwise.
- `Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` — `RenewalLink = checkout_url ?? update-payment page`. `UpdatePaymentLink` unchanged.
- `Modules/Communications/Application/MessageTemplateHydrator.cs` — `{{checkout_url}}` aliases `renewal_link`.
- `Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs` — wiki: hosted pay-this-cycle when minted.
- `Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` — new-org −3 / 0 / +3 copy. AUTO_CHARGE +1/+5 unchanged.

### API honesty

- `packages/api-spec/modules/commerce/models/subscriber.tsp` — optional `current_renewal_checkout_url`.
- `task gen` — `Lazuar.ApiContracts.cs` / `api-types-ts`.
- `CommerceQueryService.Subscribers.cs` — maps stored URL.

### Ops / portal / legal / docs

- Product create/edit: **Collection mode: pay link each cycle** vs **Auto-debit: card is saved for renewals.**
- Products list + detail: Reminder-only / Auto-renew badge for recurring.
- Subscribers: Copy pay link when a live URL exists (PAST_DUE/SUSPENDED reminder-only).
- Create subscriber: hosted link each cycle, no auto-debit.
- Dunning step editor placeholder lists `{{renewal_link}}`.
- Payment settings (ops + admin): **Pay-link renewals** (already on HEAD; left as-is).
- Checkout hop 1: `then MYR X / month|year`; Billplz **Not auto-debit**; Stripe/CHIP card-saved line.
- Legal refund §4: cancel stops future renewals (auto-debit or further pay-link emails).
- Docs `product-lines.md` names the two Commerce renewal modes.
- README honest-capability: Billplz renewals = emailed hosted link.

### Tests

- `DunningEngineJobTests` — payload includes / omits `checkout_url` by mint date.
- `BillingEngineJobTests` — no-vault mint then day-0 `reminder.dunning` carries the hosted URL.
- `DunningTemplateVariableSubstitutionTests` — hosted `renewal_link`; `update_payment_link` stays the page; fallback alias preserved.
- `DunningCampaignCommandHandlerTests` — new-org day 0 is pay-this-cycle; −3 has no `{{update_payment_link}}`.
- `CommerceHonestyDtoTests` — subscriber maps `current_renewal_checkout_url`.
- `TemplateVariablesWikiTests` — wiki no longer “same as update-payment only”.
- `MessageTemplateHydratorTests` — new seed copy + `{{checkout_url}}` preview alias.

## Tests run

- `Lazuar.ModuleTests` filter `DunningTemplateVariableSubstitutionTests|DunningCampaignCommandHandlerTests|DunningEngineJobTests|BillingEngineJobTests|CommerceHonestyDtoTests|TemplateVariablesWikiTests|MessageTemplateHydratorTests` — **98 passed**.
- `npx tsc --noEmit -p apps/lazuar-ops/tsconfig.json` and `apps/lazuar-admin/tsconfig.json` — clean.
- Portal tsc still reports a pre-existing `CommunityPortalView.tsx` `at_period_end` error (not this ticket).

Not committed. Not pushed.

Tracker `LP-053` can be marked **Y** when a reviewer agrees the due email contains the hosted bill and ops/checkout treat pay-link as the collection mode.
