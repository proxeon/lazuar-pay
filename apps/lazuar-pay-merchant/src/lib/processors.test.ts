import { describe, expect, it } from 'vitest'
import {
  defaultMintRail,
  hostListsTest,
  readyMintRails,
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
  })
})
