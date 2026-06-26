import "@typespec/http";
import "../../common/models.tsp";
import "../community/models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Platform;

@useAuth(BearerAuth)
@route("/platform")
interface PlatformOperations {
  @summary("Get the system payment gateway configuration. Requires SUPER_ADMIN role.")
  @get
  @route("/payment-config")
  getPaymentConfig(
  ): LazuarApi.Community.PaymentConfigDto | LazuarApi.Core.ProblemDetailsResponse;

  @summary("Save the system payment gateway configuration. Requires SUPER_ADMIN role.")
  @put
  @route("/payment-config")
  savePaymentConfig(
    @body body: LazuarApi.Community.SavePaymentConfigRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
