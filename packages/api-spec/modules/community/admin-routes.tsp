import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Community;

@useAuth(BearerAuth)
@route("/admin/community")
interface AdminCommunityOperations {
  @post
  @route("/spaces")
  createSpace(
    @body body: CreateCommunitySpaceRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;
}
