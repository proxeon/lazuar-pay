import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Commerce;

@useAuth(BearerAuth)
@route("/admin/commerce")
interface AdminCommerceOperations {
  @get
  @route("/products")
  getProducts(): ProductDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/products/{id}")
  getProductById(@path id: string):
    | ProductDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/products")
  createProduct(@body body: CreateProductRequestDto):
    | LazuarApi.Core.IdResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/products/{id}")
  updateProduct(@path id: string, @body body: UpdateProductRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/products/{id}")
  archiveProduct(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/products/{id}/restore")
  restoreProduct(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/dunning-campaigns")
  getDunningCampaigns():
    | DunningCampaignDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/dunning-campaigns")
  createDunningCampaign(@body body: CreateDunningCampaignRequestDto):
    | LazuarApi.Core.IdResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/dunning-campaigns/defaults")
  generateDefaultDunningCampaigns():
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/dunning-campaigns/{id}")
  updateDunningCampaign(
    @path id: string,
    @body body: UpdateDunningCampaignRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/dunning-campaigns/{id}")
  deleteDunningCampaign(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/payment-config")
  getPaymentConfig(): PaymentConfigDto | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/payment-config")
  savePaymentConfig(@body body: SavePaymentConfigRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/subscribers")
  getSubscribers(
    @query page?: int32 = 1,
    @query limit?: int32 = 50,
    @query search?: string,
  ):
    | LazuarApi.Core.PaginatedResponse<CommerceSubscriptionDto>
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers")
  createSubscriber(@body body: CreateManualSubscriberDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/dunning/pause")
  pauseSubscriberDunning(@path id: string, @body body: PauseDunningRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/dunning/resume")
  resumeSubscriberDunning(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/transactions")
  getTransactions(
    @query search?: string,
    @query page?: int32 = 1,
    @query limit?: int32 = 50,
    @query status?: string,
    @query payment_method?: string,
  ):
    | LazuarApi.Core.PaginatedResponse<TransactionLogDto>
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/coupons")
  getCoupons(): CouponDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/coupons")
  createCoupon(@body body: CreateCouponRequestDto):
    | LazuarApi.Core.IdResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/coupons/{id}")
  updateCoupon(@path id: string, @body body: UpdateCouponRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/coupons/{id}")
  deleteCoupon(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/stats")
  getStats(): CommerceStatsDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/custom-checkouts")
  createCustomCheckout(@body body: CreateCustomCheckoutRequestDto):
    | LazuarApi.Core.IdResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/checkouts/{id}/mark-paid")
  markCheckoutAsPaidOffline(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;
}
