import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Payments;

/**
 * M2M cashier: ad-hoc amount checkouts for any integrator app.
 * Auth: Authorization: Bearer sk_test_|sk_live_ with payments.checkouts:write|read.
 * Distinct from Commerce public product checkout (/public/commerce/*).
 *
 * Paths intentionally have no trailing slash (OpenAPI key /clients: `/integrations/payments/checkouts`).
 */
@route("/integrations/payments")
@doc("Integration (M2M) payment checkouts — ad-hoc amount + metadata. Requires scoped API key.")
interface IntegrationCheckoutOperations {
  /**
   * Create a hosted checkout session and return redirect URL.
   * Requires scope payments.checkouts:write.
   * Prefer Idempotency-Key header; body.idempotency_key is also accepted.
   */
  @post
  @route("/checkouts")
  @doc("Create M2M checkout. Workspace resolved from API key tenant.")
  createCheckout(
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: CreateIntegrationCheckoutRequestDto,
  ):
    | IntegrationCheckoutResponseDto
    | LazuarApi.Core.ProblemDetailsResponse;

  /**
   * Poll checkout status by id.
   * Requires scope payments.checkouts:read (write also implies read in product policy).
   */
  @get
  @route("/checkouts/{checkoutId}")
  @doc("Get integration checkout session by id (tenant-bound).")
  getCheckout(@path checkoutId: string):
    | IntegrationCheckoutResponseDto
    | LazuarApi.Core.ProblemDetailsResponse;
}
