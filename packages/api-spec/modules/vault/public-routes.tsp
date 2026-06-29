import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Vault;

@route("/public/vault")
interface PublicVaultOperations {
  @get
  @route("/{tenantSlug}/portal/assets")
  getPortalAssets(
    @path tenantSlug: string,
    @query product_ids: string[], // Array of GUID strings
  ): PortalVaultAssetDto[] | LazuarApi.Core.ProblemDetailsResponse;
}
