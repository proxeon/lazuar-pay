import { describe, expect, it } from 'vitest'
import { workspaceStatusBanner } from './workspaceStatus'
import type { WhoamiTenant } from './payApi'

const t = (status?: string): WhoamiTenant => ({
  id: 't1',
  name: 'Acme',
  role: 'owner',
  status,
})

describe('workspaceStatusBanner', () => {
  it('does not treat an active member as missing', () => {
    expect(workspaceStatusBanner(t('active'))).toBeNull()
  })

  it('names suspend instead of not-a-member', () => {
    expect(workspaceStatusBanner(t('suspended'))).toBe(
      'This workspace is suspended. Charges are paused.',
    )
  })
})
