// packages/api-spec/modules/one/routes.tsp
import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.One;

@route("/one")
interface OneOperations {
  @post
  @route("/public/register")
  registerPublicUser(
    @body body: PublicRegisterRequestDto,
  ): LoginResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/login")
  login(
    @body body: LoginRequest,
  ): LoginResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/logout")
  logout(
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/forgot-password")
  forgotPassword(
    @body body: ForgotPasswordRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/reset-password")
  resetPassword(
    @body body: ResetPasswordRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/verify-email")
  verifyEmail(
    @body body: VerifyEmailRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/resend-verification")
  resendVerification(
    @body body: ResendVerificationRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/auth/me")
  getMe(): AuthUser | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @put
  @route("/me/profile")
  updateProfile(
    @body body: UpdateProfileRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @put
  @route("/me/security/password")
  changePassword(
    @body body: ChangePasswordRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/me/entitlements")
  getMyEntitlements(): EntitlementDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/me/invitations")
  getMyInvitations(
  ): MyPendingInvitationDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/me/access-requests")
  requestAppAccess(
    @body body: CreateAppAccessRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/access-requests")
  getAppAccessRequests(
  ): AppAccessRequestDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/access-requests/{id}/approve")
  approveAppAccessRequest(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/access-requests/{id}/reject")
  rejectAppAccessRequest(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces")
  getWorkspaces(): WorkspaceDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/workspaces")
  createWorkspace(
    @body body: CreateWorkspaceRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @put
  @route("/workspaces/{id}")
  updateWorkspace(
    @path id: string,
    @body body: UpdateWorkspaceRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @delete
  @route("/workspaces/{id}")
  deleteWorkspace(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces/{id}/members")
  getWorkspaceMembers(
    @path id: string,
  ): WorkspaceMemberDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/workspaces/{id}/invites")
  inviteToWorkspace(
    @path id: string,
    @body body: CreateWorkspaceInvitationDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces/{id}/invites")
  getWorkspaceInvitations(
    @path id: string,
  ): WorkspaceInvitationDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @delete
  @route("/workspaces/{id}/invites/{inviteId}")
  revokeWorkspaceInvitation(
    @path id: string,
    @path inviteId: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @delete
  @route("/workspaces/{id}/members/{userId}")
  removeWorkspaceMember(
    @path id: string,
    @path userId: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/workspaces/invites/accept")
  acceptWorkspaceInvitation(
    @body body: AcceptWorkspaceInvitationDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces/{id}/apps")
  getWorkspaceApps(
    @path id: string,
  ): WorkspaceAppDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/workspaces/{id}/apps/{appId}")
  toggleAppEntitlement(
    @path id: string,
    @path appId: string,
    @body body: ToggleAppEntitlementRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces/{id}/lhdn-config")
  getWorkspaceLhdnConfig(
    @path id: string,
  ): WorkspaceLhdnConfigDto | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @put
  @route("/workspaces/{id}/lhdn-config")
  updateWorkspaceLhdnConfig(
    @path id: string,
    @body body: UpdateWorkspaceLhdnConfigRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
