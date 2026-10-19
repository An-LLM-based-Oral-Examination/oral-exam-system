import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { ds } from './system'

export function PageBanner({
  eyebrow,
  icon: Icon,
  title,
  description,
  actions,
  className,
  children,
}: {
  eyebrow?: string
  icon?: LucideIcon
  title: string
  description?: string
  actions?: ReactNode
  className?: string
  children?: ReactNode
}) {
  return (
    <section className={cn(ds.banner, 'min-h-[205px]', className)}>
      <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(255,255,255,0.35)_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-40" />
      <div className="pointer-events-none absolute inset-y-0 right-0 w-[60%] bg-[radial-gradient(circle_at_72%_48%,rgba(189,232,245,0.22),transparent_46%)]" />
      <div className="relative z-10 flex min-h-[205px] flex-col justify-center gap-4 px-5 py-6 sm:px-7 lg:flex-row lg:items-end lg:justify-between lg:px-8">
        <div className="max-w-[720px]">
          {eyebrow && (
            <div className={ds.bannerChip}>
              {Icon && <Icon className="size-3.5" aria-hidden="true" />}
              {eyebrow}
            </div>
          )}
          <h1 className={ds.bannerTitle}>{title}</h1>
          {description && <p className={ds.bannerText}>{description}</p>}
        </div>
        {actions}
      </div>
      {children}
    </section>
  )
}

export function SectionPanel({
  icon: Icon,
  title,
  hint,
  action,
  children,
  className,
  bodyClassName,
}: {
  icon?: LucideIcon
  title: string
  hint?: string
  action?: ReactNode
  children: ReactNode
  className?: string
  bodyClassName?: string
}) {
  return (
    <section className={cn(ds.section, className)}>
      <div className={cn(ds.sectionHeader, action && 'flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between')}>
        <div>
          <div className={ds.sectionTitleRow}>
            {Icon && <Icon className={ds.sectionIcon} aria-hidden="true" />}
            <h2 className={ds.sectionTitle}>{title}</h2>
          </div>
          {hint && <p className={ds.sectionHint}>{hint}</p>}
        </div>
        {action}
      </div>
      <div className={cn(ds.sectionBody, bodyClassName)}>{children}</div>
    </section>
  )
}
