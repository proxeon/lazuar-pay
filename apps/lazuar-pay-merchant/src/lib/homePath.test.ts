import { describe, expect, it } from 'vitest'
import { dashboardPath, resolvePostLoginPath } from './homePath'
import type { WhoamiTenant } from './payApi'

const t = (id: string): WhoamiTenant => ({ id, name: id, role: 'owner', status: 'active' })

describe('dashboardPath', () => {
  it('creates a workspace when the member list is empty', () => {
    expect(dashboardPath([], null)).toBe('/workspaces/new')
  })

  it('uses the first tenant when the last org is not in tenants', () => {
    expect(dashboardPath([t('b'), t('c')], 'a')).toBe('/o/b/overview')
  })

  it('uses the last org when still a member', () => {
    expect(dashboardPath([t('a'), t('b')], 'b')).toBe('/o/b/overview')
  })
})

describe('resolvePostLoginPath', () => {
  it('ignores a returnTo org that is not in tenants', () => {
    expect(resolvePostLoginPath('/o/a/overview', [t('b')], 'a')).toBe('/o/b/overview')
  })

  it('keeps a returnTo deep-link when the caller is still a member', () => {
    expect(resolvePostLoginPath('/o/b/checkouts', [t('b')], null)).toBe('/o/b/checkouts')
  })

  it('keeps a non-org safe path', () => {
    expect(resolvePostLoginPath('/workspaces/new', [], null)).toBe('/workspaces/new')
  })
})
