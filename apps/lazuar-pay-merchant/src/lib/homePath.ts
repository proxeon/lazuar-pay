import type { WhoamiTenant } from './payApi'
import { getOrgHint } from './sessionKeys'

/** Last used org if still a member, else first tenant. Empty → create workspace. */
export function dashboardPath(tenants: WhoamiTenant[]): string {
  if (tenants.length === 0) return '/workspaces/new'
  const hint = getOrgHint()
  const match = hint ? tenants.find((t) => t.id === hint) : undefined
  return `/o/${(match ?? tenants[0]).id}/overview`
}
