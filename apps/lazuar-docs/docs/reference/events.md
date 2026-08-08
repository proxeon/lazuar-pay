# Event catalog

## Payments (M2M / integration checkouts)

| Event | When | Action in your app |
|-------|------|--------------------|
| `payment.completed` | Gateway paid / captured | Unlock domain once |
| `payment.failed` | Gateway reported failure | Do not unlock; optional UX |
| `payment.refunded` | Refund completed | Maturing — check current Hub version |

Payload typically includes: event id/type, checkout id, gateway, gateway transaction id, amount, currency, status, metadata.

Exact JSON evolves additively; see OpenAPI when published.

## Commerce (separate)

| Event family | Examples |
|--------------|----------|
| Orders | `order.completed` |
| Subscriptions | `subscription.activated`, `subscription.past_due`, … |
| Payment links | `payment_link.paid` |

Use Commerce events only if you sell **Hub Commerce** products.

## LHDN

Invoice lifecycle events under LHDN product — not Payments cashier.
