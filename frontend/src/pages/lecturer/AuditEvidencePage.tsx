import { useEffect, useMemo, useState, type FormEvent } from 'react'
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
  Sparkles,
  UserRoundCheck,
  UsersRound,
} from 'lucide-react'
import { WaveformPlayer } from '@/components/audio/WaveformPlayer'
import { ScoreOverrideForm } from '@/components/rubric/ScoreOverrideForm'
import { officialExamService } from '@/services/officialExam.service'
import { useAuth } from '@/hooks/useAuth'
import type { AuditEvidenceItem, ExamTicketStatus } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'
import { ds } from '@/design/system'
import { ConfirmDialog } from '@/design/ConfirmDialog'

type AuditFilter = 'attention' | 'high-confidence' | 'pending' | 'all'

const REVIEWED_STATUSES: ExamTicketStatus[] = ['AUDITED', 'PUBLISHED', 'LOCKED']

const STATUS_META: Record<ExamTicketStatus, { label: string; className: string }> = {
  SCHEDULED: { label: 'Đã xếp lịch', className: 'bg-[#f0f3f7] text-[#5b6c82]' },
  IN_PROGRESS: { label: 'Đang thi', className: 'bg-[#4988C4]/16 text-[#1C4D8D]' },
  SUBMITTED: { label: 'Đã nộp', className: 'bg-[#4988C4]/16 text-[#1C4D8D]' },
  AI_GRADED: { label: 'AI đã chấm', className: 'bg-[#4988C4]/16 text-[#6256c6]' },
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
    <span className={cn('rounded-full px-2 py-1 text-xs font-bold', meta.className)}>
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
    <div className={ds.page}>
      <section className={cn(ds.banner, 'p-5 sm:p-6')}>
        <div className="pointer-events-none absolute -top-24 right-0 size-72 rounded-full bg-[#7766e9]/10 blur-3xl" />
        <div className="relative flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
          <div>
            <p className={cn(ds.bannerChip, 'w-fit')}>
              <ClipboardCheck className="size-4" aria-hidden="true" />
              MF-04 · Hậu kiểm bằng chứng & điểm
            </p>
            <h1 className={ds.bannerTitle}>
              Hậu kiểm & công bố điểm ca thi
            </h1>
            <p className={ds.bannerText}>
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
                className={cn(ds.field, 'pr-3 pl-9 font-mono text-xs placeholder:font-sans')}
              />
            </label>
            <button
              type="submit"
              className={ds.bannerButton}
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
            'flex items-start gap-2 rounded-2xl border px-4 py-3 text-sm font-semibold',
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
        <section className="flex min-h-[430px] flex-col items-center justify-center rounded-[22px] border border-dashed border-[#4988C4]/45 bg-white px-5 text-center shadow-[0_10px_34px_rgba(31,61,105,0.04)]">
          <span className="flex size-14 items-center justify-center rounded-2xl bg-[#4988C4]/16 text-[#1C4D8D]">
            <FileSearch className="size-6" aria-hidden="true" />
          </span>
          <h2 className="mt-4 text-base font-bold text-[#0F2854] sm:text-lg">Chọn ca thi cần hậu kiểm</h2>
          <p className="mt-2 max-w-lg text-xs leading-relaxed text-[#5b6b84]">
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
                className="flex items-center gap-2 rounded-xl border border-[#4988C4]/45 bg-white px-3 py-2.5 text-left text-xs font-semibold text-[#5b6b84]"
              >
                <Icon className="size-3.5 shrink-0 text-[#1C4D8D]" />
                {label}
              </div>
            ))}
          </div>
        </section>
      ) : isLoading ? (
        <section className="flex min-h-[430px] flex-col items-center justify-center rounded-[22px] border border-[#4988C4]/45 bg-white">
          <LoaderCircle className="size-8 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
          <p className="mt-3 text-sm font-semibold text-[#5b6b84]">
            Đang tải Evidence Panel của ca thi...
          </p>
        </section>
      ) : evidence.length === 0 && !error ? (
        <section className="flex min-h-[430px] flex-col items-center justify-center rounded-[22px] border border-dashed border-[#4988C4]/45 bg-white px-5 text-center">
          <UsersRound className="size-7 text-[#8495aa]" />
          <h2 className="mt-3 text-base font-bold text-[#0F2854] sm:text-lg">Ca thi chưa có bài nộp</h2>
          <p className="mt-1 text-xs text-[#5b6b84]">
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
                tone: 'bg-[#4988C4]/16 text-[#1C4D8D]',
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
                tone: 'bg-[#4988C4]/16 text-[#1C4D8D]',
              },
            ].map(({ label, value, helper, icon: Icon, tone }) => (
              <article
                key={label}
                className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_7px_26px_rgba(31,61,105,0.045)]"
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-xs font-medium text-[#5b6b84]">{label}</p>
                    <strong className="mt-2 block text-2xl font-black tracking-[-0.04em] text-[#0F2854]">
                      {String(value).padStart(2, '0')}
                    </strong>
                  </div>
                  <span className={cn('flex size-9 items-center justify-center rounded-xl', tone)}>
                    <Icon className="size-4" />
                  </span>
                </div>
                <p className="mt-1 text-xs text-[#7b899c]">{helper}</p>
              </article>
            ))}
          </section>

          <section className="overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white shadow-[0_9px_30px_rgba(31,61,105,0.05)]">
            <div className="flex flex-col gap-4 border-b border-[#e8edf4] p-4 sm:p-5 lg:flex-row lg:items-center lg:justify-between">
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <p className="text-xs font-bold tracking-[0.08em] text-[#5b6b84] uppercase">
                      Tiến độ ca thi
                    </p>
                    <h2 className="mt-1 text-base font-bold text-[#0F2854] sm:text-lg">
                      {completedScoreCount}/{evidence.length} thí sinh có điểm hoàn chỉnh
                    </h2>
                  </div>
                  <span className="font-mono text-sm font-black text-[#1C4D8D]">{progress}%</span>
                </div>
                <div className="mt-3 h-2 overflow-hidden rounded-full bg-[#4988C4]/16">
                  <div
                    className="h-full rounded-full bg-[linear-gradient(90deg,#4988C4,#49c9db)] transition-[width] duration-500"
                    style={{ width: `${progress}%` }}
                  />
                </div>
              </div>
              <div className="flex shrink-0 gap-2">
                <button
                  type="button"
                  onClick={() => setReloadKey((key) => key + 1)}
                  className="flex size-10 items-center justify-center rounded-xl border border-[#4988C4]/45 text-[#5b6b84] transition hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
                  aria-label="Tải lại bằng chứng"
                >
                  <RefreshCw className="size-4" />
                </button>
                <button
                  type="button"
                  onClick={() => setShowPublishConfirm(true)}
                  disabled={!canPublish}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#168367] px-4 text-xs font-bold text-white shadow-[0_8px_20px_rgba(22,131,103,0.2)] transition hover:bg-[#106c55] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#168367] disabled:cursor-not-allowed disabled:bg-[#a7b7b3] disabled:shadow-none"
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
              <div className="flex items-start gap-2 border-b border-[#ead9ae] bg-[#fff9ec] px-4 py-3 text-xs leading-relaxed text-[#805a20] sm:px-5">
                <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
                {!allScoresComplete
                  ? 'Chưa thể công bố: vẫn còn thí sinh chưa có điểm hoàn chỉnh.'
                  : pendingCount > 0
                    ? 'Chưa thể công bố: vẫn còn bài chưa hoàn tất phân loại độ tin cậy.'
                    : 'Chưa thể công bố: các bài bị đánh dấu nghi ngờ hoặc confidence thấp chưa được hậu kiểm đầy đủ.'}
              </div>
            )}

            <div className="grid min-h-[680px] lg:grid-cols-[330px_minmax(0,1fr)]">
              <aside className="border-b border-[#e4eaf2] bg-white lg:border-r lg:border-b-0">
                <div className="border-b border-[#e5eaf1] p-3">
                  <div className="flex items-center gap-2 text-xs font-bold text-[#667990]">
                    <Filter className="size-3.5" />
                    Phân nhóm hậu kiểm
                  </div>
                  <div className="mt-2 grid grid-cols-2 gap-1 rounded-xl bg-[#4988C4]/16 p-1 lg:grid-cols-1">
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
                          'flex min-h-9 items-center justify-between gap-2 rounded-lg px-2.5 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-[#1C4D8D]',
                          filter === item.value
                            ? 'bg-white text-[#314968] shadow-sm'
                            : 'text-[#74849a] hover:text-[#425774]',
                        )}
                      >
                        <span className="truncate">{item.label}</span>
                        <span className="rounded-full bg-[#f1f4f8] px-1.5 py-0.5 font-mono text-xs">
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
                      <p className="mt-2 text-xs font-bold text-[#5b6b84]">
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
                            'w-full rounded-xl border p-3 text-left transition focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#1C4D8D]',
                            active
                              ? 'border-[#aebbed] bg-white shadow-[0_7px_20px_rgba(69,75,155,0.09)]'
                              : 'border-transparent hover:border-[#dde4ee] hover:bg-white',
                          )}
                        >
                          <div className="flex items-start gap-3">
                            <span
                              className={cn(
                                'flex size-9 shrink-0 items-center justify-center rounded-xl font-mono text-xs font-black',
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
                              <p className="truncate text-xs font-bold text-[#293c57]">
                                {candidate.fullName || 'Chưa có họ tên'}
                              </p>
                              <p className="mt-0.5 font-mono text-xs font-semibold text-[#8190a3]">
                                {candidate.studentCode}
                              </p>
                              <div className="mt-2 flex flex-wrap items-center gap-1.5">
                                <StatusBadge status={candidate.status} />
                                {attention && (
                                  <span className="rounded-full bg-[#fff0f1] px-2 py-1 text-xs font-bold text-[#a93d48]">
                                    Cần xem kỹ
                                  </span>
                                )}
                                {pending && (
                                  <span className="rounded-full bg-[#fff6e5] px-2 py-1 text-xs font-bold text-[#8d6118]">
                                    Chờ phân loại
                                  </span>
                                )}
                              </div>
                            </div>
                            <div className="text-right">
                              <span className="block font-mono text-sm font-black text-[#1C4D8D]">
                                {score === null ? '—' : score.toFixed(1)}
                              </span>
                              <span className="text-xs text-[#8c98a9]">/10</span>
                            </div>
                          </div>
                        </button>
                      )
                    })
                  )}
                </div>
              </aside>

              <div className="min-w-0 bg-[#4988C4]/16 p-3 sm:p-4 lg:p-5">
                {activeCandidate ? (
                  <div className="space-y-4">
                    <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)]">
                      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                        <div className="flex items-center gap-3">
                          <span className="flex size-11 items-center justify-center rounded-2xl bg-[#4988C4]/16 font-mono text-xs font-black text-[#1C4D8D]">
                            {String(activeCandidate.seatNumber).padStart(2, '0')}
                          </span>
                          <div>
                            <h2 className="text-base font-bold text-[#0F2854] sm:text-lg">
                              {activeCandidate.fullName || 'Chưa có họ tên'}
                            </h2>
                            <p className="mt-0.5 font-mono text-xs font-semibold text-[#74859b]">
                              {activeCandidate.studentCode}
                            </p>
                          </div>
                        </div>
                        <div className="flex flex-wrap items-center gap-2">
                          <StatusBadge status={activeCandidate.status} />
                          {activeCandidate.isLocked && (
                            <span className="inline-flex items-center gap-1 rounded-full bg-[#fff0f1] px-2 py-1 text-xs font-bold text-[#a93d48]">
                              <LockKeyhole className="size-3" />
                              One-Way Lock
                            </span>
                          )}
                        </div>
                      </div>

                      <div className="mt-4 grid gap-3 sm:grid-cols-3">
                        <div className="rounded-xl border border-[#e2e8f0] bg-white p-3">
                          <p className="flex items-center gap-1.5 text-xs font-bold text-[#79889b] uppercase">
                            <Bot className="size-3.5 text-[#1C4D8D]" />
                            Điểm AI
                          </p>
                          <p className="mt-2 font-mono text-lg font-black text-[#0F2854]">
                            {activeCandidate.aiScore === null
                              ? '—'
                              : activeCandidate.aiScore.toFixed(1)}
                            <span className="ml-1 text-xs text-[#8290a2]">/10</span>
                          </p>
                        </div>
                        <div className="rounded-xl border border-[#e2e8f0] bg-white p-3">
                          <p className="flex items-center gap-1.5 text-xs font-bold text-[#79889b] uppercase">
                            <Gauge className="size-3.5 text-[#1C4D8D]" />
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
                        <div className="rounded-xl border border-[#e2e8f0] bg-white p-3">
                          <p className="flex items-center gap-1.5 text-xs font-bold text-[#79889b] uppercase">
                            <UserRoundCheck className="size-3.5 text-[#168367]" />
                            Điểm hiện tại
                          </p>
                          <p className="mt-2 font-mono text-lg font-black text-[#0F2854]">
                            {getDisplayedScore(activeCandidate) === null
                              ? '—'
                              : getDisplayedScore(activeCandidate)?.toFixed(1)}
                            <span className="ml-1 text-xs text-[#8290a2]">/10</span>
                          </p>
                        </div>
                      </div>

                      {needsLecturerAttention(activeCandidate) && (
                        <div className="mt-3 flex items-start gap-2 rounded-xl border border-[#efc8cc] bg-[#fff5f6] p-3 text-xs leading-relaxed text-[#953943]">
                          <ShieldAlert className="mt-0.5 size-3.5 shrink-0" />
                          {activeCandidate.suspiciousReason ||
                            'Bài nộp bị đánh dấu cần can thiệp; giảng viên phải đối chiếu toàn bộ bằng chứng.'}
                        </div>
                      )}
                      {isPendingEvaluation(activeCandidate) &&
                        !needsLecturerAttention(activeCandidate) && (
                          <div className="mt-3 flex items-start gap-2 rounded-xl border border-[#ead9ae] bg-[#fff9ec] p-3 text-xs leading-relaxed text-[#805a20]">
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
                      <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)] sm:p-5">
                        <div className="flex items-center gap-2">
                          <FileSearch className="size-4 text-[#1C4D8D]" />
                          <h3 className="text-sm font-bold text-[#0F2854]">Transcript Whisper</h3>
                        </div>
                        <div className="mt-3 max-h-72 overflow-y-auto rounded-xl border border-[#e2e8f0] bg-white p-3">
                          <p className="text-xs leading-[1.7] whitespace-pre-wrap text-[#5b6b84]">
                            {activeCandidate.transcriptWhisper ||
                              'Chưa có transcript để giảng viên đối chiếu.'}
                          </p>
                        </div>
                      </section>

                      <section className="rounded-2xl border border-[#4988C4]/45 bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)] sm:p-5">
                        <div className="flex items-center gap-2">
                          <Sparkles className="size-4 text-[#1C4D8D]" />
                          <h3 className="text-sm font-bold text-[#0F2854]">
                            Phân tích AI theo tiêu chí
                          </h3>
                        </div>
                        <div className="mt-3 max-h-72 overflow-y-auto rounded-xl border border-[#e2e8f0] bg-white p-3">
                          <p className="text-xs leading-[1.7] whitespace-pre-wrap text-[#5b6b84]">
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
                    <p className="mt-3 text-xs font-semibold text-[#687a91]">
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
        <ConfirmDialog
          tone="danger"
          icon={LockKeyhole}
          eyebrow="Không thể hoàn tác"
          title="Xác nhận công bố điểm toàn ca"
          message="Thao tác này kích hoạt khóa một chiều. Sau khi xác nhận, điểm đã công bố không sửa hoặc xóa được."
          confirmLabel={isPublishing ? 'Đang công bố…' : 'Công bố và khóa điểm'}
          cancelLabel="Hủy"
          pending={isPublishing}
          onCancel={() => setShowPublishConfirm(false)}
          onConfirm={() => void publishGrades()}
        >
          <div className="grid grid-cols-2 gap-3">
            <div className="rounded-xl border border-[#4988C4]/45 bg-white p-3 text-center">
              <p className="text-xs font-bold text-[#5b6b84] uppercase">Sẽ công bố</p>
              <p className="mt-1 font-mono text-lg font-black text-[#1C4D8D]">{evidence.length}</p>
              <p className="text-xs text-[#5b6b84]">sinh viên</p>
            </div>
            <div className="rounded-xl border border-[#4988C4]/45 bg-white p-3 text-center">
              <p className="text-xs font-bold text-[#5b6b84] uppercase">Đã hậu kiểm</p>
              <p className="mt-1 font-mono text-lg font-black text-[#16836a]">
                {reviewedAttentionCount}/{attentionCount}
              </p>
              <p className="text-xs text-[#5b6b84]">bài cần can thiệp</p>
            </div>
          </div>
        </ConfirmDialog>
      )}
    </div>
  )
}

export { AuditEvidencePage as AuditPortalPage }
