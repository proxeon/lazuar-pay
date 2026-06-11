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
    @query from_date?: utcDateTime,
    @query to_date?: utcDateTime,
  ): LazuarApi.Core.PaginatedResponse<LedgerEntryDto> | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/summary")
  getFinancialSummary(
    @query from_date?: utcDateTime,
    @query to_date?: utcDateTime,
  ): FinancialSummaryDto | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/net-profit")
  getNetProfit(
    @query period?: string = "monthly",
  ): NetProfitDto[] | LazuarApi.Core.ProblemDetailsResponse;
}
