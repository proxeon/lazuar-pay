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
  if (!Number.isFinite(value) || value <= 0 || value > 99_999_999) return null
  return value
}

/**
 * plans/031/07: display follows the charge currency — no picker, no assumptions.
 * - ISO currencies render via Intl; INR gets en-IN (lakh grouping) while the dashboard
 *   default stays en-MY until another market demands otherwise.
 * - USDC is not ISO-4217 (Intl currency style throws for it), so it formats as a plain
 *   number with 2-6 decimals — exact against Solana's 6-decimal atomic value — plus a
 *   literal code suffix. Never rounded to 2 dp on the buyer page: the QR and the
 *   on-chain transfer are the source of truth.
 * - Any other non-ISO code falls back to `<amount> <CODE>`.
 */
export function formatMoney(amount: number, currency: string): string {
  const code = currency.trim().toUpperCase()
  if (code === 'USDC') {
    const formatted = new Intl.NumberFormat('en-MY', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 6,
    }).format(amount)
    return `${formatted} USDC`
  }

  const locale = code === 'INR' ? 'en-IN' : 'en-MY'
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency: code }).format(amount)
  } catch {
    return `${amount} ${code}`
  }
}
