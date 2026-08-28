export type OccupancyRow = {
  status: string
  max_payers?: number | null
  paid_count?: number
  taken_count?: number
  unlimited?: boolean
}

export function occupancyOverCapacity(row: OccupancyRow): boolean {
  return row.status === 'over_capacity'
}

export function occupancyStatusLabel(row: OccupancyRow): string {
  if (row.status === 'over_capacity') return 'over capacity'
  if (row.status === 'full' && row.max_payers === 1 && (row.paid_count ?? 0) >= 1) return 'paid'
  return row.status
}

export function occupancyPayersLabel(row: OccupancyRow): string {
  const taken = row.taken_count ?? 0
  if (row.unlimited || row.max_payers == null) {
    return taken === 0 ? 'Unlimited' : `${taken} started · unlimited`
  }
  return `${taken} / ${row.max_payers}`
}
