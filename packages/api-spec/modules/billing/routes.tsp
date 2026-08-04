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

  @summary("Generates a secure R2 presigned download URL for the requested ledger document.")
  @get
  @route("/ledger/{id}/document")
  downloadLedgerDocument(@path id: string):
    | DocumentDownloadUrlDto
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

  @get
  @route("/credits/packages")
  getCreditPackages():
    | CreditPackageDto[]
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
  @doc("Draft proforma PDF. Requires HMAC sig + exp (payload tenantSlug:draft:sessionId:exp).")
  getDraftDocument(
    @path tenantSlug: string,
    @path sessionId: string,
    @query sig: string,
    @query exp: int64,
  ): bytes | LazuarApi.Core.ProblemDetailsResponse;
}
