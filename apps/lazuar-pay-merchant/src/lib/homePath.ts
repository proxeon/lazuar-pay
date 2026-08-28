import type { WhoamiTenant } from './payApi'
import { getOrgHint, isSafeReturnPath } from './sessionKeys'

export function orgIdFromPath(path: string): string | undefined {
  return path.match(/^\/o\/([^/]+)/)?.[1]
}

/** Last used org if still a member, else first tenant. Empty → create workspace. */
export function dashboardPath(tenants: WhoamiTenant[], hint: string | null = getOrgHint()): string {
  if (tenants.length === 0) return '/workspaces/new'
  const match = hint ? tenants.find((t) => t.id === hint) : undefined
  return `/o/${(match ?? tenants[0]).id}/overview`
}

/** Honor a deep-link only when the org is still in tenants. */
export function resolvePostLoginPath(
  returnTo: string | null,
  tenants: WhoamiTenant[],
  hint: string | null = getOrgHint(),
): string {
  if (returnTo && isSafeReturnPath(returnTo)) {
    const orgId = orgIdFromPath(returnTo)
    if (!orgId) return returnTo
    if (tenants.some((t) => t.id === orgId)) return returnTo
  }
  return dashboardPath(tenants, hint)
}
