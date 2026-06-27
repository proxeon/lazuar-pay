import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Community;

@route("/public/community")
interface PublicCommunityOperations {
  @get
  @route("/{tenantSlug}/portal/spaces")
  getPortalSpaces(
    @path tenantSlug: string,
    @query product_ids: string[], // Array of GUID strings
  ): PortalCommunitySpaceDto[] | LazuarApi.Core.ProblemDetailsResponse;
}
