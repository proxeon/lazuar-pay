// packages/api-spec/modules/auth/routes.tsp
import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Auth;

@route("/platform/auth")
interface AuthOperations {
  @post
  @route("/login")
  login(
    @body body: LoginRequest,
  ): LoginResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/me")
  getMe(): AuthUser | LazuarApi.Core.ProblemDetailsResponse;
}
