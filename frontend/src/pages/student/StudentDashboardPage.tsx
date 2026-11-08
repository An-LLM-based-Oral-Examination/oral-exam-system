import { lazy, Suspense, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  ArrowRight,
  Award,
  CalendarDays,
  CheckCircle2,
  ChevronRight,
  FileCheck2,
  Gauge,
  History,
  Mic2,
  TrendingUp,
  UsersRound,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { ds } from '@/design/system'
import {
  studentOverviewService,
  type CourseSummary,
  type MockExamQuota,
  type OfficialExamTicketOverview,
  type PracticeHistoryItem,
} from '@/services/studentOverview.service'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { StudentClassPanel } from '@/components/student/StudentClassPanel'
import { BannerFact, PageBanner } from '@/design/ui'
import { classService } from '@/services/class.service'
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
  'border border-[#4988C4]/35 bg-[#4988C4]/16 text-[#1C4D8D]',
  'border border-[#16836a]/30 bg-[#e8f8f3] text-[#16836a]',
  'border border-[#c26a12]/30 bg-[#fff4e8] text-[#c26a12]',
  'border border-[#6d4cc4]/30 bg-[#f3eefe] text-[#6d4cc4]',
  'border border-[#0e7490]/30 bg-[#e7f6fb] text-[#0e7490]',
  'border border-[#c2415b]/30 bg-[#fdecef] text-[#c2415b]',
  'border border-[#3d7a2a]/30 bg-[#eef8e8] text-[#3d7a2a]',
]

function courseCodeTone(code?: string) {
  if (!code) return COURSE_CODE_TONES[0]
  const hash = Array.from(code).reduce((sum, char) => sum + char.charCodeAt(0), 0)
  return COURSE_CODE_TONES[hash % COURSE_CODE_TONES.length]
}

function padCount(value: number) {
  return String(value).padStart(2, '0')
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
  classCount: number,
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
      label: 'Lớp đã tham gia',
      value: padCount(classCount),
      suffix: 'Lớp',
      helper: courses.length > 0 ? `${courses.length} môn · ${courseCodes}` : 'Tìm lớp để tham gia học phần',
      icon: UsersRound,
      cardClassName: 'border-[#88b2de]/55 bg-[linear-gradient(145deg,#e9f5fc_0%,#cae3f5_100%)] shadow-[0_14px_28px_rgba(28,77,141,0.18)]',
      iconClassName: 'bg-[#1C4D8D] text-white',
      badgeText: 'Học kỳ hiện tại',
      badgeClassName: 'border border-[#82aedb]/55 bg-white text-[#1C4D8D]',
    },
    {
      label: 'Phiên luyện tập hoàn tất',
      value: padCount(completed.length),
      suffix: 'Phiên',
      helper:
        quotaLimit > 0
          ? `${quotaRemaining}/${quotaLimit} lượt thi thử còn lại hôm nay`
          : passRate === null
            ? 'Chưa có phiên đã chấm điểm'
            : `${passRate}% phiên đạt từ 5.0 điểm`,
      icon: Mic2,
      cardClassName: 'border-[#82cfad]/55 bg-[linear-gradient(145deg,#e8f7f1_0%,#c6ebd9_100%)] shadow-[0_14px_28px_rgba(22,131,106,0.16)]',
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
      cardClassName: 'border-[#c4afe8]/55 bg-[linear-gradient(145deg,#f2eafc_0%,#ddcef7_100%)] shadow-[0_14px_28px_rgba(109,76,196,0.16)]',
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
  const [, setOfficialTickets] = useState<OfficialExamTicketOverview[]>([])
  const [quotas, setQuotas] = useState<MockExamQuota[]>([])
  const [classCount, setClassCount] = useState(0)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const [coursesResult, classesResult, historyResult, mockHistoryResult, officialTicketsResult] =
        await Promise.allSettled([
          studentOverviewService.getCourses(),
          classService.listMyClasses(),
          studentOverviewService.getPracticeHistory(),
          mockExamService.getHistory({ pageIndex: 1, pageSize: 10 }),
          studentOverviewService.getOfficialExamTickets(),
        ])

      if (cancelled) return

      const enrolledIds = new Set(
        classesResult.status === 'fulfilled'
          ? classesResult.value.map((item) => item.courseId).filter(Boolean)
          : [],
      )
      const nextCourses =
        coursesResult.status === 'fulfilled'
          ? coursesResult.value.filter((course) => enrolledIds.has(course.id))
          : []
      const nextHistory = historyResult.status === 'fulfilled' ? historyResult.value : []
      const nextMockHistory =
        mockHistoryResult.status === 'fulfilled' ? mockHistoryResult.value.items : []
      const nextOfficialTickets =
        officialTicketsResult.status === 'fulfilled' ? officialTicketsResult.value : []
      setClassCount(classesResult.status === 'fulfilled' ? classesResult.value.length : 0)
      setCourses(nextCourses)
      setHistory(nextHistory)
      setMockHistory(nextMockHistory)
      setOfficialTickets(nextOfficialTickets)

      if (coursesResult.status === 'rejected' && historyResult.status === 'rejected') {
        toast.error(userMessage(coursesResult.reason, 'Không tải được dữ liệu tổng quan. Vui lòng thử lại.'))
        setQuotas([])
        setIsLoading(false)
        return
      }

      if (coursesResult.status === 'rejected') {
        toast.error(userMessage(coursesResult.reason, 'Không tải được dữ liệu tổng quan. Vui lòng thử lại.'))
      } else if (historyResult.status === 'rejected') {
        toast.error(userMessage(historyResult.reason, 'Không tải được lịch sử luyện tập. Vui lòng thử lại.'))
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
    () => buildOverviewStats(courses, history, quotas, classCount, now),
    [courses, history, quotas, classCount, now],
  )
  const recentActivities = useMemo(
    () => buildRecentActivities(history, now, mockHistory),
    [history, now, mockHistory],
  )
  const displayedActivities = showAllActivities ? recentActivities : recentActivities.slice(0, 5)
  const studentName = user?.fullName ?? 'Sinh viên'

  return (
    <div className={ds.page}>
      <PageBanner
        icon={Award}
        eyebrow={`Học kỳ ${getSemesterLabel(now)}`}
        title={
          <>
            {getGreeting(now.getHours())},{' '}
            <span className="text-[#BDE8F5]">{studentName}</span>
          </>
        }
        description="Quản lý lớp học phần tại đây. Luyện tập và thi thử chỉ mở các môn của lớp bạn đã tham gia."
        extras={
          <>
            <BannerFact icon={Mic2}>Luyện tập theo Rubric</BannerFact>
            <BannerFact icon={Gauge} tone="green">
              Thi thử không tính điểm
            </BannerFact>
            <BannerFact icon={CalendarDays} tone="violet">
              Theo dõi lịch thi Lab
            </BannerFact>
          </>
        }
        scene={
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center" aria-hidden="true">
                <div className="size-28 animate-pulse rounded-full border border-[#7cdff4]/40 bg-[#dffaff]/30" />
              </div>
            }
          >
            <StudentOverviewThreeScene />
          </Suspense>
        }
      />

      <section aria-label="Tóm tắt học tập" className="relative z-10 grid gap-3 md:grid-cols-3">
        {(isLoading
          ? buildOverviewStats([], [], [], 0, now).map((stat) => ({
              ...stat,
              value: '…',
              helper: 'Đang đồng bộ dữ liệu...',
            }))
          : overviewStats
        ).map(({ label, value, suffix, helper, icon: Icon, iconClassName, cardClassName, trend }) => (
          <article
            key={label}
            className={cn('flex items-center gap-4 rounded-2xl border px-4 py-4', cardClassName)}
          >
            <span className={cn('flex size-12 shrink-0 items-center justify-center rounded-2xl', iconClassName)}>
              <Icon className="size-5" />
            </span>
            <div className="min-w-0">
              <p className="truncate text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">{label}</p>
              <div className="mt-0.5 flex items-baseline gap-1.5">
                <strong className="text-[1.65rem] leading-none font-black tracking-[-0.04em] text-[#0F2854]">{value}</strong>
                {suffix && <span className="text-xs font-bold text-[#1C4D8D]">{suffix}</span>}
              </div>
              <p className="mt-1 flex items-center gap-1 truncate text-xs font-semibold text-[#5b6b84]">
                {trend ? <TrendingUp className="size-3.5 shrink-0 text-[#16836a]" /> : <CheckCircle2 className="size-3.5 shrink-0 text-[#1C4D8D]" />}
                <span className={cn('truncate', trend && 'text-[#16836a]')}>{helper}</span>
              </p>
            </div>
          </article>
        ))}
      </section>

      <StudentClassPanel onClassCount={setClassCount} />

      <section className={cn(ds.section, 'overflow-hidden')}>
        <div
          className={cn(
            ds.sectionHeader,
            'flex flex-col justify-between gap-2 sm:flex-row sm:items-center',
          )}
        >
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <History className={ds.sectionIcon} />
              </span>
              <h2 className={ds.sectionTitle}>Hoạt động gần đây</h2>
              <span className={ds.sectionBadge}>
                {isLoading ? '…' : `${recentActivities.length} phiên`}
              </span>
            </div>
            <p className={ds.sectionHint}>Phiên luyện tập và thi thử gần đây</p>
          </div>

          {recentActivities.length > 5 && (
            <button
              type="button"
              onClick={() => setShowAllActivities((isShowingAll) => !isShowingAll)}
              className="inline-flex min-h-9 shrink-0 items-center gap-1 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition duration-200 hover:-translate-y-0.5 hover:border-[#1C4D8D] hover:bg-[#E7F3FB] hover:shadow-[0_8px_16px_rgba(15,40,84,0.14)] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
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
            <div className="py-4">
              <EmptyState
                icon={History}
                title="Chưa có phiên luyện tập hoặc thi thử"
                description="Vào Luyện tập hoặc Thi thử trên menu để bắt đầu phiên đầu tiên."
              />
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
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#1C4D8D] px-3.5 text-xs font-bold text-white shadow-2xs transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
                  aria-label={`Xem chi tiết cho ${activity.title}`}
                >
                  <FileCheck2 className="size-3.5" />
                  Xem câu hỏi & nhận xét
                </Link>
              </div>
            </article>
          ))}
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[#82aedb]/55 bg-[#F4FBFD] px-4 py-3 text-xs text-[#5b6b84] sm:px-6">
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
