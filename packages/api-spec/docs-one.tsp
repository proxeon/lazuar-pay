import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";
import "./common/models.tsp";
import "./modules/one/models.tsp";
import "./modules/one/routes.tsp";
import "./modules/billing/models.tsp";
import "./modules/billing/routes.tsp";

using TypeSpec.Http;

@service({
  title: "Lazuar Platform API (Core)",
  description: "Global identity, workspace provisioning, and platform entitlements.",
})
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
