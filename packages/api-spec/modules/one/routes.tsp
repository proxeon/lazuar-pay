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
  registerPublicUser(@body body: PublicRegisterRequestDto):
    | LoginResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/login")
  login(@body body: LoginRequest):
    | LoginResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/logout")
  logout():
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/forgot-password")
  forgotPassword(@body body: ForgotPasswordRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/reset-password")
  resetPassword(@body body: ResetPasswordRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/verify-email")
  verifyEmail(@body body: VerifyEmailRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/auth/resend-verification")
  resendVerification(@body body: ResendVerificationRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/auth/me")
  getMe(): AuthUser | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @put
  @route("/me/profile")
  updateProfile(@body body: UpdateProfileRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @put
  @route("/me/security/password")
  changePassword(@body body: ChangePasswordRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/me/entitlements")
  getMyEntitlements(): EntitlementDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces")
  getWorkspaces(): WorkspaceDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/workspaces")
  createWorkspace(@body body: CreateWorkspaceRequestDto):
    | LazuarApi.Core.IdResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @put
  @route("/workspaces/{id}")
  updateWorkspace(@path id: string, @body body: UpdateWorkspaceRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @delete
  @route("/workspaces/{id}")
  deleteWorkspace(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces/{id}/members")
  getWorkspaceMembers(@path id: string):
    | WorkspaceMemberDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

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
  getWorkspaceInvitations(@path id: string):
    | WorkspaceInvitationDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @delete
  @route("/workspaces/{id}/invites/{inviteId}")
  revokeWorkspaceInvitation(@path id: string, @path inviteId: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @delete
  @route("/workspaces/{id}/members/{userId}")
  removeWorkspaceMember(@path id: string, @path userId: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/workspaces/invites/accept")
  acceptWorkspaceInvitation(@body body: AcceptWorkspaceInvitationDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces/{id}/apps")
  getWorkspaceApps(@path id: string):
    | WorkspaceAppDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/workspaces/{id}/apps/{appId}")
  toggleAppEntitlement(
    @path id: string,
    @path appId: string,
    @body body: ToggleAppEntitlementRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  // LHDN MyInvois tenant config lives under /lhdn (GET/PUT /lhdn/workspaces/{id}/lhdn-config), not One.

  @useAuth(BearerAuth)
  @get
  @route("/workspaces/{id}/webhooks")
  getWorkspaceWebhooks(@path id: string):
    | WebhookEndpointDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/workspaces/{id}/webhooks")
  createWorkspaceWebhook(
    @path id: string,
    @body body: CreateWebhookEndpointRequestDto,
  ): CreateWebhookEndpointResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @put
  @route("/workspaces/{id}/webhooks/{endpointId}")
  updateWorkspaceWebhook(
    @path id: string,
    @path endpointId: string,
    @body body: UpdateWebhookEndpointRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @get
  @route("/workspaces/{id}/webhooks/logs")
  getWorkspaceWebhookLogs(@path id: string):
    | WebhookDeliveryLogDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/storage/presigned-url")
  getPresignedUrl(@body body: GetPresignedUrlRequestDto):
    | GetPresignedUrlResponseDto
    | LazuarApi.Core.ProblemDetailsResponse;

  // Platform API credentials — OrgAdmin JWT only
  @useAuth(BearerAuth)
  @get
  @route("/api-keys")
  listApiKeys(): ApiKeyDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @post
  @route("/api-keys")
  generateApiKey(
    @body body: GenerateApiKeyRequestDto,
  ): GenerateApiKeyResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @useAuth(BearerAuth)
  @delete
  @route("/api-keys/{id}")
  revokeApiKey(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  /**
   * Integrator provision: create Hub workspace + Aura-scoped bootstrap key for an aura_org_id.
   * Optional Connect: webhook_url registers outbound endpoint (secret_key once); owner_email attaches membership.
   * Auth: X-Lazuar-Provision-Key / Bearer provision secret, or SUPER_ADMIN JWT (not OrgAdmin / sk_*).
   * Idempotent on aura_org_id — plain_key and webhook secret_key only on first materialization.
   */
  @post
  @route("/integrations/workspaces/provision")
  provisionWorkspace(@body body: ProvisionWorkspaceRequestDto):
    | ProvisionWorkspaceResponseDto
    | LazuarApi.Core.ProblemDetailsResponse;
}
