import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/vault/models.tsp";
import "./modules/vault/admin-routes.tsp";

using TypeSpec.Http;

@service(#{ title: "Lazuar Vault API" })
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
