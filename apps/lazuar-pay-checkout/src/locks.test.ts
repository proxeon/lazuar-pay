import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')

describe('checkout honesty', () => {
  it('has no OIDC dependency', () => {
    const pkg = readFileSync(join(root, 'package.json'), 'utf8')
    expect(pkg).not.toContain('oidc-client-ts')
    expect(pkg).not.toContain('react-oidc-context')
    expect(pkg).not.toContain('@repo/api-types-ts')
  })

  it('does not render wallet tiles or card PAN', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src.toLowerCase()).not.toMatch(/grabpay|tng|touchngo|boost|duitnow|fpx|shopee/)
    expect(src).not.toContain('autocomplete="cc-number"')
  })

  it('verifying query is not paid', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src).toContain("=== 'verifying'")
    expect(src).toContain("pay.status === 'paid'")
  })

  it('polls public GET while verifying', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src).toContain('/v1/pay/')
    expect(src).toContain('setInterval')
  })

  it('does not treat customer@example.com as satisfying email_required', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src).toContain('customer@example.com')
    expect(src).toContain('usableEmail')
  })

  it('test processor copy is not a wallet tile', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src).toContain("pay.provider === 'test'")
    expect(src).toContain('No card, no secret')
  })

  it('uses copied aura-ui card chrome not a Hub portal', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src).toContain('Card')
    expect(src).toContain('Payment received')
    expect(src).toContain('Link expired')
    expect(src).toContain('Link is full')
    expect(src).toContain('no remaining seats')
    expect(src).not.toContain('no remaining payments')
    expect(src).not.toContain('lazuar-portal')
  })

  it('sends a local slot_key so one browser is one payer on a shared link', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src).toContain('lazuar-pay-slot:')
    expect(src).toContain('localStorage')
    expect(src).toContain('slot_key')
    expect(src).toContain("pay.status === 'full'")
  })

  it('maps start 400 without calling it paid', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src).toContain('response.status === 400')
    expect(src).not.toContain("status: 'paid'")
    expect(src).not.toContain('callback base not public or email required')
  })
})
