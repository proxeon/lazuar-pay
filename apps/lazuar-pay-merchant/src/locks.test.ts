import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')

function walkSrc(): string[] {
  const out: string[] = []
  function walk(dir: string) {
    for (const name of readdirSync(dir)) {
      const p = join(dir, name)
      if (statSync(p).isDirectory()) walk(p)
      else if (/\.(ts|tsx|css)$/.test(name) && !name.includes('.test.'))
        out.push(p)
    }
  }
  walk(join(root, 'src'))
  return out
}

describe('merchant honesty locks', () => {
  it('has no password form or Hub login', () => {
    const blob = walkSrc()
      .map((p) => readFileSync(p, 'utf8'))
      .join('\n')
    expect(blob).not.toMatch(/type=["']password["']/)
    expect(blob).not.toContain('/one/auth/login')
    expect(blob).not.toContain('lazuar_auth')
  })

  it('package.json does not depend on Hub types', () => {
    const pkg = readFileSync(join(root, 'package.json'), 'utf8')
    expect(pkg).not.toContain('@repo/api-types-ts')
    expect(pkg).not.toContain('@repo/aura-ui')
    expect(pkg).not.toContain('lazuar-ops')
  })

  it('CHIP PEM uses a textarea', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    expect(src).toContain('Textarea')
    expect(src).toContain('PEM from CHIP dashboard')
  })

  it('hydrates environment from GET', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    expect(src).toContain('setEnvironment(row.environment)')
    expect(src).toContain('/gateways')
  })

  it('processor vault is cards not an org default rail', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    expect(src).toContain('CardTitle')
    expect(src).not.toContain('aspect-square')
    expect(src).not.toContain('One active rail')
    expect(src).toContain('does not pick the rail for pay links')
  })

  it('processor secrets open from Edit into a dialog', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    expect(src).toContain('Edit')
    expect(src).toContain('DialogContent')
    expect(src).toContain('openEdit')
  })

  it('test processor has no secret editor', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    expect(src).toContain("r === 'test'")
    expect(src).toContain('No keys. Use this on Pay links.')
    const processors = readFileSync(join(root, 'src', 'lib', 'processors.ts'), 'utf8')
    expect(processors).toContain("'test'")
  })

  it('receipts table uses the same chrome as pay links', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'ReceiptsPage.tsx'), 'utf8')
    expect(src).toContain('rounded-xl border border-slate-200')
    expect(src).toContain('uppercase tracking-wider')
    expect(src).toContain('No receipts yet')
    expect(src).toContain('Official Receipt')
    expect(src).not.toContain('CardContent')
  })

  it('payments table uses the same chrome as pay links', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'PaymentsPage.tsx'), 'utf8')
    expect(src).toContain('rounded-xl border border-slate-200')
    expect(src).toContain('uppercase tracking-wider')
    expect(src).toContain('No payments yet')
    expect(src).toContain('formatMoney')
    expect(src).not.toContain('CardContent')
  })

  it('pay links send a chosen provider', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'CheckoutsPage.tsx'), 'utf8')
    expect(src).toContain('provider')
    expect(src).toContain('/gateways')
    expect(src).toContain('/payment-links')
    expect(src).toContain("'test'")
    expect(src).toContain('Create pay link')
    expect(src).toContain('DialogContent')
    expect(src).toContain('Table')
    expect(src).toContain('unlimited')
    expect(src).toContain('max_payers')
    expect(src).toContain('1 person only')
    expect(src).toContain('The link closes after one person starts Pay. Unpaid starts free after 30 minutes.')
    expect(src).not.toContain('The link closes after one successful payment.')
    expect(src).toContain('started · unlimited')
  })

  it('mint rails trust the host list and default to the first real rail', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'CheckoutsPage.tsx'), 'utf8')
    expect(src).toContain('readyMintRails')
    expect(src).toContain('defaultMintRail')
    expect(src).not.toContain('withTest')
    expect(src).not.toContain("useState<Rail | ''>('test')")
    expect(src).toContain("useState<Rail | ''>('')")
    expect(src).toContain('pay_url')
    expect(src).toContain('resolveCheckoutOrigin')
    expect(src).toContain('VITE_CHECKOUT_ORIGIN is required in production')
    expect(src).toContain('Buyer URL origin:')
  })

  it('overview On file excludes Test', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'OverviewPage.tsx'), 'utf8')
    expect(src).toContain('/gateways')
    expect(src).not.toContain('Active rail')
    expect(src).toContain('On file')
    expect(src).toContain('vaultedNonTest')
    expect(src).toContain('Test is always available')
  })

  it('processor cards show Test only when the host listed it', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    expect(src).toContain('visibleRails')
    expect(src).not.toContain('{payApi}/v1/webhooks')
    expect(src).toContain('/v1/webhooks/')
    expect(src).toContain('Pay:PublicBaseUrl')
    expect(src).not.toContain('localhost:8081')
  })

  it('org shell uses copied AppSidebar not Aura ops nav', () => {
    const chrome = readFileSync(join(root, 'src', 'layout', 'DashboardChrome.tsx'), 'utf8')
    const nav = readFileSync(join(root, 'src', 'layout', 'nav.ts'), 'utf8')
    expect(chrome).toContain('AppSidebar')
    expect(chrome).toContain('WorkspaceSwitcher')
    expect(nav).toContain('Processor')
    expect(nav).not.toContain('Appointments')
  })

  it('sidebar header can switch or create workspace', () => {
    const src = readFileSync(join(root, 'src', 'layout', 'WorkspaceSwitcher.tsx'), 'utf8')
    expect(src).toContain('Create workspace')
    expect(src).toContain('Switch workspace')
    expect(src).toContain('/new')
  })

  it('home redirects into last org dashboard', () => {
    const src = readFileSync(join(root, 'src', 'lib', 'homePath.ts'), 'utf8')
    expect(src).toContain('/overview')
    expect(src).toContain('/workspaces/new')
    expect(src).toContain('resolvePostLoginPath')
  })

  it('silent renew does not reuse the login callback page', () => {
    const oidc = readFileSync(join(root, 'src', 'auth', 'oidcConfig.ts'), 'utf8')
    const silent = readFileSync(join(root, 'src', 'auth', 'silentRenew.ts'), 'utf8')
    expect(oidc).toContain('silent_redirect_uri')
    expect(oidc).toContain('/silent-renew.html')
    expect(oidc).not.toMatch(/silent_redirect_uri[^\n]*\/callback/)
    expect(silent).toContain('signinSilentCallback')
    expect(silent).not.toContain('takeReturnTo')
    expect(silent).not.toContain('Navigate')
  })

  it('money pages do not duplicate the chrome h1 and the user menu is not Settings', () => {
    const chrome = readFileSync(join(root, 'src', 'layout', 'DashboardChrome.tsx'), 'utf8')
    const checkouts = readFileSync(join(root, 'src', 'pages', 'org', 'CheckoutsPage.tsx'), 'utf8')
    const gateway = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    const payments = readFileSync(join(root, 'src', 'pages', 'org', 'PaymentsPage.tsx'), 'utf8')
    const receipts = readFileSync(join(root, 'src', 'pages', 'org', 'ReceiptsPage.tsx'), 'utf8')
    expect(chrome).not.toContain('onSettingsClick')
    expect(checkouts).not.toContain('title="Pay links"')
    expect(gateway).not.toContain('title="Processor"')
    expect(payments).not.toContain('title="Payments"')
    expect(receipts).not.toContain('title="Receipts"')
  })

  it('membership miss and first workspace include Sign out', () => {
    const org = readFileSync(join(root, 'src', 'layout', 'OrgLayout.tsx'), 'utf8')
    const create = readFileSync(join(root, 'src', 'pages', 'CreateWorkspacePage.tsx'), 'utf8')
    expect(org).toContain('Switch workspace')
    expect(org).toContain('Sign out')
    expect(org).toContain('signoutRedirect')
    expect(create).toContain('Sign out')
    expect(create).toContain('signoutRedirect')
  })

  it('does not set the org hint until membership is confirmed', () => {
    const org = readFileSync(join(root, 'src', 'layout', 'OrgLayout.tsx'), 'utf8')
    const callback = readFileSync(join(root, 'src', 'pages', 'CallbackPage.tsx'), 'utf8')
    expect(org).toContain('if (match)')
    expect(org).toContain('setOrgHint(orgId)')
    expect(callback).toContain('resolvePostLoginPath')
    expect(callback).toContain('takeReturnToOnce')
  })

  it('slug pattern escapes hyphen for unicode-sets HTML pattern', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'CreateWorkspaceForm.tsx'), 'utf8')
    expect(src).toContain('pattern="[a-z0-9\\\\-]{1,64}"')
  })

  it('create workspace form uses card chrome', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'CreateWorkspaceForm.tsx'), 'utf8')
    expect(src).toContain('Card')
    expect(src).toContain('workspace_name')
  })

  it('org shell redirects when the access token is not a JWT or whoami is 401', () => {
    const org = readFileSync(join(root, 'src', 'layout', 'OrgLayout.tsx'), 'utf8')
    const requireAuth = readFileSync(join(root, 'src', 'auth', 'RequireAuth.tsx'), 'utf8')
    expect(org).toContain('signinRedirect')
    expect(org).toContain("message === 'unauthorized'")
    expect(org).toContain('setReturnTo')
    expect(org).toContain('Signing in…')
    expect(requireAuth).toContain('pickApiBearerToken')
    expect(requireAuth).toContain('signinRedirect')
    expect(requireAuth).not.toContain('id_token')
  })

  it('list GETs surface host detail instead of the empty illustration', () => {
    const pages = [
      'OverviewPage.tsx',
      'GatewayPage.tsx',
      'CheckoutsPage.tsx',
      'PaymentsPage.tsx',
      'ReceiptsPage.tsx',
    ].map((name) => readFileSync(join(root, 'src', 'pages', 'org', name), 'utf8'))
    for (const src of pages) {
      expect(src).toContain('listError')
      expect(src).toContain('role="alert"')
      expect(src).toContain('Pay unreachable')
    }
    const checkouts = pages[2]!
    expect(checkouts).toContain('No pay links yet')
    expect(checkouts).toContain('listError && links.length === 0')
    const payments = pages[3]!
    expect(payments).toContain('No payments yet')
    expect(payments).toContain('listError && payments.length === 0')
    const receipts = pages[4]!
    expect(receipts).toContain('No receipts yet')
    expect(receipts).toContain('listError && receipts.length === 0')
  })

  it('writer busy flags clear in finally and mint names a leftover product', () => {
    const gateway = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    const checkouts = readFileSync(join(root, 'src', 'pages', 'org', 'CheckoutsPage.tsx'), 'utf8')
    expect(gateway).toContain('finally')
    expect(gateway).toContain('setSaving(false)')
    expect(checkouts).toContain('finally')
    expect(checkouts).toContain('setBusy(false)')
    expect(checkouts).toContain('A product was created. Pay link failed:')
  })
})
