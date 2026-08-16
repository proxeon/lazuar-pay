import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Commerce;

@useAuth(BearerAuth)
@route("/integrations/commerce")
@doc("Machine-key Hub Commerce subscription admin. List/get/cancel only. Not Stripe Billing.")
interface IntegrationCommerceOperations {
  @get
  @route("/subscriptions")
  listIntegrationSubscriptions(
    @query page?: int32 = 1,
    @query limit?: int32 = 50,
    @query status?: string,
  ):
    | LazuarApi.Core.PaginatedResponse<CommerceSubscriptionDto>
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/subscriptions/{id}")
  getIntegrationSubscription(@path id: string):
    | CommerceSubscriptionDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/subscriptions/{id}/cancel")
  @doc("Immediate cancel (same as admin cancel without at_period_end). Already canceled → 400.")
  cancelIntegrationSubscription(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;
}
