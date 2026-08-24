import { useOutletContext } from 'react-router-dom'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { CreateWorkspaceForm } from '../CreateWorkspaceForm'

export function OrgCreateWorkspacePage() {
  const { token } = useOutletContext<OrgOutletContext>()
  return <CreateWorkspaceForm token={token} />
}
