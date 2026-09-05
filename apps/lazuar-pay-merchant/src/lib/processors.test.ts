import { describe, expect, it } from 'vitest'
import {
  createsCatalogProduct,
  defaultCurrency,
  defaultMintRail,
  hostListsTest,
  readyMintRails,
  usesCatalogProduct,
  usesReceiveAddress,
  vaultedNonTest,
  visibleRails,
  type Processor,
} from './processors'

const stripe: Processor = { provider: 'stripe', configured: true, last4: '4242' }
const test: Processor = { provider: 'test', configured: true }
const emptyChip: Processor = { provider: 'chip', configured: false }

describe('readyMintRails', () => {
  it('does not invent Test when the host omitted it', () => {
    expect(readyMintRails([stripe, emptyChip])).toEqual([stripe])
    expect(readyMintRails([stripe, emptyChip]).some((p) => p.provider === 'test')).toBe(false)
  })

  it('keeps Test only when the host listed it', () => {
    expect(readyMintRails([test, stripe]).map((p) => p.provider)).toEqual(['test', 'stripe'])
  })
})

describe('defaultMintRail', () => {
  it('prefers the first real rail over Test', () => {
    expect(defaultMintRail([test, stripe])).toBe('stripe')
  })

  it('uses Test when that is the only ready rail', () => {
    expect(defaultMintRail([test])).toBe('test')
  })

  it('is empty when the host listed nothing configured', () => {
    expect(defaultMintRail(readyMintRails([emptyChip]))).toBe('')
    expect(defaultMintRail([])).toBe('')
  })
})

describe('vaultedNonTest', () => {
  it('does not count Test as on file', () => {
    expect(vaultedNonTest([test, stripe])).toEqual([stripe])
    expect(vaultedNonTest([test])).toEqual([])
  })
})

describe('defaultCurrency', () => {
  it('mirrors the server table — the create payload currency per rail', () => {
    // Issue 003 (issues/003): the mirror said MYR for razorpay, but that rail settles INR
    // only — every Razorpay pay link this dashboard created was rejected server-side.
    expect(defaultCurrency('stripe')).toBe('MYR')
    expect(defaultCurrency('chip')).toBe('MYR')
    expect(defaultCurrency('billplz')).toBe('MYR')
    expect(defaultCurrency('xendit')).toBe('MYR')
    expect(defaultCurrency('razorpay')).toBe('INR')
    expect(defaultCurrency('solana')).toBe('USDC')
    expect(defaultCurrency('test')).toBe('MYR')
    expect(defaultCurrency('')).toBe('MYR')
  })

  it('lets the server declaration win over the local mirror', () => {
    const razorpay: Processor = { provider: 'razorpay', configured: true, currency: 'INR' }
    expect(defaultCurrency('razorpay', [razorpay])).toBe('INR')
    expect(defaultCurrency('stripe', [{ provider: 'stripe', currency: 'usd ' }])).toBe('USD')
    expect(defaultCurrency('stripe', [{ provider: 'chip', currency: 'USD' }])).toBe('MYR')
  })
})

describe('createsCatalogProduct', () => {
  it('attaches a product only when the rail settles the MYR catalog currency', () => {
    expect(createsCatalogProduct('stripe')).toBe(true)
    expect(createsCatalogProduct('chip')).toBe(true)
    expect(createsCatalogProduct('billplz')).toBe(true)
    expect(createsCatalogProduct('xendit')).toBe(true)
    expect(createsCatalogProduct('test')).toBe(true)
    // INR links cannot match a MYR catalog price — attaching a product would 400 the
    // link create and orphan the product.
    expect(createsCatalogProduct('razorpay')).toBe(false)
    expect(createsCatalogProduct('solana')).toBe(false)
    expect(createsCatalogProduct('')).toBe(false)
  })

  it('follows a server-declared currency', () => {
    expect(createsCatalogProduct('stripe', [{ provider: 'stripe', currency: 'USD' }])).toBe(false)
    expect(createsCatalogProduct('razorpay', [{ provider: 'razorpay', currency: 'INR' }])).toBe(false)
  })
})

describe('visibleRails', () => {
  it('hides Test unless the host listed it', () => {
    expect(visibleRails([stripe])).not.toContain('test')
    expect(hostListsTest([stripe])).toBe(false)
    expect(visibleRails([test, stripe])).toContain('test')
  })

  it('treats Solana as a real rail', () => {
    const solana: Processor = { provider: 'solana', configured: true, last4: 'mmc1' }
    expect(readyMintRails([solana]).map((p) => p.provider)).toEqual(['solana'])
    expect(defaultMintRail([solana])).toBe('solana')
    expect(visibleRails([])).toContain('solana')
    expect(vaultedNonTest([solana, test])).toEqual([solana])
    expect(usesReceiveAddress('solana')).toBe(true)
    expect(usesCatalogProduct('solana')).toBe(false)
    expect(defaultCurrency('solana')).toBe('USDC')
    expect(usesReceiveAddress('stripe')).toBe(false)
    expect(usesCatalogProduct('stripe')).toBe(true)
    expect(defaultCurrency('stripe')).toBe('MYR')
  })
})
