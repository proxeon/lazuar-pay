import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Commerce;

@useAuth(BearerAuth)
@route("/admin/commerce")
interface AdminCommerceOperations {
  @post
  @route("/products")
  createProduct(
    @body body: CreateProductRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;
}
