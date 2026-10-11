import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  BadgeCheck,
  Bot,
  CheckCircle2,
  ClipboardCheck,
  FileSearch,
  Filter,
  Fingerprint,
  Gauge,
  LoaderCircle,
  LockKeyhole,
  RefreshCw,
  Search,
  Send,
  ShieldAlert,
  ShieldCheck,
  Sparkles,
  UserRoundCheck,
  UsersRound,
  X,
} from 'lucide-react'
import { WaveformPlayer } from '@/components/audio/WaveformPlayer'
import { ScoreOverrideForm } from '@/components/rubric/ScoreOverrideForm'
import { officialExamService } from '@/services/officialExam.service'
import { useAuth } from '@/hooks/useAuth'
import type { AuditEvidenceItem, ExamTicketStatus } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'

type AuditFilter = 'attention' | 'high-confidence' | 'pending' | 'all'

const REVIEWED_STATUSES: ExamTicketStatus[] = ['AUDITED', 'PUBLISHED', 'LOCKED']

const STATUS_META: Record<ExamTicketStatus, { label: string; className: string }> = {
  SCHEDULED: { label: 'Đã xếp lịch', className: 'bg-[#f0f3f7] text-[#5b6c82]' },
  IN_PROGRESS: { label: 'Đang thi', className: 'bg-[#eef3ff] text-[#315fca]' },
  SUBMITTED: { label: 'Đã nộp', className: 'bg-[#eef3ff] text-[#315fca]' },
  AI_GRADED: { label: 'AI đã chấm', className: 'bg-[#f0edff] text-[#6256c6]' },
  AUDITED: { label: 'Đã hậu kiểm', className: 'bg-[#eaf9f4] text-[#17745b]' },
  PUBLISHED: { label: 'Đã công bố', className: 'bg-[#eaf9f4] text-[#17745b]' },
  LOCKED: { label: 'Đã khóa', className: 'bg-[#fff0f1] text-[#a93d48]' },
}

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể hoàn tất yêu cầu. Vui lòng thử lại.'
}

function isUuid(value: string) {
  return /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value)
}

function getDisplayedScore(candidate: AuditEvidenceItem) {
  return candidate.finalScore ?? candidate.aiScore
}

function needsLecturerAttention(candidate: AuditEvidenceItem) {
  return (
    candidate.isSuspicious ||
    (candidate.confidenceScore !== null && candidate.confidenceScore < 0.7)
  )
}

function hasCompleteScore(candidate: AuditEvidenceItem) {
  return candidate.finalScore !== null || candidate.aiScore !== null
}

function isPendingEvaluation(candidate: AuditEvidenceItem) {
  return (
    !hasCompleteScore(candidate) ||
    (candidate.confidenceScore === null && !REVIEWED_STATUSES.includes(candidate.status))
  )
}

function isHighConfidence(candidate: AuditEvidenceItem) {
  return (
    !isPendingEvaluation(candidate) &&
    !needsLecturerAttention(candidate) &&
    (candidate.confidenceScore === null || candidate.confidenceScore >= 0.7)
  )
}

function formatConfidence(value: number | null) {
  if (value === null) return 'Chưa có'
  return `${Math.round(value * 100)}%`
}

function StatusBadge({ status }: { status: ExamTicketStatus }) {
  const meta = STATUS_META[status]
  return (
    <span className={cn('rounded-full px-2 py-1 text-[8px] font-bold', meta.className)}>
      {meta.label}
    </span>
  )
}

export function AuditEvidencePage() {
  const { user } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const routeShiftId = searchParams.get('shiftId') ?? ''
  const [shiftInput, setShiftInput] = useState(routeShiftId)
  const [evidence, setEvidence] = useState<AuditEvidenceItem[]>([])
  const [selectedTicketId, setSelectedTicketId] = useState('')
  const [filter, setFilter] = useState<AuditFilter>('attention')
  const [isLoading, setIsLoading] = useState(false)
  const [isPublishing, setIsPublishing] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [showPublishConfirm, setShowPublishConfirm] = useState(false)
  const modalCloseButtonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!routeShiftId || !isUuid(routeShiftId)) return

    let isActive = true
    const loadEvidence = async () => {
      setIsLoading(true)
      setError('')
      setNotice('')
      try {
        const result = await officialExamService.getAuditEvidence(routeShiftId)
        if (!isActive) return
        const sorted = [...result].sort((left, right) => {
          const getPriority = (candidate: AuditEvidenceItem) =>
            needsLecturerAttention(candidate) ? 0 : isPendingEvaluation(candidate) ? 1 : 2
          return getPriority(left) - getPriority(right) || left.seatNumber - right.seatNumber
        })
        setEvidence(sorted)
        setSelectedTicketId((current) =>
          sorted.some((candidate) => candidate.ticketId === current)
            ? current
            : (sorted[0]?.ticketId ?? ''),
        )
      } catch (loadError) {
        if (isActive) {
          setEvidence([])
          setSelectedTicketId('')
          setError(getErrorMessage(loadError))
        }
      } finally {
        if (isActive) setIsLoading(false)
      }
    }

    void loadEvidence()
    return () => {
      isActive = false
    }
  }, [routeShiftId, reloadKey])

  useEffect(() => {
    if (!showPublishConfirm) return

    const previousOverflow = document.body.style.overflow
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isPublishing) setShowPublishConfirm(false)
    }

    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', handleKeyDown)
    modalCloseButtonRef.current?.focus()

    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isPublishing, showPublishConfirm])

  const attentionCount = evidence.filter(needsLecturerAttention).length
  const reviewedAttentionCount = evidence.filter(
    (candidate) =>
      needsLecturerAttention(candidate) && REVIEWED_STATUSES.includes(candidate.status),
  ).length
  const completedScoreCount = evidence.filter(hasCompleteScore).length
  const pendingCount = evidence.filter(isPendingEvaluation).length
  const highConfidenceCount = evidence.filter(isHighConfidence).length
  const lockedCount = evidence.filter((candidate) => candidate.isLocked).length
  const allScoresComplete = evidence.length > 0 && completedScoreCount === evidence.length
  const allAttentionReviewed = attentionCount === 0 || reviewedAttentionCount === attentionCount
  const alreadyPublished =
    evidence.length > 0 &&
    evidence.every(
      (candidate) =>
        candidate.isLocked || candidate.status === 'PUBLISHED' || candidate.status === 'LOCKED',
    )
  const canPublish =
    allScoresComplete && pendingCount === 0 && allAttentionReviewed && !alreadyPublished

  const filteredEvidence = useMemo(() => {
    if (filter === 'attention') return evidence.filter(needsLecturerAttention)
    if (filter === 'high-confidence') return evidence.filter(isHighConfidence)
    if (filter === 'pending') return evidence.filter(isPendingEvaluation)
    return evidence
  }, [evidence, filter])

  const visibleSelectedTicketId = filteredEvidence.some(
    (candidate) => candidate.ticketId === selectedTicketId,
  )
    ? selectedTicketId
    : (filteredEvidence[0]?.ticketId ?? '')
  const activeCandidate =
    evidence.find((candidate) => candidate.ticketId === visibleSelectedTicketId) ?? null

  const openShift = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const value = shiftInput.trim()
    if (!isUuid(value)) {
      setError('Shift ID phải là một UUID hợp lệ.')
      return
    }
    setError('')
    setSearchParams({ shiftId: value })
  }

  const publishGrades = async () => {
    if (!canPublish || !routeShiftId) return
    if (!isUuid(user?.id ?? '')) {
      setError(
        'Phiên đăng nhập không có mã người dùng hợp lệ. Vui lòng đăng xuất và đăng nhập lại.',
      )
      return
    }
    setIsPublishing(true)
    setError('')
    setNotice('')
    try {
      const result = await officialExamService.publishGrades(routeShiftId, {
        lecturerId: user?.id,
      })
      setNotice(result.message)
      setShowPublishConfirm(false)
      setReloadKey((key) => key + 1)
    } catch (publishError) {
      setError(getErrorMessage(publishError))
      setShowPublishConfirm(false)
    } finally {
      setIsPublishing(false)
    }
  }

  const progress =
    evidence.length === 0 ? 0 : Math.round((completedScoreCount / evidence.length) * 100)

  return (
    <div className="space-y-4 sm:space-y-5">
      <section className="relative overflow-hidden rounded-[22px] border border-[#dce5f1] bg-white p-5 shadow-[0_14px_40px_rgba(31,61,105,0.065)] sm:p-6">
        <div className="pointer-events-none absolute -top-24 right-0 size-72 rounded-full bg-[#7766e9]/10 blur-3xl" />
        <div className="relative flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
          <div>
            <p className="flex items-center gap-2 text-[9px] font-bold tracking-[0.09em] text-[#6256ce] uppercase">
              <ClipboardCheck className="size-4" aria-hidden="true" />
              MF-04 · Hậu kiểm bằng chứng & điểm
            </p>
            <h1 className="mt-2 text-xl font-bold tracking-[-0.035em] text-[#10203d] sm:text-2xl">
              Hậu kiểm & công bố điểm ca thi
            </h1>
            <p className="mt-2 max-w-3xl text-[11px] leading-relaxed text-[#687990] sm:text-xs">
              Ưu tiên bài được hệ thống đánh dấu nghi ngờ hoặc có confidence dưới 0.70; đối chiếu
              bằng chứng trước khi công bố điểm một lần cho toàn ca.
            </p>
          </div>

          <form onSubmit={openShift} className="flex w-full max-w-xl flex-col gap-2 sm:flex-row">
            <label className="relative min-w-0 flex-1">
              <span className="sr-only">Shift ID của ca thi</span>
              <Fingerprint className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#7c8ca1]" />
              <input
                value={shiftInput}
                onChange={(event) => setShiftInput(event.target.value)}
                placeholder="Nhập UUID ca thi (Shift ID)"
                className="min-h-11 w-full rounded-xl border border-[#d2dce9] bg-[#fbfcfe] pr-3 pl-9 font-mono text-[10px] text-[#2a3d58] transition outline-none placeholder:font-sans placeholder:text-[#95a3b5] focus:border-[#7774dc] focus:ring-2 focus:ring-[#e5e3ff]"
              />
            </label>
            <button
              type="submit"
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#6256ce] px-4 text-[10px] font-bold text-white shadow-[0_9px_22px_rgba(98,86,206,0.21)] transition hover:bg-[#5146b8] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#6256ce]"
            >
              <Search className="size-3.5" />
              Mở ca thi
            </button>
          </form>
        </div>
      </section>

      {(error || notice) && (
        <div
          className={cn(
            'flex items-start gap-2 rounded-2xl border px-4 py-3 text-[11px] font-semibold',
            error
              ? 'border-[#efc8cc] bg-[#fff5f6] text-[#a43c47]'
              : 'border-[#bce4d7] bg-[#effaf6] text-[#176f58]',
          )}
          role={error ? 'alert' : 'status'}
        >
          {error ? (
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
          ) : (
            <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
          )}
          {error || notice}
        </div>
      )}

      {!routeShiftId || !isUuid(routeShiftId) ? (
        <section className="flex min-h-[430px] flex-col items-center justify-center rounded-[22px] border border-dashed border-[#cbd7e6] bg-white px-5 text-center shadow-[0_10px_34px_rgba(31,61,105,0.04)]">
          <span className="flex size-14 items-center justify-center rounded-2xl bg-[#f0edff] text-[#6256ce]">
            <FileSearch className="size-6" aria-hidden="true" />
          </span>
          <h2 className="mt-4 text-base font-bold text-[#243750]">Chọn ca thi cần hậu kiểm</h2>
          <p className="mt-2 max-w-lg text-[10px] leading-relaxed text-[#76869b]">
            Nhập Shift ID từ lịch phân công. Danh sách ca thi chưa được đồng bộ vào cổng giảng viên,
            nên hệ thống chỉ tải bằng chứng sau khi nhận mã hợp lệ.
          </p>
          <div className="mt-5 grid max-w-2xl gap-3 sm:grid-cols-3">
            {[
              { icon: ShieldAlert, label: 'Ưu tiên bài nghi ngờ' },
              { icon: Sparkles, label: 'Đối chiếu bằng chứng AI' },
              { icon: LockKeyhole, label: 'Công bố & khóa toàn ca' },
            ].map(({ icon: Icon, label }) => (
              <div
                key={label}
                className="flex items-center gap-2 rounded-xl border border-[#e0e6ef] bg-[#fafcfe] px-3 py-2.5 text-left text-[9px] font-semibold text-[#60738d]"
              >
                <Icon className="size-3.5 shrink-0 text-[#6256ce]" />
                {label}
              </div>
            ))}
          </div>
        </section>
      ) : isLoading ? (
        <section className="flex min-h-[430px] flex-col items-center justify-center rounded-[22px] border border-[#dce5f1] bg-white">
          <LoaderCircle className="size-8 animate-spin text-[#6256ce] motion-reduce:animate-none" />
          <p className="mt-3 text-[11px] font-semibold text-[#6d7e94]">
            Đang tải Evidence Panel của ca thi...
          </p>
        </section>
      ) : evidence.length === 0 && !error ? (
        <section className="flex min-h-[430px] flex-col items-center justify-center rounded-[22px] border border-dashed border-[#cbd7e6] bg-white px-5 text-center">
          <UsersRound className="size-7 text-[#8495aa]" />
          <h2 className="mt-3 text-sm font-bold text-[#2b3e58]">Ca thi chưa có bài nộp</h2>
          <p className="mt-1 text-[10px] text-[#7e8c9f]">
            Chưa có dữ liệu thí sinh để giảng viên hậu kiểm.
          </p>
        </section>
      ) : evidence.length > 0 ? (
        <>
          <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {[
              {
                label: 'Tổng thí sinh',
                value: evidence.length,
                helper: 'Bài nộp trong ca',
                icon: UsersRound,
                tone: 'bg-[#edf3ff] text-[#245eea]',
              },
              {
                label: 'Cần can thiệp',
                value: attentionCount,
                helper: `${reviewedAttentionCount}/${attentionCount} đã hậu kiểm`,
                icon: ShieldAlert,
                tone: 'bg-[#fff0f1] text-[#b8404b]',
              },
              {
                label: 'Đã có điểm',
                value: completedScoreCount,
                helper: `${progress}% hoàn tất ca thi`,
                icon: UserRoundCheck,
                tone: 'bg-[#ecfaf5] text-[#168367]',
              },
              {
                label: 'Đã khóa',
                value: lockedCount,
                helper: alreadyPublished ? 'Điểm đã công bố' : 'Chờ công bố toàn ca',
                icon: LockKeyhole,
                tone: 'bg-[#f0edff] text-[#6256ce]',
              },
            ].map(({ label, value, helper, icon: Icon, tone }) => (
              <article
                key={label}
                className="rounded-2xl border border-[#e1e7f0] bg-white p-4 shadow-[0_7px_26px_rgba(31,61,105,0.045)]"
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-[10px] font-medium text-[#64748b]">{label}</p>
                    <strong className="mt-2 block text-2xl font-black tracking-[-0.04em] text-[#10203d]">
                      {String(value).padStart(2, '0')}
                    </strong>
                  </div>
                  <span className={cn('flex size-9 items-center justify-center rounded-xl', tone)}>
                    <Icon className="size-4" />
                  </span>
                </div>
                <p className="mt-1 text-[9px] text-[#7b899c]">{helper}</p>
              </article>
            ))}
          </section>

          <section className="overflow-hidden rounded-2xl border border-[#e0e7f1] bg-white shadow-[0_9px_30px_rgba(31,61,105,0.05)]">
            <div className="flex flex-col gap-4 border-b border-[#e8edf4] p-4 sm:p-5 lg:flex-row lg:items-center lg:justify-between">
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <p className="text-[9px] font-bold tracking-[0.08em] text-[#657b98] uppercase">
                      Tiến độ ca thi
                    </p>
                    <h2 className="mt-1 text-sm font-bold text-[#172640]">
                      {completedScoreCount}/{evidence.length} thí sinh có điểm hoàn chỉnh
                    </h2>
                  </div>
                  <span className="font-mono text-sm font-black text-[#245eea]">{progress}%</span>
                </div>
                <div className="mt-3 h-2 overflow-hidden rounded-full bg-[#edf1f6]">
                  <div
                    className="h-full rounded-full bg-[linear-gradient(90deg,#245eea,#49c9db)] transition-[width] duration-500"
                    style={{ width: `${progress}%` }}
                  />
                </div>
              </div>
              <div className="flex shrink-0 gap-2">
                <button
                  type="button"
                  onClick={() => setReloadKey((key) => key + 1)}
                  className="flex size-10 items-center justify-center rounded-xl border border-[#d4deeb] text-[#60738d] transition hover:bg-[#f5f8fc] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea]"
                  aria-label="Tải lại bằng chứng"
                >
                  <RefreshCw className="size-4" />
                </button>
                <button
                  type="button"
                  onClick={() => setShowPublishConfirm(true)}
                  disabled={!canPublish}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#168367] px-4 text-[10px] font-bold text-white shadow-[0_8px_20px_rgba(22,131,103,0.2)] transition hover:bg-[#106c55] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#168367] disabled:cursor-not-allowed disabled:bg-[#a7b7b3] disabled:shadow-none"
                >
                  {alreadyPublished ? (
                    <LockKeyhole className="size-3.5" />
                  ) : (
                    <Send className="size-3.5" />
                  )}
                  {alreadyPublished ? 'Điểm đã khóa' : 'Công bố điểm toàn ca'}
                </button>
              </div>
            </div>
            {!canPublish && !alreadyPublished && (
              <div className="flex items-start gap-2 border-b border-[#ead9ae] bg-[#fff9ec] px-4 py-3 text-[9px] leading-relaxed text-[#805a20] sm:px-5">
                <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
                {!allScoresComplete
                  ? 'Chưa thể công bố: vẫn còn thí sinh chưa có điểm hoàn chỉnh.'
                  : pendingCount > 0
                    ? 'Chưa thể công bố: vẫn còn bài chưa hoàn tất phân loại độ tin cậy.'
                    : 'Chưa thể công bố: các bài bị đánh dấu nghi ngờ hoặc confidence thấp chưa được hậu kiểm đầy đủ.'}
              </div>
            )}

            <div className="grid min-h-[680px] lg:grid-cols-[330px_minmax(0,1fr)]">
              <aside className="border-b border-[#e4eaf2] bg-[#fbfcfe] lg:border-r lg:border-b-0">
                <div className="border-b border-[#e5eaf1] p-3">
                  <div className="flex items-center gap-2 text-[9px] font-bold text-[#667990]">
                    <Filter className="size-3.5" />
                    Phân nhóm hậu kiểm
                  </div>
                  <div className="mt-2 grid grid-cols-2 gap-1 rounded-xl bg-[#edf1f6] p-1 lg:grid-cols-1">
                    {[
                      {
                        value: 'attention' as const,
                        label: 'Cần can thiệp',
                        count: attentionCount,
                      },
                      {
                        value: 'high-confidence' as const,
                        label: 'Tin cậy cao',
                        count: highConfidenceCount,
                      },
                      { value: 'pending' as const, label: 'Chờ AI chấm', count: pendingCount },
                      { value: 'all' as const, label: 'Tất cả', count: evidence.length },
                    ].map((item) => (
                      <button
                        key={item.value}
                        type="button"
                        onClick={() => setFilter(item.value)}
                        className={cn(
                          'flex min-h-9 items-center justify-between gap-2 rounded-lg px-2.5 text-[9px] font-bold transition focus-visible:outline-2 focus-visible:outline-[#6256ce]',
                          filter === item.value
                            ? 'bg-white text-[#314968] shadow-sm'
                            : 'text-[#74849a] hover:text-[#425774]',
                        )}
                      >
                        <span className="truncate">{item.label}</span>
                        <span className="rounded-full bg-[#f1f4f8] px-1.5 py-0.5 font-mono text-[8px]">
                          {item.count}
                        </span>
                      </button>
                    ))}
                  </div>
                </div>

                <div className="max-h-[560px] space-y-1.5 overflow-y-auto p-2 lg:max-h-[620px]">
                  {filteredEvidence.length === 0 ? (
                    <div className="px-4 py-10 text-center">
                      <BadgeCheck className="mx-auto size-6 text-[#47a88c]" />
                      <p className="mt-2 text-[10px] font-bold text-[#52657e]">
                        Không có bài trong nhóm này
                      </p>
                    </div>
                  ) : (
                    filteredEvidence.map((candidate) => {
                      const active = candidate.ticketId === visibleSelectedTicketId
                      const attention = needsLecturerAttention(candidate)
                      const pending = isPendingEvaluation(candidate)
                      const score = getDisplayedScore(candidate)
                      return (
                        <button
                          key={candidate.ticketId}
                          type="button"
                          onClick={() => setSelectedTicketId(candidate.ticketId)}
                          className={cn(
                            'w-full rounded-xl border p-3 text-left transition focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#6256ce]',
                            active
                              ? 'border-[#aebbed] bg-white shadow-[0_7px_20px_rgba(69,75,155,0.09)]'
                              : 'border-transparent hover:border-[#dde4ee] hover:bg-white',
                          )}
                        >
                          <div className="flex items-start gap-3">
                            <span
                              className={cn(
                                'flex size-9 shrink-0 items-center justify-center rounded-xl font-mono text-[10px] font-black',
                                attention
                                  ? 'bg-[#fff0f1] text-[#b8404b]'
                                  : pending
                                    ? 'bg-[#fff6e5] text-[#a66c12]'
                                    : 'bg-[#eaf9f4] text-[#168367]',
                              )}
                            >
                              {String(candidate.seatNumber).padStart(2, '0')}
                            </span>
                            <div className="min-w-0 flex-1">
                              <p className="truncate text-[10px] font-bold text-[#293c57]">
                                {candidate.fullName || 'Chưa có họ tên'}
                              </p>
                              <p className="mt-0.5 font-mono text-[8px] font-semibold text-[#8190a3]">
                                {candidate.studentCode}
                              </p>
                              <div className="mt-2 flex flex-wrap items-center gap-1.5">
                                <StatusBadge status={candidate.status} />
                                {attention && (
                                  <span className="rounded-full bg-[#fff0f1] px-2 py-1 text-[8px] font-bold text-[#a93d48]">
                                    Cần xem kỹ
                                  </span>
                                )}
                                {pending && (
                                  <span className="rounded-full bg-[#fff6e5] px-2 py-1 text-[8px] font-bold text-[#8d6118]">
                                    Chờ phân loại
                                  </span>
                                )}
                              </div>
                            </div>
                            <div className="text-right">
                              <span className="block font-mono text-[12px] font-black text-[#245eea]">
                                {score === null ? '—' : score.toFixed(1)}
                              </span>
                              <span className="text-[8px] text-[#8c98a9]">/10</span>
                            </div>
                          </div>
                        </button>
                      )
                    })
                  )}
                </div>
              </aside>

              <div className="min-w-0 bg-[#f5f8fc] p-3 sm:p-4 lg:p-5">
                {activeCandidate ? (
                  <div className="space-y-4">
                    <section className="rounded-2xl border border-[#dce5f1] bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)]">
                      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                        <div className="flex items-center gap-3">
                          <span className="flex size-11 items-center justify-center rounded-2xl bg-[#eef3ff] font-mono text-xs font-black text-[#245eea]">
                            {String(activeCandidate.seatNumber).padStart(2, '0')}
                          </span>
                          <div>
                            <h2 className="text-sm font-bold text-[#192c48]">
                              {activeCandidate.fullName || 'Chưa có họ tên'}
                            </h2>
                            <p className="mt-0.5 font-mono text-[9px] font-semibold text-[#74859b]">
                              {activeCandidate.studentCode}
                            </p>
                          </div>
                        </div>
                        <div className="flex flex-wrap items-center gap-2">
                          <StatusBadge status={activeCandidate.status} />
                          {activeCandidate.isLocked && (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#fff0f1] px-2 py-1 text-[8px] font-bold text-[#a93d48]">
                              <LockKeyhole className="size-3" />
                              One-Way Lock
                            </span>
                          )}
                        </div>
                      </div>

                      <div className="mt-4 grid gap-3 sm:grid-cols-3">
                        <div className="rounded-xl border border-[#e2e8f0] bg-[#fbfcfe] p-3">
                          <p className="flex items-center gap-1.5 text-[8px] font-bold text-[#79889b] uppercase">
                            <Bot className="size-3.5 text-[#6256ce]" />
                            Điểm AI
                          </p>
                          <p className="mt-2 font-mono text-lg font-black text-[#263b59]">
                            {activeCandidate.aiScore === null
                              ? '—'
                              : activeCandidate.aiScore.toFixed(1)}
                            <span className="ml-1 text-[9px] text-[#8290a2]">/10</span>
                          </p>
                        </div>
                        <div className="rounded-xl border border-[#e2e8f0] bg-[#fbfcfe] p-3">
                          <p className="flex items-center gap-1.5 text-[8px] font-bold text-[#79889b] uppercase">
                            <Gauge className="size-3.5 text-[#245eea]" />
                            Confidence
                          </p>
                          <p
                            className={cn(
                              'mt-2 font-mono text-lg font-black',
                              activeCandidate.confidenceScore !== null &&
                                activeCandidate.confidenceScore < 0.7
                                ? 'text-[#b8404b]'
                                : 'text-[#168367]',
                            )}
                          >
                            {formatConfidence(activeCandidate.confidenceScore)}
                          </p>
                        </div>
                        <div className="rounded-xl border border-[#e2e8f0] bg-[#fbfcfe] p-3">
                          <p className="flex items-center gap-1.5 text-[8px] font-bold text-[#79889b] uppercase">
                            <UserRoundCheck className="size-3.5 text-[#168367]" />
                            Điểm hiện tại
                          </p>
                          <p className="mt-2 font-mono text-lg font-black text-[#263b59]">
                            {getDisplayedScore(activeCandidate) === null
                              ? '—'
                              : getDisplayedScore(activeCandidate)?.toFixed(1)}
                            <span className="ml-1 text-[9px] text-[#8290a2]">/10</span>
                          </p>
                        </div>
                      </div>

                      {needsLecturerAttention(activeCandidate) && (
                        <div className="mt-3 flex items-start gap-2 rounded-xl border border-[#efc8cc] bg-[#fff5f6] p-3 text-[9px] leading-relaxed text-[#953943]">
                          <ShieldAlert className="mt-0.5 size-3.5 shrink-0" />
                          {activeCandidate.suspiciousReason ||
                            'Bài nộp bị đánh dấu cần can thiệp; giảng viên phải đối chiếu toàn bộ bằng chứng.'}
                        </div>
                      )}
                      {isPendingEvaluation(activeCandidate) &&
                        !needsLecturerAttention(activeCandidate) && (
                          <div className="mt-3 flex items-start gap-2 rounded-xl border border-[#ead9ae] bg-[#fff9ec] p-3 text-[9px] leading-relaxed text-[#805a20]">
                            <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
                            Bài nộp chưa có đủ điểm hoặc thông tin phân loại độ tin cậy. Vui lòng
                            chờ tiến trình AI hoàn tất trước khi hậu kiểm.
                          </div>
                        )}
                    </section>

                    <WaveformPlayer
                      audioUrl={activeCandidate.audioR2Url}
                      hashSha256={activeCandidate.audioHashSha256}
                      studentCode={activeCandidate.studentCode}
                      seatNumber={activeCandidate.seatNumber}
                    />

                    <div className="grid gap-4 xl:grid-cols-2">
                      <section className="rounded-2xl border border-[#dce5f1] bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)] sm:p-5">
                        <div className="flex items-center gap-2">
                          <FileSearch className="size-4 text-[#245eea]" />
                          <h3 className="text-sm font-bold text-[#192c48]">Transcript Whisper</h3>
                        </div>
                        <div className="mt-3 max-h-72 overflow-y-auto rounded-xl border border-[#e2e8f0] bg-[#fbfcfe] p-3">
                          <p className="text-[10px] leading-[1.7] whitespace-pre-wrap text-[#52657e]">
                            {activeCandidate.transcriptWhisper ||
                              'Chưa có transcript để giảng viên đối chiếu.'}
                          </p>
                        </div>
                      </section>

                      <section className="rounded-2xl border border-[#dce5f1] bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)] sm:p-5">
                        <div className="flex items-center gap-2">
                          <Sparkles className="size-4 text-[#6256ce]" />
                          <h3 className="text-sm font-bold text-[#192c48]">
                            Phân tích AI theo tiêu chí
                          </h3>
                        </div>
                        <div className="mt-3 max-h-72 overflow-y-auto rounded-xl border border-[#e2e8f0] bg-[#fbfcfe] p-3">
                          <p className="text-[10px] leading-[1.7] whitespace-pre-wrap text-[#52657e]">
                            {activeCandidate.cotTrace ||
                              'Chưa có phân tích AI để hiển thị trong Evidence Panel.'}
                          </p>
                        </div>
                      </section>
                    </div>

                    <ScoreOverrideForm
                      key={activeCandidate.ticketId}
                      initialScore={getDisplayedScore(activeCandidate)}
                      isLocked={activeCandidate.isLocked}
                      persistenceAvailable={false}
                    />
                  </div>
                ) : (
                  <div className="flex min-h-[560px] flex-col items-center justify-center text-center">
                    <FileSearch className="size-7 text-[#8797aa]" />
                    <p className="mt-3 text-[10px] font-semibold text-[#687a91]">
                      Chọn một thí sinh để mở Evidence Panel.
                    </p>
                  </div>
                )}
              </div>
            </div>
          </section>
        </>
      ) : null}

      {showPublishConfirm && (
        <div
          className="fixed inset-0 z-[70] flex items-center justify-center bg-[#07172e]/55 p-4 backdrop-blur-sm"
          role="presentation"
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="publish-confirm-title"
            className="w-full max-w-md overflow-hidden rounded-[22px] border border-white/30 bg-white shadow-[0_26px_80px_rgba(4,18,40,0.3)]"
          >
            <div className="flex items-start justify-between gap-4 border-b border-[#e8edf4] p-5">
              <div className="flex gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#fff0f1] text-[#b8404b]">
                  <LockKeyhole className="size-[18px]" />
                </span>
                <div>
                  <h2 id="publish-confirm-title" className="text-sm font-bold text-[#172640]">
                    Xác nhận công bố điểm toàn ca
                  </h2>
                  <p className="mt-1 text-[9px] leading-relaxed text-[#76869a]">
                    Thao tác này kích hoạt One-Way Lock và không thể hoàn tác.
                  </p>
                </div>
              </div>
              <button
                ref={modalCloseButtonRef}
                type="button"
                onClick={() => setShowPublishConfirm(false)}
                disabled={isPublishing}
                className="flex size-8 shrink-0 items-center justify-center rounded-lg text-[#74849a] hover:bg-[#f2f5f9] focus-visible:outline-2 focus-visible:outline-[#245eea] disabled:cursor-not-allowed disabled:opacity-40"
                aria-label="Đóng hộp xác nhận"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="p-5">
              <div className="grid grid-cols-2 gap-3">
                <div className="rounded-xl border border-[#e0e7f0] bg-[#fafcfe] p-3 text-center">
                  <p className="text-[8px] font-bold text-[#7a899c] uppercase">Sẽ công bố</p>
                  <p className="mt-1 font-mono text-lg font-black text-[#245eea]">
                    {evidence.length}
                  </p>
                  <p className="text-[8px] text-[#8491a3]">sinh viên</p>
                </div>
                <div className="rounded-xl border border-[#e0e7f0] bg-[#fafcfe] p-3 text-center">
                  <p className="text-[8px] font-bold text-[#7a899c] uppercase">
                    Đã hậu kiểm ưu tiên
                  </p>
                  <p className="mt-1 font-mono text-lg font-black text-[#168367]">
                    {reviewedAttentionCount}/{attentionCount}
                  </p>
                  <p className="text-[8px] text-[#8491a3]">bài cần can thiệp</p>
                </div>
              </div>
              <div className="mt-4 flex items-start gap-2 rounded-xl border border-[#f0d0d4] bg-[#fff6f6] p-3 text-[9px] leading-relaxed text-[#8f3942]">
                <ShieldCheck className="mt-0.5 size-3.5 shrink-0" />
                Sau khi xác nhận, hệ thống chặn 100% thao tác UPDATE hoặc DELETE điểm số.
              </div>
            </div>
            <div className="flex justify-end gap-2 border-t border-[#e8edf4] bg-[#fbfcfe] p-4">
              <button
                type="button"
                onClick={() => setShowPublishConfirm(false)}
                disabled={isPublishing}
                className="inline-flex min-h-10 items-center justify-center rounded-xl border border-[#d4deeb] bg-white px-4 text-[10px] font-bold text-[#536780] hover:bg-[#f4f7fb] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#245eea] disabled:cursor-not-allowed disabled:opacity-40"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={() => void publishGrades()}
                disabled={isPublishing}
                className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#b8404b] px-4 text-[10px] font-bold text-white shadow-[0_8px_18px_rgba(184,64,75,0.2)] hover:bg-[#9f3440] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#b8404b] disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isPublishing ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <LockKeyhole className="size-3.5" />
                )}
                {isPublishing ? 'Đang công bố...' : 'Công bố & khóa điểm'}
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}

export { AuditEvidencePage as AuditPortalPage }
