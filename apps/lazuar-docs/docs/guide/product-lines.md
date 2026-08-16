# Product lines

Do not mix these. Wrong product = wrong auth, wrong events, wrong fulfillment model.

| Product | What it is | Primary path |
|---------|------------|--------------|
| **Payments (cashier)** | Ad-hoc **amount + metadata** → gateway host page → `payment.*` webhooks | `POST /api/v1/integrations/payments/checkouts` |
| **Commerce** | Hub-native **products**, public buy links, subscription lifecycle | `/public/commerce/*` + `subscription.*` / `order.completed` |
| **LHDN** | Malaysian e-invoice | `/lhdn/*` + `invoice.*` |
| **Aura Plan / Paddle** | Salon SaaS subscription (MoR) | **Not Hub** — lives in Aura |

The table above is the SSoT. The decision tree mirrors it — no extra rules.

## Decision flowchart

```text
Need to take money or tax?
            |
            v
   What are you selling?
            |
   +--------+--------+------------------+------------------+
   |                 |                  |                  |
   v                 v                  v                  v
Ad-hoc amount     Hub-native        Malaysian          Your SaaS seat
from YOUR DB      product catalog   e-invoice only     for the platform
order/booking/    subscriptions /                      (e.g. Aura Pro)
invoice           coupons
   |                 |                  |                  |
   v                 v                  v                  v
Payments          Commerce           LHDN               Outside Hub
cashier M2M       public +           /lhdn/* +          Paddle / MoR
                  lifecycle          invoice.*          (not Billplz BYOK)
   |                 |                  |
   v                 v                  v
POST /integrations  /public/commerce/*  Domain rules
/payments/checkouts subscription.*      per tax product
events:             order.completed
payment.completed
payment.failed
   |
   v
Domain stays in your app
(M2M and Commerce events are NOT interchangeable)
```

**Summary:** Use Payments cashier when amounts and domain objects live in your app. Use Commerce when Hub owns catalog and subscription lifecycle. LHDN is e-invoice only. Platform SaaS seats (Aura Pro) stay on Paddle — never Billplz BYOK on Hub. Do not collapse M2M `payment.*` with Commerce `subscription.*`.

## When to use Payments cashier

- Variable amounts (deposits, invoices, one-off balances)
- Domain objects live in **your** database (`order_id`, `booking_id`, …)
- You only need “hosted pay + webhook when paid”

See [Payment flow](/integrations/payment-flow).

## When to use Commerce

- You want Hub to own product catalog, coupons, subscription states
- Public checkout links for **Hub** sellables
- Dunning / portal flows tied to Commerce subscriptions

Commerce subscriptions have two renewal modes. Stripe and CHIP Collect can vault and auto-debit. Billplz (and any `supports_off_session=false` rail) is **pay-link each cycle**: we email a new hosted bill; there is no silent charge.

## Email provider note

Email / Resend configuration may gate **Commerce** product activation in some flows.  
It does **not** block **M2M Payments** checkouts.

## Aura SaaS billing

Salon paying for Aura Pro (RM 149 / 1,490) uses **Paddle**, not Hub Payments. Never route that through Billplz BYOK on Hub.
