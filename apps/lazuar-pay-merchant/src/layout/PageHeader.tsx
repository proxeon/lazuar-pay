import type { ReactNode } from 'react'

export function PageHeader({ title, subtitle }: { title?: string; subtitle?: string }) {
  return (
    <div className="min-w-0">
      {title ? (
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900 sm:text-[1.75rem]">
          {title}
        </h1>
      ) : null}
      {subtitle ? (
        <p className={`${title ? 'mt-1 ' : ''}max-w-2xl text-sm text-slate-500`}>{subtitle}</p>
      ) : null}
    </div>
  )
}

export function PageCanvas({ children }: { children: ReactNode }) {
  return (
    <div className="mx-auto max-w-[1000px] space-y-5 p-4 sm:space-y-6 sm:p-6 lg:p-8">
      {children}
    </div>
  )
}
