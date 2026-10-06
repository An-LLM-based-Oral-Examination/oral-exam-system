import {
  Component,
  lazy,
  Suspense,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  Award,
  BarChart3,
  BookOpenCheck,
  CalendarDays,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Clock3,
  Download,
  FileClock,
  GraduationCap,
  Info,
  LoaderCircle,
  LockKeyhole,
  MessageSquareText,
  RefreshCw,
  ShieldCheck,
  Sparkles,
  TimerReset,
  Trophy,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/lib/utils'

const StudentResultsThreeScene = lazy(() =>
  import('./StudentResultsThreeScene').then((module) => ({
    default: module.StudentResultsThreeScene,
  })),
)

interface ResultsSceneErrorBoundaryProps {
  children: ReactNode
  fallback: ReactNode
}

class ResultsSceneErrorBoundary extends Component<
  ResultsSceneErrorBoundaryProps,
  { hasError: boolean }
> {
  state = { hasError: false }

  static getDerivedStateFromError() {
    return { hasError: true }
  }

  render() {
    return this.state.hasError ? this.props.fallback : this.props.children
  }
}

type ResultCategory = 'official' | 'mock' | 'practice'
type ResultFilter = 'all' | ResultCategory
type ResultStatus = 'LOCKED' | 'PENDING_REVIEW' | 'COMPLETED'
type ResultSort = 'recent' | 'score'

interface RubricCriterion {
  id: string
  label: string
  score: number
  maxScore: number
  feedback: string
}

interface StudentResultRecord {
  id: string
  category: ResultCategory
  status: ResultStatus
  subjectCode: string
  subjectName: string
  activityLabel: string
  completedAt: Date
  durationMinutes: number
  aiScore?: number
  finalScore?: number
  isLocked: boolean
  rubric: RubricCriterion[]
  summary: string
  lecturerNote?: string
  lockedAt?: Date
  expectedReviewAt?: Date
}

interface CategoryStyle {
  label: string
  icon: LucideIcon
  iconClassName: string
  badgeClassName: string
}

const CATEGORY_STYLES: Record<ResultCategory, CategoryStyle> = {
  official: {
    label: 'Thi chính thức',
    icon: GraduationCap,
    iconClassName: 'bg-[#07172e] text-white',
    badgeClassName: 'bg-[#edf2ff] text-[#315dc2]',
  },
  mock: {
    label: 'Thi thử Mock Exam',
    icon: TimerReset,
    iconClassName: 'bg-[#edf1f7] text-[#51647e]',
    badgeClassName: 'bg-[#f0f3f8] text-[#52647b]',
  },
  practice: {
    label: 'Luyện tập',
    icon: Sparkles,
    iconClassName: 'bg-[#f0edff] text-[#6c5bd4]',
    badgeClassName: 'bg-[#f2efff] text-[#6854c8]',
  },
}

const FILTER_LABELS: Record<ResultFilter, string> = {
  all: 'Tất cả',
  official: 'Thi chính thức',
  mock: 'Thi thử (Mock)',
  practice: 'Luyện tập',
}

function addDays(date: Date, days: number) {
  return new Date(date.getTime() + days * 86_400_000)
}

function buildRubric(
  scores: [number, number, number, number],
  feedback: [string, string, string, string],
): RubricCriterion[] {
  const labels = [
    'Độ chính xác chuyên môn',
    'Lập luận & cấu trúc',
    'Diễn đạt & thuật ngữ',
    'Phản hồi câu hỏi mở rộng',
  ]

  return labels.map((label, index) => ({
    id: `criterion-${index + 1}`,
    label,
    score: scores[index],
    maxScore: 10,
    feedback: feedback[index],
  }))
}

function buildPreviewResults(now: Date): StudentResultRecord[] {
  return [
    {
      id: 'official-operating-systems',
      category: 'official',
      status: 'LOCKED',
      subjectCode: 'IT3010',
      subjectName: 'Kiến trúc Máy tính & Hệ điều hành',
      activityLabel: 'Vấn đáp tại Lab B402',
      completedAt: addDays(now, -3),
      durationMinutes: 20,
      aiScore: 8.8,
      finalScore: 9.0,
      isLocked: true,
      rubric: buildRubric(
        [9.2, 8.8, 9.0, 8.9],
        [
          'Nắm chắc cơ chế quản lý bộ nhớ và tiến trình.',
          'Lập luận tuần tự, có ví dụ minh họa phù hợp.',
          'Diễn đạt rõ ràng, dùng đúng thuật ngữ hệ điều hành.',
          'Xử lý tốt câu hỏi follow-up về deadlock.',
        ],
      ),
      summary:
        'Bài trả lời thể hiện nền tảng hệ điều hành vững, liên hệ đúng giữa lập lịch CPU, bộ nhớ ảo và đồng bộ tiến trình.',
      lecturerNote: 'Đồng ý với phần lớn đánh giá AI và điều chỉnh nhẹ tiêu chí lập luận.',
      lockedAt: addDays(now, -2),
    },
    {
      id: 'official-database',
      category: 'official',
      status: 'LOCKED',
      subjectCode: 'IT2000',
      subjectName: 'Cơ sở Dữ liệu',
      activityLabel: 'Vấn đáp tại Lab A305',
      completedAt: addDays(now, -8),
      durationMinutes: 25,
      aiScore: 8.5,
      finalScore: 8.5,
      isLocked: true,
      rubric: buildRubric(
        [8.8, 8.4, 8.5, 8.3],
        [
          'Phân biệt đúng các dạng chuẩn và phụ thuộc hàm.',
          'Luồng phân tích rõ nhưng cần kết luận ngắn gọn hơn.',
          'Thuật ngữ nhất quán và dễ theo dõi.',
          'Trả lời đúng câu hỏi mở rộng về chỉ mục B+ Tree.',
        ],
      ),
      summary:
        'Hiểu tốt chuẩn hóa dữ liệu, giao dịch và chỉ mục; cần cô đọng hơn khi giải thích chiến lược tối ưu truy vấn.',
      lecturerNote: 'Giữ nguyên điểm do AI đề xuất sau khi nghe lại toàn bộ bản ghi.',
      lockedAt: addDays(now, -6),
    },
    {
      id: 'official-software-architecture',
      category: 'official',
      status: 'LOCKED',
      subjectCode: 'SWE3003',
      subjectName: 'Kiến trúc & Thiết kế Phần mềm',
      activityLabel: 'Vấn đáp tại Lab B401',
      completedAt: addDays(now, -13),
      durationMinutes: 22,
      aiScore: 8.6,
      finalScore: 8.7,
      isLocked: true,
      rubric: buildRubric(
        [8.7, 8.9, 8.6, 8.5],
        [
          'Nhận diện đúng trade-off của kiến trúc phân lớp.',
          'So sánh pattern có hệ thống và có phản biện.',
          'Trình bày mạch lạc, thuật ngữ chính xác.',
          'Giải quyết tốt tình huống thay đổi yêu cầu.',
        ],
      ),
      summary:
        'Phân tích kiến trúc có chiều sâu, cân bằng được khả năng bảo trì, hiệu năng và chi phí thay đổi.',
      lecturerNote: 'Điều chỉnh +0.1 ở tiêu chí lập luận sau hậu kiểm.',
      lockedAt: addDays(now, -11),
    },
    {
      id: 'official-networking-pending',
      category: 'official',
      status: 'PENDING_REVIEW',
      subjectCode: 'IT3080',
      subjectName: 'Mạng Máy tính',
      activityLabel: 'Vấn đáp tại Lab B402',
      completedAt: addDays(now, -1),
      durationMinutes: 20,
      isLocked: false,
      rubric: [],
      summary: 'Bài thi đã được hệ thống tiếp nhận và chuyển vào hàng đợi hậu kiểm của Giảng viên.',
      expectedReviewAt: addDays(now, 2),
    },
    {
      id: 'mock-artificial-intelligence',
      category: 'mock',
      status: 'COMPLETED',
      subjectCode: 'AI301',
      subjectName: 'MOCK-AI-01 · Trí tuệ Nhân tạo',
      activityLabel: 'Thi thử vấn đáp',
      completedAt: addDays(now, -4),
      durationMinutes: 20,
      aiScore: 8.4,
      isLocked: false,
      rubric: buildRubric(
        [8.5, 8.2, 8.6, 8.3],
        [
          'Nắm được nền tảng tìm kiếm và biểu diễn tri thức.',
          'Cần làm rõ giả định trước khi chọn thuật toán.',
          'Giọng nói ổn định và dùng thuật ngữ phù hợp.',
          'Phản hồi đúng hướng với câu hỏi mở rộng.',
        ],
      ),
      summary:
        'Kết quả mô phỏng cho thấy kiến thức nền tốt; nên luyện thêm cách so sánh heuristic trong các trường hợp biên.',
    },
    {
      id: 'practice-semaphore',
      category: 'practice',
      status: 'COMPLETED',
      subjectCode: 'OS-PRACTICE-04',
      subjectName: 'Luyện tập: Semaphore & Concurrency',
      activityLabel: 'Luyện tập tương tác AI',
      completedAt: addDays(now, -6),
      durationMinutes: 12,
      aiScore: 9.2,
      isLocked: false,
      rubric: buildRubric(
        [9.3, 9.1, 9.2, 9.0],
        [
          'Giải thích chính xác semaphore đếm và semaphore nhị phân.',
          'Lập luận tốt qua ví dụ producer-consumer.',
          'Diễn đạt tự tin, nhịp độ phù hợp.',
          'Xử lý tốt tình huống race condition bổ sung.',
        ],
      ),
      summary:
        'Phiên luyện tập đạt chuẩn cao; có thể chuyển sang các bài toán đồng bộ nhiều tài nguyên phức tạp hơn.',
    },
  ]
}

function formatDate(date: Date) {
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
}

function formatTime(date: Date) {
  return new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date)
}

function parseFilter(searchParams: URLSearchParams): ResultFilter {
  const requestedFilter = searchParams.get('type')
  if (
    requestedFilter === 'official' ||
    requestedFilter === 'mock' ||
    requestedFilter === 'practice'
  ) {
    return requestedFilter
  }
  if (searchParams.get('source') === 'mock-exam') return 'mock'
  return 'all'
}

function isReleasedOfficialResult(result: StudentResultRecord) {
  return (
    result.category === 'official' &&
    result.status === 'LOCKED' &&
    result.isLocked &&
    typeof result.finalScore === 'number'
  )
}

function isPendingOfficialResult(result: StudentResultRecord) {
  return result.category === 'official' && !isReleasedOfficialResult(result)
}

function getVisibleScore(result: StudentResultRecord) {
  if (result.category === 'official') {
    return isReleasedOfficialResult(result) ? result.finalScore : undefined
  }
  return result.aiScore
}

function getScoreLabel(result: StudentResultRecord) {
  if (result.category === 'official') return 'Điểm công bố'
  if (result.category === 'mock') return 'Điểm mô phỏng'
  return 'Điểm luyện tập'
}

function ResultsSceneFallback({
  averageScore,
  pendingCount,
}: {
  averageScore: number | null
  pendingCount: number
}) {
  return (
    <div className="flex size-full items-center justify-center" aria-hidden="true">
      <div className="w-[210px] rotate-[-3deg] rounded-2xl border border-[#9dc9de] bg-[linear-gradient(135deg,#0b315d,#14677b)] p-4 shadow-[0_18px_45px_rgba(20,83,112,0.2)] sm:w-[240px]">
        <div className="h-1.5 w-14 rounded-full bg-[#55e0dc]" />
        <p className="mt-3 text-[9px] font-bold tracking-[0.12em] text-[#a8f5f1] uppercase">
          Academic score
        </p>
        <p className="mt-1 text-3xl font-black text-white">
          {averageScore === null ? '—' : averageScore.toFixed(1)}
          <span className="ml-1 text-xs font-semibold text-[#b8e3ec]">/10</span>
        </p>
        <div className="mt-3 flex items-center justify-between text-[9px] font-bold text-[#d6f7f5]">
          <span>LOCKED GRADES</span>
          <span>{pendingCount} PENDING</span>
        </div>
      </div>
    </div>
  )
}

export function ExamHistoryPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const results = useMemo(() => buildPreviewResults(new Date()), [])
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [lastUpdatedAt, setLastUpdatedAt] = useState(() => new Date())
  const refreshTimerRef = useRef<number | null>(null)

  const activeFilter = parseFilter(searchParams)
  const activeSort: ResultSort = searchParams.get('sort') === 'score' ? 'score' : 'recent'
  const selectedResultId = searchParams.get('result')
  const selectedResult = results.find((result) => result.id === selectedResultId)
  const releasedOfficialResults = results.filter(isReleasedOfficialResult)
  const pendingOfficialResults = results.filter(isPendingOfficialResult)
  const officialAverage = releasedOfficialResults.length
    ? releasedOfficialResults.reduce((total, result) => total + (result.finalScore ?? 0), 0) /
      releasedOfficialResults.length
    : null

  const filterCounts = useMemo<Record<ResultFilter, number>>(
    () => ({
      all: results.length,
      official: results.filter((result) => result.category === 'official').length,
      mock: results.filter((result) => result.category === 'mock').length,
      practice: results.filter((result) => result.category === 'practice').length,
    }),
    [results],
  )

  const visibleResults = useMemo(() => {
    const filtered =
      activeFilter === 'all'
        ? [...results]
        : results.filter((result) => result.category === activeFilter)

    return filtered.sort((left, right) => {
      if (activeSort === 'score') {
        return (getVisibleScore(right) ?? -1) - (getVisibleScore(left) ?? -1)
      }
      return right.completedAt.getTime() - left.completedAt.getTime()
    })
  }, [activeFilter, activeSort, results])

  useEffect(() => {
    if (!selectedResultId) return
    const scrollTimer = window.setTimeout(() => {
      document
        .getElementById(`result-detail-${selectedResultId}`)
        ?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    }, 120)
    return () => window.clearTimeout(scrollTimer)
  }, [selectedResultId])

  useEffect(
    () => () => {
      if (refreshTimerRef.current !== null) window.clearTimeout(refreshTimerRef.current)
    },
    [],
  )

  const updateQuery = (updates: Record<string, string | null>) => {
    const nextParams = new URLSearchParams(searchParams)
    Object.entries(updates).forEach(([key, value]) => {
      if (value === null) nextParams.delete(key)
      else nextParams.set(key, value)
    })
    setSearchParams(nextParams)
  }

  const handleFilterChange = (filter: ResultFilter) => {
    updateQuery({
      type: filter === 'all' ? null : filter,
      source: null,
      result: null,
    })
  }

  const handleResultToggle = (resultId: string) => {
    updateQuery({ result: selectedResultId === resultId ? null : resultId })
  }

  const handleRefresh = () => {
    if (isRefreshing) return
    setIsRefreshing(true)
    if (refreshTimerRef.current !== null) window.clearTimeout(refreshTimerRef.current)
    refreshTimerRef.current = window.setTimeout(() => {
      setLastUpdatedAt(new Date())
      setIsRefreshing(false)
      refreshTimerRef.current = null
    }, 850)
  }

  return (
    <div lang="vi" className="space-y-4 pb-5 sm:space-y-5">
      <style>{`
        @media print {
          @page { size: A4; margin: 14mm; }
          body * { visibility: hidden !important; }
          #student-results-print,
          #student-results-print * { visibility: visible !important; }
          #student-results-print {
            display: block !important;
            position: fixed;
            inset: 0;
            width: 100%;
            padding: 22px;
            background: white;
            color: #10203d;
          }
        }
      `}</style>

      <section id="student-results-print" className="hidden">
        <div className="rounded-2xl border-2 border-[#173f9c] p-8">
          <div className="border-b border-[#dce4ef] pb-5">
            <p className="text-sm font-bold tracking-[0.12em] text-[#315eca] uppercase">
              OralExam AI · University Tech
            </p>
            <p className="mt-4 inline-flex rounded-lg bg-[#fff1cf] px-3 py-2 text-xs font-bold text-[#7d540a]">
              BẢN DỮ LIỆU MINH HỌA · KHÔNG CÓ GIÁ TRỊ HỌC VỤ
            </p>
            <h2 className="mt-3 text-3xl font-bold">Bảng điểm vấn đáp đã công bố</h2>
            <p className="mt-2 text-sm text-[#52647b]">
              Dữ liệu demo cục bộ · Học kỳ Fall 2026 · Không gắn với hồ sơ sinh viên
            </p>
          </div>
          <table className="mt-6 w-full border-collapse text-left text-sm">
            <thead>
              <tr className="border-b-2 border-[#173f9c]">
                <th className="py-3">Mã học phần</th>
                <th className="py-3">Tên học phần</th>
                <th className="py-3">Ngày thi</th>
                <th className="py-3 text-right">Điểm khóa</th>
              </tr>
            </thead>
            <tbody>
              {releasedOfficialResults.map((result) => (
                <tr key={result.id} className="border-b border-[#dce4ef]">
                  <td className="py-3 font-semibold">{result.subjectCode}</td>
                  <td className="py-3">{result.subjectName}</td>
                  <td className="py-3">{formatDate(result.completedAt)}</td>
                  <td className="py-3 text-right font-bold">
                    {result.finalScore?.toFixed(1)} / 10
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="mt-6 flex items-center justify-between rounded-xl bg-[#f1f5ff] p-4 text-sm">
            <span>Điểm trung bình chính thức</span>
            <strong className="text-xl text-[#245eea]">
              {officialAverage === null ? '—' : `${officialAverage.toFixed(1)} / 10`}
            </strong>
          </div>
          <p className="mt-5 text-xs leading-relaxed text-[#52647b]">
            Chỉ bao gồm các học phần đã được Giảng viên hậu kiểm và khóa điểm một chiều. Dữ liệu
            hiện tại là bản xem trước frontend, chưa phải chứng từ học vụ được ký số.
          </p>
        </div>
      </section>

      <section className="relative isolate overflow-hidden rounded-[24px] border border-[#dce7f3] bg-[linear-gradient(118deg,#ffffff_0%,#f6fbff_55%,#e7f8ff_100%)] shadow-[0_14px_45px_rgba(31,61,105,0.08)]">
        <div
          className="pointer-events-none absolute inset-0 opacity-55"
          style={{
            backgroundImage:
              'linear-gradient(rgba(43,112,175,0.055) 1px, transparent 1px), linear-gradient(90deg, rgba(43,112,175,0.055) 1px, transparent 1px)',
            backgroundSize: '28px 28px',
            maskImage: 'linear-gradient(90deg, black 0%, transparent 78%)',
          }}
        />

        <div className="relative z-10 p-5 sm:p-7 lg:min-h-[238px] lg:w-[61%] lg:p-8">
          <div className="flex flex-wrap items-center gap-2">
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#cddafd] bg-white/90 px-3 py-1 text-[10px] font-bold tracking-[0.04em] text-[#315bb7] uppercase shadow-sm">
              <BarChart3 className="size-3.5" /> Báo cáo năng lực học kỳ
            </span>
            <span className="rounded-full bg-[#e9f0fa] px-2 py-1 text-[9px] font-bold text-[#4f6178]">
              Dữ liệu xem trước FE
            </span>
          </div>
          <h1 className="mt-3 text-[27px] leading-[1.08] font-bold tracking-[-0.04em] text-[#10213e] sm:text-[35px]">
            Kết quả & Bảng điểm Vấn đáp
          </h1>
          <p className="mt-2 max-w-[650px] text-[11px] leading-relaxed text-[#5f7088] sm:text-xs">
            Tra cứu kết quả luyện tập, thi thử và các điểm thi chính thức đã được Giảng viên hậu
            kiểm, khóa một chiều và công bố.
          </p>

          <div className="mt-5 flex flex-col gap-2.5 sm:flex-row sm:flex-wrap">
            <button
              type="button"
              onClick={() => window.print()}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#cfd9e7] bg-white px-4 text-[10px] font-bold text-[#344861] shadow-sm transition hover:-translate-y-0.5 hover:border-[#9eb3cf] hover:bg-[#f8faff] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] motion-reduce:transform-none"
            >
              <Download className="size-4 text-[#315eca]" /> In bản xem trước
            </button>
            <button
              type="button"
              onClick={handleRefresh}
              disabled={isRefreshing}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#245eea] px-4 text-[10px] font-bold text-white shadow-[0_10px_24px_rgba(36,94,234,0.22)] transition hover:-translate-y-0.5 hover:bg-[#1b50cc] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] disabled:cursor-wait disabled:opacity-70 motion-reduce:transform-none"
            >
              {isRefreshing ? (
                <LoaderCircle className="size-4 animate-spin" />
              ) : (
                <RefreshCw className="size-4" />
              )}
              {isRefreshing ? 'Đang cập nhật...' : 'Cập nhật bản xem trước'}
            </button>
          </div>
          <p className="mt-2 text-[10px] text-[#52647b]">
            Hiển thị cập nhật lúc {formatTime(lastUpdatedAt)} · Chưa kết nối Student Results API
          </p>
        </div>

        <div className="relative h-[150px] sm:h-[175px] lg:absolute lg:inset-y-0 lg:right-0 lg:h-auto lg:w-[45%]">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_55%_52%,rgba(64,211,231,0.23),transparent_58%)]" />
          <ResultsSceneErrorBoundary
            fallback={
              <ResultsSceneFallback
                averageScore={officialAverage}
                pendingCount={pendingOfficialResults.length}
              />
            }
          >
            <Suspense
              fallback={
                <ResultsSceneFallback
                  averageScore={officialAverage}
                  pendingCount={pendingOfficialResults.length}
                />
              }
            >
              <StudentResultsThreeScene
                averageScore={officialAverage}
                pendingCount={pendingOfficialResults.length}
              />
            </Suspense>
          </ResultsSceneErrorBoundary>
        </div>
      </section>

      <section className="grid gap-3 md:grid-cols-2">
        <article className="relative overflow-hidden rounded-[20px] border border-[#dce9e4] bg-[linear-gradient(135deg,#ffffff,#f2fbf7)] p-5 shadow-[0_8px_26px_rgba(31,61,105,0.05)]">
          <div className="absolute -top-10 -right-8 size-32 rounded-full bg-[#65d9b0]/10 blur-2xl" />
          <div className="relative flex items-start justify-between gap-4">
            <div>
              <p className="flex items-center gap-2 text-[10px] font-bold tracking-[0.05em] text-[#277662] uppercase">
                <span className="size-2 rounded-full bg-[#32c695]" /> Điểm chính thức đã công bố
              </p>
              <p className="mt-3 text-[30px] leading-none font-bold tracking-[-0.04em] text-[#13263f]">
                {releasedOfficialResults.length.toString().padStart(2, '0')}
                <span className="ml-2 text-sm font-semibold text-[#53667e]">Môn học</span>
              </p>
              <span className="mt-3 inline-flex rounded-full bg-[#dff7ee] px-2.5 py-1 text-[10px] font-bold text-[#1c6b57]">
                Điểm trung bình chính thức:{' '}
                {officialAverage === null ? 'Chưa có' : `${officialAverage.toFixed(1)} / 10`}
              </span>
            </div>
            <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-white text-[#2aa67f] shadow-sm">
              <Trophy className="size-5" />
            </span>
          </div>
        </article>

        <article className="relative overflow-hidden rounded-[20px] border border-[#eee5d2] bg-[linear-gradient(135deg,#ffffff,#fff9ec)] p-5 shadow-[0_8px_26px_rgba(31,61,105,0.05)]">
          <div className="absolute -top-10 -right-8 size-32 rounded-full bg-[#f1b84a]/10 blur-2xl" />
          <div className="relative flex items-start justify-between gap-4">
            <div>
              <p className="flex items-center gap-2 text-[10px] font-bold tracking-[0.05em] text-[#86590d] uppercase">
                <span className="size-2 rounded-full bg-[#efb63d]" /> Đang chờ Giảng viên hậu kiểm
              </p>
              <p className="mt-3 text-[30px] leading-none font-bold tracking-[-0.04em] text-[#13263f]">
                {pendingOfficialResults.length.toString().padStart(2, '0')}
                <span className="ml-2 text-sm font-semibold text-[#53667e]">Bài thi</span>
              </p>
              <span className="mt-3 inline-flex rounded-full bg-[#fff0c8] px-2.5 py-1 text-[10px] font-bold text-[#7d540a]">
                Không hiển thị điểm dự thảo trước khi khóa
              </span>
            </div>
            <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-white text-[#d39627] shadow-sm">
              <FileClock className="size-5" />
            </span>
          </div>
        </article>
      </section>

      <section className="rounded-[20px] border border-[#dfe6f0] bg-white p-3 shadow-[0_8px_28px_rgba(31,61,105,0.045)] sm:p-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="overflow-x-auto pb-1 sm:pb-0">
            <div className="flex min-w-max gap-1.5" aria-label="Lọc kết quả theo loại">
              {(Object.keys(FILTER_LABELS) as ResultFilter[]).map((filter) => {
                const isActive = activeFilter === filter
                return (
                  <button
                    key={filter}
                    type="button"
                    aria-pressed={isActive}
                    onClick={() => handleFilterChange(filter)}
                    className={cn(
                      'inline-flex min-h-10 items-center gap-2 rounded-xl px-3.5 text-[10px] font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]',
                      isActive
                        ? 'bg-[#eaf0ff] text-[#2858c4] shadow-sm'
                        : 'text-[#52647b] hover:bg-[#f4f7fb] hover:text-[#344861]',
                    )}
                  >
                    {FILTER_LABELS[filter]}
                    <span
                      className={cn(
                        'rounded-full px-1.5 py-0.5 text-[9px]',
                        isActive ? 'bg-white text-[#315eca]' : 'bg-[#edf1f6] text-[#52647b]',
                      )}
                    >
                      {filterCounts[filter]}
                    </span>
                  </button>
                )
              })}
            </div>
          </div>

          <label className="flex min-h-10 items-center gap-2 rounded-xl border border-[#dce4ee] bg-[#fafbfd] px-3 text-[10px] font-semibold text-[#52647b] focus-within:border-[#6688df] focus-within:ring-2 focus-within:ring-[#245eea]/20">
            Sắp xếp
            <select
              value={activeSort}
              onChange={(event) =>
                updateQuery({ sort: event.target.value === 'score' ? 'score' : null })
              }
              className="rounded-md bg-transparent text-[10px] font-bold text-[#30445e] outline-none"
              aria-label="Sắp xếp kết quả"
            >
              <option value="recent">Mới nhất</option>
              <option value="score">Điểm cao nhất</option>
            </select>
          </label>
        </div>
      </section>

      <section aria-labelledby="results-list-title" className="space-y-3">
        <div className="flex items-center justify-between gap-4 px-1">
          <div>
            <h2 id="results-list-title" className="text-sm font-bold text-[#26374e]">
              {FILTER_LABELS[activeFilter]}
            </h2>
            <p className="mt-1 text-[10px] text-[#52647b]">
              {visibleResults.length} kết quả · Chỉ điểm `LOCKED` được tính là điểm chính thức
            </p>
          </div>
          <span className="hidden text-[10px] font-semibold text-[#52647b] sm:inline">
            Trang 1 / 1
          </span>
        </div>

        {visibleResults.length === 0 ? (
          <div className="rounded-[20px] border border-dashed border-[#cdd8e6] bg-white px-6 py-12 text-center">
            <BookOpenCheck className="mx-auto size-8 text-[#8da0b8]" />
            <h3 className="mt-3 text-sm font-bold text-[#33465f]">Chưa có kết quả phù hợp</h3>
            <p className="mt-1 text-xs text-[#52647b]">Hãy thử một bộ lọc khác.</p>
          </div>
        ) : (
          visibleResults.map((result) => {
            const categoryStyle = CATEGORY_STYLES[result.category]
            const CategoryIcon = categoryStyle.icon
            const isExpanded = selectedResult?.id === result.id
            const visibleScore = getVisibleScore(result)
            const isReleased = isReleasedOfficialResult(result)
            const isPending = isPendingOfficialResult(result)

            return (
              <article
                key={result.id}
                className={cn(
                  'overflow-hidden rounded-[18px] border bg-white shadow-[0_7px_24px_rgba(31,61,105,0.045)] transition duration-200',
                  isExpanded
                    ? 'border-[#9bb0e9] shadow-[0_12px_34px_rgba(43,84,160,0.1)]'
                    : 'border-[#dfe6f0] hover:-translate-y-0.5 hover:border-[#c6d3e3] hover:shadow-[0_11px_30px_rgba(31,61,105,0.08)] motion-reduce:transform-none',
                  isPending && 'border-l-[3px] border-l-[#efb441]',
                )}
              >
                <div className="grid gap-3 p-4 sm:p-5 lg:grid-cols-[minmax(0,1fr)_105px_125px_112px] lg:items-center">
                  <div className="flex min-w-0 items-start gap-3">
                    <span
                      className={cn(
                        'flex size-11 shrink-0 items-center justify-center rounded-xl shadow-sm',
                        categoryStyle.iconClassName,
                      )}
                    >
                      <CategoryIcon className="size-5" />
                    </span>
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-1.5">
                        <span
                          className={cn(
                            'rounded-full px-2 py-1 text-[9px] font-bold',
                            categoryStyle.badgeClassName,
                          )}
                        >
                          {categoryStyle.label}
                        </span>
                        {isReleased && (
                          <span className="inline-flex items-center gap-1 rounded-full bg-[#e6f8f1] px-2 py-1 text-[9px] font-bold text-[#1d6f5a]">
                            <LockKeyhole className="size-3" /> Đã công bố · Đã khóa điểm
                          </span>
                        )}
                        {isPending && (
                          <span className="inline-flex items-center gap-1 rounded-full bg-[#fff1cf] px-2 py-1 text-[9px] font-bold text-[#7d540a]">
                            <FileClock className="size-3" /> Chờ hậu kiểm
                          </span>
                        )}
                        {result.status === 'COMPLETED' && result.category !== 'official' && (
                          <span className="inline-flex items-center gap-1 rounded-full bg-[#eef2f7] px-2 py-1 text-[9px] font-bold text-[#52647b]">
                            <CheckCircle2 className="size-3" /> Đã hoàn thành
                          </span>
                        )}
                      </div>
                      <h3 className="mt-2 truncate text-[12px] font-bold text-[#26374e] sm:text-[13px]">
                        {result.subjectName}
                      </h3>
                      <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-[10px] text-[#56677f]">
                        <span className="inline-flex items-center gap-1">
                          <CalendarDays className="size-3" /> {formatDate(result.completedAt)}
                        </span>
                        <span className="inline-flex items-center gap-1">
                          <Clock3 className="size-3" /> {result.durationMinutes} phút vấn đáp
                        </span>
                        <span>Mã HP: {result.subjectCode}</span>
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center justify-between rounded-xl bg-[#f7f9fc] px-3 py-2 lg:block lg:bg-transparent lg:px-0 lg:py-0">
                    <span className="text-[9px] font-bold tracking-[0.04em] text-[#52647b] uppercase">
                      {getScoreLabel(result)}
                    </span>
                    <p className="mt-0.5 text-sm font-bold text-[#20334e]">
                      {visibleScore === undefined ? '—' : visibleScore.toFixed(1)}
                      {visibleScore !== undefined && (
                        <span className="ml-0.5 text-[10px] text-[#52647b]">/10</span>
                      )}
                    </p>
                  </div>

                  <div className="flex items-center justify-between rounded-xl bg-[#f7f9fc] px-3 py-2 lg:block lg:bg-transparent lg:px-0 lg:py-0">
                    <span className="text-[9px] font-bold tracking-[0.04em] text-[#52647b] uppercase">
                      Trạng thái
                    </span>
                    <p
                      className={cn(
                        'mt-0.5 text-[10px] font-bold',
                        isPending
                          ? 'text-[#8a5d0d]'
                          : isReleased
                            ? 'text-[#1f725c]'
                            : 'text-[#526582]',
                      )}
                    >
                      {isPending
                        ? 'Chưa công bố điểm'
                        : isReleased
                          ? 'LOCKED'
                          : result.category === 'mock'
                            ? 'Không tính học phần'
                            : 'Phản hồi AI'}
                    </p>
                  </div>

                  <button
                    type="button"
                    onClick={() => handleResultToggle(result.id)}
                    aria-expanded={isExpanded}
                    aria-controls={`result-detail-${result.id}`}
                    className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl bg-[#f0f4fa] px-3 text-[10px] font-bold text-[#344a67] transition hover:bg-[#e7eef8] hover:text-[#2458c7] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
                  >
                    {isExpanded ? 'Ẩn chi tiết' : 'Xem chi tiết'}
                    {isExpanded ? (
                      <ChevronDown className="size-3.5 rotate-180" />
                    ) : (
                      <ChevronRight className="size-3.5" />
                    )}
                  </button>
                </div>

                {isExpanded && (
                  <div
                    id={`result-detail-${result.id}`}
                    className="border-t border-[#e6ebf2] bg-[linear-gradient(135deg,#f8faff,#f4f8fc)] p-4 sm:p-5"
                  >
                    {isPending ? (
                      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_280px]">
                        <div className="rounded-2xl border border-[#e6dfcf] bg-white p-4">
                          <h4 className="flex items-center gap-2 text-[11px] font-bold text-[#33465e]">
                            <FileClock className="size-4 text-[#d59a2d]" /> Tiến trình hậu kiểm
                          </h4>
                          <ol className="mt-4 space-y-3">
                            {[
                              ['Bài thi đã được niêm phong', 'Hoàn tất'],
                              ['AI xử lý nội bộ theo rubric', 'Hoàn tất'],
                              ['Giảng viên nghe lại và kiểm duyệt', 'Đang thực hiện'],
                              ['Khóa điểm một chiều & công bố', 'Chưa bắt đầu'],
                            ].map(([label, state], index) => (
                              <li key={label} className="flex items-center gap-3">
                                <span
                                  className={cn(
                                    'flex size-7 shrink-0 items-center justify-center rounded-full text-[10px] font-bold',
                                    index < 2
                                      ? 'bg-[#e4f7ef] text-[#238066]'
                                      : index === 2
                                        ? 'bg-[#fff0cb] text-[#9d6a12]'
                                        : 'bg-[#edf1f6] text-[#56677f]',
                                  )}
                                >
                                  {index < 2 ? <CheckCircle2 className="size-3.5" /> : index + 1}
                                </span>
                                <div>
                                  <p className="text-[10px] font-bold text-[#35475f]">{label}</p>
                                  <p className="mt-0.5 text-[10px] text-[#56677f]">{state}</p>
                                </div>
                              </li>
                            ))}
                          </ol>
                        </div>
                        <div className="rounded-2xl border border-[#e6dfcf] bg-[#fffaf0] p-4">
                          <ShieldCheck className="size-5 text-[#c88c24]" />
                          <h4 className="mt-3 text-[11px] font-bold text-[#4b3e28]">
                            Điểm đang được bảo vệ
                          </h4>
                          <p className="mt-2 text-[10px] leading-relaxed text-[#796b54]">
                            Portal không hiển thị điểm AI dự thảo, rubric hoặc bản ghi trước khi
                            Giảng viên hoàn tất hậu kiểm và khóa điểm.
                          </p>
                          <p className="mt-3 rounded-xl bg-white px-3 py-2 text-[10px] font-semibold text-[#7d540a]">
                            Dự kiến hoàn tất trước{' '}
                            {formatDate(result.expectedReviewAt ?? new Date())}
                          </p>
                        </div>
                      </div>
                    ) : (
                      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.25fr)_minmax(260px,0.75fr)]">
                        <div className="rounded-2xl border border-[#e1e7ef] bg-white p-4">
                          <h4 className="flex items-center gap-2 text-[11px] font-bold text-[#33465e]">
                            <Award className="size-4 text-[#4568cb]" /> Rubric đánh giá
                          </h4>
                          <div className="mt-4 grid gap-3 sm:grid-cols-2">
                            {result.rubric.map((criterion) => (
                              <article
                                key={criterion.id}
                                className="rounded-xl border border-[#e7ecf2] bg-[#fafbfd] p-3"
                              >
                                <div className="flex items-center justify-between gap-3">
                                  <h5 className="text-[10px] font-bold text-[#3a4b62]">
                                    {criterion.label}
                                  </h5>
                                  <span className="text-[10px] font-bold text-[#315eca]">
                                    {criterion.score.toFixed(1)}/{criterion.maxScore}
                                  </span>
                                </div>
                                <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[#e3e9f1]">
                                  <div
                                    className="h-full rounded-full bg-[linear-gradient(90deg,#315eea,#48cfe1)]"
                                    style={{
                                      width: `${(criterion.score / criterion.maxScore) * 100}%`,
                                    }}
                                  />
                                </div>
                                <p className="mt-2 text-[10px] leading-relaxed text-[#56677f]">
                                  {criterion.feedback}
                                </p>
                              </article>
                            ))}
                          </div>
                        </div>

                        <div className="space-y-3">
                          <div className="rounded-2xl border border-[#e1e7ef] bg-white p-4">
                            <h4 className="flex items-center gap-2 text-[11px] font-bold text-[#33465e]">
                              <MessageSquareText className="size-4 text-[#4670ce]" /> Nhận xét tổng
                              quan
                            </h4>
                            <p className="mt-3 text-[10px] leading-relaxed text-[#52647b]">
                              {result.summary}
                            </p>
                          </div>

                          {isReleased ? (
                            <div className="rounded-2xl border border-[#cfe8df] bg-[#f0faf6] p-4">
                              <p className="flex items-center gap-2 text-[10px] font-bold text-[#247c65]">
                                <LockKeyhole className="size-4" /> Điểm đã khóa một chiều
                              </p>
                              <p className="mt-2 text-[10px] leading-relaxed text-[#46695e]">
                                {result.lecturerNote}
                              </p>
                              <p className="mt-2 text-[9px] font-semibold text-[#526a62]">
                                Khóa lúc {formatTime(result.lockedAt ?? new Date())} ·{' '}
                                {formatDate(result.lockedAt ?? new Date())}
                              </p>
                            </div>
                          ) : (
                            <div className="rounded-2xl border border-[#dfe5ee] bg-[#f5f7fa] p-4 text-[10px] leading-relaxed text-[#52647b]">
                              {result.category === 'mock'
                                ? 'Điểm mô phỏng và phản hồi AI không được tính vào điểm học phần.'
                                : 'Đây là phản hồi luyện tập nhằm cải thiện kỹ năng, không phải điểm chính thức.'}
                            </div>
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </article>
            )
          })
        )}
      </section>

      <footer className="flex flex-col gap-2 rounded-2xl border border-[#dfe6ef] bg-white px-4 py-3 text-[10px] text-[#52647b] shadow-[0_6px_20px_rgba(31,61,105,0.035)] sm:flex-row sm:items-center sm:justify-between">
        <p className="flex items-start gap-2 leading-relaxed sm:items-center">
          <Info className="mt-0.5 size-3.5 shrink-0 text-[#4169c7] sm:mt-0" />
          Điểm thi chính thức chỉ được phát hành sau khi Giảng viên hậu kiểm và thực hiện khóa điểm
          một chiều.
        </p>
        <span className="shrink-0 font-semibold">Trang 1 / 1</span>
      </footer>

      <div className="sr-only" aria-live="polite">
        {isRefreshing
          ? 'Đang cập nhật dữ liệu xem trước.'
          : `Dữ liệu xem trước được cập nhật lúc ${formatTime(lastUpdatedAt)}.`}
      </div>
    </div>
  )
}

export { ExamHistoryPage as StudentResultsPage }
