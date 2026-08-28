const LOCAL_FALLBACK = 'http://localhost:5179'

/** Buyer SPA origin. Production builds must set VITE_CHECKOUT_ORIGIN. */
export function resolveCheckoutOrigin(configured: string | undefined, isProd: boolean): string | null {
  const raw = configured?.trim().replace(/\/$/, '')
  if (raw) return raw
  if (isProd) return null
  return LOCAL_FALLBACK
}

export function buyerPayUrl(publicToken: string, origin: string): string {
  return `${origin}/c/${publicToken}`
}
