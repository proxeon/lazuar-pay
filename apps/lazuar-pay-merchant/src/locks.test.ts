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
  })

  it('overview lists processors not a single active rail', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'OverviewPage.tsx'), 'utf8')
    expect(src).toContain('/gateways')
    expect(src).not.toContain('Active rail')
    expect(src).toContain('On file')
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
})
