import { describe, expect, it } from 'vitest'
import { formatMoney, parseAmountInput } from './money'

describe('parseAmountInput', () => {
  it('accepts plain and cent-quoted amounts', () => {
    expect(parseAmountInput('10')).toBe(10)
    expect(parseAmountInput('10.5')).toBe(10.5)
    expect(parseAmountInput(' 10.99 ')).toBe(10.99)
    expect(parseAmountInput('0.01')).toBe(0.01)
  })

  it('refuses the empty, zero, negative, and NaN shapes', () => {
    // Number('') was 0 and Number('abc') was NaN — both used to POST as-is.
    expect(parseAmountInput('')).toBeNull()
    expect(parseAmountInput('   ')).toBeNull()
    expect(parseAmountInput('0')).toBeNull()
    expect(parseAmountInput('0.00')).toBeNull()
    expect(parseAmountInput('abc')).toBeNull()
    expect(parseAmountInput('-5')).toBeNull()
    expect(parseAmountInput('1e3')).toBeNull()
  })

  it('refuses sub-cent precision the host stores as numeric(18,2)', () => {
    expect(parseAmountInput('10.000001')).toBeNull()
    expect(parseAmountInput('1.999')).toBeNull()
  })
})

describe('formatMoney', () => {
  it('formats MYR with the dashboard default locale', () => {
    // ICU inserts a non-breaking space (U+00A0) between the code and the amount —
    // normalize before comparing so the assertion reads like the rendered string.
    expect(formatMoney(10, 'MYR').replace(/\u00a0/g, ' ')).toBe('RM 10.00')
  })

  it('formats INR with Indian lakh grouping', () => {
    // plans/031/07: en-IN grouping (1,23,456) — the en-MY default would render 123,456.
    const rendered = formatMoney(123456.78, 'INR')
    expect(rendered).toContain('₹')
    expect(rendered).toContain('1,23,456.78')
  })

  it('formats USDC with 2-6 decimals and a literal code, never a symbol', () => {
    // USDC is not ISO-4217 — Intl currency style throws for it. 2-6 dp stays exact
    // against Solana's 6-decimal atomic value; never rounded to 2 dp for the buyer.
    expect(formatMoney(10, 'USDC')).toBe('10.00 USDC')
    expect(formatMoney(0.000001, 'usdc')).toBe('0.000001 USDC')
    expect(formatMoney(1234.5, 'USDC')).toBe('1,234.50 USDC')
    expect(formatMoney(10, 'USDC').startsWith('RM')).toBe(false)
    expect(formatMoney(10, 'USDC').startsWith('$')).toBe(false)
  })

  it('falls back to a literal code for malformed currency codes', () => {
    // Intl accepts arbitrary 3-letter codes (formats them with 2 dp) but throws on
    // malformed ones like 4-letter codes — the fallback covers those.
    expect(formatMoney(10, 'ABCD')).toBe('10 ABCD')
  })
})
