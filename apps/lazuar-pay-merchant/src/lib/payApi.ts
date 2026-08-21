const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

export type WhoamiTenant = {
  id: string
  slug?: string
  name?: string
  role?: string
  status?: string
}

export type Whoami = {
  user_id: string
  email?: string
  is_platform_admin: boolean
  active_org_id?: string
  tenants: WhoamiTenant[]
}

/** credentials omitted on purpose: localhost cookies are not port-scoped. */
export async function getWhoami(
  accessToken: string,
  orgHint?: string | null,
): Promise<Whoami> {
  const headers: Record<string, string> = {
    Authorization: `Bearer ${accessToken}`,
    Accept: 'application/json',
  }
  if (orgHint) {
    headers['X-Lazuar-Tenant-Id'] = orgHint
  }
  const response = await fetch(`${payApi}/v1/whoami`, { headers })
  if (response.status === 401) {
    throw new Error('unauthorized')
  }
  if (!response.ok) {
    throw new Error(`whoami ${response.status}`)
  }
  return (await response.json()) as Whoami
}

export { payApi }
