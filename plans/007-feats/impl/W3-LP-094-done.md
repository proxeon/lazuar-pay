# W3-LP-094 — done

Commerce GMV chargebacks are first-class `commerce.Disputes` rows. Billing still owns `utility_credit_topup` / `platform_saas_fee`. Replay of the same `GatewayTransactionId` upserts one OPEN row. Matching transaction log is stamped `DISPUTED`. Subscription is not canceled. Ledger contra is the existing Billing `GatewayRefundCompleted` consumer (event id = dispute id).

## Files

- `CommerceDispute` + migration `20260820140000_AddCommerceDisputes`
- `CommerceGatewayDisputeCreatedHandler`
- `GET /admin/commerce/disputes`
- Ops Commerce → Disputes

## Tests

- Replay one row; utility no-op; sub not canceled

Not committed. Not pushed.

Tracker `LP-094` **P → Y**.
