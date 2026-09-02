/**
 * Free-text money input → chargeable amount, or null when it is not one.
 * Amounts are quoted to the cent (numeric(18,2) on the host), so anything
 * with more than 2 decimal places is refused here rather than silently
 * rounded by the database.
 */
export function parseAmountInput(raw: string): number | null {
  const text = raw.trim()
  if (!/^\d+(\.\d{1,2})?$/.test(text)) return null
  const value = Number(text)
  if (!Number.isFinite(value) || value <= 0) return null
  return value
}
