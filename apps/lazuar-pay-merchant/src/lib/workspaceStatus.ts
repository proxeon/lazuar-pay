import type { WhoamiTenant } from './payApi'

export function workspaceStatusBanner(tenant: WhoamiTenant): string | null {
  if (tenant.status === 'suspended') {
    return 'This workspace is suspended. Charges are paused.'
  }

  return null
}
