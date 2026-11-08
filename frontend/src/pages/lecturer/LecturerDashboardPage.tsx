import { lazy, Suspense, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  BookOpenCheck,
  CheckCircle2,
  ChevronRight,
  FilePenLine,
  GraduationCap,
  Mic,
  ScanSearch,
  Users,
  UsersRound,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { classService } from '@/services/class.service'
import { questionService } from '@/services/question.service'
import type { LecturerCourseOffering, ManagedClass, SemesterOption } from '@/types/class.types'
import type { QuestionSummary } from '@/types/rubric.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { BannerFact, PageBanner } from '@/design/ui'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'

const LecturerOverviewThreeScene = lazy(() =>
  import('./LecturerOverviewThreeScene').then((module) => ({
    default: module.LecturerOverviewThreeScene,
  })),
)

interface DashboardMetric {
  label: string
  value: number
  helper: string
  icon: LucideIcon
  iconClassName: string
  cardClassName: string
}

function countStatus(questions: QuestionSummary[] | undefined, status: string) {
  return Array.isArray(questions)
    ? questions.filter((question) => question.approvalStatus === status).length
    : 0
}

function isOwnedByUser(question: QuestionSummary, userId: string | undefined) {
  return Boolean(userId && question.submittedBy?.toLowerCase() === userId.toLowerCase())
}

const COURSE_CODE_TONES = [
  'border border-[#4988C4]/35 bg-[#4988C4]/16 text-[#1C4D8D]',
  'border border-[#16836a]/30 bg-[#e8f8f3] text-[#16836a]',
  'border border-[#c26a12]/30 bg-[#fff4e8] text-[#c26a12]',
  'border border-[#6d4cc4]/30 bg-[#f3eefe] text-[#6d4cc4]',
  'border border-[#0e7490]/30 bg-[#e7f6fb] text-[#0e7490]',
  'border border-[#c2415b]/30 bg-[#fdecef] text-[#c2415b]',
  'border border-[#3d7a2a]/30 bg-[#eef8e8] text-[#3d7a2a]',
  'border border-[#be185d]/30 bg-[#fff0f6] text-[#be185d]',
]

function courseToneIndex(code: string) {
  let hash = 0
  for (let index = 0; index < code.length; index += 1) hash = (hash * 31 + code.charCodeAt(index)) >>> 0
  return hash
}

function courseCodeTone(code: string) {
  return COURSE_CODE_TONES[courseToneIndex(code) % COURSE_CODE_TONES.length]
}

function dateKey(value: string) {
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return null
  return year * 10000 + month * 100 + day
}

function pickCurrentSemester(semesters: SemesterOption[]) {
  const today = new Date()
  const todayKey = today.getFullYear() * 10000 + (today.getMonth() + 1) * 100 + today.getDate()
  const covering = semesters.filter((item) => {
    const start = dateKey(item.startDate)
    const end = dateKey(item.endDate)
    return start !== null && end !== null && todayKey >= start && todayKey <= end
  })
  return covering.find((item) => item.isActive) ?? covering[0] ?? semesters.find((item) => item.isActive) ?? semesters[0]
}

function getGreeting(hour: number) {
  if (hour >= 5 && hour < 12) return 'Chào buổi sáng'
  if (hour >= 12 && hour < 18) return 'Chào buổi chiều'
  return 'Chào buổi tối'
}

export function LecturerDashboardPage() {
  const { user } = useAuth()
  const [questions, setQuestions] = useState<QuestionSummary[]>([])
  const [offerings, setOfferings] = useState<LecturerCourseOffering[]>([])
  const [classes, setClasses] = useState<ManagedClass[]>([])
  const [semester, setSemester] = useState<SemesterOption | null>(null)
  const [semesterCode, setSemesterCode] = useState('')
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let isActive = true

    const loadOverview = async () => {
      setIsLoading(true)
      try {
        const [offeringResult, semesterResult, classResult] = await Promise.all([
          classService.listMyOfferings().catch(() => [] as LecturerCourseOffering[]),
          classService.listSemesters().catch(() => [] as SemesterOption[]),
          classService.listLecturerClasses().catch(() => [] as ManagedClass[]),
        ])
        const current = pickCurrentSemester(semesterResult)
        const currentId = current?.id ?? offeringResult[0]?.semesterId ?? ''
        const currentCode = current?.code || offeringResult.find((item) => item.semesterId === currentId)?.semesterCode || ''
        if (isActive) {
          setSemester(current ?? null)
          setSemesterCode(currentCode)
          setOfferings(offeringResult.filter((item) => item.semesterId === currentId))
          setClasses(classResult.filter((item) => item.semesterId === currentId))
        }

        const firstPage = await questionService.getQuestions({ pageNumber: 1, pageSize: 100 })
        const remainingPages = Array.from(
          { length: Math.max(firstPage.totalPages - 1, 0) },
          (_, index) => index + 2,
        )
        const remainingResults = await Promise.all(
          remainingPages.map((pageNumber) =>
            questionService.getQuestions({ pageNumber, pageSize: 100 }),
          ),
        )
        const allQuestions = [
          ...(Array.isArray(firstPage.items) ? firstPage.items : []),
          ...remainingResults.flatMap((page) => (Array.isArray(page.items) ? page.items : [])),
        ]
        const visibleQuestions =
          user?.role === 'Instructor'
            ? allQuestions.filter((question) => isOwnedByUser(question, user?.id))
            : allQuestions

        if (isActive) setQuestions(visibleQuestions)
      } catch (loadError) {
        if (isActive) {
          setQuestions([])
          toast.error(userMessage(loadError, 'Không tải được dữ liệu tổng quan. Vui lòng thử lại.'))
        }
      } finally {
        if (isActive) setIsLoading(false)
      }
    }

    void loadOverview()
    return () => {
      isActive = false
    }
  }, [user?.id, user?.role])

  const assignedCourses = useMemo(() => {
    const map = new Map<string, LecturerCourseOffering>()
    for (const item of offerings) {
      if (item.courseId && !map.has(item.courseId)) map.set(item.courseId, item)
    }
    return [...map.values()]
  }, [offerings])

  const metrics = useMemo<DashboardMetric[]>(
    () => [
      {
        label: 'Môn phân công',
        value: assignedCourses.length,
        helper: semesterCode ? `Kỳ ${semesterCode}` : 'Kỳ hiện tại',
        icon: GraduationCap,
        iconClassName: 'bg-[#1C4D8D] text-white',
        cardClassName: 'border-[#88b2de]/55 bg-[linear-gradient(145deg,#e9f5fc_0%,#cae3f5_100%)] shadow-[0_14px_28px_rgba(28,77,141,0.18)]',
      },
      {
        label: 'Lớp phụ trách',
        value: classes.length,
        helper: 'Lớp đã mở trong kỳ này',
        icon: UsersRound,
        iconClassName: 'bg-[#c26a12] text-white',
        cardClassName: 'border-[#edca8c]/55 bg-[linear-gradient(145deg,#fff4e2_0%,#f9dcac_100%)] shadow-[0_14px_28px_rgba(194,106,18,0.16)]',
      },
      {
        label: 'Sinh viên',
        value: classes.reduce((total, item) => total + item.enrolledCount, 0),
        helper: 'Đang theo học các lớp',
        icon: Users,
        iconClassName: 'bg-[#6d4cc4] text-white',
        cardClassName: 'border-[#c4afe8]/55 bg-[linear-gradient(145deg,#f2eafc_0%,#ddcef7_100%)] shadow-[0_14px_28px_rgba(109,76,196,0.16)]',
      },
      {
        label: 'Câu đã duyệt',
        value: countStatus(questions, 'APPROVED'),
        helper: 'Sẵn sàng đưa vào ca thi',
        icon: CheckCircle2,
        iconClassName: 'bg-[#16836a] text-white',
        cardClassName: 'border-[#82cfad]/55 bg-[linear-gradient(145deg,#e8f7f1_0%,#c6ebd9_100%)] shadow-[0_14px_28px_rgba(22,131,106,0.16)]',
      },
    ],
    [assignedCourses.length, classes, questions, semesterCode],
  )

  const now = new Date()

  return (
    <div className={ds.page}>
      <PageBanner
        icon={GraduationCap}
        eyebrow={`Học kỳ ${semester?.name || semesterCode || 'hiện tại'}`}
        title={
          <>
            {getGreeting(now.getHours())},{' '}
            <span className="text-[#BDE8F5]">{user?.fullName ?? 'Giảng viên'}</span>
          </>
        }
        description="Soạn câu hỏi luyện tập, mở ca vấn đáp trên lớp (OTP 5 phút), rồi hậu kiểm bằng chứng trước khi công bố điểm chính thức."
        extras={
          <>
            <BannerFact icon={FilePenLine}>Soạn câu hỏi luyện tập</BannerFact>
            <BannerFact icon={Mic} tone="green">
              Mở ca vấn đáp trên lớp
            </BannerFact>
            <BannerFact icon={ScanSearch} tone="violet">
              Hậu kiểm bằng chứng
            </BannerFact>
          </>
        }
        scene={
          <Suspense
            fallback={
              <div className="flex size-full items-center justify-center" aria-hidden="true">
                <div className="size-28 animate-pulse rounded-3xl border border-[#7cdff4]/40 bg-[#dffaff]/30 motion-reduce:animate-none" />
              </div>
            }
          >
            <LecturerOverviewThreeScene />
          </Suspense>
        }
      />

      <section aria-label="Tóm tắt giảng dạy" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {metrics.map(({ label, value, helper, icon: Icon, iconClassName, cardClassName }) => (
          <article key={label} className={cn('flex items-center gap-4 rounded-2xl border px-4 py-4', cardClassName)}>
            <span className={cn('flex size-12 shrink-0 items-center justify-center rounded-2xl', iconClassName)}>
              <Icon className="size-5" aria-hidden="true" />
            </span>
            <div className="min-w-0">
              <p className="truncate text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">{label}</p>
              <strong className={cn('mt-0.5 block text-[1.65rem] leading-none font-black tracking-[-0.04em] text-[#0F2854]', isLoading && 'animate-pulse motion-reduce:animate-none')}>
                {isLoading ? '…' : String(value).padStart(2, '0')}
              </strong>
              <p className="mt-1 truncate text-xs font-semibold text-[#5b6b84]">{helper}</p>
            </div>
          </article>
        ))}
      </section>

      <section className={ds.section}>
        <div className={cn(ds.sectionHeader, 'flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between')}>
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <GraduationCap className={ds.sectionIcon} aria-hidden="true" />
              </span>
              <h2 className={ds.sectionTitle}>Môn được phân công</h2>
              <span className={ds.sectionBadge}>
                {isLoading ? '…' : `${assignedCourses.length} môn`}
              </span>
            </div>
            <p className={ds.sectionHint}>
              Môn được phân công trong {semester?.name || semesterCode || 'kỳ hiện tại'}
            </p>
          </div>
          <Link to="/lecturer/classes" className={ds.outlineButton}>
            Quản lý lớp
            <ChevronRight className="size-3.5" />
          </Link>
        </div>
        <div className={ds.sectionBody}>
          {isLoading ? (
            <p className="text-sm font-semibold text-[#5b6b84]">Đang tải môn được phân công...</p>
          ) : assignedCourses.length === 0 ? (
            <EmptyState
              icon={GraduationCap}
              title="Chưa có môn được phân công"
              description={`Chưa có môn trong kỳ ${semesterCode || 'hiện tại'}. Liên hệ Trưởng Bộ môn nếu thiếu phân công.`}
            />
          ) : (
            <ul className="grid grid-cols-1 gap-3 lg:grid-cols-3">
              {assignedCourses.map((course) => {
                const courseClasses = classes.filter((item) => item.courseCode === course.courseCode)
                const studentCount = courseClasses.reduce((total, item) => total + item.enrolledCount, 0)
                return (
                  <li
                    key={course.courseOfferingId}
                    className={cn(
                      'group relative flex h-full flex-col overflow-hidden rounded-[22px] border border-[#82aedb]/55 transition duration-200',
                      'bg-[linear-gradient(180deg,#EAF5FB_0%,#F8FBFD_22%,#FFFFFF_48%,#FFFFFF_100%)]',
                      'hover:-translate-y-0.5 hover:border-[#4988C4] hover:bg-[linear-gradient(180deg,#DCEFFA_0%,#F4FBFD_24%,#F8FBFD_52%,#F8FBFD_100%)] hover:shadow-[0_14px_28px_rgba(15,40,84,0.16)]',
                      ds.panelShadow,
                    )}
                  >
                    <div
                      className="pointer-events-none absolute inset-x-0 top-0 h-14 overflow-hidden rounded-t-[21px]"
                      aria-hidden="true"
                    >
                      <div className="absolute inset-x-6 top-0 h-10 bg-[radial-gradient(ellipse_at_top,rgba(73,136,196,0.18)_0%,rgba(189,232,245,0.12)_42%,transparent_72%)]" />
                      <div className={ds.softCardAccentLine} />
                    </div>
                    <div className="relative flex h-full flex-1 flex-col p-4">
                      <div className="flex items-center justify-between gap-2">
                        <span
                          className={cn(
                            'inline-flex h-6 items-center rounded-md px-2 text-xs font-bold tracking-[0.04em]',
                            courseCodeTone(course.courseCode),
                          )}
                        >
                          {course.courseCode}
                        </span>
                        <span className={ds.infoChip}>{course.semesterCode}</span>
                      </div>
                      <h3 className="mt-3 line-clamp-2 text-base leading-snug font-bold text-[#0F2854] sm:text-[17px]">
                        {course.courseName}
                      </h3>
                      <p className="mt-1.5 line-clamp-2 text-xs font-semibold text-[#5b6b84]">
                        {courseClasses.length > 0
                          ? courseClasses
                              .map((item) => item.code || item.name)
                              .filter(Boolean)
                              .join(' · ')
                          : 'Chưa mở lớp'}
                      </p>
                      <div className="mt-auto flex flex-wrap gap-2 pt-4">
                        <span className={ds.infoChip}>
                          <UsersRound className="size-3.5" aria-hidden="true" />
                          {courseClasses.length} lớp
                        </span>
                        <span className={ds.infoChip}>
                          <Users className="size-3.5" aria-hidden="true" />
                          {studentCount} sinh viên
                        </span>
                      </div>
                    </div>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      </section>

      <section aria-label="Lối tắt" className="grid gap-3 sm:grid-cols-3">
        <Link to="/lecturer/classes" className={cn('group flex items-center justify-between gap-3 rounded-2xl border border-[#74a3d3]/55 bg-[linear-gradient(145deg,#f7fbfe_0%,#d7ebf8_100%)] px-4 py-3.5 transition hover:-translate-y-0.5 hover:shadow-[0_12px_24px_rgba(15,40,84,0.2)]', ds.panelShadow)}>
          <span className="flex items-center gap-3">
            <span className="flex size-10 items-center justify-center rounded-xl bg-[#1C4D8D] text-white">
              <UsersRound className="size-4" aria-hidden="true" />
            </span>
            <span>
              <span className="block text-sm font-bold text-[#0F2854]">Lớp của tôi</span>
              <span className="mt-0.5 block text-xs text-[#5b6b84]">Mở lớp và sĩ số</span>
            </span>
          </span>
          <ChevronRight className="size-4 text-[#1C4D8D] transition group-hover:translate-x-0.5" />
        </Link>
        <Link to="/question-bank" className={cn('group flex items-center justify-between gap-3 rounded-2xl border border-[#b29ce6]/55 bg-[linear-gradient(145deg,#faf7fe_0%,#e7defb_100%)] px-4 py-3.5 transition hover:-translate-y-0.5 hover:shadow-[0_12px_24px_rgba(15,40,84,0.2)]', ds.panelShadow)}>
          <span className="flex items-center gap-3">
            <span className="flex size-10 items-center justify-center rounded-xl bg-[#6d4cc4] text-white">
              <BookOpenCheck className="size-4" aria-hidden="true" />
            </span>
            <span>
              <span className="block text-sm font-bold text-[#0F2854]">Câu hỏi luyện tập</span>
              <span className="mt-0.5 block text-xs text-[#5b6b84]">Soạn câu và gửi duyệt</span>
            </span>
          </span>
          <ChevronRight className="size-4 text-[#6d4cc4] transition group-hover:translate-x-0.5" />
        </Link>
      </section>
    </div>
  )
}

export { LecturerDashboardPage as DashboardPage }
