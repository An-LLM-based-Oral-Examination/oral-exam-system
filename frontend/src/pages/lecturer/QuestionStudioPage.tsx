import { useEffect, useMemo, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  BookOpenCheck,
  Check,
  CheckCircle2,
  CircleDashed,
  Eye,
  Library,
  LoaderCircle,
  Plus,
  RefreshCw,
  Search,
  Send,
  ShieldCheck,
  Sparkles,
  UserRound,
  X,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { QuestionImage } from '@/components/question/QuestionImage'
import { useAuth } from '@/hooks/useAuth'
import { CreatePracticeQuestionDialog } from '@/pages/lecturer/CreateQuestionPage'
import { courseService } from '@/services/course.service'
import { questionService } from '@/services/question.service'
import type { UserRole } from '@/types/auth.types'
import type {
  QuestionApprovalStatus,
  QuestionDetail,
  QuestionFilters,
  QuestionSummary,
} from '@/types/rubric.types'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { lockBodyScroll } from '@/design/bodyScrollLock'
import { BannerFact, LecturerPageBanner } from '@/design/ui'
import { ds, overlay } from '@/design/system'

type WorkspaceTab = 'mine' | 'bank'

const MY_STATUS_TABS: Array<{
  value: QuestionApprovalStatus | ''
  label: string
  countIdleClass: string
  dotClass: string
}> = [
  {
    value: '',
    label: 'Tất cả',
    countIdleClass: 'bg-[#e8eef6] text-[#1C4D8D]',
    dotClass: 'bg-[#1C4D8D]',
  },
  {
    value: 'DRAFT',
    label: 'Bản nháp',
    countIdleClass: 'bg-[#eef1f5] text-[#52647d]',
    dotClass: 'bg-[#7a8aa0]',
  },
  {
    value: 'SUBMITTED_FOR_REVIEW',
    label: 'Chờ duyệt',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'NEEDS_REVISION',
    label: 'Cần chỉnh sửa',
    countIdleClass: 'bg-[#fff0f1] text-[#a93d48]',
    dotClass: 'bg-[#c94a55]',
  },
  {
    value: 'APPROVED',
    label: 'Đã duyệt',
    countIdleClass: 'bg-[#e8f7f1] text-[#197158]',
    dotClass: 'bg-[#1a9a72]',
  },
  {
    value: 'REJECTED',
    label: 'Từ chối',
    countIdleClass: 'bg-[#fceef4] text-[#963b66]',
    dotClass: 'bg-[#b04a78]',
  },
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

const STATUS_META: Record<QuestionApprovalStatus, { label: string; className: string }> = {
  DRAFT: { label: 'Bản nháp', className: 'border-[#82aedb]/55 bg-[#f3f6fa] text-[#52647d]' },
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

function getDifficultyLabel(difficulty: QuestionDetail['difficulty']) {
  if (difficulty === 'easy') return 'Dễ'
  if (difficulty === 'hard') return 'Khó'
  return 'Trung bình'
}

function canManageQuestion(
  question: QuestionSummary,
  role: UserRole | undefined,
  userId: string | undefined,
) {
  const rawRole = String(role ?? '')
  if (
    rawRole === 'Admin' ||
    rawRole === 'admin' ||
    rawRole === 'DepartmentHead' ||
    rawRole === 'department_head'
  ) {
    return true
  }
  return Boolean(userId && question.submittedBy?.toLowerCase() === userId.toLowerCase())
}

async function loadApprovedForCourses(
  courseIds: string[],
  filters: Pick<QuestionFilters, 'search' | 'bloomLevel' | 'difficulty' | 'source'>,
) {
  const pages = await Promise.all(
    courseIds.map(async (courseId) => {
      const query = {
        ...filters,
        courseId,
        approvalStatus: 'APPROVED' as const,
        usageScope: 'practice' as const,
        pageSize: 100,
      }
      const first = await questionService.getQuestions({ ...query, pageNumber: 1 })
      if (first.totalPages <= 1) return first.items
      const rest = await Promise.all(
        Array.from({ length: first.totalPages - 1 }, (_, index) =>
          questionService.getQuestions({ ...query, pageNumber: index + 2 }),
        ),
      )
      return [first.items, ...rest.map((page) => page.items)].flat()
    }),
  )
  return pages.flat()
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
  const location = useLocation()
  const navigate = useNavigate()
  const rawRole = String(user?.role ?? '')
  const canReviewQuestions =
    rawRole === 'DepartmentHead' ||
    rawRole === 'Admin' ||
    rawRole === 'department_head' ||
    rawRole === 'admin'

  const [workspace, setWorkspace] = useState<WorkspaceTab>('mine')
  const [statusTab, setStatusTab] = useState<QuestionApprovalStatus | ''>('')
  const [statusCounts, setStatusCounts] = useState<Record<string, number>>({})
  const [questions, setQuestions] = useState<QuestionSummary[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [pageNumber, setPageNumber] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [filters, setFilters] = useState<QuestionFilters>({
    pageNumber: 1,
    pageSize: 12,
    usageScope: 'practice',
  })
  const [courses, setCourses] = useState<Array<{ id: string; code: string; name: string }>>([])
  const [coursesReady, setCoursesReady] = useState(false)
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [isLoading, setIsLoading] = useState(true)
  const [reloadKey, setReloadKey] = useState(0)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [submissionNotes, setSubmissionNotes] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [selectedQuestion, setSelectedQuestion] = useState<QuestionDetail | null>(null)
  const [viewingQuestionId, setViewingQuestionId] = useState<string | null>(null)
  const [isDetailOpen, setIsDetailOpen] = useState(false)
  const [isDetailLoading, setIsDetailLoading] = useState(false)
  const [detailLoadFailed, setDetailLoadFailed] = useState(false)
  const [reviewNotesError, setReviewNotesError] = useState('')
  const [reviewNotes, setReviewNotes] = useState('')
  const [isReviewing, setIsReviewing] = useState(false)
  const closeDetailButtonRef = useRef<HTMLButtonElement>(null)
  const questionRowRefs = useRef<Map<string, HTMLElement>>(new Map())
  const scrollListToViewRef = useRef(false)

  useEffect(() => {
    const state = location.state as { openCreate?: boolean } | null
    if (!state?.openCreate) return
    return scheduleUi(() => {
      setIsCreateOpen(true)
      navigate(location.pathname, { replace: true, state: {} })
    })
  }, [location.pathname, location.state, navigate])

  useEffect(() => {
    let isActive = true
    void courseService
      .getLecturerCourses()
      .then((items) => {
        if (!isActive) return
        setCourses(items)
      })
      .catch(() => {
        /* giữ dropdown môn trống nếu API lỗi */
      })
      .finally(() => {
        if (isActive) setCoursesReady(true)
      })
    return () => {
      isActive = false
    }
  }, [])

  useEffect(() => {
    let isActive = true
    const loadQuestions = async () => {
      if (workspace === 'bank' && !filters.courseId && !coursesReady) return

      setIsLoading(true)
      try {
        const query: QuestionFilters = {
          ...filters,
          pageNumber,
          search: appliedSearch.trim() || undefined,
          usageScope: 'practice',
        }

        if (workspace === 'mine') {
          if (user?.id && isUuid(user.id)) {
            query.submittedBy = user.id
          }
          query.approvalStatus = statusTab || undefined
        } else {
          query.approvalStatus = 'APPROVED'
          delete query.submittedBy
        }

        const showAssignedBank = workspace === 'bank' && !query.courseId
        const assignedItems = showAssignedBank
          ? await loadApprovedForCourses(
              courses.map((course) => course.id),
              {
                search: query.search,
                bloomLevel: query.bloomLevel,
                difficulty: query.difficulty,
                source: query.source,
              },
            )
          : null
        const result = assignedItems
          ? null
          : await questionService.getQuestions(query)
        if (!isActive) return

        const pageSize = filters.pageSize ?? 12
        const items = assignedItems
          ? assignedItems.slice((pageNumber - 1) * pageSize, pageNumber * pageSize)
          : (result?.items ?? [])

        setQuestions(items)
        setTotalCount(assignedItems ? assignedItems.length : (result?.totalCount ?? 0))
        setTotalPages(
          assignedItems
            ? Math.max(1, Math.ceil(assignedItems.length / pageSize))
            : Math.max(result?.totalPages ?? 1, 1),
        )
        const visibleDraftIds = new Set(
          items
            .filter(
              (question) =>
                (question.approvalStatus === 'DRAFT' ||
                  question.approvalStatus === 'NEEDS_REVISION') &&
                canManageQuestion(question, user?.role, user?.id),
            )
            .map(({ id }) => id),
        )
        setSelectedIds((current) => new Set([...current].filter((id) => visibleDraftIds.has(id))))
      } catch (loadError) {
        if (isActive) toast.error(userMessage(loadError, 'Không tải được danh sách câu hỏi. Vui lòng thử lại.'))
      } finally {
        if (isActive) setIsLoading(false)
      }
    }

    void loadQuestions()
    return () => {
      isActive = false
    }
  }, [
    appliedSearch,
    courses,
    coursesReady,
    filters,
    pageNumber,
    reloadKey,
    statusTab,
    user?.id,
    user?.role,
    workspace,
  ])

  useEffect(() => {
    if (workspace !== 'mine' || !user?.id || !isUuid(user.id)) {
      return scheduleUi(() => setStatusCounts({}))
    }

    let isActive = true
    const loadCounts = async () => {
      try {
        const base = {
          usageScope: 'practice' as const,
          submittedBy: user.id,
          courseId: filters.courseId,
          bloomLevel: filters.bloomLevel,
          search: appliedSearch.trim() || undefined,
          pageNumber: 1,
          pageSize: 1,
        }

        const statuses: Array<QuestionApprovalStatus | ''> = [
          '',
          'DRAFT',
          'SUBMITTED_FOR_REVIEW',
          'NEEDS_REVISION',
          'APPROVED',
          'REJECTED',
        ]

        const results = await Promise.all(
          statuses.map(async (status) => {
            const result = await questionService.getQuestions({
              ...base,
              approvalStatus: status || undefined,
            })
            return [status || 'ALL', result.totalCount] as const
          }),
        )

        if (!isActive) return
        setStatusCounts(Object.fromEntries(results))
      } catch {
        if (isActive) setStatusCounts({})
      }
    }

    void loadCounts()
    return () => {
      isActive = false
    }
  }, [appliedSearch, filters.bloomLevel, filters.courseId, reloadKey, user?.id, workspace])

  const selectedQuestions = useMemo(
    () => questions.filter((question) => selectedIds.has(question.id)),
    [questions, selectedIds],
  )
  const selectedCourseId = selectedQuestions[0]?.courseId
  const draftQuestions = questions.filter(
    (question) =>
      (question.approvalStatus === 'DRAFT' || question.approvalStatus === 'NEEDS_REVISION') &&
      canManageQuestion(question, user?.role, user?.id),
  )
  const pendingReviewQuestions = useMemo(
    () => questions.filter((question) => question.approvalStatus === 'SUBMITTED_FOR_REVIEW'),
    [questions],
  )
  const allSelectableDraftsChecked =
    draftQuestions.length > 0 &&
    draftQuestions
      .filter((question) => !selectedCourseId || question.courseId === selectedCourseId)
      .every((question) => selectedIds.has(question.id))

  const updateFilter = (key: keyof QuestionFilters, value: string) => {
    setPageNumber(1)
    setFilters((current) => ({ ...current, [key]: value || undefined }))
    setSelectedIds(new Set())
  }

  const switchWorkspace = (next: WorkspaceTab) => {
    if (next === workspace) return
    setWorkspace(next)
    setPageNumber(1)
    setSelectedIds(new Set())
    setStatusTab('')
  }

  const switchStatusTab = (next: QuestionApprovalStatus | '') => {
    if (next === statusTab) return
    setStatusTab(next)
    setPageNumber(1)
    setSelectedIds(new Set())
  }

  const courseFilterOptions = useMemo(() => {
    if (courses.length === 0) return []
    return [
      { value: '', label: 'Tất cả môn phân công' },
      ...courses.map((course) => ({
        value: course.id,
        label: `${course.code} · ${course.name}`,
      })),
    ]
  }, [courses])

  const toggleQuestion = (question: QuestionSummary) => {
    if (question.approvalStatus !== 'DRAFT' && question.approvalStatus !== 'NEEDS_REVISION') return
    if (!canManageQuestion(question, user?.role, user?.id)) {
      toast.warning('Bạn chỉ có thể gửi thẩm định các bản nháp do chính mình tạo.')
      return
    }
    if (
      selectedCourseId &&
      selectedCourseId !== question.courseId &&
      !selectedIds.has(question.id)
    ) {
      toast.warning('Chỉ có thể gửi cùng lúc các câu hỏi thuộc một môn học.')
      return
    }

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

  const detailIndex = useMemo(
    () => questions.findIndex((question) => question.id === viewingQuestionId),
    [questions, viewingQuestionId],
  )
  const hasPrevDetail = detailIndex > 0
  const hasNextDetail = detailIndex >= 0 && detailIndex < questions.length - 1

  const closeDetail = () => {
    setIsDetailOpen(false)
    setViewingQuestionId(null)
    setSelectedQuestion(null)
    setDetailLoadFailed(false)
    setReviewNotesError('')
  }

  const scrollQuestionIntoListView = (questionId: string) => {
    const row = questionRowRefs.current.get(questionId)
    if (!row) return
    row.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' })
  }

  const openDetail = async (questionId: string, options?: { scrollList?: boolean }) => {
    if (options?.scrollList) scrollListToViewRef.current = true
    setIsDetailOpen(true)
    setViewingQuestionId(questionId)
    setIsDetailLoading(true)
    setSelectedQuestion(null)
    setDetailLoadFailed(false)
    setReviewNotesError('')
    setReviewNotes('')
    try {
      const loaded = await questionService.getQuestionById(questionId)
      setSelectedQuestion(loaded)
      setReviewNotes(loaded.reviewNotes ?? '')
    } catch (loadError) {
      setDetailLoadFailed(true)
      toast.error(userMessage(loadError, 'Không tải được chi tiết câu hỏi. Vui lòng thử lại.'))
    } finally {
      setIsDetailLoading(false)
    }
  }

  const openAdjacentDetail = (direction: -1 | 1) => {
    if (detailIndex < 0) return
    const next = questions[detailIndex + direction]
    if (next) void openDetail(next.id, { scrollList: true })
  }

  useEffect(() => {
    if (!viewingQuestionId || !scrollListToViewRef.current) return
    scrollListToViewRef.current = false
    // Đợi DOM cập nhật highlight trước khi scroll.
    requestAnimationFrame(() => scrollQuestionIntoListView(viewingQuestionId))
  }, [viewingQuestionId])

  useEffect(() => {
    if (!isDetailOpen) return

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeDetail()
        return
      }
      if (event.key === 'ArrowLeft' && hasPrevDetail) {
        event.preventDefault()
        openAdjacentDetail(-1)
        return
      }
      if (event.key === 'ArrowRight' && hasNextDetail) {
        event.preventDefault()
        openAdjacentDetail(1)
      }
    }

    // Mobile: khóa scroll nền. Desktop: giữ list cuộn được để đổi câu hỏi.
    const isDesktop = window.matchMedia('(min-width: 1024px)').matches
    const unlock = isDesktop ? undefined : lockBodyScroll()
    document.addEventListener('keydown', handleKeyDown)
    closeDetailButtonRef.current?.focus()

    return () => {
      unlock?.()
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [hasNextDetail, hasPrevDetail, isDetailOpen, detailIndex, questions])

  const submitForReview = async () => {
    if (!selectedCourseId || selectedIds.size === 0) return
    if (!isUuid(user?.id ?? '')) {
      toast.error(
        'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.',
      )
      return
    }
    if (selectedQuestions.some((question) => !canManageQuestion(question, user?.role, user?.id))) {
      toast.error(
        'Danh sách chọn có câu hỏi không thuộc quyền quản lý của bạn. Vui lòng tải lại trang.',
      )
      setSelectedIds(new Set())
      return
    }
    setIsSubmitting(true)
    try {
      const result = await questionService.batchSubmitForReview({
        courseId: selectedCourseId,
        questionIds: [...selectedIds],
        submissionNotes: submissionNotes.trim() || undefined,
        lecturerId: user?.id,
      })
      toast.success(userMessage(result.message, 'Đã gửi câu hỏi lên Trưởng Bộ môn thẩm định.'))
      setSelectedIds(new Set())
      setSubmissionNotes('')
      setReloadKey((key) => key + 1)
    } catch (submitError) {
      toast.error(userMessage(submitError, 'Không gửi được câu hỏi thẩm định. Vui lòng thử lại.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleSubmitSingleFromDrawer = async () => {
    if (!selectedQuestion || isSubmitting) return
    setIsSubmitting(true)
    try {
      const result = await questionService.batchSubmitReview(
        [selectedQuestion.id],
        selectedQuestion.courseId,
        submissionNotes.trim() || 'Gửi Trưởng Bộ môn thẩm định từ chi tiết câu hỏi.',
        user?.id,
      )
      const updatedDetail: QuestionDetail = {
        ...selectedQuestion,
        approvalStatus: result.approvalStatus,
      }
      setSelectedQuestion(updatedDetail)
      setQuestions((current) =>
        current.map((item) =>
          item.id === selectedQuestion.id
            ? { ...item, approvalStatus: result.approvalStatus }
            : item,
        ),
      )
      toast.success(userMessage(result.message, 'Đã gửi câu hỏi lên Trưởng Bộ môn thẩm định.'))
      setReloadKey((key) => key + 1)
    } catch (submitError) {
      toast.error(userMessage(submitError, 'Không gửi được câu hỏi thẩm định. Vui lòng thử lại.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleReviewDecision = async (
    decision: 'APPROVED' | 'NEEDS_REVISION' | 'REJECTED',
  ) => {
    if (!selectedQuestion || isReviewing) return
    if (
      (decision === 'NEEDS_REVISION' || decision === 'REJECTED') &&
      reviewNotes.trim().length < 10
    ) {
      setReviewNotesError(
        'Vui lòng nhập nhận xét/góp ý tối thiểu 10 ký tự khi yêu cầu sửa hoặc từ chối câu hỏi.',
      )
      return
    }

    setIsReviewing(true)
    setReviewNotesError('')
    try {
      const updated = await questionService.reviewDecision(selectedQuestion.id, {
        decision,
        reviewNotes: reviewNotes.trim() || undefined,
        reviewerId: user?.id,
      })
      setSelectedQuestion(updated)
      setReviewNotes(updated.reviewNotes ?? '')
      setQuestions((current) =>
        current.map((item) =>
          item.id === updated.id
            ? {
                ...item,
                approvalStatus: updated.approvalStatus,
                approvedBy: updated.approvedBy,
              }
            : item,
        ),
      )
      const decisionLabel =
        decision === 'APPROVED'
          ? 'Đã phê duyệt câu hỏi vào ngân hàng đề.'
          : decision === 'NEEDS_REVISION'
            ? 'Đã gửi yêu cầu chỉnh sửa câu hỏi cho giảng viên.'
            : 'Đã từ chối câu hỏi.'
      toast.success(decisionLabel)
      setReloadKey((key) => key + 1)
    } catch (reviewError) {
      toast.error(userMessage(reviewError, 'Không ghi nhận được quyết định thẩm định. Vui lòng thử lại.'))
    } finally {
      setIsReviewing(false)
    }
  }

  const handleBatchApprovePending = async () => {
    if (!canReviewQuestions || pendingReviewQuestions.length === 0 || isReviewing) return
    setIsReviewing(true)
    try {
      const targetIds = pendingReviewQuestions.map((item) => item.id)
      const result = await questionService.batchApprove(targetIds)
      setQuestions((current) =>
        current.map((item) =>
          targetIds.includes(item.id) ? { ...item, approvalStatus: 'APPROVED' } : item,
        ),
      )
      toast.success(`Đã phê duyệt nhanh ${result.approvedCount} câu hỏi chờ duyệt.`)
      setReloadKey((key) => key + 1)
    } catch (approveError) {
      toast.error(userMessage(approveError, 'Không phê duyệt được câu hỏi. Vui lòng thử lại.'))
    } finally {
      setIsReviewing(false)
    }
  }

  const showSelectionActions = workspace === 'mine'

  return (
    <div lang="vi" className={cn(ds.page, 'pb-8')}>
      <LecturerPageBanner
        icon={BookOpenCheck}
        eyebrow="Câu hỏi luyện tập"
        title="Câu hỏi luyện tập"
        description="Quản lý bản nháp của bạn và xem ngân hàng câu hỏi theo môn được phân công. Chỉ câu hỏi đã duyệt mới dùng cho luyện tập, thi thử và ca thi trên lớp."
        extras={
          <>
            <BannerFact icon={UserRound}>Bản nháp của bạn</BannerFact>
            <BannerFact icon={Library} tone="green">
              Ngân hàng câu hỏi
            </BannerFact>
            <BannerFact icon={ShieldCheck} tone="violet">
              Gửi trưởng bộ môn
            </BannerFact>
          </>
        }
      />

      {showSelectionActions && selectedIds.size > 0 && (
        <section
          className="rounded-2xl border border-[#82aedb]/55 bg-[linear-gradient(135deg,#BDE8F5,#BDE8F5)] p-4 shadow-[0_10px_30px_rgba(36,94,234,0.07)]"
          aria-label="Gửi câu hỏi để thẩm định"
        >
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end">
            <div className="min-w-0 flex-1">
              <p className="flex items-center gap-2 text-sm font-bold text-[#1C4D8D]">
                <Send className="size-4" aria-hidden="true" />
                Đã chọn {selectedIds.size} bản nháp luyện tập · {selectedQuestions[0]?.courseCode}
              </p>
              <label className="mt-3 block">
                <span className="text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                  Ghi chú gửi Trưởng Bộ môn
                </span>
                <textarea
                  value={submissionNotes}
                  onChange={(event) => setSubmissionNotes(event.target.value)}
                  maxLength={1000}
                  placeholder="Nêu phạm vi CLO / nội dung cần lưu ý khi thẩm định kho luyện tập..."
                  className="mt-1.5 min-h-20 w-full resize-y rounded-xl border border-[#4988C4] bg-white/90 px-3 py-2.5 text-sm text-[#0F2854] transition outline-none placeholder:text-[#8aa0b8] focus:border-[#4988C4] focus:ring-2 focus:ring-[#4988C4]/30"
                />
              </label>
            </div>
            <div className="flex shrink-0 flex-col gap-2 sm:flex-row lg:flex-col">
              <button
                type="button"
                onClick={() => setSelectedIds(new Set())}
                className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-4 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
              >
                <X className="size-3.5" aria-hidden="true" />
                Bỏ chọn
              </button>
              <button
                type="button"
                onClick={() => void submitForReview()}
                disabled={isSubmitting}
                className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-xs font-bold text-white shadow-[0_8px_18px_rgba(22,131,106,0.2)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#136f5a] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#16836a] disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transform-none"
              >
                {isSubmitting ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <Send className="size-3.5" aria-hidden="true" />
                )}
                Gửi Trưởng BM duyệt
              </button>
            </div>
          </div>
        </section>
      )}

      <section className={cn(ds.section, 'overflow-hidden')}>
        {/* Header section: tab cấp 1 + tiêu đề/lọc — một khối liền */}
        <div className={cn(ds.sectionHeader, 'space-y-3')}>
          <div
            className="grid w-full grid-cols-2 gap-1 rounded-2xl border border-[#82aedb]/55 bg-white/80 p-1 shadow-[inset_0_1px_0_rgba(255,255,255,0.9)]"
            role="tablist"
            aria-label="Không gian làm việc câu hỏi"
          >
            <button
              type="button"
              role="tab"
              aria-selected={workspace === 'mine'}
              onClick={() => switchWorkspace('mine')}
              className={cn(
                'group inline-flex h-10 items-center justify-center gap-2 rounded-[14px] px-3 py-2 text-[15px] font-bold transition duration-200',
                workspace === 'mine'
                  ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
                  : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
              )}
            >
              <UserRound
                className={cn(
                  'size-4 shrink-0 transition',
                  workspace === 'mine' ? 'text-[#BDE8F5]' : 'text-[#7d8fa3] group-hover:text-[#1C4D8D]',
                )}
                aria-hidden="true"
              />
              <span className="truncate">Câu hỏi của tôi</span>
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={workspace === 'bank'}
              onClick={() => switchWorkspace('bank')}
              className={cn(
                'group inline-flex h-10 items-center justify-center gap-2 rounded-[14px] px-3 py-2 text-[15px] font-bold transition duration-200',
                workspace === 'bank'
                  ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
                  : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
              )}
            >
              <Library
                className={cn(
                  'size-4 shrink-0 transition',
                  workspace === 'bank' ? 'text-[#BDE8F5]' : 'text-[#7d8fa3] group-hover:text-[#1C4D8D]',
                )}
                aria-hidden="true"
              />
              <span className="truncate">Ngân hàng câu hỏi</span>
            </button>
          </div>

          <div className="flex flex-col justify-between gap-3 lg:flex-row lg:items-center">
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  {workspace === 'mine' ? (
                    <UserRound className={ds.sectionIcon} />
                  ) : (
                    <Library className={ds.sectionIcon} />
                  )}
                </span>
                <h2 className={ds.sectionTitle}>
                  {workspace === 'mine' ? 'Câu hỏi của tôi' : 'Ngân hàng câu hỏi'}
                </h2>
                <span className={ds.sectionBadge}>
                  {isLoading ? '…' : `${totalCount} câu hỏi`}
                </span>
              </div>
              <p className={ds.sectionHint}>
                {workspace === 'mine'
                  ? 'Bản nháp do bạn tạo — chọn để gửi Trưởng Bộ môn duyệt'
                  : 'Câu hỏi đã duyệt của các môn bạn được phân công'}
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <FilterDropMenu
                label="Lọc theo môn học"
                value={filters.courseId ?? ''}
                onChange={(value) => updateFilter('courseId', value)}
                options={courseFilterOptions}
                emptyText="Chưa có môn phân công"
                className="w-56 shrink-0"
              />
              <div className="relative shrink-0">
                <Search className="pointer-events-none absolute top-1/2 left-3.5 size-3.5 -translate-y-1/2 text-[#5b6b84]" />
                <input
                  type="search"
                  value={searchInput}
                  onChange={(event) => setSearchInput(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter') {
                      event.preventDefault()
                      setPageNumber(1)
                      setAppliedSearch(searchInput.trim())
                    }
                  }}
                  onBlur={() => {
                    if (searchInput.trim() !== appliedSearch) {
                      setPageNumber(1)
                      setAppliedSearch(searchInput.trim())
                    }
                  }}
                  placeholder="Tìm tiêu đề, nội dung..."
                  className="h-10 w-56 rounded-full border border-[#4988C4] bg-white py-0 pr-4 pl-9 text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:border-[#4988C4] focus:ring-2 focus:ring-[#1C4D8D]/15"
                />
              </div>
              <button
                type="button"
                onClick={() => setReloadKey((key) => key + 1)}
                disabled={isLoading}
                className="inline-flex h-10 shrink-0 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-4 text-sm font-bold text-[#1C4D8D] transition duration-200 hover:-translate-y-0.5 hover:border-[#1C4D8D] hover:bg-[#E7F3FB] hover:shadow-[0_8px_16px_rgba(15,40,84,0.14)] disabled:opacity-60 motion-reduce:transform-none"
              >
                <RefreshCw className={cn('size-3.5', isLoading && 'animate-spin')} aria-hidden="true" />
                {isLoading ? 'Đang đồng bộ...' : 'Làm mới'}
              </button>
              {workspace === 'mine' && (
                <button
                  type="button"
                  onClick={() => setIsCreateOpen(true)}
                  className="inline-flex h-10 shrink-0 items-center gap-1.5 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white shadow-[0_8px_16px_rgba(28,77,141,0.18)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] motion-reduce:transform-none"
                >
                  <Plus className="size-3.5" aria-hidden="true" />
                  Tạo câu hỏi
                </button>
              )}
            </div>
          </div>
        </div>

        {/* Tab trạng thái (folder style) + lọc phụ Bloom */}
        <div className="bg-white">
          {workspace === 'mine' && (
            <div className="px-4 pt-3 sm:px-6">
              <div
                className="flex flex-wrap items-end gap-0 border-b-2 border-[#1C4D8D]"
                role="tablist"
                aria-label="Lọc theo trạng thái thẩm định"
              >
                {MY_STATUS_TABS.map((tab) => {
                  const countKey = tab.value || 'ALL'
                  const count = statusCounts[countKey]
                  const active = statusTab === tab.value
                  return (
                    <button
                      key={countKey}
                      type="button"
                      role="tab"
                      aria-selected={active}
                      onClick={() => switchStatusTab(tab.value)}
                      className={cn(
                        'relative inline-flex items-center gap-1.5 border px-3.5 py-2.5 text-[14px] font-bold whitespace-nowrap transition sm:px-4',
                        active
                          ? '-mb-0.5 rounded-t-xl border-[#1C4D8D] border-b-white bg-[linear-gradient(165deg,#BDE8F5_0%,#EAF6FB_42%,#ffffff_100%)] text-[#1C4D8D]'
                          : 'mb-0 border-transparent text-[#6f7f93] hover:bg-[#F4FBFD] hover:text-[#1C4D8D]',
                      )}
                    >
                      <span
                        className={cn(
                          'size-1.5 rounded-full',
                          active ? 'bg-[#1C4D8D]' : tab.dotClass,
                        )}
                        aria-hidden="true"
                      />
                      <span>{tab.label}</span>
                      {typeof count === 'number' && (
                        <span
                          className={cn(
                            'rounded-full px-1.5 py-0.5 text-[12px] font-bold tabular-nums',
                            active ? 'bg-[#1C4D8D]/12 text-[#1C4D8D]' : tab.countIdleClass,
                          )}
                        >
                          {count}
                        </span>
                      )}
                    </button>
                  )
                })}
              </div>
            </div>
          )}

          <div
            className={cn(
              'flex flex-wrap items-center gap-2 border-b border-[#82aedb]/55 bg-[#F4FBFD] px-4 py-3 sm:px-6',
              workspace === 'mine' ? undefined : 'border-t-0',
            )}
          >
            <FilterDropMenu
              label="Lọc theo bậc Bloom"
              value={filters.bloomLevel ?? ''}
              onChange={(value) => updateFilter('bloomLevel', value)}
              options={BLOOM_OPTIONS}
              className="w-44 shrink-0"
            />
            {showSelectionActions && (
              <div className="ml-auto flex flex-wrap items-center gap-2">
                {canReviewQuestions && pendingReviewQuestions.length > 0 && (
                  <button
                    type="button"
                    onClick={() => void handleBatchApprovePending()}
                    disabled={isReviewing}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#197158] px-3 text-xs font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#135a46] disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none"
                  >
                    {isReviewing ? (
                      <LoaderCircle className="size-3.5 animate-spin motion-reduce:animate-none" />
                    ) : (
                      <ShieldCheck className="size-3.5" aria-hidden="true" />
                    )}
                    Duyệt nhanh ({pendingReviewQuestions.length})
                  </button>
                )}
                <button
                  type="button"
                  onClick={toggleAllDrafts}
                  disabled={draftQuestions.length === 0}
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-white disabled:cursor-not-allowed disabled:opacity-45 motion-reduce:transform-none"
                >
                  <Check className="size-3.5" aria-hidden="true" />
                  {allSelectableDraftsChecked ? 'Bỏ chọn bản nháp' : 'Chọn bản nháp'}
                </button>
              </div>
            )}
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
          <div className="p-4 sm:p-5">
            <EmptyState
              icon={CircleDashed}
              title="Chưa có câu hỏi phù hợp"
              description={
                workspace === 'mine'
                  ? 'Thay đổi bộ lọc hoặc tạo bản nháp đầu tiên cho môn học bạn phụ trách.'
                  : 'Chưa có câu hỏi đã duyệt cho môn được phân công với bộ lọc hiện tại.'
              }
              action={
                workspace === 'mine' ? (
                  <button type="button" onClick={() => setIsCreateOpen(true)} className={ds.primaryButton}>
                    <Plus className="size-4" />
                    Tạo câu hỏi luyện tập
                  </button>
                ) : undefined
              }
            />
          </div>
        ) : (
          <div className="divide-y divide-[#4988C4]/30">
            {questions.map((question) => {
              const isSelected = selectedIds.has(question.id)
              const isViewing = isDetailOpen && viewingQuestionId === question.id
              const isOwned = canManageQuestion(question, user?.role, user?.id)
              const canSelect =
                showSelectionActions &&
                (question.approvalStatus === 'DRAFT' ||
                  question.approvalStatus === 'NEEDS_REVISION') &&
                isOwned &&
                (!selectedCourseId || selectedCourseId === question.courseId || isSelected)

              return (
                <article
                  key={question.id}
                  ref={(node) => {
                    if (node) questionRowRefs.current.set(question.id, node)
                    else questionRowRefs.current.delete(question.id)
                  }}
                  role="button"
                  tabIndex={0}
                  onClick={() => void openDetail(question.id)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault()
                      void openDetail(question.id)
                    }
                  }}
                  className={cn(
                    'relative grid cursor-pointer gap-3 px-4 py-4 transition sm:px-5',
                    showSelectionActions
                      ? 'lg:grid-cols-[auto_minmax(0,1fr)_140px_140px_auto] lg:items-center'
                      : 'lg:grid-cols-[minmax(0,1fr)_140px_140px_auto] lg:items-center',
                    isViewing
                      ? 'bg-[#D7F1F8] shadow-[inset_4px_0_0_0_#1C4D8D] ring-1 ring-inset ring-[#4988C4]/55'
                      : isSelected
                        ? 'bg-[#4988C4]/16'
                        : 'hover:bg-[#F4FBFD]',
                  )}
                >
                  {isViewing && (
                    <span className="pointer-events-none absolute top-3 right-3 rounded-full bg-[#1C4D8D] px-2 py-0.5 text-[10px] font-bold tracking-wide text-white uppercase">
                      Đang xem
                    </span>
                  )}
                  {showSelectionActions && (
                    <label
                      className="flex items-center"
                      onClick={(event) => event.stopPropagation()}
                      onKeyDown={(event) => event.stopPropagation()}
                    >
                      <span className="sr-only">Chọn câu hỏi {question.title}</span>
                      <input
                        type="checkbox"
                        checked={isSelected}
                        disabled={!canSelect}
                        onChange={() => toggleQuestion(question)}
                        className="size-4 rounded border-[#4988C4] text-[#1C4D8D] accent-[#4988C4] focus:ring-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                      />
                    </label>
                  )}

                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                        {question.courseCode}
                      </span>
                      <StatusBadge status={question.approvalStatus} />
                      {workspace === 'mine' && user?.role !== 'Admin' && (
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
                    onClick={(event) => {
                      event.stopPropagation()
                      void openDetail(question.id)
                    }}
                    className={cn(
                      'inline-flex min-h-9 items-center justify-center gap-1.5 rounded-xl border px-3 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]',
                      isViewing
                        ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                        : 'border-[#4988C4] bg-white text-[#49617f] hover:border-[#4988C4] hover:bg-[#4988C4]/18',
                    )}
                  >
                    <Eye className="size-3.5" aria-hidden="true" />
                    {isViewing ? 'Đang xem' : 'Xem chi tiết'}
                  </button>
                </article>
              )
            })}
          </div>
        )}

        {!isLoading && questions.length > 0 && (
          <div className="flex items-center justify-between border-t border-[#82aedb]/55 bg-white px-4 py-3 sm:px-5">
            <button
              type="button"
              onClick={() => setPageNumber((page) => Math.max(1, page - 1))}
              disabled={pageNumber <= 1}
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#536780] transition duration-200 hover:-translate-y-0.5 hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-40 motion-reduce:transform-none"
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
              className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#536780] transition duration-200 hover:-translate-y-0.5 hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-40 motion-reduce:transform-none"
            >
              Trang sau
              <ArrowRight className="size-3.5" />
            </button>
          </div>
        )}
      </section>

      {isDetailOpen && (
        <>
          {/* Mobile: overlay đóng drawer. Desktop: để trống để bấm đổi câu trên list. */}
          <button
            type="button"
            className={cn(overlay.scrimDrawer, overlay.z.drawer)}
            onClick={closeDetail}
            aria-label="Đóng chi tiết câu hỏi"
          />
          <aside
            className={cn(overlay.drawer, overlay.z.modal, 'max-w-xl')}
            aria-label="Chi tiết câu hỏi"
            role="dialog"
            aria-modal="true"
          >
            <div className={cn(overlay.header, 'flex items-start justify-between gap-3 px-4 py-3.5 sm:px-5')}>
              <div className="min-w-0 flex-1">
                {selectedQuestion ? (
                  <>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                        {selectedQuestion.courseCode}
                      </span>
                      <StatusBadge status={selectedQuestion.approvalStatus} />
                      {detailIndex >= 0 && (
                        <span className="text-[11px] font-semibold text-[#7a8aa0]">
                          {detailIndex + 1}/{questions.length}
                        </span>
                      )}
                    </div>
                    <h2 className="mt-1.5 line-clamp-2 text-base font-bold text-[#0F2854] sm:text-lg">
                      {selectedQuestion.title}
                    </h2>
                    <p className="mt-1.5 text-xs font-medium text-[#6f8198]">
                      {[
                        getDifficultyLabel(selectedQuestion.difficulty),
                        selectedQuestion.bloomLevel,
                        getSourceLabel(selectedQuestion.source),
                        formatDate(selectedQuestion.createdAt),
                        selectedQuestion.submittedByName
                          ? selectedQuestion.submittedByName
                          : null,
                      ]
                        .filter(Boolean)
                        .join(' · ')}
                    </p>
                  </>
                ) : (
                  <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">
                    Đang tải nội dung...
                  </h2>
                )}
              </div>
              <div className="flex shrink-0 items-center gap-1.5">
                <button
                  type="button"
                  disabled={!hasPrevDetail || isDetailLoading}
                  onClick={() => openAdjacentDetail(-1)}
                  className="flex size-9 items-center justify-center rounded-xl border border-[#4988C4] bg-white text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                  aria-label="Câu hỏi trước"
                  title="Câu hỏi trước (←)"
                >
                  <ArrowLeft className="size-4" />
                </button>
                <button
                  type="button"
                  disabled={!hasNextDetail || isDetailLoading}
                  onClick={() => openAdjacentDetail(1)}
                  className="flex size-9 items-center justify-center rounded-xl border border-[#4988C4] bg-white text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                  aria-label="Câu hỏi tiếp"
                  title="Câu hỏi tiếp (→)"
                >
                  <ArrowRight className="size-4" />
                </button>
                <button
                  ref={closeDetailButtonRef}
                  type="button"
                  onClick={closeDetail}
                  className="flex size-9 items-center justify-center rounded-xl border border-[#82aedb]/55 bg-white text-[#5f7088] transition hover:bg-[#f2f5f9] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
                  aria-label="Đóng bảng chi tiết"
                >
                  <X className="size-4" />
                </button>
              </div>
            </div>

            <div className="min-h-0 flex-1 overflow-y-auto">
              {isDetailLoading ? (
                <div className="flex min-h-[50vh] flex-col items-center justify-center">
                  <LoaderCircle className="size-7 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
                  <p className="mt-3 text-sm font-semibold text-[#708096]">
                    Đang tải chi tiết...
                  </p>
                </div>
              ) : detailLoadFailed && !selectedQuestion ? (
                <div className="m-5">
                  <EmptyState compact icon={AlertCircle} title="Không thể tải chi tiết câu hỏi" />
                </div>
              ) : selectedQuestion ? (
                <div className="space-y-4 px-4 pt-4 pb-6 sm:px-5 sm:pt-5">
                  {selectedQuestion.reviewNotes && (
                    <section className={cn('rounded-2xl border border-[#ecd9ab] bg-[#fff9ec] p-4', ds.panelShadow)}>
                      <p className="flex items-center gap-2 text-xs font-bold text-[#8a5e17]">
                        <Sparkles className="size-4" />
                        Phản hồi Trưởng Bộ môn
                      </p>
                      <p className="mt-2 text-sm leading-relaxed whitespace-pre-wrap text-[#755826]">
                        {selectedQuestion.reviewNotes}
                      </p>
                    </section>
                  )}

                  <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4', ds.panelShadow)}>
                    <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
                      Nội dung câu hỏi
                    </p>
                    <QuestionImage
                      imageUrl={selectedQuestion.imageUrl}
                      imageAlt={selectedQuestion.imageAlt}
                      className="mt-3"
                    />
                    <p
                      className={cn(
                        'text-[15px] leading-relaxed font-medium whitespace-pre-wrap text-[#0F2854]',
                        selectedQuestion.imageUrl ? 'mt-4' : 'mt-3',
                      )}
                    >
                      {selectedQuestion.content}
                    </p>
                  </section>

                  <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4', ds.panelShadow)}>
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
                          Barem Rubric
                        </p>
                        <h3 className="mt-1 text-sm font-bold text-[#0F2854]">
                          {selectedQuestion.rubric.name}
                        </h3>
                      </div>
                      <span
                        className={cn(
                          'rounded-xl px-3 py-2 text-sm font-black',
                          selectedQuestion.rubric.totalMaxScore === 10
                            ? 'bg-[#eaf9f4] text-[#14765c]'
                            : 'bg-[#fff0f1] text-[#ae3945]',
                        )}
                      >
                        {selectedQuestion.rubric.totalMaxScore.toFixed(1)} / 10
                      </span>
                    </div>
                    <div className="mt-4 space-y-3">
                      {selectedQuestion.rubric.criteria.map((criterion, index) => {
                        const ratio = Math.min(
                          100,
                          Math.max(0, (criterion.maxScore / 10) * 100),
                        )
                        return (
                          <div
                            key={criterion.id}
                            className="rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD]/40 p-3"
                          >
                            <div className="flex items-start justify-between gap-3">
                              <p className="text-sm font-bold text-[#30445f]">
                                {index + 1}. {criterion.criterionName}
                              </p>
                              <span className="shrink-0 font-mono text-xs font-black text-[#1C4D8D]">
                                {criterion.maxScore.toFixed(1)}đ
                              </span>
                            </div>
                            <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[#dce6f2]">
                              <div
                                className="h-full rounded-full bg-[#4988C4]"
                                style={{ width: `${ratio}%` }}
                              />
                            </div>
                            {criterion.description && (
                              <p className="mt-2 text-xs leading-relaxed text-[#78879a]">
                                {criterion.description}
                              </p>
                            )}
                          </div>
                        )
                      })}
                    </div>
                  </section>

                  <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4', ds.panelShadow)}>
                    <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
                      Câu trả lời mẫu
                    </p>
                    <p className="mt-2 text-sm leading-relaxed whitespace-pre-wrap text-[#586a82]">
                      {selectedQuestion.sampleAnswer || 'Chưa có câu trả lời mẫu.'}
                    </p>
                  </section>

                  {canReviewQuestions &&
                    (selectedQuestion.approvalStatus === 'SUBMITTED_FOR_REVIEW' ||
                      selectedQuestion.approvalStatus === 'DRAFT' ||
                      selectedQuestion.approvalStatus === 'NEEDS_REVISION') && (
                      <section className={cn('space-y-2 rounded-2xl border border-[#82aedb]/55 bg-[#F4FBFD] p-4', ds.panelShadow)}>
                        <label className="block">
                          <span
                            className={cn(
                              'flex items-center gap-2 text-xs font-bold',
                              reviewNotesError ? 'text-[#c2415b]' : 'text-[#0F2854]',
                            )}
                          >
                            <ShieldCheck className={cn('size-3.5', reviewNotesError ? 'text-[#c2415b]' : 'text-[#1C4D8D]')} />
                            Nhận xét khi yêu cầu sửa / từ chối
                          </span>
                          <textarea
                            value={reviewNotes}
                            onChange={(event) => {
                              setReviewNotes(event.target.value)
                              setReviewNotesError('')
                            }}
                            aria-invalid={reviewNotesError ? true : undefined}
                            placeholder="Tối thiểu 10 ký tự khi yêu cầu sửa hoặc từ chối..."
                            className={cn(
                              'mt-2 min-h-20 w-full resize-y rounded-xl border border-[#4988C4] bg-white p-3 text-sm text-[#0F2854] outline-none focus:border-[#1C4D8D]',
                              reviewNotesError && fieldInvalidClass,
                            )}
                          />
                          <FieldError message={reviewNotesError} />
                        </label>
                      </section>
                    )}
                </div>
              ) : null}
            </div>

            {selectedQuestion &&
              !isDetailLoading &&
              (selectedQuestion.approvalStatus === 'DRAFT' ||
                selectedQuestion.approvalStatus === 'NEEDS_REVISION' ||
                (canReviewQuestions &&
                  selectedQuestion.approvalStatus === 'SUBMITTED_FOR_REVIEW')) && (
                <div className="shrink-0 space-y-2 border-t border-[#82aedb]/55 bg-white px-4 py-3 sm:px-5">
                  {(selectedQuestion.approvalStatus === 'DRAFT' ||
                    selectedQuestion.approvalStatus === 'NEEDS_REVISION') && (
                    <button
                      type="button"
                      disabled={isSubmitting}
                      onClick={() => void handleSubmitSingleFromDrawer()}
                      className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-sm font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transform-none"
                    >
                      {isSubmitting ? (
                        <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                      ) : (
                        <Send className="size-3.5" />
                      )}
                      Gửi Trưởng BM duyệt
                    </button>
                  )}

                  {canReviewQuestions &&
                    (selectedQuestion.approvalStatus === 'SUBMITTED_FOR_REVIEW' ||
                      selectedQuestion.approvalStatus === 'DRAFT' ||
                      selectedQuestion.approvalStatus === 'NEEDS_REVISION') && (
                      <div className="grid grid-cols-3 gap-2">
                        <button
                          type="button"
                          disabled={isReviewing}
                          onClick={() => void handleReviewDecision('APPROVED')}
                          className="inline-flex h-10 items-center justify-center gap-1 rounded-xl bg-[#197158] px-2 text-xs font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#135a46] disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none"
                        >
                          <CheckCircle2 className="size-3.5 shrink-0" />
                          Phê duyệt
                        </button>
                        <button
                          type="button"
                          disabled={isReviewing}
                          onClick={() => void handleReviewDecision('NEEDS_REVISION')}
                          className="inline-flex h-10 items-center justify-center gap-1 rounded-xl bg-[#b86b14] px-2 text-xs font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#96550d] disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none"
                        >
                          <RefreshCw className="size-3.5 shrink-0" />
                          Yêu cầu sửa
                        </button>
                        <button
                          type="button"
                          disabled={isReviewing}
                          onClick={() => void handleReviewDecision('REJECTED')}
                          className="inline-flex h-10 items-center justify-center gap-1 rounded-xl bg-[#b33d52] px-2 text-xs font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#932c3f] disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none"
                        >
                          <X className="size-3.5 shrink-0" />
                          Từ chối
                        </button>
                      </div>
                    )}
                </div>
              )}
          </aside>
        </>
      )}

      <CreatePracticeQuestionDialog
        open={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        onCreated={() => {
          setWorkspace('mine')
          setStatusTab('DRAFT')
          toast.success('Đã lưu bản nháp luyện tập. Chọn câu hỏi rồi gửi Trưởng Bộ môn duyệt.')
          setReloadKey((key) => key + 1)
        }}
      />
    </div>
  )
}

export { QuestionStudioPage as QuestionListPage }
