import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const repo = join(root, '..', '..')

function src(): string {
  return readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
}

function pay(): string {
  return readFileSync(join(root, 'src', 'pay.ts'), 'utf8')
}

describe('checkout honesty', () => {
  it('has no OIDC dependency', () => {
    const pkg = readFileSync(join(root, 'package.json'), 'utf8')
    expect(pkg).not.toContain('oidc-client-ts')
    expect(pkg).not.toContain('react-oidc-context')
    expect(pkg).not.toContain('@repo/api-types-ts')
  })

  it('does not render wallet tiles or card PAN', () => {
    const text = src().toLowerCase()
    expect(text).not.toMatch(/grabpay|tng|touchngo|boost|duitnow|fpx|shopee/)
    expect(src()).not.toContain('autocomplete="cc-number"')
  })

  it('verifying query is not paid', () => {
    expect(pay()).toContain("=== 'verifying'")
    expect(src()).toContain("pay.status === 'paid'")
  })

  it('polls public GET while verifying', () => {
    expect(src()).toContain('/v1/pay/')
    expect(src()).toContain('setInterval')
  })

  it('does not treat customer@example.com as satisfying email_required', () => {
    expect(src()).toContain('customer@example.com')
    expect(pay()).toContain('usableEmail')
    expect(src()).toContain('This processor needs an email (not customer@example.com).')
    expect(src()).toContain('Use your real email.')
    expect(src()).toContain('aria-required')
  })

  it('test processor copy is not a wallet tile', () => {
    expect(src()).toContain("pay.provider === 'test'")
    expect(src()).toContain('No card, no secret')
  })

  it('uses copied aura-ui card chrome not a Hub portal', () => {
    expect(src()).toContain('Card')
    expect(src()).toContain('Payment received')
    expect(src()).toContain('Link expired')
    expect(src()).toContain('Link is full')
    expect(src()).toContain('no remaining seats')
    expect(src()).not.toContain('no remaining payments')
    expect(src()).not.toContain('lazuar-portal')
  })

  it('sends a local slot_key so one browser is one payer on a shared link', () => {
    expect(pay()).toContain('lazuar-pay-slot:')
    expect(pay()).toContain('localStorage')
    expect(src()).toContain('slot_key')
    expect(src()).toContain("pay.status === 'full'")
  })

  it('paints a retry card when boot GET is not 404', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src).toContain("Can't reach Pay")
    expect(src).toContain('Retry')
    expect(src).toContain("r.status === 404")
    expect(src).not.toContain('Sign in')
  })

  it('maps start 400 without calling it paid', () => {
    expect(src()).toContain('response.status === 400')
    expect(src()).not.toContain("status: 'paid'")
    expect(src()).not.toContain('callback base not public or email required')
  })

  it('does not default production API to localhost', () => {
    expect(pay()).toContain("import.meta.env.DEV")
    expect(pay()).not.toContain("?? 'http://localhost:8081'")
    const vite = readFileSync(join(root, 'vite.config.ts'), 'utf8')
    expect(vite).toContain('VITE_PAY_API_URL is required for production checkout builds')
  })

  it('strangers on a one-person paid link are not thanked', () => {
    expect(src()).toContain('already_paid')
    expect(src()).toContain('This link is already paid')
    expect(src()).toContain('pay.mine === false')
  })

  it('prefills payer fields from GET after cancel', () => {
    expect(src()).toContain('payer_name')
    expect(src()).toContain('payer_email')
    expect(src()).toContain('setName')
    expect(src()).toContain('usableEmail(body.payer_email)')
  })

  it('restarts verifying poll and offers return to pay', () => {
    expect(src()).toContain('pollNonce')
    expect(src()).toContain('Return to pay')
    expect(src()).toContain('replaceState')
  })

  it('does not poll a missing checkout', () => {
    expect(src()).toContain("error === 'missing'")
    expect(src()).toContain("r.status === 404")
    expect(src()).toContain("setError('missing')")
  })

  it('catches startPay network failure and a 200 with no redirect', () => {
    expect(src()).toContain('catch')
    expect(src()).toContain("Can't reach Pay")
    expect(src()).toContain('Processor did not return a pay URL')
  })

  it('anchors the checkout path regex', () => {
    expect(pay()).toContain('^\\/c\\/([^/]+)\\/?$')
  })

  it('gitignore excludes dist so preview is not a stale SPA', () => {
    const gitignore = readFileSync(join(repo, '.gitignore'), 'utf8')
    expect(gitignore).toMatch(/^dist\/$/m)
  })

  it('status cards use headings and live regions', () => {
    const card = readFileSync(join(root, 'src', 'ui', 'components', 'card.tsx'), 'utf8')
    expect(card).toContain('ComponentProps<"h1">')
    expect(src()).toContain('aria-live')
    expect(src()).toContain('role="alert"')
    expect(src()).toContain('Confirming payment')
  })
})
