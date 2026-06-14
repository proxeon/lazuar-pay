import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Lhdn;

@useAuth(BearerAuth)
@route("/api/v1/lhdn")
interface LhdnOperations {
  @post
  @route("/documents")
  submitDocument(
    @body body: SubmitDocumentRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/documents/{internalId}")
  getDocumentStatus(
    @path internalId: string,
  ): LhdnDocumentResponseDto | LazuarApi.Core.ProblemDetailsResponse;

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
}
