// packages/api-spec/modules/ops/routes.tsp
import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Ops;

@useAuth(BearerAuth)
@route("/ops")
interface OpsOperations {
  @get
  @route("/chat/conversations")
  getConversations(
    @query limit?: int32 = 20,
    @query offset?: int32 = 0,
  ): LazuarApi.Core.PaginatedResponse<OpsConversationDto> | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/chat/conversations/{id}/messages")
  getConversationMessages(
    @path id: string,
  ): OpsMessageDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/chat")
  chat(
    @body body: ChatRequestDto,
  ): ChatResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  @doc("SSE stream of ChatStreamChunkDto frames (data: {json}\\n\\n). Terminal frame is data: [DONE]. Prefer this over POST /ops/chat for interactive UI. Clients should use raw fetch/EventSource — not openapi-fetch.")
  @post
  @route("/chat/stream")
  chatStream(
    @body body: ChatRequestDto,
  ): {
    @statusCode statusCode: 200;
    @header contentType: "text/event-stream";
    @body body: string;
  } | LazuarApi.Core.ProblemDetailsResponse;

  @doc("Persist a system-role message on an existing conversation (e.g. tool execution feedback).")
  @post
  @route("/chat/conversations/{id}/system-message")
  postSystemMessage(
    @path id: string,
    @body body: ChatRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/execute-action")
  executeAction(
    @body body: ProposedActionDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/chat/conversations/{id}/title")
  renameConversation(
    @path id: string,
    @body body: RenameConversationRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/chat/conversations/{id}")
  deleteConversation(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/chat/messages/{id}/resolve")
  resolveUiRequest(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
