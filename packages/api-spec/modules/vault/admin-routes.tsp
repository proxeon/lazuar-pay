import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Vault;

@useAuth(BearerAuth)
@route("/admin/vault")
interface AdminVaultOperations {
  @post
  @route("/assets")
  createAsset(
    @body body: CreateVaultAssetRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;
}
