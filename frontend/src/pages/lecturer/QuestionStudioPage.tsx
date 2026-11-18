import { StatusTabs } from '@/components/common/StatusTabs'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { TextareaField } from '@/components/common/TextareaField'
import { QuestionRowMetrics } from '@/components/question/QuestionRowMetrics'
import { QuestionDrawer } from '@/components/question/QuestionDrawer'
import { DividedList } from '@/components/common/DividedList'
import { SearchField } from '@/components/common/SearchField'
import { QuestionManagementTabs } from '@/pages/department-head/QuestionManagementTabs'
import { useSearchInput } from '@/hooks/useSearchInput'
import { ListPagination } from '@/components/common/ListPagination'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  AlertCircle,
  Check,
  CheckCircle2,
  CircleDashed,
  Eye,
  Library,
  LoaderCircle,
  PencilLine,
  Plus,
  Power,
  RefreshCw,
  Send,
  ShieldCheck,
  Sparkles,
  Trash2,
  UserRound,
  X,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { EditQuestionDraftDialog } from '@/components/question/EditQuestionDraftDialog'
import { QuestionDetailContent } from '@/components/question/QuestionDetailContent'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { useAuth } from '@/hooks/useAuth'
import { CreatePracticeQuestionDialog } from '@/pages/lecturer/CreateQuestionPage'
import { ROUTE_PATHS } from '@/routes/routePaths'
import { courseService } from '@/services/course.service'
import { courseCloService } from '@/services/courseClo.service'
import { questionService } from '@/services/question.service'
import type {
  QuestionApprovalStatus,
  QuestionDetail,
  QuestionFilters,
  QuestionSummary,
} from '@/types/rubric.types'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'

type WorkspaceTab = 'review' | 'mine' | 'bank'

function isSelfSubmitted(
  submittedBy: string | null | undefined,
  userId: string | undefined,
) {
  return Boolean(userId && submittedBy && submittedBy.toLowerCase() === userId.toLowerCase())
}

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
  { value: '', label: 'Tất cả Bloom' },
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

function formatDate(value?: string | null) {
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
  return source === 'flm_api' ? 'AI' : 'Thủ công'
}

function getDifficultyLabel(difficulty: QuestionDetail['difficulty']) {
  if (difficulty === 'easy') return 'Dễ'
  if (difficulty === 'hard') return 'Khó'
  return 'Trung bình'
}

function canManageQuestion(
  question: Pick<QuestionSummary, 'submittedBy'>,
  userId: string | undefined,
) {
  return isSelfSubmitted(question.submittedBy, userId)
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
  const isDepartmentWorkspace = location.pathname.startsWith('/department-head/')
  const navigate = useNavigate()
  const rawRole = String(user?.role ?? '')
  const canReviewQuestions =
    rawRole === 'DepartmentHead' ||
    rawRole === 'Admin' ||
    rawRole === 'department_head' ||
    rawRole === 'admin'

  const [workspace, setWorkspace] = useState<WorkspaceTab>(() =>
    location.pathname === ROUTE_PATHS.departmentHead.questionBank
      ? 'bank'
      : canReviewQuestions
        ? 'review'
        : 'mine',
  )
  const userSwitchedWorkspaceRef = useRef(false)
  const [reviewQueueCount, setReviewQueueCount] = useState<number | null>(null)
  const [selectedReviewIds, setSelectedReviewIds] = useState<Set<string>>(new Set())
  const [statusTab, setStatusTab] = useState<QuestionApprovalStatus | ''>('')
  const [statusCounts, setStatusCounts] = useState<Record<string, number>>({})
  const [questions, setQuestions] = useState<QuestionSummary[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [pageNumber, setPageNumber] = useState(1)
  const searchInput = useSearchInput(() => setPageNumber(1))
  const appliedSearch = searchInput.query
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
  const [submissionOpen, setSubmissionOpen] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [selectedQuestion, setSelectedQuestion] = useState<QuestionDetail | null>(null)
  const [viewingQuestionId, setViewingQuestionId] = useState<string | null>(null)
  const [isDetailOpen, setIsDetailOpen] = useState(false)
  const [isDetailLoading, setIsDetailLoading] = useState(false)
  const [detailLoadFailed, setDetailLoadFailed] = useState(false)
  const [reviewNotesError, setReviewNotesError] = useState('')
  const [reviewNotes, setReviewNotes] = useState('')
  const [isReviewing, setIsReviewing] = useState(false)
  const [isEditOpen, setIsEditOpen] = useState(false)
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false)
  const [isMutatingQuestion, setIsMutatingQuestion] = useState(false)
  const closeDetailButtonRef = useRef<HTMLButtonElement>(null)
  const questionRowRefs = useRef<Map<string, HTMLElement>>(new Map())
  const scrollListToViewRef = useRef(false)

  useEffect(() => {
    if (!canReviewQuestions || userSwitchedWorkspaceRef.current) return
    if (location.pathname !== ROUTE_PATHS.departmentHead.questionReview && workspace !== 'mine') {
      return
    }
    return scheduleUi(() => {
      setWorkspace('review')
    })
  }, [canReviewQuestions, location.pathname, workspace])

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
    const fetchCourses =
      location.pathname === ROUTE_PATHS.departmentHead.questionBank
        ? courseCloService
            .getCurrentSemesterManagedCourses()
            .catch(() => courseService.getLecturerCourses())
        : courseService.getLecturerCourses()
    void fetchCourses
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
  }, [location.pathname])

  const needsCoursesForBank = workspace === 'bank' && !filters.courseId
  const bankCourses = needsCoursesForBank && coursesReady ? courses : null

  useEffect(() => {
    let isActive = true
    const loadQuestions = async () => {
      if (needsCoursesForBank && !bankCourses) return

      setIsLoading(true)
      try {
        const query: QuestionFilters = {
          ...filters,
          pageNumber,
          search: appliedSearch.trim() || undefined,
          usageScope: 'practice',
        }

        if (workspace === 'review') {
          query.approvalStatus = 'SUBMITTED_FOR_REVIEW'
          delete query.submittedBy
        } else if (workspace === 'mine') {
          if (user?.id && isUuid(user.id)) {
            query.submittedBy = user.id
          }
          query.approvalStatus = statusTab || undefined
        } else {
          query.approvalStatus = 'APPROVED'
          delete query.submittedBy
        }

        const assignedItems = bankCourses
          ? await loadApprovedForCourses(
              bankCourses.map((course) => course.id),
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
        if (workspace === 'review') {
          setReviewQueueCount(result?.totalCount ?? items.length)
        }
        const visibleDraftIds = new Set(
          items
            .filter(
              (question) =>
                (question.approvalStatus === 'DRAFT' ||
                  question.approvalStatus === 'NEEDS_REVISION') &&
                canManageQuestion(question, user?.id),
            )
            .map(({ id }) => id),
        )
        setSelectedIds((current) => new Set([...current].filter((id) => visibleDraftIds.has(id))))
        const visibleReviewIds = new Set(
          items
            .filter(
              (question) =>
                question.approvalStatus === 'SUBMITTED_FOR_REVIEW' &&
                !isSelfSubmitted(question.submittedBy, user?.id),
            )
            .map(({ id }) => id),
        )
        setSelectedReviewIds((current) => new Set([...current].filter((id) => visibleReviewIds.has(id))))
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
    bankCourses,
    filters,
    needsCoursesForBank,
    pageNumber,
    reloadKey,
    statusTab,
    user?.id,
    user?.role,
    workspace,
  ])

  useEffect(() => {
    if (!canReviewQuestions) {
      return scheduleUi(() => setReviewQueueCount(null))
    }
    if (workspace === 'review') return

    let isActive = true
    void questionService
      .getQuestions({
        usageScope: 'practice',
        approvalStatus: 'SUBMITTED_FOR_REVIEW',
        courseId: filters.courseId,
        bloomLevel: filters.bloomLevel,
        search: appliedSearch.trim() || undefined,
        pageNumber: 1,
        pageSize: 1,
      })
      .then((result) => {
        if (isActive) setReviewQueueCount(result.totalCount)
      })
      .catch(() => {
        if (isActive) setReviewQueueCount(null)
      })

    return () => {
      isActive = false
    }
  }, [appliedSearch, canReviewQuestions, filters.bloomLevel, filters.courseId, reloadKey, workspace])

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
      canManageQuestion(question, user?.id),
  )
  const pendingReviewQuestions = useMemo(
    () => questions.filter((question) => question.approvalStatus === 'SUBMITTED_FOR_REVIEW'),
    [questions],
  )
  const approvablePendingQuestions = useMemo(
    () =>
      pendingReviewQuestions.filter(
        (question) => !isSelfSubmitted(question.submittedBy, user?.id),
      ),
    [pendingReviewQuestions, user?.id],
  )
  const selfSubmittedPendingCount =
    pendingReviewQuestions.length - approvablePendingQuestions.length
  const allApprovableChecked =
    approvablePendingQuestions.length > 0 &&
    approvablePendingQuestions.every((question) => selectedReviewIds.has(question.id))
  const allSelectableDraftsChecked =
    draftQuestions.length > 0 &&
    draftQuestions
      .filter((question) => !selectedCourseId || question.courseId === selectedCourseId)
      .every((question) => selectedIds.has(question.id))

  const updateFilter = (key: keyof QuestionFilters, value: string) => {
    setPageNumber(1)
    setFilters((current) => ({ ...current, [key]: value || undefined }))
    setSelectedIds(new Set())
    setSelectedReviewIds(new Set())
  }

  const switchWorkspace = (next: WorkspaceTab) => {
    if (next === workspace) return
    userSwitchedWorkspaceRef.current = true
    setWorkspace(next)
    setPageNumber(1)
    setSelectedIds(new Set())
    setSelectedReviewIds(new Set())
    setStatusTab('')
  }

  const switchStatusTab = (next: QuestionApprovalStatus | '') => {
    if (next === statusTab) return
    setStatusTab(next)
    setPageNumber(1)
    setSelectedIds(new Set())
    setSelectedReviewIds(new Set())
  }

  const courseFilterOptions = useMemo(() => {
    if (courses.length === 0) return []
    return [
      { value: '', label: 'Tất cả môn' },
      ...courses.map((course) => ({
        value: course.id,
        label: `${course.code} · ${course.name}`,
      })),
    ]
  }, [courses])

  const toggleQuestion = (question: QuestionSummary) => {
    if (question.approvalStatus !== 'DRAFT' && question.approvalStatus !== 'NEEDS_REVISION') return
    if (!canManageQuestion(question, user?.id)) {
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
    const courseId = selectedCourseId ?? draftQuestions[0]?.courseId
    const eligible = draftQuestions.filter(
      (question) => question.courseId === courseId,
    )
    if (!selectedCourseId && eligible.length < draftQuestions.length) {
      toast.info('Chỉ có thể gửi cùng lúc câu hỏi thuộc một môn học. Hãy lọc theo môn để chọn bản nháp của môn khác.')
    }
    setSelectedIds((current) => {
      const next = new Set(current)
      if (allSelectableDraftsChecked) eligible.forEach(({ id }) => next.delete(id))
      else eligible.forEach(({ id }) => next.add(id))
      return next
    })
  }

  const toggleReviewQuestion = (question: QuestionSummary) => {
    if (!canReviewQuestions || question.approvalStatus !== 'SUBMITTED_FOR_REVIEW') return
    if (isSelfSubmitted(question.submittedBy, user?.id)) {
      toast.warning('Bạn là người gửi câu hỏi này nên không thể tự phê duyệt.')
      return
    }
    setSelectedReviewIds((current) => {
      const next = new Set(current)
      if (next.has(question.id)) next.delete(question.id)
      else next.add(question.id)
      return next
    })
  }

  const toggleAllReviewQuestions = () => {
    if (!canReviewQuestions || approvablePendingQuestions.length === 0) return
    setSelectedReviewIds((current) => {
      const next = new Set(current)
      if (allApprovableChecked) {
        approvablePendingQuestions.forEach(({ id }) => next.delete(id))
      } else {
        approvablePendingQuestions.forEach(({ id }) => next.add(id))
      }
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
    setIsEditOpen(false)
    setIsDeleteConfirmOpen(false)
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
    setIsEditOpen(false)
    setIsDeleteConfirmOpen(false)
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

    document.addEventListener('keydown', handleKeyDown)
    closeDetailButtonRef.current?.focus()

    return () => {
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [hasNextDetail, hasPrevDetail, isDetailOpen, detailIndex, questions])

  const submitForReview = async () => {
    if (!selectedCourseId || selectedQuestions.length === 0 || isSubmitting) return
    if (!isUuid(user?.id ?? '')) {
      toast.error(
        'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.',
      )
      return
    }
    if (selectedQuestions.some((question) => !canManageQuestion(question, user?.id))) {
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
        questionIds: selectedQuestions.map((question) => question.id),
        submissionNotes: submissionNotes.trim() || undefined,
        lecturerId: user?.id,
      })
      toast.success(userMessage(result.message, 'Đã gửi câu hỏi lên Trưởng Bộ môn thẩm định.'))
      setSelectedIds(new Set())
      setSubmissionNotes('')
      setSubmissionOpen(false)
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
        'Gửi Trưởng Bộ môn thẩm định từ chi tiết câu hỏi.',
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
    if (!canReviewQuestions || approvablePendingQuestions.length === 0 || isReviewing) return
    const approvableSet = new Set(approvablePendingQuestions.map((item) => item.id))
    const selectedEligibleIds = [...selectedReviewIds].filter((id) => approvableSet.has(id))
    const targetIds =
      selectedEligibleIds.length > 0
        ? selectedEligibleIds
        : approvablePendingQuestions.map((item) => item.id)

    if (targetIds.length === 0) return

    setIsReviewing(true)
    try {
      const result = await questionService.batchApprovePendingQuestions(targetIds, {
        reviewerId: user?.id,
      })

      if (result.approvedIds.length > 0) {
        const approvedSet = new Set(result.approvedIds)
        setQuestions((current) =>
          current.map((item) =>
            approvedSet.has(item.id) ? { ...item, approvalStatus: 'APPROVED' } : item,
          ),
        )
        setSelectedReviewIds((current) => {
          const next = new Set(current)
          approvedSet.forEach((id) => next.delete(id))
          return next
        })
      }

      if (result.failed.length === 0) {
        toast.success(`Đã phê duyệt nhanh ${result.approvedCount} câu hỏi chờ duyệt.`)
      } else if (result.approvedCount > 0) {
        toast.warning(
          `Đã duyệt ${result.approvedCount} câu hỏi, ${result.failed.length} câu lỗi: ${result.failed[0]?.message ?? ''}`,
        )
      } else {
        toast.error(result.failed[0]?.message || 'Không phê duyệt được câu hỏi. Vui lòng thử lại.')
      }

      setReloadKey((key) => key + 1)
    } catch (approveError) {
      toast.error(userMessage(approveError, 'Không phê duyệt được câu hỏi. Vui lòng thử lại.'))
    } finally {
      setIsReviewing(false)
    }
  }

  const handleDeleteDraft = async () => {
    if (!selectedQuestion || isMutatingQuestion) return
    const targetId = selectedQuestion.id
    setIsMutatingQuestion(true)
    try {
      await questionService.deleteDraft(targetId)
      setSelectedIds((current) => {
        const next = new Set(current)
        next.delete(targetId)
        return next
      })
      setIsDeleteConfirmOpen(false)
      closeDetail()
      toast.success('Đã thu hồi bản nháp câu hỏi.')
      setReloadKey((key) => key + 1)
    } catch (deleteError) {
      toast.error(userMessage(deleteError, 'Không thu hồi được bản nháp. Vui lòng thử lại.'))
    } finally {
      setIsMutatingQuestion(false)
    }
  }

  const handleToggleQuestionActive = async () => {
    if (!selectedQuestion || isMutatingQuestion || !canReviewQuestions) return
    const nextActive = !selectedQuestion.isActive
    setIsMutatingQuestion(true)
    try {
      await questionService.setQuestionActive(selectedQuestion.id, nextActive)
      setSelectedQuestion((current) =>
        current && current.id === selectedQuestion.id
          ? { ...current, isActive: nextActive }
          : current,
      )
      toast.success(
        nextActive
          ? 'Đã kích hoạt lại câu hỏi trong ngân hàng đề.'
          : 'Đã tạm ngưng sử dụng câu hỏi trong ngân hàng đề.',
      )
      setReloadKey((key) => key + 1)
    } catch (toggleError) {
      toast.error(
        userMessage(toggleError, 'Không cập nhật được trạng thái hoạt động của câu hỏi.'),
      )
    } finally {
      setIsMutatingQuestion(false)
    }
  }

  const handleDraftUpdated = async (questionId: string) => {
    setIsEditOpen(false)
    setReloadKey((key) => key + 1)
    try {
      const refreshed = await questionService.getQuestionById(questionId)
      setSelectedQuestion(refreshed)
    } catch (refreshError) {
      toast.error(userMessage(refreshError, 'Đã lưu nhưng chưa tải lại được chi tiết câu hỏi.'))
    }
  }

  const showDraftSelectionActions = workspace === 'mine'
  const showReviewActions = workspace === 'review' && canReviewQuestions
  const showRowCheckbox = showDraftSelectionActions || showReviewActions
  const showActionToolbar =
    (showDraftSelectionActions &&
      (statusTab === '' || statusTab === 'DRAFT' || statusTab === 'NEEDS_REVISION')) ||
    showReviewActions

  return (
    <div lang="vi" className={cn(ds.page)}>
      <section className={cn(ds.section, 'overflow-hidden')}>
        {/* Header section: tab cấp 1 + tiêu đề/lọc — một khối liền */}
        <div className={cn(ds.sectionHeader, 'space-y-3')}>
          {location.pathname === ROUTE_PATHS.departmentHead.questionBank && <QuestionManagementTabs />}
          <div
            className={cn(
              'grid w-full gap-1 rounded-2xl border border-[#82aedb]/55 bg-white/80 p-1 shadow-[inset_0_1px_0_rgba(255,255,255,0.9)]',
              canReviewQuestions ? 'grid-cols-3' : 'grid-cols-2',
            )}
            role="tablist"
            aria-label="Không gian làm việc câu hỏi"
          >
            {canReviewQuestions && (
              <button
                type="button"
                role="tab"
                aria-selected={workspace === 'review'}
                onClick={() => switchWorkspace('review')}
                className={cn(
                  'group inline-flex h-10 items-center justify-center gap-2 rounded-[14px] px-3 py-2 text-[15px] font-bold transition duration-200',
                  workspace === 'review'
                    ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
                    : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
                )}
              >
                <ShieldCheck
                  className={cn(
                    'size-4 shrink-0 transition',
                    workspace === 'review' ? 'text-[#BDE8F5]' : 'text-[#7d8fa3] group-hover:text-[#1C4D8D]',
                  )}
                  aria-hidden="true"
                />
                <span className="truncate">Chờ phê duyệt</span>
                {typeof reviewQueueCount === 'number' && (
                  <span
                    className={cn(
                      'rounded-full px-1.5 py-0.5 text-[12px] font-bold tabular-nums',
                      workspace === 'review'
                        ? 'bg-white/20 text-white'
                        : 'bg-[#fff4df] text-[#8d6118]',
                    )}
                  >
                    {reviewQueueCount}
                  </span>
                )}
              </button>
            )}
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
            <div className={cn(ds.sectionHeading, 'shrink-0')}>
              <div className={cn(ds.sectionTitleRow, 'flex-nowrap')}>
                <span className={ds.sectionIconWrap}>
                  {workspace === 'review' ? (
                    <ShieldCheck className={ds.sectionIcon} />
                  ) : workspace === 'mine' ? (
                    <UserRound className={ds.sectionIcon} />
                  ) : (
                    <Library className={ds.sectionIcon} />
                  )}
                </span>
                <h2 className={ds.sectionTitle}>
                  {workspace === 'review'
                    ? 'Câu hỏi chờ phê duyệt'
                    : workspace === 'mine'
                      ? 'Câu hỏi của tôi'
                      : 'Ngân hàng câu hỏi'}
                </h2>
                <span className={cn(ds.sectionBadge, 'shrink-0 whitespace-nowrap')}>
                  {isLoading ? '…' : `${totalCount} câu hỏi`}
                </span>
              </div>
              <p className={ds.sectionHint}>
                {workspace === 'review'
                  ? 'Các câu hỏi giảng viên đã gửi thẩm định thuộc môn bạn phụ trách'
                  : workspace === 'mine'
                    ? 'Bản nháp do bạn tạo — chọn để gửi Trưởng Bộ môn duyệt'
                    : isDepartmentWorkspace
                      ? 'Câu hỏi bộ môn trong kỳ hiện tại'
                      : 'Câu hỏi đã duyệt của môn được phân công'}
              </p>
            </div>

            <div className="flex min-w-0 flex-nowrap items-center gap-2 overflow-x-auto py-1 lg:ml-auto">
              <FilterDropMenu
                label="Lọc theo môn học"
                value={filters.courseId ?? ''}
                onChange={(value) => updateFilter('courseId', value)}
                options={courseFilterOptions}
                menuMinWidth={ds.filterMenuMinWidth}
                wrapOptions
                emptyText={isDepartmentWorkspace ? 'Chưa có môn trong kỳ hiện tại' : 'Chưa có môn phân công'}
                className={ds.filterCourse}
              />
              <FilterDropMenu
                label="Lọc theo bậc Bloom"
                value={filters.bloomLevel ?? ''}
                onChange={(value) => updateFilter('bloomLevel', value)}
                options={BLOOM_OPTIONS}
                className={ds.filterBloom}
                menuMinWidth={ds.filterMenuMinWidth}
                wrapOptions
              />
              <div className={cn('relative', ds.searchControl)}>
                <SearchField
                  type="search"
                  value={searchInput.draft}
                  onChange={(event) => searchInput.change(event.target.value)}
                  onKeyDown={searchInput.onKeyDown}
                  placeholder="Tìm tiêu đề, nội dung..."
                  className="h-10 w-full rounded-full border border-[#4988C4] bg-white py-0 pr-4 pl-9 text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:border-[#4988C4] focus:ring-2 focus:ring-[#1C4D8D]/15"
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

        {/* Tab trạng thái (folder style) và thao tác chọn câu hỏi */}
        <div className="bg-white">
          {workspace === 'mine' && (
            <div className="px-4 pt-3 sm:px-6">
              <StatusTabs label="Lọc theo trạng thái thẩm định" value={statusTab} options={MY_STATUS_TABS.map(tab => ({ ...tab, count: statusCounts[tab.value || 'ALL'] }))} onChange={switchStatusTab} />
            </div>
          )}

          {showActionToolbar && (
            <div
              className={cn(
                'flex flex-wrap items-center gap-2 border-b border-[#82aedb]/55 bg-[#F4FBFD] px-4 py-3 sm:px-6',
                workspace === 'mine' ? undefined : 'border-t-0',
              )}
            >
              {showDraftSelectionActions && (
                <div className="flex flex-wrap items-center gap-2">
                  <button
                    type="button"
                    onClick={toggleAllDrafts}
                    disabled={draftQuestions.length === 0}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-white disabled:cursor-not-allowed disabled:opacity-45 motion-reduce:transform-none"
                  >
                    {allSelectableDraftsChecked ? (
                      <X className="size-3.5" aria-hidden="true" />
                    ) : (
                      <Check className="size-3.5" aria-hidden="true" />
                    )}
                    {allSelectableDraftsChecked ? 'Bỏ chọn' : 'Chọn tất cả'}
                  </button>
                  {selectedQuestions.length > 0 && (
                    <button
                      type="button"
                      className={cn(ds.primaryButton, 'min-h-9 whitespace-nowrap')}
                      onClick={() => {
                        setSubmissionNotes('')
                        setSubmissionOpen(true)
                      }}
                    >
                      <Send className="size-3.5" aria-hidden="true" />
                      Gửi Trưởng Bộ môn duyệt
                    </button>
                  )}
                </div>
              )}
              {showReviewActions && (
                <div className="ml-auto flex flex-wrap items-center gap-2">
                  <button
                    type="button"
                    onClick={toggleAllReviewQuestions}
                    disabled={approvablePendingQuestions.length === 0}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-white disabled:cursor-not-allowed disabled:opacity-45 motion-reduce:transform-none"
                  >
                    <Check className="size-3.5" aria-hidden="true" />
                    {allApprovableChecked ? 'Bỏ chọn' : 'Chọn tất cả'}
                  </button>
                  <button
                    type="button"
                    onClick={() => void handleBatchApprovePending()}
                    disabled={isReviewing || approvablePendingQuestions.length === 0}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#197158] px-3 text-xs font-bold text-white transition duration-200 hover:-translate-y-0.5 hover:bg-[#135a46] disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transform-none"
                  >
                    {isReviewing ? (
                      <LoaderCircle className="size-3.5 animate-spin motion-reduce:animate-none" />
                    ) : (
                      <ShieldCheck className="size-3.5" aria-hidden="true" />
                    )}
                    {selectedReviewIds.size > 0
                      ? `Duyệt đã chọn (${selectedReviewIds.size})`
                      : `Duyệt nhanh (${approvablePendingQuestions.length})`}
                  </button>
                </div>
              )}
            </div>
          )}
          {showReviewActions && selfSubmittedPendingCount > 0 && (
            <div className="border-b border-[#ecd9ab] bg-[#fff9ec] px-4 py-2.5 text-xs font-semibold text-[#8a5e17] sm:px-6">
              Có {selfSubmittedPendingCount} câu hỏi do chính bạn gửi — hệ thống không cho phép tự phê duyệt nên các câu này được loại khỏi danh sách duyệt nhanh.
            </div>
          )}
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
                workspace === 'review'
                  ? 'Hiện chưa có câu hỏi chờ phê duyệt phù hợp với bộ lọc.'
                  : workspace === 'mine'
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
          <DividedList as="div">
            {questions.map((question) => {
              const selfSubmitted = isSelfSubmitted(question.submittedBy, user?.id)
              const isSelected = showReviewActions
                ? selectedReviewIds.has(question.id)
                : selectedIds.has(question.id)
              const isViewing = isDetailOpen && viewingQuestionId === question.id
              const isOwned = canManageQuestion(question, user?.id)
              const canSelect = showReviewActions
                ? question.approvalStatus === 'SUBMITTED_FOR_REVIEW' && !selfSubmitted
                : showDraftSelectionActions &&
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
                    'relative grid cursor-pointer gap-6 px-4 py-4 transition sm:px-5',
                    showRowCheckbox
                      ? 'lg:grid-cols-[auto_minmax(0,1fr)_100px_100px_auto] lg:items-center'
                      : 'lg:grid-cols-[minmax(0,1fr)_100px_100px_auto] lg:items-center',
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
                  {showRowCheckbox && (
                    <label
                      className="flex items-center"
                      title={
                        showReviewActions && selfSubmitted
                          ? 'Bạn là người gửi câu hỏi này nên không thể tự phê duyệt.'
                          : undefined
                      }
                      onClick={(event) => event.stopPropagation()}
                      onKeyDown={(event) => event.stopPropagation()}
                    >
                      <span className="sr-only">Chọn câu hỏi {question.title}</span>
                      <input
                        type="checkbox"
                        aria-label={`Chọn câu hỏi ${question.title}`}
                        checked={isSelected}
                        disabled={!canSelect}
                        onChange={() =>
                          showReviewActions ? toggleReviewQuestion(question) : toggleQuestion(question)
                        }
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
                      {workspace === 'review' ? (
                        <span
                          className={cn(
                            'rounded-full px-2 py-1 text-xs font-bold',
                            selfSubmitted
                              ? 'bg-[#fff4df] text-[#8d6118]'
                              : 'bg-[#f1f3f6] text-[#52647d]',
                          )}
                        >
                          {selfSubmitted ? 'Của bạn · Không thể tự duyệt' : 'Giảng viên gửi'}
                        </span>
                      ) : (
                        workspace === 'mine' &&
                        user?.role !== 'Admin' && (
                          <span
                            className={cn(
                              'rounded-full px-2 py-1 text-xs font-bold',
                              selfSubmitted || isOwned
                                ? 'bg-[#eaf9f4] text-[#17745b]'
                                : 'bg-[#f1f3f6] text-[#6f7c8e]',
                            )}
                          >
                            {selfSubmitted || isOwned ? 'Của bạn' : 'Giảng viên khác'}
                          </span>
                        )
                      )}
                    </div>
                    <h3 className="mt-1 line-clamp-2 text-sm leading-snug wrap-anywhere font-bold text-[#0F2854]">
                      {question.title}
                    </h3>
                    <p className="mt-1 text-xs text-[#8794a6]">
                      {workspace === 'bank' && question.approvedAt
                        ? `Ngày duyệt ${formatDate(question.approvedAt)}`
                        : `Tạo ngày ${formatDate(question.createdAt)}`} · Nguồn{' '}
                      {getSourceLabel(question.source)}
                    </p>
                  </div>

                  <QuestionRowMetrics bloom={question.bloomLevel} score={question.totalRubricScore} />

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
          </DividedList>
        )}

        {!isLoading && questions.length > 0 && (
          <ListPagination
              label="Phân trang câu hỏi luyện tập"
              page={pageNumber}
              pageCount={totalPages}
              onPageChange={(nextPage) => {
                setPageNumber(nextPage)
                setSelectedIds(new Set())
                setSelectedReviewIds(new Set())
              }}
            />
        )}
      </section>

      {submissionOpen && (
        <FormDialog
          titleId="submit-questions-title"
          eyebrow="Ngân hàng câu hỏi"
          title="Gửi Trưởng Bộ môn duyệt"
          icon={Send}
          size="sm"
          busy={isSubmitting}
          onClose={() => setSubmissionOpen(false)}
          footer={(
            <SubmitDialogFooter
              form="submit-questions-form"
              label="Xác nhận gửi"
              busyLabel="Đang gửi…"
              busy={isSubmitting}
              disabled={selectedQuestions.length === 0}
              icon={Send}
              onCancel={() => setSubmissionOpen(false)}
            />
          )}
        >
          <form id="submit-questions-form" className="space-y-3" onSubmit={(event) => {
            event.preventDefault()
            void submitForReview()
          }}>
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] px-3 py-2.5">
              <p className="text-sm font-semibold text-[#0F2854]">{selectedQuestions.length} bản nháp được chọn</p>
              <span className={ds.infoChip}>{selectedQuestions[0]?.courseCode}</span>
            </div>
            <TextareaField label="Ghi chú (không bắt buộc)"
                value={submissionNotes}
                onChange={(event) => setSubmissionNotes(event.target.value)}
                disabled={isSubmitting}
                maxLength={1000}
                placeholder="Nhập ghi chú cho Trưởng Bộ môn" />
          </form>
        </FormDialog>
      )}

      {isDetailOpen && (
        <QuestionDrawer
          onClose={closeDetail}
          closeRef={closeDetailButtonRef}
          onPrevious={hasPrevDetail && !isDetailLoading ? () => openAdjacentDetail(-1) : undefined}
          onNext={hasNextDetail && !isDetailLoading ? () => openAdjacentDetail(1) : undefined}
          title={selectedQuestion?.title ?? 'Đang tải nội dung...'}
          badges={
            selectedQuestion && (
              <>
                <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                  {selectedQuestion.courseCode}
                </span>
                {selectedQuestion.cloCode && (
                  <span className="inline-flex items-center rounded-full border border-[#82aedb]/55 bg-[#F4FBFD] px-2 py-0.5 text-xs font-bold text-[#1C4D8D]">
                    {selectedQuestion.cloCode}
                  </span>
                )}
                <StatusBadge status={selectedQuestion.approvalStatus} />
                {selectedQuestion.approvalStatus === 'APPROVED' && (
                  <span
                    className={cn(
                      'inline-flex items-center rounded-full border px-2 py-0.5 text-[11px] font-bold',
                      selectedQuestion.isActive
                        ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                        : 'border-[#e5c5d4] bg-[#fff2f7] text-[#963b66]',
                    )}
                  >
                    {selectedQuestion.isActive ? 'Đang sử dụng' : 'Đang tạm ngưng'}
                  </span>
                )}
                {detailIndex >= 0 && (
                  <span className="text-[11px] font-semibold text-[#7a8aa0]">
                    {detailIndex + 1}/{questions.length}
                  </span>
                )}
              </>
            )
          }
          meta={
            selectedQuestion &&
            [
              getDifficultyLabel(selectedQuestion.difficulty),
              selectedQuestion.bloomLevel,
              getSourceLabel(selectedQuestion.source),
              workspace === 'bank' && selectedQuestion.approvedAt
                ? `Ngày duyệt ${formatDate(selectedQuestion.approvedAt)}`
                : formatDate(selectedQuestion.createdAt),
              selectedQuestion.submittedByName,
            ]
              .filter(Boolean)
              .join(' · ')
          }
          footer={
            selectedQuestion &&
            !isDetailLoading &&
            (((selectedQuestion.approvalStatus === 'DRAFT' ||
              selectedQuestion.approvalStatus === 'NEEDS_REVISION') &&
              (isSelfSubmitted(selectedQuestion.submittedBy, user?.id) || canReviewQuestions)) ||
              (canReviewQuestions &&
                (selectedQuestion.approvalStatus === 'SUBMITTED_FOR_REVIEW' ||
                  selectedQuestion.approvalStatus === 'APPROVED'))) && (
              <div className="space-y-2">
                {(selectedQuestion.approvalStatus === 'DRAFT' ||
                  selectedQuestion.approvalStatus === 'NEEDS_REVISION') &&
                  (isSelfSubmitted(selectedQuestion.submittedBy, user?.id) ||
                    canReviewQuestions) && (
                    <div
                      className={cn(
                        'grid gap-2',
                        selectedQuestion.approvalStatus === 'DRAFT' ? 'grid-cols-2' : 'grid-cols-1',
                      )}
                    >
                      <button
                        type="button"
                        disabled={isSubmitting || isMutatingQuestion}
                        onClick={() => setIsEditOpen(true)}
                        className="inline-flex h-10 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition duration-200 hover:-translate-y-0.5 hover:border-[#1C4D8D] hover:bg-[#E7F3FB] disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transform-none"
                      >
                        <PencilLine className="size-3.5 shrink-0" />
                        Chỉnh sửa câu hỏi
                      </button>

                      {selectedQuestion.approvalStatus === 'DRAFT' && (
                        <button
                          type="button"
                          disabled={isSubmitting || isMutatingQuestion}
                          onClick={() => setIsDeleteConfirmOpen(true)}
                          className="inline-flex h-10 items-center justify-center gap-1.5 rounded-xl border border-[#efc8cc] bg-[#fff2f3] px-3 text-xs font-bold text-[#a93d48] transition duration-200 hover:-translate-y-0.5 hover:bg-[#fde4e7] disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transform-none"
                        >
                          <Trash2 className="size-3.5 shrink-0" />
                          Thu hồi bản nháp
                        </button>
                      )}
                    </div>
                  )}

                {(selectedQuestion.approvalStatus === 'DRAFT' ||
                  selectedQuestion.approvalStatus === 'NEEDS_REVISION') &&
                  isSelfSubmitted(selectedQuestion.submittedBy, user?.id) && (
                    <button
                      type="button"
                      disabled={isSubmitting || isMutatingQuestion}
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

                {canReviewQuestions && selectedQuestion.approvalStatus === 'APPROVED' && (
                  <button
                    type="button"
                    disabled={isMutatingQuestion}
                    onClick={() => void handleToggleQuestionActive()}
                    className={cn(
                      'inline-flex h-10 w-full items-center justify-center gap-2 rounded-xl border px-4 text-sm font-bold transition duration-200 hover:-translate-y-0.5 disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transform-none',
                      selectedQuestion.isActive
                        ? 'border-[#efc8cc] bg-[#fff2f3] text-[#a93d48] hover:bg-[#fde4e7]'
                        : 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a] hover:bg-[#d8f3e7]',
                    )}
                  >
                    {isMutatingQuestion ? (
                      <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                    ) : (
                      <Power className="size-4" />
                    )}
                    {selectedQuestion.isActive ? 'Tạm ngưng sử dụng' : 'Kích hoạt lại'}
                  </button>
                )}

                {canReviewQuestions &&
                  selectedQuestion.approvalStatus === 'SUBMITTED_FOR_REVIEW' && (
                    <>
                      {isSelfSubmitted(selectedQuestion.submittedBy, user?.id) && (
                        <p className="rounded-xl border border-[#ecd9ab] bg-[#fff9ec] px-3 py-2 text-xs font-semibold text-[#8a5e17]">
                          Bạn là người gửi câu hỏi này nên không thể tự phê duyệt.
                        </p>
                      )}
                      <div className="grid grid-cols-3 gap-2">
                        <button
                          type="button"
                          disabled={
                            isReviewing ||
                            isSelfSubmitted(selectedQuestion.submittedBy, user?.id)
                          }
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
                    </>
                  )}
              </div>
            )
          }
        >
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

              <QuestionDetailContent question={selectedQuestion} />

              {canReviewQuestions &&
                selectedQuestion.approvalStatus === 'SUBMITTED_FOR_REVIEW' && (
                  <section className={cn('space-y-2 rounded-2xl border border-[#82aedb]/55 bg-[#F4FBFD] p-4', ds.panelShadow)}>
                    <TextareaField
                      label="Nhận xét thẩm định"
                      error={reviewNotesError}
                      className="min-h-20"
                      value={reviewNotes}
                      onChange={(event) => {
                        setReviewNotes(event.target.value)
                        setReviewNotesError('')
                      }}
                      placeholder="Tối thiểu 10 ký tự khi yêu cầu sửa hoặc từ chối..."
                    />
                  </section>
                )}
            </div>
          ) : null}
        </QuestionDrawer>
      )}

      <EditQuestionDraftDialog
        open={isEditOpen}
        question={selectedQuestion}
        onClose={() => setIsEditOpen(false)}
        onSaved={(questionId) => void handleDraftUpdated(questionId)}
      />

      {isDeleteConfirmOpen && selectedQuestion && (
        <ConfirmDialog
          tone="danger"
          eyebrow="Ngân hàng câu hỏi"
          title="Thu hồi bản nháp câu hỏi?"
          message={`Bạn có chắc chắn muốn thu hồi bản nháp "${selectedQuestion.title}" không? Câu hỏi nháp này sẽ được loại khỏi danh sách soạn thảo.`}
          confirmLabel="Xác nhận thu hồi"
          cancelLabel="Giữ lại"
          pending={isMutatingQuestion}
          onConfirm={() => void handleDeleteDraft()}
          onCancel={() => setIsDeleteConfirmOpen(false)}
        />
      )}

      <CreatePracticeQuestionDialog
        open={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        onCreated={() => {
          userSwitchedWorkspaceRef.current = true
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
