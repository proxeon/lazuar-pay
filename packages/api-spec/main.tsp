// packages/api-spec/main.tsp
import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

// 1. Core/Shared Types
import "./common/models.tsp";

// 2. Auth Module
import "./modules/auth/models.tsp";
import "./modules/auth/routes.tsp";

// 3. Messaging Module
import "./modules/messaging/models.tsp";

// 4. Community Module
import "./modules/community/models.tsp";
import "./modules/community/admin-routes.tsp";
import "./modules/community/public-routes.tsp";

using TypeSpec.Http;

@service({
  title: "Lazuar Platform API",
})
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
