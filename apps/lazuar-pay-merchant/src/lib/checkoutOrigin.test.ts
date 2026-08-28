import { describe, expect, it } from 'vitest'
import { buyerPayUrl, resolveCheckoutOrigin } from './checkoutOrigin'

describe('resolveCheckoutOrigin', () => {
  it('uses the env when set and strips a trailing slash', () => {
    expect(resolveCheckoutOrigin('https://pay.example/', true)).toBe('https://pay.example')
    expect(resolveCheckoutOrigin('http://localhost:5179', false)).toBe('http://localhost:5179')
  })

  it('keeps the local 5179 fallback only outside production', () => {
    expect(resolveCheckoutOrigin(undefined, false)).toBe('http://localhost:5179')
    expect(resolveCheckoutOrigin('  ', false)).toBe('http://localhost:5179')
  })

  it('fails closed in production when the env is empty', () => {
    expect(resolveCheckoutOrigin(undefined, true)).toBeNull()
    expect(resolveCheckoutOrigin('', true)).toBeNull()
  })
})

describe('buyerPayUrl', () => {
  it('builds /c/{token} on the origin', () => {
    expect(buyerPayUrl('tok', 'http://localhost:5179')).toBe('http://localhost:5179/c/tok')
  })
})
