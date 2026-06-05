// packages/api-spec/modules/auth/models.tsp

namespace LazuarApi.Auth;

model LoginRequest {
  email: string;
  password?: string;
}

model LoginResponse {
  token: string;
  user: AuthUser;
}

model AuthUser {
  email: string;
  name: string;
  role: string;
}
