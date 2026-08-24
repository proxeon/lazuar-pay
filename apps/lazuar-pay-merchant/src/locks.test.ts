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
    expect(src).toContain('setEnvironment(body.environment)')
  })

  it('org shell uses copied AppSidebar not Aura ops nav', () => {
    const layout = readFileSync(join(root, 'src', 'layout', 'OrgLayout.tsx'), 'utf8')
    const nav = readFileSync(join(root, 'src', 'layout', 'nav.ts'), 'utf8')
    expect(layout).toContain('AppSidebar')
    expect(layout).toContain('WorkspaceSwitcher')
    expect(nav).toContain('Processor')
    expect(nav).not.toContain('Appointments')
  })

  it('sidebar header can switch or create workspace', () => {
    const src = readFileSync(join(root, 'src', 'layout', 'WorkspaceSwitcher.tsx'), 'utf8')
    expect(src).toContain('Create workspace')
    expect(src).toContain('Switch workspace')
    expect(src).toContain('/workspaces/new')
  })

  it('home redirects into last org dashboard', () => {
    const src = readFileSync(join(root, 'src', 'lib', 'homePath.ts'), 'utf8')
    expect(src).toContain('/overview')
    expect(src).toContain('/workspaces/new')
  })

  it('slug pattern escapes hyphen for unicode-sets HTML pattern', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'CreateWorkspacePage.tsx'), 'utf8')
    expect(src).toContain('pattern="[a-z0-9\\\\-]{1,64}"')
  })
})
