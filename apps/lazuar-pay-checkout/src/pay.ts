const memorySlots = new Map<string, string>()

export type SlotStore = Pick<Storage, 'getItem' | 'setItem'>

export function payApiOrigin(): string {
  const raw = (import.meta.env.VITE_PAY_API_URL as string | undefined)?.trim()
  if (raw) return raw.replace(/\/+$/, '')
  if (import.meta.env.DEV) return 'http://localhost:8081'
  throw new Error('VITE_PAY_API_URL is required')
}

export const payApi = payApiOrigin()

export function slotKey(token: string, stores: SlotStore[] = defaultStores()): string {
  const cached = memorySlots.get(token)
  if (cached) return cached

  const key = `lazuar-pay-slot:${token}`
  for (const store of stores) {
    try {
      const existing = store.getItem(key)
      if (existing) {
        memorySlots.set(token, existing)
        return existing
      }
    } catch {
      // private mode / blocked storage — try the next store
    }
  }

  const next = crypto.randomUUID()
  memorySlots.set(token, next)
  for (const store of stores) {
    try {
      store.setItem(key, next)
      break
    } catch {
      // keep the in-memory key for this document
    }
  }
  return next
}

export function tokenFromPath(pathname?: string): string | null {
  const path = pathname ?? (typeof window === 'undefined' ? '' : window.location.pathname)
  const m = path.match(/^\/c\/([^/]+)\/?$/)
  if (!m) return null
  try {
    const token = decodeURIComponent(m[1])
    if (!token || token.includes('/') || token.includes('..')) return null
    return token
  } catch {
    return null
  }
}

export function verifyingQuery(search?: string): boolean {
  const raw = search ?? (typeof window === 'undefined' ? '' : window.location.search)
  return new URLSearchParams(raw).get('status') === 'verifying'
}

export function usableEmail(value: string): boolean {
  const trimmed = value.trim()
  return trimmed.length > 0 && trimmed.toLowerCase() !== 'customer@example.com'
}

export function payPath(token: string): string {
  return `${payApi}/v1/pay/${encodeURIComponent(token)}?slot_key=${encodeURIComponent(slotKey(token))}`
}

function defaultStores(): SlotStore[] {
  const stores: SlotStore[] = []
  try {
    if (typeof localStorage !== 'undefined') stores.push(localStorage)
  } catch {
    // ignore
  }
  try {
    if (typeof sessionStorage !== 'undefined') stores.push(sessionStorage)
  } catch {
    // ignore
  }
  return stores
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
