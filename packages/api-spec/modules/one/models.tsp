namespace LazuarApi.One;

// --- Auth Models ---
model LoginRequest {
  email: string;
  password?: string;
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
}

// --- Entitlement Models ---
model EntitlementDto {
  workspace_id: string; // GUID
  workspace_name: string;
  workspace_slug: string;
  role: string;
}
