import { Link, useOutletContext } from 'react-router-dom'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../ui/components/card'
import { PageCanvas, PageHeader } from '../../layout/PageHeader'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { useEffect, useState } from 'react'
import { payFetch } from '../../lib/payApi'
import { isRail, railLabel, type Processor } from '../../lib/processors'

export function OverviewPage() {
  const { orgId, tenant, token, write } = useOutletContext<OrgOutletContext>()
  const [processors, setProcessors] = useState<Processor[]>([])

  useEffect(() => {
    payFetch(token, `/v1/orgs/${orgId}/gateways`, { orgHint: orgId })
      .then(async (r) => {
        if (!r.ok) return
        const body = (await r.json()) as { processors?: Processor[] }
        setProcessors(body.processors ?? [])
      })
      .catch(() => undefined)
  }, [orgId, token])

  const onFile = processors.filter((p) => p.configured)

  return (
    <PageCanvas>
      <PageHeader
        title={tenant.name ?? orgId}
        subtitle="Hosted pay links. Official Receipts. No SST, no e-invoice."
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Processors</CardTitle>
            <CardDescription>Vault only. Pay links pick a rail at mint. Capability is hosted_link.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>
              <span className="text-slate-500">On file </span>
              {onFile.length === 0 ? 'none' : `${onFile.length}`}
            </p>
            {onFile.map((p) => (
              <p key={p.provider}>
                <code>{isRail(p.provider) ? railLabel[p.provider] : p.provider}</code>
                {p.last4 ? <span className="text-slate-500"> · …{p.last4}</span> : null}
                <span className="text-slate-500">
                  {' '}
                  · webhook {p.webhook_configured ? 'on file' : 'not set'}
                </span>
              </p>
            ))}
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
