import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  CircleDashed,
  ClipboardList,
  Clock3,
  Eye,
  LoaderCircle,
  Lock,
  Pencil,
  Save,
  Search,
  Timer,
  UsersRound,
  X,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { toast } from '@/components/common/toastStore'
import { lockBodyScroll } from '@/design/bodyScrollLock'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { InClassShiftPageBanner } from '@/pages/lecturer/InClassShiftPageBanner'
import { ds, overlay } from '@/design/system'
import { cn } from '@/lib/utils'
import { inClassShiftService } from '@/services/inClassShift.service'
import type { InClassShift, InClassSubmission } from '@/types/inClassShift.types'

type SubmissionFilter = 'ALL' | 'PENDING' | 'AI' | 'OVERRIDE' | 'LOCKED'

const SUBMISSION_TABS: Array<{
  value: SubmissionFilter
  label: string
  countIdleClass: string
  dotClass: string
}> = [
  {
    value: 'ALL',
    label: 'Tất cả',
    countIdleClass: 'bg-[#e8eef6] text-[#1C4D8D]',
    dotClass: 'bg-[#1C4D8D]',
  },
  {
    value: 'PENDING',
    label: 'Chưa nộp',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'AI',
    label: 'Đã chấm AI',
    countIdleClass: 'bg-[#e8f7f1] text-[#197158]',
    dotClass: 'bg-[#1a9a72]',
  },
  {
    value: 'OVERRIDE',
    label: 'Đã chỉnh',
    countIdleClass: 'bg-[#eef1f5] text-[#52647d]',
    dotClass: 'bg-[#7a8aa0]',
  },
  {
    value: 'LOCKED',
    label: 'Đã khóa',
    countIdleClass: 'bg-[#fceef4] text-[#963b66]',
    dotClass: 'bg-[#b04a78]',
  },
]

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  if (error instanceof Error && error.message) return error.message
  return 'Không thể tải bảng điểm.'
}

function isInProgress(status: InClassSubmission['status']) {
  return status === 'JOINED' || status === 'IN_PROGRESS'
}

function submissionBucket(item: InClassSubmission): SubmissionFilter | null {
  if (isInProgress(item.status)) return 'PENDING'
  if (item.status === 'FINALIZED') return 'LOCKED'
  if (item.overridden) return 'OVERRIDE'
  if (item.aiScore != null) return 'AI'
  return null
}

function shiftStatusLabel(status: InClassShift['status']) {
  if (status === 'SCHEDULED') return 'Chờ kích hoạt'
  if (status === 'ACTIVE') return 'Đang mở'
  if (status === 'FINALIZED') return 'Đã chốt điểm'
  return 'Đã kết thúc'
}

function formatStamp(value?: string | null) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function formatClock(value?: string | null) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
}

function shiftWindow(shift: InClassShift) {
  const start = shift.startTime ? new Date(shift.startTime) : null
  const end = shift.endTime ? new Date(shift.endTime) : null
  const validStart = start && !Number.isNaN(start.getTime()) ? start : null
  const validEnd = end && !Number.isNaN(end.getTime()) ? end : null
  if (!validStart) return 'Chưa có giờ'
  const date = validStart.toLocaleDateString('vi-VN')
  const startClock = formatClock(shift.startTime)
  if (!validEnd) return `${date} ${startClock}`
  const endClock = formatClock(shift.endTime)
  const sameDay = validEnd.toDateString() === validStart.toDateString()
  return sameDay
    ? `${date} ${startClock} – ${endClock}`
    : `${date} ${startClock} – ${validEnd.toLocaleDateString('vi-VN')} ${endClock}`
}

function formatExamDuration(minutes: number) {
  if (minutes <= 0) return '0 phút'
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  if (hours === 0) return `${rest} phút`
  if (rest === 0) return `${hours} giờ`
  return `${hours} giờ ${rest} phút`
}

const shiftFactClass =
  'inline-flex items-center gap-1.5 rounded-lg bg-[#D1EFF8] px-2.5 py-1.5 text-xs font-semibold text-[#1C4D8D]'

export function InClassShiftResultsPage() {
  const { shiftId = '' } = useParams()
  const [shift, setShift] = useState<InClassShift | null>(null)
  const [submissions, setSubmissions] = useState<InClassSubmission[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [filter, setFilter] = useState<SubmissionFilter>('ALL')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [examScore, setExamScore] = useState('')
  const [examScoreError, setExamScoreError] = useState('')
  const [examComment, setExamComment] = useState('')
  const [confirmFinalize, setConfirmFinalize] = useState(false)
  const [gradeDialogOpen, setGradeDialogOpen] = useState(false)
  const [reviewOpen, setReviewOpen] = useState(false)
  const [answerCursor, setAnswerCursor] = useState(0)
  const closeReviewButtonRef = useRef<HTMLButtonElement>(null)

  const locked = shift?.status === 'FINALIZED'

  const load = async () => {
    if (!shiftId) {
      setError('Thiếu mã ca thi.')
      setLoading(false)
      return
    }
    setLoading(true)
    setError('')
    try {
      const [shiftResult, submissionResult] = await Promise.all([
        inClassShiftService.getShift(shiftId),
        inClassShiftService.listSubmissions(shiftId),
      ])
      setShift(shiftResult)
      setSubmissions(submissionResult)
      setSelectedId((current) => {
        if (current && submissionResult.some((item) => item.id === current)) return current
        const firstReady = submissionResult.find((item) => !isInProgress(item.status))
        return firstReady?.id ?? submissionResult[0]?.id ?? null
      })
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [shiftId])

  const selected = useMemo(() => {
    const found = submissions.find((item) => item.id === selectedId) ?? null
    if (!found) return null
    return {
      ...found,
      answers: Array.isArray(found.answers) ? found.answers : [],
    }
  }, [submissions, selectedId])

  const answers = selected?.answers ?? []
  const answerIndex = answers.length === 0 ? 0 : Math.min(answerCursor, answers.length - 1)
  const activeAnswer = answers[answerIndex] ?? null

  useEffect(() => {
    setAnswerCursor(0)
    setGradeDialogOpen(false)
  }, [selected?.id])

  const viewingInProgress = selected ? isInProgress(selected.status) : false

  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('vi-VN')
    return submissions.filter((item) => {
      if (filter !== 'ALL' && submissionBucket(item) !== filter) return false
      if (!query) return true
      return (
        item.studentCode.toLocaleLowerCase('vi-VN').includes(query) ||
        item.studentName.toLocaleLowerCase('vi-VN').includes(query) ||
        (item.studentEmail ?? '').toLocaleLowerCase('vi-VN').includes(query)
      )
    })
  }, [submissions, filter, search])

  const stats = useMemo(() => {
    const ready = submissions.filter((item) => !isInProgress(item.status))
    const graded = ready.filter((item) => item.finalScore != null)
    const avg =
      graded.length === 0
        ? null
        : graded.reduce((sum, item) => sum + (item.finalScore ?? 0), 0) / graded.length
    const inProgressCount = submissions.length - ready.length
    return {
      graded: graded.length,
      total: ready.length,
      avg,
      inProgressCount,
    }
  }, [submissions])

  const filterCounts = useMemo(() => {
    const counts: Record<SubmissionFilter, number> = {
      ALL: submissions.length,
      PENDING: 0,
      AI: 0,
      OVERRIDE: 0,
      LOCKED: 0,
    }
    for (const item of submissions) {
      const bucket = submissionBucket(item)
      if (bucket) counts[bucket] += 1
    }
    return counts
  }, [submissions])

  const reviewIndex = filtered.findIndex((item) => item.id === selectedId)
  const hasPrevReview = reviewIndex > 0
  const hasNextReview = reviewIndex >= 0 && reviewIndex < filtered.length - 1

  const openReview = (studentId: string) => {
    setSelectedId(studentId)
    setReviewOpen(true)
  }

  const closeReview = () => {
    if (gradeDialogOpen) return
    setReviewOpen(false)
  }

  const openAdjacentReview = (direction: -1 | 1) => {
    const next = filtered[reviewIndex + direction]
    if (next) setSelectedId(next.id)
  }

  useEffect(() => {
    if (!reviewOpen) return

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        if (gradeDialogOpen) {
          if (!busy) setGradeDialogOpen(false)
          return
        }
        setReviewOpen(false)
        return
      }
      const target = event.target
      if (
        target instanceof HTMLInputElement ||
        target instanceof HTMLTextAreaElement ||
        target instanceof HTMLSelectElement
      ) {
        return
      }
      if (gradeDialogOpen) return
      if (event.key === 'ArrowLeft' && hasPrevReview) {
        event.preventDefault()
        openAdjacentReview(-1)
      }
      if (event.key === 'ArrowRight' && hasNextReview) {
        event.preventDefault()
        openAdjacentReview(1)
      }
    }

    const isDesktop = window.matchMedia('(min-width: 1024px)').matches
    const unlock = isDesktop ? undefined : lockBodyScroll()
    document.addEventListener('keydown', handleKeyDown)
    if (!gradeDialogOpen) closeReviewButtonRef.current?.focus()

    return () => {
      unlock?.()
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [reviewOpen, gradeDialogOpen, busy, hasPrevReview, hasNextReview, reviewIndex, filtered])

  const openGradeDialog = () => {
    if (!selected || locked || viewingInProgress) return
    const score = selected.finalScore ?? selected.aiScore
    setExamScore(score != null ? String(score) : '')
    setExamComment(selected.overrideReason ?? '')
    setExamScoreError('')
    setError('')
    setGradeDialogOpen(true)
  }

  const closeGradeDialog = () => {
    if (busy) return
    setGradeDialogOpen(false)
    setExamScoreError('')
  }

  const saveExamGrade = async () => {
    if (!shiftId || !selected || locked || busy || viewingInProgress) return
    const score = Number(examScore)
    if (!examScore.trim() || !Number.isFinite(score) || score < 0 || score > 10) {
      setExamScoreError('Điểm cả bài phải trong khoảng 0–10.')
      return
    }
    const answerIds = answers
      .filter((answer) => answer.status !== 'PENDING' && !answer.answerId.startsWith('pending-'))
      .map((answer) => answer.answerId)
    if (answerIds.length === 0) {
      setExamScoreError('Chưa có bài nộp để chấm.')
      return
    }
    const comment = examComment.trim()
    setExamScoreError('')
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const updated = await inClassShiftService.gradeExam(shiftId, selected.id, answerIds, {
        lecturerScore: score,
        lecturerComment: comment,
      })
      setSubmissions((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      )
      const who = updated.studentCode !== '—' ? updated.studentCode : updated.studentName
      setNotice(`Đã lưu điểm cả bài cho ${who} (${score.toFixed(1)}).`)
      setGradeDialogOpen(false)
    } catch (gradeError) {
      toast.error(getErrorMessage(gradeError))
    } finally {
      setBusy(false)
    }
  }

  const finalize = async () => {
    if (!shiftId || locked || busy) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const result = await inClassShiftService.finalizeGrades(shiftId)
      setShift((current) =>
        current
          ? { ...current, status: 'FINALIZED', finalizedAt: result.finalizedAt }
          : current,
      )
      setSubmissions((current) =>
        current.map((item) => ({
          ...item,
          status: 'FINALIZED',
          finalScore: item.finalScore ?? item.aiScore,
        })),
      )
      setConfirmFinalize(false)
      setNotice(result.message)
    } catch (finalizeError) {
      setError(getErrorMessage(finalizeError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div lang="vi" className={cn(ds.page, 'pb-28')}>
      <div className="space-y-3">
        <InClassShiftPageBanner />

        <Link
          to="/in-class-shifts"
          className="inline-flex min-h-8 w-fit items-center gap-1.5 rounded-lg text-base font-bold text-[#1C4D8D] transition hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
        >
          <ArrowLeft className="size-5" aria-hidden="true" />
          Quay lại danh sách ca
        </Link>

        {(error || notice) && (
          <div
            className={cn(
              'flex items-center justify-between gap-3 rounded-2xl border px-4 py-3 text-sm',
              error
                ? 'border-[#f3c8cf] bg-[#fff5f6] text-[#b33d52]'
                : 'border-[#bcebdc] bg-[#effbf6] text-[#16785f]',
            )}
            role={error ? 'alert' : 'status'}
          >
            <span className="flex items-center gap-2 font-medium">
              {error ? (
                <AlertCircle className="size-4 shrink-0" />
              ) : (
                <CheckCircle2 className="size-4 shrink-0" />
              )}
              {error || notice}
            </span>
            <button
              type="button"
              onClick={() => {
                setError('')
                setNotice('')
              }}
              className="text-xs font-bold underline-offset-2 hover:underline"
            >
              Đóng
            </button>
          </div>
        )}

        <section className={cn(ds.section, 'overflow-hidden')}>
        <div className={ds.sectionHeader}>
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}>
                <ClipboardList className={ds.sectionIcon} />
              </span>
              <h3 className={ds.sectionTitle}>
                {shift?.title ?? 'Đang tải thông tin ca...'}
              </h3>
              <span className={shift?.status === 'ACTIVE' && !locked ? ds.successChip : ds.infoChip}>
                {shift ? shiftStatusLabel(shift.status) : 'Đang tải'}
              </span>
              {shift?.className ? (
                <span className={ds.sectionBadge}>{shift.className}</span>
              ) : null}
            </div>
            {shift?.description ? (
              <p className={cn(ds.sectionHint, 'line-clamp-1')}>{shift.description}</p>
            ) : null}
            {shift ? (
              <div className={ds.sectionMeta}>
                <div className="mt-3 flex flex-wrap items-center gap-1.5">
                    <span className={shiftFactClass}>
                      <Clock3 className="size-3.5 shrink-0 text-[#1C4D8D]" />
                      <span className="font-bold text-[#0F2854]">{shiftWindow(shift)}</span>
                    </span>
                    {shift.examDurationMinutes != null && (
                      <span className={shiftFactClass}>
                        <Timer className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        Làm bài{' '}
                        <span className="font-bold text-[#0F2854]">
                          {formatExamDuration(shift.examDurationMinutes)}
                        </span>
                      </span>
                    )}
                    <span className={shiftFactClass}>
                      <ClipboardList className="size-3.5 shrink-0 text-[#1C4D8D]" />
                      <span>
                        <span className="font-bold text-[#0F2854]">{shift.questionCount}</span> câu
                        {shift.hasFollowUp ? (
                          <>
                            {' · phụ '}
                            <span className="font-bold text-[#0F2854]">
                              {shift.maxFollowUpQuestions || 1}
                            </span>
                          </>
                        ) : null}
                      </span>
                    </span>
                    <span className={shiftFactClass}>
                      <UsersRound className="size-3.5 shrink-0 text-[#1C4D8D]" />
                      <span>
                        <span className="font-bold text-[#0F2854]">{shift.joinedCount}</span>
                        {' vào ca · '}
                        <span className="font-bold text-[#0F2854]">{shift.submittedCount}</span>
                        {' đã nộp'}
                      </span>
                    </span>
                    <span className={shiftFactClass}>
                      {locked
                        ? formatStamp(shift.finalizedAt)
                          ? `Đã chốt ${formatStamp(shift.finalizedAt)}`
                          : 'Đã chốt'
                        : 'Chưa chốt'}
                    </span>
                </div>
              </div>
            ) : null}
          </div>
        </div>

        <div className="bg-white px-4 pt-3 sm:px-6">
          <div className="relative">
            <div
              className="flex min-w-0 flex-wrap items-end gap-0 border-b-2 border-[#1C4D8D] lg:pr-64"
              role="tablist"
              aria-label="Lọc trạng thái bài làm"
            >
              {SUBMISSION_TABS.map((tab) => {
                const count = filterCounts[tab.value]
                const active = filter === tab.value
                return (
                  <button
                    key={tab.value}
                    type="button"
                    role="tab"
                    aria-selected={active}
                    onClick={() => setFilter(tab.value)}
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
                    <span
                      className={cn(
                        'rounded-full px-1.5 py-0.5 text-[12px] font-bold tabular-nums',
                        active ? 'bg-[#1C4D8D]/12 text-[#1C4D8D]' : tab.countIdleClass,
                      )}
                    >
                      {count}
                    </span>
                  </button>
                )
              })}
            </div>
            <div className="relative mt-3 w-full lg:absolute lg:right-0 lg:bottom-2 lg:mt-0 lg:w-56">
              <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#6485b2]" />
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="MSSV hoặc họ tên..."
                aria-label="Tìm sinh viên"
                className="h-10 w-full rounded-full border border-[#4988C4] bg-white pr-3 pl-9 text-sm font-semibold outline-none focus:ring-2 focus:ring-[#4988C4]/30"
              />
            </div>
          </div>
        </div>

        {loading ? (
          <div className="flex min-h-72 flex-col items-center justify-center px-5 text-center">
            <LoaderCircle className="size-7 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
            <p className="mt-3 text-sm font-semibold text-[#5b6b84]">Đang tải bài nộp...</p>
          </div>
        ) : filtered.length === 0 ? (
          <div className="p-4 sm:p-5">
            <EmptyState
              icon={CircleDashed}
              title="Chưa có bài làm phù hợp"
              description="Thay đổi bộ lọc hoặc từ khóa tìm sinh viên."
            />
          </div>
        ) : (
          <div className="divide-y divide-[#4988C4]/30">
            {filtered.map((item) => {
              const viewing = reviewOpen && item.id === selectedId
              const inProgress = isInProgress(item.status)
              const aiLabel = inProgress || item.aiScore == null ? '—' : item.aiScore.toFixed(1)
              const finalLabel =
                inProgress || item.finalScore == null ? '—' : item.finalScore.toFixed(1)
              return (
                <article
                  key={item.id}
                  role="button"
                  tabIndex={0}
                  aria-selected={viewing}
                  onClick={() => openReview(item.id)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault()
                      openReview(item.id)
                    }
                  }}
                  className={cn(
                    'relative grid cursor-pointer gap-3 px-4 py-4 transition sm:px-6 lg:grid-cols-[minmax(0,1fr)_8.5rem_minmax(11rem,16rem)_140px_140px_auto] lg:items-center',
                    viewing
                      ? 'bg-[#D7F1F8] shadow-[inset_4px_0_0_0_#1C4D8D] ring-1 ring-inset ring-[#4988C4]/55'
                      : 'hover:bg-[#F4FBFD]',
                    inProgress && !viewing && 'opacity-80',
                  )}
                >
                  {viewing && (
                    <span className="pointer-events-none absolute top-3 right-3 rounded-full bg-[#1C4D8D] px-2 py-0.5 text-[10px] font-bold tracking-wide text-white uppercase">
                      Đang xem
                    </span>
                  )}
                  <div className="min-w-0">
                    <h3 className="line-clamp-2 text-base leading-snug font-bold text-[#0F2854]">
                      {item.studentName}
                    </h3>
                    <p className="mt-1 text-sm text-[#8794a6]">
                      {inProgress
                        ? 'Đang làm bài'
                        : item.submittedAt
                          ? `Nộp ${formatStamp(item.submittedAt)}`
                          : 'Chưa có giờ nộp'}
                    </p>
                  </div>

                  <div className="flex items-center gap-2 lg:block">
                    <span className="text-sm font-semibold text-[#8995a6] lg:block">MSSV</span>
                    <p className="mt-0.5 font-mono text-sm font-bold text-[#1C4D8D]">
                      {item.studentCode !== '—' ? item.studentCode : 'Chưa có'}
                    </p>
                  </div>

                  <div className="flex min-w-0 items-center gap-2 lg:block">
                    <span className="text-sm font-semibold text-[#8995a6] lg:block">Email</span>
                    <p className="mt-0.5 truncate text-sm font-semibold text-[#1C4D8D]">
                      {item.studentEmail || '—'}
                    </p>
                  </div>

                  <div className="flex items-center gap-2 lg:block">
                    <span className="text-sm font-semibold text-[#8995a6] lg:block">AI cả bài</span>
                    <span className="mt-0.5 inline-flex rounded-lg bg-[#4988C4]/16 px-2.5 py-1 text-sm font-bold text-[#1C4D8D]">
                      {aiLabel}
                    </span>
                  </div>

                  <div className="flex items-center gap-2 lg:block">
                    <span className="text-sm font-semibold text-[#8995a6] lg:block">Điểm cuối</span>
                    <span className="mt-0.5 text-base font-black text-[#0F2854]">{finalLabel}</span>
                  </div>

                  <button
                    type="button"
                    onClick={(event) => {
                      event.stopPropagation()
                      openReview(item.id)
                    }}
                    className={cn(
                      'inline-flex min-h-10 items-center justify-center gap-1.5 rounded-xl border px-3.5 text-sm font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]',
                      viewing
                        ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                        : 'border-[#4988C4] bg-white text-[#49617f] hover:border-[#4988C4] hover:bg-[#4988C4]/18',
                    )}
                  >
                    <Eye className="size-4" aria-hidden="true" />
                    {viewing ? 'Đang xem' : 'Xem bài'}
                  </button>
                </article>
              )
            })}
          </div>
        )}
      </section>
      </div>

      {reviewOpen && selected && (
            <>
              <button
                type="button"
                className={cn(overlay.scrimDrawer, overlay.z.drawer)}
                onClick={closeReview}
                aria-label="Đóng bài làm"
              />
              <aside
                className={cn(overlay.drawer, overlay.z.modal, 'max-w-2xl bg-[#F4FBFD]')}
                aria-label="Bài làm sinh viên"
                role="dialog"
                aria-modal="true"
              >
                <div className={cn(overlay.header, 'bg-white')}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className={overlay.eyebrow}>
                        Xem bài
                        {reviewIndex >= 0 ? ` · ${reviewIndex + 1}/${filtered.length}` : ''}
                      </p>
                      <h2 className={cn(overlay.title, 'truncate')}>
                        {selected.studentName}
                      </h2>
                    </div>
                    <div className="flex shrink-0 items-center gap-1.5">
                      <button
                        type="button"
                        disabled={!hasPrevReview}
                        onClick={() => openAdjacentReview(-1)}
                        className="flex size-9 items-center justify-center rounded-xl border border-[#4988C4] bg-white text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                        aria-label="Sinh viên trước"
                        title="Sinh viên trước (←)"
                      >
                        <ArrowLeft className="size-4" />
                      </button>
                      <button
                        type="button"
                        disabled={!hasNextReview}
                        onClick={() => openAdjacentReview(1)}
                        className="flex size-9 items-center justify-center rounded-xl border border-[#4988C4] bg-white text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35"
                        aria-label="Sinh viên tiếp"
                        title="Sinh viên tiếp (→)"
                      >
                        <ArrowRight className="size-4" />
                      </button>
                      <button
                        ref={closeReviewButtonRef}
                        type="button"
                        onClick={closeReview}
                        className={overlay.close}
                        aria-label="Đóng bảng chi tiết"
                      >
                        <X className="size-4" />
                      </button>
                    </div>
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <span className="rounded-full bg-[#D7F1F8] px-2.5 py-1 text-xs font-bold text-[#1C4D8D]">
                      MSSV {selected.studentCode !== '—' ? selected.studentCode : 'chưa có'}
                    </span>
                    {selected.studentEmail ? (
                      <span className="max-w-full truncate rounded-full bg-[#D7F1F8] px-2.5 py-1 text-xs font-semibold text-[#1C4D8D]">
                        {selected.studentEmail}
                      </span>
                    ) : null}
                    <span className="rounded-full bg-[#eef3f8] px-2.5 py-1 text-xs font-semibold text-[#5b6b84]">
                      {viewingInProgress
                        ? 'Đang làm bài'
                        : selected.submittedAt
                          ? `Nộp ${formatStamp(selected.submittedAt)}`
                          : 'Chưa có giờ nộp'}
                    </span>
                  </div>
                  {!viewingInProgress && (
                    <div className="mt-3 grid grid-cols-2 gap-3">
                      <div className="rounded-xl bg-[#D7F1F8] px-3 py-3 transition hover:bg-[#BDE8F5]">
                        <p className="text-[11px] font-bold text-[#5b6b84] uppercase">Điểm AI</p>
                        <p className="mt-1 font-mono text-2xl font-black text-[#1C4D8D]">
                          {selected.aiScore != null ? selected.aiScore.toFixed(1) : '—'}
                        </p>
                      </div>
                      <div className="rounded-xl bg-[#D7F1F8] px-3 py-3 transition hover:bg-[#BDE8F5]">
                        <p className="text-[11px] font-bold text-[#5b6b84] uppercase">Điểm cuối</p>
                        <p className="mt-1 font-mono text-2xl font-black text-[#0F2854]">
                          {selected.finalScore != null ? selected.finalScore.toFixed(1) : '—'}
                        </p>
                      </div>
                    </div>
                  )}
                  {selected.overrideReason?.trim() ? (
                    <p className="mt-3 rounded-xl border border-[#ecd9ab] bg-[#fff9ec] px-3 py-2.5 text-sm leading-relaxed text-[#755826]">
                      <span className="font-bold text-[#8a5e17]">Nhận xét giảng viên. </span>
                      {selected.overrideReason}
                    </p>
                  ) : null}
                </div>

                {!viewingInProgress && answers.length > 0 && (
                  <div className="flex shrink-0 gap-2 overflow-x-auto border-b border-[#82aedb]/55 bg-white px-5 py-3">
                    {answers.map((answer, index) => {
                      const active = index === answerIndex
                      const label = answer.isFollowUp ? `Câu phụ ${index + 1}` : `Câu ${index + 1}`
                      return (
                        <button
                          key={answer.answerId}
                          type="button"
                          onClick={() => setAnswerCursor(index)}
                          className={cn(
                            'inline-flex min-h-9 shrink-0 items-center rounded-full px-3.5 text-sm font-bold transition',
                            active
                              ? 'bg-[#1C4D8D] text-white'
                              : 'bg-[#F4FBFD] text-[#1C4D8D] hover:bg-[#D7F1F8]',
                          )}
                        >
                          {label}
                        </button>
                      )
                    })}
                  </div>
                )}

                <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
                  <div className="space-y-4 px-5 pt-5 pb-6">
                    {viewingInProgress ? (
                      <section className="rounded-2xl bg-[#fff9ec] p-4">
                        <p className="text-xs font-bold text-[#8a5e17]">Sinh viên đang làm bài</p>
                        <p className="mt-2 text-sm leading-relaxed text-[#755826]">
                          Chưa nộp xong nên chưa mở bảng điểm. Không hiển thị nội dung câu hỏi, câu
                          trả lời, nhận xét AI hay điểm cho đến khi sinh viên hoàn tất bài.
                        </p>
                      </section>
                    ) : !activeAnswer ? (
                      <EmptyState
                        compact
                        icon={ClipboardList}
                        title="Chưa có danh sách câu hỏi"
                        description="Bài nộp này chưa có dữ liệu câu hỏi để xem."
                      />
                    ) : (
                      <>
                        <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4', ds.panelShadow)}>
                          <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
                            Đề bài
                          </p>
                          <h3 className="mt-2 text-base leading-snug font-bold text-[#0F2854]">
                            {activeAnswer.title}
                          </h3>
                          <p className="mt-3 text-[15px] leading-relaxed whitespace-pre-wrap text-[#24364f]">
                            {activeAnswer.content?.trim() || 'Không có nội dung đề bài.'}
                          </p>
                        </section>
                        <section className={cn('rounded-2xl border border-[#82aedb]/55 bg-white p-4', ds.panelShadow)}>
                          <p className="text-xs font-bold tracking-[0.07em] text-[#5b6b84] uppercase">
                            Câu trả lời
                          </p>
                          <p className="mt-2 text-[15px] leading-relaxed whitespace-pre-wrap text-[#0F2854]">
                            {activeAnswer.transcript?.trim() || 'Chưa có transcript.'}
                          </p>
                        </section>
                        <section className={cn('rounded-2xl border border-[#bcebdc] bg-[#effbf6] p-4', ds.panelShadow)}>
                          <p className="text-xs font-bold tracking-[0.07em] text-[#176f58] uppercase">
                            Nhận xét AI
                          </p>
                          <p className="mt-2 text-[15px] leading-relaxed whitespace-pre-wrap text-[#176f58]">
                            {activeAnswer.aiFeedback?.trim() || 'AI chưa trả nhận xét.'}
                          </p>
                        </section>
                      </>
                    )}
                  </div>
                </div>

                {!viewingInProgress && (
                  <div className={cn(overlay.footer, 'bg-white')}>
                    {!locked ? (
                      <button
                        type="button"
                        onClick={openGradeDialog}
                        className={cn(ds.primaryButton, 'w-full')}
                      >
                        <Pencil className="size-3.5" />
                        Chỉnh điểm cả bài
                      </button>
                    ) : (
                      <p className="w-full rounded-xl border border-[#82aedb]/55 bg-[#f3f6fa] px-3 py-2.5 text-center text-xs text-[#5b6b84]">
                        Ca đã chốt. Không thể chỉnh điểm.
                      </p>
                    )}
                  </div>
                )}
              </aside>
            </>
          )}

      {!locked && (
        <footer className={cn('fixed right-0 bottom-0 left-0 z-40 border-t border-[#82aedb]/55 bg-white/95 px-4 py-3 backdrop-blur-sm md:left-[220px] xl:left-[240px]', reviewOpen && 'lg:right-[42rem]')}>
          <div className="mx-auto flex max-w-[1380px] flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-[#5b6b84]">
              Đã có điểm <strong className="text-[#0F2854]">{stats.graded}/{stats.total}</strong>
              {stats.inProgressCount > 0 && (
                <> · {stats.inProgressCount} SV đang làm (chưa mở điểm)</>
              )}
              {stats.avg != null && (
                <>
                  {' '}
                  · TB{' '}
                  <strong className="font-mono text-[#0F2854]">{stats.avg.toFixed(1)}</strong>
                </>
              )}
            </p>
            <button
              type="button"
              disabled={busy || stats.total === 0}
              onClick={() => setConfirmFinalize(true)}
              className="inline-flex h-10 items-center justify-center gap-2 rounded-xl bg-[#16836a] px-4 text-sm font-bold text-white shadow-[0_8px_18px_rgba(22,131,106,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#136f5a] disabled:opacity-50 motion-reduce:transform-none"
            >
              <Lock className="size-4" />
              Chốt bảng điểm thành phần
            </button>
          </div>
        </footer>
      )}

      {gradeDialogOpen && selected && !locked && !viewingInProgress && (
        <FormDialog
          titleId="grade-exam-dialog-title"
          eyebrow="Chỉnh điểm"
          title={selected.studentName}
          subtitle={`${selected.studentEmail || selected.studentCode} · AI chấm điểm cho cả bài`}
          icon={Pencil}
          onClose={closeGradeDialog}
          busy={busy}
          size="sm"
          align="center"
          nested
          maxHeight="max-h-[min(92dvh,640px)]"
          footer={(
            <DialogFooter>
              <button type="button" disabled={busy} onClick={closeGradeDialog} className={ds.outlineButton}>
                Hủy
              </button>
              <button type="button" disabled={busy} onClick={() => void saveExamGrade()} className={ds.primaryButton}>
                {busy ? <LoaderCircle className="size-4 animate-spin" /> : <Save className="size-4" />}
                Lưu điểm cả bài
              </button>
            </DialogFooter>
          )}
        >
          <div className="grid grid-cols-2 gap-2">
            <div className="rounded-xl border border-[#82aedb]/55 bg-white px-3 py-2.5">
              <p className="text-[11px] font-bold text-[#5b6b84] uppercase">Điểm AI</p>
              <p className="mt-1 font-mono text-lg font-black text-[#1C4D8D]">
                {selected.aiScore != null ? selected.aiScore.toFixed(1) : '—'}
              </p>
            </div>
            <div className="rounded-xl border border-[#82aedb]/55 bg-white px-3 py-2.5">
              <p className="text-[11px] font-bold text-[#5b6b84] uppercase">Điểm cuối hiện tại</p>
              <p className="mt-1 font-mono text-lg font-black text-[#0F2854]">
                {selected.finalScore != null ? selected.finalScore.toFixed(1) : '—'}
              </p>
            </div>
          </div>
          <div className="mt-4 space-y-3">
            <div>
              <label htmlFor="exam-grade-score" className={fieldLabelClass(Boolean(examScoreError))}>
                Điểm cả bài
              </label>
              <input
                id="exam-grade-score"
                type="number"
                min={0}
                max={10}
                step={0.1}
                value={examScore}
                onChange={(event) => {
                  setExamScore(event.target.value)
                  setExamScoreError('')
                }}
                aria-invalid={examScoreError ? true : undefined}
                className={cn(ds.field, 'mt-1', examScoreError && fieldInvalidClass)}
                autoFocus
              />
              <FieldError message={examScoreError} />
            </div>
            <label className="block" htmlFor="exam-grade-comment">
              <span className="text-xs font-semibold text-[#5b6b84]">
                Nhận xét / lý do chỉnh (không bắt buộc)
              </span>
              <textarea
                id="exam-grade-comment"
                value={examComment}
                onChange={(event) => setExamComment(event.target.value)}
                rows={3}
                placeholder="Có thể để trống"
                className="mt-1 min-h-20 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-[#4988C4]/30"
              />
            </label>
          </div>
        </FormDialog>
      )}

      {confirmFinalize && (
        <ConfirmDialog
          tone="warning"
          icon={Lock}
          title="Chốt One-Way Lock?"
          message="Sau khi chốt, bảng điểm thành phần của ca này không thể chỉnh sửa. Hành động không hoàn tác."
          confirmLabel="Xác nhận chốt"
          cancelLabel="Hủy"
          pending={busy}
          onConfirm={() => void finalize()}
          onCancel={() => setConfirmFinalize(false)}
        />
      )}

    </div>
  )
}
