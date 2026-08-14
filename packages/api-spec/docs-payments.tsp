import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/payments/models.tsp";
import "./modules/payments/routes.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Lazuar Payments Integration API" })
@info(#{ version: "1.0.0" })
@doc("""
Lazuar Payments — M2M cashier (integration checkouts).

**Integrator-facing:**
- `POST/GET /integrations/payments/checkouts` — ad-hoc amount checkout for any server app.
- `GET /integrations/payments/me` — **DRAFT P00.31** (runtime P02). Introspect Bearer K1. Not implemented yet.
- Auth: `Authorization: Bearer sk_test_…|sk_live_…` with scopes
  `payments.checkouts:write` and/or `payments.checkouts:read`.
- Outbound webhooks: `payment.completed` / `payment.failed` with `X-Lazuar-Signature`.

**Not this product:**
- Commerce public product buy links (`/public/commerce/*`) — Hub catalog CaaS.
- LHDN e-invoice — separate product.
- Paddle SaaS (Aura Plan) — out of Hub Payments.

**Versioning:** Additive fields only on v1. Breaking changes require a new path prefix or major version.
See docs/payments-integration-quickstart.md and docs/api-versioning.md.
""")
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
// Integration clients use Authorization: Bearer sk_test_|sk_live_… (documented as Bearer).
// ApiKeyAuth is also declared so Scalar “Try it” accepts a raw key in the Authorization header.
@useAuth(BearerAuth | ApiKeyAuth<ApiKeyLocation.header, "Authorization">)
namespace LazuarApi;
