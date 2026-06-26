import "@typespec/http";
import "../../common/models.tsp";
import "../community/models.tsp";
import "../one/models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Platform;

@route("/platform")
interface PlatformOperations {
  @summary("Authenticate as a superadmin and receive a dedicated platform cookie.")
  @post
  @route("/auth/login")
  login(
    @body body: LazuarApi.One.LoginRequest,
  ): LazuarApi.One.LoginResponse | LazuarApi.Core.ProblemDetailsResponse;

  @summary("Destroy the platform superadmin cookie.")
  @post
  @route("/auth/logout")
  logout(
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @summary("Get the currently authenticated superadmin's profile.")
  @get
  @route("/auth/me")
  getMe(): LazuarApi.One.AuthUser | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @summary("Get the system payment gateway configuration. Requires SUPER_ADMIN role.")
  @get
  @route("/payment-config")
  getPaymentConfig(
  ): LazuarApi.Community.PaymentConfigDto | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @summary("Save the system payment gateway configuration. Requires SUPER_ADMIN role.")
  @put
  @route("/payment-config")
  savePaymentConfig(
    @body body: LazuarApi.Community.SavePaymentConfigRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
