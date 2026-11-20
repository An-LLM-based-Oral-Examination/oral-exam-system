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

const calmBannerFactTone = {
  blue: 'border-[#0F2854]/10 bg-[#F4FBFD] text-[#1C4D8D]',
  amber: 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]',
  green: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]',
  violet: 'border-[#0F2854]/10 bg-[#F8FBFD] text-[#0F2854]',
} as const

export function BannerFact({
  icon: Icon,
  children,
  tone = 'blue',
  surface = 'banner',
}: {
  icon?: LucideIcon
  children: ReactNode
  tone?: keyof typeof bannerFactTone
  surface?: 'banner' | 'calm'
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 border px-3 py-1 text-xs font-medium',
        surface === 'calm'
          ? cn('rounded-lg', calmBannerFactTone[tone])
          : cn('rounded-full py-1.5 font-semibold', bannerFactTone[tone]),
      )}
    >
      {Icon ? <Icon className="size-3.5 shrink-0 opacity-95" strokeWidth={1.5} aria-hidden="true" /> : null}
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
  sceneClassName?: string
  className?: string
  children?: ReactNode
  variant?: 'default' | 'calm'
}

/** Banner trang chuẩn (cùng shell với cổng giảng viên). Không đặt nút trên banner. */
export function PageBanner({
  eyebrow,
  icon: Icon,
  title,
  description,
  extras,
  scene,
  sceneClassName,
  className,
  children,
  variant = 'default',
}: PageBannerProps) {
  const hasScene = Boolean(scene || children)
  const isCalm = variant === 'calm'

  return (
    <section
      className={cn(
        isCalm
          ? 'relative overflow-hidden rounded-2xl border border-[#0F2854]/10 bg-white'
          : ds.banner,
        className,
      )}
    >
      {!isCalm ? (
        <div className="pointer-events-none absolute inset-0 overflow-hidden rounded-[inherit]" aria-hidden="true">
          <div
            className="absolute inset-0 opacity-70"
            style={{
              backgroundImage:
                'linear-gradient(rgba(255,255,255,0.08) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.08) 1px, transparent 1px)',
              backgroundSize: '28px 28px',
            }}
          />
          <div className="pointer-events-none absolute -top-28 -right-20 size-80 rounded-full bg-[#57dcff]/12 blur-3xl" />
          <div className="pointer-events-none absolute -bottom-32 left-[34%] size-80 rounded-full bg-[#7357ff]/9 blur-3xl" />
        </div>
      ) : null}

      <div
        className={cn(
          'relative',
          hasScene && 'grid lg:grid-cols-[minmax(0,1.25fr)_minmax(280px,0.7fr)]',
        )}
      >
        <div className={cn('relative z-10 flex flex-col justify-center', isCalm ? 'p-5 sm:p-6' : 'p-6 sm:p-8')}>
          {eyebrow != null && eyebrow !== false ? (
            <span
              className={
                isCalm
                  ? 'inline-flex w-fit items-center gap-2 rounded-lg border border-[#0F2854]/10 bg-[#F4FBFD] px-2.5 py-1 text-xs font-medium text-[#1C4D8D]'
                  : ds.bannerChip
              }
            >
              {Icon ? <Icon className="size-4" strokeWidth={1.5} aria-hidden="true" /> : null}
              {eyebrow}
            </span>
          ) : null}
          <h1
            className={
              isCalm
                ? 'mt-3 max-w-3xl text-xl leading-tight font-semibold tracking-[-0.02em] text-[#0F2854] sm:text-2xl'
                : ds.bannerTitle
            }
          >
            {title}
          </h1>
          {description ? (
            <p className={isCalm ? 'mt-2 max-w-3xl text-sm leading-relaxed text-[#5b6b84]' : ds.bannerText}>
              {description}
            </p>
          ) : null}
          {extras ? <div className="mt-4 flex flex-wrap gap-2">{extras}</div> : null}
        </div>

        {hasScene ? (
          <div
            className={cn('relative min-h-[150px] overflow-hidden lg:min-h-full', sceneClassName)}
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
  variant = 'default',
}: {
  icon?: LucideIcon
  title: ReactNode
  badge?: ReactNode
  hint?: ReactNode
  action?: ReactNode
  className?: string
  as?: 'h2' | 'h3' | 'div'
  variant?: 'default' | 'calm'
}) {
  const isCalm = variant === 'calm'
  const badgeNode =
    badge == null || badge === false
      ? null
      : typeof badge === 'string' || typeof badge === 'number' ? (
          <span
            className={
              isCalm
                ? 'rounded-md border border-[#0F2854]/10 bg-white px-2 py-0.5 text-xs font-medium text-[#1C4D8D] tabular-nums'
                : ds.sectionBadge
            }
          >
            {badge}
          </span>
        ) : (
          badge
        )

  return (
    <div
      className={cn(
        isCalm
          ? 'rounded-t-2xl border-b border-[#0F2854]/10 bg-[#F8FBFD] px-5 py-3.5 sm:px-6'
          : ds.sectionHeader,
        action && 'flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between',
        className,
      )}
    >
      <div className={ds.sectionHeading}>
        <div className={ds.sectionTitleRow}>
          {Icon ? (
            <span
              className={
                isCalm
                  ? 'flex size-8 shrink-0 items-center justify-center rounded-lg border border-[#0F2854]/10 bg-white text-[#1C4D8D]'
                  : ds.sectionIconWrap
              }
            >
              <Icon className={ds.sectionIcon} strokeWidth={1.5} aria-hidden="true" />
            </span>
          ) : null}
          <TitleTag
            className={
              isCalm
                ? 'text-lg font-semibold tracking-[-0.01em] text-[#0F2854] sm:text-xl'
                : ds.sectionTitle
            }
          >
            {title}
          </TitleTag>
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
