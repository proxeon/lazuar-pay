import { afterEach, describe, expect, it, vi } from 'vitest'
import { getWhoami, listItems, payJson } from './payApi'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('getWhoami', () => {
  it('maps 401 to unauthorized and does not send id_token', async () => {
    const fetchMock = vi.fn(async () => jsonResponse(401, { detail: 'Identity provider rejected the token' }))
    vi.stubGlobal('fetch', fetchMock)
    await expect(getWhoami('access.jwt.sig')).rejects.toThrow('unauthorized')
    expect(fetchMock).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer access.jwt.sig' }),
      }),
    )
  })

  it('surfaces One 503 detail instead of whoami 503', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => jsonResponse(503, { detail: 'Identity provider unreachable' })),
    )
    await expect(getWhoami('tok')).rejects.toThrow('Identity provider unreachable')
    await expect(getWhoami('tok')).rejects.not.toThrow('whoami 503')
  })

  it('maps a network throw to Pay unreachable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
    await expect(getWhoami('tok')).rejects.toThrow('Pay unreachable')
  })
})

describe('listItems', () => {
  it('accepts a raw array or { items }', () => {
    expect(listItems([1, 2])).toEqual([1, 2])
    expect(listItems({ items: [3], next_cursor: 'x' })).toEqual([3])
    expect(listItems(null)).toEqual([])
  })
})

describe('payJson', () => {
  it('returns JSON on HTTP 200', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(200, [])))
    await expect(payJson<unknown[]>('tok', '/v1/orgs/o1/payment-links', { orgHint: 'o1' })).resolves.toEqual([])
  })

  it('throws host detail on non-OK and does not look like an empty list', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(503, { detail: 'Identity provider unreachable' })))
    await expect(payJson('tok', '/v1/orgs/o1/gateways', { orgHint: 'o1' })).rejects.toThrow(
      'Identity provider unreachable',
    )
  })

  it('throws Pay unreachable when fetch throws', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
    await expect(payJson('tok', '/v1/orgs/o1/payments', { orgHint: 'o1' })).rejects.toThrow('Pay unreachable')
  })
})
