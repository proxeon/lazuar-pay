import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";
import "./common/models.tsp";
import "./modules/ops/models.tsp";
import "./modules/ops/routes.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Lazuar Ops API (Internal)" })
@info(#{ version: "1.0.0" })
@doc("Internal operator console surfaces. Not intended for external integrators.")
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
