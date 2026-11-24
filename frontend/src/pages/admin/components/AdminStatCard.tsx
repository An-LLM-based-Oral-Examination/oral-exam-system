import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

interface AdminStatCardProps {
  label: string
  value: string | number
  meta?: string
  icon: LucideIcon
  to?: string
  linkLabel?: string
  tone?: 'default' | 'warning' | 'danger' | 'success'
  className?: string
  footerExtra?: ReactNode
}

const TONE_ICON_WRAP = {
  default: 'border-[#94b8da] bg-[#edf5fd] text-[#1C4D8D]',
  warning: 'border-amber-300 bg-amber-50 text-amber-800',
  danger: 'border-rose-300 bg-rose-50 text-rose-700',
  success: 'border-emerald-300 bg-emerald-50 text-emerald-800',
} as const

export function AdminStatCard({
  label,
  value,
  meta,
  icon: Icon,
  to,
  linkLabel,
  tone = 'default',
  className,
  footerExtra,
}: AdminStatCardProps) {
  return (
    <article
      className={cn(
        'flex flex-col justify-between rounded-2xl border border-[#b6cde2] bg-white p-5 shadow-[0_1px_3px_rgba(15,40,84,0.06)] transition-colors duration-150 hover:border-[#7ca7d1] motion-reduce:transition-none',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-[13px] font-semibold text-slate-600">{label}</p>
          <p className="mt-2 text-3xl leading-none font-bold tracking-tight text-[#0F2854] tabular-nums">
            {value}
          </p>
          {meta ? <p className="mt-2 text-xs font-medium text-slate-600 tabular-nums">{meta}</p> : null}
        </div>
        <span
          className={cn(
            'flex size-10 shrink-0 items-center justify-center rounded-xl border',
            TONE_ICON_WRAP[tone],
          )}
        >
          <Icon className="size-5" strokeWidth={1.75} aria-hidden="true" />
        </span>
      </div>

      {(to && linkLabel) || footerExtra ? (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 pt-3">
          {footerExtra}
          {to && linkLabel ? (
            <Link
              to={to}
              className="inline-flex min-h-11 items-center gap-1.5 text-[13px] font-semibold text-[#1C4D8D] transition-colors duration-150 hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none sm:min-h-8"
            >
              {linkLabel}
              <ArrowRight className="size-4" strokeWidth={1.75} aria-hidden="true" />
            </Link>
          ) : null}
        </div>
      ) : null}
    </article>
  )
}
