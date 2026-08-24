import { describe, expect, it } from 'vitest'
import type { User } from 'oidc-client-ts'
import { staffDisplay } from './staffDisplay'
import type { Whoami } from './payApi'

const who = (over: Partial<Whoami> = {}): Whoami => ({
  user_id: '387725576103826436',
  is_platform_admin: false,
  tenants: [],
  ...over,
})

describe('staffDisplay', () => {
  it('prefers whoami email over numeric user_id', () => {
    const d = staffDisplay(who({ email: 'ada@acme.test', name: 'Ada' }))
    expect(d.email).toBe('ada@acme.test')
    expect(d.name).toBe('Ada')
  })

  it('uses OIDC profile email when whoami email is missing', () => {
    const user = { profile: { email: 'ada@acme.test', name: 'Ada Lovelace' } } as User
    const d = staffDisplay(who(), user)
    expect(d.email).toBe('ada@acme.test')
    expect(d.name).toBe('Ada Lovelace')
    expect(d.name).not.toBe('387725576103826436')
  })

  it('does not show a numeric Zitadel sub as the label', () => {
    const d = staffDisplay(who())
    expect(d.name).toBe('Signed in')
    expect(d.email).toBeNull()
  })
})
