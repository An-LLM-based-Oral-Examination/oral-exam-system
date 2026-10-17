import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  ArrowRight,
  BookOpenCheck,
  CheckCircle2,
  ClipboardCheck,
  Eye,
  FileClock,
  FilePenLine,
  LoaderCircle,
  Plus,
  RefreshCw,
  ScanSearch,
  ShieldCheck,
  Sparkles,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react'
import { OverlayDrawer, OverlayModal } from '@/components/ui/OverlayDrawer'
import { useAuth } from '@/hooks/useAuth'
import { questionService } from '@/services/question.service'
import type {
  QuestionApprovalStatus,
  QuestionDetail,
  QuestionSummary,
} from '@/types/rubric.types'
import { cn } from '@/lib/utils'

interface DashboardMetric {
  label: string
  value: number
  helper: string
  icon: LucideIcon
  tone: string
}

const STATUS_META: Record<QuestionApprovalStatus, { label: string; className: string }> = {
  DRAFT: { label: 'Bản nháp', className: 'border-[#CBD5E1] bg-[#F8FAFC] text-[#475569]' },
  SUBMITTED_FOR_REVIEW: {
    label: 'Chờ duyệt',
    className: 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]',
  },
  APPROVED: {
    label: 'Đã phê duyệt',
    className: 'border-emerald-200 bg-emerald-50 text-[#047857]',
  },
  NEEDS_REVISION: {
    label: 'Cần chỉnh sửa',
    className: 'border-rose-200 bg-rose-50 text-[#BE123C]',
  },
  REJECTED: {
    label: 'Đã từ chối',
    className: 'border-rose-200 bg-rose-50 text-[#9F1239]',
  },
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

function formatDate(value?: string) {
  if (!value) return 'Chưa xác định'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Chưa xác định'
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
}

export function LecturerDashboardPage() {
  const { user } = useAuth()
  const [questions, setQuestions] = useState<QuestionSummary[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [reloadKey, setReloadKey] = useState(0)

  const [isWorkflowModalOpen, setIsWorkflowModalOpen] = useState(false)
  const [selectedQuestionId, setSelectedQuestionId] = useState<string | null>(null)
  const [questionDetail, setQuestionDetail] = useState<QuestionDetail | null>(null)
  const [isDetailLoading, setIsDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState('')

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
          user?.role === 'Instructor'
            ? allQuestions.filter((question) => isOwnedByUser(question, user?.id))
            : allQuestions

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

  const openQuickView = async (questionId: string) => {
    setSelectedQuestionId(questionId)
    setQuestionDetail(null)
    setDetailError('')
    setIsDetailLoading(true)
    try {
      const loaded = await questionService.getQuestionById(questionId)
      setQuestionDetail(loaded)
    } catch (loadErr) {
      setDetailError(getErrorMessage(loadErr))
    } finally {
      setIsDetailLoading(false)
    }
  }

  const metrics = useMemo<DashboardMetric[]>(
    () => [
      {
        label: 'Bản nháp đang biên soạn',
        value: countStatus(questions, 'DRAFT'),
        helper: 'Có thể chọn và gửi duyệt theo môn',
        icon: FilePenLine,
        tone: 'border border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]',
      },
      {
        label: 'Đang chờ Bộ môn duyệt',
        value: countStatus(questions, 'SUBMITTED_FOR_REVIEW'),
        helper: 'Theo dõi phản hồi trước khi sử dụng',
        icon: FileClock,
        tone: 'border border-amber-200 bg-amber-50 text-[#B45309]',
      },
      {
        label: 'Cần chỉnh sửa',
        value: countStatus(questions, 'NEEDS_REVISION'),
        helper: 'Ưu tiên xử lý góp ý đã nhận',
        icon: TriangleAlert,
        tone: 'border border-rose-200 bg-rose-50 text-[#BE123C]',
      },
      {
        label: 'Đã được phê duyệt',
        value: countStatus(questions, 'APPROVED'),
        helper: 'Sẵn sàng trong ngân hàng đề chính thức',
        icon: CheckCircle2,
        tone: 'border border-emerald-200 bg-emerald-50 text-[#047857]',
      },
    ],
    [questions],
  )

  const recentQuestions = useMemo(() => questions.slice(0, 6), [questions])
  const now = new Date()

  return (
    <div className="space-y-4 sm:space-y-5">
      <section className="overflow-hidden rounded-2xl border border-[#E2E8F0] border-t-4 border-t-[#F26F21] bg-white p-5 shadow-sm sm:p-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="mb-2 flex flex-wrap items-center gap-2 text-xs font-semibold">
              <span className="inline-flex items-center gap-1.5 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1 font-bold text-[#C2410C]">
                <Sparkles className="size-3.5" aria-hidden="true" />
                Không gian Giảng viên · FPT Education
              </span>
              <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-[#047857]">
                <span className="size-2 rounded-full bg-[#10B981]" />
                Hệ thống Khảo thí & Ngân hàng đề
              </span>
            </div>

            <h1 className="text-xl font-bold tracking-tight text-[#1E293B] sm:text-2xl">
              {getGreeting(now.getHours())},{' '}
              <span className="text-[#F26F21]">{user?.fullName ?? 'Giảng viên'}</span>
            </h1>
            <p className="mt-1 max-w-2xl text-xs leading-relaxed text-[#475569] sm:text-sm">
              Quản lý ngân hàng câu hỏi chuẩn Barem Rubric 10.0 điểm và hậu kiểm bằng chứng ca thi.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <button
              type="button"
              onClick={() => setIsWorkflowModalOpen(true)}
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 text-xs font-bold text-[#C2410C] transition hover:bg-[#FFEDD5] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
            >
              📋 Xem quy trình khảo thí
            </button>
            <Link
              to="/lecturer/audit?shiftId=00000000-0000-0000-0000-000000000023"
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl border border-[#E2E8F0] bg-white px-3.5 text-xs font-bold text-[#1E293B] shadow-2xs transition hover:border-[#FED7AA] hover:bg-[#FFF7ED] hover:text-[#C2410C] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
            >
              <ScanSearch className="size-4 text-[#F26F21]" aria-hidden="true" />
              Mở Cổng Hậu kiểm
            </Link>
            <Link
              to="/question-bank/create"
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-sm transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
            >
              <Plus className="size-4" aria-hidden="true" />
              Tạo câu hỏi & Rubric
            </Link>
          </div>
        </div>
      </section>

      {error && (
        <div
          className="flex flex-col gap-3 rounded-2xl border border-[#FECDD3] bg-[#FFF1F2] px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
          role="alert"
        >
          <p className="flex items-center gap-2 text-xs font-semibold text-[#9F1239] sm:text-sm">
            <TriangleAlert className="size-4 shrink-0" aria-hidden="true" />
            {error}
          </p>
          <button
            type="button"
            onClick={() => setReloadKey((key) => key + 1)}
            className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-lg border border-[#FDA4AF] bg-white px-3 text-xs font-bold text-[#9F1239] transition hover:bg-[#FFE4E6] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#BE123C]"
          >
            <RefreshCw className="size-3.5" aria-hidden="true" />
            Thử lại
          </button>
        </div>
      )}

      <section
        aria-label="Tình trạng ngân hàng câu hỏi"
        className="grid gap-3.5 sm:grid-cols-2 xl:grid-cols-4"
      >
        {metrics.map(({ label, value, helper, icon: Icon, tone }) => (
          <article
            key={label}
            className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-sm transition hover:border-[#FED7AA]"
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-xs font-semibold text-[#475569]">{label}</p>
                <strong
                  className={cn(
                    'mt-2 block text-2xl font-bold tracking-tight text-[#1E293B] sm:text-3xl',
                    isLoading && 'animate-pulse text-[#94A3B8] motion-reduce:animate-none',
                  )}
                >
                  {isLoading ? '—' : String(value).padStart(2, '0')}
                </strong>
              </div>
              <span className={cn('flex size-10 items-center justify-center rounded-xl', tone)}>
                <Icon className="size-5" aria-hidden="true" />
              </span>
            </div>
            <p className="mt-2 text-xs leading-relaxed text-[#64748B]">{helper}</p>
          </article>
        ))}
      </section>

      <section className="overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-sm">
        <div className="flex flex-col gap-3 border-b border-[#E2E8F0] px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-xs font-bold tracking-wider text-[#C2410C] uppercase">
              Ngân hàng câu hỏi gần đây
            </p>
            <h2 className="mt-0.5 text-base font-bold text-[#1E293B]">
              Danh sách câu hỏi mới nhất
            </h2>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setReloadKey((key) => key + 1)}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] px-3 text-xs font-semibold text-[#475569] transition hover:border-[#FED7AA] hover:bg-[#FFF7ED] hover:text-[#C2410C]"
            >
              <RefreshCw className="size-3.5" aria-hidden="true" />
              Làm mới
            </button>
            <Link
              to="/question-bank"
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 text-xs font-bold text-[#C2410C] transition hover:bg-[#FFEDD5]"
            >
              Mở toàn bộ Ngân hàng đề
              <ArrowRight className="size-3.5" aria-hidden="true" />
            </Link>
          </div>
        </div>

        {isLoading ? (
          <div className="flex min-h-56 flex-col items-center justify-center px-5 text-center">
            <LoaderCircle className="size-6 animate-spin text-[#F26F21]" />
            <p className="mt-2.5 text-xs font-semibold text-[#475569] sm:text-sm">
              Đang tải danh sách câu hỏi...
            </p>
          </div>
        ) : recentQuestions.length === 0 ? (
          <div className="flex min-h-56 flex-col items-center justify-center px-5 text-center">
            <BookOpenCheck className="size-8 text-[#F26F21]" />
            <h3 className="mt-2.5 text-sm font-bold text-[#1E293B] sm:text-base">
              Chưa có câu hỏi nào trong danh sách
            </h3>
            <p className="mt-1 max-w-md text-xs text-[#64748B] sm:text-sm">
              Hãy bắt đầu tạo câu hỏi thủ công hoặc sinh tự động từ FLM Syllabus theo CLO.
            </p>
            <Link
              to="/question-bank/create"
              className="mt-4 inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white transition hover:bg-[#D95B16]"
            >
              <Plus className="size-4" />
              Tạo câu hỏi đầu tiên
            </Link>
          </div>
        ) : (
          <div className="divide-y divide-[#E2E8F0]">
            {recentQuestions.map((question) => {
              const statusMeta = STATUS_META[question.approvalStatus]
              return (
                <article
                  key={question.id}
                  className="flex flex-col gap-3 px-5 py-3.5 transition hover:bg-[#FFF7ED]/50 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="rounded-md border border-[#FED7AA] bg-[#FFF7ED] px-2 py-0.5 font-mono text-xs font-bold text-[#C2410C]">
                        {question.courseCode}
                      </span>
                      <span
                        className={cn(
                          'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-bold',
                          statusMeta.className,
                        )}
                      >
                        {statusMeta.label}
                      </span>
                      <span className="rounded-md border border-[#E2E8F0] bg-[#F8FAFC] px-2 py-0.5 text-xs font-semibold text-[#475569]">
                        {question.bloomLevel}
                      </span>
                      <span className="text-xs text-[#64748B]">
                        {formatDate(question.createdAt)}
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={() => void openQuickView(question.id)}
                      className="mt-1.5 block max-w-full truncate text-left text-sm font-bold text-[#1E293B] transition hover:text-[#F26F21] focus-visible:outline-2 focus-visible:outline-[#F26F21]"
                    >
                      {question.title}
                    </button>
                  </div>

                  <div className="flex shrink-0 items-center gap-3">
                    <span
                      className={cn(
                        'rounded-lg px-2.5 py-1 font-mono text-xs font-black',
                        question.totalRubricScore === 10
                          ? 'bg-emerald-50 text-[#047857]'
                          : 'bg-rose-50 text-[#BE123C]',
                      )}
                    >
                      {question.totalRubricScore.toFixed(1)} / 10đ
                    </span>
                    <button
                      type="button"
                      onClick={() => void openQuickView(question.id)}
                      className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3 text-xs font-bold text-[#C2410C] transition hover:bg-[#F26F21] hover:text-white"
                    >
                      <Eye className="size-3.5" aria-hidden="true" />
                      Xem nhanh
                    </button>
                  </div>
                </article>
              )
            })}
          </div>
        )}
      </section>

      <OverlayDrawer
        open={Boolean(selectedQuestionId)}
        onClose={() => setSelectedQuestionId(null)}
        title={questionDetail?.title ?? 'Chi tiết câu hỏi & Barem Rubric'}
        subtitle={
          questionDetail
            ? `Học phần ${questionDetail.courseCode} · Bậc Bloom: ${questionDetail.bloomLevel}`
            : 'Đang tải thông tin câu hỏi từ ngân hàng đề...'
        }
        badge={questionDetail ? STATUS_META[questionDetail.approvalStatus].label : 'Xem nhanh'}
        width="xl"
        footer={
          <div className="flex flex-wrap items-center justify-between gap-2">
            <button
              type="button"
              onClick={() => setSelectedQuestionId(null)}
              className="inline-flex min-h-9 items-center justify-center rounded-xl border border-[#CBD5E1] bg-white px-4 text-xs font-bold text-[#475569] transition hover:bg-[#F8FAFC]"
            >
              Đóng
            </button>
            <Link
              to="/question-bank"
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-xs transition hover:bg-[#D95B16]"
            >
              Quản lý tại Ngân hàng đề
              <ArrowRight className="size-3.5" />
            </Link>
          </div>
        }
      >
        {isDetailLoading ? (
          <div className="flex min-h-60 flex-col items-center justify-center text-center">
            <LoaderCircle className="size-7 animate-spin text-[#F26F21]" />
            <p className="mt-2.5 text-xs font-semibold text-[#475569]">
              Đang tải nội dung câu hỏi và Rubric...
            </p>
          </div>
        ) : detailError ? (
          <div className="rounded-2xl border border-rose-200 bg-rose-50 p-4 text-xs font-semibold text-[#BE123C]">
            {detailError}
          </div>
        ) : questionDetail ? (
          <div className="space-y-4">
            <section className="rounded-2xl border border-[#E2E8F0] bg-[#F8FAFC] p-4">
              <p className="text-xs font-bold tracking-wider text-[#C2410C] uppercase">
                Nội dung câu hỏi vấn đáp
              </p>
              <p className="mt-2 text-sm leading-relaxed whitespace-pre-wrap text-[#1E293B]">
                {questionDetail.content}
              </p>
            </section>

            <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4">
              <div className="flex items-center justify-between gap-2">
                <div>
                  <p className="text-xs font-bold tracking-wider text-[#64748B] uppercase">
                    Barem Rubric chuẩn hóa
                  </p>
                  <h3 className="mt-0.5 text-sm font-bold text-[#1E293B]">
                    {questionDetail.rubric.name}
                  </h3>
                </div>
                <span className="rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-1 font-mono text-xs font-black text-[#047857]">
                  Tổng: {questionDetail.rubric.totalMaxScore.toFixed(1)} / 10.0đ
                </span>
              </div>
              <div className="mt-3 space-y-2">
                {questionDetail.rubric.criteria.map((criterion, index) => (
                  <div
                    key={criterion.id}
                    className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <p className="text-xs font-bold text-[#1E293B] sm:text-sm">
                        {index + 1}. {criterion.criterionName}
                      </p>
                      <span className="shrink-0 font-mono text-xs font-black text-[#C2410C]">
                        {criterion.maxScore.toFixed(1)}đ
                      </span>
                    </div>
                    {criterion.description && (
                      <p className="mt-1 text-xs leading-relaxed text-[#475569]">
                        {criterion.description}
                      </p>
                    )}
                  </div>
                ))}
              </div>
            </section>

            <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4">
              <p className="text-xs font-bold tracking-wider text-[#64748B] uppercase">
                Đáp án mẫu (Model Answer)
              </p>
              <p className="mt-2 text-xs leading-relaxed whitespace-pre-wrap text-[#334155] sm:text-sm">
                {questionDetail.sampleAnswer || 'Chưa có câu trả lời mẫu.'}
              </p>
            </section>

            {questionDetail.reviewNotes && (
              <section className="rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] p-4">
                <p className="text-xs font-bold text-[#C2410C] uppercase">
                  Phản hồi thẩm định của Trưởng Bộ môn
                </p>
                <p className="mt-1.5 text-xs leading-relaxed whitespace-pre-wrap text-[#7C2D12] sm:text-sm">
                  {questionDetail.reviewNotes}
                </p>
              </section>
            )}
          </div>
        ) : null}
      </OverlayDrawer>

      <OverlayModal
        open={isWorkflowModalOpen}
        onClose={() => setIsWorkflowModalOpen(false)}
        title="Quy trình Biên soạn Đề & Chấm duyệt Khảo thí"
        subtitle="Hướng dẫn biên soạn ngân hàng đề, chuẩn hóa Barem Rubric 10.0 và hậu kiểm ca thi chính thức."
        badge="Cổng kiểm soát học thuật"
        size="lg"
        footer={
          <div className="flex flex-wrap items-center justify-end gap-2">
            <button
              type="button"
              onClick={() => setIsWorkflowModalOpen(false)}
              className="inline-flex min-h-9 items-center justify-center rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white transition hover:bg-[#D95B16]"
            >
              Đã hiểu quy trình
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div className="grid gap-3.5 sm:grid-cols-2">
            <div className="rounded-2xl border border-[#FED7AA] bg-[#FFF7ED]/60 p-4">
              <div className="flex items-center justify-between gap-2">
                <span className="flex size-9 items-center justify-center rounded-xl border border-[#FED7AA] bg-white text-[#C2410C]">
                  <BookOpenCheck className="size-4" />
                </span>
                <span className="rounded-full border border-[#FED7AA] bg-white px-2.5 py-0.5 font-mono text-xs font-bold text-[#C2410C]">
                  Biên soạn đề
                </span>
              </div>
              <h3 className="mt-3 text-sm font-bold text-[#1E293B]">
                Ngân hàng đề & Rubric Studio 10.0
              </h3>
              <p className="mt-1.5 text-xs leading-relaxed text-[#475569]">
                Biên soạn thủ công hoặc dùng AI sinh câu hỏi từ FLM Syllabus theo CLO. Barem Rubric
                bắt buộc có tổng điểm chính xác 10.0 điểm và Model Answer tối thiểu 50 ký tự trước
                khi gửi Trưởng Bộ môn phê duyệt.
              </p>
            </div>

            <div className="rounded-2xl border border-[#E2E8F0] bg-[#F8FAFC] p-4">
              <div className="flex items-center justify-between gap-2">
                <span className="flex size-9 items-center justify-center rounded-xl border border-[#FED7AA] bg-[#FFF7ED] text-[#F26F21]">
                  <ClipboardCheck className="size-4" />
                </span>
                <span className="rounded-full border border-[#E2E8F0] bg-white px-2.5 py-0.5 font-mono text-xs font-bold text-[#475569]">
                  Hậu kiểm & Công bố
                </span>
              </div>
              <h3 className="mt-3 text-sm font-bold text-[#1E293B]">
                Hậu kiểm bằng chứng & Công bố điểm
              </h3>
              <p className="mt-1.5 text-xs leading-relaxed text-[#475569]">
                Ưu tiên kiểm tra Nhóm 1 (bài nghi ngờ hoặc độ tin cậy AI &lt; 0.70), nghe lại Audio
                R2 (`STT_MSSV.webm`), đối chiếu Transcript Whisper & AI CoT trước khi bấm Công bố
                điểm.
              </p>
            </div>
          </div>

          <div className="rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] p-4">
            <div className="flex items-center gap-2 text-xs font-bold tracking-wider text-[#C2410C] uppercase">
              <ShieldCheck className="size-4" aria-hidden="true" />3 nguyên tắc bắt buộc trước khi
              công bố điểm
            </div>
            <ol className="mt-3 space-y-2.5">
              {[
                'Rà soát toàn bộ bài được đánh dấu nghi ngờ hoặc confidence dưới 0.70.',
                'Đối chiếu Audio R2, transcript Whisper và lập luận chấm điểm theo Rubric.',
                'Chỉ công bố khi 100% thí sinh đã có điểm hoàn chỉnh. Sau khi công bố, niêm phong bảng điểm chính thức sẽ khóa chỉnh sửa trái phép.',
              ].map((item, index) => (
                <li
                  key={item}
                  className="flex gap-2.5 text-xs leading-relaxed text-[#7C2D12] sm:text-sm"
                >
                  <span className="flex size-5 shrink-0 items-center justify-center rounded-full border border-[#F26F21]/40 bg-white font-mono text-xs font-bold text-[#C2410C]">
                    {index + 1}
                  </span>
                  <span>{item}</span>
                </li>
              ))}
            </ol>
          </div>
        </div>
      </OverlayModal>
    </div>
  )
}

export { LecturerDashboardPage as DashboardPage }
