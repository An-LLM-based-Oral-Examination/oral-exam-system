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
  CheckCircle2,
  ChevronRight,
  ClipboardCheck,
  FileClock,
  FilePenLine,
  RefreshCw,
  ScanSearch,
  ShieldCheck,
  Sparkles,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { questionService } from '@/services/question.service'
import type { QuestionSummary } from '@/types/rubric.types'
import { cn } from '@/lib/utils'

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
  tone: string
}

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể đồng bộ dữ liệu câu hỏi. Vui lòng thử lại.'
}

function countStatus(questions: QuestionSummary[] | undefined, status: string) {
  return Array.isArray(questions)
    ? questions.filter((question) => question.approvalStatus === status).length
    : 0
}

function isOwnedByUser(question: QuestionSummary, userId: string | undefined) {
  return Boolean(userId && question.submittedBy?.toLowerCase() === userId.toLowerCase())
}

function getGreeting(hour: number) {
  if (hour >= 5 && hour < 12) return 'Chào buổi sáng'
  if (hour >= 12 && hour < 18) return 'Chào buổi chiều'
  return 'Chào buổi tối'
}

export function LecturerDashboardPage() {
  const { user } = useAuth()
  const [questions, setQuestions] = useState<QuestionSummary[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let isActive = true

    const loadOverview = async () => {
      setIsLoading(true)
      setError('')
      try {
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
          user?.role === 'Admin'
            ? allQuestions
            : allQuestions.filter((question) => isOwnedByUser(question, user?.id))

        if (isActive) setQuestions(visibleQuestions)
      } catch (loadError) {
        if (isActive) {
          setQuestions([])
          setError(getErrorMessage(loadError))
        }
      } finally {
        if (isActive) setIsLoading(false)
      }
    }

    void loadOverview()
    return () => {
      isActive = false
    }
  }, [reloadKey, user?.id, user?.role])

  const metrics = useMemo<DashboardMetric[]>(
    () => [
      {
        label: 'Bản nháp đang biên soạn',
        value: countStatus(questions, 'DRAFT'),
        helper: 'Có thể chọn và gửi duyệt theo môn',
        icon: FilePenLine,
        tone: 'bg-[#edf3ff] text-[#245eea]',
      },
      {
        label: 'Đang chờ Bộ môn duyệt',
        value: countStatus(questions, 'SUBMITTED_FOR_REVIEW'),
        helper: 'Theo dõi phản hồi trước khi sử dụng',
        icon: FileClock,
        tone: 'bg-[#fff6e5] text-[#a66c12]',
      },
      {
        label: 'Cần chỉnh sửa',
        value: countStatus(questions, 'NEEDS_REVISION'),
        helper: 'Ưu tiên xử lý góp ý đã nhận',
        icon: TriangleAlert,
        tone: 'bg-[#fff0f1] text-[#b8404b]',
      },
      {
        label: 'Đã được phê duyệt',
        value: countStatus(questions, 'APPROVED'),
        helper: 'Sẵn sàng trong ngân hàng đề chính thức',
        icon: CheckCircle2,
        tone: 'bg-[#ecfaf5] text-[#168367]',
      },
    ],
    [questions],
  )

  const handleHeroPointerMove = (event: ReactPointerEvent<HTMLElement>) => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
    const rect = event.currentTarget.getBoundingClientRect()
    const x = ((event.clientX - rect.left) / rect.width - 0.5) * 2
    const y = ((event.clientY - rect.top) / rect.height - 0.5) * 2
    event.currentTarget.style.setProperty('--lecturer-scene-x', `${x * 7}px`)
    event.currentTarget.style.setProperty('--lecturer-scene-y', `${y * 5}px`)
    event.currentTarget.style.setProperty('--lecturer-grid-x', `${x * -3}px`)
    event.currentTarget.style.setProperty('--lecturer-grid-y', `${y * -2}px`)
  }

  const resetHeroParallax = (event: ReactPointerEvent<HTMLElement>) => {
    event.currentTarget.style.setProperty('--lecturer-scene-x', '0px')
    event.currentTarget.style.setProperty('--lecturer-scene-y', '0px')
    event.currentTarget.style.setProperty('--lecturer-grid-x', '0px')
    event.currentTarget.style.setProperty('--lecturer-grid-y', '0px')
  }

  const now = new Date()

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
              'translate3d(var(--lecturer-grid-x, 0px), var(--lecturer-grid-y, 0px), 0) scale(1.03)',
          }}
        />
        <div className="pointer-events-none absolute -top-28 -right-20 size-80 rounded-full bg-[#57dcff]/12 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-32 left-[34%] size-80 rounded-full bg-[#7357ff]/9 blur-3xl" />

        <div className="relative grid min-h-[320px] lg:grid-cols-[minmax(0,1.25fr)_minmax(340px,0.75fr)]">
          <div className="relative z-10 flex flex-col justify-center px-5 py-7 sm:px-7 xl:px-9">
            <div className="mb-3 flex flex-wrap items-center gap-2 text-[10px] font-semibold">
              <span className="inline-flex items-center gap-1.5 rounded-full border border-[#dce8f6] bg-[#f5f9ff] px-2.5 py-1 text-[#225bc9]">
                <Sparkles className="size-3" aria-hidden="true" />
                Không gian giảng viên
              </span>
              <span className="inline-flex items-center gap-1.5 text-[#25856e]">
                <span className="size-1.5 rounded-full bg-[#35c992]" />
                Rubric Studio sẵn sàng
              </span>
            </div>

            <p className="text-[10px] font-semibold tracking-[0.09em] text-[#657b98] uppercase">
              MF-03 · MF-04
            </p>
            <h1 className="mt-2 max-w-2xl text-[clamp(1.65rem,3.2vw,2.55rem)] leading-[1.1] font-bold tracking-[-0.045em] text-[#0b1830]">
              {getGreeting(now.getHours())},{' '}
              <span className="text-[#245eea]">{user?.fullName ?? 'Giảng viên'}</span>
            </h1>
            <p className="mt-3 max-w-2xl text-[12px] leading-relaxed text-[#5f6f85] sm:text-[13px]">
              Biên soạn câu hỏi theo Barem Rubric 10.0, gửi Trưởng Bộ môn thẩm định và hậu kiểm bằng
              chứng trước khi công bố điểm chính thức.
            </p>

            <div className="mt-5 flex flex-col gap-2.5 sm:flex-row">
              <Link
                to="/question-bank/create"
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-4 text-[11px] font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.24)] transition hover:-translate-y-0.5 hover:bg-[#194fcf] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
              >
                <FilePenLine className="size-4" aria-hidden="true" />
                Soạn câu hỏi mới
                <ArrowRight className="size-3.5" aria-hidden="true" />
              </Link>
              <Link
                to="/lecturer/audit"
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#cfdaea] bg-white/80 px-4 text-[11px] font-bold text-[#314968] backdrop-blur-md transition hover:-translate-y-0.5 hover:border-[#aebfda] hover:bg-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
              >
                <ScanSearch className="size-4 text-[#5c62da]" aria-hidden="true" />
                Mở cổng hậu kiểm
              </Link>
            </div>
          </div>

          <div className="relative min-h-[250px] overflow-hidden lg:min-h-full">
            <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_48%,rgba(76,206,245,0.16),transparent_50%)]" />
            <div
              className="absolute inset-[-7%] transition-transform duration-200 ease-out motion-reduce:transform-none"
              style={{
                transform:
                  'translate3d(var(--lecturer-scene-x, 0px), var(--lecturer-scene-y, 0px), 0)',
              }}
            >
              <Suspense
                fallback={
                  <div className="flex size-full items-center justify-center" aria-hidden="true">
                    <div className="size-28 animate-pulse rounded-3xl border border-[#7cdff4]/40 bg-[#dffaff]/30 motion-reduce:animate-none" />
                  </div>
                }
              >
                <LecturerOverviewThreeScene />
              </Suspense>
            </div>
            <div className="pointer-events-none absolute right-5 bottom-4 hidden items-center gap-2 rounded-full border border-[#d8e8f3] bg-white/76 px-3 py-1.5 text-[9px] font-semibold text-[#5b708c] shadow-sm backdrop-blur-md sm:flex">
              <span className="size-1.5 animate-pulse rounded-full bg-[#38cfa0] motion-reduce:animate-none" />
              Rubric 10.0 → Hậu kiểm
            </div>
          </div>
        </div>
      </section>

      {error && (
        <div
          className="flex flex-col gap-3 rounded-2xl border border-[#f0d1d5] bg-[#fff7f7] px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
          role="alert"
        >
          <p className="flex items-center gap-2 text-[11px] font-semibold text-[#9e3742]">
            <TriangleAlert className="size-4 shrink-0" aria-hidden="true" />
            {error}
          </p>
          <button
            type="button"
            onClick={() => setReloadKey((key) => key + 1)}
            className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-lg border border-[#e4bfc4] bg-white px-3 text-[10px] font-bold text-[#963743] transition hover:bg-[#fff0f1] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#b8404b]"
          >
            <RefreshCw className="size-3.5" aria-hidden="true" />
            Thử lại
          </button>
        </div>
      )}

      <section
        aria-label="Tình trạng ngân hàng câu hỏi"
        className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"
      >
        {metrics.map(({ label, value, helper, icon: Icon, tone }) => (
          <article
            key={label}
            className="group rounded-2xl border border-[#e1e7f0] bg-white p-4 shadow-[0_7px_26px_rgba(31,61,105,0.045)] transition duration-200 hover:-translate-y-0.5 hover:border-[#cfdbeb] hover:shadow-[0_12px_32px_rgba(31,61,105,0.08)]"
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-[10px] font-medium text-[#64748b]">{label}</p>
                <strong
                  className={cn(
                    'mt-3 block text-[1.7rem] font-bold tracking-[-0.045em] text-[#0b1830]',
                    isLoading && 'animate-pulse text-[#a8b4c4] motion-reduce:animate-none',
                  )}
                >
                  {isLoading ? '—' : String(value).padStart(2, '0')}
                </strong>
              </div>
              <span className={cn('flex size-9 items-center justify-center rounded-xl', tone)}>
                <Icon className="size-4" aria-hidden="true" />
              </span>
            </div>
            <p className="mt-1.5 text-[9px] leading-relaxed text-[#758399]">{helper}</p>
          </article>
        ))}
      </section>

      <section className="grid gap-4 lg:grid-cols-[minmax(0,1.28fr)_minmax(300px,0.72fr)]">
        <article className="overflow-hidden rounded-2xl border border-[#e1e7f0] bg-white shadow-[0_8px_30px_rgba(31,61,105,0.05)]">
          <div className="flex items-center justify-between border-b border-[#e8edf4] px-5 py-4">
            <div>
              <p className="text-[9px] font-bold tracking-[0.09em] text-[#56759d] uppercase">
                Không gian nghiệp vụ
              </p>
              <h2 className="mt-1 text-sm font-bold text-[#13213a]">Công việc của giảng viên</h2>
            </div>
            <span className="rounded-full bg-[#eef3ff] px-2.5 py-1 text-[9px] font-bold text-[#3562c8]">
              2 Main Flows
            </span>
          </div>

          <div className="grid gap-3 p-4 sm:grid-cols-2 sm:p-5">
            <Link
              to="/question-bank"
              className="group rounded-2xl border border-[#dce5f1] bg-[#fbfcff] p-4 transition hover:-translate-y-0.5 hover:border-[#afc5ec] hover:bg-[#f6f9ff] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
            >
              <div className="flex items-start justify-between gap-3">
                <span className="flex size-10 items-center justify-center rounded-xl bg-[#eaf1ff] text-[#245eea]">
                  <BookOpenCheck className="size-[18px]" aria-hidden="true" />
                </span>
                <span className="rounded-full bg-white px-2 py-1 font-mono text-[9px] font-bold text-[#50709a] shadow-sm">
                  MF-03
                </span>
              </div>
              <h3 className="mt-4 text-sm font-bold text-[#172640]">Ngân hàng đề & Rubric</h3>
              <p className="mt-1.5 text-[10px] leading-relaxed text-[#6d7d92]">
                Tạo thủ công hoặc dùng AI/FLM, chuẩn hóa Rubric 10.0 và gửi Trưởng Bộ môn phê duyệt.
              </p>
              <span className="mt-4 inline-flex items-center gap-1 text-[10px] font-bold text-[#245eea]">
                Mở ngân hàng đề
                <ChevronRight className="size-3.5 transition-transform group-hover:translate-x-0.5" />
              </span>
            </Link>

            <Link
              to="/lecturer/audit"
              className="group rounded-2xl border border-[#dce5f1] bg-[#fbfcff] p-4 transition hover:-translate-y-0.5 hover:border-[#c2b9ee] hover:bg-[#faf8ff] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#5c62da]"
            >
              <div className="flex items-start justify-between gap-3">
                <span className="flex size-10 items-center justify-center rounded-xl bg-[#f0edff] text-[#6458cf]">
                  <ClipboardCheck className="size-[18px]" aria-hidden="true" />
                </span>
                <span className="rounded-full bg-white px-2 py-1 font-mono text-[9px] font-bold text-[#6b5fa5] shadow-sm">
                  MF-04
                </span>
              </div>
              <h3 className="mt-4 text-sm font-bold text-[#172640]">Hậu kiểm & công bố điểm</h3>
              <p className="mt-1.5 text-[10px] leading-relaxed text-[#6d7d92]">
                Ưu tiên bài nghi ngờ, đối chiếu audio và transcript trước khi công bố điểm toàn ca.
              </p>
              <span className="mt-4 inline-flex items-center gap-1 text-[10px] font-bold text-[#5c55c5]">
                Mở cổng hậu kiểm
                <ChevronRight className="size-3.5 transition-transform group-hover:translate-x-0.5" />
              </span>
            </Link>
          </div>
        </article>

        <aside className="rounded-2xl border border-[#dce5f1] bg-[#0b1d38] p-5 text-white shadow-[0_14px_36px_rgba(9,30,62,0.16)]">
          <div className="flex items-center gap-2 text-[10px] font-bold tracking-[0.08em] text-[#80e4ee] uppercase">
            <ShieldCheck className="size-4" aria-hidden="true" />
            Cổng kiểm soát
          </div>
          <h2 className="mt-3 text-base font-bold tracking-[-0.025em]">Trước khi công bố điểm</h2>
          <ol className="mt-4 space-y-3">
            {[
              'Rà soát toàn bộ bài được đánh dấu nghi ngờ hoặc confidence dưới 0.70.',
              'Đối chiếu Audio R2, transcript Whisper và bằng chứng chấm theo rubric.',
              'Chỉ công bố khi 100% thí sinh đã có điểm hoàn chỉnh.',
            ].map((item, index) => (
              <li key={item} className="flex gap-3 text-[10px] leading-relaxed text-[#c2d0e4]">
                <span className="flex size-5 shrink-0 items-center justify-center rounded-full border border-[#3f5b80] bg-[#112b4d] font-mono text-[9px] font-bold text-[#74dce8]">
                  {index + 1}
                </span>
                {item}
              </li>
            ))}
          </ol>
          <p className="mt-5 rounded-xl border border-white/10 bg-white/5 p-3 text-[9px] leading-relaxed text-[#93a9c5]">
            Sau khi công bố, One-Way Lock chặn mọi thao tác cập nhật hoặc xóa điểm.
          </p>
        </aside>
      </section>
    </div>
  )
}

export { LecturerDashboardPage as DashboardPage }
