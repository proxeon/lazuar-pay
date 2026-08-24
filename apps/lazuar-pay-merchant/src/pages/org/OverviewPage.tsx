import { Link, useOutletContext } from 'react-router-dom'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../ui/components/card'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { useEffect, useState } from 'react'
import { payFetch } from '../../lib/payApi'

type Gateway = {
  provider?: string
  last4?: string
  configured?: boolean
  capability?: string
  webhook_configured?: boolean
}

export function OverviewPage() {
  const { orgId, tenant, token, write } = useOutletContext<OrgOutletContext>()
  const [gateway, setGateway] = useState<Gateway | null>(null)

  useEffect(() => {
    payFetch(token, `/v1/orgs/${orgId}/gateway`, { orgHint: orgId })
      .then(async (r) => {
        if (r.ok) setGateway((await r.json()) as Gateway)
      })
      .catch(() => undefined)
  }, [orgId, token])

  return (
    <PageCanvas>
      <PageHeader
        title={tenant.name ?? orgId}
        subtitle="Hosted pay links. Official Receipts. No SST, no e-invoice."
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Active rail</CardTitle>
            <CardDescription>One processor per org. Capability is hosted_link.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>
              <span className="text-slate-500">Provider </span>
              <code>{gateway?.configured ? gateway.provider : 'none'}</code>
            </p>
            {gateway?.last4 ? (
              <p>
                <span className="text-slate-500">Last4 </span>
                {gateway.last4}
              </p>
            ) : null}
            {gateway?.capability ? (
              <p>
                <span className="text-slate-500">Capability </span>
                {gateway.capability}
              </p>
            ) : null}
            <p>
              <span className="text-slate-500">Webhook secret </span>
              {gateway?.webhook_configured ? 'on file' : 'not set'}
            </p>
            {write ? (
              <p>
                <Link className="text-sky-700 underline-offset-2 hover:underline" to={`/o/${orgId}/gateway`}>
                  Paste keys
                </Link>
              </p>
            ) : (
              <p className="text-slate-500">Member can view. Cannot paste keys.</p>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Your role</CardTitle>
            <CardDescription>One tenant id is Pay org_id.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>
              <code>{tenant.role}</code> · {tenant.status}
            </p>
            <p className="text-slate-500">Switch or create a workspace from the sidebar header.</p>
          </CardContent>
        </Card>
      </div>
    </PageCanvas>
  )
}
