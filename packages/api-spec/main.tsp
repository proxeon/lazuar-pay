import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";

import "./modules/one/models.tsp";
import "./modules/one/routes.tsp";

import "./modules/messaging/models.tsp";

// Hidden: Community module
// import "./modules/community/models.tsp";
// import "./modules/community/admin-routes.tsp";
// import "./modules/community/public-routes.tsp";

import "./modules/ops/models.tsp";
import "./modules/ops/routes.tsp";

import "./modules/commerce/models.tsp";
import "./modules/commerce/admin-routes.tsp";
import "./modules/commerce/public-routes.tsp";

import "./modules/communications/models.tsp";
import "./modules/communications/admin-routes.tsp";

import "./modules/billing/models.tsp";
import "./modules/billing/routes.tsp";

import "./modules/lhdn/models.tsp";
import "./modules/lhdn/routes.tsp";

import "./modules/crm/models.tsp";

// Hidden: Vault module
// import "./modules/vault/models.tsp";
// import "./modules/vault/admin-routes.tsp";
// import "./modules/vault/public-routes.tsp";

import "./modules/platform/routes.tsp";

using TypeSpec.Http;

@service(#{ title: "Lazuar Platform API" })
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;
