import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Vault;

@useAuth(BearerAuth)
@route("/admin/vault")
interface AdminVaultOperations {
  @get
  @route("/assets")
  getAssets(): VaultAssetDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/assets/{id}")
  getAssetById(
    @path id: string,
  ): VaultAssetDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/assets")
  createAsset(
    @body body: CreateVaultAssetRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/assets/{id}")
  updateAsset(
    @path id: string,
    @body body: UpdateVaultAssetRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/assets/{id}")
  deleteAsset(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/presigned-url")
  getPresignedUrl(
    @body body: GetPresignedUrlRequestDto,
  ): GetPresignedUrlResponseDto | LazuarApi.Core.ProblemDetailsResponse;
}
