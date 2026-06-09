import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/community/models.tsp";
import "./modules/community/admin-routes.tsp";
import "./modules/community/public-routes.tsp";

using TypeSpec.Http;

@service({
  title: "Lazuar Community API",
  description: "Manage subscription plans, recurring billing, and automated reminders.",
})
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
