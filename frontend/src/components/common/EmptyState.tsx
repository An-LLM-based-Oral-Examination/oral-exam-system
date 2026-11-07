import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

interface EmptyStateProps {
  icon?: LucideIcon
  title: string
  description?: ReactNode
  action?: ReactNode
  className?: string
  /** Chiều cao tối thiểu khi nằm trong danh sách cao. */
  compact?: boolean
}

/**
 * Empty state chuẩn: khung viền nét đứt, icon, tiêu đề, mô tả, CTA tùy chọn.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className,
  compact = false,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center rounded-2xl border border-dashed border-[#82aedb]/55 bg-[#F8FBFD] px-6 text-center',
        compact ? 'py-6' : 'py-8',
        className,
      )}
      role="status"
    >
      {Icon ? <Icon className="mx-auto size-10 text-[#4988C4]" aria-hidden="true" /> : null}
      <h3 className={cn('text-lg leading-snug font-bold text-[#0F2854]', Icon ? 'mt-2' : null)}>
        {title}
      </h3>
      {description ? (
        <p className="mx-auto mt-1 max-w-md text-sm leading-snug text-[#5b6b84]">{description}</p>
      ) : null}
      {action ? <div className="mt-4 flex flex-wrap items-center justify-center gap-2">{action}</div> : null}
    </div>
  )
}
