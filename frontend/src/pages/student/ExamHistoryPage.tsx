import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  Award,
  BookOpenCheck,
  CheckCircle2,
  Download,
  GraduationCap,
  LoaderCircle,
  LockKeyhole,
  MessageSquareText,
  Mic2,
  RefreshCw,
  Search,
  Send,
  ShieldCheck,
  TimerReset,
  type LucideIcon,
} from 'lucide-react'
import { OverlayModal } from '@/components/ui/OverlayDrawer'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import apiClient from '@/services/api.client'
import { mockExamService } from '@/services/mockExam.service'
import { practiceService } from '@/services/practice.service'
import type { MockExamHistoryItem, MockExamScorecard } from '@/types/mockExam.types'
import type { PracticeHistoryItem, PracticeSessionDetail } from '@/types/practice.types'

type ResultCategory = 'official' | 'mock' | 'practice'
type ResultFilter = 'all' | ResultCategory
type ResultStatus = 'LOCKED' | 'PENDING_REVIEW' | 'COMPLETED'
type ResultSort = 'recent' | 'score'

interface StudentAppealRecord {
  id: string
  ticketId: string
  studentId: string
  reason: string
  status: string
  assignedLecturerId?: string | null
  newScore?: number | null
  resolutionNote?: string | null
  createdAt?: string
}

interface RubricCriterion {
  id: string
  label: string
  score: number
  maxScore: number
  feedback: string
}

interface QuestionBreakdownItem {
  id: string
  orderLabel: string
  questionText: string
  answerTranscript: string | null
  score: number | null
  feedback: string | null
  criteria: RubricCriterion[]
}

interface StudentResultRecord {
  id: string
  rawSessionId?: string
  ticketId?: string
  category: ResultCategory
  status: ResultStatus
  subjectCode: string
  subjectName: string
  activityLabel: string
  completedAt: Date
  durationMinutes: number
  answeredCount?: number
  totalCount?: number
  aiScore?: number
  finalScore?: number
  isLocked: boolean
  rubric: RubricCriterion[]
  summary: string
  questionBreakdowns?: QuestionBreakdownItem[]
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
    iconClassName: 'border border-slate-200 bg-slate-50 text-slate-600',
    badgeClassName: 'border border-slate-200 bg-slate-100 text-slate-700',
  },
  mock: {
    label: 'Thi thử',
    icon: TimerReset,
    iconClassName: 'border border-slate-200 bg-slate-50 text-slate-600',
    badgeClassName: 'border border-slate-200 bg-slate-100 text-slate-700',
  },
  practice: {
    label: 'Luyện tập',
    icon: Mic2,
    iconClassName: 'border border-slate-200 bg-slate-50 text-slate-600',
    badgeClassName: 'border border-slate-200 bg-slate-100 text-slate-700',
  },
}

const FILTER_ORDER: ResultFilter[] = ['all', 'practice', 'mock', 'official']

const FILTER_LABELS: Record<ResultFilter, string> = {
  all: 'Tất cả',
  practice: 'Luyện tập',
  mock: 'Thi thử',
  official: 'Thi chính thức',
}

const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function addDays(date: Date, days: number) {
  return new Date(date.getTime() + days * 86_400_000)
}

function buildRubric(
  scores: [number, number, number, number],
  feedback: [string, string, string, string],
): RubricCriterion[] {
  const labels = [
    'Độ chính xác chuyên môn (CLO)',
    'Lập luận & cấu trúc giải pháp',
    'Diễn đạt & thuật ngữ Kỹ thuật Phần mềm',
    'Xử lý câu hỏi truy vấn phụ',
  ]

  return labels.map((label, index) => ({
    id: `criterion-${index + 1}`,
    label,
    score: scores[index],
    maxScore: 10,
    feedback: feedback[index],
  }))
}

function buildOfficialPreviewResults(now: Date): StudentResultRecord[] {
  return [
    {
      id: 'official-prn231-real-ticket',
      ticketId: '00000000-0000-0000-0000-000000000024',
      category: 'official',
      status: 'LOCKED',
      subjectCode: 'PRN231',
      subjectName: 'Phát triển Ứng dụng Đa nền tảng với .NET',
      activityLabel: 'Kỳ thi vấn đáp tại Phòng Lab 301 · Ca thi Kíp 1',
      completedAt: addDays(now, -1),
      durationMinutes: 20,
      answeredCount: 2,
      totalCount: 2,
      aiScore: 6.2,
      finalScore: 8.5,
      isLocked: true,
      rubric: buildRubric(
        [8.5, 8.5, 8.6, 8.4],
        [
          'Phân tích rõ cơ chế Clean Architecture, MediatR CQRS và Dependency Injection trong .NET 8.',
          'Giải thích đầy đủ luồng xử lý bất đồng bộ kết hợp hàng đợi trong bộ nhớ.',
          'Sử dụng chuẩn xác thuật ngữ chuyên ngành Kỹ thuật Phần mềm.',
          'Trả lời mạch lạc câu hỏi truy vấn phụ về cơ chế thử lại tự động khi dịch vụ quá tải.',
        ],
      ),
      summary:
        'Bài thi vấn đáp tại Phòng Lab 301 đã hoàn tất chấm điểm và được Giảng viên xác nhận công bố điểm chính thức.',
      lecturerNote:
        'Giảng viên đã nghe lại bản ghi âm phòng thi và xác nhận công bố điểm chính thức.',
      lockedAt: now,
    },
    {
      id: 'official-software-architecture',
      ticketId: '00000000-0000-0000-0000-000000000024',
      category: 'official',
      status: 'LOCKED',
      subjectCode: 'SWD392',
      subjectName: 'Kiến trúc & Thiết kế Phần mềm',
      activityLabel: 'Kỳ thi vấn đáp tại Phòng Lab B401',
      completedAt: addDays(now, -3),
      durationMinutes: 20,
      answeredCount: 3,
      totalCount: 3,
      aiScore: 8.6,
      finalScore: 8.8,
      isLocked: true,
      rubric: buildRubric(
        [9.0, 8.8, 8.7, 8.7],
        [
          'Phân tích chính xác ranh giới 4 tầng Clean Architecture và Dependency Inversion.',
          'Trình bày rõ ưu nhược điểm giữa hàng đợi nội bộ và hàng đợi phân tán.',
          'Sử dụng chuẩn xác các thuật ngữ CQRS, MediatR, FluentValidation và Idempotency.',
          'Giải quyết tốt tình huống xử lý lỗi quá tải dịch vụ bằng cơ chế thử lại.',
        ],
      ),
      summary:
        'Bài thi vấn đáp thể hiện tư duy thiết kế hệ thống vững chắc, giải thích mạch lạc cơ chế bảo toàn dữ liệu.',
      lecturerNote:
        'Giảng viên đã đối chiếu bản ghi âm phòng thi và đồng thuận với đánh giá hệ thống, cộng thêm 0.2đ phần trả lời câu hỏi phụ.',
      lockedAt: addDays(now, -2),
    },
    {
      id: 'official-database',
      ticketId: '00000000-0000-0000-0000-000000000024',
      category: 'official',
      status: 'LOCKED',
      subjectCode: 'DBI202',
      subjectName: 'Hệ Quản trị Cơ sở Dữ liệu',
      activityLabel: 'Kỳ thi vấn đáp tại Phòng Lab A305',
      completedAt: addDays(now, -8),
      durationMinutes: 25,
      answeredCount: 3,
      totalCount: 3,
      aiScore: 8.5,
      finalScore: 8.5,
      isLocked: true,
      rubric: buildRubric(
        [8.8, 8.4, 8.5, 8.3],
        [
          'Phân biệt chính xác chuẩn 3NF, khóa ngoại và ràng buộc toàn vẹn dữ liệu.',
          'Luồng phân tích rõ ràng, nêu được cách thiết kế lược đồ cơ sở dữ liệu chuẩn hóa.',
          'Thuật ngữ chuyên ngành nhất quán và dễ theo dõi.',
          'Trả lời đúng câu hỏi mở rộng về đánh chỉ mục B+ Tree và cô lập giao dịch.',
        ],
      ),
      summary:
        'Nắm vững thiết kế lược đồ 3NF, giao dịch ACID và tối ưu truy vấn; cần cô đọng hơn khi trình bày ví dụ thực tế.',
      lecturerNote: 'Giữ nguyên điểm đề xuất sau khi nghe lại toàn bộ bản ghi âm phòng thi.',
      lockedAt: addDays(now, -6),
    },
    {
      id: 'official-networking-pending',
      ticketId: '00000000-0000-0000-0000-000000000024',
      category: 'official',
      status: 'PENDING_REVIEW',
      subjectCode: 'SWR302',
      subjectName: 'Yêu cầu Phần mềm',
      activityLabel: 'Kỳ thi vấn đáp tại Phòng Lab B402',
      completedAt: addDays(now, -1),
      durationMinutes: 20,
      answeredCount: 3,
      totalCount: 3,
      isLocked: false,
      rubric: [],
      summary:
        'Bài thi đã được niêm phong và lưu trữ an toàn. Điểm chính thức sẽ hiển thị sau khi Giảng viên hoàn tất chấm duyệt và công bố điểm.',
      expectedReviewAt: addDays(now, 2),
    },
  ]
}

function mapPracticeHistoryItemToRecord(item: PracticeHistoryItem): StudentResultRecord {
  const startedMs = Date.parse(item.startedAt)
  const endedMs = item.endedAt ? Date.parse(item.endedAt) : startedMs
  const calculatedMinutes =
    !Number.isNaN(startedMs) && !Number.isNaN(endedMs) && endedMs > startedMs
      ? Math.max(1, Math.round((endedMs - startedMs) / 60_000))
      : Math.max(5, item.totalQuestions * 3)

  const completedDate = new Date(
    !Number.isNaN(endedMs) ? endedMs : !Number.isNaN(startedMs) ? startedMs : Date.now(),
  )
  const isFullSession = item.practiceMode === 'full_session'
  const score = item.averageScore ?? undefined

  return {
    id: `practice-${item.sessionId}`,
    rawSessionId: item.sessionId,
    category: 'practice',
    status: 'COMPLETED',
    subjectCode: item.courseCode || 'PRACTICE',
    subjectName: item.courseName || 'Phiên luyện tập tương tác AI',
    activityLabel: isFullSession
      ? `Luyện tập cả phiên (${item.answeredQuestions}/${item.totalQuestions} câu)`
      : `Luyện tập theo từng câu (${item.answeredQuestions} câu đã trả lời)`,
    completedAt: completedDate,
    durationMinutes: calculatedMinutes,
    answeredCount: item.answeredQuestions,
    totalCount: item.totalQuestions,
    aiScore: score,
    isLocked: false,
    rubric:
      score !== undefined
        ? buildRubric(
            [
              score,
              Math.max(0, Math.min(10, Number((score - 0.2).toFixed(1)))),
              Math.max(0, Math.min(10, Number((score + 0.1).toFixed(1)))),
              score,
            ],
            [
              'Đánh giá tổng hợp dựa trên các câu trả lời trong phiên luyện tập.',
              'Cấu trúc trình bày được ghi nhận qua hệ thống chấm điểm AI.',
              'Sử dụng thuật ngữ chuyên ngành Kỹ thuật Phần mềm trong bản transcript.',
              'Chọn phiên này để tải chi tiết từng câu hỏi và nhận xét cụ thể từ máy chủ.',
            ],
          )
        : [],
    summary:
      score !== undefined
        ? `Phiên luyện tập hoàn tất với ${item.answeredQuestions}/${item.totalQuestions} câu trả lời, điểm trung bình AI đạt ${score.toFixed(1)}/10.`
        : `Phiên luyện tập ghi nhận ${item.answeredQuestions}/${item.totalQuestions} câu trả lời. Đang đồng bộ chi tiết chấm điểm từ hệ thống.`,
  }
}

function mapMockHistoryItemToRecord(item: MockExamHistoryItem): StudentResultRecord {
  const startedMs = Date.parse(item.startedAt)
  const completedDate = new Date(!Number.isNaN(startedMs) ? startedMs : Date.now())
  const score = item.totalScore ?? undefined

  return {
    id: `mock-${item.sessionId}`,
    rawSessionId: item.sessionId,
    category: 'mock',
    status: 'COMPLETED',
    subjectCode: item.courseCode || 'MOCK',
    subjectName: item.courseName || 'Ca thi thử vấn đáp bấm giờ',
    activityLabel: `Thi thử tính giờ (${item.answeredQuestions}/${item.totalQuestions} câu)`,
    completedAt: completedDate,
    durationMinutes: item.durationMinutes || 20,
    answeredCount: item.answeredQuestions,
    totalCount: item.totalQuestions,
    aiScore: score,
    isLocked: false,
    rubric:
      score !== undefined
        ? buildRubric(
            [
              score,
              Math.max(0, Math.min(10, Number((score - 0.1).toFixed(1)))),
              Math.max(0, Math.min(10, Number((score + 0.1).toFixed(1)))),
              score,
            ],
            [
              'Kết quả chấm tức thì theo chuẩn đầu ra CLO của đề thi thử.',
              'Đánh giá khả năng phân tích và phản xạ trong thời gian giới hạn.',
              'Ghi nhận khả năng diễn đạt giọng nói qua công cụ nhận diện STT.',
              'Bấm xem chi tiết để tải Scorecard từng tiêu chí của ca thi thử.',
            ],
          )
        : [],
    summary:
      score !== undefined
        ? `Ca thi thử hoàn thành trong ${item.durationMinutes} phút (${item.answeredQuestions}/${item.totalQuestions} câu), đạt ${score.toFixed(1)}/10 điểm.`
        : `Ca thi thử đã ghi nhận ${item.answeredQuestions}/${item.totalQuestions} câu trả lời.`,
  }
}

function enrichPracticeRecordWithDetail(
  base: StudentResultRecord,
  detail: PracticeSessionDetail,
): StudentResultRecord {
  const questionMap = new Map(detail.questions.map((q) => [q.questionId, q.content]))
  const aggregatedCriteria: RubricCriterion[] = []

  const breakdowns: QuestionBreakdownItem[] = detail.answers.map((answer, index) => {
    const questionText =
      answer.isFollowUp && answer.followUpPrompt
        ? `[Câu hỏi phụ] ${answer.followUpPrompt}`
        : (questionMap.get(answer.questionId) ?? `Câu hỏi luyện tập #${index + 1}`)

    const criteria: RubricCriterion[] = answer.criteriaScores.map((crit, critIdx) => {
      const mapped: RubricCriterion = {
        id: `${answer.answerId}-${crit.criterionId || critIdx}`,
        label: crit.criterionName || `Tiêu chí #${critIdx + 1}`,
        score: crit.score,
        maxScore: 10,
        feedback: crit.comment ?? 'Đã chấm theo chuẩn Rubric.',
      }
      aggregatedCriteria.push(mapped)
      return mapped
    })

    return {
      id: answer.answerId,
      orderLabel: answer.isFollowUp ? `Follow-up #${index + 1}` : `Câu ${index + 1}`,
      questionText,
      answerTranscript: answer.answerText,
      score: answer.totalScore,
      feedback: answer.feedback,
      criteria,
    }
  })

  const feedbacks = detail.answers
    .map((ans) => ans.feedback)
    .filter((f): f is string => Boolean(f && f.trim()))

  return {
    ...base,
    rubric: aggregatedCriteria.length > 0 ? aggregatedCriteria.slice(0, 8) : base.rubric,
    summary: feedbacks.length > 0 ? feedbacks.join(' ') : base.summary,
    questionBreakdowns: breakdowns,
  }
}

function enrichMockRecordWithScorecard(
  base: StudentResultRecord,
  scorecard: MockExamScorecard,
): StudentResultRecord {
  const aggregatedCriteria: RubricCriterion[] = []

  const breakdowns: QuestionBreakdownItem[] = scorecard.questions.map((question) => {
    const criteria: RubricCriterion[] = question.criteriaScores.map((crit, critIdx) => {
      const mapped: RubricCriterion = {
        id: `${question.questionId}-${crit.criterionId || critIdx}`,
        label: `${crit.criterionName}${crit.bloomLevel ? ` (${crit.bloomLevel})` : ''}`,
        score: crit.score,
        maxScore: crit.maxScore || 10,
        feedback: crit.comment ?? 'Đã đánh giá theo barem CLO.',
      }
      aggregatedCriteria.push(mapped)
      return mapped
    })

    return {
      id: question.questionId,
      orderLabel: `Câu ${question.orderIndex}`,
      questionText: `${question.title}: ${question.content}`,
      answerTranscript: question.transcript,
      score: question.score,
      feedback: question.feedback,
      criteria,
    }
  })

  return {
    ...base,
    aiScore: scorecard.totalScore,
    rubric: aggregatedCriteria.length > 0 ? aggregatedCriteria.slice(0, 8) : base.rubric,
    summary: `Xếp loại: ${scorecard.classification} · Tổng điểm: ${scorecard.totalScore.toFixed(1)}/10 (${scorecard.answeredQuestions}/${scorecard.totalQuestions} câu đã trả lời trong ${scorecard.durationMinutes} phút).`,
    questionBreakdowns: breakdowns,
  }
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
  if (result.category === 'official') return 'Điểm chính thức'
  if (result.category === 'mock') return 'Điểm thi thử'
  return 'Điểm luyện tập'
}

interface ResultSemanticTone {
  leftBorder: string
  badgeClassName: string
  statusLabel: string
  iconClassName: string
}

function getResultSemanticTone(visibleScore: number | undefined): ResultSemanticTone {
  if (visibleScore === undefined) {
    return {
      leftBorder: 'border-l-4 border-l-amber-500',
      badgeClassName: 'border-amber-200 bg-amber-50 text-amber-800',
      statusLabel: 'Đang tổng hợp',
      iconClassName: 'border-amber-200 bg-amber-50 text-amber-700',
    }
  }

  if (visibleScore >= 8.0) {
    return {
      leftBorder: 'border-l-4 border-l-emerald-500',
      badgeClassName: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      statusLabel: 'Đạt tốt',
      iconClassName: 'border-emerald-200 bg-emerald-50 text-emerald-600',
    }
  }

  if (visibleScore >= 5.0) {
    return {
      leftBorder: 'border-l-4 border-l-amber-500',
      badgeClassName: 'border-amber-200 bg-amber-50 text-amber-800',
      statusLabel: 'Đáng chú ý',
      iconClassName: 'border-amber-200 bg-amber-50 text-amber-700',
    }
  }

  return {
    leftBorder: 'border-l-4 border-l-rose-500',
    badgeClassName: 'border-rose-200 bg-rose-50 text-rose-700',
    statusLabel: 'Cảnh báo · Cần cải thiện',
    iconClassName: 'border-rose-200 bg-rose-50 text-rose-600',
  }
}

function getCriterionSemanticTone(score: number, maxScore: number) {
  const ratio = maxScore > 0 ? score / maxScore : 0
  if (ratio >= 0.8) {
    return {
      barClassName: 'bg-emerald-500',
      badgeClassName: 'border-emerald-200 bg-emerald-50 text-emerald-700',
    }
  }
  if (ratio >= 0.5) {
    return {
      barClassName: 'bg-amber-500',
      badgeClassName: 'border-amber-200 bg-amber-50 text-amber-800',
    }
  }
  return {
    barClassName: 'bg-rose-500',
    badgeClassName: 'border-rose-200 bg-rose-50 text-rose-700',
  }
}

export function ExamHistoryPage() {
  const { user } = useAuth()
  const studentId = user?.id
  const [searchParams, setSearchParams] = useSearchParams()
  const [searchQuery, setSearchQuery] = useState('')
  const [practiceItems, setPracticeItems] = useState<PracticeHistoryItem[]>([])
  const [mockItems, setMockItems] = useState<MockExamHistoryItem[]>([])
  const [appeals, setAppeals] = useState<StudentAppealRecord[]>([])
  const [appealReason, setAppealReason] = useState(
    'Em đề nghị phúc khảo lại câu trả lời chuyên sâu về kiến trúc Clean Architecture và xử lý bất đồng bộ do một số thuật ngữ chuyên ngành tiếng Anh bị nhận diện thiếu.',
  )
  const [isSubmittingAppeal, setIsSubmittingAppeal] = useState(false)
  const [appealNotice, setAppealNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(
    null,
  )
  const [practiceDetails, setPracticeDetails] = useState<Record<string, PracticeSessionDetail>>({})
  const [mockDetails, setMockDetails] = useState<Record<string, MockExamScorecard>>({})
  const [failedDetailIds, setFailedDetailIds] = useState<Record<string, boolean>>({})
  const [isLoadingList, setIsLoadingList] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [drawerResultId, setDrawerResultId] = useState<string | null>(
    () => searchParams.get('result'),
  )

  const fetchHistoryRecords = useCallback(async () => {
    const validStudentGuid = studentId && UUID_REGEX.test(studentId) ? studentId : undefined

    const [practiceRes, mockRes, appealsRes] = await Promise.allSettled([
      practiceService.getStudentHistory(validStudentGuid),
      mockExamService.getHistory({ pageIndex: 1, pageSize: 50 }),
      apiClient.get<StudentAppealRecord[]>('/appeals', {
        params: validStudentGuid ? { studentId: validStudentGuid } : undefined,
      }),
    ])

    return {
      nextPractice: practiceRes.status === 'fulfilled' ? practiceRes.value : null,
      nextMock: mockRes.status === 'fulfilled' ? mockRes.value.items : null,
      nextAppeals:
        appealsRes.status === 'fulfilled' && Array.isArray(appealsRes.value.data)
          ? appealsRes.value.data
          : null,
    }
  }, [studentId])

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const { nextPractice, nextMock, nextAppeals } = await fetchHistoryRecords()
      if (cancelled) return
      if (nextPractice) setPracticeItems(nextPractice)
      if (nextMock) setMockItems(nextMock)
      if (nextAppeals) setAppeals(nextAppeals)
      setIsLoadingList(false)
    })()

    return () => {
      cancelled = true
    }
  }, [fetchHistoryRecords])

  const handleRefresh = async () => {
    if (isRefreshing) return
    setIsRefreshing(true)
    const { nextPractice, nextMock, nextAppeals } = await fetchHistoryRecords()
    if (nextPractice) setPracticeItems(nextPractice)
    if (nextMock) setMockItems(nextMock)
    if (nextAppeals) setAppeals(nextAppeals)
    setIsRefreshing(false)
  }

  const handleCreateAppeal = async (ticketId: string) => {
    const cleanReason = appealReason.trim()
    if (!cleanReason) {
      setAppealNotice({
        type: 'error',
        text: 'Vui lòng nhập lý do phúc khảo rõ ràng trước khi gửi đơn.',
      })
      return
    }
    setIsSubmittingAppeal(true)
    setAppealNotice(null)
    try {
      await apiClient.post('/appeals', {
        ticketId,
        reason: cleanReason,
      })
      const { nextAppeals } = await fetchHistoryRecords()
      if (nextAppeals) setAppeals(nextAppeals)
      setAppealNotice({
        type: 'success',
        text: 'Đã gửi đơn đề nghị phúc khảo thành công. Đơn của bạn đã được chuyển tới Trưởng Bộ môn để phân công chấm lại.',
      })
    } catch (error) {
      const detail =
        typeof error === 'object' && error !== null && 'detail' in error
          ? String((error as { detail?: unknown }).detail ?? '')
          : ''
      setAppealNotice({
        type: 'error',
        text: detail || 'Không thể gửi đơn phúc khảo lúc này.',
      })
    } finally {
      setIsSubmittingAppeal(false)
    }
  }

  const results = useMemo<StudentResultRecord[]>(() => {
    const now = new Date()
    const officialRecords = buildOfficialPreviewResults(now)

    const mappedPractice = practiceItems.map((item) => {
      const base = mapPracticeHistoryItemToRecord(item)
      const detail = practiceDetails[item.sessionId]
      return detail ? enrichPracticeRecordWithDetail(base, detail) : base
    })

    const mappedMock = mockItems.map((item) => {
      const base = mapMockHistoryItemToRecord(item)
      const detail = mockDetails[item.sessionId]
      return detail ? enrichMockRecordWithScorecard(base, detail) : base
    })

    return [...officialRecords, ...mappedMock, ...mappedPractice]
  }, [practiceItems, mockItems, practiceDetails, mockDetails])

  const activeFilter = parseFilter(searchParams)
  const activeSort: ResultSort = searchParams.get('sort') === 'score' ? 'score' : 'recent'

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
    const byCategory =
      activeFilter === 'all'
        ? [...results]
        : results.filter((result) => result.category === activeFilter)

    const normalizedSearch = searchQuery.trim().toLowerCase()
    const byQuery = normalizedSearch
      ? byCategory.filter(
          (item) =>
            item.subjectCode.toLowerCase().includes(normalizedSearch) ||
            item.subjectName.toLowerCase().includes(normalizedSearch) ||
            item.activityLabel.toLowerCase().includes(normalizedSearch),
        )
      : byCategory

    return byQuery.sort((left, right) => {
      if (activeSort === 'score') {
        return (getVisibleScore(right) ?? -1) - (getVisibleScore(left) ?? -1)
      }
      return right.completedAt.getTime() - left.completedAt.getTime()
    })
  }, [activeFilter, activeSort, results, searchQuery])

  const selectedResult = useMemo(() => {
    if (!drawerResultId) return null
    return (
      results.find(
        (item) => item.id === drawerResultId || item.rawSessionId === drawerResultId,
      ) ?? null
    )
  }, [drawerResultId, results])

  useEffect(() => {
    const sessionId = selectedResult?.rawSessionId
    const category = selectedResult?.category
    if (!sessionId || !category || failedDetailIds[sessionId]) return

    if (category === 'practice' && !practiceDetails[sessionId]) {
      let cancelled = false
      void (async () => {
        try {
          const detail = await practiceService.getSession(sessionId)
          if (!cancelled) {
            setPracticeDetails((prev) => ({ ...prev, [sessionId]: detail }))
          }
        } catch {
          if (!cancelled) {
            setFailedDetailIds((prev) => ({ ...prev, [sessionId]: true }))
          }
        }
      })()
      return () => {
        cancelled = true
      }
    }

    if (category === 'mock' && !mockDetails[sessionId]) {
      let cancelled = false
      void (async () => {
        try {
          const scorecard = await mockExamService.getHistoryDetail(sessionId)
          if (!cancelled) {
            setMockDetails((prev) => ({ ...prev, [sessionId]: scorecard }))
          }
        } catch {
          if (!cancelled) {
            setFailedDetailIds((prev) => ({ ...prev, [sessionId]: true }))
          }
        }
      })()
      return () => {
        cancelled = true
      }
    }
  }, [
    selectedResult?.rawSessionId,
    selectedResult?.category,
    practiceDetails,
    mockDetails,
    failedDetailIds,
  ])

  const isSelectedDetailLoading = Boolean(
    selectedResult?.rawSessionId &&
      !failedDetailIds[selectedResult.rawSessionId] &&
      ((selectedResult.category === 'practice' && !practiceDetails[selectedResult.rawSessionId]) ||
        (selectedResult.category === 'mock' && !mockDetails[selectedResult.rawSessionId])),
  )

  const releasedOfficialResults = results.filter(isReleasedOfficialResult)
  const officialAverage = releasedOfficialResults.length
    ? releasedOfficialResults.reduce((total, result) => total + (result.finalScore ?? 0), 0) /
      releasedOfficialResults.length
    : null

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
    setDrawerResultId(null)
  }

  const handleOpenDetailDrawer = (resultId: string) => {
    setDrawerResultId(resultId)
    setAppealNotice(null)
    updateQuery({ result: resultId })
  }

  const handleCloseDetailDrawer = () => {
    setDrawerResultId(null)
    updateQuery({ result: null })
  }

  return (
    <div lang="vi" className="space-y-4 pb-6 font-sans">
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
            color: #1E293B;
          }
        }
      `}</style>

      {/* Bản in A4 */}
      <section id="student-results-print" className="hidden">
        <div className="rounded-2xl border border-slate-200 p-8">
          <div className="border-b border-slate-200 pb-5">
            <p className="text-sm font-semibold text-[#F26F21]">
              FPT University · Hệ thống Đánh giá Vấn đáp
            </p>
            <h2 className="mt-2 text-xl font-semibold text-slate-900">
              Phiếu Tổng hợp Điểm Khảo thí Vấn đáp
            </h2>
            <p className="mt-1 text-xs text-slate-600">
              Sinh viên: {user?.fullName ?? 'Sinh viên FPT'} · Học kỳ Fall 2026
            </p>
          </div>
          <table className="mt-6 w-full border-collapse text-left text-sm">
            <thead>
              <tr className="border-b border-slate-200">
                <th className="py-3 font-semibold text-slate-900">Mã HP</th>
                <th className="py-3 font-semibold text-slate-900">Tên học phần</th>
                <th className="py-3 font-semibold text-slate-900">Ngày thi</th>
                <th className="py-3 text-right font-semibold text-slate-900">Điểm chính thức</th>
              </tr>
            </thead>
            <tbody>
              {releasedOfficialResults.map((result) => (
                <tr key={result.id} className="border-b border-slate-200">
                  <td className="py-3 font-semibold text-slate-900">{result.subjectCode}</td>
                  <td className="py-3 text-slate-600">{result.subjectName}</td>
                  <td className="py-3 text-slate-600">{formatDate(result.completedAt)}</td>
                  <td className="py-3 text-right font-semibold text-slate-900">
                    {result.finalScore?.toFixed(1)} / 10
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="mt-6 flex items-center justify-between rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm">
            <span className="font-semibold text-slate-900">Điểm trung bình chính thức</span>
            <strong className="text-base font-semibold text-slate-900">
              {officialAverage === null ? '—' : `${officialAverage.toFixed(1)} / 10`}
            </strong>
          </div>
        </div>
      </section>

      {/* Thanh tiêu đề ấm áp kèm chip thống kê nhanh */}
      <section className="rounded-2xl border border-slate-200 bg-gradient-to-br from-[#FFF7ED]/80 via-white to-amber-50/30 p-5 shadow-xs">
        <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
          <div>
            <h1 className="text-xl font-semibold text-slate-900">Kết quả & Lịch sử làm bài</h1>
            <p className="mt-1 text-xs text-slate-500">
              Chọn một phiên làm bài bên dưới để xem chi tiết điểm và nhận xét
            </p>
            <div className="mt-3 flex flex-wrap items-center gap-2 text-xs">
              <span className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-200 bg-emerald-50 px-2.5 py-1 font-semibold text-emerald-700">
                Điểm TB chính thức:{' '}
                {officialAverage === null ? '—' : `${officialAverage.toFixed(1)} / 10`}
              </span>
              <span className="inline-flex items-center gap-1.5 rounded-lg border border-amber-200 bg-amber-50 px-2.5 py-1 font-semibold text-amber-800">
                {filterCounts.practice} phiên luyện tập · {filterCounts.mock} ca thi thử
              </span>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <button
              type="button"
              onClick={() => window.print()}
              className="inline-flex min-h-9 cursor-pointer items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-3.5 text-xs font-semibold text-slate-700 shadow-2xs transition hover:bg-slate-50"
            >
              <Download className="size-4 text-slate-600" />
              In phiếu điểm
            </button>

            <button
              type="button"
              onClick={() => void handleRefresh()}
              disabled={isRefreshing}
              className="inline-flex min-h-9 cursor-pointer items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-3.5 text-xs font-semibold text-white shadow-xs transition hover:bg-[#D95B16] disabled:opacity-60"
            >
              {isRefreshing ? (
                <LoaderCircle className="size-4 animate-spin" />
              ) : (
                <RefreshCw className="size-4" />
              )}
              Làm mới
            </button>
          </div>
        </div>
      </section>

      {/* Thanh bộ lọc & Tìm kiếm đồng nhất */}
      <section className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-3.5 shadow-xs lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap gap-1.5" aria-label="Lọc kết quả theo phân hệ">
          {FILTER_ORDER.map((filter) => {
            const isActive = activeFilter === filter
            return (
              <button
                key={filter}
                type="button"
                aria-pressed={isActive}
                onClick={() => handleFilterChange(filter)}
                className={cn(
                  'inline-flex min-h-9 cursor-pointer items-center gap-2 rounded-xl px-3.5 text-xs transition',
                  isActive
                    ? 'bg-[#F26F21] font-semibold text-white'
                    : 'font-medium text-slate-600 hover:bg-slate-100',
                )}
              >
                {FILTER_LABELS[filter]}
                <span
                  className={cn(
                    'rounded-full px-2 py-0.5 text-xs',
                    isActive ? 'bg-white/20 text-white' : 'bg-slate-100 text-slate-600',
                  )}
                >
                  {filterCounts[filter]}
                </span>
              </button>
            )
          })}
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          <div className="relative flex-1 sm:w-64 sm:flex-initial">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-3.5 -translate-y-1/2 text-slate-500" />
            <input
              type="search"
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
              placeholder="Tìm theo mã môn, tên môn..."
              aria-label="Tìm kiếm kết quả theo môn học"
              className="min-h-9 w-full rounded-xl border border-slate-200 bg-slate-50 pr-3 pl-8 text-xs text-slate-900 outline-none focus:border-[#F26F21] focus:bg-white"
            />
          </div>

          <label className="flex min-h-9 items-center gap-2 rounded-xl border border-slate-200 bg-slate-50 px-3 text-xs text-slate-600">
            Sắp xếp:
            <select
              value={activeSort}
              onChange={(event) =>
                updateQuery({ sort: event.target.value === 'score' ? 'score' : null })
              }
              className="bg-transparent text-xs font-semibold text-slate-900 outline-none"
              aria-label="Sắp xếp kết quả"
            >
              <option value="recent">Thời gian mới nhất</option>
              <option value="score">Điểm số cao nhất</option>
            </select>
          </label>
        </div>
      </section>

      {/* Danh sách các phiên kết quả phân màu ngữ nghĩa 3 cấp */}
      {visibleResults.length === 0 ? (
        <div className="rounded-2xl border border-slate-200 bg-white px-6 py-12 text-center">
          <BookOpenCheck className="mx-auto size-8 text-slate-400" />
          <h2 className="mt-3 text-base font-semibold text-slate-900">
            {isLoadingList ? 'Đang tải lịch sử kết quả...' : 'Chưa có kết quả phù hợp bộ lọc'}
          </h2>
          <p className="mt-1 text-xs text-slate-500">
            Hãy chọn bộ lọc khác, xóa từ khóa tìm kiếm hoặc hoàn thành một phiên làm bài mới.
          </p>
        </div>
      ) : (
        <section aria-label="Danh sách phiên khảo thí" className="space-y-3">
          {visibleResults.map((result) => {
            const categoryStyle = CATEGORY_STYLES[result.category]
            const CategoryIcon = categoryStyle.icon
            const visibleScore = getVisibleScore(result)
            const tone = getResultSemanticTone(visibleScore)

            return (
              <article
                key={result.id}
                className={cn(
                  'rounded-2xl border border-slate-200 bg-white p-4 shadow-xs transition-all hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-md',
                  tone.leftBorder,
                )}
              >
                <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
                  <div className="flex min-w-0 items-center gap-3.5">
                    <span
                      className={cn(
                        'flex size-10 shrink-0 items-center justify-center rounded-xl border',
                        tone.iconClassName,
                      )}
                    >
                      <CategoryIcon className="size-5" />
                    </span>

                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <h2 className="truncate text-sm font-semibold text-slate-900">
                          {result.subjectCode} · {result.subjectName}
                        </h2>
                        <span
                          className={cn(
                            'rounded-md border px-2 py-0.5 text-[11px] font-semibold',
                            tone.badgeClassName,
                          )}
                        >
                          {tone.statusLabel}
                        </span>
                      </div>
                      <p className="mt-0.5 text-xs text-slate-500">
                        {categoryStyle.label} · {formatDate(result.completedAt)} ·{' '}
                        {formatTime(result.completedAt)}
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center justify-between gap-3 sm:justify-end">
                    <span
                      className={cn(
                        'rounded-xl border px-3 py-1.5 text-sm font-semibold',
                        tone.badgeClassName,
                      )}
                    >
                      {visibleScore === undefined ? 'Chờ công bố' : `${visibleScore.toFixed(1)} / 10`}
                    </span>

                    <button
                      type="button"
                      onClick={() => handleOpenDetailDrawer(result.id)}
                      className="cursor-pointer rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-2 text-xs font-semibold text-slate-700 transition hover:border-[#F26F21] hover:bg-orange-50/40 hover:text-[#C2410C]"
                    >
                      Xem chi tiết →
                    </button>
                  </div>
                </div>
              </article>
            )
          })}
        </section>
      )}

      {/* Màn hình phụ (OverlayModal khi bấm vào 1 kết quả) */}
      <OverlayModal
        open={Boolean(selectedResult)}
        onClose={handleCloseDetailDrawer}
        badge={
          selectedResult ? CATEGORY_STYLES[selectedResult.category].label : 'Chi tiết kết quả'
        }
        title={
          selectedResult
            ? `${selectedResult.subjectCode} · ${selectedResult.subjectName}`
            : 'Chi tiết kết quả vấn đáp'
        }
        subtitle={
          selectedResult
            ? `${selectedResult.activityLabel} · Ngày ${formatDate(selectedResult.completedAt)} lúc ${formatTime(selectedResult.completedAt)}`
            : undefined
        }
        size="3xl"
      >
        {selectedResult &&
          (() => {
            const visibleScore = getVisibleScore(selectedResult)
            const isReleased = isReleasedOfficialResult(selectedResult)
            const isPending = isPendingOfficialResult(selectedResult)
            const isDetailLoading = isSelectedDetailLoading
            const selectedTone = getResultSemanticTone(visibleScore)

            return (
              <div className="space-y-5">
                {/* Thanh tóm tắt tổng điểm */}
                <div className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-slate-200 bg-slate-50 p-4">
                  <div>
                    <span className="text-xs text-slate-500">{getScoreLabel(selectedResult)}</span>
                    <div className="mt-1 flex flex-wrap items-center gap-2.5">
                      <span
                        className={cn(
                          'rounded-xl border px-3 py-1 text-lg font-semibold',
                          selectedTone.badgeClassName,
                        )}
                      >
                        {visibleScore === undefined ? 'Chờ công bố' : `${visibleScore.toFixed(1)} / 10`}
                      </span>
                      <span
                        className={cn(
                          'rounded-md border px-2.5 py-0.5 text-xs font-semibold',
                          selectedTone.badgeClassName,
                        )}
                      >
                        {selectedTone.statusLabel}
                      </span>
                      {isReleased && (
                        <span className="inline-flex items-center gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-700">
                          <LockKeyhole className="size-3" /> Đã công bố chính thức
                        </span>
                      )}
                    </div>
                  </div>

                  {isDetailLoading && (
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-800">
                      <LoaderCircle className="size-3.5 animate-spin text-amber-600" />
                      Đang tải chi tiết câu trả lời...
                    </span>
                  )}
                </div>

                {isPending ? (
                  <div className="space-y-4">
                    <div className="rounded-xl border border-amber-200 bg-amber-50/70 p-4">
                      <div className="flex items-center gap-2 text-sm font-semibold text-amber-900">
                        <ShieldCheck className="size-4 text-amber-600" />
                        Bài thi đang trong quy trình Giảng viên chấm duyệt
                      </div>
                      <p className="mt-1.5 text-xs leading-relaxed text-amber-900/80">
                        Theo quy chế khảo thí, điểm chi tiết của bài thi chính thức sẽ được hiển thị
                        sau khi Giảng viên hoàn tất đối chiếu bài làm và công bố điểm ca thi.
                      </p>
                      <p className="mt-2 text-xs font-semibold text-amber-950">
                        Dự kiến công bố trước:{' '}
                        {formatDate(selectedResult.expectedReviewAt ?? new Date())}
                      </p>
                    </div>

                    <div className="rounded-xl border border-slate-200 bg-white p-4">
                      <h3 className="text-sm font-semibold text-slate-900">
                        Tiến trình xử lý bài thi
                      </h3>
                      <ol className="mt-3 space-y-2.5">
                        {[
                          ['Tiếp nhận & Niêm phong bài làm phòng thi', 'Hoàn tất'],
                          ['Hệ thống chấm điểm tự động theo tiêu chí', 'Hoàn tất'],
                          ['Giảng viên đối chiếu bản ghi âm & xác nhận', 'Đang thực hiện'],
                          ['Công bố điểm chính thức lên cổng sinh viên', 'Chờ thực hiện'],
                        ].map(([stepTitle, stepState], index) => (
                          <li key={stepTitle} className="flex items-center gap-3 text-xs">
                            <span
                              className={cn(
                                'flex size-6 shrink-0 items-center justify-center rounded-full font-semibold',
                                index < 2
                                  ? 'border border-emerald-200 bg-emerald-50 text-emerald-700'
                                  : index === 2
                                    ? 'bg-amber-500 text-white'
                                    : 'border border-slate-200 bg-slate-100 text-slate-700',
                              )}
                            >
                              {index < 2 ? <CheckCircle2 className="size-3.5" /> : index + 1}
                            </span>
                            <span className="font-semibold text-slate-900">{stepTitle}</span>
                            <span className="ml-auto text-slate-500">{stepState}</span>
                          </li>
                        ))}
                      </ol>
                    </div>
                  </div>
                ) : (
                  <>
                    {/* Nhận xét tổng quát */}
                    <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                      <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                        <MessageSquareText className="size-4 text-[#F26F21]" />
                        Nhận xét tổng quát
                      </h3>
                      <p className="mt-2 text-sm leading-relaxed text-slate-600">
                        {selectedResult.summary}
                      </p>

                      {isReleased && selectedResult.lecturerNote && (
                        <div className="mt-3 rounded-xl border border-emerald-200 bg-emerald-50/50 p-3">
                          <p className="flex items-center gap-1.5 text-xs font-semibold text-emerald-900">
                            <LockKeyhole className="size-3.5 text-emerald-700" />
                            Nhận xét xác nhận của Giảng viên chấm thi
                          </p>
                          <p className="mt-1 text-xs leading-relaxed text-emerald-900/90">
                            {selectedResult.lecturerNote}
                          </p>
                        </div>
                      )}
                    </div>

                    {/* Bảng điểm từng tiêu chí Rubric với thanh điểm màu ngữ nghĩa 3 cấp */}
                    <div>
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                          <Award className="size-4 text-[#F26F21]" />
                          Chi tiết điểm theo tiêu chí Rubric
                        </h3>
                        <span className="text-xs text-slate-500">Chuẩn thang điểm 10.0</span>
                      </div>

                      {selectedResult.rubric.length === 0 ? (
                        <p className="mt-3 rounded-xl border border-slate-200 bg-slate-50 p-4 text-xs text-slate-500">
                          Phiên này chưa có điểm tiêu chí chi tiết hoặc đang trong quá trình chấm.
                        </p>
                      ) : (
                        <div className="mt-3 grid gap-3 sm:grid-cols-2">
                          {selectedResult.rubric.map((criterion) => {
                            const maxScore = criterion.maxScore || 10
                            const percentage = Math.min(
                              100,
                              Math.max(0, (criterion.score / maxScore) * 100),
                            )
                            const critTone = getCriterionSemanticTone(criterion.score, maxScore)

                            return (
                              <article
                                key={criterion.id}
                                className="rounded-xl border border-slate-200 bg-white p-3.5 shadow-2xs"
                              >
                                <div className="flex items-center justify-between gap-2">
                                  <h4 className="text-xs font-semibold text-slate-900">
                                    {criterion.label}
                                  </h4>
                                  <span
                                    className={cn(
                                      'shrink-0 rounded-md border px-2 py-0.5 text-xs font-semibold',
                                      critTone.badgeClassName,
                                    )}
                                  >
                                    {criterion.score.toFixed(1)}/{maxScore}
                                  </span>
                                </div>
                                <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-100">
                                  <div
                                    className={cn(
                                      'h-full rounded-full transition-all',
                                      critTone.barClassName,
                                    )}
                                    style={{ width: `${percentage}%` }}
                                  />
                                </div>
                                <p className="mt-2 text-xs leading-relaxed text-slate-600">
                                  {criterion.feedback}
                                </p>
                              </article>
                            )
                          })}
                        </div>
                      )}
                    </div>

                    {/* Chi tiết từng câu hỏi & bản ghi Transcript */}
                    {selectedResult.questionBreakdowns &&
                      selectedResult.questionBreakdowns.length > 0 && (
                        <div className="border-t border-slate-200 pt-4">
                          <h3 className="text-sm font-semibold text-slate-900">
                            Chi tiết từng câu hỏi ({selectedResult.questionBreakdowns.length} câu)
                          </h3>
                          <div className="mt-3 space-y-3">
                            {selectedResult.questionBreakdowns.map((item) => {
                              const itemTone = getResultSemanticTone(item.score ?? undefined)
                              return (
                                <article
                                  key={item.id}
                                  className="rounded-xl border border-slate-200 bg-slate-50 p-4"
                                >
                                  <div className="flex items-center justify-between gap-2">
                                    <span className="rounded-md border border-slate-200 bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-700">
                                      {item.orderLabel}
                                    </span>
                                    <span
                                      className={cn(
                                        'rounded-md border px-2 py-0.5 text-xs font-semibold',
                                        itemTone.badgeClassName,
                                      )}
                                    >
                                      Điểm câu:{' '}
                                      {item.score === null
                                        ? 'Đang chấm'
                                        : `${item.score.toFixed(1)}/10`}
                                    </span>
                                  </div>
                                  <p className="mt-2 text-sm font-semibold text-slate-900">
                                    {item.questionText}
                                  </p>
                                  {item.answerTranscript && (
                                    <div className="mt-2 rounded-lg border border-slate-200 bg-white p-3">
                                      <p className="text-xs font-semibold text-slate-700">
                                        Nội dung câu trả lời của bạn:
                                      </p>
                                      <p className="mt-1 text-xs leading-relaxed text-slate-600">
                                        {item.answerTranscript}
                                      </p>
                                    </div>
                                  )}
                                  {item.feedback && (
                                    <p className="mt-2 text-xs leading-relaxed text-slate-600">
                                      <strong className="font-semibold text-slate-900">
                                        Nhận xét:{' '}
                                      </strong>
                                      {item.feedback}
                                    </p>
                                  )}
                                </article>
                              )
                            })}
                          </div>
                        </div>
                      )}

                    {/* Khu vực gửi phúc khảo (nếu là bài thi chính thức đã công bố điểm) */}
                    {isReleased && (
                      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <div>
                            <h3 className="text-sm font-semibold text-slate-900">
                              Đề nghị phúc khảo điểm thi
                            </h3>
                            <p className="mt-0.5 text-xs text-slate-500">
                              Đơn phúc khảo sẽ được gửi tới Trưởng Bộ môn để phân công Giảng viên
                              thẩm định và chấm lại bài thi của bạn.
                            </p>
                          </div>
                          <span className="rounded-md border border-amber-200 bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-800">
                            Phúc khảo nội bộ
                          </span>
                        </div>

                        {appeals.length > 0 && (
                          <div className="mt-3 space-y-2">
                            <p className="text-xs font-semibold text-slate-900">
                              Lịch sử đơn phúc khảo của bạn ({appeals.length} đơn):
                            </p>
                            {appeals.map((appeal) => (
                              <div
                                key={appeal.id}
                                className="rounded-lg border border-slate-200 bg-white p-3 text-xs"
                              >
                                <div className="flex flex-wrap items-center justify-between gap-2">
                                  <span className="font-semibold text-slate-900">
                                    Mã đơn #{appeal.id.slice(0, 8)}
                                  </span>
                                  <span className="rounded-md border border-amber-200 bg-amber-50 px-2 py-0.5 font-semibold text-amber-800">
                                    Trạng thái: {appeal.status}
                                  </span>
                                </div>
                                <p className="mt-1 text-slate-600">
                                  <strong className="font-semibold text-slate-900">Lý do:</strong>{' '}
                                  {appeal.reason}
                                </p>
                                {appeal.resolutionNote && (
                                  <p className="mt-1 text-emerald-700">
                                    <strong className="font-semibold text-emerald-900">
                                      Kết luận phúc khảo:
                                    </strong>{' '}
                                    {appeal.resolutionNote}{' '}
                                    {typeof appeal.newScore === 'number'
                                      ? `(Điểm sau phúc khảo: ${appeal.newScore.toFixed(1)}/10)`
                                      : ''}
                                  </p>
                                )}
                              </div>
                            ))}
                          </div>
                        )}

                        <div className="mt-3 space-y-2.5">
                          <label
                            htmlFor="official-appeal-reason"
                            className="block text-xs font-semibold text-slate-900"
                          >
                            Lý do đề nghị phúc khảo bài thi vấn đáp
                          </label>
                          <textarea
                            id="official-appeal-reason"
                            rows={3}
                            value={appealReason}
                            onChange={(event) => setAppealReason(event.target.value)}
                            placeholder="Nêu rõ câu hỏi hoặc tiêu chí đánh giá bạn đề nghị chấm phúc khảo lại..."
                            className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs text-slate-900 outline-none focus:border-[#F26F21]"
                          />
                          {appealNotice && (
                            <p
                              className={cn(
                                'rounded-lg border px-3 py-2 text-xs font-semibold',
                                appealNotice.type === 'success'
                                  ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
                                  : 'border-rose-200 bg-rose-50 text-rose-700',
                              )}
                            >
                              {appealNotice.text}
                            </p>
                          )}
                          <div className="flex justify-end">
                            <button
                              type="button"
                              disabled={isSubmittingAppeal}
                              onClick={() =>
                                void handleCreateAppeal(
                                  selectedResult.ticketId ??
                                    '00000000-0000-0000-0000-000000000024',
                                )
                              }
                              className="inline-flex cursor-pointer items-center gap-2 rounded-xl bg-[#F26F21] px-4 py-2 text-xs font-semibold text-white transition hover:bg-[#D95B16] disabled:cursor-not-allowed disabled:opacity-60"
                            >
                              {isSubmittingAppeal ? (
                                <LoaderCircle className="size-3.5 animate-spin" />
                              ) : (
                                <Send className="size-3.5" />
                              )}
                              Gửi đơn phúc khảo
                            </button>
                          </div>
                        </div>
                      </div>
                    )}
                  </>
                )}
              </div>
            )
          })()}
      </OverlayModal>
    </div>
  )
}

export { ExamHistoryPage as StudentResultsPage }
