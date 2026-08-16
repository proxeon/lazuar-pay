import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/commerce/models.tsp";
import "./modules/commerce/admin-routes.tsp";
import "./modules/commerce/public-routes.tsp";
import "./modules/commerce/integration-routes.tsp";
import "./modules/communications/models.tsp";
import "./modules/communications/admin-routes.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Lazuar Commerce API" })
@info(#{ version: "1.0.0" })
@doc("""
Lazuar Commerce — integrator v1 surface (D5: webhooks + public checkout first).

**Integrator-facing:**
- Public routes under `/public/commerce/*` — unauthenticated buy links, checkout initiation,
  portal magic-token access, arrears/update-payment, and custom checkout sessions.
- Key-auth M2M **list / get / cancel** Hub subscriptions: `/integrations/commerce/subscriptions`.
  Scopes: `commerce.subscriptions:read` / `commerce.subscriptions:write`.
- Workspace outbound webhooks for unlock/revoke. Frozen SaaS names (no subscription.updated):
  subscription.activated|resumed|past_due|canceled|suspended, plus order.completed and payment_link.paid.

**Console-only:**
- `/admin/commerce/*` requires human JWT + OrgAdmin for products, enroll, refunds, campaigns.
  Product CRUD is not key-auth.
""")
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
