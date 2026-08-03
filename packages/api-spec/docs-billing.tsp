import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";
import "./common/models.tsp";
import "./modules/billing/models.tsp";
import "./modules/billing/routes.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Lazuar Billing API" })
@info(#{ version: "1.0.0" })
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
