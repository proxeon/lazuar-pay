namespace LazuarApi.One;

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
