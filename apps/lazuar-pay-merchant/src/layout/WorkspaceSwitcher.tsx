import { Check, ChevronsUpDown, Plus } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import type { WhoamiTenant } from '../lib/payApi'
import { setOrgHint } from '../lib/sessionKeys'
import { Button } from '../ui/components/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../ui/components/dropdown-menu'

export function WorkspaceSwitcher({
  orgId,
  tenants,
}: {
  orgId: string
  tenants: WhoamiTenant[]
}) {
  const navigate = useNavigate()
  const current = tenants.find((t) => t.id === orgId)
  const locationName = current?.name ?? current?.slug ?? orgId

  function openOrg(id: string) {
    setOrgHint(id)
    void navigate(`/o/${id}/overview`)
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className="group flex h-auto w-full items-center justify-between gap-2 rounded-lg border border-white/20 bg-white/10 px-3 py-2.5 text-left text-white shadow-none hover:bg-white/15 hover:text-white data-[state=open]:bg-white/15 data-[state=open]:text-white"
          aria-label="Switch workspace"
        >
          <div className="flex min-w-0 flex-col text-left">
            <span className="truncate text-[11px] font-medium leading-tight text-sky-100/85">
              Lazuar Pay
            </span>
            <span className="truncate text-sm font-semibold leading-snug text-white">
              {locationName}
            </span>
          </div>
          <ChevronsUpDown className="h-4 w-4 shrink-0 text-sky-100/70 group-hover:text-white" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        className="w-[var(--radix-dropdown-menu-trigger-width)] min-w-56 rounded-lg border-slate-200 bg-white p-1.5 text-slate-900 shadow-lg"
        align="start"
        side="bottom"
        sideOffset={6}
      >
        <p className="px-2.5 py-1.5 text-xs font-medium text-slate-500">Workspaces</p>
        {tenants.map((t) => {
          const selected = t.id === orgId
          return (
            <DropdownMenuItem
              key={t.id}
              className="cursor-pointer rounded-md px-2.5 py-2 text-sm font-medium"
              onClick={() => openOrg(t.id)}
            >
              <Check className={`mr-2 h-4 w-4 ${selected ? 'opacity-100' : 'opacity-0'}`} />
              <span className="min-w-0 flex-1 truncate">{t.name ?? t.slug ?? t.id}</span>
            </DropdownMenuItem>
          )
        })}
        <DropdownMenuSeparator className="my-1.5" />
        <DropdownMenuItem
          className="cursor-pointer rounded-md px-2.5 py-2 text-sm font-medium"
          onClick={() => navigate('/workspaces/new')}
        >
          <Plus className="mr-2 h-4 w-4 text-slate-400" />
          Create workspace
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
