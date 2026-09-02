import { describe, expect, it } from 'vitest'
import { parseAmountInput } from './money'

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
