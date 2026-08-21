import "@typespec/http";
import "@typespec/openapi";

using Http;
using OpenAPI;

/** Focused Pay HTTP contract. Not packages/api-spec. Checkout is a fixture (open session), not a charge. */
@service(#{ title: "Lazuar Pay" })
@info(#{ version: "0.1.0" })
@server("http://localhost:8081", "Local focused Pay host")
namespace LazuarPay;

model HealthResponse {
  status: string;
}

model WhoamiTenant {
  id: string;
  slug?: string;
  name?: string;
  role?: string;
  status?: string;
}

model WhoamiResponse {
  user_id: string;
  email?: string;
  is_platform_admin: boolean;
  active_org_id?: string;
  tenants: WhoamiTenant[];
}

model OrgReadyResponse {
  org_id: string;
  ready: boolean;
}

model CreateCheckoutRequest {
  org_id: string;
  amount: decimal;
  currency?: string;
  success_url?: string;
  cancel_url?: string;
  idempotency_key?: string;
}

model CheckoutSession {
  id: string;
  org_id: string;
  amount: decimal;
  currency: string;
  status: string;
  success_url?: string;
  cancel_url?: string;
  created_at: utcDateTime;
}

@route("/v1")
@tag("Health")
interface Health {
  /** Process liveness for the focused Pay host. */
  @get
  @route("/health")
  check(): HealthResponse;
}

@route("/v1")
@tag("Session")
interface Session {
  /** Pay projection of One GET /me. Requires Bearer. Not Hub /one/auth/me. */
  @get
  @route("/whoami")
  whoami(): WhoamiResponse;
}

@route("/v1")
@tag("Orgs")
interface Orgs {
  /** Dummy admin: path org id is One tenant id; Pay calls authz/check member. */
  @get
  @route("/orgs/{orgId}/ready")
  ready(@path orgId: string): OrgReadyResponse;
}

@route("/v1")
@tag("Checkouts")
interface Checkouts {
  /** Merchant creates a fixture checkout. org_id is the One tenant id. Requires Bearer + member. */
  @post
  @route("/checkouts")
  create(
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: CreateCheckoutRequest,
  ): CheckoutSession;

  /** Merchant reads a checkout they are a member of. */
  @get
  @route("/checkouts/{id}")
  get(@path id: string): CheckoutSession;
}
