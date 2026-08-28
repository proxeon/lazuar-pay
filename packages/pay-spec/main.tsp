import "@typespec/http";
import "@typespec/openapi";

using Http;
using OpenAPI;

/** Focused Pay HTTP contract. Not packages/api-spec. Checkouts persist in Postgres; paid via verified PSP webhook. */
@service(#{ title: "Lazuar Pay" })
@info(#{ version: "0.1.0" })
@server("http://localhost:8081", "Local focused Pay host")
namespace LazuarPay;

model HealthResponse {
  status: string;
}

model WhoamiTenant {
  id: string;
  slug?: string;
  name?: string;
  role?: string;
  status?: string;
}

model WhoamiResponse {
  user_id: string;
  email?: string;
  name?: string;
  is_platform_admin: boolean;
  active_org_id?: string;
  tenants: WhoamiTenant[];
}

/** Member ping plus whether this shop can take money: not charges_paused, and a vault row or Test allowed. */
model OrgReadyResponse {
  org_id: string;
  ready: boolean;
}

/** stripe|chip|billplz|xendit|razorpay|test. Host has no default; unknown → 400. */
model CreateCheckoutRequest {
  org_id: string;
  provider: string;
  product_id?: string;
  amount: decimal;
  currency?: string;
  success_url?: string;
  cancel_url?: string;
  idempotency_key?: string;
}

model CheckoutSession {
  id: string;
  org_id: string;
  provider?: string;
  product_id?: string;
  payment_link_id?: string;
  slot_key?: string;
  amount: decimal;
  currency: string;
  status: string;
  public_token?: string;
  interval?: string;
  success_url?: string;
  cancel_url?: string;
  payer_name?: string;
  payer_email?: string;
  created_at: utcDateTime;
}

model CheckoutListItem {
  id: string;
  org_id: string;
  provider?: string;
  amount: decimal;
  currency: string;
  status: string;
  public_token?: string;
  created_at: utcDateTime;
  label?: string;
}

model PublicPay {
  token: string;
  amount: decimal;
  currency: string;
  status: string;
  payer_name?: string;
  payer_email?: string;
  email_required?: boolean;
  started?: boolean;
  mine?: boolean;
  provider?: string;
  redirect_url?: string;
  remaining?: int32;
  max_payers?: int32;
  paid_count?: int32;
  taken_count?: int32;
}

/**
 * Buyer start body. `slot_key` is required for payment-link tokens (trim, length 8–128);
 * standalone checkout tokens ignore it. Also accepted as `?slot_key=` on GET /pay/{token}.
 */
model StartPayRequest {
  name?: string;
  email?: string;
  slot_key?: string;
}

model GatewayView {
  org_id: string;
  provider?: string;
  last4?: string;
  configured: boolean;
  capability?: string;
  public_merchant_id?: string;
  environment?: string;
  webhook_configured?: boolean;
}

model GatewayList {
  org_id: string;
  processors: GatewayView[];
}

/** BYOK. provider is stripe|chip|billplz|xendit|razorpay. PUT test → 400. key_id+key_secret concat into secret when secret is empty. */
model PutGateway {
  provider: string;
  secret?: string;
  webhook_secret: string;
  public_merchant_id?: string;
  environment?: string;
  key_id?: string;
  key_secret?: string;
}

model PutOneWebhook {
  webhook_secret: string;
}

model OneWebhookView {
  org_id: string;
  webhook_configured: boolean;
}

model CreateProductRequest {
  name: string;
  description?: string;
  amount: decimal;
  currency?: string;
  interval?: string;
}

model CreatedProduct {
  id: string;
  org_id: string;
  name: string;
  price_id: string;
  amount: decimal;
  currency: string;
  interval: string;
}

model ProductPrice {
  id: string;
  amount: decimal;
  currency: string;
  interval: string;
}

model Product {
  id: string;
  org_id: string;
  name: string;
  prices?: ProductPrice[];
}

model StartPayResponse {
  redirect_url: string;
}

model CreatePaymentLinkRequest {
  org_id: string;
  provider: string;
  product_id?: string;
  amount: decimal;
  currency?: string;
  max_payers?: int32;
  unlimited?: boolean;
}

model PaymentLink {
  id: string;
  org_id: string;
  provider: string;
  amount: decimal;
  currency: string;
  status: string;
  public_token: string;
  created_at: utcDateTime;
  max_payers?: int32;
  unlimited: boolean;
  paid_count: int32;
  taken_count: int32;
  remaining?: int32;
  label?: string;
}

model Payment {
  id: string;
  org_id: string;
  checkout_id: string;
  amount: decimal;
  currency: string;
  status: string;
  provider?: string;
  payer_name?: string;
  created_at?: utcDateTime;
  label?: string;
}

model Receipt {
  id: string;
  org_id: string;
  number: string;
  title?: string;
  checkout_id: string;
  amount?: decimal;
  currency?: string;
  payer_name?: string;
  created_at?: utcDateTime;
  label?: string;
  status?: string;
}

model WebhookOk {
  ok: boolean;
}

model WebhookDuplicate {
  duplicate: boolean;
}

model WebhookIgnored {
  ignored?: string;
}

union PspWebhookResult {
  ok: WebhookOk,
  duplicate: WebhookDuplicate,
  ignored: WebhookIgnored,
}

union OneWebhookResult {
  ok: WebhookOk,
  duplicate: WebhookDuplicate,
}

model OneWebhookEvent {
  type?: string;
  id?: string;
  org_id?: string;
  tenant_id?: string;
}

@route("/v1")
@tag("Health")
interface Health {
  /** Process liveness for the focused Pay host. */
  @get
  @route("/health")
  check(): HealthResponse;
}

@route("/v1")
@tag("Session")
interface Session {
  /** Pay projection of One GET /me. Requires Bearer. Not Hub /one/auth/me. */
  @get
  @route("/whoami")
  whoami(): WhoamiResponse;
}

@route("/v1")
@tag("Orgs")
interface Orgs {
  /** Member ping. ready is true when charges are not paused and (vault on file or Test is allowed). */
  @get
  @route("/orgs/{orgId}/ready")
  ready(@path orgId: string): OrgReadyResponse;
}

@route("/v1")
@tag("Checkouts")
interface Checkouts {
  /** Merchant creates a checkout. org_id is the One tenant id. Writer is One JWT owner/admin or a bound One `lzr_sk_`. 201 on mint; 200 on idempotent replay. */
  @post
  @route("/checkouts")
  create(
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: CreateCheckoutRequest,
  ): {
    @statusCode statusCode: 201;
    @body body: CheckoutSession;
  } | {
    @statusCode statusCode: 200;
    @body body: CheckoutSession;
  };

  /** Merchant reads a checkout they are a member of. */
  @get
  @route("/checkouts/{id}")
  get(@path id: string): CheckoutSession;

  /** Member list. Mixes one-off mints and payment-link children. */
  @get
  @route("/orgs/{orgId}/checkouts")
  list(@path orgId: string): CheckoutListItem[];
}

@route("/v1")
@tag("PaymentLinks")
interface PaymentLinks {
  /** Merchant mint door. Requires Bearer + writer. No Idempotency-Key. */
  @post
  @route("/payment-links")
  create(@body body: CreatePaymentLinkRequest): {
    @statusCode statusCode: 201;
    @body body: PaymentLink;
  };

  @get
  @route("/orgs/{orgId}/payment-links")
  list(@path orgId: string): PaymentLink[];
}

@route("/v1")
@tag("Pay")
interface PublicPayApi {
  /** Token is a payment-link or checkout public token. `slot_key` resumes a link child. */
  @get
  @route("/pay/{token}")
  get(@path token: string, @query slot_key?: string): PublicPay;

  /**
   * Starts hosted pay. Payment-link tokens require `slot_key` (8–128) or 400.
   * Standalone checkout tokens do not.
   */
  @post
  @route("/pay/{token}/start")
  start(@path token: string, @body body?: StartPayRequest): StartPayResponse;
}

@route("/v1")
@tag("Catalog")
interface Catalog {
  /** Requires Bearer + writer. Currency other than MYR → 400. */
  @post
  @route("/orgs/{orgId}/products")
  createProduct(@path orgId: string, @body body: CreateProductRequest): {
    @statusCode statusCode: 201;
    @body body: CreatedProduct;
  };

  @get
  @route("/orgs/{orgId}/products")
  listProducts(@path orgId: string): Product[];
}

@route("/v1")
@tag("Gateways")
interface Gateways {
  /** BYOK keys. provider is stripe|chip|billplz|xendit|razorpay. Writer only. PUT test → 400. */
  @put
  @route("/orgs/{orgId}/gateway")
  put(@path orgId: string, @body body: PutGateway): GatewayView;

  /** Singular requires `provider`. Missing query is 400, not the list envelope. */
  @get
  @route("/orgs/{orgId}/gateway")
  get(@path orgId: string, @query provider: string): GatewayView;

  /** Member list. processors is five rails, or six with `test` outside Production. */
  @get
  @route("/orgs/{orgId}/gateways")
  list(@path orgId: string): GatewayList;
}

@route("/v1")
@tag("Money")
interface Money {
  @get
  @route("/orgs/{orgId}/payments")
  listPayments(@path orgId: string): Payment[];

  @get
  @route("/orgs/{orgId}/receipts")
  listReceipts(@path orgId: string): Receipt[];

  @get
  @route("/orgs/{orgId}/receipts/{id}")
  getReceipt(@path orgId: string, @path id: string): Receipt;
}

@route("/v1")
@tag("Webhooks")
interface Webhooks {
  /** Plane B. provider is stripe|chip|billplz|xendit|razorpay|test. 200 is paid `{ok}`, replay `{duplicate}`, or `{ignored}`. */
  @post
  @route("/webhooks/{provider}/{orgId}")
  psp(@path provider: string, @path orgId: string): PspWebhookResult;

  @post
  @route("/one/webhooks")
  one(
    @header("X-Lazuar-Signature") signature?: string,
    @header("X-Lazuar-Timestamp") timestamp?: string,
    @body body?: OneWebhookEvent,
  ): OneWebhookResult;

  /** Writer stores this shop's One `whsec_`. Process `Pay:OneWebhookSecret` is the one-shop fallback. */
  @put
  @route("/orgs/{orgId}/one-webhook")
  putOne(@path orgId: string, @body body: PutOneWebhook): OneWebhookView;

  @get
  @route("/orgs/{orgId}/one-webhook")
  getOne(@path orgId: string): OneWebhookView;
}
