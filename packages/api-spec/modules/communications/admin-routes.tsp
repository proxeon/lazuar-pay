import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Communications;

@useAuth(BearerAuth)
@route("/admin/communications")
interface AdminCommunicationsOperations {
  @get
  @route("/templates")
  getTemplates(): MessageTemplateDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/templates")
  createTemplate(@body body: CreateTemplateRequestDto):
    | LazuarApi.Core.IdResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/templates/variables")
  getTemplateVariables():
    | TemplateVariableCategoryDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/templates/preview")
  previewTemplate(@body body: TemplatePreviewRequestDto):
    | TemplatePreviewResponseDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/templates/{id}")
  updateTemplate(@path id: string, @body body: UpdateTemplateRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/templates/{id}")
  resetTemplate(@path id: string):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/reminders/test")
  testReminder(@body body: TestReminderRequestDto):
    | TestReminderResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/broadcasts")
  sendBroadcast(@body body: CreateBroadcastRequestDto):
    | LazuarApi.Core.IdResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/broadcasts/preview")
  @doc("Pre-send audience size and credit preview. v1 broadcasts are free; credit fields are reserved and return zeros.")
  previewBroadcastCost():
    | BroadcastCostPreviewDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/broadcasts/{id}")
  @doc("Broadcast fan-out status. credits_reserved/credits_used are reserved; v1 always 0.")
  getBroadcastStatus(@path id: string):
    | BroadcastStatusDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/email-config")
  getEmailConfig(): EmailConfigDto | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/email-config")
  saveEmailConfig(@body body: SaveEmailConfigRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;
}
