import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  ArrowRight,
  CalendarDays,
  CheckCircle2,
  ChevronRight,
  Gauge,
  History,
  Mic2,
  Search,
  TimerReset,
  TrendingUp,
  type LucideIcon,
} from 'lucide-react'
import { OverlayDrawer, OverlayModal } from '@/components/ui/OverlayDrawer'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import { mockExamService } from '@/services/mockExam.service'
import {
  studentOverviewService,
  type CourseSummary,
  type PracticeHistoryItem,
} from '@/services/studentOverview.service'
import type { ProblemDetails } from '@/types/auth.types'
import type { MockExamHistoryItem } from '@/types/mockExam.types'

interface OverviewStat {
  id: 'completed' | 'average' | 'open_courses'
  label: string
  value: string
  helper: string
  icon: LucideIcon
  tone: 'emerald' | 'amber' | 'rose' | 'slate'
}

interface RecentActivity {
  id: string
  rawId: string
  type: 'practice' | 'guided' | 'mock'
  typeLabel: string
  title: string
  subtitle: string
  completedLabel: string
  timestampMs: number
  inProgress: boolean
  score: number | null
  icon: LucideIcon
  destination: string
  actionLabel: string
}

type ReadinessLevel = 'ready' | 'practicing' | 'warning' | 'not_started'

interface CourseReadinessInfo {
  level: ReadinessLevel
  badgeLabel: string
  readinessScore: number
  averageScore: number | null
  answeredQuestions: number
  badgeClassName: string
  barClassName: string
}

const DEFAULT_COURSE_FALLBACKS: CourseSummary[] = [
  {
    id: '00000000-0000-0000-0000-000000000008',
    code: 'PRN231',
    name: 'Building Cross-Platform Back-End Applications with .NET',
    credits: 3,
    isActive: true,
  },
  {
    id: '00000000-0000-0000-0000-000000000009',
    code: 'SWD392',
    name: 'Software Architecture and Design',
    credits: 3,
    isActive: true,
  },
  {
    id: '00000000-0000-0000-0000-00000000000a',
    code: 'SWR302',
    name: 'Software Requirements',
    credits: 3,
    isActive: true,
  },
]

const STAT_TONE_STYLES: Record<
  OverviewStat['tone'],
  {
    topBar: string
    surfaceBg: string
    iconBadge: string
    valueText: string
    helperBadge: string
  }
> = {
  emerald: {
    topBar: 'border-t-[3px] border-t-emerald-500',
    surfaceBg: 'bg-gradient-to-br from-emerald-50/55 via-white to-white',
    iconBadge: 'border-emerald-200 bg-emerald-100/80 text-emerald-700',
    valueText: 'text-emerald-700',
    helperBadge: 'border-emerald-200/80 bg-emerald-50 text-emerald-900',
  },
  amber: {
    topBar: 'border-t-[3px] border-t-amber-500',
    surfaceBg: 'bg-gradient-to-br from-amber-50/60 via-white to-white',
    iconBadge: 'border-amber-200 bg-amber-100/80 text-amber-700',
    valueText: 'text-[#C2410C]',
    helperBadge: 'border-amber-200/80 bg-amber-50 text-amber-950',
  },
  rose: {
    topBar: 'border-t-[3px] border-t-rose-500',
    surfaceBg: 'bg-gradient-to-br from-rose-50/55 via-white to-white',
    iconBadge: 'border-rose-200 bg-rose-100/80 text-rose-700',
    valueText: 'text-rose-700',
    helperBadge: 'border-rose-200/80 bg-rose-50 text-rose-900',
  },
  slate: {
    topBar: 'border-t-[3px] border-t-emerald-500',
    surfaceBg: 'bg-gradient-to-br from-emerald-50/45 via-white to-white',
    iconBadge: 'border-emerald-200 bg-emerald-100/70 text-emerald-700',
    valueText: 'text-slate-900',
    helperBadge: 'border-slate-200 bg-slate-100 text-slate-800',
  },
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

function getScoreBadgeTone(score: number | null) {
  if (score === null) {
    return 'border-amber-200 bg-amber-50 text-amber-700'
  }
  if (score >= 8.0) {
    return 'border-emerald-200 bg-emerald-50 text-emerald-700'
  }
  if (score >= 5.0) {
    return 'border-amber-200 bg-amber-50 text-amber-800'
  }
  return 'border-rose-200 bg-rose-50 text-rose-700'
}

function formatRelativeCompletion(iso: string | null, now: Date, inProgress: boolean) {
  if (inProgress) return 'Đang diễn ra'
  if (!iso) return 'Đã ghi nhận'

  const then = new Date(iso)
  if (Number.isNaN(then.getTime())) return 'Đã hoàn thành'

  const minutes = Math.round((now.getTime() - then.getTime()) / 60_000)
  if (minutes < 1) return 'Vừa xong'
  if (minutes < 60) return `${minutes} phút trước`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours} giờ trước`
  const days = Math.round(hours / 24)
  if (days === 1) return 'Hôm qua'
  if (days < 7) return `${days} ngày trước`
  const weeks = Math.round(days / 7)
  return `${weeks} tuần trước`
}

function buildOverviewStats(
  practiceHistory: PracticeHistoryItem[],
  mockHistory: MockExamHistoryItem[],
  openCourseCount: number,
): OverviewStat[] {
  const completedPractice = practiceHistory.filter(
    (item) => item.status.toLowerCase() === 'completed',
  )
  const completedMock = mockHistory.filter((item) => {
    const normalized = item.status.toLowerCase()
    return (
      normalized === 'completed' ||
      normalized === 'submitted' ||
      normalized === 'graded' ||
      item.totalScore !== null
    )
  })

  const totalCompletedCount = completedPractice.length + completedMock.length

  const practiceScores = completedPractice
    .map((item) => item.averageScore)
    .filter((score): score is number => score !== null)

  const mockScores = completedMock
    .map((item) => item.totalScore)
    .filter((score): score is number => score !== null)

  const allScores = [...practiceScores, ...mockScores]
  const overallAverage = average(allScores)
  const passingCount = allScores.filter((score) => score >= 5).length
  const passRate =
    allScores.length > 0 ? Math.round((passingCount / allScores.length) * 100) : 100

  const averageTone: OverviewStat['tone'] =
    overallAverage === null
      ? 'slate'
      : overallAverage >= 8.0
        ? 'emerald'
        : overallAverage >= 5.0
          ? 'amber'
          : 'rose'

  return [
    {
      id: 'completed',
      label: 'Phiên hoàn thành',
      value: `${totalCompletedCount} phiên`,
      helper: `${completedPractice.length} luyện tập · ${completedMock.length} thi thử`,
      icon: CheckCircle2,
      tone: 'emerald',
    },
    {
      id: 'average',
      label: 'Điểm trung bình',
      value: overallAverage === null ? '—' : `${formatScore(overallAverage)} / 10`,
      helper:
        overallAverage === null
          ? 'Sẵn sàng ghi nhận điểm phiên đầu tiên'
          : overallAverage >= 8.0
            ? 'Phong độ tốt · Duy trì ổn định'
            : overallAverage >= 5.0
              ? 'Đạt yêu cầu · Cần chú ý nâng điểm'
              : 'Cảnh báo · Cần ôn luyện củng cố ngay',
      icon: Gauge,
      tone: averageTone,
    },
    {
      id: 'open_courses',
      label: 'Môn học đang mở',
      value: `${openCourseCount} học phần`,
      helper: `Học kỳ Fall 2026 · Tỷ lệ đạt ${passRate}%`,
      icon: TrendingUp,
      tone: 'amber',
    },
  ]
}

function calculateCourseReadiness(
  course: CourseSummary,
  practiceHistory: PracticeHistoryItem[],
  mockHistory: MockExamHistoryItem[],
): CourseReadinessInfo {
  const relevantPractice = practiceHistory.filter(
    (item) => item.courseId === course.id || item.courseCode === course.code,
  )
  const relevantMock = mockHistory.filter(
    (item) => item.courseId === course.id || item.courseCode === course.code,
  )

  const practiceScores = relevantPractice
    .map((item) => item.averageScore)
    .filter((score): score is number => score !== null)
  const mockScores = relevantMock
    .map((item) => item.totalScore)
    .filter((score): score is number => score !== null)

  const allScores = [...practiceScores, ...mockScores]
  const avg = average(allScores)

  const totalAnswered =
    relevantPractice.reduce((sum, item) => sum + item.answeredQuestions, 0) +
    relevantMock.reduce((sum, item) => sum + item.answeredQuestions, 0)

  if (allScores.length === 0 && totalAnswered === 0) {
    return {
      level: 'not_started',
      badgeLabel: 'Sẵn sàng bắt đầu',
      readinessScore: 16,
      averageScore: null,
      answeredQuestions: 0,
      badgeClassName: 'border-emerald-200 bg-emerald-50/60 text-emerald-700',
      barClassName: 'bg-emerald-500',
    }
  }

  if (avg !== null && avg < 5.0) {
    const lowScoreProgress = Math.max(
      18,
      Math.min(42, Math.round((avg / 10) * 40 + Math.min(totalAnswered * 2, 12))),
    )
    return {
      level: 'warning',
      badgeLabel: 'Cần ôn tập',
      readinessScore: lowScoreProgress,
      averageScore: avg,
      answeredQuestions: totalAnswered,
      badgeClassName: 'border-rose-200 bg-rose-50 text-rose-700',
      barClassName: 'bg-rose-500',
    }
  }

  const computedScore = Math.min(
    100,
    Math.round(((avg ?? 6.5) / 10) * 65 + Math.min(totalAnswered * 5, 35)),
  )

  if (computedScore >= 75 || (avg !== null && avg >= 7.5 && totalAnswered >= 5)) {
    const readyProgress = Math.max(75, computedScore)
    return {
      level: 'ready',
      badgeLabel: 'Sẵn sàng',
      readinessScore: readyProgress,
      averageScore: avg,
      answeredQuestions: totalAnswered,
      badgeClassName: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      barClassName: 'bg-emerald-500',
    }
  }

  const practicingProgress = Math.max(45, Math.min(74, computedScore))
  return {
    level: 'practicing',
    badgeLabel: 'Đang luyện',
    readinessScore: practicingProgress,
    averageScore: avg,
    answeredQuestions: totalAnswered,
    badgeClassName: 'border-amber-200 bg-amber-50 text-amber-700',
    barClassName: 'bg-amber-500',
  }
}

function buildRecentActivities(
  practiceHistory: PracticeHistoryItem[],
  mockHistory: MockExamHistoryItem[],
  now: Date,
): RecentActivity[] {
  const practiceActivities: RecentActivity[] = practiceHistory.map((item) => {
    const normalizedStatus = item.status.toLowerCase()
    const inProgress = normalizedStatus === 'in_progress'
    const isFullSession = item.practiceMode === 'full_session'
    const isoDate = item.endedAt ?? item.startedAt
    const parsedTime = Date.parse(isoDate)

    return {
      id: `practice-${item.sessionId}`,
      rawId: item.sessionId,
      type: isFullSession ? 'guided' : 'practice',
      typeLabel: isFullSession ? 'Luyện tập cả phiên' : 'Luyện tập theo câu',
      title: [item.courseCode, item.courseName].filter(Boolean).join(' — ') || 'Phiên luyện tập AI',
      subtitle: `Đã trả lời ${item.answeredQuestions}/${item.totalQuestions} câu hỏi`,
      completedLabel: formatRelativeCompletion(isoDate, now, inProgress),
      timestampMs: Number.isNaN(parsedTime) ? 0 : parsedTime,
      inProgress,
      score: item.averageScore,
      icon: Mic2,
      destination: inProgress
        ? `/practice/session?session=${encodeURIComponent(item.sessionId)}`
        : `/results?type=practice&result=practice-${encodeURIComponent(item.sessionId)}`,
      actionLabel: inProgress ? 'Tiếp tục' : 'Xem chi tiết',
    }
  })

  const mockActivities: RecentActivity[] = mockHistory.map((item) => {
    const normalizedStatus = item.status.toLowerCase()
    const inProgress = normalizedStatus === 'in_progress' || normalizedStatus === 'started'
    const parsedTime = Date.parse(item.startedAt)

    return {
      id: `mock-${item.sessionId}`,
      rawId: item.sessionId,
      type: 'mock',
      typeLabel: 'Thi thử tính giờ',
      title: [item.courseCode, item.courseName].filter(Boolean).join(' — ') || 'Ca thi thử vấn đáp',
      subtitle: `Thời lượng ${item.durationMinutes} phút · ${item.answeredQuestions}/${item.totalQuestions} câu`,
      completedLabel: formatRelativeCompletion(item.startedAt, now, inProgress),
      timestampMs: Number.isNaN(parsedTime) ? 0 : parsedTime,
      inProgress,
      score: item.totalScore,
      icon: TimerReset,
      destination: inProgress
        ? `/mock-exam/session?session=${encodeURIComponent(item.sessionId)}`
        : `/results?type=mock&result=mock-${encodeURIComponent(item.sessionId)}`,
      actionLabel: inProgress ? 'Tiếp tục' : 'Xem bảng điểm',
    }
  })

  return [...practiceActivities, ...mockActivities].sort(
    (left, right) => right.timestampMs - left.timestampMs,
  )
}

function getGreeting(hour: number) {
  if (hour >= 5 && hour < 12) return 'Chào buổi sáng'
  if (hour >= 12 && hour < 18) return 'Chào buổi chiều'
  return 'Chào buổi tối'
}

export function StudentDashboardPage() {
  const { user } = useAuth()
  const [now, setNow] = useState(() => new Date())
  const [isActivityDrawerOpen, setIsActivityDrawerOpen] = useState(false)
  const [selectedCourseDetail, setSelectedCourseDetail] = useState<CourseSummary | null>(null)
  const [courses, setCourses] = useState<CourseSummary[]>([])
  const [practiceHistory, setPracticeHistory] = useState<PracticeHistoryItem[]>([])
  const [mockHistory, setMockHistory] = useState<MockExamHistoryItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [courseSearch, setCourseSearch] = useState('')

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    let cancelled = false

    void (async () => {
      setIsLoading(true)
      setLoadError(null)

      const [coursesResult, practiceResult, mockResult] = await Promise.allSettled([
        studentOverviewService.getCourses(),
        studentOverviewService.getPracticeHistory(),
        mockExamService.getHistory({ pageIndex: 1, pageSize: 10 }),
      ])

      if (cancelled) return

      if (coursesResult.status === 'fulfilled') {
        setCourses(coursesResult.value)
      } else {
        setLoadError(getLoadErrorMessage(coursesResult.reason))
      }

      if (practiceResult.status === 'fulfilled') {
        setPracticeHistory(practiceResult.value)
      }

      if (mockResult.status === 'fulfilled') {
        setMockHistory(mockResult.value.items)
      }

      setIsLoading(false)
    })()

    return () => {
      cancelled = true
    }
  }, [])

  const displayCourses = useMemo(
    () => (courses.length > 0 ? courses : DEFAULT_COURSE_FALLBACKS),
    [courses],
  )

  const filteredCourses = useMemo(() => {
    const query = courseSearch.trim().toLowerCase()
    if (!query) return displayCourses
    return displayCourses.filter(
      (course) =>
        course.code.toLowerCase().includes(query) ||
        course.name.toLowerCase().includes(query),
    )
  }, [displayCourses, courseSearch])

  const overviewStats = useMemo(
    () => buildOverviewStats(practiceHistory, mockHistory, displayCourses.length),
    [practiceHistory, mockHistory, displayCourses.length],
  )

  const recentActivities = useMemo(
    () => buildRecentActivities(practiceHistory, mockHistory, now),
    [practiceHistory, mockHistory, now],
  )

  const studentName = user?.fullName ?? user?.username ?? 'Sinh viên'
  const studentIdentifier = user?.studentCode ?? user?.username ?? user?.email ?? 'Student'

  return (
    <div className="space-y-5 font-sans">
      {/* Khối 1: Thanh đầu trang ấm áp + Hộp thông báo lịch thi đáng chú ý */}
      <section className="rounded-2xl border border-[#FED7AA] bg-gradient-to-br from-[#FFF7ED] via-white to-amber-50/50 p-5 shadow-xs">
        <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-center">
          <div>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-orange-200 bg-orange-50 px-3 py-0.5 text-xs font-bold text-[#C2410C]">
              Học kỳ Fall 2026 · MSSV: {studentIdentifier}
            </span>
            <h1 className="mt-2 text-xl font-bold text-slate-900 sm:text-2xl">
              {getGreeting(now.getHours())}, {studentName}
            </h1>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <Link
              to="/practice"
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-xs transition hover:-translate-y-0.5 hover:bg-[#D95B16]"
            >
              <Mic2 className="size-4" />
              Luyện tập
            </Link>

            <Link
              to="/mock-exam"
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl border border-amber-300 bg-amber-50/80 px-4 text-xs font-bold text-amber-950 shadow-2xs transition hover:-translate-y-0.5 hover:bg-amber-100/80"
            >
              <TimerReset className="size-4 text-amber-700" />
              Thi thử
            </Link>

            <button
              type="button"
              onClick={() => setIsActivityDrawerOpen(true)}
              className="inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50/80 px-4 text-xs font-bold text-emerald-900 shadow-2xs transition hover:-translate-y-0.5 hover:bg-emerald-100/80"
            >
              <History className="size-4 text-emerald-700" />
              Nhật ký ({recentActivities.length})
            </button>
          </div>
        </div>

        <Link
          to="/exam"
          className="group mt-4 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-300 bg-gradient-to-r from-amber-50 via-orange-50/70 to-amber-50 px-4 py-3 text-xs text-amber-950 shadow-2xs transition hover:border-[#F26F21] hover:shadow-xs"
        >
          <div className="flex flex-wrap items-center gap-2.5">
            <span className="inline-flex items-center gap-1.5 rounded-lg border border-amber-300 bg-amber-100 px-2.5 py-1 text-xs font-bold text-amber-950">
              <span className="size-2 rounded-full bg-amber-500 animate-pulse" />
              Đáng chú ý · Lịch thi sắp tới
            </span>
            <span className="flex items-center gap-1.5 text-xs font-bold text-slate-900 sm:text-sm">
              <CalendarDays className="size-4 shrink-0 text-[#F26F21]" />
              PRN231 · 24/10/2026 · 09:00 – 10:30 · Phòng Lab 301
            </span>
          </div>
          <span className="inline-flex items-center gap-1.5 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-bold text-[#C2410C] shadow-2xs transition group-hover:border-[#F26F21] group-hover:bg-[#F26F21] group-hover:text-white">
            Xem lịch thi
            <ArrowRight className="size-3.5" />
          </span>
        </Link>
      </section>

      {loadError && (
        <div
          role="alert"
          className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-bold text-rose-700"
        >
          {loadError}
        </div>
      )}

      {/* Khối 2: 3 Thẻ số liệu KPI có chiều sâu và màu ngữ nghĩa */}
      <section aria-label="Tóm tắt số liệu học tập" className="grid gap-4 sm:grid-cols-3">
        {(isLoading
          ? buildOverviewStats([], [], displayCourses.length).map((stat) => ({
              ...stat,
              value: '…',
              helper: 'Đang tải dữ liệu...',
            }))
          : overviewStats
        ).map(({ id, label, value, helper, icon: Icon, tone }) => {
          const style = STAT_TONE_STYLES[tone]
          return (
            <article
              key={id}
              className={cn(
                'rounded-2xl border border-slate-200 p-4 shadow-xs transition-all hover:-translate-y-0.5 hover:shadow-md',
                style.topBar,
                style.surfaceBg,
              )}
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-xs font-bold tracking-wide text-slate-700 uppercase">
                    {label}
                  </p>
                  <p className={cn('mt-1.5 text-2xl font-extrabold tracking-tight', style.valueText)}>
                    {value}
                  </p>
                  <span
                    className={cn(
                      'mt-2 inline-block rounded-lg border px-2.5 py-0.5 text-xs font-semibold',
                      style.helperBadge,
                    )}
                  >
                    {helper}
                  </span>
                </div>
                <div
                  className={cn(
                    'flex size-10 shrink-0 items-center justify-center rounded-xl border shadow-2xs',
                    style.iconBadge,
                  )}
                >
                  <Icon className="size-5" />
                </div>
              </div>
            </article>
          )
        })}
      </section>

      {/* Khối 3: Danh sách Môn học với Huy hiệu & Thanh tiến độ màu ngữ nghĩa */}
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
          <div className="flex flex-wrap items-center gap-2.5">
            <h2 className="text-base font-bold text-slate-900">
              Học phần của bạn ({filteredCourses.length})
            </h2>
            <div className="hidden items-center gap-2 text-xs font-semibold text-slate-700 sm:flex">
              <span className="inline-flex items-center gap-1.5 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-emerald-800">
                <span className="size-2 rounded-full bg-emerald-500" /> Sẵn sàng
              </span>
              <span className="inline-flex items-center gap-1.5 rounded-md border border-amber-200 bg-amber-50 px-2 py-0.5 text-amber-900">
                <span className="size-2 rounded-full bg-amber-500" /> Đang luyện
              </span>
              <span className="inline-flex items-center gap-1.5 rounded-md border border-rose-200 bg-rose-50 px-2 py-0.5 text-rose-800">
                <span className="size-2 rounded-full bg-rose-500" /> Cần ôn tập
              </span>
            </div>
          </div>

          <div className="relative w-full sm:w-64">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-600" />
            <input
              type="text"
              value={courseSearch}
              onChange={(event) => setCourseSearch(event.target.value)}
              placeholder="Tìm mã hoặc tên môn..."
              className="w-full rounded-xl border border-slate-300 bg-slate-50 py-2 pr-3 pl-9 text-xs font-medium text-slate-900 placeholder:text-slate-500 focus:border-[#F26F21] focus:bg-white focus:outline-none"
            />
          </div>
        </div>

        {filteredCourses.length === 0 ? (
          <div className="mt-4 rounded-xl border border-dashed border-slate-300 bg-slate-50 py-8 text-center text-xs font-semibold text-slate-700">
            Không tìm thấy học phần nào khớp với từ khóa &ldquo;{courseSearch}&rdquo;.
          </div>
        ) : (
          <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-3">
            {filteredCourses.map((course) => {
              const readiness = calculateCourseReadiness(course, practiceHistory, mockHistory)
              const avgText =
                readiness.averageScore === null ? '—' : formatScore(readiness.averageScore)

              return (
                <button
                  key={course.id}
                  type="button"
                  onClick={() => setSelectedCourseDetail(course)}
                  className="group flex cursor-pointer flex-col justify-between rounded-2xl border border-slate-200 bg-gradient-to-b from-white via-white to-slate-50/70 p-4 text-left shadow-xs transition-all hover:-translate-y-0.5 hover:border-[#F26F21]/60 hover:shadow-md"
                >
                  <div className="w-full">
                    <div className="flex items-center justify-between gap-2">
                      <span className="rounded-lg border border-orange-200 bg-orange-50 px-2.5 py-0.5 text-xs font-bold text-[#C2410C]">
                        {course.code}
                      </span>
                      <span
                        className={cn(
                          'rounded-lg border px-2.5 py-0.5 text-xs font-bold',
                          readiness.badgeClassName,
                        )}
                      >
                        {readiness.badgeLabel}
                      </span>
                    </div>

                    <h3 className="mt-3 line-clamp-2 text-sm font-bold text-slate-900 transition-colors group-hover:text-[#C2410C]">
                      {course.name}
                    </h3>

                    <div className="mt-3.5 rounded-xl border border-slate-200/90 bg-slate-50/90 p-3">
                      <div className="flex items-center justify-between text-xs">
                        <span className="font-semibold text-slate-700">Mức độ sẵn sàng</span>
                        <span className="font-bold text-slate-900">
                          {readiness.readinessScore}%
                        </span>
                      </div>
                      <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-slate-200">
                        <div
                          className={cn('h-full rounded-full transition-all', readiness.barClassName)}
                          style={{ width: `${Math.max(12, readiness.readinessScore)}%` }}
                        />
                      </div>
                    </div>
                  </div>

                  <div className="mt-4 flex w-full items-center justify-between border-t border-slate-200/80 pt-3 text-xs">
                    <span className="font-semibold text-slate-800">
                      Điểm TB: <strong className="font-extrabold text-slate-950">{avgText}</strong>{' '}
                      · {readiness.answeredQuestions} câu
                    </span>
                    <span className="inline-flex items-center gap-1 rounded-lg bg-[#FFF7ED] px-2.5 py-1 font-bold text-[#C2410C] transition group-hover:bg-[#F26F21] group-hover:text-white">
                      Chọn môn →
                    </span>
                  </div>
                </button>
              )
            })}
          </div>
        )}
      </section>

      {/* Màn hình phụ 1: OverlayModal khi bấm vào 1 Môn học */}
      <OverlayModal
        open={Boolean(selectedCourseDetail)}
        onClose={() => setSelectedCourseDetail(null)}
        size="md"
        badge={selectedCourseDetail?.code}
        title={selectedCourseDetail?.name ?? 'Chi tiết học phần'}
      >
        {selectedCourseDetail && (
          <div className="space-y-4">
            {(() => {
              const readiness = calculateCourseReadiness(
                selectedCourseDetail,
                practiceHistory,
                mockHistory,
              )
              return (
                <>
                  <div className="grid grid-cols-3 gap-3">
                    <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                      <span className="block text-xs font-semibold text-slate-700">
                        Điểm trung bình
                      </span>
                      <strong className="mt-1 block text-base font-bold text-slate-900">
                        {readiness.averageScore === null
                          ? '—'
                          : `${formatScore(readiness.averageScore)} / 10`}
                      </strong>
                    </div>

                    <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                      <span className="block text-xs font-semibold text-slate-700">
                        Số câu đã luyện
                      </span>
                      <strong className="mt-1 block text-base font-bold text-slate-900">
                        {readiness.answeredQuestions} câu
                      </strong>
                    </div>

                    <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                      <span className="block text-xs font-semibold text-slate-700">Trạng thái</span>
                      <span
                        className={cn(
                          'mt-1 inline-block rounded-md border px-2 py-0.5 text-xs font-bold',
                          readiness.badgeClassName,
                        )}
                      >
                        {readiness.badgeLabel}
                      </span>
                    </div>
                  </div>

                  <div className="rounded-xl border border-slate-200 bg-white p-3.5">
                    <div className="flex items-center justify-between text-xs">
                      <span className="font-semibold text-slate-800">Tiến độ sẵn sàng học phần</span>
                      <strong className="font-bold text-slate-900">
                        {readiness.readinessScore}%
                      </strong>
                    </div>
                    <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-slate-200">
                      <div
                        className={cn('h-full rounded-full transition-all', readiness.barClassName)}
                        style={{ width: `${Math.max(12, readiness.readinessScore)}%` }}
                      />
                    </div>
                  </div>
                </>
              )
            })()}

            <div className="space-y-2.5 pt-1">
              <p className="text-xs font-bold text-slate-800">Chọn hình thức thực hiện:</p>

              <Link
                to={`/practice?course=${encodeURIComponent(selectedCourseDetail.id)}`}
                onClick={() => setSelectedCourseDetail(null)}
                className="flex items-center justify-between gap-3 rounded-xl bg-[#F26F21] p-4 text-white shadow-xs transition hover:bg-[#D95B16]"
              >
                <span className="text-sm font-bold">Luyện tập vấn đáp môn này</span>
                <ArrowRight className="size-4 shrink-0" />
              </Link>

              <Link
                to={`/mock-exam?course=${encodeURIComponent(selectedCourseDetail.id)}`}
                onClick={() => setSelectedCourseDetail(null)}
                className="flex items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50/60 p-4 text-slate-900 shadow-2xs transition hover:border-amber-300 hover:bg-amber-100/60"
              >
                <span className="text-sm font-bold text-amber-950">Thi thử tính giờ môn này</span>
                <ArrowRight className="size-4 shrink-0 text-amber-700" />
              </Link>
            </div>
          </div>
        )}
      </OverlayModal>

      {/* Màn hình phụ 2: OverlayDrawer Nhật ký hoạt động */}
      <OverlayDrawer
        open={isActivityDrawerOpen}
        onClose={() => setIsActivityDrawerOpen(false)}
        badge="Nhật ký hoạt động"
        title={`Lịch sử gần đây (${recentActivities.length})`}
        width="xl"
        footer={
          <div className="flex flex-wrap items-center justify-between gap-3 text-xs font-semibold text-slate-700">
            <span>Đồng bộ từ hệ thống khảo thí</span>
            <Link
              to="/results"
              onClick={() => setIsActivityDrawerOpen(false)}
              className="inline-flex items-center gap-1 font-bold text-[#C2410C] hover:underline"
            >
              Xem toàn bộ bảng điểm & phúc khảo
              <ArrowRight className="size-3.5" />
            </Link>
          </div>
        }
      >
        <div className="divide-y divide-slate-200">
          {recentActivities.length === 0 ? (
            <div className="py-10 text-center">
              <p className="text-sm font-bold text-slate-900">
                Chưa có phiên hoạt động nào được ghi nhận.
              </p>
              <p className="mt-1 text-xs font-medium text-slate-700">
                Chọn một môn học để bắt đầu luyện tập hoặc thi thử.
              </p>
            </div>
          ) : (
            recentActivities.map((activity) => {
              const Icon = activity.icon
              return (
                <article
                  key={activity.id}
                  className="flex flex-col justify-between gap-3 py-3.5 sm:flex-row sm:items-center"
                >
                  <div className="flex min-w-0 items-start gap-3">
                    <div className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-slate-100 text-slate-800">
                      <Icon className="size-4" />
                    </div>
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="rounded-md border border-slate-200 bg-slate-100 px-2 py-0.5 text-xs font-bold text-slate-800">
                          {activity.typeLabel}
                        </span>
                        <span className="text-xs font-semibold text-slate-700">
                          {activity.completedLabel}
                        </span>
                      </div>
                      <h3 className="mt-1 truncate text-sm font-bold text-slate-900">
                        {activity.title}
                      </h3>
                      <p className="mt-0.5 text-xs font-medium text-slate-700">
                        {activity.subtitle}
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center justify-between gap-4 sm:justify-end">
                    <div className="text-right">
                      <span className="block text-xs font-semibold text-slate-700">Điểm</span>
                      <span
                        className={cn(
                          'mt-0.5 inline-block rounded-md border px-2 py-0.5 text-xs font-bold',
                          getScoreBadgeTone(activity.score),
                        )}
                      >
                        {activity.score === null ? 'Đang chấm' : `${formatScore(activity.score)} / 10`}
                      </span>
                    </div>

                    <Link
                      to={activity.destination}
                      onClick={() => setIsActivityDrawerOpen(false)}
                      className={cn(
                        'inline-flex min-h-9 items-center gap-1 rounded-xl px-3 text-xs font-bold transition',
                        activity.inProgress
                          ? 'bg-[#F26F21] text-white hover:bg-[#D95B16]'
                          : 'border border-slate-200 bg-white text-slate-800 hover:bg-slate-50',
                      )}
                    >
                      {activity.actionLabel}
                      <ChevronRight className="size-3.5" />
                    </Link>
                  </div>
                </article>
              )
            })
          )}
        </div>
      </OverlayDrawer>
    </div>
  )
}

export { StudentDashboardPage as StudentOverviewPage }
