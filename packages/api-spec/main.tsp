import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

using TypeSpec.Http;

@service({
  title: "Lazuar Platform API",
})
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;

// --- Shared Models ---
model ErrorResponse {
  @statusCode statusCode: 400 | 401 | 404 | 500;
  error: string;
}

// --- Community Domain Models ---
model CommunityPlanDto {
  id: string; // GUID
  slug: string;
  name: string;
  audience: string;
  short_description: string;
  long_description?: string;
  price: float64;
  interval: string;
  is_active: boolean;
  max_capacity?: int32;
  enrolled_count: int32;
  spots_remaining?: int32;
  is_full: boolean;
}

// --- Endpoints ---
@route("/public/community")
namespace PublicCommunity {
  @summary("Get all active public plans for a tenant")
  @route("/{tenantSlug}/plans")
  @get
  op getPlans(@path tenantSlug: string): CommunityPlanDto[] | ErrorResponse;

  @summary("Get a specific plan by its slug")
  @route("/{tenantSlug}/plans/{slug}")
  @get
  op getPlanBySlug(
    @path tenantSlug: string,
    @path slug: string,
  ): CommunityPlanDto | ErrorResponse;
}
