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
@doc("Universal checkout engine, products, subscription lifecycle, and public portal routes. Admin routes require human JWT + OrgAdmin; public checkout/portal routes are unauthenticated. Prefer workspace outbound webhooks for subscription lifecycle (see Developers hub Event catalog).")
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
