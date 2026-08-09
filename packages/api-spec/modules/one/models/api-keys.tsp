namespace LazuarApi.One;

// Platform API credentials (owned by One; product routes may façade these DTOs)
model GenerateApiKeyRequestDto {
  name: string;
  is_test_mode: boolean;
  /**
   * Optional closed-catalog scopes (e.g. payments.checkouts:write, lhdn.documents:read).
   * Omit for LHDN document defaults; empty array is rejected; unknown strings return 400.
   */
  scopes?: string[];
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
