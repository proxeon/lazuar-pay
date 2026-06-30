import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Billing;

@useAuth(BearerAuth)
@route("/admin/billing")
interface AdminBillingOperations {
  @get
  @route("/ledger")
  getLedgerEntries(
    @query page?: int32 = 1,
    @query limit?: int32 = 50,
    @query search?: string,
    @query type_filter?: string,
    @query from_date?: utcDateTime,
    @query to_date?: utcDateTime,
  ):
    | LazuarApi.Core.PaginatedResponse<LedgerEntryDto>
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/summary")
  getFinancialSummary(
    @query from_date?: utcDateTime,
    @query to_date?: utcDateTime,
  ): FinancialSummaryDto | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/net-profit")
  getNetProfit(@query period?: string = "monthly"):
    | NetProfitDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/credits")
  getApiCreditBalance():
    | CreditBalanceDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/credits/top-up")
  createTopUpSession(@body body: CreateTopUpRequestDto):
    | TopUpResponseDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/profile")
  getBillingProfile():
    | TenantBillingProfileDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @put
  @route("/profile")
  updateBillingProfile(@body body: UpdateTenantBillingProfileRequestDto):
    | LazuarApi.Core.StatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;
}

@route("/public/billing")
interface PublicBillingOperations {
  @get
  @route("/{tenantSlug}/profile")
  getPublicBillingProfile(@path tenantSlug: string):
    | TenantBillingProfileDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/documents/draft/{sessionId}")
  getDraftDocument(@path tenantSlug: string, @path sessionId: string):
    | bytes
    | LazuarApi.Core.ProblemDetailsResponse;
}
