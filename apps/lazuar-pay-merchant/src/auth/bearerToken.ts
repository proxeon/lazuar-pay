import type { User } from 'oidc-client-ts'

/** True if token looks like a compact JWS (header.payload.sig) — not opaque / JWE. */
export function isJwtLike(token: string | undefined | null): boolean {
  if (!token) return false
  const parts = token.split('.')
  return parts.length === 3 && parts.every((p) => p.length > 0)
}

/**
 * Pick a Bearer token for Pay / One APIs.
 * Send only a JWT access_token. Never send id_token (not an API credential).
 */
export function pickApiBearerToken(user: User | null | undefined): string | undefined {
  if (!user) return undefined
  if (isJwtLike(user.access_token)) return user.access_token
  return undefined
}
