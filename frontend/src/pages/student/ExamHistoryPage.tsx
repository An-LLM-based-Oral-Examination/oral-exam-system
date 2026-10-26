import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  ArrowRight,
  Award,
  BarChart3,
  BookOpenCheck,
  CalendarDays,
  CheckCircle2,
  ChevronDown,
  Clock3,
  FileCheck2,
  Filter,
  History,
  LoaderCircle,
  MessageSquareText,
  Mic2,
  RefreshCw,
  Search,
  ShieldCheck,
  Trophy,
  X,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'
import apiClient from '@/services/api.client'
import { appealsService } from '@/services/appeals.service'
import { mockExamService } from '@/services/mockExam.service'
import { officialExamService } from '@/services/officialExam.service'
import { practiceService } from '@/services/practice.service'
import { MockExamScorecard } from '@/components/mock-exam/MockExamScorecard'
import { PracticeSessionScorecard } from '@/components/practice/PracticeSessionScorecard'
import { QuestionImage } from '@/components/question/QuestionImage'
import type { Appeal } from '@/types/appeal.types'
import type {
  MockExamHistoryItem,
  MockExamScorecard as MockExamScorecardData,
} from '@/types/mockExam.types'
import type { StudentOfficialExamGrade } from '@/types/officialExam.types'
import type { PracticeHistoryItem, PracticeSessionDetail } from '@/types/practice.types'

type HistoryCategory = 'all' | 'practice' | 'mock' | 'official'

const COURSE_CODE_TONES = [
  'bg-[#4988C4]/16 text-[#1C4D8D]',
  'bg-[#e8f8f3] text-[#16836a]',
  'bg-[#fff4e8] text-[#c26a12]',
  'bg-[#f3eefe] text-[#6d4cc4]',
  'bg-[#e7f6fb] text-[#0e7490]',
  'bg-[#fdecef] text-[#c2415b]',
  'bg-[#eef8e8] text-[#3d7a2a]',
]

function courseCodeTone(code?: string) {
  if (!code) return COURSE_CODE_TONES[0]
  const hash = Array.from(code).reduce((sum, char) => sum + char.charCodeAt(0), 0)
  return COURSE_CODE_TONES[hash % COURSE_CODE_TONES.length]
}

interface LiveOfficialExamSummary {
  ticketId: string
  shiftId: string
  courseCode: string
  courseName: string
  sessionTitle: string
  shiftName: string
  roomLab: string
  examDate: string
  startTime: string
  endTime: string
  seatNumber: number
  status: string
  finalScore: number | null
  isLocked: boolean
  studentAcknowledgementStatus: string
}

interface UnifiedHistoryRow {
  uid: string
  rawId: string
  category: 'practice' | 'mock' | 'official'
  badgeLabel: string
  courseCode: string
  courseName: string
  title: string
  subtitle: string
  timestamp: string
  statusRaw: string
  statusLabel: string
  statusTone: 'emerald' | 'blue' | 'amber' | 'slate'
  score: number | null
  canContinue: boolean
  continueUrl: string | null
}

function unwrapDashboardPayload<T>(payload: unknown): T {
  if (payload && typeof payload === 'object') {
    const record = payload as Record<string, unknown>
    return (record.value ?? record.Value ?? record.data ?? record.Data ?? payload) as T
  }
  return payload as T
}

function extractErrorMessage(error: unknown, fallback = 'Không thể tải dữ liệu lịch sử.') {
  if (typeof error === 'object' && error !== null) {
    const rec = error as Record<string, unknown>
    const detail = rec.detail ?? rec.Detail ?? rec.error ?? rec.message
    if (typeof detail === 'string' && detail.trim()) return detail.trim()
  }
  return error instanceof Error ? error.message : fallback
}

function formatDateTime(value: string | null) {
  if (!value) return 'Chưa ghi nhận'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour12: false,
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(date)
}

function averageScore(values: number[]): number | null {
  if (values.length === 0) return null
  const sum = values.reduce((acc, v) => acc + v, 0)
  return Math.round((sum / values.length) * 100) / 100
}

function OfficialExamScorecardView({
  ticket,
  grade,
  appeal,
  appealReason,
  onChangeAppealReason,
  onAcknowledge,
  onCreateAppeal,
  busy,
}: {
  ticket: LiveOfficialExamSummary
  grade: StudentOfficialExamGrade
  appeal: Appeal | undefined
  appealReason: string
  onChangeAppealReason: (next: string) => void
  onAcknowledge: () => void
  onCreateAppeal: () => void
  busy: boolean
}) {
  const [openQuestionId, setOpenQuestionId] = useState<string | null>(
    () => grade.detailedScorecard[0]?.questionId ?? null,
  )

  return (
    <div className="space-y-3">
      <section className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_10px_28px_rgba(23,48,96,0.06)]">
        <div className="bg-[linear-gradient(118deg,#0F2854_0%,#1C4D8D_58%,#4988C4_100%)] px-5 py-5 text-white">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 text-xs font-bold tracking-[0.08em] text-[#BDE8F5] uppercase">
                <Trophy className="size-3.5" />
                Bảng điểm kỳ thi chính thức · Phòng Lab
              </p>
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <span className="inline-flex rounded-full bg-white/15 px-2.5 py-1 text-xs font-bold">
                  {grade.courseCode || ticket.courseCode}
                </span>
                <span className="inline-flex rounded-full bg-[#BDE8F5]/20 px-2.5 py-1 text-xs font-semibold text-[#BDE8F5]">
                  {ticket.shiftName} · Phòng {ticket.roomLab} · Máy {ticket.seatNumber}
                </span>
              </div>
              <h2 className="mt-2 text-xl leading-snug font-bold">
                {ticket.courseName || ticket.sessionTitle}
              </h2>
              <p className="mt-2 text-sm text-[#d5e2f7]">
                Công bố lúc {formatDateTime(grade.publishedAt)} · Người công bố: {grade.publishedBy}
              </p>
            </div>

            <div className="flex flex-col items-end gap-2 self-start">
              <div className="flex items-center gap-3 rounded-2xl bg-white/10 px-4 py-3">
                <div>
                  <p className="text-xs font-semibold text-[#BDE8F5]">Điểm chính thức</p>
                  <p className="text-4xl leading-none font-black tracking-[-0.04em]">
                    {grade.totalScore === null ? '—' : grade.totalScore.toFixed(2)}
                    <span className="ml-1 text-sm font-semibold text-[#BDE8F5]">/10</span>
                  </p>
                </div>
                <span
                  className={cn(
                    'rounded-full px-3 py-1 text-xs font-bold',
                    grade.studentAcknowledgementStatus === 'ACKNOWLEDGED'
                      ? 'bg-[#edfbf6] text-[#0f8f6b]'
                      : 'bg-[#fff8eb] text-[#9a6414]',
                  )}
                >
                  {grade.studentAcknowledgementStatus === 'ACKNOWLEDGED'
                    ? 'Đã xác nhận'
                    : 'Chờ xác nhận'}
                </span>
              </div>

              {grade.studentAcknowledgementStatus !== 'ACKNOWLEDGED' && (
                <button
                  type="button"
                  disabled={busy}
                  onClick={onAcknowledge}
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-white px-3.5 py-2 text-xs font-bold text-[#0F2854] shadow-sm transition hover:bg-[#F4FBFD] disabled:opacity-60"
                >
                  <CheckCircle2 className="size-3.5 text-[#15866a]" />
                  Xác nhận đồng ý điểm thi
                </button>
              )}
            </div>
          </div>
        </div>
      </section>

      <div className="space-y-2">
        {grade.detailedScorecard.length === 0 ? (
          <div className="rounded-2xl border border-[#4988C4]/45 bg-white p-6 text-center text-sm text-[#5b6b84]">
            Chưa có chi tiết từng câu hỏi cho ca thi này.
          </div>
        ) : (
          grade.detailedScorecard.map((question, idx) => {
            const isOpen = openQuestionId === question.questionId
            return (
              <article
                key={`${question.questionId}-${idx}`}
                className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white"
              >
                <button
                  type="button"
                  onClick={() => setOpenQuestionId(isOpen ? null : question.questionId)}
                  className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-[#4988C4]/14"
                  aria-expanded={isOpen}
                >
                  <span className="min-w-0">
                    <span className="text-xs font-bold text-[#1C4D8D]">
                      Câu {idx + 1} · {question.questionTitle || 'Câu hỏi vấn đáp'}
                    </span>
                    <span
                      className={cn(
                        'mt-1 block text-sm leading-relaxed font-semibold whitespace-pre-line text-[#0F2854]',
                        !isOpen && 'line-clamp-2',
                      )}
                    >
                      {question.questionContent || question.questionTitle || `Câu hỏi #${idx + 1}`}
                    </span>
                  </span>
                  <span className="flex shrink-0 items-center gap-2">
                    <span className="rounded-full bg-[#4988C4]/16 px-2.5 py-1 text-sm font-bold text-[#1C4D8D]">
                      {question.score.toFixed(1)}/10
                    </span>
                    <ChevronDown
                      className={cn('size-4 text-[#74839a] transition', isOpen && 'rotate-180')}
                    />
                  </span>
                </button>

                {isOpen && (
                  <div className="space-y-3 border-t border-[#4988C4]/45 px-4 py-4">
                    {question.questionImageUrl?.trim() ? (
                      <div className="overflow-hidden rounded-xl border border-[#4988C4]/45 bg-white p-3">
                        <QuestionImage
                          imageUrl={question.questionImageUrl}
                          imageAlt={question.questionImageDescription}
                          framed={false}
                          showCaption={false}
                          className="w-full overflow-hidden rounded-xl"
                        />
                      </div>
                    ) : null}

                    <div className="rounded-xl bg-[#4988C4]/16 p-3">
                      <p className="text-xs font-bold text-[#1C4D8D]">
                        Bản bóc băng câu trả lời của bạn
                      </p>
                      <p className="mt-1.5 text-sm leading-relaxed whitespace-pre-line text-[#0F2854]">
                        {question.studentTranscript?.trim() ||
                          'Không ghi nhận nội dung trả lời cho câu hỏi này.'}
                      </p>
                    </div>

                    <div className="rounded-xl border border-[#d9f3ea] bg-[#f4fbf8] p-3">
                      <p className="flex items-center gap-1.5 text-xs font-bold text-[#16886c]">
                        <MessageSquareText className="size-3.5" />
                        Nhận xét đánh giá
                      </p>
                      <p className="mt-1.5 text-sm leading-relaxed whitespace-pre-line text-[#3f5c52]">
                        {question.strengths?.trim() || 'Đã chấm điểm theo tiêu chí Rubric.'}
                      </p>
                      {question.lecturerOverrideReason?.trim() && (
                        <p className="mt-2 rounded-lg bg-white/80 px-2.5 py-1.5 text-xs font-semibold text-[#1C4D8D]">
                          Ghi chú hậu kiểm của Giảng viên: {question.lecturerOverrideReason}
                        </p>
                      )}
                    </div>

                    {question.criteriaScores.length > 0 && (
                      <div className="space-y-2">
                        <p className="text-xs font-bold tracking-[0.04em] text-[#1C4D8D] uppercase">
                          Chi tiết theo tiêu chí Rubric
                        </p>
                        {question.criteriaScores.map((criterion, cIdx) => {
                          const ratio =
                            criterion.maxScore > 0
                              ? Math.min(
                                  100,
                                  Math.max(0, (criterion.score / criterion.maxScore) * 100),
                                )
                              : 0
                          return (
                            <div
                              key={`${criterion.criterionName}-${cIdx}`}
                              className="rounded-xl border border-[#e6edf5] px-3 py-2.5"
                            >
                              <div className="flex items-start justify-between gap-3">
                                <p className="text-sm font-bold text-[#25364e]">
                                  {criterion.criterionName}
                                </p>
                                <p className="shrink-0 text-sm font-bold text-[#1C4D8D]">
                                  {criterion.score.toFixed(1)}/{criterion.maxScore.toFixed(1)}
                                </p>
                              </div>
                              <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[#eef2f7]">
                                <div
                                  className="h-full rounded-full bg-[#4f7df0]"
                                  style={{ width: `${ratio}%` }}
                                />
                              </div>
                              {criterion.comment && (
                                <p className="mt-2 text-sm leading-relaxed text-[#5b6b84]">
                                  {criterion.comment}
                                </p>
                              )}
                            </div>
                          )
                        })}
                      </div>
                    )}
                  </div>
                )}
              </article>
            )
          })
        )}
      </div>

      {grade.internalAppeal.canAppeal && (
        <section className="rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] p-4">
          <p className="text-sm font-bold text-[#0F2854]">
            Phúc khảo nội bộ (Thời hạn {grade.internalAppeal.appealDeadlineDays} ngày)
          </p>
          <p className="mt-1 text-xs text-[#5b6b84]">{grade.internalAppeal.instruction}</p>
          {appeal ? (
            <div className="mt-3 rounded-xl border border-[#4988C4]/35 bg-white p-3 text-xs text-[#0F2854]">
              Trạng thái đơn phúc khảo: <strong className="text-[#1C4D8D]">{appeal.status}</strong>{' '}
              · Lý do: {appeal.reason}
            </div>
          ) : (
            <div className="mt-3 space-y-2.5">
              <textarea
                value={appealReason}
                onChange={(event) => onChangeAppealReason(event.target.value)}
                placeholder="Nhập lý do đề nghị phúc khảo chi tiết (tối thiểu 10 ký tự)..."
                className="min-h-20 w-full rounded-xl border border-[#4988C4]/45 bg-white p-3 text-sm text-[#0F2854] outline-none focus:border-[#1C4D8D]"
              />
              <button
                type="button"
                disabled={busy}
                onClick={onCreateAppeal}
                className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#1C4D8D] px-4 py-2 text-xs font-bold text-white transition hover:bg-[#0F2854] disabled:opacity-60"
              >
                Gửi đơn phúc khảo tới Trưởng Bộ môn
              </button>
            </div>
          )}
        </section>
      )}
    </div>
  )
}

function LiveExamHistoryPage() {
  const { user } = useAuth()
  const userId = user?.id
  const [searchParams, setSearchParams] = useSearchParams()

  const [practiceItems, setPracticeItems] = useState<PracticeHistoryItem[]>([])
  const [mockItems, setMockItems] = useState<MockExamHistoryItem[]>([])
  const [officialItems, setOfficialItems] = useState<LiveOfficialExamSummary[]>([])
  const [appeals, setAppeals] = useState<Appeal[]>([])

  const [categoryFilter, setCategoryFilter] = useState<HistoryCategory>(() => {
    const paramType = searchParams.get('type')
    return paramType === 'practice' || paramType === 'mock' || paramType === 'official'
      ? paramType
      : 'all'
  })
  const [courseFilter, setCourseFilter] = useState<string>('ALL')
  const [searchQuery, setSearchQuery] = useState<string>('')

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [activeModal, setActiveModal] = useState<{
    category: 'practice' | 'mock' | 'official'
    id: string
  } | null>(null)
  const [modalLoading, setModalLoading] = useState(false)
  const [modalError, setModalError] = useState<string | null>(null)
  const [practiceDetails, setPracticeDetails] = useState<Record<string, PracticeSessionDetail>>({})
  const [mockDetails, setMockDetails] = useState<Record<string, MockExamScorecardData>>({})
  const [officialGrades, setOfficialGrades] = useState<Record<string, StudentOfficialExamGrade>>({})
  const [appealReason, setAppealReason] = useState('')
  const [actionBusy, setActionBusy] = useState(false)

  const loadAllHistories = useCallback(async () => {
    setLoading(true)
    setError(null)

    const [practiceRes, mockRes, dashboardRes, appealsRes] = await Promise.allSettled([
      practiceService.getStudentHistory(userId),
      mockExamService.getHistory({ pageIndex: 1, pageSize: 50 }),
      apiClient.get<unknown>('/student/dashboard'),
      appealsService.list(),
    ])

    if (practiceRes.status === 'fulfilled') {
      setPracticeItems(practiceRes.value)
    } else {
      setPracticeItems([])
    }

    if (mockRes.status === 'fulfilled') {
      setMockItems(mockRes.value.items)
    } else {
      setMockItems([])
    }

    if (dashboardRes.status === 'fulfilled') {
      const dashboard = unwrapDashboardPayload<Record<string, unknown>>(dashboardRes.value.data)
      const raw = dashboard.officialExamTickets ?? dashboard.OfficialExamTickets
      const list = Array.isArray(raw) ? raw : []
      setOfficialItems(
        list
          .map((value) => {
            const record = value as Record<string, unknown>
            const read = (key: string) =>
              record[key] ?? record[key.charAt(0).toUpperCase() + key.slice(1)]
            return {
              ticketId: String(read('ticketId') ?? ''),
              shiftId: String(read('shiftId') ?? ''),
              courseCode: String(read('courseCode') ?? ''),
              courseName: String(read('courseName') ?? ''),
              sessionTitle: String(read('sessionTitle') ?? ''),
              shiftName: String(read('shiftName') ?? ''),
              roomLab: String(read('roomLab') ?? ''),
              examDate: String(read('examDate') ?? ''),
              startTime: String(read('startTime') ?? ''),
              endTime: String(read('endTime') ?? ''),
              seatNumber: Number(read('seatNumber') ?? 0),
              status: String(read('status') ?? ''),
              finalScore:
                typeof read('finalScore') === 'number' ? Number(read('finalScore')) : null,
              isLocked: Boolean(read('isLocked')),
              studentAcknowledgementStatus: String(
                read('studentAcknowledgementStatus') ?? 'PENDING',
              ),
            }
          })
          .filter((item) => item.ticketId),
      )
    } else {
      setOfficialItems([])
    }

    if (appealsRes.status === 'fulfilled') {
      setAppeals(appealsRes.value)
    } else {
      setAppeals([])
    }

    if (
      practiceRes.status === 'rejected' &&
      mockRes.status === 'rejected' &&
      dashboardRes.status === 'rejected'
    ) {
      setError(extractErrorMessage(practiceRes.reason))
    }

    setLoading(false)
  }, [userId])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadAllHistories()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [loadAllHistories])

  const openDetailModal = useCallback(
    async (category: 'practice' | 'mock' | 'official', id: string) => {
      setActiveModal({ category, id })
      setModalError(null)

      if (category === 'practice') {
        if (practiceDetails[id]) return
        setModalLoading(true)
        try {
          const detail = await practiceService.getSession(id)
          setPracticeDetails((prev) => ({ ...prev, [id]: detail }))
        } catch (err) {
          setModalError(
            extractErrorMessage(err, 'Không thể tải chi tiết câu hỏi và nhận xét phiên luyện tập.'),
          )
        } finally {
          setModalLoading(false)
        }
        return
      }

      if (category === 'mock') {
        if (mockDetails[id]) return
        setModalLoading(true)
        try {
          const detail = await mockExamService.getHistoryDetail(id)
          setMockDetails((prev) => ({ ...prev, [id]: detail }))
        } catch (err) {
          setModalError(
            extractErrorMessage(err, 'Không thể tải bảng điểm chi tiết ca thi thử này.'),
          )
        } finally {
          setModalLoading(false)
        }
        return
      }

      if (category === 'official') {
        if (officialGrades[id]) return
        setModalLoading(true)
        try {
          const grade = await officialExamService.getStudentGrade(id, userId)
          setOfficialGrades((prev) => ({ ...prev, [id]: grade }))
        } catch (err) {
          setModalError(
            extractErrorMessage(
              err,
              'Điểm ca thi chính thức chưa được công bố hoặc chưa có dữ liệu hậu kiểm.',
            ),
          )
        } finally {
          setModalLoading(false)
        }
      }
    },
    [mockDetails, officialGrades, practiceDetails, userId],
  )

  useEffect(() => {
    const paramType = searchParams.get('type')
    const paramId = searchParams.get('id')
    if (
      (paramType === 'practice' || paramType === 'mock' || paramType === 'official') &&
      paramId
    ) {
      const timer = window.setTimeout(() => {
        setCategoryFilter(paramType)
        void openDetailModal(paramType, paramId)
      }, 0)
      return () => window.clearTimeout(timer)
    }
  }, [openDetailModal, searchParams])

  const closeDetailModal = () => {
    setActiveModal(null)
    setModalError(null)
    if (searchParams.has('id') || searchParams.has('type')) {
      setSearchParams({}, { replace: true })
    }
  }

  const acknowledgeOfficialGrade = async (ticketId: string) => {
    setActionBusy(true)
    setModalError(null)
    try {
      await officialExamService.acknowledgeGrade(ticketId, user?.id)
      setOfficialItems((current) =>
        current.map((item) =>
          item.ticketId === ticketId
            ? { ...item, studentAcknowledgementStatus: 'ACKNOWLEDGED' }
            : item,
        ),
      )
      setOfficialGrades((current) =>
        current[ticketId]
          ? {
              ...current,
              [ticketId]: {
                ...current[ticketId],
                studentAcknowledgementStatus: 'ACKNOWLEDGED',
              },
            }
          : current,
      )
      setNotice('Đã xác nhận đồng ý kết quả điểm thi chính thức.')
    } catch (requestError) {
      setModalError(extractErrorMessage(requestError))
    } finally {
      setActionBusy(false)
    }
  }

  const createOfficialAppeal = async (ticketId: string) => {
    if (appealReason.trim().length < 10) {
      setModalError('Lý do phúc khảo cần có ít nhất 10 ký tự.')
      return
    }
    setActionBusy(true)
    setModalError(null)
    try {
      const created = await appealsService.create({ ticketId, reason: appealReason.trim() })
      setAppeals((current) => [created, ...current])
      setAppealReason('')
      setNotice('Đã gửi đơn phúc khảo. Trưởng Bộ môn sẽ tiếp nhận và phân công chấm lại.')
    } catch (requestError) {
      setModalError(extractErrorMessage(requestError))
    } finally {
      setActionBusy(false)
    }
  }

  const unifiedRows = useMemo<UnifiedHistoryRow[]>(() => {
    const rows: UnifiedHistoryRow[] = []

    practiceItems.forEach((item) => {
      const isFull = item.practiceMode === 'full_session'
      const inProgress = item.status === 'in_progress'
      rows.push({
        uid: `practice-${item.sessionId}`,
        rawId: item.sessionId,
        category: 'practice',
        badgeLabel: isFull ? 'Luyện trọn gói' : 'Luyện từng câu',
        courseCode: item.courseCode,
        courseName: item.courseName,
        title: item.courseName,
        subtitle: `Đã trả lời ${item.answeredQuestions}/${item.totalQuestions} câu chính`,
        timestamp: item.endedAt ?? item.startedAt,
        statusRaw: item.status,
        statusLabel:
          item.status === 'completed'
            ? 'Hoàn tất'
            : item.status === 'in_progress'
              ? 'Đang diễn ra'
              : 'Đã kết thúc',
        statusTone:
          item.status === 'completed' ? 'emerald' : item.status === 'in_progress' ? 'amber' : 'slate',
        score: item.averageScore,
        canContinue: inProgress,
        continueUrl: inProgress
          ? `/practice/session?session=${encodeURIComponent(item.sessionId)}`
          : null,
      })
    })

    mockItems.forEach((item) => {
      const normalizedStatus = item.status.toLowerCase()
      const inProgress = normalizedStatus === 'in_progress'
      rows.push({
        uid: `mock-${item.sessionId}`,
        rawId: item.sessionId,
        category: 'mock',
        badgeLabel: 'Thi thử tính giờ',
        courseCode: item.courseCode,
        courseName: item.courseName,
        title: item.courseName,
        subtitle: `Đã trả lời ${item.answeredQuestions}/${item.totalQuestions} câu · Thời lượng ${item.durationMinutes} phút`,
        timestamp: item.startedAt,
        statusRaw: item.status,
        statusLabel: inProgress ? 'Đang làm bài' : 'Đã nộp & Chấm điểm',
        statusTone: inProgress ? 'amber' : 'blue',
        score: item.totalScore,
        canContinue: inProgress,
        continueUrl: inProgress ? `/mock-exam/session?session=${encodeURIComponent(item.sessionId)}` : null,
      })
    })

    officialItems.forEach((item) => {
      const canViewGrade =
        item.isLocked || item.status === 'PUBLISHED' || item.status === 'LOCKED' || item.finalScore !== null
      rows.push({
        uid: `official-${item.ticketId}`,
        rawId: item.ticketId,
        category: 'official',
        badgeLabel: 'Thi chính thức',
        courseCode: item.courseCode,
        courseName: item.courseName || item.sessionTitle,
        title: item.courseName || item.sessionTitle,
        subtitle: `${item.shiftName} · Phòng ${item.roomLab} · Máy số ${item.seatNumber}`,
        timestamp: item.startTime || item.examDate,
        statusRaw: item.status,
        statusLabel: item.isLocked
          ? 'Đã công bố & Khóa điểm'
          : canViewGrade
            ? 'Đã công bố điểm'
            : item.status,
        statusTone: canViewGrade ? 'emerald' : 'amber',
        score: item.finalScore,
        canContinue: false,
        continueUrl: null,
      })
    })

    return rows.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
  }, [mockItems, officialItems, practiceItems])

  const courseOptions = useMemo(() => {
    const map = new Map<string, string>()
    unifiedRows.forEach((row) => {
      if (row.courseCode && !map.has(row.courseCode)) {
        map.set(row.courseCode, row.courseName)
      }
    })
    return Array.from(map.entries()).map(([code, name]) => ({ code, name }))
  }, [unifiedRows])

  const filteredRows = useMemo(() => {
    return unifiedRows.filter((row) => {
      if (categoryFilter !== 'all' && row.category !== categoryFilter) return false
      if (courseFilter !== 'ALL' && row.courseCode !== courseFilter) return false
      if (searchQuery.trim()) {
        const q = searchQuery.trim().toLowerCase()
        const hay = `${row.courseCode} ${row.courseName} ${row.title} ${row.subtitle} ${row.badgeLabel}`.toLowerCase()
        if (!hay.includes(q)) return false
      }
      return true
    })
  }, [categoryFilter, courseFilter, searchQuery, unifiedRows])

  const stats = useMemo(() => {
    const practiceScored = practiceItems
      .map((i) => i.averageScore)
      .filter((s): s is number => s !== null && Number.isFinite(s))
    const mockScored = mockItems
      .map((i) => i.totalScore)
      .filter((s): s is number => s !== null && Number.isFinite(s))
    const officialScored = officialItems
      .map((i) => i.finalScore)
      .filter((s): s is number => s !== null && Number.isFinite(s))

    return {
      practiceCount: practiceItems.length,
      practiceAvg: averageScore(practiceScored),
      mockCount: mockItems.length,
      mockAvg: averageScore(mockScored),
      officialCount: officialItems.length,
      officialAvg: averageScore(officialScored),
    }
  }, [mockItems, officialItems, practiceItems])

  return (
    <div lang="vi" className={cn(ds.page, 'pb-8')}>
      <section className={ds.banner}>
        <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(255,255,255,0.3)_0.8px,transparent_0.8px)] [background-size:22px_22px] opacity-35" />
        <div className="relative z-10 flex flex-col justify-between gap-4 p-6 sm:flex-row sm:items-end sm:p-8">
          <div className="max-w-2xl">
            <div className="inline-flex items-center gap-2 rounded-full border border-white/25 bg-white/14 px-3 py-1 text-xs font-bold tracking-[0.06em] text-[#BDE8F5] uppercase backdrop-blur-sm">
              <BarChart3 className="size-3.5" />
              Hồ sơ kết quả & bảng điểm
            </div>
            <h1 className="mt-3 text-2xl font-bold tracking-[-0.03em] text-white sm:text-3xl">
              Lịch sử Luyện tập, Thi thử & Kết quả Thi chính thức
            </h1>
            <p className="mt-2 text-sm leading-relaxed text-white/85">
              Tra cứu toàn bộ các phiên luyện tập, ca thi thử tính giờ và kết quả thi chính thức
              phòng Lab kèm nội dung từng câu hỏi, câu trả lời đã ghi nhận, điểm Rubric và nhận xét
              đánh giá chi tiết.
            </p>
          </div>

          <button
            type="button"
            onClick={() => void loadAllHistories()}
            disabled={loading}
            className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 self-start rounded-xl bg-white px-4 py-2.5 text-xs font-bold text-[#0F2854] shadow-sm transition hover:-translate-y-0.5 hover:bg-[#F4FBFD] disabled:opacity-60 sm:self-auto"
          >
            <RefreshCw className={cn('size-3.5 text-[#1C4D8D]', loading && 'animate-spin')} />
            {loading ? 'Đang đồng bộ...' : 'Làm mới dữ liệu'}
          </button>
        </div>
      </section>

      {(error || notice) && (
        <div
          className={cn(
            'flex items-center justify-between gap-3 rounded-2xl border px-4 py-3 text-sm',
            error
              ? 'border-[#f3c8cf] bg-[#fff5f6] text-[#b33d52]'
              : 'border-[#bcebdc] bg-[#effbf6] text-[#16785f]',
          )}
        >
          <span className="flex items-center gap-2 font-medium">
            <AlertCircle className="size-4 shrink-0" />
            {error ?? notice}
          </span>
          <button
            type="button"
            onClick={() => {
              setError(null)
              setNotice(null)
            }}
            className="rounded-lg p-1 hover:bg-black/5"
            aria-label="Đóng thông báo"
          >
            <X className="size-4" />
          </button>
        </div>
      )}

      <section aria-label="Thống kê tổng hợp" className="grid gap-3 md:grid-cols-3">
        <article className="rounded-2xl border border-[#b7e4cf] bg-[#f3fbf7] p-4 shadow-[0_7px_24px_rgba(15,40,84,0.04)] sm:p-5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <span className="inline-flex rounded-full bg-white px-2.5 py-0.5 text-[11px] font-bold text-[#16836a] shadow-2xs">
                Luyện tập tự do
              </span>
              <div className="mt-2.5 flex items-baseline gap-2">
                <strong className="text-3xl font-black text-[#0F2854]">
                  {stats.practiceCount}
                </strong>
                <span className="text-xs font-bold text-[#16836a]">phiên luyện tập</span>
              </div>
              <p className="mt-2 text-xs font-semibold text-[#0F2854]/80">
                Điểm trung bình:{' '}
                <strong className="text-[#16836a]">
                  {stats.practiceAvg === null ? 'Chưa chấm' : `${stats.practiceAvg.toFixed(2)}/10`}
                </strong>
              </p>
            </div>
            <div className="flex size-10 items-center justify-center rounded-xl bg-[#16836a] text-white shadow-xs">
              <Mic2 className="size-5" />
            </div>
          </div>
        </article>

        <article className="rounded-2xl border border-[#9ec4e8] bg-[#f3f9fd] p-4 shadow-[0_7px_24px_rgba(15,40,84,0.04)] sm:p-5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <span className="inline-flex rounded-full bg-white px-2.5 py-0.5 text-[11px] font-bold text-[#1C4D8D] shadow-2xs">
                Thi thử tính giờ
              </span>
              <div className="mt-2.5 flex items-baseline gap-2">
                <strong className="text-3xl font-black text-[#0F2854]">{stats.mockCount}</strong>
                <span className="text-xs font-bold text-[#1C4D8D]">ca thi thử</span>
              </div>
              <p className="mt-2 text-xs font-semibold text-[#0F2854]/80">
                Điểm trung bình:{' '}
                <strong className="text-[#1C4D8D]">
                  {stats.mockAvg === null ? 'Chưa chấm' : `${stats.mockAvg.toFixed(2)}/10`}
                </strong>
              </p>
            </div>
            <div className="flex size-10 items-center justify-center rounded-xl bg-[#1C4D8D] text-white shadow-xs">
              <Clock3 className="size-5" />
            </div>
          </div>
        </article>

        <article className="rounded-2xl border border-[#d4c4f5] bg-[#f7f3fd] p-4 shadow-[0_7px_24px_rgba(15,40,84,0.04)] sm:p-5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <span className="inline-flex rounded-full bg-white px-2.5 py-0.5 text-[11px] font-bold text-[#6d4cc4] shadow-2xs">
                Thi chính thức
              </span>
              <div className="mt-2.5 flex items-baseline gap-2">
                <strong className="text-3xl font-black text-[#0F2854]">
                  {stats.officialCount}
                </strong>
                <span className="text-xs font-bold text-[#6d4cc4]">vé thi phòng Lab</span>
              </div>
              <p className="mt-2 text-xs font-semibold text-[#0F2854]/80">
                Điểm công bố TB:{' '}
                <strong className="text-[#6d4cc4]">
                  {stats.officialAvg === null ? 'Chưa công bố' : `${stats.officialAvg.toFixed(2)}/10`}
                </strong>
              </p>
            </div>
            <div className="flex size-10 items-center justify-center rounded-xl bg-[#6d4cc4] text-white shadow-xs">
              <ShieldCheck className="size-5" />
            </div>
          </div>
        </article>
      </section>

      <section className={cn(ds.section, 'overflow-hidden')}>
        <div
          className={cn(
            ds.sectionHeader,
            'flex flex-col justify-between gap-3 lg:flex-row lg:items-center',
          )}
        >
          <div>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <History className="size-4 text-[#1C4D8D]" />
              </span>
              <h2 className={ds.sectionTitle}>Danh sách phiên làm bài</h2>
              <span className="rounded-full bg-white px-2.5 py-0.5 text-xs font-bold text-[#1C4D8D] shadow-2xs">
                {filteredRows.length} bản ghi
              </span>
            </div>
            <p className={ds.sectionHint}>
              Bấm vào từng bản ghi để mở bảng điểm chi tiết, nội dung từng câu hỏi, bài trả lời và
              nhận xét đánh giá
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <div className="relative">
              <Filter className="pointer-events-none absolute top-1/2 left-3 size-3.5 -translate-y-1/2 text-[#1C4D8D]" />
              <select
                value={courseFilter}
                onChange={(event) => setCourseFilter(event.target.value)}
                aria-label="Lọc theo môn học"
                className="min-h-9 rounded-xl border border-[#4988C4] bg-white py-1.5 pr-8 pl-8 text-xs font-bold text-[#0F2854] outline-none focus:border-[#1C4D8D]"
              >
                <option value="ALL">Tất cả học phần</option>
                {courseOptions.map((c) => (
                  <option key={c.code} value={c.code}>
                    {c.code} — {c.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="relative">
              <Search className="pointer-events-none absolute top-1/2 left-3 size-3.5 -translate-y-1/2 text-[#5b6b84]" />
              <input
                type="search"
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="Tìm môn học, ca thi..."
                className="min-h-9 w-48 rounded-xl border border-[#4988C4] bg-white py-1.5 pr-3 pl-8 text-xs font-medium text-[#0F2854] outline-none focus:border-[#1C4D8D] sm:w-56"
              />
            </div>
          </div>
        </div>

        <div className="border-b border-[#4988C4]/35 bg-[#F4FBFD] px-4 py-3 sm:px-6">
          <div className="flex flex-wrap items-center gap-1.5">
            {(
              [
                { id: 'all', label: 'Tất cả', count: unifiedRows.length },
                { id: 'practice', label: 'Luyện tập', count: practiceItems.length },
                { id: 'mock', label: 'Thi thử', count: mockItems.length },
                { id: 'official', label: 'Thi chính thức', count: officialItems.length },
              ] as const
            ).map((tab) => {
              const active = categoryFilter === tab.id
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setCategoryFilter(tab.id)}
                  className={cn(
                    'inline-flex min-h-9 items-center gap-2 rounded-xl px-3.5 py-1.5 text-xs font-bold transition',
                    active
                      ? 'bg-[#1C4D8D] text-white shadow-xs'
                      : 'bg-white text-[#0F2854] hover:bg-[#4988C4]/18',
                  )}
                >
                  <span>{tab.label}</span>
                  <span
                    className={cn(
                      'rounded-full px-2 py-0.5 text-[11px]',
                      active ? 'bg-white/20 text-white' : 'bg-[#4988C4]/16 text-[#1C4D8D]',
                    )}
                  >
                    {tab.count}
                  </span>
                </button>
              )
            })}
          </div>
        </div>

        <div className="divide-y divide-[#4988C4]/30">
          {loading ? (
            <div className="flex items-center justify-center gap-2.5 py-16 text-sm font-semibold text-[#1C4D8D]">
              <LoaderCircle className="size-5 animate-spin" />
              Đang tải toàn bộ lịch sử luyện tập, thi thử và thi chính thức...
            </div>
          ) : filteredRows.length === 0 ? (
            <div className="p-10 text-center">
              <History className="mx-auto size-9 text-[#4988C4]" />
              <p className="mt-3 text-sm font-bold text-[#0F2854]">
                Chưa có bản ghi nào trong danh mục đã chọn
              </p>
              <p className="mt-1 text-xs text-[#5b6b84]">
                Hãy bắt đầu một phiên Luyện tập hoặc Thi thử để xem kết quả và nhận xét chi tiết
                tại đây.
              </p>
              <div className="mt-4 flex flex-wrap justify-center gap-2">
                <Link
                  to="/practice"
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#1C4D8D] px-4 py-2 text-xs font-bold text-white transition hover:bg-[#0F2854]"
                >
                  <Mic2 className="size-3.5" />
                  Bắt đầu Luyện tập
                </Link>
                <Link
                  to="/mock-exam"
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-[#F4FBFD] px-4 py-2 text-xs font-bold text-[#0F2854] transition hover:bg-[#4988C4]/18"
                >
                  <BookOpenCheck className="size-3.5 text-[#1C4D8D]" />
                  Vào phòng Thi thử
                </Link>
              </div>
            </div>
          ) : (
            filteredRows.map((row) => (
              <article
                key={row.uid}
                className="flex flex-col justify-between gap-4 p-4 transition hover:bg-[#F4FBFD] sm:flex-row sm:items-center sm:px-6"
              >
                <div className="flex min-w-0 flex-1 items-start gap-3.5">
                  <span
                    className={cn(
                      'mt-0.5 inline-flex h-10 min-w-[76px] shrink-0 items-center justify-center rounded-xl px-2.5 text-xs font-black tracking-[0.02em]',
                      courseCodeTone(row.courseCode),
                    )}
                  >
                    {row.courseCode || 'MÔN'}
                  </span>

                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span
                        className={cn(
                          'inline-flex items-center gap-1 rounded-md px-2.5 py-0.5 text-xs font-bold',
                          row.category === 'practice' && 'bg-[#e8f8f3] text-[#16836a]',
                          row.category === 'mock' && 'bg-[#0F2854] text-[#BDE8F5]',
                          row.category === 'official' && 'bg-[#f3eefe] text-[#6d4cc4]',
                        )}
                      >
                        {row.badgeLabel}
                      </span>
                      <span
                        className={cn(
                          'inline-flex rounded-full px-2.5 py-0.5 text-xs font-bold',
                          row.statusTone === 'emerald' && 'bg-[#edfbf6] text-[#0f8f6b]',
                          row.statusTone === 'blue' && 'bg-[#4988C4]/16 text-[#1C4D8D]',
                          row.statusTone === 'amber' && 'bg-[#fff8eb] text-[#9a6414]',
                          row.statusTone === 'slate' && 'bg-[#f1f5f9] text-[#64748b]',
                        )}
                      >
                        {row.statusLabel}
                      </span>
                      <span className="inline-flex items-center gap-1 text-xs font-medium text-[#5b6b84]">
                        <CalendarDays className="size-3.5 text-[#4988C4]" />
                        {formatDateTime(row.timestamp)}
                      </span>
                    </div>

                    <h3 className="mt-1.5 text-sm font-bold text-[#0F2854] sm:text-base">
                      {row.title}
                    </h3>
                    <p className="mt-0.5 text-xs text-[#5b6b84]">{row.subtitle}</p>
                  </div>
                </div>

                <div className="flex flex-wrap items-center justify-between gap-3 sm:justify-end">
                  <div
                    className={cn(
                      'rounded-xl px-3.5 py-2 text-right',
                      row.score === null
                        ? 'bg-[#fff8eb] text-[#9a6414]'
                        : row.score >= 8
                          ? 'bg-[#e8f8f3] text-[#16836a]'
                          : row.score >= 5
                            ? 'bg-[#4988C4]/14 text-[#0F2854]'
                            : 'bg-[#fdecef] text-[#c2415b]',
                    )}
                  >
                    <p className="text-[11px] font-bold tracking-[0.04em] uppercase opacity-75">
                      Điểm tổng kết
                    </p>
                    <p className="mt-0.5 text-base font-black">
                      {row.score === null ? '—' : `${row.score.toFixed(2)} / 10`}
                    </p>
                  </div>

                  <div className="flex items-center gap-2">
                    {row.canContinue && row.continueUrl && (
                      <Link
                        to={row.continueUrl}
                        className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 py-2 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#4988C4]/16"
                      >
                        Tiếp tục làm
                        <ArrowRight className="size-3.5" />
                      </Link>
                    )}

                    <button
                      type="button"
                      onClick={() => void openDetailModal(row.category, row.rawId)}
                      className="inline-flex min-h-9 items-center gap-1.5 rounded-xl bg-[#1C4D8D] px-3.5 py-2 text-xs font-bold text-white shadow-sm transition hover:bg-[#0F2854]"
                    >
                      <FileCheck2 className="size-3.5" />
                      Xem câu hỏi & nhận xét
                    </button>
                  </div>
                </div>
              </article>
            ))
          )}
        </div>
      </section>

      {activeModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#0F2854]/60 p-3 backdrop-blur-xs sm:p-5"
          role="dialog"
          aria-modal="true"
          aria-label="Chi tiết bảng điểm và nhận xét"
        >
          <div className="flex max-h-[90vh] w-full max-w-4xl flex-col overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-[#F4FBFD] shadow-2xl">
            <div className="flex items-center justify-between gap-3 border-b border-[#4988C4]/45 bg-white px-5 py-3.5">
              <div className="flex items-center gap-2">
                <Award className="size-5 text-[#1C4D8D]" />
                <h2 className="text-base font-bold text-[#0F2854]">
                  {activeModal.category === 'practice' &&
                    'Chi tiết Phiên luyện tập · Câu hỏi, Trả lời & Nhận xét'}
                  {activeModal.category === 'mock' &&
                    'Chi tiết Ca thi thử · Bảng điểm & Nhận xét theo CLO'}
                  {activeModal.category === 'official' &&
                    'Chi tiết Kết quả Thi chính thức · Bảng điểm & Phúc khảo'}
                </h2>
              </div>
              <button
                type="button"
                onClick={closeDetailModal}
                className="inline-flex size-8 items-center justify-center rounded-lg text-[#5b6b84] transition hover:bg-[#4988C4]/16 hover:text-[#0F2854]"
                aria-label="Đóng cửa sổ chi tiết"
              >
                <X className="size-4" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-4 sm:p-5">
              {modalLoading ? (
                <div className="flex min-h-[260px] flex-col items-center justify-center gap-2 text-sm font-semibold text-[#1C4D8D]">
                  <LoaderCircle className="size-6 animate-spin" />
                  Đang tải nội dung câu hỏi, câu trả lời và nhận xét...
                </div>
              ) : modalError ? (
                <div className="rounded-2xl border border-[#f3c8cf] bg-[#fff5f6] p-5 text-sm text-[#b33d52]">
                  <p className="font-bold">Chưa thể hiển thị bảng điểm chi tiết</p>
                  <p className="mt-1">{modalError}</p>
                </div>
              ) : activeModal.category === 'practice' && practiceDetails[activeModal.id] ? (
                <PracticeSessionScorecard session={practiceDetails[activeModal.id]} />
              ) : activeModal.category === 'mock' && mockDetails[activeModal.id] ? (
                <MockExamScorecard scorecard={mockDetails[activeModal.id]} />
              ) : activeModal.category === 'official' && officialGrades[activeModal.id] ? (
                <OfficialExamScorecardView
                  ticket={
                    officialItems.find((item) => item.ticketId === activeModal.id) ?? {
                      ticketId: activeModal.id,
                      shiftId: '',
                      courseCode: officialGrades[activeModal.id].courseCode,
                      courseName: officialGrades[activeModal.id].courseCode,
                      sessionTitle: '',
                      shiftName: 'Ca thi chính thức',
                      roomLab: 'Lab',
                      examDate: officialGrades[activeModal.id].publishedAt,
                      startTime: officialGrades[activeModal.id].publishedAt,
                      endTime: officialGrades[activeModal.id].publishedAt,
                      seatNumber: 1,
                      status: officialGrades[activeModal.id].status,
                      finalScore: officialGrades[activeModal.id].totalScore,
                      isLocked: officialGrades[activeModal.id].isLocked,
                      studentAcknowledgementStatus:
                        officialGrades[activeModal.id].studentAcknowledgementStatus,
                    }
                  }
                  grade={officialGrades[activeModal.id]}
                  appeal={appeals.find((a) => a.ticketId === activeModal.id)}
                  appealReason={appealReason}
                  onChangeAppealReason={setAppealReason}
                  onAcknowledge={() => void acknowledgeOfficialGrade(activeModal.id)}
                  onCreateAppeal={() => void createOfficialAppeal(activeModal.id)}
                  busy={actionBusy}
                />
              ) : null}
            </div>

            <div className="flex items-center justify-end gap-2 border-t border-[#4988C4]/45 bg-white px-5 py-3">
              <button
                type="button"
                onClick={closeDetailModal}
                className="inline-flex min-h-9 items-center justify-center rounded-xl bg-[#1C4D8D] px-4 py-2 text-xs font-bold text-white transition hover:bg-[#0F2854]"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export { LiveExamHistoryPage as ExamHistoryPage, LiveExamHistoryPage as StudentResultsPage }
