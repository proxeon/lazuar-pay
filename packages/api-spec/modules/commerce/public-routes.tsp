import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Commerce;

@route("/public/commerce")
interface PublicCommerceOperations {
  @get
  @route("/{tenantSlug}/products/{slug}")
  getProductBySlug(@path tenantSlug: string, @path slug: string):
    | ProductDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/validate-coupon")
  validateCoupon(
    @path tenantSlug: string,
    @query code: string,
    @query product_slug: string,
  ): ValidateCouponResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/checkout")
  initiateCheckout(@body body: PublicCheckoutRequestDto):
    | CheckoutResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/portal")
  getPortalData(@path tenantSlug: string, @query token: string):
    | AggregatedPortalDataResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/{tenantSlug}/portal/magic-link")
  requestMagicLink(@path tenantSlug: string, @body body: MagicLinkRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/{tenantSlug}/portal/cancel")
  cancelPortalSubscription(
    @path tenantSlug: string,
    @query token: string,
    @body body: CancelPortalRequest,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/portal/billing-link")
  getBillingLink(@path tenantSlug: string, @query token: string):
    | BillingLinkResponseDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/checkout/{subId}/status")
  getCheckoutStatus(@path subId: string):
    | CheckoutStatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/custom-checkouts/{sessionId}")
  getCustomCheckout(@path tenantSlug: string, @path sessionId: string):
    | CustomCheckoutDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/checkout/{subId}/arrears")
  getArrearsSummary(@path subId: string):
    | ArrearsSummaryDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/checkout/{subId}/update-payment")
  updatePayment(@path subId: string):
    | CheckoutResponse
    | LazuarApi.Core.ProblemDetailsResponse;
}
