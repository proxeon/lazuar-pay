import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Community;

@route("/public/community")
interface PublicCommunityOperations {
  @get
  @route("/{tenantSlug}/plans")
  getPublicPlans(
    @path tenantSlug: string,
  ): CommunityPlanDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/plans/{slug}")
  getPlanBySlug(
    @path tenantSlug: string,
    @path slug: string,
  ): CommunityPlanDto | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/validate-coupon")
  validateCoupon(
    @path tenantSlug: string,
    @query code: string,
    @query plan_slug: string,
  ): ValidateCouponResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/checkout")
  initiateCheckout(
    @body body: PublicCheckoutRequestDto,
  ): CheckoutResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/{tenantSlug}/portal/magic-link")
  requestMagicLink(
    @path tenantSlug: string,
    @body body: MagicLinkRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/portal")
  getPortalData(
    @path tenantSlug: string,
    @query token: string,
  ): PortalDataResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/{tenantSlug}/portal/cancel")
  cancelPortalSubscription(
    @path tenantSlug: string,
    @query token: string,
    @body body: CancelPortalRequest,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/portal/billing-link")
  getBillingLink(
    @path tenantSlug: string,
    @query token: string,
  ): BillingLinkResponseDto | LazuarApi.Core.ProblemDetailsResponse;
}
