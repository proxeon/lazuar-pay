import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/lhdn/models.tsp";
import "./modules/lhdn/routes.tsp";

using TypeSpec.Http;

@service({
  title: "Lazuar LHDN API",
  description: "RESTful integration for Malaysian LHDN e-Invoicing compliance. Send JSON, we handle the UBL 2.1 XML and PKI signatures.",
})
@server("https://api.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
@useAuth(ApiKeyAuth<ApiKeyLocation.header, "Authorization"> | BearerAuth)
namespace LazuarApi;
