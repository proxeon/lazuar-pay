// packages/api-spec/common/models.tsp
import "@typespec/http";

using TypeSpec.Http;

namespace LazuarApi.Core;

@summary("RFC 7807 Standardized Error Response")
model ProblemDetails {
  type?: string;
  title?: string;
  status?: int32;
  detail?: string;
  instance?: string;
}

@summary("RFC 7807 Extended Validation Error Response")
model ValidationProblemDetails extends ProblemDetails {
  errors?: Record<string[]>;
}

model IdResponse {
  id: string; // GUID
}

model StatusResponse {
  status: string;
}

@error
model ProblemDetailsResponse {
  @statusCode statusCode: 400 | 401 | 403 | 404 | 500;
  @body body: ProblemDetails;
}
