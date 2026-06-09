import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Ops;

@useAuth(BearerAuth)
@route("/ops")
interface OpsOperations {
  @post
  @route("/chat")
  chat(
    @body body: ChatRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/execute-action")
  executeAction(
    @body body: ActionApprovalRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
