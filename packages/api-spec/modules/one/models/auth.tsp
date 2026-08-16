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
  /** Clickwrap. Public register rejects the request when this is not true. */
  accepted_terms?: boolean;
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
