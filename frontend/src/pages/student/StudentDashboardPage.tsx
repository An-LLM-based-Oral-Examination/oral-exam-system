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
  Award,
  BarChart3,
  BookOpenCheck,
  CalendarDays,
  CheckCircle2,
  ChevronRight,
  Clock3,
  FileCheck2,
  Gauge,
  History,
  Mic2,
  TimerReset,
  TrendingUp,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'
import type { ProblemDetails } from '@/types/auth.types'
import {
  studentOverviewService,
  type CourseSummary,
  type MockExamQuota,
  type OfficialExamTicketOverview,
  type PracticeHistoryItem,
} from '@/services/studentOverview.service'
import { mockExamService } from '@/services/mockExam.service'
import type { MockExamHistoryItem } from '@/types/mockExam.types'

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
  cardClassName: string
  iconClassName: string
  badgeText: string
  badgeClassName: string
  trend?: string
}

interface RecentActivity {
  id: string
  type: 'practice' | 'mock' | 'guided'
  typeLabel: string
  courseCode: string
  courseName: string
  title: string
  completedLabel: string
  score: number | null
  destination: string
}

const COURSE_CODE_TONES = [
  'bg-[#4988C4]/16 text-[#1C4D8D]',
  'bg-[#e8f8f3] text-[#16836a]',
  'bg-[#fff4e8] text-[#c26a12]',
  'bg-[#f3eefe] text-[#6d4cc4]',
  'bg-[#e7f6fb] text-[#0e7490]',
  'bg-[#fdecef] text-[#c2415b]',
  'bg-[#eef8e8] text-[#3d7a2a]',
]

function courseCodeTone(code?: string) {
  if (!code) return COURSE_CODE_TONES[0]
  const hash = Array.from(code).reduce((sum, char) => sum + char.charCodeAt(0), 0)
  return COURSE_CODE_TONES[hash % COURSE_CODE_TONES.length]
}

function padCount(value: number) {
  return String(value).padStart(2, '0')
}

function getLoadErrorMessage(error: unknown) {
  if (!error || typeof error !== 'object') return 'Không tải được dữ liệu tổng quan.'
  const problem = error as Partial<ProblemDetails>
  return problem.detail || problem.title || 'Không tải được dữ liệu tổng quan.'
}

function average(values: number[]) {
  if (values.length === 0) return null
  return values.reduce((sum, value) => sum + value, 0) / values.length
}

function formatScore(score: number) {
  return score.toFixed(1)
}

function formatSignedDelta(delta: number) {
  const rounded = Math.round(delta * 10) / 10
  const prefix = rounded > 0 ? '+' : ''
  return `${prefix}${rounded.toFixed(1)}`
}

function formatRelativeCompletion(iso: string | null, now: Date, inProgress: boolean) {
  if (inProgress) return 'Đang diễn ra'
  if (!iso) return 'Chưa có thời điểm hoàn thành'

  const then = new Date(iso)
  if (Number.isNaN(then.getTime())) return 'Đã ghi nhận'

  const minutes = Math.round((now.getTime() - then.getTime()) / 60_000)
  if (minutes < 1) return 'Hoàn thành vừa xong'
  if (minutes < 60) return `Hoàn thành ${minutes} phút trước`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `Hoàn thành ${hours} giờ trước`
  const days = Math.round(hours / 24)
  if (days === 1) return 'Hoàn thành hôm qua'
  if (days < 7) return `Hoàn thành ${days} ngày trước`
  const weeks = Math.round(days / 7)
  return `Hoàn thành ${weeks} tuần trước`
}

function buildOverviewStats(
  courses: CourseSummary[],
  history: PracticeHistoryItem[],
  quotas: MockExamQuota[],
  now: Date,
): OverviewStat[] {
  const completed = history.filter((item) => item.status === 'completed')
  const scored = completed.filter(
    (item): item is PracticeHistoryItem & { averageScore: number } => item.averageScore !== null,
  )
  const overallAverage = average(scored.map((item) => item.averageScore))
  const passingCount = scored.filter((item) => item.averageScore >= 5).length
  const passRate = scored.length > 0 ? Math.round((passingCount / scored.length) * 1000) / 10 : null

  const weekMs = 7 * 24 * 60 * 60 * 1000
  const recentScores = scored
    .filter((item) => now.getTime() - new Date(item.endedAt ?? item.startedAt).getTime() <= weekMs)
    .map((item) => item.averageScore)
  const previousScores = scored
    .filter((item) => {
      const age = now.getTime() - new Date(item.endedAt ?? item.startedAt).getTime()
      return age > weekMs && age <= weekMs * 2
    })
    .map((item) => item.averageScore)
  const recentAverage = average(recentScores)
  const previousAverage = average(previousScores)
  const scoreDelta =
    recentAverage !== null && previousAverage !== null ? recentAverage - previousAverage : null

  const quotaRemaining = quotas.reduce((sum, quota) => sum + quota.remainingCount, 0)
  const quotaLimit = quotas.reduce((sum, quota) => sum + quota.maxDailyQuota, 0)
  const courseCodes = courses
    .map((course) => course.code)
    .filter(Boolean)
    .slice(0, 3)
    .join(', ')

  return [
    {
      label: 'Học phần đang mở',
      value: padCount(courses.length),
      suffix: 'Môn',
      helper:
        quotaLimit > 0
          ? `${quotaRemaining}/${quotaLimit} lượt thi thử còn lại hôm nay`
          : courseCodes || 'Chưa có môn học đang mở',
      icon: BookOpenCheck,
      cardClassName: 'border-[#9ec4e8] bg-[#f3f9fd]',
      iconClassName: 'bg-[#1C4D8D] text-white',
      badgeText: 'Học kỳ hiện tại',
      badgeClassName: 'bg-white text-[#1C4D8D]',
    },
    {
      label: 'Phiên luyện tập hoàn tất',
      value: padCount(completed.length),
      suffix: 'Phiên',
      helper:
        passRate === null ? 'Chưa có phiên đã chấm điểm' : `${passRate}% phiên đạt từ 5.0 điểm`,
      icon: Mic2,
      cardClassName: 'border-[#b7e4cf] bg-[#f3fbf7]',
      iconClassName: 'bg-[#16836a] text-white',
      badgeText: 'Tích lũy',
      badgeClassName: 'bg-white text-[#16836a]',
    },
    {
      label: 'Điểm trung bình tích lũy',
      value: overallAverage === null ? '—' : formatScore(overallAverage),
      suffix: overallAverage === null ? undefined : '/10',
      helper:
        scoreDelta === null
          ? 'Tính trên các phiên đã hoàn thành'
          : `${formatSignedDelta(scoreDelta)} điểm so với tuần trước`,
      trend: scoreDelta !== null && scoreDelta > 0 ? formatSignedDelta(scoreDelta) : undefined,
      icon: Gauge,
      cardClassName: 'border-[#d4c4f5] bg-[#f7f3fd]',
      iconClassName: 'bg-[#6d4cc4] text-white',
      badgeText: 'Theo Rubric',
      badgeClassName: 'bg-white text-[#6d4cc4]',
    },
  ]
}

function buildRecentActivities(
  history: PracticeHistoryItem[],
  now: Date,
  mockHistory: MockExamHistoryItem[] = [],
): RecentActivity[] {
  const practiceActivities: Array<RecentActivity & { sortTime: number }> = history.map((item) => {
    const inProgress = item.status !== 'completed'
    const isFullSession = item.practiceMode === 'full_session'
    const iso = item.endedAt ?? item.startedAt
    return {
      id: `practice-${item.sessionId}`,
      type: isFullSession ? 'guided' : 'practice',
      typeLabel: isFullSession ? 'Luyện trọn gói' : 'Luyện từng câu',
      courseCode: item.courseCode,
      courseName: item.courseName,
      title: item.courseName || 'Phiên luyện tập',
      completedLabel: formatRelativeCompletion(iso, now, inProgress),
      score: item.averageScore,
      destination: inProgress
        ? `/practice/session?session=${encodeURIComponent(item.sessionId)}`
        : `/results?type=practice&id=${encodeURIComponent(item.sessionId)}`,
      sortTime: new Date(iso).getTime() || 0,
    }
  })

  const mockActivities: Array<RecentActivity & { sortTime: number }> = mockHistory.map((item) => {
    const inProgress = item.status.toLowerCase() === 'in_progress'
    return {
      id: `mock-${item.sessionId}`,
      type: 'mock',
      typeLabel: 'Thi thử tính giờ',
      courseCode: item.courseCode,
      courseName: item.courseName,
      title: item.courseName || 'Ca thi thử',
      completedLabel: formatRelativeCompletion(item.startedAt, now, inProgress),
      score: item.totalScore,
      destination: inProgress
        ? `/mock-exam/session?session=${encodeURIComponent(item.sessionId)}`
        : `/results?type=mock&id=${encodeURIComponent(item.sessionId)}`,
      sortTime: new Date(item.startedAt).getTime() || 0,
    }
  })

  return [...practiceActivities, ...mockActivities]
    .sort((a, b) => b.sortTime - a.sortTime)
    .map((entry) => ({
      id: entry.id,
      type: entry.type,
      typeLabel: entry.typeLabel,
      courseCode: entry.courseCode,
      courseName: entry.courseName,
      title: entry.title,
      completedLabel: entry.completedLabel,
      score: entry.score,
      destination: entry.destination,
    }))
}

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

function getActivityBadgeClass(type: RecentActivity['type']) {
  if (type === 'mock') return 'bg-[#0F2854] text-[#BDE8F5]'
  if (type === 'guided') return 'bg-[#f3eefe] text-[#6d4cc4]'
  return 'bg-[#e8f8f3] text-[#16836a]'
}

export function StudentDashboardPage() {
  const { user } = useAuth()
  const [now, setNow] = useState(() => new Date())
  const [showAllActivities, setShowAllActivities] = useState(false)
  const [courses, setCourses] = useState<CourseSummary[]>([])
  const [history, setHistory] = useState<PracticeHistoryItem[]>([])
  const [mockHistory, setMockHistory] = useState<MockExamHistoryItem[]>([])
  const [officialTickets, setOfficialTickets] = useState<OfficialExamTicketOverview[]>([])
  const [quotas, setQuotas] = useState<MockExamQuota[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const [coursesResult, historyResult, mockHistoryResult, officialTicketsResult] =
        await Promise.allSettled([
          studentOverviewService.getCourses(),
          studentOverviewService.getPracticeHistory(),
          mockExamService.getHistory({ pageIndex: 1, pageSize: 10 }),
          studentOverviewService.getOfficialExamTickets(),
        ])

      if (cancelled) return

      const nextCourses = coursesResult.status === 'fulfilled' ? coursesResult.value : []
      const nextHistory = historyResult.status === 'fulfilled' ? historyResult.value : []
      const nextMockHistory =
        mockHistoryResult.status === 'fulfilled' ? mockHistoryResult.value.items : []
      const nextOfficialTickets =
        officialTicketsResult.status === 'fulfilled' ? officialTicketsResult.value : []
      setCourses(nextCourses)
      setHistory(nextHistory)
      setMockHistory(nextMockHistory)
      setOfficialTickets(nextOfficialTickets)

      if (coursesResult.status === 'rejected' && historyResult.status === 'rejected') {
        setLoadError(getLoadErrorMessage(coursesResult.reason))
        setQuotas([])
        setIsLoading(false)
        return
      }

      if (coursesResult.status === 'rejected') {
        setLoadError(getLoadErrorMessage(coursesResult.reason))
      } else if (historyResult.status === 'rejected') {
        setLoadError(getLoadErrorMessage(historyResult.reason))
      }

      const quotaResults = await Promise.all(
        nextCourses.map(async (course) => {
          try {
            return await studentOverviewService.getMockExamQuota(course.id)
          } catch {
            return null
          }
        }),
      )

      if (cancelled) return
      setQuotas(quotaResults.filter((quota): quota is MockExamQuota => quota !== null))
      setIsLoading(false)
    })()

    return () => {
      cancelled = true
    }
  }, [])

  const overviewStats = useMemo(
    () => buildOverviewStats(courses, history, quotas, now),
    [courses, history, quotas, now],
  )
  const recentActivities = useMemo(
    () => buildRecentActivities(history, now, mockHistory),
    [history, now, mockHistory],
  )
  const displayedActivities = showAllActivities ? recentActivities : recentActivities.slice(0, 5)
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
    <div className={ds.page}>
      <section
        className={ds.banner}
        onPointerMove={handleHeroPointerMove}
        onPointerLeave={resetHeroParallax}
      >
        <div
          className="pointer-events-none absolute inset-0 opacity-70 transition-transform duration-300 ease-out motion-reduce:transform-none"
          style={{
            backgroundImage:
              'linear-gradient(rgba(255,255,255,0.08) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.08) 1px, transparent 1px)',
            backgroundSize: '28px 28px',
            transform:
              'translate3d(var(--hero-grid-x, 0px), var(--hero-grid-y, 0px), 0) scale(1.03)',
          }}
        />
        <div className="pointer-events-none absolute -top-24 -right-20 size-80 rounded-full bg-[#57dcff]/10 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-28 left-[35%] size-72 rounded-full bg-[#5674ef]/8 blur-3xl" />

        <div className="relative grid min-h-[285px] lg:grid-cols-[minmax(0,1.35fr)_minmax(300px,0.65fr)]">
          <div className="relative z-10 flex flex-col justify-center px-5 py-6 sm:px-7 sm:py-7 xl:px-9">
            <div className="mb-3 flex flex-wrap items-center gap-2 text-xs font-medium text-white/80">
              <span className="inline-flex items-center gap-1.5 rounded-full border border-white/25 bg-white/14 px-3 py-1 font-bold text-white">
                <Award className="size-3.5 text-[#BDE8F5]" />
                Học kỳ {getSemesterLabel(now)}
              </span>
              <span className="inline-flex items-center rounded-full bg-white/10 px-3 py-1 font-semibold text-[#BDE8F5]">
                Khoa Kỹ thuật Phần mềm · FPTU
              </span>
              <span className="inline-flex items-center rounded-full bg-white/10 px-3 py-1 font-semibold text-white">
                MSSV: {studentIdentifier}
              </span>
            </div>

            <div className="max-w-[680px]">
              <h1 className="text-[clamp(1.55rem,3vw,2.35rem)] leading-[1.12] font-bold tracking-[-0.04em] text-white">
                {getGreeting(now.getHours())},{' '}
                <span className="block text-[#BDE8F5] sm:inline">{studentName}</span>
              </h1>
              <p className="mt-2 max-w-[620px] text-sm leading-relaxed text-white/85">
                Theo dõi lộ trình ôn tập vấn đáp chuyên ngành, chủ động luyện phản xạ câu hỏi theo
                học phần và tra cứu bảng điểm chi tiết từng ca thi.
              </p>
            </div>

            <div className="mt-5 flex max-w-[660px] flex-col gap-3 sm:flex-row sm:items-center">
              <div className="flex min-w-0 flex-1 items-center gap-3 rounded-xl border border-[#4988C4]/45 bg-white/92 p-3 shadow-[0_6px_20px_rgba(15,40,84,0.08)] backdrop-blur-md">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D] text-white">
                  <CalendarDays className="size-[18px]" />
                </div>
                <div className="min-w-0">
                  <p className="text-[11px] font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">
                    Ca thi chính thức sắp tới
                  </p>
                  <p className="truncate text-sm font-bold text-[#0F2854]">
                    {officialTickets[0]
                      ? `${officialTickets[0].courseCode} · ${officialTickets[0].shiftName}`
                      : 'Chưa có lịch thi phòng Lab'}
                  </p>
                  <p className="mt-0.5 flex items-center gap-1 text-xs font-medium text-[#5b6b84]">
                    <Clock3 className="size-3 text-[#1C4D8D]" />
                    {officialTickets[0]
                      ? `Phòng ${officialTickets[0].roomLab} · Máy ${officialTickets[0].seatNumber}`
                      : 'Sẽ hiển thị khi Bộ môn phân công ca thi'}
                  </p>
                </div>
              </div>

              <Link
                to="/exam"
                className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-white px-4 text-sm font-bold text-[#0F2854] shadow-sm transition hover:-translate-y-0.5 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
              >
                Vào phòng thi Kiosk
                <ArrowRight className="size-4 text-[#1C4D8D]" />
              </Link>
            </div>
          </div>

          <div className="relative min-h-[210px] overflow-hidden lg:min-h-full">
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
          </div>
        </div>
      </section>

      {loadError && (
        <div
          role="alert"
          className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-relaxed text-red-700"
        >
          {loadError}
        </div>
      )}

      <section aria-label="Tóm tắt học tập" className="grid gap-3 md:grid-cols-3">
        {(isLoading
          ? buildOverviewStats([], [], [], now).map((stat) => ({
              ...stat,
              value: '…',
              helper: 'Đang đồng bộ dữ liệu...',
            }))
          : overviewStats
        ).map(
          ({
            label,
            value,
            suffix,
            helper,
            icon: Icon,
            cardClassName,
            iconClassName,
            badgeText,
            badgeClassName,
            trend,
          }) => (
            <article
              key={label}
              className={cn(
                'group rounded-2xl border p-4 shadow-[0_7px_24px_rgba(15,40,84,0.05)] transition duration-200 hover:-translate-y-0.5 hover:shadow-[0_12px_30px_rgba(15,40,84,0.09)] sm:p-5',
                cardClassName,
              )}
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <span
                    className={cn(
                      'inline-flex rounded-full px-2.5 py-0.5 text-[11px] font-bold shadow-2xs',
                      badgeClassName,
                    )}
                  >
                    {badgeText}
                  </span>
                  <p className="mt-2.5 text-xs font-bold text-[#0F2854]/80">{label}</p>
                  <div className="mt-1.5 flex items-baseline gap-1.5">
                    <strong className="text-3xl font-black tracking-[-0.04em] text-[#0F2854]">
                      {value}
                    </strong>
                    {suffix && (
                      <span className="text-xs font-bold text-[#1C4D8D]">{suffix}</span>
                    )}
                  </div>
                </div>
                <div
                  className={cn(
                    'flex size-10 items-center justify-center rounded-xl shadow-xs',
                    iconClassName,
                  )}
                >
                  <Icon className="size-5" />
                </div>
              </div>
              <p className="mt-3 flex items-center gap-1.5 text-xs font-semibold text-[#0F2854]/75">
                {trend ? (
                  <TrendingUp className="size-3.5 text-[#16836a]" />
                ) : (
                  <CheckCircle2 className="size-3.5 text-[#1C4D8D]" />
                )}
                <span className={cn(trend && 'text-[#16836a]')}>{helper}</span>
              </p>
            </article>
          ),
        )}
      </section>

      <section aria-label="Lối tắt học tập" className="grid gap-3 sm:grid-cols-3">
        <Link
          to="/practice"
          className="group flex items-center justify-between gap-3 rounded-2xl border border-[#4988C4] bg-white p-4 shadow-xs transition hover:-translate-y-0.5 hover:bg-[#F4FBFD] hover:shadow-md"
        >
          <div className="flex items-center gap-3">
            <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#e8f8f3] text-[#16836a]">
              <Mic2 className="size-5" />
            </span>
            <div>
              <h2 className="text-sm font-bold text-[#0F2854]">Luyện tập tự do</h2>
              <p className="mt-0.5 text-xs text-[#5b6b84]">
                Luyện từng câu hoặc trọn gói 3–10 câu
              </p>
            </div>
          </div>
          <ArrowRight className="size-4 shrink-0 text-[#1C4D8D] transition-transform group-hover:translate-x-1" />
        </Link>

        <Link
          to="/mock-exam"
          className="group flex items-center justify-between gap-3 rounded-2xl border border-[#4988C4] bg-white p-4 shadow-xs transition hover:-translate-y-0.5 hover:bg-[#F4FBFD] hover:shadow-md"
        >
          <div className="flex items-center gap-3">
            <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#4988C4]/16 text-[#1C4D8D]">
              <TimerReset className="size-5" />
            </span>
            <div>
              <h2 className="text-sm font-bold text-[#0F2854]">Thi thử tính giờ</h2>
              <p className="mt-0.5 text-xs text-[#5b6b84]">
                Mô phỏng áp lực phòng thi & chấm theo CLO
              </p>
            </div>
          </div>
          <ArrowRight className="size-4 shrink-0 text-[#1C4D8D] transition-transform group-hover:translate-x-1" />
        </Link>

        <Link
          to="/results"
          className="group flex items-center justify-between gap-3 rounded-2xl border border-[#4988C4] bg-white p-4 shadow-xs transition hover:-translate-y-0.5 hover:bg-[#F4FBFD] hover:shadow-md"
        >
          <div className="flex items-center gap-3">
            <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#f3eefe] text-[#6d4cc4]">
              <BarChart3 className="size-5" />
            </span>
            <div>
              <h2 className="text-sm font-bold text-[#0F2854]">Lịch sử & Bảng điểm</h2>
              <p className="mt-0.5 text-xs text-[#5b6b84]">
                Xem lại câu hỏi, bài trả lời & nhận xét
              </p>
            </div>
          </div>
          <ArrowRight className="size-4 shrink-0 text-[#1C4D8D] transition-transform group-hover:translate-x-1" />
        </Link>
      </section>

      <section className={cn(ds.section, 'overflow-hidden')}>
        <div
          className={cn(
            ds.sectionHeader,
            'flex flex-col justify-between gap-2 sm:flex-row sm:items-center',
          )}
        >
          <div>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <History className="size-4 text-[#1C4D8D]" />
              </span>
              <h2 className={ds.sectionTitle}>Hoạt động gần đây</h2>
              <span className="rounded-full bg-white px-2.5 py-0.5 text-xs font-bold text-[#1C4D8D] shadow-2xs">
                {isLoading ? '…' : recentActivities.length} phiên
              </span>
            </div>
            <p className={ds.sectionHint}>
              Các phiên luyện tập và ca thi thử vừa thực hiện kèm điểm đánh giá và nhận xét chi tiết
            </p>
          </div>

          {recentActivities.length > 5 && (
            <button
              type="button"
              onClick={() => setShowAllActivities((isShowingAll) => !isShowingAll)}
              className="inline-flex min-h-9 shrink-0 items-center gap-1 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
              aria-expanded={showAllActivities}
            >
              {showAllActivities ? 'Thu gọn danh sách' : 'Xem toàn bộ'}
              <ChevronRight
                className={cn('size-3.5 transition-transform', showAllActivities && 'rotate-90')}
              />
            </button>
          )}
        </div>

        <div className="divide-y divide-[#4988C4]/30 px-4 sm:px-6">
          {!isLoading && displayedActivities.length === 0 && (
            <div className="py-10 text-center">
              <p className="text-sm font-bold text-[#0F2854]">
                Chưa có phiên luyện tập hoặc thi thử nào
              </p>
              <p className="mt-1 text-xs text-[#5b6b84]">
                Hãy chọn Luyện tập hoặc Thi thử ở phía trên để bắt đầu phiên đầu tiên của bạn.
              </p>
            </div>
          )}

          {displayedActivities.map((activity) => (
            <article
              key={activity.id}
              className="group grid gap-3 py-3.5 transition sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center"
            >
              <div className="flex min-w-0 items-center gap-3.5">
                <span
                  className={cn(
                    'inline-flex h-9 min-w-[74px] shrink-0 items-center justify-center rounded-xl px-2.5 text-xs font-black tracking-[0.02em]',
                    courseCodeTone(activity.courseCode),
                  )}
                >
                  {activity.courseCode || 'MÔN'}
                </span>

                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span
                      className={cn(
                        'rounded-md px-2 py-0.5 text-xs font-bold',
                        getActivityBadgeClass(activity.type),
                      )}
                    >
                      {activity.typeLabel}
                    </span>
                    <span className="text-xs font-medium text-[#5b6b84]">
                      {activity.completedLabel}
                    </span>
                  </div>
                  <h3 className="mt-1 truncate text-sm font-bold text-[#0F2854]">
                    {activity.title}
                  </h3>
                </div>
              </div>

              <div className="flex items-center justify-between gap-3 pl-12 sm:justify-end sm:pl-0">
                <span
                  className={cn(
                    'inline-flex items-baseline gap-1 rounded-xl px-3 py-1.5 text-xs font-bold',
                    activity.score === null
                      ? 'bg-[#fff8eb] text-[#9a6414]'
                      : activity.score >= 8
                        ? 'bg-[#e8f8f3] text-[#16836a]'
                        : activity.score >= 5
                          ? 'bg-[#4988C4]/16 text-[#1C4D8D]'
                          : 'bg-[#fdecef] text-[#c2415b]',
                  )}
                >
                  <strong className="text-sm font-black">
                    {activity.score === null ? 'Đang chấm' : activity.score.toFixed(1)}
                  </strong>
                  {activity.score !== null && <span>/10</span>}
                </span>

                <Link
                  to={activity.destination}
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#1C4D8D] px-3.5 text-xs font-bold text-white shadow-2xs transition hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
                  aria-label={`Xem chi tiết cho ${activity.title}`}
                >
                  <FileCheck2 className="size-3.5" />
                  Xem câu hỏi & nhận xét
                </Link>
              </div>
            </article>
          ))}
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[#4988C4]/45 bg-[#F4FBFD] px-4 py-3 text-xs text-[#5b6b84] sm:px-6">
          <span className="inline-flex items-center gap-1.5 font-medium text-[#0F2854]">
            <CheckCircle2 className="size-3.5 text-[#16836a]" />
            Lưu trữ đầy đủ câu hỏi, bản ghi câu trả lời và điểm từng tiêu chí Rubric
          </span>
          <Link
            to="/results"
            className="inline-flex items-center gap-1 font-bold text-[#1C4D8D] hover:underline"
          >
            Mở trang Kết quả & Lịch sử đầy đủ
            <ArrowRight className="size-3.5" />
          </Link>
        </div>
      </section>

      <div className="sr-only" aria-live="polite">
        {showAllActivities
          ? 'Đang hiển thị toàn bộ hoạt động'
          : 'Đang hiển thị năm hoạt động gần nhất'}
      </div>
    </div>
  )
}

export { StudentDashboardPage as StudentOverviewPage }
