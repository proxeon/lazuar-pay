import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/commerce/models.tsp";
import "./modules/commerce/admin-routes.tsp";
import "./modules/commerce/public-routes.tsp";
import "./modules/community/models.tsp";

using TypeSpec.Http;

@service({
  title: "Lazuar Commerce API",
  description: "Universal checkout engine, products, and subscription lifecycle management.",
})
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
