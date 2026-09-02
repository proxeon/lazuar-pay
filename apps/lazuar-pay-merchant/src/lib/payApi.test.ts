import { afterEach, describe, expect, it, vi } from 'vitest'
import { getWhoami, listAll, listItems, payJson } from './payApi'

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

describe('listAll', () => {
  // Issue 006 (issues/001): the merchant pages fetched only page 1 of a cursor-paginated
  // API, silently hiding every row past the server's 50-row clamp. listAll must follow
  // next_cursor until exhausted.
  it('follows next_cursor until it is exhausted', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { items: [{ id: 'a1' }, { id: 'a2' }], next_cursor: 'cur-1' }))
      .mockResolvedValueOnce(jsonResponse(200, { items: [{ id: 'b1' }], next_cursor: 'cur-2' }))
      .mockResolvedValueOnce(jsonResponse(200, { items: [{ id: 'c1' }], next_cursor: null }))
    vi.stubGlobal('fetch', fetchMock)

    const rows = await listAll<{ id: string }>('tok', '/v1/orgs/o1/payment-links', { orgHint: 'o1' })

    expect(rows.map((r) => r.id)).toEqual(['a1', 'a2', 'b1', 'c1'])
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      expect.stringContaining('/v1/orgs/o1/payment-links'),
      expect.anything(),
    )
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      expect.stringContaining('after=cur-1'),
      expect.anything(),
    )
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      expect.stringContaining('after=cur-2'),
      expect.anything(),
    )
  })

  it('returns page 1 as-is when there is no cursor and accepts bare arrays', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, [{ id: 'x' }]))
    vi.stubGlobal('fetch', fetchMock)
    await expect(listAll<{ id: string }>('tok', '/v1/orgs/o1/payments')).resolves.toEqual([{ id: 'x' }])
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('stops at maxPages so a pathological cursor cannot loop forever', async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(async () => jsonResponse(200, { items: [{ id: 'row' }], next_cursor: 'again' }))
    vi.stubGlobal('fetch', fetchMock)

    const rows = await listAll<{ id: string }>('tok', '/v1/orgs/o1/receipts', undefined, { maxPages: 3 })

    expect(rows).toHaveLength(3)
    expect(fetchMock).toHaveBeenCalledTimes(3)
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
