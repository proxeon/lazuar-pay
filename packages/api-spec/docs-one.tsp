import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";
import "./common/models.tsp";
import "./modules/one/models.tsp";
import "./modules/one/routes.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Lazuar Platform API (Core)" })
@info(#{ version: "1.0.0" })
@doc("Global identity, workspace provisioning, and platform entitlements. Machine-key introspect for Payments is GET /integrations/payments/me (Payments product, draft P00), not /one/auth/me.")
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
