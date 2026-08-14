import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/commerce/models.tsp";
import "./modules/commerce/admin-routes.tsp";
import "./modules/commerce/public-routes.tsp";
import "./modules/communications/models.tsp";
import "./modules/communications/admin-routes.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Lazuar Commerce API" })
@info(#{ version: "1.0.0" })
@doc("""
Lazuar Commerce — integrator v1 surface (D5: webhooks + public checkout first).

**Integrator-facing (no M2M product admin):**
- Public routes under `/public/commerce/*` — unauthenticated buy links, checkout initiation,
  portal magic-token access, arrears/update-payment, and custom checkout sessions.
- Workspace outbound webhooks for unlock/revoke. Frozen SaaS names (no subscription.updated):
  subscription.activated|resumed|past_due|canceled|suspended, plus order.completed and payment_link.paid.
  Envelope + SubscriptionWebhookData: modules/commerce/models/webhooks.tsp.
  Ignore on the SaaS URL: payment.*, order.completed, payment_link.paid, invoice.*, unknown types.

**Console-only (not for machine integrators):**
- `/admin/commerce/*` requires human JWT + OrgAdmin for products, subscriptions, campaigns, etc.
  Full key-authenticated M2M admin is out of scope for v1.
""")
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
