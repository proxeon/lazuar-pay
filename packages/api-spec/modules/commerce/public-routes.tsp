import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";
import "../community/models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Commerce;

@route("/public/commerce")
interface PublicCommerceOperations {
  @get
  @route("/{tenantSlug}/products/{slug}")
  getProductBySlug(
    @path tenantSlug: string,
    @path slug: string,
  ): ProductDto | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/validate-coupon")
  validateCoupon(
    @path tenantSlug: string,
    @query code: string,
    @query product_slug: string,
  ): LazuarApi.Community.ValidateCouponResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/checkout")
  initiateCheckout(
    @body body: PublicCheckoutRequestDto,
  ): LazuarApi.Community.CheckoutResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/portal")
  getPortalData(
    @path tenantSlug: string,
    @query token: string,
  ): AggregatedPortalDataResponse | LazuarApi.Core.ProblemDetailsResponse;
}
