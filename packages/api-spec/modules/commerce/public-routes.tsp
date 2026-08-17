import "@typespec/http";
import "../../common/models.tsp";
import "./models.tsp";

using TypeSpec.Http;

namespace LazuarApi.Commerce;

/**
 * Integrator-facing public commerce surface (D5).
 * Unauthenticated buy links, checkout, and customer portal. Pair with workspace webhooks
 * for subscription unlock/revoke — do not rely on key-authenticated M2M product admin in v1.
 */
@route("/public/commerce")
@doc("Public checkout / portal routes for storefronts and customer self-service. No API key required. Use workspace webhooks for lifecycle unlock/revoke.")
interface PublicCommerceOperations {
  /** Resolve a sellable product by workspace slug + product slug (buy-link catalog). */
  @get
  @route("/{tenantSlug}/products/{slug}")
  @doc("Public product detail for buy links. tenantSlug is the workspace public slug.")
  getProductBySlug(@path tenantSlug: string, @path slug: string):
    | ProductDto
    | LazuarApi.Core.ProblemDetailsResponse;

  /** Validate a coupon against a product before checkout. */
  @get
  @route("/{tenantSlug}/validate-coupon")
  @doc("Coupon preview for storefront checkout forms.")
  validateCoupon(
    @path tenantSlug: string,
    @query code: string,
    @query product_slug: string,
  ): ValidateCouponResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  /** Validate buyer TIN + ID against MyInvois using the merchant's credentials. */
  @post
  @route("/{tenantSlug}/validate-tin")
  validateTin(
    @path tenantSlug: string,
    @body body: PublicValidateTinRequestDto,
  ): PublicValidateTinResponseDto | LazuarApi.Core.ProblemDetailsResponse;

  /** Start checkout; returns payment gateway redirect / session details. */
  @post
  @route("/checkout")
  @doc("Initiate public checkout for a product (subscription or one-time). Optional Idempotency-Key replays the same session.")
  initiateCheckout(
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: PublicCheckoutRequestDto,
  ):
    | CheckoutResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  /**
   * Customer portal snapshot. `token` is a magic link token issued by the platform
   * (email/billing link) — not an API key.
   */
  @get
  @route("/{tenantSlug}/portal")
  @doc("Portal data for the customer holding the magic token.")
  getPortalData(@path tenantSlug: string, @query token: string):
    | AggregatedPortalDataResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/{tenantSlug}/portal/cancel")
  @doc("Customer-initiated cancel via portal magic token. Defaults to cancel at period end.")
  cancelPortalSubscription(
    @path tenantSlug: string,
    @query token: string,
    @body body: CancelPortalRequest,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/portal/plans")
  @doc("Eligible recurring products for a portal plan change.")
  getPortalPlans(@path tenantSlug: string, @query token: string):
    | PortalPlanDto[]
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/{tenantSlug}/portal/change-plan")
  @doc("Schedule a plan change from the buyer portal. No charge today.")
  changePortalPlan(
    @path tenantSlug: string,
    @query token: string,
    @body body: PortalChangePlanRequest,
  ): PlanChangePreviewDto | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/{tenantSlug}/portal/keep")
  @doc("Undo a scheduled period-end cancel while the subscription is still ACTIVE.")
  keepPortalSubscription(
    @path tenantSlug: string,
    @query token: string,
    @body body: KeepPortalRequest,
  ): LazuarApi.Core.StatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  /**
   * Request a 24h portal magic-link email. Always 200.
   * Sends only when the email matches a subscription in the workspace.
   */
  @post
  @route("/{tenantSlug}/portal/magic-link")
  @doc("Email a 24h portal magic link when the address matches a subscription. Always returns 200.")
  requestPortalMagicLink(
    @path tenantSlug: string,
    @body body: RequestPortalMagicLinkRequest,
  ): LazuarApi.Core.StatusResponse;

  /**
   * Buyer document inbox (official receipts, tax invoices, credit notes, quotes).
   * Same magic token as the portal snapshot. HMAC download URLs only when a PDF exists.
   */
  @get
  @route("/{tenantSlug}/portal/documents")
  @doc("List this buyer's commercial documents for the workspace. Token-bound; no cross-buyer rows.")
  getPortalDocuments(@path tenantSlug: string, @query token: string):
    | PortalDocumentsResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  /** Poll checkout session activation after payment redirect. COMPLETED mints a 24h portal token when a subscription exists. */
  @get
  @route("/{tenantSlug}/checkout/{sessionId}/status")
  @doc("Checkout status poller for client-side success pages. Bound to workspace slug. COMPLETED responses include a portal token when a subscription exists.")
  getCheckoutStatus(@path tenantSlug: string, @path sessionId: string):
    | CheckoutStatusResponse
    | LazuarApi.Core.ProblemDetailsResponse;

  /** Legacy status poll: requires tenant_slug query; never mints portal tokens. Prefer path with tenantSlug. */
  @get
  @route("/checkout/{subId}/status")
  @doc("Legacy checkout status poller. Requires tenant_slug query parameter.")
  getCheckoutStatusLegacy(
    @path subId: string,
    @query tenant_slug: string,
  ): CheckoutStatusResponse | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/custom-checkouts/{sessionId}")
  @doc("Load a custom payment-link checkout session (high-ticket / manual links).")
  getCustomCheckout(@path tenantSlug: string, @path sessionId: string):
    | CustomCheckoutDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @get
  @route("/checkout/{subId}/arrears")
  @doc("Arrears summary for update-payment flows.")
  getArrearsSummary(@path subId: string, @query token: string):
    | ArrearsSummaryDto
    | LazuarApi.Core.ProblemDetailsResponse;

  @post
  @route("/checkout/{subId}/update-payment")
  @doc("Start update-payment. PAST_DUE/SUSPENDED charges the due amount. ACTIVE vaulted uses a verification amount. ACTIVE reminder-only returns 400 REMINDER_ONLY.")
  updatePayment(
    @header("Idempotency-Key") idempotencyKey?: string,
    @path subId: string,
    @query token: string,
  ):
    | CheckoutResponse
    | LazuarApi.Core.ProblemDetailsResponse;
}
