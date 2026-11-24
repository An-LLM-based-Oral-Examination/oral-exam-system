import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

interface AdminPageHeaderProps {
  icon: LucideIcon
  title: string
  description?: string
  count?: number | string
  meta?: ReactNode
  actions?: ReactNode
  className?: string
}

export function AdminPageHeader({
  icon: Icon,
  title,
  description,
  count,
  meta,
  actions,
  className,
}: AdminPageHeaderProps) {
  return (
    <header
      className={cn(
        'flex flex-col gap-4 rounded-2xl border border-[#b6cde2] bg-white px-5 py-4 shadow-[0_1px_3px_rgba(15,40,84,0.06)] lg:flex-row lg:items-center lg:justify-between sm:px-6',
        className,
      )}
    >
      <div className="flex min-w-0 items-start gap-3.5">
        <span className="mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-xl border border-[#94b8da] bg-[#edf5fd] text-[#1C4D8D]">
          <Icon className="size-5" strokeWidth={1.75} aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-xl leading-tight font-bold tracking-[-0.02em] text-[#0F2854] sm:text-2xl">
              {title}
            </h1>
            {count != null ? (
              <span className="inline-flex items-center rounded-full border border-[#94b8da] bg-[#edf5fd] px-2.5 py-0.5 text-xs font-semibold text-[#1C4D8D] tabular-nums">
                {typeof count === 'number' ? count.toLocaleString('vi-VN') : count}
              </span>
            ) : null}
          </div>
          {description ? (
            <p className="mt-1 max-w-2xl text-sm leading-relaxed text-slate-600">{description}</p>
          ) : null}
          {meta ? <div className="mt-2 flex flex-wrap items-center gap-2">{meta}</div> : null}
        </div>
      </div>

      {actions ? (
        <div className="flex shrink-0 flex-wrap items-center gap-2.5">{actions}</div>
      ) : null}
    </header>
  )
}
