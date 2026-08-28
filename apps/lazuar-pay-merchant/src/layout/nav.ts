import { CreditCard, LayoutDashboard, Link2, Receipt, Webhook, Wallet } from 'lucide-react'
import type { AppSidebarNavGroup } from '../ui/components/app-sidebar'

export function getPayNavGroups(orgId: string): AppSidebarNavGroup[] {
  const base = `/o/${orgId}`
  return [
    {
      label: 'Money',
      items: [
        { name: 'Overview', icon: LayoutDashboard, path: `${base}/overview` },
        { name: 'Processor', icon: CreditCard, path: `${base}/gateway` },
        { name: 'Pay links', icon: Link2, path: `${base}/checkouts` },
        { name: 'Payments', icon: Wallet, path: `${base}/payments` },
        { name: 'Receipts', icon: Receipt, path: `${base}/receipts` },
        { name: 'Webhooks', icon: Webhook, path: `${base}/webhooks` },
      ],
    },
  ]
}
