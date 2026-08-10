# Aura as a reference client

[Aura](https://github.com/proxeon/aura) is the first full product using Hub Payments. Use it as a **pattern**, not a requirement.

## Mapping

| Aura concept | Hub concept |
|--------------|-------------|
| Organization (salon) | Workspace |
| `HubWorkspaceId` | Workspace id |
| Encrypted `sk_*` | Machine API key |
| `HubWebhookSecret` | Outbound signing secret |
| Mode `hub` / `legacy` / `dual` | Dual-run during migration |
| Booking / gift / pass | Your domain objects |

## Aura-side pieces (names)

| Piece | Role |
|-------|------|
| `IHubPaymentsClient` | HTTP to Hub provision + checkouts |
| Connect API (`hub-connect`) | Owner provisions + stores mapping |
| `POST /api/v1/webhooks/hub/payments` | Verifies Hub signature → completion ports |
| Metadata | `type`, `booking_id`, `gift_card_id`, `payment_type`, `aura_org_id`, … |

## What you should copy

- Server-only keys  
- Opaque metadata for your ids  
- Webhook-first fulfillment  
- Least-privilege scopes  
- Dual-run until canaries prove safety  

## What you should not copy

- Beauty booking domain into Hub  
- Storing Billplz secrets in your app long-term (if Hub is cashier)  
- Treating Paddle SaaS as Hub Payments  

## Dual-run lesson

Aura still ships legacy Billplz/Stripe adapters for `legacy` mode. That is **migration insurance**, not the end state. New apps can start Hub-only if they never had in-app gateways.

## Further reading

- Aura `idea/021-payment/` and `idea/022-remaining/` (internal product notes)  
- This site: [Payments cashier](/integrations/payments-cashier)  
- [Architecture: who does what](/guide/architecture-who-does-what)  
- [Hub vs DIY](/integrations/hub-vs-diy) (dual-run is migration insurance only)  
- [Second-app checklist](/integrations/second-app-checklist)  
