import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Commerce;

@useAuth(BearerAuth)
@route("/admin/commerce")
interface AdminCommerceOperations {
  @get
  @route("/products")
  getProducts(): ProductDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/products/{id}")
  getProductById(
    @path id: string,
  ): ProductDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/products")
  createProduct(
    @body body: CreateProductRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/products/{id}")
  updateProduct(
    @path id: string,
    @body body: UpdateProductRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/products/{id}")
  archiveProduct(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/reminder-schedules")
  getReminderSchedules(
  ): ReminderScheduleDto[] | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/reminder-schedules")
  createReminderSchedule(
    @body body: CreateReminderScheduleRequestDto,
  ): LazuarApi.Core.IdResponse | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/reminder-schedules/{id}")
  updateReminderSchedule(
    @path id: string,
    @body body: UpdateReminderScheduleRequestDto,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @delete
  @route("/reminder-schedules/{id}")
  deleteReminderSchedule(
    @path id: string,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;
}
