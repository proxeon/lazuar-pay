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
  getPaymentConfigs():
    | PaymentConfigDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

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

  @get
  @route("/subscribers/export")
  @doc("CSV export of subscribers for the active workspace.")
  exportSubscribers(@query search?: string):
    | string
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/portal-link")
  @doc("Generate a Stripe Customer Portal link for a customer email (requires Stripe).")
  generateCustomerPortalLink(@body body: GenerateCustomerPortalRequestDto):
    | GenerateCustomerPortalResponseDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/cancel")
  @doc("Cancel now (default) or schedule cancel at period end.")
  cancelSubscriber(@path id: string, @body body?: CancelSubscriberRequest):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/keep")
  @doc("Undo a scheduled period-end cancel.")
  keepSubscriber(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/record-payment")
  recordSubscriberPayment(@path id: string, @body body: RecordPaymentRequestDto):
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

  @post
  @route("/subscribers/{id}/anonymize")
  @doc("PDPA wipe: anonymize the buyer on this subscription. Cancels their subscriptions and stops email. Idempotent.")
  anonymizeSubscriber(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/transactions/export")
  @doc("UTF-8 CSV of transaction logs. Default last 31 days. Cap 50000. Match external_reference to the gateway id. Fees are Hub-recorded.")
  exportTransactions(
    @query from?: utcDateTime,
    @query to?: utcDateTime,
    @query status?: string,
  ): string | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/transactions")
  getTransactions(
    @query search?: string,
    @query page?: int32 = 1,
    @query limit?: int32 = 50,
    @query status?: string,
    @query payment_method?: string,
    @query subscription_id?: string,
    @query gateway_name?: string,
  ):
    | LazuarApi.Core.PaginatedResponse<TransactionLogDto>
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/transactions/{id}/refund")
  refundTransaction(@path id: string, @body body?: RecordRefundRequestDto):
    | LazuarApi.Core.StatusResponse
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

  @get
  @route("/custom-checkouts")
  getCustomCheckouts(@query page?: int32 = 1, @query limit?: int32 = 50):
    | LazuarApi.Core.PaginatedResponse<CustomCheckoutDto>
    | LazuarApi.Core.ProblemDetailsResponse;
}
