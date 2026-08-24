import type { User } from 'oidc-client-ts'
import type { Whoami } from './payApi'

function usable(value: string | undefined | null): string | null {
  const trimmed = value?.trim()
  if (!trimmed) return null
  if (/^\d+$/.test(trimmed)) return null
  return trimmed
}

/** Sidebar label. Prefer email/name from whoami or OIDC profile. Never a Zitadel numeric sub. */
export function staffDisplay(
  who: Whoami,
  user?: User | null,
): { name: string; email: string | null } {
  const profile = user?.profile
  const email =
    usable(who.email) ??
    usable(profile?.email) ??
    (profile?.preferred_username?.includes('@') ? usable(profile.preferred_username) : null)
  const name =
    usable(who.name) ??
    usable(typeof profile?.name === 'string' ? profile.name : undefined) ??
    (email ? email.split('@')[0]! : null)
  return { name: name ?? 'Signed in', email }
}
