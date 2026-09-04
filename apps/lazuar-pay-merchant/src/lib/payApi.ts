import { problemDetail } from './http'

const payApi = (import.meta.env.VITE_PAY_API_URL || 'http://localhost:8081').trim().replace(/\/+$/, '')

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
  name?: string
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
  let response: Response
  try {
    response = await fetch(`${payApi}/v1/whoami`, { headers })
  } catch {
    throw new Error('Pay unreachable')
  }
  if (response.status === 401) {
    throw new Error('unauthorized')
  }
  if (!response.ok) {
    throw new Error(await problemDetail(response, `whoami ${response.status}`))
  }
  return (await response.json()) as Whoami
}

export async function payFetch(
  accessToken: string,
  path: string,
  init?: RequestInit & { orgHint?: string },
): Promise<Response> {
  const headers = new Headers(init?.headers)
  headers.set('Authorization', `Bearer ${accessToken}`)
  headers.set('Accept', 'application/json')
  if (init?.orgHint) headers.set('X-Lazuar-Tenant-Id', init.orgHint)
  return fetch(`${payApi}${path}`, { ...init, headers })
}

/** GET/POST JSON. Non-OK uses host `detail`. Network throw → Pay unreachable. */
export async function payJson<T>(
  accessToken: string,
  path: string,
  init?: RequestInit & { orgHint?: string },
): Promise<T> {
  let response: Response
  try {
    response = await payFetch(accessToken, path, init)
  } catch {
    throw new Error('Pay unreachable')
  }
  if (!response.ok) {
    throw new Error(await problemDetail(response, `Pay ${response.status}`))
  }
  try {
    return (await response.json()) as T
  } catch {
    throw new Error('Pay unreachable')
  }
}

export type PayPage<T> = { items: T[]; next_cursor?: string | null }

export function listItems<T>(payload: T[] | PayPage<T> | null | undefined): T[] {
  if (payload == null) return []
  if (Array.isArray(payload)) return payload
  return payload.items ?? []
}

/**
 * Issue 006 (issues/001): every list endpoint clamps to a page (default 50 rows) and
 * answers with next_cursor — which the merchant pages never followed, so an org's 51st pay
 * link (or payment, or receipt) silently vanished from the dashboard. Follow the cursor
 * until it is exhausted. maxPages bounds the loop so a pathological org cannot hang the UI.
 */
export async function listAll<T>(
  accessToken: string,
  path: string,
  init?: RequestInit & { orgHint?: string },
  { maxPages = 50 }: { maxPages?: number } = {},
): Promise<T[]> {
  const out: T[] = []
  let cursor: string | null | undefined = null
  for (let page = 0; page < maxPages; page++) {
    const suffix: string = cursor
      ? `${path.includes('?') ? '&' : '?'}after=${encodeURIComponent(cursor)}`
      : ''
    const body = await payJson<T[] | PayPage<T>>(accessToken, path + suffix, init)
    const payload: PayPage<T> | null = Array.isArray(body) ? { items: body } : body
    out.push(...(payload?.items ?? []))
    cursor = payload?.next_cursor
    if (!cursor) break
  }
  return out
}

export { payApi }
