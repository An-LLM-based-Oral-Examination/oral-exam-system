import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  BookOpenCheck,
  Check,
  CheckCircle2,
  CircleDashed,
  Eye,
  Filter,
  LoaderCircle,
  Plus,
  RefreshCw,
  Search,
  Send,
  Sparkles,
  X,
} from 'lucide-react'
import { questionService } from '@/services/question.service'
import { useAuth } from '@/hooks/useAuth'
import type { UserRole } from '@/types/auth.types'
import type {
  QuestionApprovalStatus,
  QuestionDetail,
  QuestionFilters,
  QuestionSummary,
} from '@/types/rubric.types'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'

const STATUS_OPTIONS: Array<{ value: QuestionApprovalStatus | ''; label: string }> = [
  { value: '', label: 'Tất cả trạng thái' },
  { value: 'DRAFT', label: 'Bản nháp' },
  { value: 'SUBMITTED_FOR_REVIEW', label: 'Chờ duyệt' },
  { value: 'APPROVED', label: 'Đã phê duyệt' },
  { value: 'NEEDS_REVISION', label: 'Cần chỉnh sửa' },
  { value: 'REJECTED', label: 'Đã từ chối' },
]

const BLOOM_OPTIONS = [
  { value: '', label: 'Tất cả bậc Bloom' },
  { value: 'Remember', label: 'Remember' },
  { value: 'Understand', label: 'Understand' },
  { value: 'Apply', label: 'Apply' },
  { value: 'Analyze', label: 'Analyze' },
  { value: 'Evaluate', label: 'Evaluate' },
  { value: 'Create', label: 'Create' },
]

const DIFFICULTY_OPTIONS = [
  { value: '', label: 'Mọi độ khó' },
  { value: 'easy', label: 'Dễ' },
  { value: 'medium', label: 'Trung bình' },
  { value: 'hard', label: 'Khó' },
]

const STATUS_META: Record<QuestionApprovalStatus, { label: string; className: string }> = {
  DRAFT: { label: 'Bản nháp', className: 'border-[#4988C4]/45 bg-[#f3f6fa] text-[#52647d]' },
  SUBMITTED_FOR_REVIEW: {
    label: 'Chờ duyệt',
    className: 'border-[#ead5a7] bg-[#fff8e9] text-[#8d6118]',
  },
  APPROVED: {
    label: 'Đã phê duyệt',
    className: 'border-[#bce4d7] bg-[#effaf6] text-[#197158]',
  },
  NEEDS_REVISION: {
    label: 'Cần chỉnh sửa',
    className: 'border-[#efc8cc] bg-[#fff2f3] text-[#a93d48]',
  },
  REJECTED: {
    label: 'Đã từ chối',
    className: 'border-[#e5c5d4] bg-[#fff2f7] text-[#963b66]',
  },
}

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể hoàn tất yêu cầu. Vui lòng thử lại.'
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

function isUuid(value: string) {
  return /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value)
}

function getSourceLabel(source: QuestionSummary['source']) {
  return source === 'flm_api' ? 'AI từ FLM' : 'Thủ công'
}

function canManageQuestion(
  question: QuestionSummary,
  role: UserRole | undefined,
  userId: string | undefined,
) {
  if (role === 'Admin') return true
  return Boolean(userId && question.submittedBy?.toLowerCase() === userId.toLowerCase())
}

function StatusBadge({ status }: { status: QuestionApprovalStatus }) {
  const meta = STATUS_META[status]
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-bold',
        meta.className,
      )}
    >
      {meta.label}
    </span>
  )
}

export function QuestionStudioPage() {
  const { user } = useAuth()
  const [questions, setQuestions] = useState<QuestionSummary[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [pageNumber, setPageNumber] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [filters, setFilters] = useState<QuestionFilters>({
    pageNumber: 1,
    pageSize: 12,
  })
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [submissionNotes, setSubmissionNotes] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [detail, setDetail] = useState<QuestionDetail | null>(null)
  const [isDetailOpen, setIsDetailOpen] = useState(false)
  const [isDetailLoading, setIsDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState('')
  const closeDetailButtonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    let isActive = true
    const loadQuestions = async () => {
      setIsLoading(true)
      setError('')
      try {
        const result = await questionService.getQuestions({ ...filters, pageNumber })
        if (!isActive) return
        setQuestions(result.items)
        setTotalCount(result.totalCount)
        setTotalPages(Math.max(result.totalPages, 1))
        const visibleDraftIds = new Set(
          result.items
            .filter(
              (question) =>
                question.approvalStatus === 'DRAFT' &&
                canManageQuestion(question, user?.role, user?.id),
            )
            .map(({ id }) => id),
        )
        setSelectedIds((current) => new Set([...current].filter((id) => visibleDraftIds.has(id))))
      } catch (loadError) {
        if (isActive) setError(getErrorMessage(loadError))
      } finally {
        if (isActive) setIsLoading(false)
      }
    }

    void loadQuestions()
    return () => {
      isActive = false
    }
  }, [filters, pageNumber, reloadKey, user?.id, user?.role])

  useEffect(() => {
    if (!isDetailOpen) return

    const previousOverflow = document.body.style.overflow
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsDetailOpen(false)
    }

    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', handleKeyDown)
    closeDetailButtonRef.current?.focus()

    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isDetailOpen])

  const selectedQuestions = useMemo(
    () => questions.filter((question) => selectedIds.has(question.id)),
    [questions, selectedIds],
  )
  const selectedCourseId = selectedQuestions[0]?.courseId
  const draftQuestions = questions.filter(
    (question) =>
      question.approvalStatus === 'DRAFT' && canManageQuestion(question, user?.role, user?.id),
  )
  const allSelectableDraftsChecked =
    draftQuestions.length > 0 &&
    draftQuestions
      .filter((question) => !selectedCourseId || question.courseId === selectedCourseId)
      .every((question) => selectedIds.has(question.id))

  const applyFilters = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setPageNumber(1)
    setFilters((current) => ({ ...current, search: searchInput.trim() || undefined }))
    setSelectedIds(new Set())
  }

  const updateFilter = (key: keyof QuestionFilters, value: string) => {
    setPageNumber(1)
    setFilters((current) => ({ ...current, [key]: value || undefined }))
    setSelectedIds(new Set())
  }

  const toggleQuestion = (question: QuestionSummary) => {
    if (question.approvalStatus !== 'DRAFT') return
    if (!canManageQuestion(question, user?.role, user?.id)) {
      setNotice('Bạn chỉ có thể gửi thẩm định các bản nháp do chính mình tạo.')
      return
    }
    if (
      selectedCourseId &&
      selectedCourseId !== question.courseId &&
      !selectedIds.has(question.id)
    ) {
      setNotice('Chỉ có thể gửi cùng lúc các câu hỏi thuộc một môn học.')
      return
    }

    setNotice('')
    setSelectedIds((current) => {
      const next = new Set(current)
      if (next.has(question.id)) next.delete(question.id)
      else next.add(question.id)
      return next
    })
  }

  const toggleAllDrafts = () => {
    const eligible = draftQuestions.filter(
      (question) => !selectedCourseId || question.courseId === selectedCourseId,
    )
    setSelectedIds((current) => {
      const next = new Set(current)
      if (allSelectableDraftsChecked) eligible.forEach(({ id }) => next.delete(id))
      else eligible.forEach(({ id }) => next.add(id))
      return next
    })
  }

  const openDetail = async (questionId: string) => {
    setIsDetailOpen(true)
    setIsDetailLoading(true)
    setDetail(null)
    setDetailError('')
    try {
      setDetail(await questionService.getQuestionById(questionId))
    } catch (loadError) {
      setDetailError(getErrorMessage(loadError))
    } finally {
      setIsDetailLoading(false)
    }
  }

  const submitForReview = async () => {
    if (!selectedCourseId || selectedIds.size === 0) return
    if (!isUuid(user?.id ?? '')) {
      setError(
        'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.',
      )
      return
    }
    if (selectedQuestions.some((question) => !canManageQuestion(question, user?.role, user?.id))) {
      setError(
        'Danh sách chọn có câu hỏi không thuộc quyền quản lý của bạn. Vui lòng tải lại trang.',
      )
      setSelectedIds(new Set())
      return
    }
    setIsSubmitting(true)
    setError('')
    setNotice('')
    try {
      const result = await questionService.batchSubmitForReview({
        courseId: selectedCourseId,
        questionIds: [...selectedIds],
        submissionNotes: submissionNotes.trim() || undefined,
        lecturerId: user?.id,
      })
      setNotice(result.message)
      setSelectedIds(new Set())
      setSubmissionNotes('')
      setReloadKey((key) => key + 1)
    } catch (submitError) {
      setError(getErrorMessage(submitError))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className={ds.page}>
      <section className={cn(ds.banner, 'p-5 sm:p-6')}>
        <div className="pointer-events-none absolute -top-24 right-10 size-64 rounded-full bg-[#55d9f1]/10 blur-3xl" />
        <div className="relative flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className={ds.bannerChip}>
              <BookOpenCheck className="size-4" aria-hidden="true" />
              MF-03 · Ngân hàng đề & Rubric
            </div>
            <h1 className={ds.bannerTitle}>
              Ngân hàng câu hỏi của giảng viên
            </h1>
            <p className={ds.bannerText}>
              Biên soạn câu hỏi thủ công hoặc bằng AI, kiểm tra Barem Rubric đúng 10.0 điểm và gửi
              Trưởng Bộ môn thẩm định trước khi đưa vào ngân hàng đề chính thức.
            </p>
          </div>
          <Link
            to="/question-bank/create"
            className={cn(ds.bannerButton, 'shrink-0')}
          >
            <Plus className="size-4" aria-hidden="true" />
            Tạo câu hỏi & Rubric
          </Link>
        </div>
      </section>

      {(error || notice) && (
        <div
          className={cn(
            'flex items-start gap-2 rounded-2xl border px-4 py-3 text-sm font-semibold',
            error
              ? 'border-[#efc8cc] bg-[#fff5f6] text-[#a43c47]'
              : 'border-[#bce4d7] bg-[#effaf6] text-[#176f58]',
          )}
          role={error ? 'alert' : 'status'}
        >
          {error ? (
            <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          ) : (
            <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          )}
          <span>{error || notice}</span>
        </div>
      )}

      <form
        onSubmit={applyFilters}
        className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_8px_28px_rgba(31,61,105,0.045)]"
        aria-label="Bộ lọc câu hỏi"
      >
        <div className="grid gap-3 lg:grid-cols-[minmax(240px,1.35fr)_repeat(3,minmax(150px,0.65fr))_auto]">
          <label className="relative block">
            <span className="sr-only">Tìm kiếm câu hỏi</span>
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#8a99ad]" />
            <input
              type="search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Tìm tiêu đề hoặc nội dung..."
              className="min-h-11 w-full rounded-xl border border-[#4988C4]/45 bg-white pr-3 pl-9 text-sm text-[#0F2854] transition outline-none placeholder:text-[#8aa0b8] focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
            />
          </label>

          <label className="relative block">
            <span className="sr-only">Lọc theo trạng thái</span>
            <select
              value={filters.approvalStatus ?? ''}
              onChange={(event) => updateFilter('approvalStatus', event.target.value)}
              className="min-h-11 w-full appearance-none rounded-xl border border-[#4988C4]/45 bg-white px-3 text-sm font-semibold text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
            >
              {STATUS_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className="relative block">
            <span className="sr-only">Lọc theo bậc Bloom</span>
            <select
              value={filters.bloomLevel ?? ''}
              onChange={(event) => updateFilter('bloomLevel', event.target.value)}
              className="min-h-11 w-full appearance-none rounded-xl border border-[#4988C4]/45 bg-white px-3 text-sm font-semibold text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
            >
              {BLOOM_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className="relative block">
            <span className="sr-only">Lọc theo độ khó</span>
            <select
              value={filters.difficulty ?? ''}
              onChange={(event) => updateFilter('difficulty', event.target.value)}
              className="min-h-11 w-full appearance-none rounded-xl border border-[#4988C4]/45 bg-white px-3 text-sm font-semibold text-[#0F2854] transition outline-none focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
            >
              {DIFFICULTY_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <button
            type="submit"
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-sm font-bold text-[#0F2854] transition hover:border-[#4988C4]/45 hover:bg-[#edf3fc] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
          >
            <Filter className="size-3.5" aria-hidden="true" />
            Áp dụng
          </button>
        </div>
      </form>

      {selectedIds.size > 0 && (
        <section
          className="rounded-2xl border border-[#bfd0f2] bg-[linear-gradient(135deg,#BDE8F5,#BDE8F5)] p-4 shadow-[0_10px_30px_rgba(36,94,234,0.07)]"
          aria-label="Gửi câu hỏi để thẩm định"
        >
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end">
            <div className="min-w-0 flex-1">
              <p className="flex items-center gap-2 text-sm font-bold text-[#1C4D8D]">
                <Send className="size-4" aria-hidden="true" />
                Đã chọn {selectedIds.size} bản nháp · {selectedQuestions[0]?.courseCode}
              </p>
              <label className="mt-3 block">
                <span className="text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                  Ghi chú gửi Trưởng Bộ môn
                </span>
                <textarea
                  value={submissionNotes}
                  onChange={(event) => setSubmissionNotes(event.target.value)}
                  maxLength={1000}
                  placeholder="Nêu phạm vi nội dung hoặc điểm cần lưu ý khi thẩm định..."
                  className="mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#4988C4]/45 bg-white/90 px-3 py-2.5 text-sm text-[#0F2854] transition outline-none placeholder:text-[#8aa0b8] focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </label>
            </div>
            <div className="flex shrink-0 flex-col gap-2 sm:flex-row lg:flex-col">
              <button
                type="button"
                onClick={() => setSelectedIds(new Set())}
                className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4]/45 bg-white px-4 text-xs font-bold text-[#5b6b84] transition hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
              >
                <X className="size-3.5" aria-hidden="true" />
                Bỏ chọn
              </button>
              <button
                type="button"
                onClick={() => void submitForReview()}
                disabled={isSubmitting}
                className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-bold text-white shadow-[0_8px_18px_rgba(36,94,234,0.2)] transition hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isSubmitting ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <Send className="size-3.5" aria-hidden="true" />
                )}
                Gửi thẩm định
              </button>
            </div>
          </div>
        </section>
      )}

      <section className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_9px_30px_rgba(31,61,105,0.05)]">
        <div className="flex flex-col gap-3 border-b border-[#e8edf4] px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
          <div>
            <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">Danh sách câu hỏi</h2>
            <p className="mt-1 text-xs text-[#5b6b84]">
              {isLoading ? 'Đang đồng bộ...' : `${totalCount} câu hỏi phù hợp bộ lọc`}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={toggleAllDrafts}
              disabled={draftQuestions.length === 0}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#4988C4]/45 bg-white px-3 text-xs font-bold text-[#5b6b84] transition hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45"
            >
              <Check className="size-3.5" aria-hidden="true" />
              {allSelectableDraftsChecked ? 'Bỏ chọn bản nháp' : 'Chọn bản nháp'}
            </button>
            <button
              type="button"
              onClick={() => setReloadKey((key) => key + 1)}
              className="flex size-9 items-center justify-center rounded-lg border border-[#4988C4]/45 text-[#5b6b84] transition hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
              aria-label="Tải lại danh sách"
            >
              <RefreshCw className="size-3.5" aria-hidden="true" />
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="flex min-h-72 flex-col items-center justify-center px-5 text-center">
            <LoaderCircle className="size-7 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
            <p className="mt-3 text-sm font-semibold text-[#5b6b84]">
              Đang tải ngân hàng câu hỏi...
            </p>
          </div>
        ) : questions.length === 0 ? (
          <div className="flex min-h-72 flex-col items-center justify-center px-5 text-center">
            <span className="flex size-12 items-center justify-center rounded-2xl bg-[#eef3fb] text-[#6d82a0]">
              <CircleDashed className="size-5" aria-hidden="true" />
            </span>
            <h3 className="mt-3 text-sm font-bold text-[#253750]">Chưa có câu hỏi phù hợp</h3>
            <p className="mt-1 max-w-md text-xs leading-relaxed text-[#7b899c]">
              Thay đổi bộ lọc hoặc tạo bản nháp đầu tiên cho môn học bạn phụ trách.
            </p>
            <Link
              to="/question-bank/create"
              className="mt-4 inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
            >
              <Plus className="size-3.5" />
              Tạo câu hỏi
            </Link>
          </div>
        ) : (
          <div className="divide-y divide-[#4988C4]/30">
            {questions.map((question) => {
              const isSelected = selectedIds.has(question.id)
              const isOwned = canManageQuestion(question, user?.role, user?.id)
              const canSelect =
                question.approvalStatus === 'DRAFT' &&
                isOwned &&
                (!selectedCourseId || selectedCourseId === question.courseId || isSelected)

              return (
                <article
                  key={question.id}
                  className={cn(
                    'grid gap-3 px-4 py-4 transition sm:px-5 lg:grid-cols-[auto_minmax(0,1fr)_140px_140px_auto] lg:items-center',
                    isSelected ? 'bg-[#4988C4]/16' : 'hover:bg-white',
                  )}
                >
                  <label className="flex items-center">
                    <span className="sr-only">Chọn câu hỏi {question.title}</span>
                    <input
                      type="checkbox"
                      checked={isSelected}
                      disabled={!canSelect}
                      onChange={() => toggleQuestion(question)}
                      className="size-4 rounded border-[#bfcce0] text-[#1C4D8D] accent-[#4988C4] focus:ring-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                    />
                  </label>

                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                        {question.courseCode}
                      </span>
                      <StatusBadge status={question.approvalStatus} />
                      {user?.role !== 'Admin' && (
                        <span
                          className={cn(
                            'rounded-full px-2 py-1 text-xs font-bold',
                            isOwned ? 'bg-[#eaf9f4] text-[#17745b]' : 'bg-[#f1f3f6] text-[#6f7c8e]',
                          )}
                        >
                          {isOwned ? 'Của bạn' : 'Giảng viên khác'}
                        </span>
                      )}
                    </div>
                    <h3 className="mt-2 line-clamp-2 text-sm leading-snug font-bold text-[#0F2854]">
                      {question.title}
                    </h3>
                    <p className="mt-1 text-xs text-[#8794a6]">
                      Tạo ngày {formatDate(question.createdAt)} · Nguồn{' '}
                      {getSourceLabel(question.source)}
                    </p>
                  </div>

                  <div className="flex items-center gap-2 lg:block">
                    <span className="text-xs font-semibold text-[#8995a6] lg:block">Bloom</span>
                    <span className="mt-0.5 inline-flex rounded-lg bg-[#4988C4]/16 px-2 py-1 text-xs font-bold text-[#1C4D8D]">
                      {question.bloomLevel}
                    </span>
                  </div>

                  <div className="flex items-center gap-2 lg:block">
                    <span className="text-xs font-semibold text-[#8995a6] lg:block">Barem</span>
                    <span
                      className={cn(
                        'mt-0.5 text-sm font-black',
                        question.totalRubricScore === 10 ? 'text-[#168367]' : 'text-[#b8404b]',
                      )}
                    >
                      {question.totalRubricScore.toFixed(1)} / 10
                    </span>
                  </div>

                  <button
                    type="button"
                    onClick={() => void openDetail(question.id)}
                    className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4]/45 bg-white px-3 text-xs font-bold text-[#49617f] transition hover:border-[#b8c8dd] hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
                  >
                    <Eye className="size-3.5" aria-hidden="true" />
                    Xem chi tiết
                  </button>
                </article>
              )
            })}
          </div>
        )}

        {!isLoading && questions.length > 0 && (
          <div className="flex items-center justify-between border-t border-[#e8edf4] bg-white px-4 py-3 sm:px-5">
            <button
              type="button"
              onClick={() => setPageNumber((page) => Math.max(1, page - 1))}
              disabled={pageNumber <= 1}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#4988C4]/45 bg-white px-3 text-xs font-bold text-[#536780] transition hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ArrowLeft className="size-3.5" />
              Trang trước
            </button>
            <span className="text-xs font-semibold text-[#708096]">
              Trang {pageNumber} / {totalPages}
            </span>
            <button
              type="button"
              onClick={() => setPageNumber((page) => Math.min(totalPages, page + 1))}
              disabled={pageNumber >= totalPages}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#4988C4]/45 bg-white px-3 text-xs font-bold text-[#536780] transition hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-40"
            >
              Trang sau
              <ArrowRight className="size-3.5" />
            </button>
          </div>
        )}
      </section>

      {isDetailOpen && (
        <>
          <button
            type="button"
            className="fixed inset-0 z-40 bg-[#0F2854]/45 backdrop-blur-[2px]"
            onClick={() => setIsDetailOpen(false)}
            aria-label="Đóng chi tiết câu hỏi"
          />
          <aside
            className="fixed inset-y-0 right-0 z-50 w-full max-w-xl overflow-y-auto border-l border-[#4988C4]/45 bg-[#4988C4]/16 shadow-[-20px_0_60px_rgba(10,28,57,0.2)]"
            aria-label="Chi tiết câu hỏi"
            role="dialog"
            aria-modal="true"
          >
            <div className="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-[#4988C4]/45 bg-white/94 px-5 backdrop-blur-xl">
              <div>
                <p className="text-xs font-bold tracking-[0.08em] text-[#1C4D8D] uppercase">
                  Chi tiết câu hỏi
                </p>
                <h2 className="mt-0.5 text-base font-bold text-[#0F2854] sm:text-lg">
                  Barem & lịch sử thẩm định
                </h2>
              </div>
              <button
                ref={closeDetailButtonRef}
                type="button"
                onClick={() => setIsDetailOpen(false)}
                className="flex size-9 items-center justify-center rounded-xl border border-[#4988C4]/45 text-[#5f7088] transition hover:bg-[#f2f5f9] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
                aria-label="Đóng bảng chi tiết"
              >
                <X className="size-4" />
              </button>
            </div>

            {isDetailLoading ? (
              <div className="flex min-h-[70vh] flex-col items-center justify-center">
                <LoaderCircle className="size-7 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
                <p className="mt-3 text-sm font-semibold text-[#708096]">
                  Đang tải chi tiết...
                </p>
              </div>
            ) : detailError ? (
              <div className="m-5 rounded-2xl border border-[#efc8cc] bg-[#fff5f6] p-4 text-sm text-[#a43c47]">
                <p className="flex items-start gap-2 font-semibold">
                  <AlertCircle className="mt-0.5 size-4 shrink-0" />
                  {detailError}
                </p>
              </div>
            ) : detail ? (
              <div className="space-y-4 p-4 sm:p-5">
                <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-sm">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                      {detail.courseCode}
                    </span>
                    <StatusBadge status={detail.approvalStatus} />
                    <span className="rounded-full bg-[#4988C4]/16 px-2 py-1 text-xs font-bold text-[#6558c8]">
                      {detail.bloomLevel}
                    </span>
                  </div>
                  <h3 className="mt-3 text-base leading-snug font-bold text-[#182a45]">
                    {detail.title}
                  </h3>
                  <p className="mt-3 text-sm leading-relaxed whitespace-pre-wrap text-[#586a82]">
                    {detail.content}
                  </p>
                </section>

                <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-sm">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
                        Barem Rubric
                      </p>
                      <h3 className="mt-1 text-sm font-bold text-[#0F2854]">
                        {detail.rubric.name}
                      </h3>
                    </div>
                    <span
                      className={cn(
                        'rounded-xl px-3 py-2 text-sm font-black',
                        detail.rubric.totalMaxScore === 10
                          ? 'bg-[#eaf9f4] text-[#14765c]'
                          : 'bg-[#fff0f1] text-[#ae3945]',
                      )}
                    >
                      {detail.rubric.totalMaxScore.toFixed(1)} / 10
                    </span>
                  </div>
                  <div className="mt-4 space-y-2">
                    {detail.rubric.criteria.map((criterion, index) => (
                      <div
                        key={criterion.id}
                        className="rounded-xl border border-[#e3e9f1] bg-white p-3"
                      >
                        <div className="flex items-start justify-between gap-3">
                          <p className="text-sm font-bold text-[#30445f]">
                            {index + 1}. {criterion.criterionName}
                          </p>
                          <span className="shrink-0 font-mono text-xs font-black text-[#1C4D8D]">
                            {criterion.maxScore.toFixed(1)}đ
                          </span>
                        </div>
                        {criterion.description && (
                          <p className="mt-1 text-xs leading-relaxed text-[#78879a]">
                            {criterion.description}
                          </p>
                        )}
                      </div>
                    ))}
                  </div>
                </section>

                <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-sm">
                  <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
                    Model Answer
                  </p>
                  <p className="mt-2 text-xs leading-relaxed whitespace-pre-wrap text-[#586a82]">
                    {detail.sampleAnswer || 'Chưa có câu trả lời mẫu.'}
                  </p>
                </section>

                {detail.reviewNotes && (
                  <section className="rounded-2xl border border-[#ecd9ab] bg-[#fff9ec] p-4">
                    <p className="flex items-center gap-2 text-xs font-bold text-[#8a5e17]">
                      <Sparkles className="size-4" />
                      Phản hồi của Trưởng Bộ môn
                    </p>
                    <p className="mt-2 text-xs leading-relaxed whitespace-pre-wrap text-[#755826]">
                      {detail.reviewNotes}
                    </p>
                  </section>
                )}

                {(detail.approvalStatus === 'DRAFT' ||
                  detail.approvalStatus === 'NEEDS_REVISION') && (
                  <section className="rounded-2xl border border-[#4988C4]/45 bg-[#4988C4]/16 p-4 text-xs leading-relaxed text-[#62748c]">
                    Backend hiện chưa cung cấp API cập nhật bản nháp. Màn hình này chỉ hiển thị nội
                    dung đã lưu và không mô phỏng thao tác chỉnh sửa.
                  </section>
                )}
              </div>
            ) : null}
          </aside>
        </>
      )}
    </div>
  )
}

export { QuestionStudioPage as QuestionListPage }
