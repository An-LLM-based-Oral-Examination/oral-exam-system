import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  BadgeCheck,
  Bot,
  CheckCircle2,
  ClipboardCheck,
  FileSearch,
  Fingerprint,
  Gauge,
  Headphones,
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
} from 'lucide-react'
import { WaveformPlayer } from '@/components/audio/WaveformPlayer'
import { ScoreOverrideForm } from '@/components/rubric/ScoreOverrideForm'
import { OverlayDrawer, OverlayModal } from '@/components/ui/OverlayDrawer'
import { officialExamService } from '@/services/officialExam.service'
import { useAuth } from '@/hooks/useAuth'
import type { AuditEvidenceItem, ExamTicketStatus } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'

type AuditFilter = 'all' | 'attention' | 'high-confidence' | 'pending'

const REVIEWED_STATUSES: ExamTicketStatus[] = ['AUDITED', 'PUBLISHED', 'LOCKED']

const STATUS_META: Record<ExamTicketStatus, { label: string; className: string }> = {
  SCHEDULED: { label: 'Đã xếp lịch', className: 'bg-[#f0f3f7] text-[#5b6c82]' },
  IN_PROGRESS: { label: 'Đang thi', className: 'bg-[#eef3ff] text-[#315fca]' },
  SUBMITTED: { label: 'Đã nộp', className: 'bg-[#eef3ff] text-[#315fca]' },
  AI_GRADED: { label: 'AI đã chấm', className: 'bg-[#FFF7ED] text-[#C2410C]' },
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
    <span className={cn('rounded-full px-2.5 py-0.5 text-xs font-bold', meta.className)}>
      {meta.label}
    </span>
  )
}

const DEFAULT_SHIFT_ID = '00000000-0000-0000-0000-000000000023'

const PRESET_SHIFTS = [
  {
    id: '00000000-0000-0000-0000-000000000023',
    label: 'Kíp 1 (08h00 - 09h30) · Phòng Lab 301 · PRN231',
    subtitle: 'Danh sách thí sinh: SE170001 Lê Vũ Hoàng & SE170002 Trần Thị Mai',
  },
]

export function AuditEvidencePage() {
  const { user } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const rawShiftParam = searchParams.get('shiftId') ?? ''
  const routeShiftId = isUuid(rawShiftParam) ? rawShiftParam : DEFAULT_SHIFT_ID
  const [shiftInput, setShiftInput] = useState(routeShiftId)
  const [evidence, setEvidence] = useState<AuditEvidenceItem[]>([])
  const [selectedTicketId, setSelectedTicketId] = useState('')
  const [isDrawerOpen, setIsDrawerOpen] = useState(false)
  const [filter, setFilter] = useState<AuditFilter>('all')
  const [isLoading, setIsLoading] = useState(false)
  const [isPublishing, setIsPublishing] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [showPublishConfirm, setShowPublishConfirm] = useState(false)

  useEffect(() => {
    if (!rawShiftParam) {
      setSearchParams({ shiftId: DEFAULT_SHIFT_ID }, { replace: true })
    }
  }, [rawShiftParam, setSearchParams])

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

  const activeCandidate =
    evidence.find((candidate) => candidate.ticketId === selectedTicketId) ?? null

  const openCandidateDrawer = (ticketId: string) => {
    setSelectedTicketId(ticketId)
    setIsDrawerOpen(true)
  }

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

  const selectPresetShift = (presetId: string) => {
    setShiftInput(presetId)
    setError('')
    setSearchParams({ shiftId: presetId })
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
      <section className="overflow-hidden rounded-2xl border border-[#E2E8F0] border-t-4 border-t-[#F26F21] bg-white p-5 shadow-sm sm:p-6">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1 text-xs font-bold tracking-wide text-[#C2410C] uppercase">
              <ClipboardCheck className="size-3.5" aria-hidden="true" />
              Thẩm định Bài thi & Công bố Điểm
            </div>
            <h1 className="mt-2.5 text-xl font-bold tracking-tight text-[#1E293B] sm:text-2xl">
              Hậu kiểm & công bố điểm ca thi
            </h1>
            <p className="mt-1.5 max-w-3xl text-xs leading-relaxed text-[#475569] sm:text-sm">
              Chọn ca thi, rà soát danh sách thí sinh và mở ngăn kéo bằng chứng để nghe lại Audio
              R2, đối chiếu Transcript Whisper & AI CoT trước khi công bố điểm.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <button
              type="button"
              onClick={() => setReloadKey((key) => key + 1)}
              className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-[#CBD5E1] bg-white px-3.5 text-xs font-bold text-[#475569] transition hover:border-[#FED7AA] hover:bg-[#FFF7ED] hover:text-[#C2410C] sm:text-sm"
            >
              <RefreshCw className="size-4" />
              Làm mới
            </button>
            <button
              type="button"
              onClick={() => setShowPublishConfirm(true)}
              disabled={!canPublish}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-sm transition hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:bg-[#CBD5E1] disabled:text-[#64748B] disabled:shadow-none sm:text-sm"
            >
              {alreadyPublished ? (
                <LockKeyhole className="size-4" />
              ) : (
                <Send className="size-4" />
              )}
              {alreadyPublished
                ? 'Đã công bố & Khóa sổ điểm'
                : 'Công bố điểm ca thi'}
            </button>
          </div>
        </div>

        <div className="mt-4 flex flex-col gap-3 border-t border-[#E2E8F0] pt-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs font-bold text-[#475569]">Ca thi phân công:</span>
            {PRESET_SHIFTS.map((shift) => {
              const isSelected = routeShiftId.toLowerCase() === shift.id.toLowerCase()
              return (
                <button
                  key={shift.id}
                  type="button"
                  onClick={() => selectPresetShift(shift.id)}
                  className={cn(
                    'inline-flex items-center gap-2 rounded-xl border px-3 py-1.5 text-xs font-bold transition',
                    isSelected
                      ? 'border-[#F26F21] bg-[#FFF7ED] text-[#C2410C]'
                      : 'border-[#CBD5E1] bg-[#F8FAFC] text-[#334155] hover:bg-white',
                  )}
                >
                  <BadgeCheck className="size-3.5 text-[#F26F21]" />
                  {shift.label}
                </button>
              )
            })}
          </div>

          <form onSubmit={openShift} className="flex w-full max-w-md gap-2">
            <label className="relative min-w-0 flex-1">
              <span className="sr-only">Shift ID của ca thi</span>
              <Fingerprint className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#64748B]" />
              <input
                value={shiftInput}
                onChange={(event) => setShiftInput(event.target.value)}
                placeholder="Nhập UUID ca thi (Shift ID)"
                className="min-h-10 w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] pr-3 pl-9 font-mono text-xs text-[#1E293B] transition outline-none placeholder:font-sans placeholder:text-[#94A3B8] focus:border-[#F26F21] focus:bg-white focus:ring-2 focus:ring-[#FED7AA]"
              />
            </label>
            <button
              type="submit"
              className="inline-flex min-h-10 shrink-0 items-center justify-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 text-xs font-bold text-[#C2410C] transition hover:bg-[#F26F21] hover:text-white"
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
            'flex items-start gap-2 rounded-2xl border px-4 py-3 text-xs font-semibold sm:text-sm',
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
        <section className="flex min-h-80 flex-col items-center justify-center rounded-2xl border border-dashed border-[#CBD5E1] bg-white px-5 text-center shadow-sm">
          <span className="flex size-14 items-center justify-center rounded-2xl border border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]">
            <FileSearch className="size-6" aria-hidden="true" />
          </span>
          <h2 className="mt-4 text-base font-bold text-[#1E293B] sm:text-lg">
            Chọn ca thi cần hậu kiểm
          </h2>
          <p className="mt-2 max-w-lg text-xs leading-relaxed text-[#64748B] sm:text-sm">
            Nhập Shift ID hợp lệ từ lịch phân công để tải danh sách bài thi và bằng chứng.
          </p>
        </section>
      ) : isLoading ? (
        <section className="flex min-h-80 flex-col items-center justify-center rounded-2xl border border-[#E2E8F0] bg-white">
          <LoaderCircle className="size-8 animate-spin text-[#F26F21] motion-reduce:animate-none" />
          <p className="mt-3 text-xs font-semibold text-[#475569] sm:text-sm">
            Đang tải dữ liệu hậu kiểm của ca thi...
          </p>
        </section>
      ) : evidence.length === 0 && !error ? (
        <section className="flex min-h-80 flex-col items-center justify-center rounded-2xl border border-dashed border-[#CBD5E1] bg-white px-5 text-center">
          <UsersRound className="size-7 text-[#64748B]" />
          <h2 className="mt-3 text-base font-bold text-[#1E293B]">Ca thi chưa có bài nộp</h2>
          <p className="mt-1 text-xs text-[#64748B] sm:text-sm">
            Chưa có dữ liệu thí sinh để giảng viên hậu kiểm.
          </p>
        </section>
      ) : evidence.length > 0 ? (
        <>
          <section className="grid gap-3.5 sm:grid-cols-2 xl:grid-cols-4">
            {[
              {
                label: 'Tổng số bài nộp',
                value: evidence.length,
                helper: `${completedScoreCount}/${evidence.length} bài đã có điểm (${progress}%)`,
                icon: UsersRound,
                tone: 'border border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]',
              },
              {
                label: 'Nhóm 1: Cần xem lại / Nghi ngờ',
                value: attentionCount,
                helper: `${reviewedAttentionCount}/${attentionCount} đã hậu kiểm (< 0.70)`,
                icon: ShieldAlert,
                tone: 'border border-rose-200 bg-rose-50 text-[#BE123C]',
              },
              {
                label: 'Nhóm 2: Tin cậy cao',
                value: highConfidenceCount,
                helper: 'Confidence ≥ 70%, sẵn sàng công bố',
                icon: UserRoundCheck,
                tone: 'border border-emerald-200 bg-emerald-50 text-[#047857]',
              },
              {
                label: 'Trạng thái khóa điểm (is_locked)',
                value: lockedCount,
                helper: alreadyPublished ? 'Đã khóa sổ điểm' : 'Chờ công bố toàn ca',
                icon: LockKeyhole,
                tone: 'border border-[#FED7AA] bg-[#FFF7ED] text-[#F26F21]',
              },
            ].map(({ label, value, helper, icon: Icon, tone }) => (
              <article
                key={label}
                className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-sm"
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-xs font-semibold text-[#475569]">{label}</p>
                    <strong className="mt-2 block text-2xl font-black tracking-tight text-[#1E293B] sm:text-3xl">
                      {String(value).padStart(2, '0')}
                    </strong>
                  </div>
                  <span className={cn('flex size-10 items-center justify-center rounded-xl', tone)}>
                    <Icon className="size-5" />
                  </span>
                </div>
                <p className="mt-1.5 text-xs text-[#64748B]">{helper}</p>
              </article>
            ))}
          </section>

          <section className="overflow-hidden rounded-2xl border border-[#E2E8F0] bg-white shadow-sm">
            <div className="flex flex-col gap-3 border-b border-[#E2E8F0] px-4 py-4 sm:px-5 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex flex-wrap items-center gap-2">
                {[
                  { value: 'all' as const, label: 'Tất cả', count: evidence.length },
                  {
                    value: 'attention' as const,
                    label: 'Nhóm 1: Cần hậu kiểm / Nghi ngờ',
                    count: attentionCount,
                  },
                  {
                    value: 'high-confidence' as const,
                    label: 'Nhóm 2: Tin cậy cao',
                    count: highConfidenceCount,
                  },
                  ...(pendingCount > 0
                    ? [{ value: 'pending' as const, label: 'Chờ AI chấm', count: pendingCount }]
                    : []),
                ].map((tab) => {
                  const isActive = filter === tab.value
                  return (
                    <button
                      key={tab.value}
                      type="button"
                      onClick={() => setFilter(tab.value)}
                      className={cn(
                        'inline-flex min-h-9 items-center gap-2 rounded-xl border px-3.5 text-xs font-bold transition focus-visible:outline-2 focus-visible:outline-[#F26F21]',
                        isActive
                          ? 'border-[#F26F21] bg-[#FFF7ED] text-[#C2410C]'
                          : 'border-[#E2E8F0] bg-[#F8FAFC] text-[#475569] hover:border-[#FED7AA] hover:bg-white',
                      )}
                    >
                      <span>{tab.label}</span>
                      <span
                        className={cn(
                          'rounded-full px-2 py-0.5 font-mono text-xs',
                          isActive
                            ? 'bg-[#F26F21] text-white'
                            : 'bg-white text-[#475569]',
                        )}
                      >
                        {tab.count}
                      </span>
                    </button>
                  )
                })}
              </div>

              <div className="flex items-center gap-2 text-xs font-semibold text-[#64748B]">
                <span>Tiến độ chấm toàn ca:</span>
                <span className="font-mono font-bold text-[#F26F21]">{progress}%</span>
              </div>
            </div>

            {!canPublish && !alreadyPublished && (
              <div className="flex items-start gap-2 border-b border-[#FED7AA] bg-[#FFF7ED] px-4 py-3 text-xs leading-relaxed text-[#9A3412] sm:px-5">
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                {!allScoresComplete
                  ? 'Chưa thể công bố: vẫn còn thí sinh chưa có điểm hoàn chỉnh.'
                  : pendingCount > 0
                    ? 'Chưa thể công bố: vẫn còn bài chưa hoàn tất phân loại độ tin cậy.'
                    : 'Chưa thể công bố: các bài thuộc Nhóm 1 (nghi ngờ hoặc confidence < 0.70) cần được hậu kiểm đầy đủ.'}
              </div>
            )}

            {filteredEvidence.length === 0 ? (
              <div className="flex min-h-56 flex-col items-center justify-center px-5 text-center">
                <BadgeCheck className="size-7 text-[#10B981]" />
                <p className="mt-2 text-sm font-bold text-[#1E293B]">
                  Không có bài thi nào trong nhóm lọc này
                </p>
                <p className="mt-1 text-xs text-[#64748B]">
                  Hãy chuyển sang tab khác để xem danh sách bài thi của sinh viên.
                </p>
              </div>
            ) : (
              <div className="divide-y divide-[#E2E8F0]">
                {filteredEvidence.map((candidate) => {
                  const attention = needsLecturerAttention(candidate)
                  const pending = isPendingEvaluation(candidate)
                  const score = getDisplayedScore(candidate)
                  return (
                    <article
                      key={candidate.ticketId}
                      className="flex flex-col gap-3 px-4 py-4 transition hover:bg-[#FFF7ED]/40 sm:px-5 lg:flex-row lg:items-center lg:justify-between"
                    >
                      <div className="flex min-w-0 items-start gap-3.5">
                        <span
                          className={cn(
                            'flex size-10 shrink-0 items-center justify-center rounded-xl border font-mono text-xs font-black',
                            attention
                              ? 'border-rose-200 bg-rose-50 text-[#BE123C]'
                              : pending
                                ? 'border-amber-200 bg-amber-50 text-[#B45309]'
                                : 'border-[#FED7AA] bg-[#FFF7ED] text-[#C2410C]',
                          )}
                        >
                          {String(candidate.seatNumber).padStart(2, '0')}
                        </span>
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <h3 className="text-sm font-bold text-[#1E293B]">
                              {candidate.fullName || 'Chưa có họ tên'}
                            </h3>
                            <span className="rounded-md border border-[#E2E8F0] bg-[#F8FAFC] px-2 py-0.5 font-mono text-xs font-bold text-[#475569]">
                              {candidate.studentCode}
                            </span>
                            <StatusBadge status={candidate.status} />
                            {attention && (
                              <span className="rounded-full border border-rose-200 bg-rose-50 px-2.5 py-0.5 text-xs font-bold text-[#BE123C]">
                                Nhóm 1 · Cần hậu kiểm
                              </span>
                            )}
                            {candidate.isLocked && (
                              <span className="inline-flex items-center gap-1 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-0.5 text-xs font-bold text-[#C2410C]">
                                <LockKeyhole className="size-3" />
                                Đã khóa
                              </span>
                            )}
                          </div>
                          <p className="mt-1 line-clamp-1 text-xs text-[#64748B]">
                            {candidate.suspiciousReason
                              ? `Lý do cần xem lại: ${candidate.suspiciousReason}`
                              : `Audio: ${String(candidate.seatNumber).padStart(2, '0')}_${candidate.studentCode}.webm · Độ tin cậy AI: ${formatConfidence(candidate.confidenceScore)}`}
                          </p>
                        </div>
                      </div>

                      <div className="flex flex-wrap items-center justify-between gap-4 lg:justify-end">
                        <div className="flex items-center gap-4">
                          <div className="text-right">
                            <span className="block text-xs font-semibold text-[#64748B]">
                              Confidence
                            </span>
                            <span
                              className={cn(
                                'font-mono text-xs font-bold',
                                candidate.confidenceScore !== null &&
                                  candidate.confidenceScore < 0.7
                                  ? 'text-[#BE123C]'
                                  : 'text-[#047857]',
                              )}
                            >
                              {formatConfidence(candidate.confidenceScore)}
                            </span>
                          </div>
                          <div className="text-right">
                            <span className="block text-xs font-semibold text-[#64748B]">
                              Điểm số
                            </span>
                            <span className="font-mono text-sm font-black text-[#1E293B]">
                              {score === null ? '—' : score.toFixed(1)}
                              <span className="text-xs font-normal text-[#64748B]">/10</span>
                            </span>
                          </div>
                        </div>

                        <button
                          type="button"
                          onClick={() => openCandidateDrawer(candidate.ticketId)}
                          className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 text-xs font-bold text-[#C2410C] transition hover:bg-[#F26F21] hover:text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21]"
                        >
                          <Headphones className="size-3.5" aria-hidden="true" />
                          🎧 Mở Bằng chứng & Chấm lại
                        </button>
                      </div>
                    </article>
                  )
                })}
              </div>
            )}
          </section>
        </>
      ) : null}

      <OverlayDrawer
        open={isDrawerOpen && Boolean(activeCandidate)}
        onClose={() => setIsDrawerOpen(false)}
        title={
          activeCandidate
            ? `Máy ${String(activeCandidate.seatNumber).padStart(2, '0')} · ${activeCandidate.fullName || 'Thí sinh'} (${activeCandidate.studentCode})`
            : 'Bằng chứng hậu kiểm bài thi'
        }
        subtitle={
          activeCandidate
            ? `File ghi âm R2: ${String(activeCandidate.seatNumber).padStart(2, '0')}_${activeCandidate.studentCode}.webm · Trạng thái: ${STATUS_META[activeCandidate.status].label}`
            : undefined
        }
        badge={
          activeCandidate && needsLecturerAttention(activeCandidate)
            ? 'Nhóm 1 · Cần hậu kiểm kỹ'
            : 'Hồ sơ Bài làm Thí sinh'
        }
        width="2xl"
      >
        {activeCandidate && (
          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
                <p className="flex items-center gap-1.5 text-xs font-bold text-[#64748B] uppercase">
                  <Bot className="size-4 text-[#F26F21]" />
                  Điểm AI chấm
                </p>
                <p className="mt-1.5 font-mono text-xl font-black text-[#1E293B]">
                  {activeCandidate.aiScore === null ? '—' : activeCandidate.aiScore.toFixed(1)}
                  <span className="ml-1 text-xs text-[#64748B]">/10</span>
                </p>
              </div>
              <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
                <p className="flex items-center gap-1.5 text-xs font-bold text-[#64748B] uppercase">
                  <Gauge className="size-4 text-[#C2410C]" />
                  Độ tin cậy AI
                </p>
                <p
                  className={cn(
                    'mt-1.5 font-mono text-xl font-black',
                    activeCandidate.confidenceScore !== null &&
                      activeCandidate.confidenceScore < 0.7
                      ? 'text-[#BE123C]'
                      : 'text-[#047857]',
                  )}
                >
                  {formatConfidence(activeCandidate.confidenceScore)}
                </p>
              </div>
              <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
                <p className="flex items-center gap-1.5 text-xs font-bold text-[#64748B] uppercase">
                  <UserRoundCheck className="size-4 text-[#047857]" />
                  Điểm hiện tại
                </p>
                <p className="mt-1.5 font-mono text-xl font-black text-[#1E293B]">
                  {getDisplayedScore(activeCandidate) === null
                    ? '—'
                    : getDisplayedScore(activeCandidate)?.toFixed(1)}
                  <span className="ml-1 text-xs text-[#64748B]">/10</span>
                </p>
              </div>
            </div>

            {needsLecturerAttention(activeCandidate) && (
              <div className="flex items-start gap-2 rounded-xl border border-[#FDA4AF] bg-[#FFF1F2] p-3.5 text-xs leading-relaxed text-[#9F1239] sm:text-sm">
                <ShieldAlert className="mt-0.5 size-4 shrink-0" />
                {activeCandidate.suspiciousReason ||
                  'Bài nộp bị đánh dấu cần can thiệp; giảng viên phải đối chiếu toàn bộ bằng chứng.'}
              </div>
            )}

            <WaveformPlayer
              audioUrl={activeCandidate.audioR2Url}
              hashSha256={activeCandidate.audioHashSha256}
              studentCode={activeCandidate.studentCode}
              seatNumber={activeCandidate.seatNumber}
            />

            <div className="grid gap-4 lg:grid-cols-2">
              <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-2xs">
                <div className="flex items-center gap-2">
                  <FileSearch className="size-4 text-[#F26F21]" />
                  <h3 className="text-sm font-bold text-[#1E293B]">
                    Transcript gốc từ Whisper STT
                  </h3>
                </div>
                <div className="mt-3 max-h-72 overflow-y-auto rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
                  <p className="text-xs leading-relaxed whitespace-pre-wrap text-[#334155] sm:text-sm">
                    {activeCandidate.transcriptWhisper ||
                      'Chưa có transcript để giảng viên đối chiếu.'}
                  </p>
                </div>
              </section>

              <section className="rounded-2xl border border-[#E2E8F0] bg-white p-4 shadow-2xs">
                <div className="flex items-center gap-2">
                  <Sparkles className="size-4 text-[#F26F21]" />
                  <h3 className="text-sm font-bold text-[#1E293B]">
                    Chuỗi lập luận AI (Chain-of-Thought) & Tiêu chí
                  </h3>
                </div>
                <div className="mt-3 max-h-72 overflow-y-auto rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
                  <p className="text-xs leading-relaxed whitespace-pre-wrap text-[#334155] sm:text-sm">
                    {activeCandidate.cotTrace ||
                      'Chưa có phân tích AI để hiển thị trong hồ sơ bài làm.'}
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
        )}
      </OverlayDrawer>

      <OverlayModal
        open={showPublishConfirm}
        onClose={() => {
          if (!isPublishing) setShowPublishConfirm(false)
        }}
        title="Xác nhận công bố điểm toàn ca"
        subtitle="Sau khi công bố, bảng điểm ca thi sẽ được khóa chính thức và hiển thị trên cổng thông tin sinh viên"
        badge="Xác nhận Công bố Điểm"
        size="md"
        footer={
          <div className="flex justify-end gap-2.5">
            <button
              type="button"
              onClick={() => setShowPublishConfirm(false)}
              disabled={isPublishing}
              className="inline-flex min-h-10 items-center justify-center rounded-xl border border-[#CBD5E1] bg-white px-4 text-xs font-bold text-[#334155] hover:bg-[#F1F5F9] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-40 sm:text-sm"
            >
              Hủy
            </button>
            <button
              type="button"
              onClick={() => void publishGrades()}
              disabled={isPublishing}
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-sm hover:bg-[#D95B16] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#F26F21] disabled:cursor-not-allowed disabled:opacity-60 sm:text-sm"
            >
              {isPublishing ? (
                <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
              ) : (
                <LockKeyhole className="size-4" />
              )}
              {isPublishing ? 'Đang công bố...' : 'Công bố & khóa điểm'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3 text-center">
              <p className="text-xs font-bold text-[#64748B] uppercase">Sẽ công bố</p>
              <p className="mt-1 font-mono text-xl font-black text-[#1E293B]">
                {evidence.length}
              </p>
              <p className="text-xs text-[#64748B]">sinh viên</p>
            </div>
            <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3 text-center">
              <p className="text-xs font-bold text-[#64748B] uppercase">
                Đã hậu kiểm ưu tiên
              </p>
              <p className="mt-1 font-mono text-xl font-black text-[#047857]">
                {reviewedAttentionCount}/{attentionCount}
              </p>
              <p className="text-xs text-[#64748B]">bài cần can thiệp</p>
            </div>
          </div>
          <div className="flex items-start gap-2 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-3.5 text-xs leading-relaxed text-[#9A3412]">
            <ShieldCheck className="mt-0.5 size-4 shrink-0 text-[#F26F21]" />
            Sau khi xác nhận công bố, điểm số sẽ được khóa chính thức. Mọi điều chỉnh điểm sau thời
            điểm này chỉ được thực hiện thông qua quy trình chấm phúc khảo do Trưởng Bộ môn phân
            công.
          </div>
        </div>
      </OverlayModal>
    </div>
  )
}

export { AuditEvidencePage as AuditPortalPage }
