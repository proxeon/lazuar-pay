namespace LazuarApi.One;

// --- Auth Models ---
model LoginRequest {
  email: string;
  password?: string;
}

model PublicRegisterRequestDto {
  email: string;
  password?: string;
  name?: string;
}

model LoginResponse {
  user: AuthUser;
}

model AuthUser {
  email: string;
  name: string;
  role: string;
  is_system_admin: boolean;
}

// --- Workspace Models ---
model CreateWorkspaceRequestDto {
  name: string;
  slug: string;
  provision_apps: string[];
}

model WorkspaceDto {
  id: string; // GUID
  name: string;
  slug: string;
  is_active: boolean;
  created_at: utcDateTime;
}

// --- Entitlement Models ---
model EntitlementDto {
  workspace_id: string; // GUID
  workspace_name: string;
  workspace_slug: string;
  role: string;
}

// --- App Entitlement Models ---
model ToggleAppEntitlementRequestDto {
  is_active: boolean;
}

model WorkspaceAppDto {
  app_id: string;
}
