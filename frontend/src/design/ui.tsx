import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { studentCardTone } from './studentCardTones'
import { ds } from './system'

const bannerFactTone = {
  blue: 'border-[#82aedb]/50 bg-[#4988C4]/30 text-[#EAF6FF]',
  amber: 'border-[#f3d19a]/45 bg-[#c26a12]/28 text-[#FFF4E4]',
  green: 'border-[#9ddec4]/45 bg-[#16836a]/28 text-[#E7FFF6]',
  violet: 'border-[#c4afe8]/45 bg-[#6d4cc4]/28 text-[#F3ECFF]',
} as const

export function BannerFact({
  icon: Icon,
  children,
  tone = 'blue',
}: {
  icon?: LucideIcon
  children: ReactNode
  tone?: keyof typeof bannerFactTone
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-semibold',
        bannerFactTone[tone],
      )}
    >
      {Icon ? <Icon className="size-3.5 shrink-0 opacity-95" aria-hidden="true" /> : null}
      {children}
    </span>
  )
}

type PageBannerProps = {
  eyebrow?: ReactNode
  icon?: LucideIcon
  title: ReactNode
  description?: ReactNode
  extras?: ReactNode
  scene?: ReactNode
  className?: string
  children?: ReactNode
}

/** Banner trang chuẩn (cùng shell với cổng giảng viên). Không đặt nút trên banner. */
export function PageBanner({
  eyebrow,
  icon: Icon,
  title,
  description,
  extras,
  scene,
  className,
  children,
}: PageBannerProps) {
  const hasScene = Boolean(scene || children)

  return (
    <section className={cn(ds.banner, className)}>
      <div
        className="pointer-events-none absolute inset-0 opacity-70"
        style={{
          backgroundImage:
            'linear-gradient(rgba(255,255,255,0.08) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.08) 1px, transparent 1px)',
          backgroundSize: '28px 28px',
        }}
      />
      <div className="pointer-events-none absolute -top-28 -right-20 size-80 rounded-full bg-[#57dcff]/12 blur-3xl" />
      <div className="pointer-events-none absolute -bottom-32 left-[34%] size-80 rounded-full bg-[#7357ff]/9 blur-3xl" />

      <div
        className={cn(
          'relative',
          hasScene && 'grid lg:grid-cols-[minmax(0,1.25fr)_minmax(280px,0.7fr)]',
        )}
      >
        <div className="relative z-10 flex flex-col justify-center p-6 sm:p-8">
          {eyebrow != null && eyebrow !== false ? (
            <span className={ds.bannerChip}>
              {Icon ? <Icon className="size-4" aria-hidden="true" /> : null}
              {eyebrow}
            </span>
          ) : null}
          <h1 className={ds.bannerTitle}>{title}</h1>
          {description ? <p className={ds.bannerText}>{description}</p> : null}
          {extras ? <div className="mt-4 flex flex-wrap gap-2">{extras}</div> : null}
        </div>

        {hasScene ? (
          <div
            className="relative min-h-[150px] overflow-hidden lg:min-h-full"
            aria-hidden={scene || children ? undefined : true}
          >
            {scene}
            {children}
          </div>
        ) : null}
      </div>
    </section>
  )
}

/** Alias tương thích — cùng shell với PageBanner. */
export function LecturerPageBanner({
  eyebrow,
  icon,
  title,
  description,
  extras,
  scene,
  className,
}: {
  eyebrow: ReactNode
  icon?: LucideIcon
  title: ReactNode
  description?: ReactNode
  extras?: ReactNode
  scene?: ReactNode
  className?: string
}) {
  return (
    <PageBanner
      eyebrow={eyebrow}
      icon={icon}
      title={title}
      description={description}
      extras={extras}
      scene={scene}
      className={className}
    />
  )
}

/** Header section chuẩn: icon + title + badge cùng hàng; hint dưới căn theo icon. */
export function SectionHeader({
  icon: Icon,
  title,
  badge,
  hint,
  action,
  className,
  as: TitleTag = 'h2',
}: {
  icon?: LucideIcon
  title: ReactNode
  badge?: ReactNode
  hint?: ReactNode
  action?: ReactNode
  className?: string
  as?: 'h2' | 'h3' | 'div'
}) {
  const badgeNode =
    badge == null || badge === false
      ? null
      : typeof badge === 'string' || typeof badge === 'number' ? (
          <span className={ds.sectionBadge}>{badge}</span>
        ) : (
          badge
        )

  return (
    <div
      className={cn(
        ds.sectionHeader,
        action && 'flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between',
        className,
      )}
    >
      <div className={ds.sectionHeading}>
        <div className={ds.sectionTitleRow}>
          {Icon ? (
            <span className={ds.sectionIconWrap}>
              <Icon className={ds.sectionIcon} aria-hidden="true" />
            </span>
          ) : null}
          <TitleTag className={ds.sectionTitle}>{title}</TitleTag>
          {badgeNode}
        </div>
        {hint ? <p className={ds.sectionHint}>{hint}</p> : null}
      </div>
      {action}
    </div>
  )
}

export function SectionPanel({
  icon,
  title,
  badge,
  hint,
  action,
  children,
  className,
  bodyClassName,
}: {
  icon?: LucideIcon
  title: string
  badge?: ReactNode
  hint?: string
  action?: ReactNode
  children: ReactNode
  className?: string
  bodyClassName?: string
}) {
  return (
    <section className={cn(ds.section, className)}>
      <SectionHeader icon={icon} title={title} badge={badge} hint={hint} action={action} />
      <div className={cn(ds.sectionBody, bodyClassName)}>{children}</div>
    </section>
  )
}

/** Accent mist + line trên card mềm giảng viên. */
export function SoftCardAccent() {
  return (
    <div className={ds.softCardAccentWrap} aria-hidden="true">
      <div className={ds.softCardAccentGlow} />
      <div className={ds.softCardAccentLine} />
    </div>
  )
}

/** Trang trí orb mềm cho card sinh viên — không dùng line màu. */
export function StudentColorCardAccent({
  seed,
}: {
  seed: string | number
}) {
  const { orbA, orbB } = studentCardTone(seed)
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden rounded-[21px]" aria-hidden="true">
      <div className={cn('absolute -top-14 -right-10 size-40 rounded-full blur-2xl', orbA)} />
      <div className={cn('absolute -bottom-16 -left-12 size-36 rounded-full blur-2xl', orbB)} />
      <div className="absolute inset-x-0 top-0 h-20 bg-[linear-gradient(180deg,rgba(255,255,255,0.45)_0%,transparent_100%)]" />
    </div>
  )
}
