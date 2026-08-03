import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

import "./common/models.tsp";
import "./modules/one/models.tsp"; // platform API key DTOs (aliased by LHDN models)
import "./modules/lhdn/models.tsp";
import "./modules/lhdn/routes.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Lazuar LHDN API" })
@info(#{ version: "1.0.0" })
@doc("Malaysian e-Invoicing compliance API. Authenticate with Authorization: Bearer sk_test_… or sk_live_… (API keys from Ops → Developer → API Keys). Never embed user JWTs in ERP or machine clients. Include Idempotency-Key on POST /lhdn/documents.")
@server("https://hub.lazuar.com/api/v1", "Production server")
@server("http://localhost:8080/api/v1", "Local development server")
// Integration clients use Authorization: Bearer sk_test_|sk_live_… (documented as Bearer).
// ApiKeyAuth is also declared so Scalar “Try it” accepts a raw key in the Authorization header.
@useAuth(BearerAuth | ApiKeyAuth<ApiKeyLocation.header, "Authorization">)
namespace LazuarApi;
