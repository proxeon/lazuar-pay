export const rails = ['test', 'stripe', 'chip', 'billplz', 'xendit', 'razorpay'] as const

export type Rail = (typeof rails)[number]

export const railLabel: Record<Rail, string> = {
  test: 'Test',
  stripe: 'Stripe',
  chip: 'CHIP',
  billplz: 'Billplz',
  xendit: 'Xendit',
  razorpay: 'Razorpay',
}

export const railCopy: Record<Rail, string> = {
  test: 'Local only. No secrets. Pay on the hosted link marks the checkout paid and writes an Official Receipt.',
  stripe: 'Hosted Checkout on Stripe. Cards on Stripe’s page. Official Receipt, not an e-invoice.',
  chip: 'Hosted CHIP page (FPX/wallets if enabled on the brand). Auto-debit later, not this program. Paste PEM from the CHIP dashboard — Pay does not register webhooks.',
  billplz: 'Reminder + hosted bill. We do not auto-debit. Callback must be public https (localhost will fail).',
  xendit: 'Hosted invoice. Wallets on Xendit’s page if you enabled them there. We do not auto-debit.',
  razorpay: 'Hosted payment link. Not e-mandate. We do not auto-debit.',
}

export const railBlurb: Record<Rail, string> = {
  test: 'No secrets. Pay marks the link paid.',
  stripe: 'Hosted Checkout. Cards on Stripe.',
  chip: 'Hosted CHIP page. Paste PEM from their dashboard.',
  billplz: 'Reminder + hosted bill. Public https callback.',
  xendit: 'Hosted invoice. Wallets on Xendit.',
  razorpay: 'Hosted payment link. Not e-mandate.',
}

export type Processor = {
  provider?: string
  last4?: string
  configured?: boolean
  capability?: string
  public_merchant_id?: string
  environment?: string
  webhook_configured?: boolean
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
