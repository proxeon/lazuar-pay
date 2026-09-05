export const rails = ['test', 'stripe', 'chip', 'billplz', 'xendit', 'razorpay', 'solana'] as const

export type Rail = (typeof rails)[number]

export const railLabel: Record<Rail, string> = {
  test: 'Test',
  stripe: 'Stripe',
  chip: 'CHIP',
  billplz: 'Billplz',
  xendit: 'Xendit',
  razorpay: 'Razorpay',
  solana: 'Solana',
}

export const railCopy: Record<Rail, string> = {
  test: 'Local only. No secrets. Pay on the hosted link marks the checkout paid and writes an Official Receipt.',
  stripe: 'Hosted Checkout on Stripe. Cards on Stripe’s page. Official Receipt, not an e-invoice.',
  chip: 'Hosted CHIP page (FPX/wallets if enabled on the brand). Auto-debit later, not this program. Paste PEM from the CHIP dashboard — Pay does not register webhooks.',
  billplz: 'Reminder + hosted bill. We do not auto-debit. Callback must be public https (localhost will fail).',
  xendit: 'Hosted invoice. Wallets on Xendit’s page if you enabled them there. We do not auto-debit.',
  razorpay: 'Hosted payment link. Not e-mandate. We do not auto-debit.',
  solana:
    'Solana Pay QR on the hosted checkout. USDC only. Paste the merchant receive address — a public wallet address, not a private key, not a PEM, not a Razorpay key_id. Pay does not import a wallet SDK and does not register a Solana program. Official Receipt, not an e-invoice. We do not auto-debit.',
}

export const railBlurb: Record<Rail, string> = {
  test: 'No secrets. Pay marks the link paid.',
  stripe: 'Hosted Checkout. Cards on Stripe.',
  chip: 'Hosted CHIP page. Paste PEM from their dashboard.',
  billplz: 'Reminder + hosted bill. Public https callback.',
  xendit: 'Hosted invoice. Wallets on Xendit.',
  razorpay: 'Hosted payment link. Not e-mandate.',
  solana: 'Solana Pay QR on checkout. USDC. Paste a receive address.',
}

export type Processor = {
  provider?: string
  last4?: string
  configured?: boolean
  capability?: string
  public_merchant_id?: string
  environment?: string
  webhook_configured?: boolean
  /** Server-declared settlement currency for NEW charges on this rail (RailCurrencies.Default). */
  currency?: string
}

export function isRail(value: string | undefined | null): value is Rail {
  return !!value && (rails as readonly string[]).includes(value)
}

/** Configured rails the host listed. Do not invent Test. */
export function readyMintRails(list: Processor[]): Processor[] {
  return list.filter((p) => p.configured && isRail(p.provider))
}

/** First vaulted non-test rail, else Test if the host listed it, else empty. */
export function defaultMintRail(ready: Processor[]): Rail | '' {
  const firstReal = ready.find((p) => p.provider !== 'test')?.provider
  const first = firstReal ?? ready[0]?.provider
  return isRail(first) ? first : ''
}

/** Vaulted secrets. Test is always configured when listed — it is not "on file". */
export function vaultedNonTest(list: Processor[]): Processor[] {
  return list.filter((p) => p.configured && p.provider !== 'test')
}

export function hostListsTest(list: Processor[]): boolean {
  return list.some((p) => p.provider === 'test')
}

export function visibleRails(list: Processor[]): Rail[] {
  return hostListsTest(list) ? [...rails] : rails.filter((r) => r !== 'test')
}

export function usesReceiveAddress(rail: Rail): boolean {
  return rail === 'solana'
}

export function usesCatalogProduct(rail: Rail): boolean {
  return !usesReceiveAddress(rail)
}

/**
 * The currency NEW charges on this rail quote. The server's /gateways answer
 * (RailCurrencies.Default) wins when a processor row carries it; the local mirror below
 * only covers the moment before that payload arrives. Issue 003 (issues/003): the mirror
 * used to say MYR for every card rail, but Razorpay settles INR only — the server
 * rejected every Razorpay pay link this dashboard tried to create.
 */
export function defaultCurrency(rail: Rail | '', processors?: Processor[]): string {
  const declared = processors
    ?.find((p) => p.provider === rail)
    ?.currency?.trim()
    ?.toUpperCase()
  if (declared) return declared
  if (rail === 'solana') return 'USDC'
  if (rail === 'razorpay') return 'INR'
  return 'MYR'
}

/**
 * Whether the create-link flow should mint a catalog product for this rail. Catalog
 * products are MYR-only server-side ("Bar B currency is MYR") and a pay link attached to
 * a product must match its price, so a rail that settles another currency (razorpay →
 * INR) must skip the product: the link would 400 on the price match and the product
 * would be orphaned. The label still travels on the pay link itself.
 */
export function createsCatalogProduct(rail: Rail | '', processors?: Processor[]): boolean {
  return rail !== '' && usesCatalogProduct(rail) && defaultCurrency(rail, processors) === 'MYR'
}
