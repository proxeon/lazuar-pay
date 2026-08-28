import { describe, expect, it } from 'vitest'
import { occupancyOverCapacity, occupancyPayersLabel, occupancyStatusLabel } from './occupancyDisplay'

describe('occupancyDisplay', () => {
  it('does not hide over-admit as full remaining 0', () => {
    const row = { status: 'over_capacity', max_payers: 1, taken_count: 2, paid_count: 2 }
    expect(occupancyOverCapacity(row)).toBe(true)
    expect(occupancyStatusLabel(row)).toBe('over capacity')
    expect(occupancyPayersLabel(row)).toBe('2 / 1')
  })

  it('still shows a one-person paid link as paid', () => {
    const row = { status: 'full', max_payers: 1, taken_count: 1, paid_count: 1 }
    expect(occupancyStatusLabel(row)).toBe('paid')
  })
})
