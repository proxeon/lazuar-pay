import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.One;

@route("/one")
interface OneOperations {
  // --- Auth ---
  @post
  @route("/auth/login")
  login(
    @body body: LoginRequest,
  ): LoginResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/logout")
  logout(
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/auth/me")
  getMe(): AuthUser | LazuarApi.Core.ProblemDetailsResponse;

  // --- Entitlements ---
  @useAuth(BearerAuth)
  @get
  @route("/me/entitlements")
  getMyEntitlements(): EntitlementDto[] | LazuarApi.Core.ProblemDetailsResponse;

  // --- Workspaces ---
  @useAuth(BearerAuth)
  @post
  @route("/workspaces")
  createWorkspace(
    @body body: CreateWorkspaceRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;
}
