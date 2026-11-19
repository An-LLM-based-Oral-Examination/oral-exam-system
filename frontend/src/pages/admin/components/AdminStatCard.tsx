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
  default: 'border-[#0F2854]/10 bg-[#F4FBFD] text-[#1C4D8D]',
  warning: 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]',
  danger: 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]',
  success: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]',
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
        'flex flex-col justify-between rounded-2xl border border-[#0F2854]/10 bg-white p-5',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium text-[#5b6b84]">{label}</p>
          <p className="mt-2 text-2xl leading-none font-semibold text-[#0F2854] tabular-nums">
            {value}
          </p>
          {meta ? <p className="mt-2 text-xs text-[#5b6b84] tabular-nums">{meta}</p> : null}
        </div>
        <span
          className={cn(
            'flex size-10 shrink-0 items-center justify-center rounded-xl border',
            TONE_ICON_WRAP[tone],
          )}
        >
          <Icon className="size-[18px]" strokeWidth={1.5} aria-hidden="true" />
        </span>
      </div>

      {(to && linkLabel) || footerExtra ? (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-[#0F2854]/8 pt-3">
          {footerExtra}
          {to && linkLabel ? (
            <Link
              to={to}
              className="inline-flex min-h-11 items-center gap-1.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none sm:min-h-8"
            >
              {linkLabel}
              <ArrowRight className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
            </Link>
          ) : null}
        </div>
      ) : null}
    </article>
  )
}
