import { describe, expect, it } from 'vitest'
import { slotKey, tokenFromPath, usableEmail, type SlotStore } from './pay'

describe('slotKey', () => {
  it('returns the same id when storage throws on every call', () => {
    const boom: SlotStore = {
      getItem() {
        throw new Error('blocked')
      },
      setItem() {
        throw new Error('blocked')
      },
    }
    const token = `blocked-${crypto.randomUUID()}`
    expect(slotKey(token, [boom])).toBe(slotKey(token, [boom]))
  })

  it('reuses a key written to storage', () => {
    const data = new Map<string, string>()
    const store: SlotStore = {
      getItem(key) {
        return data.get(key) ?? null
      },
      setItem(key, value) {
        data.set(key, value)
      },
    }
    const token = `store-${crypto.randomUUID()}`
    const first = slotKey(token, [store])
    const memoryMiss: SlotStore = {
      getItem(key) {
        return data.get(key) ?? null
      },
      setItem(key, value) {
        data.set(key, value)
      },
    }
    expect(slotKey(token, [memoryMiss])).toBe(first)
    expect(data.get(`lazuar-pay-slot:${token}`)).toBe(first)
  })
})

describe('tokenFromPath', () => {
  it('accepts /c/{token} and an optional trailing slash', () => {
    expect(tokenFromPath('/c/tok')).toBe('tok')
    expect(tokenFromPath('/c/tok/')).toBe('tok')
  })

  it('rejects extra path segments', () => {
    expect(tokenFromPath('/c/tok/receipt')).toBeNull()
    expect(tokenFromPath('/c/tok/extra')).toBeNull()
    expect(tokenFromPath('/pay/tok')).toBeNull()
  })

  it('treats malformed percent-escapes as missing', () => {
    expect(tokenFromPath('/c/%E0%A4%A')).toBeNull()
    expect(tokenFromPath('/c/%ZZ')).toBeNull()
  })
})

describe('usableEmail', () => {
  it('rejects empty and the Hub placeholder', () => {
    expect(usableEmail('')).toBe(false)
    expect(usableEmail('customer@example.com')).toBe(false)
    expect(usableEmail('Ada@acme.test')).toBe(true)
  })
})
