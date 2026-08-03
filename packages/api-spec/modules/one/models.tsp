// packages/api-spec/modules/one/models.tsp
namespace LazuarApi.One;

model LoginRequest {
  email: string;
  password?: string;
}

model PublicRegisterRequestDto {
  email: string;
  password?: string;
  name?: string;
  workspace_name: string;
  tenant_slug: string;
}

model LoginResponse {
  user: AuthUser;
}

model AuthUser {
  email: string;
  name: string;
  role: string;
  is_email_verified: boolean;
}

model ForgotPasswordRequestDto {
  email: string;
}

model ResetPasswordRequestDto {
  email: string;
  token: string;
  new_password: string;
}

model VerifyEmailRequestDto {
  token: string;
}

model ResendVerificationRequestDto {
  email: string;
}

model UpdateProfileRequestDto {
  name: string;
}

model ChangePasswordRequestDto {
  current_password: string;
  new_password: string;
}

model CreateWorkspaceRequestDto {
  name: string;
  slug: string;
  provision_apps: string[];
}

model UpdateWorkspaceRequestDto {
  name: string;
  slug: string;
}

model WorkspaceDto {
  id: string; // GUID
  name: string;
  slug: string;
  is_active: boolean;
  created_at: utcDateTime;
}

model WorkspaceMemberDto {
  id: string; // GUID
  global_user_id: string; // GUID
  name: string;
  email: string;
  role: string;
  joined_at: utcDateTime;
}

model CreateWorkspaceInvitationDto {
  email: string;
  role: string;
}

model WorkspaceInvitationDto {
  id: string; // GUID
  email: string;
  role: string;
  status: string;
  expires_at: utcDateTime;
}

model AcceptWorkspaceInvitationDto {
  token: string;
}

model EntitlementDto {
  workspace_id: string; // GUID
  workspace_name: string;
  workspace_slug: string;
  role: string;
}

model ToggleAppEntitlementRequestDto {
  is_active: boolean;
}

model WorkspaceAppDto {
  app_id: string;
}

/** List/detail metadata — never includes full signing secret. */
model WebhookEndpointDto {
  id: string; // GUID
  url: string;
  is_active: boolean;
  created_at: utcDateTime;
  /** Empty array means the endpoint receives all event types. */
  enabled_events: string[];
  has_secret: boolean;
  /** Last 4 characters of the signing secret for display. */
  secret_hint?: string;
}

model CreateWebhookEndpointRequestDto {
  url: string;
  is_active?: boolean = true;
  /** Empty or omitted = all events. */
  enabled_events?: string[];
}

/** Create response — includes full secret once. */
model CreateWebhookEndpointResponseDto {
  id: string; // GUID
  url: string;
  /** Full signing secret — returned only once on create. */
  secret_key: string;
  is_active: boolean;
  enabled_events: string[];
  created_at: utcDateTime;
}

model UpdateWebhookEndpointRequestDto {
  url: string;
  is_active: boolean;
  /** When provided, replaces the endpoint event filter (empty = all). */
  enabled_events?: string[];
}

/** @deprecated Prefer create + update-by-id multi-endpoint API. */
model SaveWebhookEndpointRequestDto {
  url: string;
  is_active: boolean;
}

model WebhookDeliveryLogDto {
  id: string; // GUID
  event_type: string;
  status: string;
  attempt_count: int32;
  last_error?: string;
  created_at: utcDateTime;
}

model GetPresignedUrlRequestDto {
  file_name: string;
  content_type: string;
}

model GetPresignedUrlResponseDto {
  upload_url: string;
  final_url: string;
}

// Platform API credentials (owned by One; product routes may façade these DTOs)
model GenerateApiKeyRequestDto {
  name: string;
  is_test_mode: boolean;
}

model GenerateApiKeyResponseDto {
  id: string; // GUID
  name: string;
  prefix: string;
  /** Last 4 characters of the plain key for display (not secret). */
  hint: string;
  created_at: utcDateTime;
  /** Full secret — returned only once on create. */
  plain_key: string;
  scopes: string[];
}

model ApiKeyDto {
  id: string; // GUID
  name: string;
  prefix: string;
  /** Last 4 characters of the plain key for display (not secret). */
  hint: string;
  is_active: boolean;
  created_at: utcDateTime;
  scopes: string[];
}
