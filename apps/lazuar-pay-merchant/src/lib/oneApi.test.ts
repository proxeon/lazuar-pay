import { afterEach, describe, expect, it, vi } from 'vitest'
import { createPayWorkerKey, listApiKeys, PAY_WORKER_SCOPES, revokeApiKey } from './oneApi'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

function jsonResponse(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('PAY_WORKER_SCOPES', () => {
  it('is the pair Pay MemberGate needs, not a catalog', () => {
    expect([...PAY_WORKER_SCOPES]).toEqual(['tenant:read', 'authz:check'])
  })
})

describe('createPayWorkerKey', () => {
  it('POSTs One /tenants/{id}/api-keys with the Pay worker scopes', async () => {
    const fetchMock = vi.fn<typeof fetch>(async () =>
      jsonResponse(201, {
        id: 'k1',
        name: 'Pay worker',
        prefix: 'lzr_sk_abcd',
        scopes: ['tenant:read', 'authz:check'],
        secret: 'lzr_sk_secret',
      }),
    )
    vi.stubGlobal('fetch', fetchMock)
    const created = await createPayWorkerKey('jwt.tok.en', 'org-1', 'Pay worker')
    expect(created.secret).toBe('lzr_sk_secret')
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/tenants\/org-1\/api-keys$/),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          name: 'Pay worker',
          scopes: ['tenant:read', 'authz:check'],
        }),
      }),
    )
    const url = String(fetchMock.mock.calls[0][0])
    expect(url).not.toContain(':8081')
    expect(url).not.toContain('/v1/orgs/')
    const headers = new Headers(fetchMock.mock.calls[0][1]?.headers)
    expect(headers.get('Authorization')).toBe('Bearer jwt.tok.en')
    expect(headers.get('X-Lazuar-Tenant-Id')).toBe('org-1')
  })

  it('maps a network throw to One unreachable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
    await expect(createPayWorkerKey('tok', 'org-1', 'x')).rejects.toThrow('One unreachable')
  })
})

describe('listApiKeys', () => {
  it('reads One { data } and does not call Pay', async () => {
    const fetchMock = vi.fn<typeof fetch>(async () =>
      jsonResponse(200, { data: [{ id: 'k1', name: 'n', prefix: 'lzr_sk_ab', scopes: [] }] }),
    )
    vi.stubGlobal('fetch', fetchMock)
    await expect(listApiKeys('tok', 'org-1')).resolves.toHaveLength(1)
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/tenants/org-1/api-keys'),
      expect.anything(),
    )
    expect(String(fetchMock.mock.calls[0][0])).not.toContain('/v1/orgs/')
  })
})

describe('revokeApiKey', () => {
  it('DELETE One /api-keys/{id}', async () => {
    const fetchMock = vi.fn<typeof fetch>(async () => new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)
    await revokeApiKey('tok', 'org-1', 'key-9')
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/tenants/org-1/api-keys/key-9'),
      expect.objectContaining({ method: 'DELETE' }),
    )
  })
})
