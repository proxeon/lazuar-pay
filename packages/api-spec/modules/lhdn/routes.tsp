import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Lhdn;

@useAuth(BearerAuth)
@route("/lhdn")
interface LhdnOperations {
  @doc("Validate a taxpayer TIN against MyInvois (cached). Requires API key with lhdn.documents:read or write.")
  @post
  @route("/taxpayer/validate")
  validateTaxpayerTin(
    @body body: ValidateTinRequestDto,
  ): ValidateTinResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/documents")
  submitDocument(
    @header("Idempotency-Key") idempotencyKey: string,
    @body body: SubmitDocumentRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/documents/{internalId}")
  getDocumentStatus(
    @path internalId: string,
  ): LhdnDocumentResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/documents/{internalId}/cancel")
  cancelDocument(
    @path internalId: string,
    @body body: CancelDocumentRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/webhooks")
  registerWebhook(
    @body body: RegisterWebhookRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/webhooks")
  listWebhooks(
  ): WebhookSubscriptionDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/webhooks/{id}")
  deleteWebhook(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @doc("Get tenant MyInvois / supplier config. Secrets masked (has_* flags only). OrgAdmin JWT.")
  @get
  @route("/workspaces/{id}/lhdn-config")
  getLhdnTenantConfig(
    @path id: string,
  ): LhdnTenantConfigDto | LazuarApi.Core.ProblemDetailsResponse;

  @doc("Create or update tenant MyInvois / supplier config (TIN, BRN, MSIC, env, credentials, legal address). OrgAdmin JWT.")
  @put
  @route("/workspaces/{id}/lhdn-config")
  updateLhdnTenantConfig(
    @path id: string,
    @body body: UpdateLhdnTenantConfigRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/workspaces/{id}/lhdn-certificate")
  updateCertificate(
    @path id: string,
    @body body: UpdateLhdnCertificateRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/api-keys")
  generateApiKey(
    @body body: GenerateApiKeyRequestDto,
  ): GenerateApiKeyResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/api-keys")
  listApiKeys(): ApiKeyDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/api-keys/{id}")
  revokeApiKey(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
