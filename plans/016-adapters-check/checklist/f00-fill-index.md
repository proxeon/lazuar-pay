# F00 — Fill-tests index (015 ticks that had no method)

**Track:** Fill · **Depends:** S10–S18 first (do not clone a weak paid test)  
**Analysis:** [`../09-tests-inventory.md`](../09-tests-inventory.md) §8 / §10  
**IDs:** C32, B28, X23, R25, P23, H14, P20, P24  
**Goal:** One phase file per remaining `[Test]`. Method names are locked. Pointers to G/E/D/J when the method already lives there.

**Do not start F00 as a dump before I10 and G14.**

| ID | File | Method |
|----|------|--------|
| fs10 | [fs10-missing-stripe-signature.md](./fs10-missing-stripe-signature.md) | `Missing_stripe_signature_header_is_400` |
| fs11 | [fs11-amount-mismatch.md](./fs11-amount-mismatch.md) | **same as G15** |
| fs12 | [fs12-currency-mismatch.md](./fs12-currency-mismatch.md) | `Currency_mismatch_does_not_mint_receipt` |
| fs13 | [fs13-unknown-event-ignored.md](./fs13-unknown-event-ignored.md) | `Unknown_event_type_is_ignored` |
| fs14 | [fs14-rail-not-configured-body.md](./fs14-rail-not-configured-body.md) | `Rail_not_configured_is_400_when_body_present` |
| fs15 | [fs15-production-whsec.md](./fs15-production-whsec.md) | **same as E12** |
| fs16 | [fs16-fulfill-throw.md](./fs16-fulfill-throw.md) | **same as G12/G13** |
| fs17 | [fs17-stripe-whitespace-body.md](./fs17-stripe-whitespace-body.md) | `Stripe_whitespace_webhook_is_400` |
| fs18 | [fs18-stripe-missing-currency.md](./fs18-stripe-missing-currency.md) | **same as D16** |
| fc10 | [fc10-chip-bad-signature.md](./fc10-chip-bad-signature.md) | `Chip_bad_signature_is_400` |
| fc11 | [fc11-chip-missing-signature.md](./fc11-chip-missing-signature.md) | `Chip_missing_signature_header_is_400` |
| fc12 | [fc12-chip-missing-currency.md](./fc12-chip-missing-currency.md) | `Chip_missing_currency_does_not_pay` |
| fc13 | [fc13-chip-payment-failure.md](./fc13-chip-payment-failure.md) | `Chip_payment_failure_is_ignored` |
| fc14 | [fc14-chip-failure-then-paid.md](./fc14-chip-failure-then-paid.md) | `Chip_failure_then_paid_still_mints_one_receipt` |
| fc15 | [fc15-chip-cross-org.md](./fc15-chip-cross-org.md) | `Chip_cross_org_checkout_is_400` |
| fc16 | [fc16-chip-placeholder-email.md](./fc16-chip-placeholder-email.md) | `Chip_placeholder_email_is_400` |
| fc17 | [fc17-chip-start-without-brand.md](./fc17-chip-start-without-brand.md) | `Chip_start_without_brand_id_is_503` |
| fc18 | [fc18-chip-amount-mismatch.md](./fc18-chip-amount-mismatch.md) | `Chip_amount_mismatch_does_not_pay` |
| fb10 | [fb10-billplz-empty-body.md](./fb10-billplz-empty-body.md) | `Billplz_empty_body_400` |
| fb11 | [fb11-billplz-bad-hmac.md](./fb11-billplz-bad-hmac.md) | `Billplz_bad_hmac_is_400` |
| fb12 | [fb12-billplz-hmac-with-extra.md](./fb12-billplz-hmac-with-extra.md) | `Billplz_hmac_with_extra_fields_paid` |
| fb13 | [fb13-billplz-hmac-without-extra.md](./fb13-billplz-hmac-without-extra.md) | `Billplz_hmac_without_extra_fields_paid` |
| fb14 | [fb14-billplz-unpaid.md](./fb14-billplz-unpaid.md) | `Billplz_unpaid_is_ignored` |
| fb15 | [fb15-billplz-localhost.md](./fb15-billplz-localhost.md) | `Billplz_localhost_callback_start_is_400_without_psp_http` |
| fb16 | [fb16-billplz-missing-email.md](./fb16-billplz-missing-email.md) | `Billplz_start_without_email_is_400` |
| fb17 | [fb17-billplz-placeholder-email.md](./fb17-billplz-placeholder-email.md) | `Billplz_placeholder_email_is_400` |
| fb18 | [fb18-billplz-put-collection.md](./fb18-billplz-put-collection.md) | `Billplz_put_requires_collection_id` |
| fb19 | [fb19-billplz-put-environment.md](./fb19-billplz-put-environment.md) | `Billplz_put_requires_environment` |
| fb20 | [fb20-billplz-start-without-collection.md](./fb20-billplz-start-without-collection.md) | `Billplz_start_without_collection_is_503` |
| fb21 | [fb21-billplz-cross-org.md](./fb21-billplz-cross-org.md) | `Billplz_cross_org_is_400` |
| fb22 | [fb22-billplz-amount-mismatch.md](./fb22-billplz-amount-mismatch.md) | `Billplz_amount_mismatch_does_not_pay` |
| fb23 | [fb23-billplz-join-reference-1.md](./fb23-billplz-join-reference-1.md) | `Billplz_join_via_reference_1_when_query_missing` |
| fb24 | [fb24-billplz-live-host.md](./fb24-billplz-live-host.md) | `Billplz_live_environment_hits_www_host` |
| fb25 | [fb25-billplz-missing-currency.md](./fb25-billplz-missing-currency.md) | after D15 |
| fx10 | [fx10-xendit-empty-body.md](./fx10-xendit-empty-body.md) | `Xendit_empty_body_400` |
| fx11 | [fx11-xendit-bad-token.md](./fx11-xendit-bad-token.md) | `Xendit_bad_callback_token_is_400` |
| fx12 | [fx12-xendit-missing-token.md](./fx12-xendit-missing-token.md) | `Xendit_missing_callback_token_is_400` |
| fx13 | [fx13-xendit-expired.md](./fx13-xendit-expired.md) | `Xendit_expired_is_ignored` |
| fx14 | [fx14-xendit-pending.md](./fx14-xendit-pending.md) | `Xendit_pending_is_ignored` |
| fx15 | [fx15-xendit-paid-replay.md](./fx15-xendit-paid-replay.md) | `Xendit_paid_replay_is_duplicate` |
| fx16 | [fx16-xendit-missing-email.md](./fx16-xendit-missing-email.md) | `Xendit_start_without_email_is_400` |
| fx17 | [fx17-xendit-placeholder-email.md](./fx17-xendit-placeholder-email.md) | `Xendit_placeholder_email_is_400` |
| fx18 | [fx18-xendit-missing-currency.md](./fx18-xendit-missing-currency.md) | `Xendit_missing_currency_does_not_pay` |
| fx19 | [fx19-xendit-cross-org.md](./fx19-xendit-cross-org.md) | `Xendit_cross_org_is_400` |
| fx20 | [fx20-xendit-amount-mismatch.md](./fx20-xendit-amount-mismatch.md) | `Xendit_amount_mismatch_does_not_pay` |
| fr10 | [fr10-razorpay-empty-body.md](./fr10-razorpay-empty-body.md) | `Razorpay_empty_body_400` |
| fr11 | [fr11-razorpay-bad-signature.md](./fr11-razorpay-bad-signature.md) | `Razorpay_bad_signature_is_400` |
| fr12 | [fr12-razorpay-missing-signature.md](./fr12-razorpay-missing-signature.md) | `Razorpay_missing_signature_is_400` |
| fr13 | [fr13-razorpay-payment-failed.md](./fr13-razorpay-payment-failed.md) | `Razorpay_payment_failed_is_ignored` |
| fr14 | [fr14-razorpay-failed-then-captured.md](./fr14-razorpay-failed-then-captured.md) | `Razorpay_failed_then_captured_still_pays` |
| fr15 | [fr15-razorpay-replay.md](./fr15-razorpay-replay.md) | `Razorpay_captured_replay_is_duplicate` |
| fr16 | [fr16-razorpay-event-id-header.md](./fr16-razorpay-event-id-header.md) | `Razorpay_event_id_prefers_header` |
| fr17 | [fr17-razorpay-missing-email.md](./fr17-razorpay-missing-email.md) | `Razorpay_start_without_email_is_400` |
| fr18 | [fr18-razorpay-placeholder-email.md](./fr18-razorpay-placeholder-email.md) | `Razorpay_placeholder_email_is_400` |
| fr19 | [fr19-razorpay-put-colon.md](./fr19-razorpay-put-colon.md) | `Razorpay_put_requires_key_id_colon_secret` |
| fr20 | [fr20-razorpay-missing-currency.md](./fr20-razorpay-missing-currency.md) | `Razorpay_missing_currency_does_not_pay` |
| fr21 | [fr21-razorpay-cross-org.md](./fr21-razorpay-cross-org.md) | `Razorpay_cross_org_is_400` |
| fr22 | [fr22-razorpay-amount-mismatch.md](./fr22-razorpay-amount-mismatch.md) | `Razorpay_amount_mismatch_does_not_pay` |
| fr23 | [fr23-razorpay-without-notes.md](./fr23-razorpay-without-notes.md) | **same as J16** |
| fg10 | [fg10-member-get.md](./fg10-member-get.md) | `Member_can_get_gateway_metadata` |
| fg11 | [fg11-put-unknown.md](./fg11-put-unknown.md) | `Put_unknown_provider_is_400` |
| fg12 | [fg12-get-query-provider.md](./fg12-get-query-provider.md) | `Get_optional_provider_query_does_not_change_active` |
| fg13 | [fg13-get-unknown-query.md](./fg13-get-unknown-query.md) | `Get_unknown_provider_query_is_400` |
| fg14 | [fg14-put-chip-active.md](./fg14-put-chip-active.md) | `Put_chip_get_active_is_chip_not_stripe` |
| fp10 | [fp10-email-required-chip.md](./fp10-email-required-chip.md) | `Email_required_true_when_active_chip` |
| fp11 | [fp11-email-required-stripe.md](./fp11-email-required-stripe.md) | `Email_required_false_when_active_stripe` |
| fp12 | [fp12-start-without-rail.md](./fp12-start-without-rail.md) | `Start_without_rail_is_503` |
| fi10 | [fi10-isolation.md](./fi10-isolation.md) | **same as S17** |

Fixtures: `PayApiFactory`, owner One responder as in `RailTests`. No live PSP. No `Razorpay.Api`.
