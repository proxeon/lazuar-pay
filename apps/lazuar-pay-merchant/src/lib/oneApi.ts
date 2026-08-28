import { problemDetail } from './http'

const oneApi =
  import.meta.env.VITE_ONE_API_URL ?? 'http://localhost:8080/api/v1'

/** Scopes a Pay worker needs. Not a catalog — One remains the full key console. */
export const PAY_WORKER_SCOPES = ['tenant:read', 'authz:check'] as const

function oneBase(): string {
  return oneApi.replace(/\/$/, '')
}

export type OneApiKey = {
  id: string
  name: string
  prefix: string
  scopes: string[]
  revoked_at?: string | null
  created_at?: string
}

export type OneApiKeyCreated = OneApiKey & { secret: string }

export async function createTenant(
  accessToken: string,
  name: string,
  slug: string,
): Promise<{ id: string; slug: string; name: string }> {
  const response = await fetch(`${oneBase()}/tenants`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    body: JSON.stringify({ name, slug }),
  })
  if (!response.ok) {
    throw new Error(await problemDetail(response, `create tenant ${response.status}`))
  }
  return (await response.json()) as { id: string; slug: string; name: string }
}

async function oneFetch(
  accessToken: string,
  path: string,
  init?: RequestInit & { orgHint?: string },
): Promise<Response> {
  const { orgHint, headers: initHeaders, ...rest } = init ?? {}
  const headers = new Headers(initHeaders)
  headers.set('Authorization', `Bearer ${accessToken}`)
  headers.set('Accept', 'application/json')
  if (rest.body != null && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  if (orgHint) headers.set('X-Lazuar-Tenant-Id', orgHint)
  return fetch(`${oneBase()}${path}`, { ...rest, headers })
}

async function oneJson<T>(
  accessToken: string,
  path: string,
  init?: RequestInit & { orgHint?: string },
): Promise<T | undefined> {
  let response: Response
  try {
    response = await oneFetch(accessToken, path, init)
  } catch {
    throw new Error('One unreachable')
  }
  if (response.status === 204) return undefined
  if (!response.ok) {
    throw new Error(await problemDetail(response, `One ${response.status}`))
  }
  const text = await response.text()
  if (!text) return undefined
  return JSON.parse(text) as T
}

export async function listApiKeys(
  accessToken: string,
  tenantId: string,
): Promise<OneApiKey[]> {
  const page = await oneJson<{ data?: OneApiKey[] }>(
    accessToken,
    `/tenants/${encodeURIComponent(tenantId)}/api-keys?page=1&page_size=50`,
    { orgHint: tenantId },
  )
  return page?.data ?? []
}

/** Mint a One lzr_sk_ that can call Pay. Scopes are not caller-chosen. */
export async function createPayWorkerKey(
  accessToken: string,
  tenantId: string,
  name: string,
): Promise<OneApiKeyCreated> {
  const created = await oneJson<OneApiKeyCreated>(
    accessToken,
    `/tenants/${encodeURIComponent(tenantId)}/api-keys`,
    {
      method: 'POST',
      orgHint: tenantId,
      body: JSON.stringify({
        name: name.trim(),
        scopes: [...PAY_WORKER_SCOPES],
      }),
    },
  )
  if (!created?.secret) {
    throw new Error('One did not return a secret')
  }
  return created
}

export async function revokeApiKey(
  accessToken: string,
  tenantId: string,
  keyId: string,
): Promise<void> {
  await oneJson(
    accessToken,
    `/tenants/${encodeURIComponent(tenantId)}/api-keys/${encodeURIComponent(keyId)}`,
    { method: 'DELETE', orgHint: tenantId },
  )
}
