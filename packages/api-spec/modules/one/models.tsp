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

alias EnvironmentType = "PROD" | "SANDBOX";

model WorkspaceLhdnConfigDto {
  myinvois_client_id?: string;
  myinvois_client_secret?: string;
  myinvois_env: EnvironmentType;
  supplier_tin?: string;
  msic_code?: string;
}

model UpdateWorkspaceLhdnConfigRequestDto {
  myinvois_client_id?: string;
  myinvois_client_secret?: string;
  myinvois_env: EnvironmentType;
  supplier_tin?: string;
  msic_code?: string;
}

model WebhookEndpointDto {
  id: string; // GUID
  url: string;
  secret_key: string;
  is_active: boolean;
  created_at: utcDateTime;
}

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
