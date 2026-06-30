import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";
import "./common/models.tsp";
import "./modules/ops/models.tsp";
import "./modules/ops/routes.tsp";
import "./modules/billing/models.tsp";
import "./modules/billing/routes.tsp";

using TypeSpec.Http;

@service(#{ title: "Lazuar Ops API" })
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
