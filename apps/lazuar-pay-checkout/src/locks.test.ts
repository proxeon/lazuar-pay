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
})
