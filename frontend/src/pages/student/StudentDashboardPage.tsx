import {
  lazy,
  Suspense,
  useEffect,
  useMemo,
  useState,
  type PointerEvent as ReactPointerEvent,
} from 'react'
import { Link } from 'react-router-dom'
import {
  ArrowRight,
  BookOpenCheck,
  CalendarDays,
  CheckCircle2,
  ChevronRight,
  Clock3,
  Code2,
  Database,
  Gauge,
  Mic2,
  Network,
  ShieldCheck,
  Sparkles,
  Target,
  TrendingUp,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'

const StudentOverviewThreeScene = lazy(() =>
  import('./StudentOverviewThreeScene').then((module) => ({
    default: module.StudentOverviewThreeScene,
  })),
)

interface OverviewStat {
  label: string
  value: string
  suffix?: string
  helper: string
  icon: LucideIcon
  iconClassName: string
  trend?: string
}

interface RecentActivity {
  id: string
  type: 'practice' | 'mock' | 'guided'
  typeLabel: string
  title: string
  completedLabel: string
  score: number
  icon: LucideIcon
  destination: string
}

const OVERVIEW_STATS: OverviewStat[] = [
  {
    label: 'Môn học kích hoạt',
    value: '04',
    suffix: 'Môn',
    helper: '2 bài kiểm tra sắp diễn ra',
    icon: BookOpenCheck,
    iconClassName: 'bg-[#eef3ff] text-[#3568df]',
  },
  {
    label: 'Phiên luyện tập đã hoàn thành',
    value: '18',
    suffix: 'Phiên',
    helper: '86.5% đánh giá đạt chuẩn',
    icon: Mic2,
    iconClassName: 'bg-[#ebeefe] text-[#5b62e8]',
  },
  {
    label: 'Điểm trung bình AI',
    value: '8.8',
    suffix: '/10',
    helper: '+0.4 so với tuần trước',
    trend: '+0.4',
    icon: Gauge,
    iconClassName: 'bg-[#eef8f7] text-[#168f86]',
  },
]

const RECENT_ACTIVITIES: RecentActivity[] = [
  {
    id: 'practice-networking',
    type: 'practice',
    typeLabel: 'Luyện tập tự do',
    title: 'Mạng máy tính: Giao thức TCP/UDP',
    completedLabel: 'Hoàn thành 2 giờ trước',
    score: 9.0,
    icon: Network,
    destination: '/practice',
  },
  {
    id: 'mock-database',
    type: 'mock',
    typeLabel: 'Thi thử Mock Exam',
    title: 'Cơ sở dữ liệu: Chuẩn hóa 3NF/BCNF',
    completedLabel: 'Hoàn thành hôm qua',
    score: 8.5,
    icon: Database,
    destination: '/mock-exam',
  },
  {
    id: 'practice-oop',
    type: 'guided',
    typeLabel: 'Luyện tập theo bộ',
    title: 'Lập trình hướng đối tượng: Đa hình',
    completedLabel: 'Hoàn thành 3 ngày trước',
    score: 9.2,
    icon: Code2,
    destination: '/practice',
  },
  {
    id: 'practice-design-patterns',
    type: 'practice',
    typeLabel: 'Luyện tập tự do',
    title: 'Mẫu thiết kế: Strategy và Factory Method',
    completedLabel: 'Hoàn thành 5 ngày trước',
    score: 8.7,
    icon: Target,
    destination: '/practice',
  },
  {
    id: 'mock-web-api',
    type: 'mock',
    typeLabel: 'Thi thử Mock Exam',
    title: 'Phát triển Web API: REST và bảo mật JWT',
    completedLabel: 'Hoàn thành 1 tuần trước',
    score: 8.9,
    icon: ShieldCheck,
    destination: '/mock-exam',
  },
]

function getGreeting(hour: number) {
  if (hour >= 5 && hour < 12) return 'Chào buổi sáng'
  if (hour >= 12 && hour < 18) return 'Chào buổi chiều'
  return 'Chào buổi tối'
}

function getSemesterLabel(date: Date) {
  const month = date.getMonth() + 1
  const semester = month <= 4 ? 'Spring' : month <= 8 ? 'Summer' : 'Fall'
  return `${semester} ${date.getFullYear()}`
}

function getNextExamDate(now: Date) {
  const nextExam = new Date(now)
  nextExam.setDate(nextExam.getDate() + 1)
  nextExam.setHours(14, 0, 0, 0)
  return nextExam
}

function formatExamDate(date: Date) {
  return new Intl.DateTimeFormat('vi-VN', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
  }).format(date)
}

function getActivityBadgeClass(type: RecentActivity['type']) {
  if (type === 'mock') return 'bg-[#fff5df] text-[#9a6411]'
  if (type === 'guided') return 'bg-[#f1edff] text-[#6546bd]'
  return 'bg-[#eaf2ff] text-[#2759c8]'
}

export function StudentDashboardPage() {
  const { user } = useAuth()
  const [now, setNow] = useState(() => new Date())
  const [showAllActivities, setShowAllActivities] = useState(false)

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  const nextExam = useMemo(() => getNextExamDate(now), [now])
  const displayedActivities = showAllActivities ? RECENT_ACTIVITIES : RECENT_ACTIVITIES.slice(0, 3)
  const studentName = user?.fullName ?? 'Sinh viên'
  const studentIdentifier = user?.studentCode ?? user?.email ?? user?.id ?? 'Student'

  const handleHeroPointerMove = (event: ReactPointerEvent<HTMLElement>) => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

    const rect = event.currentTarget.getBoundingClientRect()
    const x = ((event.clientX - rect.left) / rect.width - 0.5) * 2
    const y = ((event.clientY - rect.top) / rect.height - 0.5) * 2

    event.currentTarget.style.setProperty('--hero-scene-x', `${x * 8}px`)
    event.currentTarget.style.setProperty('--hero-scene-y', `${y * 6}px`)
    event.currentTarget.style.setProperty('--hero-grid-x', `${x * -3}px`)
    event.currentTarget.style.setProperty('--hero-grid-y', `${y * -2}px`)
  }

  const resetHeroParallax = (event: ReactPointerEvent<HTMLElement>) => {
    event.currentTarget.style.setProperty('--hero-scene-x', '0px')
    event.currentTarget.style.setProperty('--hero-scene-y', '0px')
    event.currentTarget.style.setProperty('--hero-grid-x', '0px')
    event.currentTarget.style.setProperty('--hero-grid-y', '0px')
  }

  return (
    <div className="space-y-4 sm:space-y-5">
      <section
        className="relative isolate overflow-hidden rounded-[22px] border border-[#dce5f1] bg-white shadow-[0_16px_50px_rgba(30,65,115,0.08)]"
        onPointerMove={handleHeroPointerMove}
        onPointerLeave={resetHeroParallax}
      >
        <div
          className="pointer-events-none absolute inset-0 opacity-70 transition-transform duration-300 ease-out motion-reduce:transform-none"
          style={{
            backgroundImage:
              'linear-gradient(rgba(69,104,164,0.035) 1px, transparent 1px), linear-gradient(90deg, rgba(69,104,164,0.035) 1px, transparent 1px)',
            backgroundSize: '28px 28px',
            transform:
              'translate3d(var(--hero-grid-x, 0px), var(--hero-grid-y, 0px), 0) scale(1.03)',
          }}
        />
        <div className="pointer-events-none absolute -top-24 -right-20 size-80 rounded-full bg-[#57dcff]/10 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-28 left-[35%] size-72 rounded-full bg-[#5674ef]/8 blur-3xl" />

        <div className="relative grid min-h-[310px] lg:grid-cols-[minmax(0,1.35fr)_minmax(310px,0.65fr)]">
          <div className="relative z-10 flex flex-col justify-center px-5 py-6 sm:px-7 sm:py-8 xl:px-9">
            <div className="mb-3 flex flex-wrap items-center gap-x-2 gap-y-1 text-[10px] font-medium text-[#6f7f95]">
              <span className="inline-flex items-center gap-1.5 rounded-full border border-[#dce8f6] bg-[#f5f9ff] px-2.5 py-1 font-semibold text-[#225bc9]">
                <Sparkles className="size-3" />
                Học kỳ {getSemesterLabel(now)}
              </span>
              <span className="text-[#bdc6d3]">•</span>
              <span>Khoa Công nghệ Phần mềm</span>
              <span className="text-[#bdc6d3]">•</span>
              <span className="inline-flex items-center gap-1 text-[#25856e]">
                <span className="size-1.5 rounded-full bg-[#35c992]" />
                AI sẵn sàng
              </span>
            </div>

            <div className="max-w-[690px]">
              <p className="text-[10px] font-semibold tracking-[0.08em] text-[#57708f] uppercase">
                Hệ thống vấn đáp tự động hóa AI
              </p>
              <h1 className="mt-2 text-[clamp(1.55rem,3vw,2.35rem)] leading-[1.12] font-bold tracking-[-0.045em] text-[#0b1830]">
                {getGreeting(now.getHours())},{' '}
                <span className="block text-[#1858d8] sm:inline">{studentName}</span>
              </h1>
              <p className="mt-2 max-w-[640px] text-[12px] leading-relaxed text-[#5f6f85] sm:text-[13px]">
                Sẵn sàng cho chu kỳ đánh giá khẩu vấn học thuật định kỳ. Tiếp tục luyện tập, kiểm
                tra lịch thi và theo dõi phản hồi AI trong một không gian thống nhất.
              </p>
              <p className="mt-1 text-[10px] font-medium text-[#8b98aa]">
                Mã sinh viên: {studentIdentifier}
              </p>
            </div>

            <div className="mt-5 flex max-w-[660px] flex-col gap-3 sm:flex-row sm:items-center">
              <div className="flex min-w-0 flex-1 items-center gap-3 rounded-xl border border-[#e1e8f2] bg-white/84 p-3 shadow-[0_6px_20px_rgba(40,73,120,0.055)] backdrop-blur-md">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#eaf0ff] text-[#225be0]">
                  <CalendarDays className="size-[18px]" />
                </div>
                <div className="min-w-0">
                  <p className="text-[9px] font-bold tracking-[0.08em] text-[#8b96a8] uppercase">
                    Lịch thi gần nhất
                  </p>
                  <p className="truncate text-[11px] font-bold text-[#25344b] sm:text-[12px]">
                    Kiến trúc Máy tính & Hệ điều hành — Phòng Lab B402
                  </p>
                  <p className="mt-0.5 flex items-center gap-1 text-[9px] font-semibold text-[#2860d9]">
                    <Clock3 className="size-3" />
                    14:00 · {formatExamDate(nextExam)} · Kiosk 08
                  </p>
                </div>
              </div>

              <Link
                to="/exam?device-check=1"
                className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-4 text-[11px] font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.24)] transition hover:-translate-y-0.5 hover:bg-[#194fcf] hover:shadow-[0_14px_30px_rgba(36,94,234,0.3)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
              >
                Kiểm tra phòng thi & thiết bị
                <ArrowRight className="size-3.5" />
              </Link>
            </div>
          </div>

          <div className="relative min-h-[230px] overflow-hidden lg:min-h-full">
            <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_48%,rgba(76,206,245,0.16),transparent_48%)]" />
            <div
              className="absolute inset-[-8%] transition-transform duration-200 ease-out motion-reduce:transform-none"
              style={{
                transform: 'translate3d(var(--hero-scene-x, 0px), var(--hero-scene-y, 0px), 0)',
              }}
            >
              <Suspense
                fallback={
                  <div className="flex size-full items-center justify-center" aria-hidden="true">
                    <div className="size-28 animate-pulse rounded-full border border-[#7cdff4]/40 bg-[#dffaff]/30" />
                  </div>
                }
              >
                <StudentOverviewThreeScene />
              </Suspense>
            </div>
            <div className="pointer-events-none absolute right-5 bottom-4 hidden items-center gap-2 rounded-full border border-[#d8e8f3] bg-white/72 px-3 py-1.5 text-[9px] font-semibold text-[#5b708c] shadow-sm backdrop-blur-md sm:flex">
              <span className="size-1.5 animate-pulse rounded-full bg-[#38cfa0] motion-reduce:animate-none" />
              AI Voice Core đang hoạt động
            </div>
          </div>
        </div>
      </section>

      <section aria-label="Tóm tắt học tập" className="grid gap-3 md:grid-cols-3">
        {OVERVIEW_STATS.map(
          ({ label, value, suffix, helper, icon: Icon, iconClassName, trend }) => (
            <article
              key={label}
              className="group rounded-2xl border border-[#e1e7f0] bg-white p-4 shadow-[0_7px_26px_rgba(31,61,105,0.045)] transition duration-200 hover:-translate-y-0.5 hover:border-[#cfdbeb] hover:shadow-[0_12px_32px_rgba(31,61,105,0.08)] sm:p-5"
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-[10px] font-medium text-[#64748b]">{label}</p>
                  <div className="mt-3 flex items-baseline gap-1.5">
                    <strong className="text-2xl font-bold tracking-[-0.045em] text-[#0b1830] sm:text-[1.7rem]">
                      {value}
                    </strong>
                    {suffix && (
                      <span className="text-[10px] font-semibold text-[#42536b]">{suffix}</span>
                    )}
                  </div>
                </div>
                <div
                  className={cn(
                    'flex size-9 items-center justify-center rounded-xl',
                    iconClassName,
                  )}
                >
                  <Icon className="size-4" />
                </div>
              </div>
              <p className="mt-1.5 flex items-center gap-1 text-[9px] font-medium text-[#758399]">
                {trend ? (
                  <TrendingUp className="size-3 text-[#1d9a78]" />
                ) : (
                  <span className="size-1.5 rounded-full bg-[#4c8df6]" />
                )}
                <span className={cn(trend && 'text-[#167e65]')}>{helper}</span>
              </p>
            </article>
          ),
        )}
      </section>

      <section className="overflow-hidden rounded-2xl border border-[#e1e7f0] bg-white shadow-[0_8px_30px_rgba(31,61,105,0.05)]">
        <div className="flex items-start justify-between gap-4 border-b border-[#edf1f6] px-4 py-4 sm:items-center sm:px-5">
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-sm font-bold text-[#17243a] sm:text-[15px]">
                Hoạt động luyện tập gần đây
              </h2>
              <span className="hidden rounded-full bg-[#edf3ff] px-2 py-0.5 text-[9px] font-semibold text-[#2c5dc8] sm:inline">
                {RECENT_ACTIVITIES.length} phiên
              </span>
            </div>
            <p className="mt-1 text-[10px] text-[#7b889b]">
              Lịch sử đánh giá giọng nói và phản hồi rubric gần nhất
            </p>
          </div>

          <button
            type="button"
            onClick={() => setShowAllActivities((isShowingAll) => !isShowingAll)}
            className="inline-flex min-h-9 shrink-0 items-center gap-1 rounded-lg px-2 text-[10px] font-semibold text-[#245bd2] transition hover:bg-[#f0f5ff] focus-visible:outline-2 focus-visible:outline-[#245eea]"
            aria-expanded={showAllActivities}
          >
            {showAllActivities ? 'Thu gọn' : 'Xem tất cả'}
            <ChevronRight
              className={cn('size-3.5 transition-transform', showAllActivities && 'rotate-90')}
            />
          </button>
        </div>

        <div className="divide-y divide-[#edf1f6] px-4 sm:px-5">
          {displayedActivities.map((activity) => {
            const Icon = activity.icon

            return (
              <article
                key={activity.id}
                className="group grid gap-3 py-3 transition sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center"
              >
                <div className="flex min-w-0 items-center gap-3">
                  <div className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-[#e4e9f1] bg-[#f8faff] text-[#3264d4] transition group-hover:border-[#cddaf0] group-hover:bg-[#eef4ff]">
                    <Icon className="size-4" />
                  </div>
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span
                        className={cn(
                          'rounded-md px-2 py-0.5 text-[8px] font-bold',
                          getActivityBadgeClass(activity.type),
                        )}
                      >
                        {activity.typeLabel}
                      </span>
                      <span className="text-[9px] text-[#8a96a8]">{activity.completedLabel}</span>
                    </div>
                    <h3 className="mt-1 truncate text-[11px] font-bold text-[#26364d] sm:text-[12px]">
                      {activity.title}
                    </h3>
                  </div>
                </div>

                <div className="flex items-center justify-between gap-4 pl-12 sm:justify-end sm:pl-0">
                  <p className="text-[10px] text-[#69778b]">
                    Điểm AI:{' '}
                    <strong className="text-[12px] text-[#173c83]">
                      {activity.score.toFixed(1)}
                    </strong>
                    <span className="text-[9px]"> /10</span>
                  </p>
                  <Link
                    to={activity.destination}
                    className="inline-flex min-h-8 items-center gap-1 rounded-lg bg-[#f3f6fc] px-3 text-[9px] font-semibold text-[#3361c5] transition hover:bg-[#e8effc] hover:text-[#194dbf] focus-visible:outline-2 focus-visible:outline-[#245eea]"
                    aria-label={`Xem nhận xét cho ${activity.title}`}
                  >
                    Xem nhận xét
                    <ChevronRight className="size-3" />
                  </Link>
                </div>
              </article>
            )
          })}
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[#edf1f6] bg-[#fafcff] px-4 py-3 text-[9px] text-[#77869a] sm:px-5">
          <span className="inline-flex items-center gap-1.5">
            <CheckCircle2 className="size-3.5 text-[#20a47e]" />
            Dữ liệu được đồng bộ từ các phiên luyện tập gần nhất
          </span>
          <Link
            to="/results"
            className="inline-flex items-center gap-1 font-semibold text-[#245bd2] hover:underline"
          >
            Mở trang kết quả
            <ArrowRight className="size-3" />
          </Link>
        </div>
      </section>

      <div className="sr-only" aria-live="polite">
        {showAllActivities
          ? 'Đang hiển thị toàn bộ hoạt động'
          : 'Đang hiển thị ba hoạt động gần nhất'}
      </div>
    </div>
  )
}

export { StudentDashboardPage as StudentOverviewPage }
