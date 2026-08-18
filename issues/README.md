# Issues

One file per bug from `plans/009-bugs` (HEAD `297ba98`, 17 August 2026).
Numbered **P0 → P1 → P2**, then original audit id.
Resolve them one at a time. Status lives in each file’s front matter.

**Total:** 334  ·  P0 27  ·  P1 153  ·  P2 154

## Resolved

| # | ID | Branch | What shipped |
|---|-----|--------|--------------|
| 001 | B01-C01 | `fix/001-trial-hop2-activate` | Vaulting trials stamp `commerce_subscription`; leftover `type=trial` still activates |
| 002 | B02-C01 | `fix/002-billing-batch-starve` | `processedIds` so a vaulted due cannot eat the 50-slot batch |
| 025 | B10-X01 | `fix/002-billing-batch-starve` | Same starve as 002 |
| 003 | B03-C01 | `fix/003-update-payment-decline-not-pastdue` | `update_payment` decline stays ACTIVE |
| 004 | B03-C02 | `fix/004-pastdue-renewal-checkout-cache` | PAST_DUE pay link cached; second complete does not roll dates |
| 005 | B04-P01 | `fix/005-chip-preauthorized-vault` | CHIP `$0` `purchase.preauthorized` + token → `PAYMENT_COMPLETED` |
| 006 | B04-P02 | `fix/006-m2m-fail-then-pay` | Failed M2M session can still complete when the buyer later pays |
| 007 | B05-L01 | `fix/007-lhdn-cancel-skip-if-refunded` | ≤72h IRBM cancel after refund does not double-reverse cash/tax |
| 008 | B05-L03 | `fix/008-zero-amount-trial-ledger` | Non-vault trial $0 checkout books a balanced discount journal |
| 009 | B05-L04 | `fix/009-chargeback-claw-idempotent` | Utility chargeback claw runs once per gateway tx |
| 010 | B05-L05 | `fix/010-renewal-sst-tax-payable` | Renewal SST books `LIABILITY_TAX_PAYABLE`, not all revenue |
| 011 | B06-D01 | `fix/011-quote-b2b-crm-arity` | Quote B2B resolve uses named CRM args; company name is not `IdValue` |
| 012 | B06-D02 | `fix/012-inv-not-tax-invoice-until-valid` | B2B pay PDF is `Invoice` until MyInvois VALID |
| 013 | B06-D03 | `fix/013-document-pdf-keep-buyer-tin` | Document PDF billed-to uses CRM TIN/company, not log-only name |
| 014 | B06-D04 | `fix/014-quoteview-id-pair-validate-tin` | Quote B2B collects ID pair and validates TIN |
| 015 | B06-D05 | `fix/015-crm-overwrite-poisoned-idvalue` | Later checkout can correct a poisoned CRM `IdValue` |
| 016 | B06-D09 | `fix/016-type01-tax-percent-scale` | Type 01 UBL Percent is 8, not 0.08 |
| 017 | B06-D19 | `fix/017-credit-note-tax-totals` | Type 02 CN does not add tax on top of a gross refund |
| 018 | B07-I01 | `fix/018-invite-mail-platform-resend` | Invite mail uses platform Resend, not tenant BYOK |
| 019 | B08-M01 | `fix/019-resend-svix-whsec-hmac` | Resend webhook verifies Svix `whsec_` HMAC correctly |
| 020 | B08-M02 | `fix/011-quote-b2b-crm-arity` | Same quote CRM arity as 011 |
| 021 | B09-U01 | `fix/021-checkout-success-portal-token` | COMPLETED checkout status mints a portal token |
| 022 | B09-U02 | `fix/022-portal-no-token-shows-form` | Tokenless portal shows the magic-link form, not 404 |
| 023 | B09-U03 | `fix/023-portal-token-hrefs` | Portal/update-payment hrefs never use `token=undefined` |
| 024 | B09-U04 | `fix/024-viewer-cannot-change-plan` | Viewer cannot change plan, seats, pause, or export |
| 026 | B10-X02 | `fix/026-b2c-already-consolidated-ignore-filters` | B2C alreadyConsolidated sees rows without ambient tenant |
| 027 | B10-X03 | `fix/027-lhdn-webhooks-dual-write` | POST /lhdn/webhooks dual-writes the live workspace dispatcher |
| 028 | B01-C02 | `fix/028-coupon-lock-transaction` | Coupon `FOR UPDATE` + reserve + session insert share one transaction |
| 029 | B01-C03 | `fix/029-zero-amount-offline-chosen-price` | $0 / mark-paid discount the chosen price row |
| 030 | B01-C04 | `fix/030-idempotency-replay-open-only` | Idempotency replay only OPEN live URLs; resume empty-URL rows |
| 031 | B01-C05 | `fix/031-quote-reuse-hop2-idem-key` | Quote hop-2 is reused; portal key is per session |
| 032 | B01-C06 | `fix/032-hop1-show-sst` | Hop-1 shows exclusive SST line + gross from product SST fields |
| 033 | B01-C07 | `fix/033-validate-coupon-chosen-price` | Validate-coupon discounts the chosen price × qty; hop-1 drops the catalog ratio |
| 034 | B01-C08 | `fix/034-quote-offline-sst` | Custom quotes and mark-paid book exclusive SST when the merchant has an SST id |
| 035 | B01-C09 | `fix/035-session-complete-cas` | Only one completer wins: TryComplete + Status concurrency token |
| 036 | B01-C10 | `fix/036-expire-vs-paid-revive` | Paid webhook revives an EXPIRED session and still fulfills |
| 037 | B02-C02 | `fix/037-pending-product-after-load` | Missing pending product no longer commits a ghost ProductId |
| 038 | B02-C03 | `fix/038-pending-plan-billing-interval` | Pending plan snapshot / preview use BillingInterval, not catalog default |
| 039 | B02-C04 | `fix/039-no-refresh-snapshot-on-renewal` | Renewal webhook does not RefreshSnapshot / unfreeze UnitAmount |
| 040 | B02-C05 | `fix/040-record-payment-billing-interval` | Record-payment advances with BillingInterval, not catalog Interval |
| 041 | B02-C06 | `fix/041-stats-mrr-billing-interval` | Stats MRR uses BillingInterval so yearly seats are /12 |
| 042 | B02-C07 | `fix/042-arpu-exclude-pastdue` | ARPU denominator is MRR contributors, not PAST_DUE |
| 043 | B02-C08 | `fix/043-pause-expiry-skip-back-invoice` | Pause expiry skips the back invoice like manual resume |
| 044 | B02-C09 | `fix/044-skip-open-dispute-billing` | Open disputes are not billed; close clears the flag |
| 045 | B02-C10 | `fix/045-zero-unit-snapshot` | Written UnitAmount 0 is 0; missing snapshot still uses catalog |
| 046 | B02-C11 | `fix/046-orgread-subscriber-writes` | Change-plan / seats / pause / resume already require OrgMember |
| 047 | B02-C12 | `fix/047-trial-convert-stall-pastdue` | Trial convert stall after attempt 1 becomes PAST_DUE |
| 048 | B03-C03 | `fix/048-unique-dunning-dayoffset` | Campaign save rejects two steps on the same DayOffset |
| 049 | B03-C04 | `fix/049-reminder-log-after-publish` | Missing CRM email does not consume the dunning reminder slot |
| 050 | B03-C05 | `fix/050-update-payment-myr-minimum` | ACTIVE update-payment is RM 2, not RM 1 |
| 051 | B03-C06 | `fix/051-arrears-row-reminder-only` | Arrears reminder-only comes from the subscription row |
| 052 | B03-C07 | `fix/052-dunning-pause-predunning` | Dunning pause also stops pre-dunning “renews soon” mail |
| 053 | B03-C08 | `fix/053-zero-unit-gross` | Arrears Gross treats a written UnitAmount 0 as zero |
| 054 | B03-C09 | `fix/054-success-url-magic-token` | Success and dashboard links keep the portal HMAC |
| 055 | B03-C10 | `fix/055-magic-link-throttle` | Magic-link is throttled 5 / 10 minutes per IP |
| 056 | B03-C11 | `fix/056-magic-token-constant-time` | Portal HMAC is constant-time; missing Jwt:Secret fails closed |
| 057 | B03-C12 | `fix/057-dunning-start-lock` | Fail handler / billing start-run lock the subscription before dunning |
| 058 | B03-C13 | `fix/058-grace-zero-next-tick` | Grace 0 emails first; cancel is the next tick |
| 059 | B03-C14 | `fix/059-autocharge-skip-open-dispute` | AUTO_CHARGE skips seats with an open dispute |
| 060 | B04-P03 | `fix/060-chip-recurring-token` | CHIP off-session does not GET /purchases/{recurring_token} as a purchase |
| 061 | B04-P04 | `fix/061-chip-offsession-idempotency` | CHIP off-session sends Idempotency-Key + reference; retries reuse the purchase |
| 062 | B04-P05 | `fix/062-chip-xendit-paying-tenant` | CHIP/Xendit generate keep paying tenant_id; system org is platform_tenant_id |
| 063 | B04-P06 | `fix/063-webhook-tenant-eventid` | Inbound tenant_id must match URL tenant (platform checkout: paying tenant_id + platform_tenant_id == system URL); EventId unique is per tenant |
| 064 | B04-P07 | `fix/064-offsession-pending-not-success` | Stripe processing / CHIP pending_charge are not off-session success |
| 065 | B04-P08 | `fix/065-ignore-late-payment-failed` | Late PAYMENT_FAILED after COMPLETED on the same object is ignored |
| 066 | B04-P09 | `fix/066-razorpay-eventid-fallback` | Razorpay EventId fallback is namespaced, not the bare payment id |
| 067 | B04-P10 | `fix/067-offsession-fail-txid` | Off-session fail transaction id is per charge attempt, not per seat |
| 068 | B04-P11 | `fix/068-razorpay-no-card-registration` | Razorpay SetupFutureUsage mints a payment link, not a card-registration mandate |
| 069 | B04-P12 | `fix/069-razorpay-invoice-expired` | Razorpay invoice.expired is ignored, not PAYMENT_FAILED |
| 070 | B04-P13 | `fix/070-refund-pending-idempotency` | Stripe pending is not refund success; CHIP/Xendit/Razorpay send refund idempotency keys |
| 071 | B04-P14 | `fix/071-xendit-refund-payment-id` | Xendit refund uses payment_id from the invoice when present |
| 072 | B04-P15 | `fix/072-currency-normalize` | Webhook currency is fail-closed and published uppercase |
| 073 | B04-P16 | `fix/073-xendit-callback-constant-time` | Xendit callback token compare is constant-time including length mismatch |
| 074 | B04-P17 | `fix/074-minor-units-policy` | One ToMinorUnits policy; zero-decimal currencies are not ×100 |
| 075 | B05-L02 | `fix/075-skip-zero-gmv-setup` | $0 Stripe setup / coupon vault is not booked as GMV |
| 076 | B05-L06 | `fix/076-b2b-tax-resolved-sst` | B2B MyInvois tax is resolved SST, not raw event.TaxAmount |
| 077 | B05-L07 | `fix/077-refund-not-b2c-consolidation` | GATEWAY_REFUND is not required for B2C consolidation |
| 078 | B05-L08 | `fix/078-already-consolidated-ignore-filters` | alreadyConsolidated already IgnoreQueryFilters; leftover PENDING does not re-issue |
| 079 | B05-L09 | `fix/079-sequence-in-ledger-transaction` | Sequence increment shares the ledger SaveChanges transaction |
| 080 | B05-L10 | `fix/080-ledger-unique-tenant` | Unique ledger key is per tenant, not global (ReferenceType, ReferenceId) |
| 081 | B05-L11 | `fix/081-hasentry-tenant` | HasEntryBeenProcessed is per tenant, matching the unique ledger key |
| 082 | B05-L12 | `fix/082-refund-fx` | Refund journals copy sale FX into BaseCurrencyAmount |
| 083 | B05-L13 | `fix/083-refund-tax-remainder` | Last partial-refund slice takes remaining tax |
| 084 | B05-L14 | `fix/084-refund-cap-original` | Second Completed cannot book refunds past the original sale |
| 085 | B05-L15 | `fix/085-inbound-refund-webhooks` | Succeeded inbound Stripe refunds publish Completed; pending is not success |
| 086 | B05-L16 | `fix/086-lost-chargeback-journal` | Lost GMV chargeback books GATEWAY_DISPUTE unless already refunded |
| 087 | B05-L17 | `fix/087-dispute-status-honesty` | Fully refunded logs stay REFUNDED if a dispute arrives later |
| 088 | B05-L18 | `fix/088-clawback-granted-credits` | Utility claw uses credits granted on the original top-up |
| 089 | B05-L19 | `fix/089-underpack-unmatched-cash` | Under-pack utility payment books unmatched cash, not a silent no-op |
| 090 | B05-L20 | `fix/090-saas-zero-not-free` | AmountMyr 0 is unpaid Hub, not “free today” |
| 091 | B05-L21 | `fix/091-lhdn-deduct-fail-closed` | Live LHDN deduct runs before persist so a 402 is not a free submit |
| 092 | B05-L22 | `fix/092-pdf-retry-after-processed` | Payment retry still generates the PDF after the ledger row exists |
| 093 | B05-L23 | `fix/093-cn-including-tax` | Type 02 CN treats RefundedAmount as gross, not net+tax |
| 094 | B06-D06 | `fix/094-tax-invoice-badge-honesty` | Cons VALID does not badge RCPT rows; ops empty state is sales documents |
| 095 | B06-D08 | `fix/095-offline-product-b2b` | Offline product mark-paid keeps RequiresTaxId as IsB2bRequired |
| 096 | B06-D10 | `fix/096-b2b-event-resolved-sst` | B2B MyInvois TaxAmount is resolved SST (shipped with 076) |
| 097 | B06-D11 | `fix/097-b2b-line-description` | Type 01 line uses product name, not synthetic B2B sale |
| 098 | B06-D12 | `fix/098-tin-200-not-valid` | TIN 200 with empty or garbage body is not valid |
| 099 | B06-D13 | `fix/099-stub-tin-lists` | Stub TIN lists are the same (C / IG / EI) |
| 100 | B06-D14 | `fix/100-poll-uuid-on-valid` | Poller writes UUID onto TaxDocument when submit missed it |
| 101 | B06-D15 | `fix/101-qr-host-preprod` | Share QR host follows tenant Environment; ops QR is same-origin |
| 102 | B06-D16 | `fix/102-environment-cosmetic-country-my` | PROD Environment hits api.myinvois; checkout country default is MYS |
| 103 | B06-D17 | `fix/103-deduct-after-persist` | Live deduct already runs before persist (shipped with 091) |
| 104 | B06-D18 | `fix/104-accepted-for-processing` | Integrator submit returns pending; B2B pay proceeds without MyInvois |
| 105 | B06-D20 | `fix/105-partial-refunds-skip-lhdn` | Partial refunds file type 02; only a full ≤72h refund cancels |
| 106 | B06-D21 | `fix/106-credit-note-pdf` | Refund books a Credit Note PDF; LHDN reuses Billing CN- |
| 107 | B06-D22 | `fix/107-original-doc-cancel-double` | Original doc lookup prefers UUID; cancel+refund is one contra |
| 108 | B06-D24 | `fix/108-cons-banner-valid` | VALID keeps B2C-CONS- on TaxInvoiceId; cons submit key is stable |
| 109 | B06-D25 | `fix/109-taxdocument-internal-unique` | TaxDocument (org, InternalReferenceId) is unique |
| 110 | B06-D26 | `fix/110-placeholder-invalid` | Missing state is 17; no dummy phone; period is One-time unless cons |
| 111 | B06-D29 | `fix/111-tax-invoice-email-fallback` | Tax Invoice / CN have their own templates; no receipt fallback |
| 112 | B07-I02 | `fix/112-reset-verify-404` | Reset/verify links go to ops pages; verify works logged out |
| 113 | B07-I03 | `fix/113-double-accept-500` | Already-member accept is 400, not a unique-index 500 |
| 114 | B07-I04 | `fix/114-pending-invite-unique` | One pending invite per org+email; second invite is 400 |
| 115 | B07-I05 | `fix/115-accept-audit` | Accept pre-checks membership and writes member.accepted |
| 116 | B07-I06 | `fix/116-logout-cookie-domain` | Logout/stamp delete uses the same Domain/Path as set |
| 117 | B07-I07 | `fix/117-security-stamp-middleware` | JWT stamp is checked on every cookie request, not only /auth/me |
| 118 | B07-I10 | `fix/118-last-admin` | Last admin cannot be removed; Team hides that action |
| 119 | B07-I11 | `fix/119-archive-revoke` | Archive revokes keys, drops members, unpublishes products |
| 120 | B07-I12 | `fix/120-superadmin-403` | System admin switcher injects ADMIN so /admin/* is not 403 |
| 121 | B07-I13 | `fix/121-login-rate-limit` | Login/forgot/resend throttled 5/10 min; failed login is HTTP 401; empty limiter keys deny |
| 122 | B07-I19 | `fix/122-exception-message-500` | 500 ProblemDetails no longer echo exception.Message |
| 123 | B07-I20 | `fix/123-provision-membership-superadmin` | Provision JWT requires is_system_admin; membership SUPER_ADMIN is 403 |
| 124 | B07-I25 | `fix/124-cors-fail-closed` | Empty App:CorsOrigins fails boot in Production/Staging |
| 125 | B07-I26 | `fix/125-apikey-cache-revoke` | Revoke evicts the API-key cache; middleware re-checks IsActive |
| 126 | B08-M03 | `fix/126-crm-email-merge` | Resolve matches email+phone; LHDN prefers checkout snapshot over CRM |
| 127 | B08-M04 | `fix/127-suppression-upgrade` | Bounce/complaint upgrades an unsubscribe row (no transactional-lane hole) |
| 128 | B08-M05 | `fix/128-email-config-decrypt` | HasValidEmailConfig requires a decryptable (or legacy re_) sender key |
| 129 | B08-M06 | `fix/129-email-html-encode` | Email HTML encodes buyer names; markdown disables raw HTML |
| 130 | B08-M07 | `fix/130-anonymize-honest` | Delivery logs scrub inbox; filed PDFs/MyInvois stay; ops copy is honest |
| 131 | B08-M08 | `fix/131-digital-delivery-https` | Digital delivery mail only when the product has an https fulfillment URL |
| 132 | B08-M09 | `fix/132-unsubscribe-empty-jwt` | Empty Jwt:Secret fails closed on unsubscribe (503); broadcasts skip the URL |
| 133 | B08-M10 | `fix/133-cancel-mail-gross` | Cancel/lifecycle mail {{amount}} is Gross this cycle, same as dunning |
| 134 | B09-U05 | `fix/134-mobile-nav-hamburger` | Ops/admin mobile header has a hamburger; resize no longer force-closes |

| # | Sev | ID | Title | File |
|---|-----|----|-------|------|
| 001 | P0 | `B01-C01` | `type=trial` hop-2 is dropped; Stripe/CHIP trials never activate | [001-p0-b01-c01-type-trial-hop-2-is-dropped-stripe-chip-trials-never-activate.md](./001-p0-b01-c01-type-trial-hop-2-is-dropped-stripe-chip-trials-never-activate.md) |
| 002 | P0 | `B02-C01` | Vaulted due row starves the 50-slot batch (failedIds / processedIds hole) | [002-p0-b02-c01-vaulted-due-row-starves-the-50-slot-batch-failedids-processedids.md](./002-p0-b02-c01-vaulted-due-row-starves-the-50-slot-batch-failedids-processedids.md) |
| 003 | P0 | `B03-C01` | RM 1 / hosted-checkout decline marks a healthy subscription PAST_DUE | [003-p0-b03-c01-rm-1-hosted-checkout-decline-marks-a-healthy-subscription-pastdu.md](./003-p0-b03-c01-rm-1-hosted-checkout-decline-marks-a-healthy-subscription-pastdu.md) |
| 004 | P0 | `B03-C02` | PAST_DUE update-payment mint is not cached; two completions double-capture and skip a cycle | [004-p0-b03-c02-pastdue-update-payment-mint-is-not-cached-two-completions-double.md](./004-p0-b03-c02-pastdue-update-payment-mint-is-not-cached-two-completions-double.md) |
| 005 | P0 | `B04-P01` | CHIP `$0` + `skip_capture` never fulfills and never vaults | [005-p0-b04-p01-chip-zero-amount-skipcapture-never-fulfills-and-never-vaults.md](./005-p0-b04-p01-chip-zero-amount-skipcapture-never-fulfills-and-never-vaults.md) |
| 006 | P0 | `B04-P02` | M2M fail-then-pay: session stays `failed`, outbound `payment.completed` never sent | [006-p0-b04-p02-m2m-fail-then-pay-session-stays-failed-outbound-payment-complete.md](./006-p0-b04-p02-m2m-fail-then-pay-session-stays-failed-outbound-payment-complete.md) |
| 007 | P0 | `B05-L01` | Full B2B refund ≤72h double-reverses cash and tax | [007-p0-b05-l01-full-b2b-refund-72h-double-reverses-cash-and-tax.md](./007-p0-b05-l01-full-b2b-refund-72h-double-reverses-cash-and-tax.md) |
| 008 | P0 | `B05-L03` | `ZeroAmountCheckoutHandler` unbalanced on non-vault trials | [008-p0-b05-l03-zeroamountcheckouthandler-unbalanced-on-non-vault-trials.md](./008-p0-b05-l03-zeroamountcheckouthandler-unbalanced-on-non-vault-trials.md) |
| 009 | P0 | `B05-L04` | Utility chargeback claw is not idempotent | [009-p0-b05-l04-utility-chargeback-claw-is-not-idempotent.md](./009-p0-b05-l04-utility-chargeback-claw-is-not-idempotent.md) |
| 010 | P0 | `B05-L05` | Commerce SST on renewals never hits `LIABILITY_TAX_PAYABLE` | [010-p0-b05-l05-commerce-sst-on-renewals-never-hits-liabilitytaxpayable.md](./010-p0-b05-l05-commerce-sst-on-renewals-never-hits-liabilitytaxpayable.md) |
| 011 | P0 | `B06-D01` | Quote B2B CRM arity: company name written into `IdValue` | [011-p0-b06-d01-quote-b2b-crm-arity-company-name-written-into-idvalue.md](./011-p0-b06-d01-quote-b2b-crm-arity-company-name-written-into-idvalue.md) |
| 012 | P0 | `B06-D02` | `INV-` PDF titled “Tax Invoice” on pay, before VALID | [012-p0-b06-d02-inv-pdf-titled-tax-invoice-on-pay-before-valid.md](./012-p0-b06-d02-inv-pdf-titled-tax-invoice-on-pay-before-valid.md) |
| 013 | P0 | `B06-D03` | Transaction-log short-circuit strips buyer TIN / company / address from the PDF | [013-p0-b06-d03-transaction-log-short-circuit-strips-buyer-tin-company-address-f.md](./013-p0-b06-d03-transaction-log-short-circuit-strips-buyer-tin-company-address-f.md) |
| 014 | P0 | `B06-D04` | QuoteView collects TIN only; no ID pair; no `validate-tin` | [014-p0-b06-d04-quoteview-collects-tin-only-no-id-pair-no-validate-tin.md](./014-p0-b06-d04-quoteview-collects-tin-only-no-id-pair-no-validate-tin.md) |
| 015 | P0 | `B06-D05` | CRM enrich-only: poisoned `IdValue` can never be corrected | [015-p0-b06-d05-crm-enrich-only-poisoned-idvalue-can-never-be-corrected.md](./015-p0-b06-d05-crm-enrich-only-poisoned-idvalue-can-never-be-corrected.md) |
| 016 | P0 | `B06-D09` | Type `01` tax `Percent` is a fraction, not a percent | [016-p0-b06-d09-type-01-tax-percent-is-a-fraction-not-a-percent.md](./016-p0-b06-d09-type-01-tax-percent-is-a-fraction-not-a-percent.md) |
| 017 | P0 | `B06-D19` | Type `02` credit note UBL can double-count tax | [017-p0-b06-d19-type-02-credit-note-ubl-can-double-count-tax.md](./017-p0-b06-d19-type-02-credit-note-ubl-can-double-count-tax.md) |
| 018 | P0 | `B07-I01` | Invite mail still requires tenant Resend BYOK; token is unrecoverable | [018-p0-b07-i01-invite-mail-still-requires-tenant-resend-byok-token-is-unrecover.md](./018-p0-b07-i01-invite-mail-still-requires-tenant-resend-byok-token-is-unrecover.md) |
| 019 | P0 | `B08-M01` | Resend bounce/complaint webhook never verifies a real `whsec_` secret | [019-p0-b08-m01-resend-bounce-complaint-webhook-never-verifies-a-real-whsec-secr.md](./019-p0-b08-m01-resend-bounce-complaint-webhook-never-verifies-a-real-whsec-secr.md) |
| 020 | P0 | `B08-M02` | Custom-quote B2B resolve stores CompanyName as LHDN IdValue | [020-p0-b08-m02-custom-quote-b2b-resolve-stores-companyname-as-lhdn-idvalue.md](./020-p0-b08-m02-custom-quote-b2b-resolve-stores-companyname-as-lhdn-idvalue.md) |
| 021 | P0 | `B09-U01` | Checkout success never receives a portal token | [021-p0-b09-u01-checkout-success-never-receives-a-portal-token.md](./021-p0-b09-u01-checkout-success-never-receives-a-portal-token.md) |
| 022 | P0 | `B09-U02` | Cookie session on `/{slug}/portal` is a 404 | [022-p0-b09-u02-cookie-session-on-slug-portal-is-a-404.md](./022-p0-b09-u02-cookie-session-on-slug-portal-is-a-404.md) |
| 023 | P0 | `B09-U03` | “Update payment method” from a cookie/tokenless portal interpolates `token=undefined` | [023-p0-b09-u03-update-payment-method-from-a-cookie-tokenless-portal-interpolate.md](./023-p0-b09-u03-update-payment-method-from-a-cookie-tokenless-portal-interpolate.md) |
| 024 | P0 | `B09-U04` | Viewer can change plan, seats, and collection pause | [024-p0-b09-u04-viewer-can-change-plan-seats-and-collection-pause.md](./024-p0-b09-u04-viewer-can-change-plan-seats-and-collection-pause.md) |
| 025 | P0 | `B10-X01` | Billing auto-debit claim starve (sibling of the pause bug 911d358 closed) | [025-p0-b10-x01-billing-auto-debit-claim-starve-sibling-of-the-pause-bug-911d358.md](./025-p0-b10-x01-billing-auto-debit-claim-starve-sibling-of-the-pause-bug-911d358.md) |
| 026 | P0 | `B10-X02` | B2C `alreadyConsolidated` is a no-op under fail-closed filters | [026-p0-b10-x02-b2c-alreadyconsolidated-is-a-no-op-under-fail-closed-filters.md](./026-p0-b10-x02-b2c-alreadyconsolidated-is-a-no-op-under-fail-closed-filters.md) |
| 027 | P0 | `B10-X03` | `POST /lhdn/webhooks` is a live dead register; Developers hub still teaches it | [027-p0-b10-x03-post-lhdn-webhooks-is-a-live-dead-register-developers-hub-still.md](./027-p0-b10-x03-post-lhdn-webhooks-is-a-live-dead-register-developers-hub-still.md) |
| 028 | P1 | `B01-C02` | Coupon `FOR UPDATE` is not inside a transaction | [028-p1-b01-c02-coupon-for-update-is-not-inside-a-transaction.md](./028-p1-b01-c02-coupon-for-update-is-not-inside-a-transaction.md) |
| 029 | P1 | `B01-C03` | Zero-amount and offline re-discount `product.Price`, not the chosen price row | [029-p1-b01-c03-zero-amount-and-offline-re-discount-product-price-not-the-chosen.md](./029-p1-b01-c03-zero-amount-and-offline-re-discount-product-price-not-the-chosen.md) |
| 030 | P1 | `B01-C04` | Idempotency replay returns EXPIRED URLs and empty-URL rows fall through to a second insert | [030-p1-b01-c04-idempotency-replay-returns-expired-urls-and-empty-url-rows-fall.md](./030-p1-b01-c04-idempotency-replay-returns-expired-urls-and-empty-url-rows-fall.md) |
| 031 | P1 | `B01-C05` | Custom quote remints hop-2 every time; portal key is per slug not per quote | [031-p1-b01-c05-custom-quote-remints-hop-2-every-time-portal-key-is-per-slug-not.md](./031-p1-b01-c05-custom-quote-remints-hop-2-every-time-portal-key-is-per-slug-not.md) |
| 032 | P1 | `B01-C06` | Hop-1 total omits SST; buyer is charged unit+tax | [032-p1-b01-c06-hop-1-total-omits-sst-buyer-is-charged-unit-tax.md](./032-p1-b01-c06-hop-1-total-omits-sst-buyer-is-charged-unit-tax.md) |
| 033 | P1 | `B01-C07` | Validate-coupon and hop-1 discount math ignore the selected price row | [033-p1-b01-c07-validate-coupon-and-hop-1-discount-math-ignore-the-selected-pric.md](./033-p1-b01-c07-validate-coupon-and-hop-1-discount-math-ignore-the-selected-pric.md) |
| 034 | P1 | `B01-C08` | Custom quotes and offline mark-paid never apply SST on first charge | [034-p1-b01-c08-custom-quotes-and-offline-mark-paid-never-apply-sst-on-first-cha.md](./034-p1-b01-c08-custom-quotes-and-offline-mark-paid-never-apply-sst-on-first-cha.md) |
| 035 | P1 | `B01-C09` | OPEN session has no concurrency token; two completers can both fulfill | [035-p1-b01-c09-open-session-has-no-concurrency-token-two-completers-can-both-fu.md](./035-p1-b01-c09-open-session-has-no-concurrency-token-two-completers-can-both-fu.md) |
| 036 | P1 | `B01-C10` | Expiry job vs paid webhook: money captured, session EXPIRED, no entitlement | [036-p1-b01-c10-expiry-job-vs-paid-webhook-money-captured-session-expired-no-ent.md](./036-p1-b01-c10-expiry-job-vs-paid-webhook-money-captured-session-expired-no-ent.md) |
| 037 | P1 | `B02-C02` | Missing pending product commits a broken ProductId | [037-p1-b02-c02-missing-pending-product-commits-a-broken-productid.md](./037-p1-b02-c02-missing-pending-product-commits-a-broken-productid.md) |
| 038 | P1 | `B02-C03` | Pending plan snapshot uses catalog default interval, not BillingInterval | [038-p1-b02-c03-pending-plan-snapshot-uses-catalog-default-interval-not-billingi.md](./038-p1-b02-c03-pending-plan-snapshot-uses-catalog-default-interval-not-billingi.md) |
| 039 | P1 | `B02-C04` | Success webhook RefreshSnapshot unfreezes UnitAmount | [039-p1-b02-c04-success-webhook-refreshsnapshot-unfreezes-unitamount.md](./039-p1-b02-c04-success-webhook-refreshsnapshot-unfreezes-unitamount.md) |
| 040 | P1 | `B02-C05` | Record-payment advances with product.Interval, not BillingInterval | [040-p1-b02-c05-record-payment-advances-with-product-interval-not-billinginterva.md](./040-p1-b02-c05-record-payment-advances-with-product-interval-not-billinginterva.md) |
| 041 | P1 | `B02-C06` | Stats MRR uses p.Interval, not BillingInterval | [041-p1-b02-c06-stats-mrr-uses-p-interval-not-billinginterval.md](./041-p1-b02-c06-stats-mrr-uses-p-interval-not-billinginterval.md) |
| 042 | P1 | `B02-C07` | ARPU denominator includes PAST_DUE | [042-p1-b02-c07-arpu-denominator-includes-pastdue.md](./042-p1-b02-c07-arpu-denominator-includes-pastdue.md) |
| 043 | P1 | `B02-C08` | Pause expiry charges the back invoice; manual resume skips it | [043-p1-b02-c08-pause-expiry-charges-the-back-invoice-manual-resume-skips-it.md](./043-p1-b02-c08-pause-expiry-charges-the-back-invoice-manual-resume-skips-it.md) |
| 044 | P1 | `B02-C09` | HasOpenDispute is set and billing ignores it | [044-p1-b02-c09-hasopendispute-is-set-and-billing-ignores-it.md](./044-p1-b02-c09-hasopendispute-is-set-and-billing-ignores-it.md) |
| 045 | P1 | `B02-C10` | UnitAmount > 0 sentinel cannot represent a $0 snapshot | [045-p1-b02-c10-unitamount-0-sentinel-cannot-represent-a-zero-amount-snapshot.md](./045-p1-b02-c10-unitamount-0-sentinel-cannot-represent-a-zero-amount-snapshot.md) |
| 046 | P1 | `B02-C11` | OrgRead can change plan, set seats, pause and resume collection | [046-p1-b02-c11-orgread-can-change-plan-set-seats-pause-and-resume-collection.md](./046-p1-b02-c11-orgread-can-change-plan-set-seats-pause-and-resume-collection.md) |
| 047 | P1 | `B02-C12` | Trial convert can stall in TRIALING after attempt 1 (webhook-dependent, job will not retry) | [047-p1-b02-c12-trial-convert-can-stall-in-trialing-after-attempt-1-webhook-depe.md](./047-p1-b02-c12-trial-convert-can-stall-in-trialing-after-attempt-1-webhook-depe.md) |
| 048 | P1 | `B03-C03` | One reminder slot per DayOffset; same-day EMAIL + AUTO_CHARGE cannot both run | [048-p1-b03-c03-one-reminder-slot-per-dayoffset-same-day-email-autocharge-cannot.md](./048-p1-b03-c03-one-reminder-slot-per-dayoffset-same-day-email-autocharge-cannot.md) |
| 049 | P1 | `B03-C04` | Reminder log is written when Commerce publishes, not when the buyer is emailed | [049-p1-b03-c04-reminder-log-is-written-when-commerce-publishes-not-when-the-buy.md](./049-p1-b03-c04-reminder-log-is-written-when-commerce-publishes-not-when-the-buy.md) |
| 050 | P1 | `B03-C05` | ACTIVE update-payment is RM 1; Stripe MYR minimum in this repo is RM 2 | [050-p1-b03-c05-active-update-payment-is-rm-1-stripe-myr-minimum-in-this-repo-is.md](./050-p1-b03-c05-active-update-payment-is-rm-1-stripe-myr-minimum-in-this-repo-is.md) |
| 051 | P1 | `B03-C06` | Arrears `is_reminder_only` is gateway-derived; Stripe reminder-only is sold as “update card” | [051-p1-b03-c06-arrears-isreminderonly-is-gateway-derived-stripe-reminder-only-i.md](./051-p1-b03-c06-arrears-isreminderonly-is-gateway-derived-stripe-reminder-only-i.md) |
| 052 | P1 | `B03-C07` | `DunningPausedUntil` does not pause pre-dunning | [052-p1-b03-c07-dunningpauseduntil-does-not-pause-pre-dunning.md](./052-p1-b03-c07-dunningpauseduntil-does-not-pause-pre-dunning.md) |
| 053 | P1 | `B03-C08` | `UnitAmount == 0` Gross is catalog `Price`, not zero | [053-p1-b03-c08-unitamount-0-gross-is-catalog-price-not-zero.md](./053-p1-b03-c08-unitamount-0-gross-is-catalog-price-not-zero.md) |
| 054 | P1 | `B03-C09` | Success and “dashboard” links drop the HMAC; buyer pays and cannot open the portal | [054-p1-b03-c09-success-and-dashboard-links-drop-the-hmac-buyer-pays-and-cannot.md](./054-p1-b03-c09-success-and-dashboard-links-drop-the-hmac-buyer-pays-and-cannot.md) |
| 055 | P1 | `B03-C10` | Magic-link endpoint is always-200 and unthrottled in this tree | [055-p1-b03-c10-magic-link-endpoint-is-always-200-and-unthrottled-in-this-tree.md](./055-p1-b03-c10-magic-link-endpoint-is-always-200-and-unthrottled-in-this-tree.md) |
| 056 | P1 | `B03-C11` | HMAC compare is not constant-time; missing `Jwt:Secret` is a shared mint key | [056-p1-b03-c11-hmac-compare-is-not-constant-time-missing-jwt-secret-is-a-shared.md](./056-p1-b03-c11-hmac-compare-is-not-constant-time-missing-jwt-secret-is-a-shared.md) |
| 057 | P1 | `B03-C12` | Failed-handler / Billing `StartPastDueDunningRunAsync` race the hourly claim | [057-p1-b03-c12-failed-handler-billing-startpastduedunningrunasync-race-the-hour.md](./057-p1-b03-c12-failed-handler-billing-startpastduedunningrunasync-race-the-hour.md) |
| 058 | P1 | `B03-C13` | Grace 0 / last-step day cancels in the same tick as “please pay” | [058-p1-b03-c13-grace-0-last-step-day-cancels-in-the-same-tick-as-please-pay.md](./058-p1-b03-c13-grace-0-last-step-day-cancels-in-the-same-tick-as-please-pay.md) |
| 059 | P1 | `B03-C14` | AUTO_CHARGE / Gross ignore `HasOpenDispute` | [059-p1-b03-c14-autocharge-gross-ignore-hasopendispute.md](./059-p1-b03-c14-autocharge-gross-ignore-hasopendispute.md) |
| 060 | P1 | `B04-P03` | CHIP off-session: `tokenId` used as a purchase id; `recurring_token` may not be one | [060-p1-b04-p03-chip-off-session-tokenid-used-as-a-purchase-id-recurringtoken-ma.md](./060-p1-b04-p03-chip-off-session-tokenid-used-as-a-purchase-id-recurringtoken-ma.md) |
| 061 | P1 | `B04-P04` | CHIP off-session has no processor idempotency key | [061-p1-b04-p04-chip-off-session-has-no-processor-idempotency-key.md](./061-p1-b04-p04-chip-off-session-has-no-processor-idempotency-key.md) |
| 062 | P1 | `B04-P05` | CHIP / Xendit clobber paying `tenant_id` on generate | [062-p1-b04-p05-chip-xendit-clobber-paying-tenantid-on-generate.md](./062-p1-b04-p05-chip-xendit-clobber-paying-tenantid-on-generate.md) |
| 063 | P1 | `B04-P06` | No inbound `tenant_id` vs URL tenant check; EventId unique is not tenant-scoped | [063-p1-b04-p06-no-inbound-tenantid-vs-url-tenant-check-eventid-unique-is-not-te.md](./063-p1-b04-p06-no-inbound-tenantid-vs-url-tenant-check-eventid-unique-is-not-te.md) |
| 064 | P1 | `B04-P07` | Off-session success is webhook-only; `processing` / `pending_charge` are adapter-true | [064-p1-b04-p07-off-session-success-is-webhook-only-processing-pendingcharge-are.md](./064-p1-b04-p07-off-session-success-is-webhook-only-processing-pendingcharge-are.md) |
| 065 | P1 | `B04-P08` | Late `PAYMENT_FAILED` after `PAYMENT_COMPLETED` on the same object still publishes | [065-p1-b04-p08-late-paymentfailed-after-paymentcompleted-on-the-same-object-sti.md](./065-p1-b04-p08-late-paymentfailed-after-paymentcompleted-on-the-same-object-sti.md) |
| 066 | P1 | `B04-P09` | Razorpay EventId fallback is still the payment id | [066-p1-b04-p09-razorpay-eventid-fallback-is-still-the-payment-id.md](./066-p1-b04-p09-razorpay-eventid-fallback-is-still-the-payment-id.md) |
| 067 | P1 | `B04-P10` | Off-session fail `GatewayTransactionId` is `off_session:{subscriptionId}` | [067-p1-b04-p10-off-session-fail-gatewaytransactionid-is-offsession-subscription.md](./067-p1-b04-p10-off-session-fail-gatewaytransactionid-is-offsession-subscription.md) |
| 068 | P1 | `B04-P11` | Razorpay `SetupFutureUsage` still mints a card registration link | [068-p1-b04-p11-razorpay-setupfutureusage-still-mints-a-card-registration-link.md](./068-p1-b04-p11-razorpay-setupfutureusage-still-mints-a-card-registration-link.md) |
| 069 | P1 | `B04-P12` | Razorpay `invoice.expired` mapped as payment-failed via the payment entity | [069-p1-b04-p12-razorpay-invoice-expired-mapped-as-payment-failed-via-the-paymen.md](./069-p1-b04-p12-razorpay-invoice-expired-mapped-as-payment-failed-via-the-paymen.md) |
| 070 | P1 | `B04-P13` | Refund loop is adapter bool; Stripe `pending` is success; only Stripe has an idempotency key | [070-p1-b04-p13-refund-loop-is-adapter-bool-stripe-pending-is-success-only-strip.md](./070-p1-b04-p13-refund-loop-is-adapter-bool-stripe-pending-is-success-only-strip.md) |
| 071 | P1 | `B04-P14` | Xendit refund posts `invoice_id`; API often wants a payment id | [071-p1-b04-p14-xendit-refund-posts-invoiceid-api-often-wants-a-payment-id.md](./071-p1-b04-p14-xendit-refund-posts-invoiceid-api-often-wants-a-payment-id.md) |
| 072 | P1 | `B04-P15` | Currency invented or case-split | [072-p1-b04-p15-currency-invented-or-case-split.md](./072-p1-b04-p15-currency-invented-or-case-split.md) |
| 073 | P1 | `B04-P16` | Xendit callback token is a shared secret, not a body signature | [073-p1-b04-p16-xendit-callback-token-is-a-shared-secret-not-a-body-signature.md](./073-p1-b04-p16-xendit-callback-token-is-a-shared-secret-not-a-body-signature.md) |
| 074 | P1 | `B04-P17` | Minor-units policy is three-way and quantity is applied differently | [074-p1-b04-p17-minor-units-policy-is-three-way-and-quantity-is-applied-differen.md](./074-p1-b04-p17-minor-units-policy-is-three-way-and-quantity-is-applied-differen.md) |
| 075 | P1 | `B05-L02` | `$0` Stripe setup booked as GMV `GATEWAY_PAYMENT` | [075-p1-b05-l02-zero-amount-stripe-setup-booked-as-gmv-gatewaypayment.md](./075-p1-b05-l02-zero-amount-stripe-setup-booked-as-gmv-gatewaypayment.md) |
| 076 | P1 | `B05-L06` | B2B MyInvois tax is raw `event.TaxAmount`, not resolved SST | [076-p1-b05-l06-b2b-myinvois-tax-is-raw-event-taxamount-not-resolved-sst.md](./076-p1-b05-l06-b2b-myinvois-tax-is-raw-event-taxamount-not-resolved-sst.md) |
| 077 | P1 | `B05-L07` | `GATEWAY_REFUND` rows are B2C/null consolidation and enter `B2cConsolidationJob` | [077-p1-b05-l07-gatewayrefund-rows-are-b2c-null-consolidation-and-enter-b2cconso.md](./077-p1-b05-l07-gatewayrefund-rows-are-b2c-null-consolidation-and-enter-b2cconso.md) |
| 078 | P1 | `B05-L08` | `alreadyConsolidated` check is fail-closed-blind | [078-p1-b05-l08-alreadyconsolidated-check-is-fail-closed-blind.md](./078-p1-b05-l08-alreadyconsolidated-check-is-fail-closed-blind.md) |
| 079 | P1 | `B05-L09` | Sequence allocation is not in the ledger transaction; comment lies | [079-p1-b05-l09-sequence-allocation-is-not-in-the-ledger-transaction-comment-lie.md](./079-p1-b05-l09-sequence-allocation-is-not-in-the-ledger-transaction-comment-lie.md) |
| 080 | P1 | `B05-L10` | Unique ledger key is global `(ReferenceType, ReferenceId)` | [080-p1-b05-l10-unique-ledger-key-is-global-referencetype-referenceid.md](./080-p1-b05-l10-unique-ledger-key-is-global-referencetype-referenceid.md) |
| 081 | P1 | `B05-L11` | `HasEntryBeenProcessedAsync` ignores tenant | [081-p1-b05-l11-hasentrybeenprocessedasync-ignores-tenant.md](./081-p1-b05-l11-hasentrybeenprocessedasync-ignores-tenant.md) |
| 082 | P1 | `B05-L12` | Refund journals drop FX | [082-p1-b05-l12-refund-journals-drop-fx.md](./082-p1-b05-l12-refund-journals-drop-fx.md) |
| 083 | P1 | `B05-L13` | Partial refund tax is independently scaled; last slice does not take remainder | [083-p1-b05-l13-partial-refund-tax-is-independently-scaled-last-slice-does-not-t.md](./083-p1-b05-l13-partial-refund-tax-is-independently-scaled-last-slice-does-not-t.md) |
| 084 | P1 | `B05-L14` | Billing will book a second full refund if a second Completed arrives | [084-p1-b05-l14-billing-will-book-a-second-full-refund-if-a-second-completed-arr.md](./084-p1-b05-l14-billing-will-book-a-second-full-refund-if-a-second-completed-arr.md) |
| 085 | P1 | `B05-L15` | Inbound refund webhooks are dropped; Stripe `pending` is terminal | [085-p1-b05-l15-inbound-refund-webhooks-are-dropped-stripe-pending-is-terminal.md](./085-p1-b05-l15-inbound-refund-webhooks-are-dropped-stripe-pending-is-terminal.md) |
| 086 | P1 | `B05-L16` | Lost GMV chargeback never journals unless ops refunds | [086-p1-b05-l16-lost-gmv-chargeback-never-journals-unless-ops-refunds.md](./086-p1-b05-l16-lost-gmv-chargeback-never-journals-unless-ops-refunds.md) |
| 087 | P1 | `B05-L17` | `HasOpenDispute` never clears; `MarkDisputed` overwrites `REFUNDED` | [087-p1-b05-l17-hasopendispute-never-clears-markdisputed-overwrites-refunded.md](./087-p1-b05-l17-hasopendispute-never-clears-markdisputed-overwrites-refunded.md) |
| 088 | P1 | `B05-L18` | Utility clawback uses dispute amount vs pack table, not credits granted | [088-p1-b05-l18-utility-clawback-uses-dispute-amount-vs-pack-table-not-credits-g.md](./088-p1-b05-l18-utility-clawback-uses-dispute-amount-vs-pack-table-not-credits-g.md) |
| 089 | P1 | `B05-L19` | Under-pack utility payment is a silent no-op | [089-p1-b05-l19-under-pack-utility-payment-is-a-silent-no-op.md](./089-p1-b05-l19-under-pack-utility-payment-is-a-silent-no-op.md) |
| 090 | P1 | `B05-L20` | `Saas:Plan:AmountMyr = 0` means unpaid Hub | [090-p1-b05-l20-saas-plan-amountmyr-0-means-unpaid-hub.md](./090-p1-b05-l20-saas-plan-amountmyr-0-means-unpaid-hub.md) |
| 091 | P1 | `B05-L21` | Live LHDN deduct can fail open after persist | [091-p1-b05-l21-live-lhdn-deduct-can-fail-open-after-persist.md](./091-p1-b05-l21-live-lhdn-deduct-can-fail-open-after-persist.md) |
| 092 | P1 | `B05-L22` | PDF after `SaveChanges` is not retried | [092-p1-b05-l22-pdf-after-savechanges-is-not-retried.md](./092-p1-b05-l22-pdf-after-savechanges-is-not-retried.md) |
| 093 | P1 | `B05-L23` | LHDN type-02 CN overstates `Total_including_tax` | [093-p1-b05-l23-lhdn-type-02-cn-overstates-totalincludingtax.md](./093-p1-b05-l23-lhdn-type-02-cn-overstates-totalincludingtax.md) |
| 094 | P1 | `B06-D06` | Ops / portal teach “Tax Invoice” / `VALID` on objects that are not cleared | [094-p1-b06-d06-ops-portal-teach-tax-invoice-valid-on-objects-that-are-not-clear.md](./094-p1-b06-d06-ops-portal-teach-tax-invoice-valid-on-objects-that-are-not-clear.md) |
| 095 | P1 | `B06-D08` | Offline product mark-paid drops `IsB2bRequired` | [095-p1-b06-d08-offline-product-mark-paid-drops-isb2brequired.md](./095-p1-b06-d08-offline-product-mark-paid-drops-isb2brequired.md) |
| 096 | P1 | `B06-D10` | B2B event `TaxAmount` is the raw gateway field, not the resolved SST | [096-p1-b06-d10-b2b-event-taxamount-is-the-raw-gateway-field-not-the-resolved-ss.md](./096-p1-b06-d10-b2b-event-taxamount-is-the-raw-gateway-field-not-the-resolved-ss.md) |
| 097 | P1 | `B06-D11` | One synthetic line `"B2B sale"` / classification `022`; quote lines discarded | [097-p1-b06-d11-one-synthetic-line-b2b-sale-classification-022-quote-lines-disca.md](./097-p1-b06-d11-one-synthetic-line-b2b-sale-classification-022-quote-lines-disca.md) |
| 098 | P1 | `B06-D12` | TIN HTTP 200 with empty / unparseable body is treated as valid | [098-p1-b06-d12-tin-http-200-with-empty-unparseable-body-is-treated-as-valid.md](./098-p1-b06-d12-tin-http-200-with-empty-unparseable-body-is-treated-as-valid.md) |
| 099 | P1 | `B06-D13` | Stub TIN lists are not the same | [099-p1-b06-d13-stub-tin-lists-are-not-the-same.md](./099-p1-b06-d13-stub-tin-lists-are-not-the-same.md) |
| 100 | P1 | `B06-D14` | Poller does not write poll UUID back onto `TaxDocument` | [100-p1-b06-d14-poller-does-not-write-poll-uuid-back-onto-taxdocument.md](./100-p1-b06-d14-poller-does-not-write-poll-uuid-back-onto-taxdocument.md) |
| 101 | P1 | `B06-D15` | QR host is always preprod; ops renders via `api.qrserver.com` | [101-p1-b06-d15-qr-host-is-always-preprod-ops-renders-via-api-qrserver-com.md](./101-p1-b06-d15-qr-host-is-always-preprod-ops-renders-via-api-qrserver-com.md) |
| 102 | P1 | `B06-D16` | Tenant `Environment` is cosmetic; checkout country default is `MY` | [102-p1-b06-d16-tenant-environment-is-cosmetic-checkout-country-default-is-my.md](./102-p1-b06-d16-tenant-environment-is-cosmetic-checkout-country-default-is-my.md) |
| 103 | P1 | `B06-D17` | Submit without credits: deduct-after-persist is fail-open | [103-p1-b06-d17-submit-without-credits-deduct-after-persist-is-fail-open.md](./103-p1-b06-d17-submit-without-credits-deduct-after-persist-is-fail-open.md) |
| 104 | P1 | `B06-D18` | Integrator `accepted_for_processing` is Lazuar, not MyInvois; product B2B checkout is coupled to MyInvois | [104-p1-b06-d18-integrator-acceptedforprocessing-is-lazuar-not-myinvois-product.md](./104-p1-b06-d18-integrator-acceptedforprocessing-is-lazuar-not-myinvois-product.md) |
| 105 | P1 | `B06-D20` | Partial refunds skip LHDN entirely; commercial `CN-` still issued | [105-p1-b06-d20-partial-refunds-skip-lhdn-entirely-commercial-cn-still-issued.md](./105-p1-b06-d20-partial-refunds-skip-lhdn-entirely-commercial-cn-still-issued.md) |
| 106 | P1 | `B06-D21` | Credit note PDF is never generated on refund; Lhdn handler can mint a second `CN-` | [106-p1-b06-d21-credit-note-pdf-is-never-generated-on-refund-lhdn-handler-can-mi.md](./106-p1-b06-d21-credit-note-pdf-is-never-generated-on-refund-lhdn-handler-can-mi.md) |
| 107 | P1 | `B06-D22` | Original document resolution can walk the wrong key; cancel+refund double row | [107-p1-b06-d22-original-document-resolution-can-walk-the-wrong-key-cancel-refun.md](./107-p1-b06-d22-original-document-resolution-can-walk-the-wrong-key-cancel-refun.md) |
| 108 | P1 | `B06-D24` | B2C consolidation idempotency is dead in workers; banner dies after VALID | [108-p1-b06-d24-b2c-consolidation-idempotency-is-dead-in-workers-banner-dies-aft.md](./108-p1-b06-d24-b2c-consolidation-idempotency-is-dead-in-workers-banner-dies-aft.md) |
| 109 | P1 | `B06-D25` | Sequence “prevents gaps” comment; `TaxDocument.InternalReferenceId` not unique | [109-p1-b06-d25-sequence-prevents-gaps-comment-taxdocument-internalreferenceid-n.md](./109-p1-b06-d25-sequence-prevents-gaps-comment-taxdocument-internalreferenceid-n.md) |
| 110 | P1 | `B06-D26` | Country / address / phone placeholders that INVALID a real submit | [110-p1-b06-d26-country-address-phone-placeholders-that-invalid-a-real-submit.md](./110-p1-b06-d26-country-address-phone-placeholders-that-invalid-a-real-submit.md) |
| 111 | P1 | `B06-D29` | Tax Invoice / Credit Note email falls back to Official Receipt template | [111-p1-b06-d29-tax-invoice-credit-note-email-falls-back-to-official-receipt-tem.md](./111-p1-b06-d29-tax-invoice-credit-note-email-falls-back-to-official-receipt-tem.md) |
| 112 | P1 | `B07-I02` | Password-reset and verify-email links still 404 | [112-p1-b07-i02-password-reset-and-verify-email-links-still-404.md](./112-p1-b07-i02-password-reset-and-verify-email-links-still-404.md) |
| 113 | P1 | `B07-I03` | Double-accept / already-member / second pending token → 500 | [113-p1-b07-i03-double-accept-already-member-second-pending-token-500.md](./113-p1-b07-i03-double-accept-already-member-second-pending-token-500.md) |
| 114 | P1 | `B07-I04` | Pending invite index is not unique | [114-p1-b07-i04-pending-invite-index-is-not-unique.md](./114-p1-b07-i04-pending-invite-index-is-not-unique.md) |
| 115 | P1 | `B07-I05` | Accept does not pre-check membership and writes no audit | [115-p1-b07-i05-accept-does-not-pre-check-membership-and-writes-no-audit.md](./115-p1-b07-i05-accept-does-not-pre-check-membership-and-writes-no-audit.md) |
| 116 | P1 | `B07-I06` | Production logout / stamp-mismatch may not delete `lazuar_auth` | [116-p1-b07-i06-production-logout-stamp-mismatch-may-not-delete-lazuarauth.md](./116-p1-b07-i06-production-logout-stamp-mismatch-may-not-delete-lazuarauth.md) |
| 117 | P1 | `B07-I07` | Security stamp is only enforced on `/auth/me` and platform `/auth/me` | [117-p1-b07-i07-security-stamp-is-only-enforced-on-auth-me-and-platform-auth-me.md](./117-p1-b07-i07-security-stamp-is-only-enforced-on-auth-me-and-platform-auth-me.md) |
| 118 | P1 | `B07-I10` | Last admin can be removed; self-remove is offered | [118-p1-b07-i10-last-admin-can-be-removed-self-remove-is-offered.md](./118-p1-b07-i10-last-admin-can-be-removed-self-remove-is-offered.md) |
| 119 | P1 | `B07-I11` | Archive does not revoke keys, drop memberships, or unpublish | [119-p1-b07-i11-archive-does-not-revoke-keys-drop-memberships-or-unpublish.md](./119-p1-b07-i11-archive-does-not-revoke-keys-drop-memberships-or-unpublish.md) |
| 120 | P1 | `B07-I12` | Superadmin synthetic entitlements vs real 403 | [120-p1-b07-i12-superadmin-synthetic-entitlements-vs-real-403.md](./120-p1-b07-i12-superadmin-synthetic-entitlements-vs-real-403.md) |
| 121 | P1 | `B07-I13` | Login is unauthenticated and unlimited | [121-p1-b07-i13-login-is-unauthenticated-and-unlimited.md](./121-p1-b07-i13-login-is-unauthenticated-and-unlimited.md) |
| 122 | P1 | `B07-I19` | `GlobalExceptionHandler` puts `exception.Message` on 500s | [122-p1-b07-i19-globalexceptionhandler-puts-exception-message-on-500s.md](./122-p1-b07-i19-globalexceptionhandler-puts-exception-message-on-500s.md) |
| 123 | P1 | `B07-I20` | `IntegratorProvisionAuth` treats injected membership `SUPER_ADMIN` as platform admin | [123-p1-b07-i20-integratorprovisionauth-treats-injected-membership-superadmin-as.md](./123-p1-b07-i20-integratorprovisionauth-treats-injected-membership-superadmin-as.md) |
| 124 | P1 | `B07-I25` | CORS default allow-any when `App:CorsOrigins` is empty | [124-p1-b07-i25-cors-default-allow-any-when-app-corsorigins-is-empty.md](./124-p1-b07-i25-cors-default-allow-any-when-app-corsorigins-is-empty.md) |
| 125 | P1 | `B07-I26` | API key 5-minute cache if revoke never consumes | [125-p1-b07-i26-api-key-5-minute-cache-if-revoke-never-consumes.md](./125-p1-b07-i26-api-key-5-minute-cache-if-revoke-never-consumes.md) |
| 126 | P1 | `B08-M03` | Resolve merges strangers by email and freezes the first tax identity | [126-p1-b08-m03-resolve-merges-strangers-by-email-and-freezes-the-first-tax-iden.md](./126-p1-b08-m03-resolve-merges-strangers-by-email-and-freezes-the-first-tax-iden.md) |
| 127 | P1 | `B08-M04` | Unsubscribe row blocks later BOUNCE/COMPLAINT insert | [127-p1-b08-m04-unsubscribe-row-blocks-later-bounce-complaint-insert.md](./127-p1-b08-m04-unsubscribe-row-blocks-later-bounce-complaint-insert.md) |
| 128 | P1 | `B08-M05` | HasValidEmailConfig is a false “valid”; quotes skip it | [128-p1-b08-m05-hasvalidemailconfig-is-a-false-valid-quotes-skip-it.md](./128-p1-b08-m05-hasvalidemailconfig-is-a-false-valid-quotes-skip-it.md) |
| 129 | P1 | `B08-M06` | Untrusted names and merchant HTML are emitted as email HTML | [129-p1-b08-m06-untrusted-names-and-merchant-html-are-emitted-as-email-html.md](./129-p1-b08-m06-untrusted-names-and-merchant-html-are-emitted-as-email-html.md) |
| 130 | P1 | `B08-M07` | Anonymize does not reach Billing PDFs, LHDN submissions, or delivery logs | [130-p1-b08-m07-anonymize-does-not-reach-billing-pdfs-lhdn-submissions-or-delive.md](./130-p1-b08-m07-anonymize-does-not-reach-billing-pdfs-lhdn-submissions-or-delive.md) |
| 131 | P1 | `B08-M08` | Digital Product Delivery fires for every one-time order and lies about the file | [131-p1-b08-m08-digital-product-delivery-fires-for-every-one-time-order-and-lies.md](./131-p1-b08-m08-digital-product-delivery-fires-for-every-one-time-order-and-lies.md) |
| 132 | P1 | `B08-M09` | Empty `Jwt:Secret` is a working HMAC key on unsubscribe | [132-p1-b08-m09-empty-jwt-secret-is-a-working-hmac-key-on-unsubscribe.md](./132-p1-b08-m09-empty-jwt-secret-is-a-working-hmac-key-on-unsubscribe.md) |
| 133 | P1 | `B08-M10` | Cancel (and wiki) still speak list price after Gross | [133-p1-b08-m10-cancel-and-wiki-still-speak-list-price-after-gross.md](./133-p1-b08-m10-cancel-and-wiki-still-speak-list-price-after-gross.md) |
| 134 | P1 | `B09-U05` | Ops/admin mobile nav cannot be reopened | [134-p1-b09-u05-ops-admin-mobile-nav-cannot-be-reopened.md](./134-p1-b09-u05-ops-admin-mobile-nav-cannot-be-reopened.md) |
| 135 | P1 | `B09-U06` | Production portal `/accept-invite` 302s to `localhost:3003` | [135-p1-b09-u06-production-portal-accept-invite-302s-to-localhost-3003.md](./135-p1-b09-u06-production-portal-accept-invite-302s-to-localhost-3003.md) |
| 136 | P1 | `B09-U07` | Admin login open redirect | [136-p1-b09-u07-admin-login-open-redirect.md](./136-p1-b09-u07-admin-login-open-redirect.md) |
| 137 | P1 | `B09-U08` | Portal cancel / keep ignore API errors | [137-p1-b09-u08-portal-cancel-keep-ignore-api-errors.md](./137-p1-b09-u08-portal-cancel-keep-ignore-api-errors.md) |
| 138 | P1 | `B09-U09` | Quote settled CTA and custom-success return are tokenless | [138-p1-b09-u09-quote-settled-cta-and-custom-success-return-are-tokenless.md](./138-p1-b09-u09-quote-settled-cta-and-custom-success-return-are-tokenless.md) |
| 139 | P1 | `B09-U10` | Update-payment `err=1` is never shown | [139-p1-b09-u10-update-payment-err-1-is-never-shown.md](./139-p1-b09-u10-update-payment-err-1-is-never-shown.md) |
| 140 | P1 | `B09-U11` | “Buyer Dashboard” header 404s | [140-p1-b09-u11-buyer-dashboard-header-404s.md](./140-p1-b09-u11-buyer-dashboard-header-404s.md) |
| 141 | P1 | `B09-U12` | Sales documents paint receipts as e-invoices | [141-p1-b09-u12-sales-documents-paint-receipts-as-e-invoices.md](./141-p1-b09-u12-sales-documents-paint-receipts-as-e-invoices.md) |
| 142 | P1 | `B09-U13` | Portal documents table puts LHDN Status on receipts and proformas | [142-p1-b09-u13-portal-documents-table-puts-lhdn-status-on-receipts-and-proforma.md](./142-p1-b09-u13-portal-documents-table-puts-lhdn-status-on-receipts-and-proforma.md) |
| 143 | P1 | `B09-U14` | No role chrome anywhere in ops | [143-p1-b09-u14-no-role-chrome-anywhere-in-ops.md](./143-p1-b09-u14-no-role-chrome-anywhere-in-ops.md) |
| 144 | P1 | `B09-U15` | Dashboard + Checkout Links lie to Member/Viewer | [144-p1-b09-u15-dashboard-checkout-links-lie-to-member-viewer.md](./144-p1-b09-u15-dashboard-checkout-links-lie-to-member-viewer.md) |
| 145 | P1 | `B09-U16` | Product form and checkout disagree about TIN | [145-p1-b09-u16-product-form-and-checkout-disagree-about-tin.md](./145-p1-b09-u16-product-form-and-checkout-disagree-about-tin.md) |
| 146 | P1 | `B09-U17` | Invite signup still creates a dummy workspace | [146-p1-b09-u17-invite-signup-still-creates-a-dummy-workspace.md](./146-p1-b09-u17-invite-signup-still-creates-a-dummy-workspace.md) |
| 147 | P1 | `B09-U18` | Entitlements query failure skips empty state | [147-p1-b09-u18-entitlements-query-failure-skips-empty-state.md](./147-p1-b09-u18-entitlements-query-failure-skips-empty-state.md) |
| 148 | P1 | `B09-U19` | Pricing page says LHDN merchant UI is not live | [148-p1-b09-u19-pricing-page-says-lhdn-merchant-ui-is-not-live.md](./148-p1-b09-u19-pricing-page-says-lhdn-merchant-ui-is-not-live.md) |
| 149 | P1 | `B09-U20` | Legal/privacy/landing still sell WhatsApp, communities, courses | [149-p1-b09-u20-legal-privacy-landing-still-sell-whatsapp-communities-courses.md](./149-p1-b09-u20-legal-privacy-landing-still-sell-whatsapp-communities-courses.md) |
| 150 | P1 | `B09-U21` | Superadmin cannot Save General Settings | [150-p1-b09-u21-superadmin-cannot-save-general-settings.md](./150-p1-b09-u21-superadmin-cannot-save-general-settings.md) |
| 151 | P1 | `B09-U22` | Email-missing checkout error is labeled a gateway outage | [151-p1-b09-u22-email-missing-checkout-error-is-labeled-a-gateway-outage.md](./151-p1-b09-u22-email-missing-checkout-error-is-labeled-a-gateway-outage.md) |
| 152 | P1 | `B09-U23` | `Period started {current_period_end}` | [152-p1-b09-u23-period-started-currentperiodend.md](./152-p1-b09-u23-period-started-currentperiodend.md) |
| 153 | P1 | `B09-U24` | Admin returnUrl drops search | [153-p1-b09-u24-admin-returnurl-drops-search.md](./153-p1-b09-u24-admin-returnurl-drops-search.md) |
| 154 | P1 | `B09-U25` | Anonymize / Invite / Save vault painted for roles that 403 | [154-p1-b09-u25-anonymize-invite-save-vault-painted-for-roles-that-403.md](./154-p1-b09-u25-anonymize-invite-save-vault-painted-for-roles-that-403.md) |
| 155 | P1 | `B09-U26` | Subscribers have no page 2; status filter is fake | [155-p1-b09-u26-subscribers-have-no-page-2-status-filter-is-fake.md](./155-p1-b09-u26-subscribers-have-no-page-2-status-filter-is-fake.md) |
| 156 | P1 | `B09-U27` | Catch-all erases 404 | [156-p1-b09-u27-catch-all-erases-404.md](./156-p1-b09-u27-catch-all-erases-404.md) |
| 157 | P1 | `B09-U28` | Portal plan change is ACTIVE+token only | [157-p1-b09-u28-portal-plan-change-is-active-token-only.md](./157-p1-b09-u28-portal-plan-change-is-active-token-only.md) |
| 158 | P1 | `B09-U29` | QuoteView can submit `customer@example.com` | [158-p1-b09-u29-quoteview-can-submit-customer-example-com.md](./158-p1-b09-u29-quoteview-can-submit-customer-example-com.md) |
| 159 | P1 | `B09-U30` | Accept-invite maps every 5xx to “already accepted” | [159-p1-b09-u30-accept-invite-maps-every-5xx-to-already-accepted.md](./159-p1-b09-u30-accept-invite-maps-every-5xx-to-already-accepted.md) |
| 160 | P1 | `B10-X04` | Inbox consumer marks success when the payload is not `INotification` | [160-p1-b10-x04-inbox-consumer-marks-success-when-the-payload-is-not-inotificati.md](./160-p1-b10-x04-inbox-consumer-marks-success-when-the-payload-is-not-inotificati.md) |
| 161 | P1 | `B10-X05` | `InMemoryEventBus` treats “no handlers” as success | [161-p1-b10-x05-inmemoryeventbus-treats-no-handlers-as-success.md](./161-p1-b10-x05-inmemoryeventbus-treats-no-handlers-as-success.md) |
| 162 | P1 | `B10-X06` | `TypeResolver` caches null for the process lifetime | [162-p1-b10-x06-typeresolver-caches-null-for-the-process-lifetime.md](./162-p1-b10-x06-typeresolver-caches-null-for-the-process-lifetime.md) |
| 163 | P1 | `B10-X07` | Repository ID lookups that **keep** the fail-closed filter (workers see nothing) | [163-p1-b10-x07-repository-id-lookups-that-keep-the-fail-closed-filter-workers-s.md](./163-p1-b10-x07-repository-id-lookups-that-keep-the-fail-closed-filter-workers-s.md) |
| 164 | P1 | `B10-X08` | Repository ID lookups that **ignore** filters without an org predicate | [164-p1-b10-x08-repository-id-lookups-that-ignore-filters-without-an-org-predica.md](./164-p1-b10-x08-repository-id-lookups-that-ignore-filters-without-an-org-predica.md) |
| 165 | P1 | `B10-X09` | `CrmQueryService.GetClientProfileAsync` is a global PII read by GUID | [165-p1-b10-x09-crmqueryservice-getclientprofileasync-is-a-global-pii-read-by-gu.md](./165-p1-b10-x09-crmqueryservice-getclientprofileasync-is-a-global-pii-read-by-gu.md) |
| 166 | P1 | `B10-X10` | Invoice reminder and checkout expiry have no claim | [166-p1-b10-x10-invoice-reminder-and-checkout-expiry-have-no-claim.md](./166-p1-b10-x10-invoice-reminder-and-checkout-expiry-have-no-claim.md) |
| 167 | P1 | `B10-X11` | `GetService` SST fail-open (undercharge) | [167-p1-b10-x11-getservice-sst-fail-open-undercharge.md](./167-p1-b10-x11-getservice-sst-fail-open-undercharge.md) |
| 168 | P1 | `B10-X12` | `GetService` CRM / One / tokens / config fail-open on money comms | [168-p1-b10-x12-getservice-crm-one-tokens-config-fail-open-on-money-comms.md](./168-p1-b10-x12-getservice-crm-one-tokens-config-fail-open-on-money-comms.md) |
| 169 | P1 | `B10-X13` | `AppOptions.ClientUrl` default 3020 is unbound; three other fallbacks disagree | [169-p1-b10-x13-appoptions-clienturl-default-3020-is-unbound-three-other-fallbac.md](./169-p1-b10-x13-appoptions-clienturl-default-3020-is-unbound-three-other-fallbac.md) |
| 170 | P1 | `B10-X14` | JWT secret is the HMAC key for documents, unsubscribe, magic links, and (fallback) vault | [170-p1-b10-x14-jwt-secret-is-the-hmac-key-for-documents-unsubscribe-magic-links.md](./170-p1-b10-x14-jwt-secret-is-the-hmac-key-for-documents-unsubscribe-magic-links.md) |
| 171 | P1 | `B10-X15` | M2M `?status=` filters the current page and rewrites `total_count` | [171-p1-b10-x15-m2m-status-filters-the-current-page-and-rewrites-totalcount.md](./171-p1-b10-x15-m2m-status-filters-the-current-page-and-rewrites-totalcount.md) |
| 172 | P1 | `B10-X16` | `PaymentWebhookPayloadDto` is still not the wire | [172-p1-b10-x16-paymentwebhookpayloaddto-is-still-not-the-wire.md](./172-p1-b10-x16-paymentwebhookpayloaddto-is-still-not-the-wire.md) |
| 173 | P1 | `B10-X17` | Human catalog and lifecycle tests still describe a four-status world | [173-p1-b10-x17-human-catalog-and-lifecycle-tests-still-describe-a-four-status-w.md](./173-p1-b10-x17-human-catalog-and-lifecycle-tests-still-describe-a-four-status-w.md) |
| 174 | P1 | `B10-X18` | Dead letters have metrics and no redrive | [174-p1-b10-x18-dead-letters-have-metrics-and-no-redrive.md](./174-p1-b10-x18-dead-letters-have-metrics-and-no-redrive.md) |
| 175 | P1 | `B10-X19` | Boot `MigrateAsync` continues on `PendingModelChanges` | [175-p1-b10-x19-boot-migrateasync-continues-on-pendingmodelchanges.md](./175-p1-b10-x19-boot-migrateasync-continues-on-pendingmodelchanges.md) |
| 176 | P1 | `B10-X20` | Accept-invite does not check existing membership and does not audit | [176-p1-b10-x20-accept-invite-does-not-check-existing-membership-and-does-not-au.md](./176-p1-b10-x20-accept-invite-does-not-check-existing-membership-and-does-not-au.md) |
| 177 | P1 | `B10-X21` | `/one/workspaces` exemption + empty ambient is a loaded gun | [177-p1-b10-x21-one-workspaces-exemption-empty-ambient-is-a-loaded-gun.md](./177-p1-b10-x21-one-workspaces-exemption-empty-ambient-is-a-loaded-gun.md) |
| 178 | P1 | `B10-X22` | `excludeIds` SQL concatenation (billing + dunning) | [178-p1-b10-x22-excludeids-sql-concatenation-billing-dunning.md](./178-p1-b10-x22-excludeids-sql-concatenation-billing-dunning.md) |
| 179 | P1 | `B10-X23` | Child / log tables with `OrganizationId` (or session id) and no tenant filter | [179-p1-b10-x23-child-log-tables-with-organizationid-or-session-id-and-no-tenant.md](./179-p1-b10-x23-child-log-tables-with-organizationid-or-session-id-and-no-tenant.md) |
| 180 | P1 | `B10-X24` | Eight idle inbox pollers + one global trigger | [180-p1-b10-x24-eight-idle-inbox-pollers-one-global-trigger.md](./180-p1-b10-x24-eight-idle-inbox-pollers-one-global-trigger.md) |
| 181 | P2 | `B01-C11` | Optional `IBillingQueryService` silently zeroes hop-1 SST | [181-p2-b01-c11-optional-ibillingqueryservice-silently-zeroes-hop-1-sst.md](./181-p2-b01-c11-optional-ibillingqueryservice-silently-zeroes-hop-1-sst.md) |
| 182 | P2 | `B01-C12` | SST is rounded per unit then multiplied | [182-p2-b01-c12-sst-is-rounded-per-unit-then-multiplied.md](./182-p2-b01-c12-sst-is-rounded-per-unit-then-multiplied.md) |
| 183 | P2 | `B01-C13` | `CheckoutSession` status machine is two unguarded setters | [183-p2-b01-c13-checkoutsession-status-machine-is-two-unguarded-setters.md](./183-p2-b01-c13-checkoutsession-status-machine-is-two-unguarded-setters.md) |
| 184 | P2 | `B01-C14` | Public checkout does not call `HasActiveSubscriptionAsync` | [184-p2-b01-c14-public-checkout-does-not-call-hasactivesubscriptionasync.md](./184-p2-b01-c14-public-checkout-does-not-call-hasactivesubscriptionasync.md) |
| 185 | P2 | `B01-C15` | Ad-hoc lines accept qty ≤ 0 and negative prices | [185-p2-b01-c15-ad-hoc-lines-accept-qty-0-and-negative-prices.md](./185-p2-b01-c15-ad-hoc-lines-accept-qty-0-and-negative-prices.md) |
| 186 | P2 | `B01-C16` | Custom hop-2 currency is hardcoded `MYR` | [186-p2-b01-c16-custom-hop-2-currency-is-hardcoded-myr.md](./186-p2-b01-c16-custom-hop-2-currency-is-hardcoded-myr.md) |
| 187 | P2 | `B01-C17` | Failed or abandoned hop-2 holds coupon inventory until expiry | [187-p2-b01-c17-failed-or-abandoned-hop-2-holds-coupon-inventory-until-expiry.md](./187-p2-b01-c17-failed-or-abandoned-hop-2-holds-coupon-inventory-until-expiry.md) |
| 188 | P2 | `B01-C18` | Open-checkout one-time vs subscription keys off `product.Interval`, not the paid price | [188-p2-b01-c18-open-checkout-one-time-vs-subscription-keys-off-product-interval.md](./188-p2-b01-c18-open-checkout-one-time-vs-subscription-keys-off-product-interval.md) |
| 189 | P2 | `B01-C19` | Session-by-id and coupon-by-id repository loads honour the fail-closed tenant filter | [189-p2-b01-c19-session-by-id-and-coupon-by-id-repository-loads-honour-the-fail.md](./189-p2-b01-c19-session-by-id-and-coupon-by-id-repository-loads-honour-the-fail.md) |
| 190 | P2 | `B01-C20` | Address country default `MYS` vs hop-1 form `MY` | [190-p2-b01-c20-address-country-default-mys-vs-hop-1-form-my.md](./190-p2-b01-c20-address-country-default-mys-vs-hop-1-form-my.md) |
| 191 | P2 | `B01-C21` | Mark-paid / zero-amount `ConfirmReservation` throws if reserved was already released | [191-p2-b01-c21-mark-paid-zero-amount-confirmreservation-throws-if-reserved-was.md](./191-p2-b01-c21-mark-paid-zero-amount-confirmreservation-throws-if-reserved-was.md) |
| 192 | P2 | `B01-C22` | Quote pay posts a fake email when CRM email is missing | [192-p2-b01-c22-quote-pay-posts-a-fake-email-when-crm-email-is-missing.md](./192-p2-b01-c22-quote-pay-posts-a-fake-email-when-crm-email-is-missing.md) |
| 193 | P2 | `B02-C13` | Claim exclude clause is FromSqlRaw string concat | [193-p2-b02-c13-claim-exclude-clause-is-fromsqlraw-string-concat.md](./193-p2-b02-c13-claim-exclude-clause-is-fromsqlraw-string-concat.md) |
| 194 | P2 | `B02-C14` | TrialEndsAt is never cleared | [194-p2-b02-c14-trialendsat-is-never-cleared.md](./194-p2-b02-c14-trialendsat-is-never-cleared.md) |
| 195 | P2 | `B02-C16` | CurrentPeriodEnd means start on paid rows and end on trials | [195-p2-b02-c16-currentperiodend-means-start-on-paid-rows-and-end-on-trials.md](./195-p2-b02-c16-currentperiodend-means-start-on-paid-rows-and-end-on-trials.md) |
| 196 | P2 | `B02-C17` | Resume() does not set CurrentPeriodEnd | [196-p2-b02-c17-resume-does-not-set-currentperiodend.md](./196-p2-b02-c17-resume-does-not-set-currentperiodend.md) |
| 197 | P2 | `B02-C18` | Cycle key and “period end” are UTC Date, not merchant local | [197-p2-b02-c18-cycle-key-and-period-end-are-utc-date-not-merchant-local.md](./197-p2-b02-c18-cycle-key-and-period-end-are-utc-date-not-merchant-local.md) |
| 198 | P2 | `B02-C19` | Admin can schedule plan/qty on a flagged sub; job discards them | [198-p2-b02-c19-admin-can-schedule-plan-qty-on-a-flagged-sub-job-discards-them.md](./198-p2-b02-c19-admin-can-schedule-plan-qty-on-a-flagged-sub-job-discards-them.md) |
| 199 | P2 | `B02-C20` | SST per-unit then × seats can be 1 sen off a line tax | [199-p2-b02-c20-sst-per-unit-then-seats-can-be-1-sen-off-a-line-tax.md](./199-p2-b02-c20-sst-per-unit-then-seats-can-be-1-sen-off-a-line-tax.md) |
| 200 | P2 | `B02-C21` | Activate-from-arrears no-op dates is a footgun | [200-p2-b02-c21-activate-from-arrears-no-op-dates-is-a-footgun.md](./200-p2-b02-c21-activate-from-arrears-no-op-dates-is-a-footgun.md) |
| 201 | P2 | `B02-C22` | ApplyPendingPlanChange returns true even when pending == current ProductId | [201-p2-b02-c22-applypendingplanchange-returns-true-even-when-pending-current-pr.md](./201-p2-b02-c22-applypendingplanchange-returns-true-even-when-pending-current-pr.md) |
| 202 | P2 | `B02-C23` | Integration cancel is immediate only | [202-p2-b02-c23-integration-cancel-is-immediate-only.md](./202-p2-b02-c23-integration-cancel-is-immediate-only.md) |
| 203 | P2 | `B03-C15` | Pre-dunning claim window is hardcoded 14 days | [203-p2-b03-c15-pre-dunning-claim-window-is-hardcoded-14-days.md](./203-p2-b03-c15-pre-dunning-claim-window-is-hardcoded-14-days.md) |
| 204 | P2 | `B03-C16` | TRIALING is invisible to pre-dunning | [204-p2-b03-c16-trialing-is-invisible-to-pre-dunning.md](./204-p2-b03-c16-trialing-is-invisible-to-pre-dunning.md) |
| 205 | P2 | `B03-C17` | Tokens are standard Base64 concatenated into query strings | [205-p2-b03-c17-tokens-are-standard-base64-concatenated-into-query-strings.md](./205-p2-b03-c17-tokens-are-standard-base64-concatenated-into-query-strings.md) |
| 206 | P2 | `B03-C18` | Arrears / renewal mint always `SetupFutureUsage: true` | [206-p2-b03-c18-arrears-renewal-mint-always-setupfutureusage-true.md](./206-p2-b03-c18-arrears-renewal-mint-always-setupfutureusage-true.md) |
| 207 | P2 | `B03-C19` | Snapshot lazy-backfill re-reads the live campaign | [207-p2-b03-c19-snapshot-lazy-backfill-re-reads-the-live-campaign.md](./207-p2-b03-c19-snapshot-lazy-backfill-re-reads-the-live-campaign.md) |
| 208 | P2 | `B03-C20` | `DeclineClassifier` is a Stripe hard-code table; `expired_card` is soft | [208-p2-b03-c20-declineclassifier-is-a-stripe-hard-code-table-expiredcard-is-sof.md](./208-p2-b03-c20-declineclassifier-is-a-stripe-hard-code-table-expiredcard-is-sof.md) |
| 209 | P2 | `B03-C21` | PENDING ChargeAttempt never times out | [209-p2-b03-c21-pending-chargeattempt-never-times-out.md](./209-p2-b03-c21-pending-chargeattempt-never-times-out.md) |
| 210 | P2 | `B03-C22` | Org-wide AUTO_CHARGE campaign is allowed on a Billplz-only tenant | [210-p2-b03-c22-org-wide-autocharge-campaign-is-allowed-on-a-billplz-only-tenant.md](./210-p2-b03-c22-org-wide-autocharge-campaign-is-allowed-on-a-billplz-only-tenant.md) |
| 211 | P2 | `B03-C23` | Newest-sub token subject ignores status | [211-p2-b03-c23-newest-sub-token-subject-ignores-status.md](./211-p2-b03-c23-newest-sub-token-subject-ignores-status.md) |
| 212 | P2 | `B03-C24` | Batch 50 / hour | [212-p2-b03-c24-batch-50-hour.md](./212-p2-b03-c24-batch-50-hour.md) |
| 213 | P2 | `B03-C25` | Portal documents merge by email, wider than ArrearsAccess | [213-p2-b03-c25-portal-documents-merge-by-email-wider-than-arrearsaccess.md](./213-p2-b03-c25-portal-documents-merge-by-email-wider-than-arrearsaccess.md) |
| 214 | P2 | `B03-C26` | `InferPaymentMethod` is “has vault id” | [214-p2-b03-c26-inferpaymentmethod-is-has-vault-id.md](./214-p2-b03-c26-inferpaymentmethod-is-has-vault-id.md) |
| 215 | P2 | `B03-C27` | WhatsApp flag true still “dispatches” | [215-p2-b03-c27-whatsapp-flag-true-still-dispatches.md](./215-p2-b03-c27-whatsapp-flag-true-still-dispatches.md) |
| 216 | P2 | `B03-C28` | Arrears API is not tenant-slug-bound | [216-p2-b03-c28-arrears-api-is-not-tenant-slug-bound.md](./216-p2-b03-c28-arrears-api-is-not-tenant-slug-bound.md) |
| 217 | P2 | `B03-C29` | `current_period_end` in dunning copy is `NextBillingDate` | [217-p2-b03-c29-currentperiodend-in-dunning-copy-is-nextbillingdate.md](./217-p2-b03-c29-currentperiodend-in-dunning-copy-is-nextbillingdate.md) |
| 218 | P2 | `B03-C30` | No HTTP test that missing token is 401 | [218-p2-b03-c30-no-http-test-that-missing-token-is-401.md](./218-p2-b03-c30-no-http-test-that-missing-token-is-401.md) |
| 219 | P2 | `B04-P18` | Empty webhook body is HTTP 500 | [219-p2-b04-p18-empty-webhook-body-is-http-500.md](./219-p2-b04-p18-empty-webhook-body-is-http-500.md) |
| 220 | P2 | `B04-P19` | CHIP webhook auto-register duplicates; verify key may not be `Webhook.public_key` | [220-p2-b04-p19-chip-webhook-auto-register-duplicates-verify-key-may-not-be-webh.md](./220-p2-b04-p19-chip-webhook-auto-register-duplicates-verify-key-may-not-be-webh.md) |
| 221 | P2 | `B04-P20` | Stripe setup `PAYMENT_COMPLETED` with null token if SetupIntent expand fails | [221-p2-b04-p20-stripe-setup-paymentcompleted-with-null-token-if-setupintent-exp.md](./221-p2-b04-p20-stripe-setup-paymentcompleted-with-null-token-if-setupintent-exp.md) |
| 222 | P2 | `B04-P21` | Stripe / CHIP fee expand failure is silent `GatewayFee=0` | [222-p2-b04-p21-stripe-chip-fee-expand-failure-is-silent-gatewayfee-0.md](./222-p2-b04-p21-stripe-chip-fee-expand-failure-is-silent-gatewayfee-0.md) |
| 223 | P2 | `B04-P22` | Dropped event types (wrong mapping / swallowed) | [223-p2-b04-p22-dropped-event-types-wrong-mapping-swallowed.md](./223-p2-b04-p22-dropped-event-types-wrong-mapping-swallowed.md) |
| 224 | P2 | `B04-P23` | M2M amount is `double` on the wire | [224-p2-b04-p23-m2m-amount-is-double-on-the-wire.md](./224-p2-b04-p23-m2m-amount-is-double-on-the-wire.md) |
| 225 | P2 | `B04-P24` | Dead / unused in this module | [225-p2-b04-p24-dead-unused-in-this-module.md](./225-p2-b04-p24-dead-unused-in-this-module.md) |
| 226 | P2 | `B04-P25` | Integration checkout GET lazy-expires only while `open` | [226-p2-b04-p25-integration-checkout-get-lazy-expires-only-while-open.md](./226-p2-b04-p25-integration-checkout-get-lazy-expires-only-while-open.md) |
| 227 | P2 | `B04-P26` | Placeholder PII on generate | [227-p2-b04-p26-placeholder-pii-on-generate.md](./227-p2-b04-p26-placeholder-pii-on-generate.md) |
| 228 | P2 | `B05-L24` | `ValidateBalanced` is a one-sided toy | [228-p2-b05-l24-validatebalanced-is-a-one-sided-toy.md](./228-p2-b05-l24-validatebalanced-is-a-one-sided-toy.md) |
| 229 | P2 | `B05-L25` | Document year is UTC, not MYT | [229-p2-b05-l25-document-year-is-utc-not-myt.md](./229-p2-b05-l25-document-year-is-utc-not-myt.md) |
| 230 | P2 | `B05-L26` | Summary is P&amp;L net, labelled cash, currency hardcoded MYR | [230-p2-b05-l26-summary-is-p-amp-l-net-labelled-cash-currency-hardcoded-myr.md](./230-p2-b05-l26-summary-is-p-amp-l-net-labelled-cash-currency-hardcoded-myr.md) |
| 231 | P2 | `B05-L27` | Ledger `type_filter=reversals` omits `SYSTEM_CREDIT_CHARGEBACK` | [231-p2-b05-l27-ledger-typefilter-reversals-omits-systemcreditchargeback.md](./231-p2-b05-l27-ledger-typefilter-reversals-omits-systemcreditchargeback.md) |
| 232 | P2 | `B05-L28` | `RefundedFee` is always 0 | [232-p2-b05-l28-refundedfee-is-always-0.md](./232-p2-b05-l28-refundedfee-is-always-0.md) |
| 233 | P2 | `B05-L29` | Manual enrollment is 100% cash, 0 tax, 0 fee | [233-p2-b05-l29-manual-enrollment-is-100-cash-0-tax-0-fee.md](./233-p2-b05-l29-manual-enrollment-is-100-cash-0-tax-0-fee.md) |
| 234 | P2 | `B05-L30` | Dead / parked writers that will confuse the next editor | [234-p2-b05-l30-dead-parked-writers-that-will-confuse-the-next-editor.md](./234-p2-b05-l30-dead-parked-writers-that-will-confuse-the-next-editor.md) |
| 235 | P2 | `B05-L31` | Credit hold: no unique correlation; `RELEASED` never written; exhaust stays `HELD` | [235-p2-b05-l31-credit-hold-no-unique-correlation-released-never-written-exhaust.md](./235-p2-b05-l31-credit-hold-no-unique-correlation-released-never-written-exhaust.md) |
| 236 | P2 | `B05-L32` | `LedgerLine` and `CreditLedger` have no `OrganizationId` | [236-p2-b05-l32-ledgerline-and-creditledger-have-no-organizationid.md](./236-p2-b05-l32-ledgerline-and-creditledger-have-no-organizationid.md) |
| 237 | P2 | `B05-L33` | `$0`-priced `ProcessZeroAmount` writes a no-line journal | [237-p2-b05-l33-zero-amount-priced-processzeroamount-writes-a-no-line-journal.md](./237-p2-b05-l33-zero-amount-priced-processzeroamount-writes-a-no-line-journal.md) |
| 238 | P2 | `B05-L34` | Credit-note PDF builder ignores contra lines | [238-p2-b05-l34-credit-note-pdf-builder-ignores-contra-lines.md](./238-p2-b05-l34-credit-note-pdf-builder-ignores-contra-lines.md) |
| 239 | P2 | `B05-L35` | Billplz fee is always 0 in the journal | [239-p2-b05-l35-billplz-fee-is-always-0-in-the-journal.md](./239-p2-b05-l35-billplz-fee-is-always-0-in-the-journal.md) |
| 240 | P2 | `B05-L36` | Hub SaaS dispute does not reverse `SYSTEM_SAAS_FEE` | [240-p2-b05-l36-hub-saas-dispute-does-not-reverse-systemsaasfee.md](./240-p2-b05-l36-hub-saas-dispute-does-not-reverse-systemsaasfee.md) |
| 241 | P2 | `B05-L37` | Platform invoice fallback can print a Guid slice | [241-p2-b05-l37-platform-invoice-fallback-can-print-a-guid-slice.md](./241-p2-b05-l37-platform-invoice-fallback-can-print-a-guid-slice.md) |
| 242 | P2 | `B05-L38` | `TaxInvoiceId` is still the dual-use dumping ground | [242-p2-b05-l38-taxinvoiceid-is-still-the-dual-use-dumping-ground.md](./242-p2-b05-l38-taxinvoiceid-is-still-the-dual-use-dumping-ground.md) |
| 243 | P2 | `B05-L39` | `ChargebackClawbackHandler` comment still says “utility only” | [243-p2-b05-l39-chargebackclawbackhandler-comment-still-says-utility-only.md](./243-p2-b05-l39-chargebackclawbackhandler-comment-still-says-utility-only.md) |
| 244 | P2 | `B05-L40` | `ManualPaymentRecordedIntegrationEvent` has no consumer | [244-p2-b05-l40-manualpaymentrecordedintegrationevent-has-no-consumer.md](./244-p2-b05-l40-manualpaymentrecordedintegrationevent-has-no-consumer.md) |
| 245 | P2 | `B06-D07` | ProductForm subtitle: “We do not validate the TIN at checkout” | [245-p2-b06-d07-productform-subtitle-we-do-not-validate-the-tin-at-checkout.md](./245-p2-b06-d07-productform-subtitle-we-do-not-validate-the-tin-at-checkout.md) |
| 246 | P2 | `B06-D23` | Types `03` / `04` / `11`–`14` are strategy-only; page title overclaims | [246-p2-b06-d23-types-03-04-11-14-are-strategy-only-page-title-overclaims.md](./246-p2-b06-d23-types-03-04-11-14-are-strategy-only-page-title-overclaims.md) |
| 247 | P2 | `B06-D27` | InvoiceIssued is dead; comments name handlers that do not exist | [247-p2-b06-d27-invoiceissued-is-dead-comments-name-handlers-that-do-not-exist.md](./247-p2-b06-d27-invoiceissued-is-dead-comments-name-handlers-that-do-not-exist.md) |
| 248 | P2 | `B06-D28` | Lhdn README still says signatures unimplemented / XAdES | [248-p2-b06-d28-lhdn-readme-still-says-signatures-unimplemented-xades.md](./248-p2-b06-d28-lhdn-readme-still-says-signatures-unimplemented-xades.md) |
| 249 | P2 | `B06-D30` | Draft proforma identity and date are thin | [249-p2-b06-d30-draft-proforma-identity-and-date-are-thin.md](./249-p2-b06-d30-draft-proforma-identity-and-date-are-thin.md) |
| 250 | P2 | `B06-D31` | Quote-only buyer cannot open portal documents; same-email union | [250-p2-b06-d31-quote-only-buyer-cannot-open-portal-documents-same-email-union.md](./250-p2-b06-d31-quote-only-buyer-cannot-open-portal-documents-same-email-union.md) |
| 251 | P2 | `B06-D32` | Large B2C `NEEDS_BUYER_TIN` has no resolution product | [251-p2-b06-d32-large-b2c-needsbuyertin-has-no-resolution-product.md](./251-p2-b06-d32-large-b2c-needsbuyertin-has-no-resolution-product.md) |
| 252 | P2 | `B06-D33` | Buyer reject is not implemented | [252-p2-b06-d33-buyer-reject-is-not-implemented.md](./252-p2-b06-d33-buyer-reject-is-not-implemented.md) |
| 253 | P2 | `B06-D34` | Stationery empty TIN is omitted, not “TIN not on file” | [253-p2-b06-d34-stationery-empty-tin-is-omitted-not-tin-not-on-file.md](./253-p2-b06-d34-stationery-empty-tin-is-omitted-not-tin-not-on-file.md) |
| 254 | P2 | `B06-D35` | Quotes page leftover “Tracking ad-hoc invoices” | [254-p2-b06-d35-quotes-page-leftover-tracking-ad-hoc-invoices.md](./254-p2-b06-d35-quotes-page-leftover-tracking-ad-hoc-invoices.md) |
| 255 | P2 | `B06-D36` | JSON 1.1 signer exists; ACCEPT does not (honesty, not a code bug) | [255-p2-b06-d36-json-1-1-signer-exists-accept-does-not-honesty-not-a-code-bug.md](./255-p2-b06-d36-json-1-1-signer-exists-accept-does-not-honesty-not-a-code-bug.md) |
| 256 | P2 | `B07-I08` | AcceptInvitePage maps every 500 to “already accepted” and caches errors | [256-p2-b07-i08-acceptinvitepage-maps-every-500-to-already-accepted-and-caches-e.md](./256-p2-b07-i08-acceptinvitepage-maps-every-500-to-already-accepted-and-caches-e.md) |
| 257 | P2 | `B07-I09` | Team page never lists or revokes pending invites | [257-p2-b07-i09-team-page-never-lists-or-revokes-pending-invites.md](./257-p2-b07-i09-team-page-never-lists-or-revokes-pending-invites.md) |
| 258 | P2 | `B07-I14` | Register always creates a workspace (invite leftover) | [258-p2-b07-i14-register-always-creates-a-workspace-invite-leftover.md](./258-p2-b07-i14-register-always-creates-a-workspace-invite-leftover.md) |
| 259 | P2 | `B07-I15` | Dual role model + register body `ADMIN` vs cookie `CLIENT` | [259-p2-b07-i15-dual-role-model-register-body-admin-vs-cookie-client.md](./259-p2-b07-i15-dual-role-model-register-body-admin-vs-cookie-client.md) |
| 260 | P2 | `B07-I16` | Slug uniqueness is check-then-act; update skips the check | [260-p2-b07-i16-slug-uniqueness-is-check-then-act-update-skips-the-check.md](./260-p2-b07-i16-slug-uniqueness-is-check-then-act-update-skips-the-check.md) |
| 261 | P2 | `B07-I17` | Reset-password is an email oracle | [261-p2-b07-i17-reset-password-is-an-email-oracle.md](./261-p2-b07-i17-reset-password-is-an-email-oracle.md) |
| 262 | P2 | `B07-I18` | API key prefix parse is case-insensitive; hash is not | [262-p2-b07-i18-api-key-prefix-parse-is-case-insensitive-hash-is-not.md](./262-p2-b07-i18-api-key-prefix-parse-is-case-insensitive-hash-is-not.md) |
| 263 | P2 | `B07-I21` | Human ADMIN bypass of Integration* policies (except PaymentsMe) | [263-p2-b07-i21-human-admin-bypass-of-integration-policies-except-paymentsme.md](./263-p2-b07-i21-human-admin-bypass-of-integration-policies-except-paymentsme.md) |
| 264 | P2 | `B07-I22` | Entitlements query error skips empty-state and renders a hollow shell | [264-p2-b07-i22-entitlements-query-error-skips-empty-state-and-renders-a-hollow.md](./264-p2-b07-i22-entitlements-query-error-skips-empty-state-and-renders-a-hollow.md) |
| 265 | P2 | `B07-I23` | `accepted_terms` is a request-time gate; TOS is the buyer document | [265-p2-b07-i23-acceptedterms-is-a-request-time-gate-tos-is-the-buyer-document.md](./265-p2-b07-i23-acceptedterms-is-a-request-time-gate-tos-is-the-buyer-document.md) |
| 266 | P2 | `B07-I24` | Register rate-limit key trusts first `X-Forwarded-For` hop; empty key allows | [266-p2-b07-i24-register-rate-limit-key-trusts-first-x-forwarded-for-hop-empty-k.md](./266-p2-b07-i24-register-rate-limit-key-trusts-first-x-forwarded-for-hop-empty-k.md) |
| 267 | P2 | `B07-I27` | Cookie `OnMessageReceived` always wins over Authorization JWT | [267-p2-b07-i27-cookie-onmessagereceived-always-wins-over-authorization-jwt.md](./267-p2-b07-i27-cookie-onmessagereceived-always-wins-over-authorization-jwt.md) |
| 268 | P2 | `B07-I28` | No invite resend; revoke has no audit | [268-p2-b07-i28-no-invite-resend-revoke-has-no-audit.md](./268-p2-b07-i28-no-invite-resend-revoke-has-no-audit.md) |
| 269 | P2 | `B07-I29` | `HasTenantAccess` ignores archive and role | [269-p2-b07-i29-hastenantaccess-ignores-archive-and-role.md](./269-p2-b07-i29-hastenantaccess-ignores-archive-and-role.md) |
| 270 | P2 | `B07-I30` | GET members/workspace use 401 for IDOR; audit uses 403 | [270-p2-b07-i30-get-members-workspace-use-401-for-idor-audit-uses-403.md](./270-p2-b07-i30-get-members-workspace-use-401-for-idor-audit-uses-403.md) |
| 271 | P2 | `B07-I31` | `ExecutionContextAccessor.UserRole` is the first role claim | [271-p2-b07-i31-executioncontextaccessor-userrole-is-the-first-role-claim.md](./271-p2-b07-i31-executioncontextaccessor-userrole-is-the-first-role-claim.md) |
| 272 | P2 | `B07-I32` | Genesis rotates superadmin password from env every boot | [272-p2-b07-i32-genesis-rotates-superadmin-password-from-env-every-boot.md](./272-p2-b07-i32-genesis-rotates-superadmin-password-from-env-every-boot.md) |
| 273 | P2 | `B07-I33` | Storage presign is any member; path is tenant-required | [273-p2-b07-i33-storage-presign-is-any-member-path-is-tenant-required.md](./273-p2-b07-i33-storage-presign-is-any-member-path-is-tenant-required.md) |
| 274 | P2 | `B07-I34` | IApiCredentialService + LHDN façade comments still promise implicit LHDN defaults | [274-p2-b07-i34-iapicredentialservice-lhdn-fa-ade-comments-still-promise-implici.md](./274-p2-b07-i34-iapicredentialservice-lhdn-fa-ade-comments-still-promise-implici.md) |
| 275 | P2 | `B07-I35` | One README still documents `CLIENT` membership and omits `AuditEvents` | [275-p2-b07-i35-one-readme-still-documents-client-membership-and-omits-auditeven.md](./275-p2-b07-i35-one-readme-still-documents-client-membership-and-omits-auditeven.md) |
| 276 | P2 | `B07-I36` | AppOptions ClientUrl default 3020 vs live 3004 | [276-p2-b07-i36-appoptions-clienturl-default-3020-vs-live-3004.md](./276-p2-b07-i36-appoptions-clienturl-default-3020-vs-live-3004.md) |
| 277 | P2 | `B07-I37` | Invite token in the query string | [277-p2-b07-i37-invite-token-in-the-query-string.md](./277-p2-b07-i37-invite-token-in-the-query-string.md) |
| 278 | P2 | `B07-I38` | CSRF residual: SameSite=Lax, no anti-CSRF token, Domain `.lazuar.com` | [278-p2-b07-i38-csrf-residual-samesite-lax-no-anti-csrf-token-domain-lazuar-com.md](./278-p2-b07-i38-csrf-residual-samesite-lax-no-anti-csrf-token-domain-lazuar-com.md) |
| 279 | P2 | `B07-I39` | No MFA, SSO, lockout, session list, password complexity | [279-p2-b07-i39-no-mfa-sso-lockout-session-list-password-complexity.md](./279-p2-b07-i39-no-mfa-sso-lockout-session-list-password-complexity.md) |
| 280 | P2 | `B07-I40` | `UserRegisteredDomainEvent` is orphaned; verify never starts at register | [280-p2-b07-i40-userregistereddomainevent-is-orphaned-verify-never-starts-at-reg.md](./280-p2-b07-i40-userregistereddomainevent-is-orphaned-verify-never-starts-at-reg.md) |
| 281 | P2 | `B08-M11` | CreateClientProfile `email OR phone` matches empty phones | [281-p2-b08-m11-createclientprofile-email-or-phone-matches-empty-phones.md](./281-p2-b08-m11-createclientprofile-email-or-phone-matches-empty-phones.md) |
| 282 | P2 | `B08-M12` | Unique `(Email, Phone)` vs resolve-by-email | [282-p2-b08-m12-unique-email-phone-vs-resolve-by-email.md](./282-p2-b08-m12-unique-email-phone-vs-resolve-by-email.md) |
| 283 | P2 | `B08-M13` | GlobalUserProfileUpdated overwrites every linked CRM email | [283-p2-b08-m13-globaluserprofileupdated-overwrites-every-linked-crm-email.md](./283-p2-b08-m13-globaluserprofileupdated-overwrites-every-linked-crm-email.md) |
| 284 | P2 | `B08-M14` | Invoice reminder currency/SST and missing-template burn | [284-p2-b08-m14-invoice-reminder-currency-sst-and-missing-template-burn.md](./284-p2-b08-m14-invoice-reminder-currency-sst-and-missing-template-burn.md) |
| 285 | P2 | `B08-M15` | Immediate fail amount is empty; context port cannot carry Gross | [285-p2-b08-m15-immediate-fail-amount-is-empty-context-port-cannot-carry-gross.md](./285-p2-b08-m15-immediate-fail-amount-is-empty-context-port-cannot-carry-gross.md) |
| 286 | P2 | `B08-M16` | Tax Invoice / Credit Note email uses Official Receipt copy | [286-p2-b08-m16-tax-invoice-credit-note-email-uses-official-receipt-copy.md](./286-p2-b08-m16-tax-invoice-credit-note-email-uses-official-receipt-copy.md) |
| 287 | P2 | `B08-M17` | Template update skips variable validation; hydrator leaves unknown tags | [287-p2-b08-m17-template-update-skips-variable-validation-hydrator-leaves-unknow.md](./287-p2-b08-m17-template-update-skips-variable-validation-hydrator-leaves-unknow.md) |
| 288 | P2 | `B08-M18` | Broadcast counts lie; RecordSent is pre-provider; consent is unreachable from checkout | [288-p2-b08-m18-broadcast-counts-lie-recordsent-is-pre-provider-consent-is-unrea.md](./288-p2-b08-m18-broadcast-counts-lie-recordsent-is-pre-provider-consent-is-unrea.md) |
| 289 | P2 | `B08-M19` | `POST /messaging/notify` trusts body.TenantId | [289-p2-b08-m19-post-messaging-notify-trusts-body-tenantid.md](./289-p2-b08-m19-post-messaging-notify-trusts-body-tenantid.md) |
| 290 | P2 | `B08-M20` | `FixedTimeEquals` on hex/base64 of unequal length throws 500 | [290-p2-b08-m20-fixedtimeequals-on-hex-base64-of-unequal-length-throws-500.md](./290-p2-b08-m20-fixedtimeequals-on-hex-base64-of-unequal-length-throws-500.md) |
| 291 | P2 | `B08-M21` | SaveEmailConfig does not require SenderEmail ∈ listed domains | [291-p2-b08-m21-saveemailconfig-does-not-require-senderemail-listed-domains.md](./291-p2-b08-m21-saveemailconfig-does-not-require-senderemail-listed-domains.md) |
| 292 | P2 | `B08-M22` | `GetClientProfileAsync` is global-by-id | [292-p2-b08-m22-getclientprofileasync-is-global-by-id.md](./292-p2-b08-m22-getclientprofileasync-is-global-by-id.md) |
| 293 | P2 | `B08-M23` | Parser misses string `to`; webhook 200 on suppress failure | [293-p2-b08-m23-parser-misses-string-to-webhook-200-on-suppress-failure.md](./293-p2-b08-m23-parser-misses-string-to-webhook-200-on-suppress-failure.md) |
| 294 | P2 | `B08-M24` | Test reminder always mails `admin@lazuars.io` via tenant BYOK | [294-p2-b08-m24-test-reminder-always-mails-admin-lazuars-io-via-tenant-byok.md](./294-p2-b08-m24-test-reminder-always-mails-admin-lazuars-io-via-tenant-byok.md) |
| 295 | P2 | `B08-M25` | Anonymize then cancel mails `deleted_{id}@localhost` | [295-p2-b08-m25-anonymize-then-cancel-mails-deleted-id-localhost.md](./295-p2-b08-m25-anonymize-then-cancel-mails-deleted-id-localhost.md) |
| 296 | P2 | `B08-M26` | Checkout never collects marketing consent | [296-p2-b08-m26-checkout-never-collects-marketing-consent.md](./296-p2-b08-m26-checkout-never-collects-marketing-consent.md) |
| 297 | P2 | `B08-M27` | Dual CMS and leftover `reminder.due` | [297-p2-b08-m27-dual-cms-and-leftover-reminder-due.md](./297-p2-b08-m27-dual-cms-and-leftover-reminder-due.md) |
| 298 | P2 | `B08-M28` | Brand wrapper still injects `<br/>` into HTML | [298-p2-b08-m28-brand-wrapper-still-injects-br-into-html.md](./298-p2-b08-m28-brand-wrapper-still-injects-br-into-html.md) |
| 299 | P2 | `B09-U31` | `hasChanges \|\| true` | [299-p2-b09-u31-haschanges-true.md](./299-p2-b09-u31-haschanges-true.md) |
| 300 | P2 | `B09-U32` | Utility Ledger is a secret route | [300-p2-b09-u32-utility-ledger-is-a-secret-route.md](./300-p2-b09-u32-utility-ledger-is-a-secret-route.md) |
| 301 | P2 | `B09-U33` | Portal header shows “Member” for guests | [301-p2-b09-u33-portal-header-shows-member-for-guests.md](./301-p2-b09-u33-portal-header-shows-member-for-guests.md) |
| 302 | P2 | `B09-U34` | Portal logout does not redirect | [302-p2-b09-u34-portal-logout-does-not-redirect.md](./302-p2-b09-u34-portal-logout-does-not-redirect.md) |
| 303 | P2 | `B09-U35` | WhatsApp Body * required on template create; dunning editor says not connected | [303-p2-b09-u35-whatsapp-body-required-on-template-create-dunning-editor-says-no.md](./303-p2-b09-u35-whatsapp-body-required-on-template-create-dunning-editor-says-no.md) |
| 304 | P2 | `B09-U36` | Checkout i18n holes | [304-p2-b09-u36-checkout-i18n-holes.md](./304-p2-b09-u36-checkout-i18n-holes.md) |
| 305 | P2 | `B09-U37` | Disputes are a museum | [305-p2-b09-u37-disputes-are-a-museum.md](./305-p2-b09-u37-disputes-are-a-museum.md) |
| 306 | P2 | `B09-U38` | Audit 403 → empty | [306-p2-b09-u38-audit-403-empty.md](./306-p2-b09-u38-audit-403-empty.md) |
| 307 | P2 | `B09-U39` | ARR tooltip is the MRR sentence | [307-p2-b09-u39-arr-tooltip-is-the-mrr-sentence.md](./307-p2-b09-u39-arr-tooltip-is-the-mrr-sentence.md) |
| 308 | P2 | `B09-U40` | Draft vs Archived | [308-p2-b09-u40-draft-vs-archived.md](./308-p2-b09-u40-draft-vs-archived.md) |
| 309 | P2 | `B09-U41` | Ops legal hrefs require Caddy | [309-p2-b09-u41-ops-legal-hrefs-require-caddy.md](./309-p2-b09-u41-ops-legal-hrefs-require-caddy.md) |
| 310 | P2 | `B09-U42` | Country `MY` vs stationery `MYS` | [310-p2-b09-u42-country-my-vs-stationery-mys.md](./310-p2-b09-u42-country-my-vs-stationery-mys.md) |
| 311 | P2 | `B09-U43` | Xendit/Razorpay/Stripe first-save does not require webhook secret | [311-p2-b09-u43-xendit-razorpay-stripe-first-save-does-not-require-webhook-secre.md](./311-p2-b09-u43-xendit-razorpay-stripe-first-save-does-not-require-webhook-secre.md) |
| 312 | P2 | `B09-U44` | Admin vault has no environment select | [312-p2-b09-u44-admin-vault-has-no-environment-select.md](./312-p2-b09-u44-admin-vault-has-no-environment-select.md) |
| 313 | P2 | `B09-U45` | Identity Verified on any successful GET | [313-p2-b09-u45-identity-verified-on-any-successful-get.md](./313-p2-b09-u45-identity-verified-on-any-successful-get.md) |
| 314 | P2 | `B09-U46` | Sidebar collapse localStorage inverted | [314-p2-b09-u46-sidebar-collapse-localstorage-inverted.md](./314-p2-b09-u46-sidebar-collapse-localstorage-inverted.md) |
| 315 | P2 | `B09-U47` | Credit-note rows open a tax-invoice cancel panel | [315-p2-b09-u47-credit-note-rows-open-a-tax-invoice-cancel-panel.md](./315-p2-b09-u47-credit-note-rows-open-a-tax-invoice-cancel-panel.md) |
| 316 | P2 | `B09-U48` | QR via qrserver.com | [316-p2-b09-u48-qr-via-qrserver-com.md](./316-p2-b09-u48-qr-via-qrserver-com.md) |
| 317 | P2 | `B09-U49` | Create workspace in the switcher for every role | [317-p2-b09-u49-create-workspace-in-the-switcher-for-every-role.md](./317-p2-b09-u49-create-workspace-in-the-switcher-for-every-role.md) |
| 318 | P2 | `B09-U50` | No pending invites UI | [318-p2-b09-u50-no-pending-invites-ui.md](./318-p2-b09-u50-no-pending-invites-ui.md) |
| 319 | P2 | `B09-U51` | Community leftover fulfillment still filtered, labels still exist | [319-p2-b09-u51-community-leftover-fulfillment-still-filtered-labels-still-exist.md](./319-p2-b09-u51-community-leftover-fulfillment-still-filtered-labels-still-exist.md) |
| 320 | P2 | `B09-U52` | `CommunityPortalView` dead | [320-p2-b09-u52-communityportalview-dead.md](./320-p2-b09-u52-communityportalview-dead.md) |
| 321 | P2 | `B09-U53` | Ops chat still `[MVP-HIDE]` | [321-p2-b09-u53-ops-chat-still-mvp-hide.md](./321-p2-b09-u53-ops-chat-still-mvp-hide.md) |
| 322 | P2 | `B09-U54` | Admin “wrong console” is silent | [322-p2-b09-u54-admin-wrong-console-is-silent.md](./322-p2-b09-u54-admin-wrong-console-is-silent.md) |
| 323 | P2 | `B09-U55` | Portal i18n Accept-Language prefers any `ms` tag even at low q | [323-p2-b09-u55-portal-i18n-accept-language-prefers-any-ms-tag-even-at-low-q.md](./323-p2-b09-u55-portal-i18n-accept-language-prefers-any-ms-tag-even-at-low-q.md) |
| 324 | P2 | `B09-U56` | AppOptions default ClientUrl 3020 | [324-p2-b09-u56-appoptions-default-clienturl-3020.md](./324-p2-b09-u56-appoptions-default-clienturl-3020.md) |
| 325 | P2 | `B09-U57` | Zero tests in ops and admin | [325-p2-b09-u57-zero-tests-in-ops-and-admin.md](./325-p2-b09-u57-zero-tests-in-ops-and-admin.md) |
| 326 | P2 | `B09-U58` | Buttons that POST routes that exist but 403 | [326-p2-b09-u58-buttons-that-post-routes-that-exist-but-403.md](./326-p2-b09-u58-buttons-that-post-routes-that-exist-but-403.md) |
| 327 | P2 | `B10-X25` | Architecture tests allow the leak they were written to prevent | [327-p2-b10-x25-architecture-tests-allow-the-leak-they-were-written-to-prevent.md](./327-p2-b10-x25-architecture-tests-allow-the-leak-they-were-written-to-prevent.md) |
| 328 | P2 | `B10-X26` | Tests that pin bugs, tautologies, or never run | [328-p2-b10-x26-tests-that-pin-bugs-tautologies-or-never-run.md](./328-p2-b10-x26-tests-that-pin-bugs-tautologies-or-never-run.md) |
| 329 | P2 | `B10-X27` | `IAuditRecorder?` optional constructors fail open in any host that forgets the registration | [329-p2-b10-x27-iauditrecorder-optional-constructors-fail-open-in-any-host-that.md](./329-p2-b10-x27-iauditrecorder-optional-constructors-fail-open-in-any-host-that.md) |
| 330 | P2 | `B10-X28` | Honesty / docs residuals after `cbe17c2` | [330-p2-b10-x28-honesty-docs-residuals-after-cbe17c2.md](./330-p2-b10-x28-honesty-docs-residuals-after-cbe17c2.md) |
| 331 | P2 | `B10-X29` | Pre-dunning SQL excludes `TRIALING` (comms hole, not a 02 claim bug) | [331-p2-b10-x29-pre-dunning-sql-excludes-trialing-comms-hole-not-a-02-claim-bug.md](./331-p2-b10-x29-pre-dunning-sql-excludes-trialing-comms-hole-not-a-02-claim-bug.md) |
| 332 | P2 | `B10-X30` | Outbox publisher holds SKIP LOCKED rows while running all in-process handlers | [332-p2-b10-x30-outbox-publisher-holds-skip-locked-rows-while-running-all-in-pro.md](./332-p2-b10-x30-outbox-publisher-holds-skip-locked-rows-while-running-all-in-pro.md) |
| 333 | P2 | `B10-X31` | `DatabaseJobTrigger` is a single process-wide TCS | [333-p2-b10-x31-databasejobtrigger-is-a-single-process-wide-tcs.md](./333-p2-b10-x31-databasejobtrigger-is-a-single-process-wide-tcs.md) |
| 334 | P2 | `B10-X32` | Clock: invoice reminder UTC date vs `DueAt` | [334-p2-b10-x32-clock-invoice-reminder-utc-date-vs-dueat.md](./334-p2-b10-x32-clock-invoice-reminder-utc-date-vs-dueat.md) |
