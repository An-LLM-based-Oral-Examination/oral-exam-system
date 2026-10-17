import { useEffect, useMemo, useState, type FormEvent } from 'react'
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
import { OverlayDrawer } from '@/components/ui/OverlayDrawer'
import { courseService } from '@/services/course.service'
import { questionService } from '@/services/question.service'
import { useAuth } from '@/hooks/useAuth'
import type { UserRole } from '@/types/auth.types'
import type { CourseDto } from '@/types/practice.types'
import type {
  QuestionApprovalStatus,
  QuestionDetail,
  QuestionFilters,
  QuestionSummary,
} from '@/types/rubric.types'
import { cn } from '@/lib/utils'

const FALLBACK_COURSES: CourseDto[] = [
  {
    id: '00000000-0000-0000-0000-000000000008',
    code: 'PRN231',
    name: 'Building Cross-Platform Back-End Applications with .NET',
    credits: 3,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
    isActive: true,
  },
  {
    id: '00000000-0000-0000-0000-000000000009',
    code: 'SWD392',
    name: 'Software Architecture and Design',
    credits: 3,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
    isActive: true,
  },
  {
    id: '00000000-0000-0000-0000-00000000000a',
    code: 'SWR302',
    name: 'Software Requirements',
    credits: 3,
    transcriptBufferSeconds: 60,
    maxFollowUpQuestions: 2,
    hasFollowUp: true,
    examInputMode: 'VoiceWithTranscriptEdit',
    isActive: true,
  },
]

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
  DRAFT: { label: 'Bản nháp', className: 'border-[#cfdbeb] bg-[#f3f6fa] text-[#52647d]' },
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
  const [courses, setCourses] = useState<CourseDto[]>(FALLBACK_COURSES)
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
  const [reviewNotesInput, setReviewNotesInput] = useState('')
  const [targetScopeInput, setTargetScopeInput] = useState<'exam' | 'practice' | 'both'>('both')
  const [isReviewingDecision, setIsReviewingDecision] = useState(false)

  useEffect(() => {
    let isActive = true
    void courseService
      .getCourses()
      .then((loadedCourses) => {
        if (isActive && loadedCourses.length > 0) {
          setCourses(loadedCourses)
        }
      })
      .catch(() => {
        // Giữ danh sách môn học dự phòng nếu kết nối tạm gián đoạn
      })
    return () => {
      isActive = false
    }
  }, [])

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
    setReviewNotesInput('')
    try {
      const loaded = await questionService.getQuestionById(questionId)
      setDetail(loaded)
      setReviewNotesInput(loaded.reviewNotes ?? '')
    } catch (loadError) {
      setDetailError(getErrorMessage(loadError))
    } finally {
      setIsDetailLoading(false)
    }
  }

  const handleReviewDecision = async (decision: 'APPROVED' | 'NEEDS_REVISION' | 'REJECTED') => {
    if (!detail || isReviewingDecision) return
    if (
      (decision === 'NEEDS_REVISION' || decision === 'REJECTED') &&
      !reviewNotesInput.trim()
    ) {
      setDetailError('Vui lòng nhập nhận xét / lý do khi yêu cầu chỉnh sửa hoặc từ chối câu hỏi.')
      return
    }

    setIsReviewingDecision(true)
    setDetailError('')
    setNotice('')
    try {
      const res = await questionService.reviewDecision(detail.id, {
        decision,
        reviewNotes: reviewNotesInput.trim() || undefined,
        targetUsageScope: targetScopeInput,
      })
      const updatedDetail = await questionService.getQuestionById(detail.id)
      setDetail(updatedDetail)
      setNotice(res.message)
      setReloadKey((key) => key + 1)
    } catch (reviewErr) {
      setDetailError(getErrorMessage(reviewErr))
    } finally {
      setIsReviewingDecision(false)
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
    <div className="space-y-4 sm:space-y-5">
      <section className="overflow-hidden rounded-2xl border border-[#E2E8F0] border-t-4 border-t-[#F26F21] bg-white p-5 shadow-sm sm:p-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1 text-xs font-bold tracking-wide text-[#C2410C] uppercase">
              <BookOpenCheck className="size-3.5" aria-hidden="true" />
              Ngân hàng Câu hỏi & Barem Đánh giá
            </div>
            <h1 className="mt-2.5 text-xl font-bold tracking-tight text-[#1E293B] sm:text-2xl">
              Ngân hàng câu hỏi của giảng viên
            </h1>
            <p className="mt-2 max-w-3xl text-xs leading-relaxed text-[#475569] sm:text-sm">
              Biên soạn câu hỏi thủ công hoặc bằng AI từ FLM, kiểm tra Barem Rubric đúng 10.0 điểm
              và gửi Trưởng Bộ môn thẩm định trước khi đưa vào ngân hàng đề chính thức.
            </p>
          </div>
          <Link
            to="/question-bank/create"
            className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-sm transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
          >
            <Plus className="size-4" aria-hidden="true" />
            Tạo câu hỏi mới
          </Link>
        </div>
      </section>

      {(error || notice) && (
        <div
          className={cn(
            'flex items-start gap-2 rounded-2xl border px-4 py-3 text-xs font-semibold sm:text-sm',
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
        className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-sm"
        aria-label="Bộ lọc câu hỏi"
      >
        <div className="grid gap-3 lg:grid-cols-[minmax(200px,1.2fr)_repeat(4,minmax(140px,0.65fr))_auto]">
          <label className="relative block">
            <span className="sr-only">Tìm kiếm câu hỏi</span>
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#64748B]" />
            <input
              type="search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Tìm tiêu đề hoặc nội dung..."
              className="min-h-11 w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] pr-3 pl-9 text-xs text-[#1E293B] transition outline-none placeholder:text-[#94A3B8] focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
            />
          </label>

          <label className="relative block">
            <span className="sr-only">Lọc theo học phần</span>
            <select
              value={filters.courseId ?? ''}
              onChange={(event) => updateFilter('courseId', event.target.value)}
              className="min-h-11 w-full appearance-none rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 text-xs font-semibold text-[#334155] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
            >
              <option value="">Tất cả học phần</option>
              {courses.map((course) => (
                <option key={course.id} value={course.id}>
                  {course.code} — {course.name}
                </option>
              ))}
            </select>
          </label>

          <label className="relative block">
            <span className="sr-only">Lọc theo trạng thái</span>
            <select
              value={filters.approvalStatus ?? ''}
              onChange={(event) => updateFilter('approvalStatus', event.target.value)}
              className="min-h-11 w-full appearance-none rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 text-xs font-semibold text-[#334155] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
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
              className="min-h-11 w-full appearance-none rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 text-xs font-semibold text-[#334155] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
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
              className="min-h-11 w-full appearance-none rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3 text-xs font-semibold text-[#334155] transition outline-none focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
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
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-2xs transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
          >
            <Filter className="size-4" aria-hidden="true" />
            Áp dụng
          </button>
        </div>
      </form>

      {selectedIds.size > 0 && (
        <section
          className="rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] p-4 shadow-sm"
          aria-label="Gửi câu hỏi để thẩm định"
        >
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end">
            <div className="min-w-0 flex-1">
              <p className="flex items-center gap-2 text-sm font-bold text-[#C2410C]">
                <Send className="size-4" aria-hidden="true" />
                Đã chọn {selectedIds.size} bản nháp · {selectedQuestions[0]?.courseCode}
              </p>
              <label className="mt-3 block">
                <span className="text-xs font-bold tracking-wide text-[#9A3412] uppercase">
                  Ghi chú gửi Trưởng Bộ môn
                </span>
                <textarea
                  value={submissionNotes}
                  onChange={(event) => setSubmissionNotes(event.target.value)}
                  maxLength={1000}
                  placeholder="Nêu phạm vi nội dung hoặc điểm cần lưu ý khi thẩm định..."
                  className="mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#FED7AA] bg-white px-3 py-2.5 text-xs text-[#1E293B] transition outline-none placeholder:text-[#94A3B8] focus:border-[#F26F21] focus:ring-2 focus:ring-[#FED7AA] sm:text-sm"
                />
              </label>
            </div>
            <div className="flex shrink-0 flex-col gap-2 sm:flex-row lg:flex-col">
              <button
                type="button"
                onClick={() => setSelectedIds(new Set())}
                className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border border-[#CBD5E1] bg-white px-4 text-xs font-bold text-[#475569] transition hover:bg-[#F8FAFC] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
              >
                <X className="size-4" aria-hidden="true" />
                Bỏ chọn
              </button>
              <button
                type="button"
                onClick={() => void submitForReview()}
                disabled={isSubmitting}
                className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-sm transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-60 sm:text-sm"
              >
                {isSubmitting ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <Send className="size-4" aria-hidden="true" />
                )}
                Gửi Trưởng Bộ môn duyệt
              </button>
            </div>
          </div>
        </section>
      )}

      <section className="overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-sm">
        <div className="flex flex-col gap-3 border-b border-[#E2E8F0] px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
          <div>
            <h2 className="text-base font-bold text-[#1E293B]">Danh sách câu hỏi</h2>
            <p className="mt-1 text-xs text-[#64748B]">
              {isLoading ? 'Đang đồng bộ...' : `${totalCount} câu hỏi phù hợp bộ lọc`}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={toggleAllDrafts}
              disabled={draftQuestions.length === 0}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#FED7AA] bg-[#FFF7ED] px-3 text-xs font-bold text-[#C2410C] transition hover:bg-[#FFEDD5] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-45"
            >
              <Check className="size-3.5" aria-hidden="true" />
              {allSelectableDraftsChecked ? 'Bỏ chọn bản nháp' : 'Chọn bản nháp'}
            </button>
            <button
              type="button"
              onClick={() => setReloadKey((key) => key + 1)}
              className="flex size-9 items-center justify-center rounded-lg border border-[#CBD5E1] text-[#475569] transition hover:border-[#FED7AA] hover:bg-[#FFF7ED] hover:text-[#C2410C] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21]"
              aria-label="Tải lại danh sách"
            >
              <RefreshCw className="size-4" aria-hidden="true" />
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="flex min-h-72 flex-col items-center justify-center px-5 text-center">
            <LoaderCircle className="size-7 animate-spin text-[#F26F21] motion-reduce:animate-none" />
            <p className="mt-3 text-xs font-semibold text-[#475569] sm:text-sm">
              Đang tải ngân hàng câu hỏi...
            </p>
          </div>
        ) : questions.length === 0 ? (
          <div className="flex min-h-72 flex-col items-center justify-center px-5 text-center">
            <span className="flex size-12 items-center justify-center rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]">
              <CircleDashed className="size-5" aria-hidden="true" />
            </span>
            <h3 className="mt-3 text-base font-bold text-[#1E293B]">Chưa có câu hỏi phù hợp</h3>
            <p className="mt-1 max-w-md text-xs leading-relaxed text-[#64748B] sm:text-sm">
              Thay đổi bộ lọc hoặc tạo bản nháp đầu tiên cho môn học bạn phụ trách.
            </p>
            <Link
              to="/question-bank/create"
              className="mt-4 inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] sm:text-sm"
            >
              <Plus className="size-4" />
              Tạo câu hỏi mới
            </Link>
          </div>
        ) : (
          <div className="divide-y divide-[#E2E8F0]">
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
                    'grid gap-3 px-4 py-4 transition sm:px-5 lg:grid-cols-[auto_minmax(0,1fr)_150px_140px_auto] lg:items-center',
                    isSelected ? 'bg-[#FFF7ED]/70' : 'hover:bg-[#F8FAFC]',
                  )}
                >
                  <label className="flex items-center">
                    <span className="sr-only">Chọn câu hỏi {question.title}</span>
                    <input
                      type="checkbox"
                      checked={isSelected}
                      disabled={!canSelect}
                      onChange={() => toggleQuestion(question)}
                      className="size-4 rounded border-[#CBD5E1] text-[#F26F21] accent-[#F26F21] focus:ring-[#F26F21] disabled:cursor-not-allowed disabled:opacity-35"
                    />
                  </label>

                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-xs font-bold text-[#C2410C]">
                        {question.courseCode}
                      </span>
                      <StatusBadge status={question.approvalStatus} />
                      {user?.role !== 'Admin' && (
                        <span
                          className={cn(
                            'rounded-full px-2.5 py-0.5 text-xs font-semibold',
                            isOwned ? 'bg-[#ECFDF5] text-[#047857]' : 'bg-[#F1F5F9] text-[#64748B]',
                          )}
                        >
                          {isOwned ? 'Của bạn' : 'Giảng viên khác'}
                        </span>
                      )}
                    </div>
                    <h3 className="mt-2 line-clamp-2 text-sm leading-snug font-bold text-[#1E293B]">
                      {question.title}
                    </h3>
                    <p className="mt-1 text-xs text-[#64748B]">
                      Tạo ngày {formatDate(question.createdAt)} · Nguồn{' '}
                      {getSourceLabel(question.source)}
                    </p>
                  </div>

                  <div className="flex items-center gap-2 lg:block">
                    <span className="text-xs font-semibold text-[#64748B] lg:block">Bloom</span>
                    <span className="mt-1 inline-flex rounded-lg border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-1 text-xs font-bold text-[#C2410C]">
                      {question.bloomLevel}
                    </span>
                  </div>

                  <div className="flex items-center gap-2 lg:block">
                    <span className="text-xs font-semibold text-[#64748B] lg:block">Barem</span>
                    <span
                      className={cn(
                        'mt-1 block text-sm font-black',
                        question.totalRubricScore === 10 ? 'text-[#047857]' : 'text-[#BE123C]',
                      )}
                    >
                      {question.totalRubricScore.toFixed(1)} / 10
                    </span>
                  </div>

                  <button
                    type="button"
                    onClick={() => void openDetail(question.id)}
                    className="inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 text-xs font-bold text-[#C2410C] transition hover:bg-[#F26F21] hover:text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21]"
                  >
                    <Eye className="size-4" aria-hidden="true" />
                    Chi tiết / Duyệt
                  </button>
                </article>
              )
            })}
          </div>
        )}

        {!isLoading && questions.length > 0 && (
          <div className="flex items-center justify-between border-t border-[#E2E8F0] bg-[#F8FAFC] px-4 py-3 sm:px-5">
            <button
              type="button"
              onClick={() => setPageNumber((page) => Math.max(1, page - 1))}
              disabled={pageNumber <= 1}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#CBD5E1] bg-white px-3 text-xs font-bold text-[#334155] transition hover:bg-[#F1F5F9] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ArrowLeft className="size-4" />
              Trang trước
            </button>
            <span className="text-xs font-semibold text-[#475569]">
              Trang {pageNumber} / {totalPages}
            </span>
            <button
              type="button"
              onClick={() => setPageNumber((page) => Math.min(totalPages, page + 1))}
              disabled={pageNumber >= totalPages}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#CBD5E1] bg-white px-3 text-xs font-bold text-[#334155] transition hover:bg-[#F1F5F9] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-40"
            >
              Trang sau
              <ArrowRight className="size-4" />
            </button>
          </div>
        )}
      </section>

      <OverlayDrawer
        open={isDetailOpen}
        onClose={() => setIsDetailOpen(false)}
        title={detail?.title ?? 'Barem & Lịch sử thẩm định câu hỏi'}
        subtitle={
          detail
            ? `Học phần: ${detail.courseCode} · Bậc Bloom: ${detail.bloomLevel}`
            : 'Xem chi tiết nội dung câu hỏi, đáp án mẫu và Barem Rubric 10.0 điểm'
        }
        badge={detail ? STATUS_META[detail.approvalStatus].label : 'Chi tiết câu hỏi'}
        width="2xl"
      >
        {isDetailLoading ? (
          <div className="flex min-h-72 flex-col items-center justify-center">
            <LoaderCircle className="size-7 animate-spin text-[#F26F21] motion-reduce:animate-none" />
            <p className="mt-3 text-xs font-semibold text-[#475569] sm:text-sm">
              Đang tải chi tiết...
            </p>
          </div>
        ) : detailError ? (
          <div className="rounded-2xl border border-[#efc8cc] bg-[#fff5f6] p-4 text-xs text-[#a43c47] sm:text-sm">
            <p className="flex items-start gap-2 font-semibold">
              <AlertCircle className="mt-0.5 size-4 shrink-0" />
              {detailError}
            </p>
          </div>
        ) : detail ? (
          <div className="space-y-4">
            <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-2xs">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-mono text-xs font-bold text-[#C2410C]">
                  {detail.courseCode}
                </span>
                <StatusBadge status={detail.approvalStatus} />
                <span className="rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-1 text-xs font-bold text-[#C2410C]">
                  {detail.bloomLevel}
                </span>
              </div>
              <h3 className="mt-3 text-base leading-snug font-bold text-[#1E293B] sm:text-lg">
                {detail.title}
              </h3>
              <p className="mt-3 text-sm leading-relaxed whitespace-pre-wrap text-[#334155]">
                {detail.content}
              </p>
            </section>

            <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-2xs">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="text-xs font-bold tracking-wider text-[#64748B] uppercase">
                    Barem Rubric 10.0
                  </p>
                  <h3 className="mt-1 text-base font-bold text-[#1E293B]">
                    {detail.rubric.name}
                  </h3>
                </div>
                <span
                  className={cn(
                    'rounded-xl px-3 py-2 text-sm font-black',
                    detail.rubric.totalMaxScore === 10
                      ? 'bg-[#ECFDF5] text-[#047857]'
                      : 'bg-[#FFF1F2] text-[#BE123C]',
                  )}
                >
                  {detail.rubric.totalMaxScore.toFixed(1)} / 10
                </span>
              </div>
              <div className="mt-4 space-y-2.5">
                {detail.rubric.criteria.map((criterion, index) => (
                  <div
                    key={criterion.id}
                    className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <p className="text-sm font-bold text-[#1E293B]">
                        {index + 1}. {criterion.criterionName}
                      </p>
                      <span className="shrink-0 font-mono text-xs font-black text-[#C2410C]">
                        {criterion.maxScore.toFixed(1)}đ
                      </span>
                    </div>
                    {criterion.description && (
                      <p className="mt-1.5 text-xs leading-relaxed text-[#475569] sm:text-sm">
                        {criterion.description}
                      </p>
                    )}
                  </div>
                ))}
              </div>
            </section>

            <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-2xs">
              <p className="text-xs font-bold tracking-wider text-[#64748B] uppercase">
                Model Answer
              </p>
              <p className="mt-2 text-sm leading-relaxed whitespace-pre-wrap text-[#334155]">
                {detail.sampleAnswer || 'Chưa có câu trả lời mẫu.'}
              </p>
            </section>

            {detail.reviewNotes && (
              <section className="rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] p-4">
                <p className="flex items-center gap-2 text-xs font-bold text-[#C2410C] sm:text-sm">
                  <Sparkles className="size-4" />
                  Phản hồi của Trưởng Bộ môn
                </p>
                <p className="mt-2 text-xs leading-relaxed whitespace-pre-wrap text-[#7C2D12] sm:text-sm">
                  {detail.reviewNotes}
                </p>
              </section>
            )}

            {(user?.role === 'DepartmentHead' || user?.role === 'Admin') && (
              <section className="rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] p-4 shadow-2xs">
                <p className="text-xs font-bold tracking-wider text-[#C2410C] uppercase">
                  Thẩm định & Phê duyệt của Trưởng Bộ môn
                </p>
                <p className="mt-1 text-xs leading-relaxed text-[#7C2D12]">
                  Chọn kho lưu trữ câu hỏi và ghi chú thẩm định trước khi ra quyết định phê duyệt,
                  yêu cầu chỉnh sửa hoặc từ chối.
                </p>

                <label className="mt-3 block">
                  <span className="text-xs font-bold text-[#9A3412] uppercase">
                    Kho lưu trữ đích khi phê duyệt
                  </span>
                  <select
                    value={targetScopeInput}
                    onChange={(event) =>
                      setTargetScopeInput(
                        event.target.value as 'exam' | 'practice' | 'both',
                      )
                    }
                    className="mt-1.5 min-h-10 w-full rounded-xl border border-[#FED7AA] bg-white px-3 text-xs font-semibold text-[#1E293B] outline-none focus:border-[#F26F21]"
                  >
                    <option value="both">
                      Cả 2 kho: Luyện tập/Thi thử (practice_questions) & Thi thật (exam_questions)
                    </option>
                    <option value="exam">Chỉ kho Thi thật (exam_questions)</option>
                    <option value="practice">Chỉ kho Luyện tập/Thi thử (practice_questions)</option>
                  </select>
                </label>

                <label className="mt-3 block">
                  <span className="text-xs font-bold text-[#9A3412] uppercase">
                    Nhận xét / Lý do thẩm định
                  </span>
                  <textarea
                    value={reviewNotesInput}
                    onChange={(event) => setReviewNotesInput(event.target.value)}
                    placeholder="Nhập nhận xét chuyên môn hoặc yêu cầu chỉnh sửa Barem Rubric..."
                    className="mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#FED7AA] bg-white px-3 py-2 text-xs text-[#1E293B] outline-none focus:border-[#F26F21]"
                  />
                </label>

                <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-3">
                  <button
                    type="button"
                    disabled={isReviewingDecision}
                    onClick={() => void handleReviewDecision('APPROVED')}
                    className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl bg-[#047857] px-3 text-xs font-bold text-white shadow-xs transition hover:bg-[#065F46] disabled:opacity-60"
                  >
                    <CheckCircle2 className="size-4" />
                    Phê duyệt (APPROVED)
                  </button>
                  <button
                    type="button"
                    disabled={isReviewingDecision}
                    onClick={() => void handleReviewDecision('NEEDS_REVISION')}
                    className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl bg-[#D97706] px-3 text-xs font-bold text-white shadow-xs transition hover:bg-[#B45309] disabled:opacity-60"
                  >
                    <RefreshCw className="size-4" />
                    Yêu cầu sửa (NEEDS_REVISION)
                  </button>
                  <button
                    type="button"
                    disabled={isReviewingDecision}
                    onClick={() => void handleReviewDecision('REJECTED')}
                    className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl bg-[#BE123C] px-3 text-xs font-bold text-white shadow-xs transition hover:bg-[#9F1239] disabled:opacity-60"
                  >
                    <X className="size-4" />
                    Từ chối (REJECTED)
                  </button>
                </div>
              </section>
            )}

            {(detail.approvalStatus === 'DRAFT' ||
              detail.approvalStatus === 'NEEDS_REVISION') && (
              <section className="rounded-2xl border border-[#E2E8F0] bg-[#F8FAFC] p-4 text-xs leading-relaxed text-[#475569]">
                Màn hình chi tiết hiển thị đầy đủ Barem Rubric 10.0 điểm và Model Answer đã lưu
                trên hệ thống.
              </section>
            )}
          </div>
        ) : null}
      </OverlayDrawer>
    </div>
  )
}

export { QuestionStudioPage as QuestionListPage }
