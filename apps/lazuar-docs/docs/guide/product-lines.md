# Product lines

Do not mix these. Wrong product = wrong auth, wrong events, wrong fulfillment model.

| Product | What it is | Primary path |
|---------|------------|--------------|
| **Payments (cashier)** | Ad-hoc **amount + metadata** → gateway host page → `payment.*` webhooks | `POST /api/v1/integrations/payments/checkouts` |
| **Commerce** | Hub-native **products**, public buy links, subscription lifecycle | `/public/commerce/*` + `subscription.*` / `order.completed` |
| **LHDN** | Malaysian e-invoice | `/lhdn/*` + `invoice.*` |
| **Aura Plan / Paddle** | Salon SaaS subscription (MoR) | **Not Hub** — lives in Aura |

## When to use Payments cashier

- Variable amounts (deposits, invoices, one-off balances)
- Domain objects live in **your** database (`order_id`, `booking_id`, …)
- You only need “hosted pay + webhook when paid”

## When to use Commerce

- You want Hub to own product catalog, coupons, subscription states
- Public checkout links for **Hub** sellables
- Dunning / portal flows tied to Commerce subscriptions

## Email provider note

Email / Resend configuration may gate **Commerce** product activation in some flows.  
It does **not** block **M2M Payments** checkouts.

## Aura SaaS billing

Salon paying for Aura Pro (RM 149 / 1,490) uses **Paddle**, not Hub Payments. Never route that through Billplz BYOK on Hub.
