import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  BadgeCheck,
  Bot,
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
  Download,
} from 'lucide-react'
import { WaveformPlayer } from '@/components/audio/WaveformPlayer'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { StatusToast } from '@/components/common/toast'
import { ScoreOverrideForm } from '@/components/rubric/ScoreOverrideForm'
import { officialExamService } from '@/services/officialExam.service'
import { useAuth } from '@/hooks/useAuth'
import type { AuditEvidenceItem, ExamTicketStatus } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { EmptyState } from '@/components/common/EmptyState'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
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
  return userMessage(error, 'Không hoàn tất được yêu cầu hậu kiểm. Vui lòng thử lại.')
}

function isUuid(value: string) {
  return /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value)
}

function getDisplayedScore(candidate: AuditEvidenceItem) {
  return candidate.finalScore ?? candidate.aiScore
}

function needsLecturerAttention(candidate: AuditEvidenceItem) {
  const effectiveScore = candidate.finalScore ?? candidate.aiScore
  return (
    candidate.isSuspicious ||
    (candidate.confidenceScore !== null && candidate.confidenceScore < 0.7) ||
    (effectiveScore !== null && effectiveScore < 5.0)
  )
}

function hasCompleteScore(candidate: AuditEvidenceItem) {
  return candidate.finalScore !== null || candidate.aiScore !== null
}

function isPendingEvaluation(candidate: AuditEvidenceItem) {
  return (
    !hasCompleteScore(candidate) ||
    (candidate.confidenceScore === null && !REVIEWED_STATUSES.includes(candidate.status as ExamTicketStatus))
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
  const [isSavingOverride, setIsSavingOverride] = useState(false)
  const [isLocking, setIsLocking] = useState(false)
  const [isExporting, setIsExporting] = useState<'excel' | 'pdf' | null>(null)
  const [shiftInputError, setShiftInputError] = useState('')
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

    // ConfirmDialog tự khóa scroll; chỉ giữ Escape phụ nếu cần.
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isPublishing) setShowPublishConfirm(false)
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [isPublishing, showPublishConfirm])

  const attentionCount = evidence.filter(needsLecturerAttention).length
  const reviewedAttentionCount = evidence.filter(
    (candidate) =>
      needsLecturerAttention(candidate) && REVIEWED_STATUSES.includes(candidate.status as ExamTicketStatus),
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
    if (!value) {
      setShiftInputError('Vui lòng nhập Shift ID của ca thi.')
      return
    }
    if (!isUuid(value)) {
      setShiftInputError('Shift ID phải là một UUID hợp lệ.')
      return
    }
    setShiftInputError('')
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

  const saveOverride = async (newScore: number, reason: string) => {
    if (!activeCandidate) return
    setIsSavingOverride(true)
    setError('')
    setNotice('')
    try {
      const service = officialExamService as typeof officialExamService & {
        overrideScore: (ticketId: string, payload: { auditedScore: number; overrideReason: string }) => Promise<unknown>
      }
      await service.overrideScore(activeCandidate.ticketId, {
        auditedScore: newScore,
        overrideReason: reason,
      })
      setNotice('Đã lưu điểm hậu kiểm và lý do giải trình.')
      setReloadKey((key) => key + 1)
    } catch (overrideError) {
      setError(getErrorMessage(overrideError))
    } finally {
      setIsSavingOverride(false)
    }
  }

  const lockTicket = async () => {
    if (!activeCandidate || activeCandidate.isLocked) return
    setIsLocking(true)
    setError('')
    setNotice('')
    try {
      const service = officialExamService as typeof officialExamService & {
        lockTicket: (ticketId: string) => Promise<unknown>
      }
      await service.lockTicket(activeCandidate.ticketId)
      setNotice('Đã khóa một chiều điểm của thí sinh.')
      setReloadKey((key) => key + 1)
    } catch (lockError) {
      setError(getErrorMessage(lockError))
    } finally {
      setIsLocking(false)
    }
  }

  const exportShift = async (kind: 'excel' | 'pdf') => {
    if (!routeShiftId) return
    setIsExporting(kind)
    setError('')
    try {
      const service = officialExamService as typeof officialExamService & {
        exportShiftExcel: (shiftId: string) => Promise<{ blob: Blob }>
        exportShiftPdf: (shiftId: string) => Promise<{ blob: Blob }>
      }
      const file = kind === 'excel'
        ? await service.exportShiftExcel(routeShiftId)
        : await service.exportShiftPdf(routeShiftId)
      const blob = file instanceof Blob ? file : (file as { blob: Blob }).blob
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = `official-exam-${routeShiftId}.${kind === 'excel' ? 'xlsx' : 'pdf'}`
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (exportError) {
      setError(getErrorMessage(exportError))
    } finally {
      setIsExporting(null)
    }
  }

  const progress =
    evidence.length === 0 ? 0 : Math.round((completedScoreCount / evidence.length) * 100)

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={ClipboardCheck}
        eyebrow="Hậu kiểm điểm"
        title="Hậu kiểm và công bố điểm ca thi"
        description="Ưu tiên bài được hệ thống đánh dấu nghi ngờ, điểm dưới 5.0 hoặc độ tin cậy dưới 0.70. Đối chiếu bằng chứng trước khi công bố điểm một lần cho toàn ca."
        extras={
          <>
            <BannerFact icon={ShieldAlert}>Bài nghi ngờ</BannerFact>
            <BannerFact icon={FileSearch} tone="green">
              Đối chiếu bằng chứng
            </BannerFact>
            <BannerFact icon={LockKeyhole} tone="violet">
              Công bố một lần
            </BannerFact>
          </>
        }
      />

      <section className={ds.section}>
        <SectionHeader
          icon={Fingerprint}
          title="Mở ca thi"
          hint="Nhập mã ca để xem bài cần hậu kiểm"
        />
        <div className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center">
          <form onSubmit={openShift} className="flex min-w-0 flex-1 flex-col gap-2 sm:flex-row sm:items-start">
            <label className="block min-w-0 flex-1">
              <span className="sr-only">Shift ID của ca thi</span>
              <span className="relative block">
                <Fingerprint className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#7c8ca1]" />
                <input
                  value={shiftInput}
                  onChange={(event) => {
                    setShiftInput(event.target.value)
                    if (shiftInputError) setShiftInputError('')
                  }}
                  placeholder="Nhập UUID ca thi (Shift ID)"
                  aria-invalid={Boolean(shiftInputError)}
                  className={cn(
                    ds.field,
                    'pr-3 pl-9 font-mono text-xs placeholder:font-sans',
                    shiftInputError && fieldInvalidClass,
                  )}
                />
              </span>
              <FieldError message={shiftInputError} />
            </label>
            <button type="submit" className={ds.primaryButton}>
              <Search className="size-3.5" />
              Mở ca thi
            </button>
          </form>
          {routeShiftId && isUuid(routeShiftId) && (
            <div className="flex flex-wrap gap-2">
              <button type="button" className={ds.outlineButton} disabled={Boolean(isExporting)} onClick={() => void exportShift('excel')}>
                <Download className="size-3.5" />
                {isExporting === 'excel' ? 'Đang xuất...' : 'Xuất Excel'}
              </button>
              <button type="button" className={ds.outlineButton} disabled={Boolean(isExporting)} onClick={() => void exportShift('pdf')}>
                <Download className="size-3.5" />
                {isExporting === 'pdf' ? 'Đang xuất...' : 'Xuất PDF'}
              </button>
            </div>
          )}
        </div>
      </section>

      <StatusToast
        notice={notice}
        error={error}
        onDismiss={() => {
          setError('')
          setNotice('')
        }}
      />

      {!routeShiftId || !isUuid(routeShiftId) ? (
        <EmptyState
          className="min-h-[430px] justify-center"
          icon={FileSearch}
          title="Chọn ca thi cần hậu kiểm"
          description="Nhập Shift ID từ lịch phân công. Hệ thống chỉ tải bằng chứng sau khi nhận mã hợp lệ."
          action={
            <div className="grid max-w-2xl gap-3 sm:grid-cols-3">
              {[
                { icon: ShieldAlert, label: 'Ưu tiên bài nghi ngờ' },
                { icon: Sparkles, label: 'Đối chiếu bằng chứng AI' },
                { icon: LockKeyhole, label: 'Công bố & khóa toàn ca' },
              ].map(({ icon: Icon, label }) => (
                <div
                  key={label}
                  className="flex items-center gap-2 rounded-xl border border-[#82aedb]/55 bg-white px-3 py-2.5 text-left text-xs font-semibold text-[#5b6b84]"
                >
                  <Icon className="size-3.5 shrink-0 text-[#1C4D8D]" />
                  {label}
                </div>
              ))}
            </div>
          }
        />
      ) : isLoading ? (
        <section className="flex min-h-[430px] flex-col items-center justify-center rounded-2xl border border-[#82aedb]/55 bg-[#F8FBFD]">
          <LoaderCircle className="size-8 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
          <p className="mt-3 text-sm font-semibold text-[#5b6b84]">
            Đang tải Evidence Panel của ca thi...
          </p>
        </section>
      ) : evidence.length === 0 && !error ? (
        <EmptyState
          className="min-h-[430px] justify-center"
          icon={UsersRound}
          title="Ca thi chưa có bài nộp"
          description="Chưa có dữ liệu thí sinh để giảng viên hậu kiểm."
        />
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
                className="rounded-2xl border border-[#82aedb]/55 bg-white p-4 shadow-[0_7px_26px_rgba(31,61,105,0.045)]"
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

          <section className="overflow-hidden rounded-2xl border border-[#82aedb]/55 bg-white shadow-[0_9px_30px_rgba(31,61,105,0.05)]">
            <div className="flex flex-col gap-4 border-b border-[#82aedb]/55 p-4 sm:p-5 lg:flex-row lg:items-center lg:justify-between">
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
                  className="flex size-10 items-center justify-center rounded-xl border border-[#82aedb]/55 text-[#5b6b84] transition hover:bg-[#4988C4]/18 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
                  aria-label="Tải lại bằng chứng"
                >
                  <RefreshCw className="size-4" />
                </button>
                <button
                  type="button"
                  onClick={() => setShowPublishConfirm(true)}
                  disabled={!canPublish}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-[#168367] px-4 text-xs font-bold text-white shadow-[0_8px_20px_rgba(22,131,103,0.2)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#106c55] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#168367] disabled:cursor-not-allowed disabled:bg-[#a7b7b3] disabled:shadow-none motion-reduce:transform-none"
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
                    : 'Chưa thể công bố: các bài bị đánh dấu nghi ngờ, điểm rớt (< 5.0) hoặc confidence thấp chưa được hậu kiểm đầy đủ.'}
              </div>
            )}

            <div className="grid min-h-[680px] lg:grid-cols-[330px_minmax(0,1fr)]">
              <aside className="border-b border-[#82aedb]/55 bg-white lg:border-r lg:border-b-0">
                <div className="border-b border-[#82aedb]/55 p-3">
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
                              ? 'border-[#82aedb]/55 bg-white shadow-[0_7px_20px_rgba(69,75,155,0.09)]'
                              : 'border-transparent hover:border-[#82aedb]/55 hover:bg-white',
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
                                <StatusBadge status={candidate.status as ExamTicketStatus} />
                                {attention && (
                                  <span className="rounded-full border border-[#f3c2c8] bg-[#fff0f1] px-2 py-1 text-xs font-bold text-[#a93d48]">
                                    Cần xem kỹ
                                  </span>
                                )}
                                {pending && (
                                  <span className="rounded-full border border-[#f3ddb0] bg-[#fff6e5] px-2 py-1 text-xs font-bold text-[#8d6118]">
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
                    <section className="rounded-2xl border border-[#82aedb]/55 bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)]">
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
                          <StatusBadge status={activeCandidate.status as ExamTicketStatus} />
                          {activeCandidate.isLocked && (
                            <span className="inline-flex items-center gap-1 rounded-full border border-[#f3c2c8] bg-[#fff0f1] px-2 py-1 text-xs font-bold text-[#a93d48]">
                              <LockKeyhole className="size-3" />
                              One-Way Lock
                            </span>
                          )}
                        </div>
                      </div>

                      <div className="mt-4 grid gap-3 sm:grid-cols-3">
                        <div className="rounded-xl border border-[#82aedb]/55 bg-white p-3">
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
                        <div className="rounded-xl border border-[#82aedb]/55 bg-white p-3">
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
                        <div className="rounded-xl border border-[#82aedb]/55 bg-white p-3">
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
                            (getDisplayedScore(activeCandidate) !== null &&
                            (getDisplayedScore(activeCandidate) ?? 0) < 5.0
                              ? 'Bài thi có điểm dưới mức đạt (< 5.0); giảng viên bắt buộc hậu kiểm bằng chứng trước khi công bố.'
                              : 'Bài nộp bị đánh dấu cần can thiệp; giảng viên phải đối chiếu toàn bộ bằng chứng.')}
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
                      <section className="rounded-2xl border border-[#82aedb]/55 bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)] sm:p-5">
                        <div className="flex items-center gap-2">
                          <FileSearch className="size-4 text-[#1C4D8D]" />
                          <h3 className="text-sm font-bold text-[#0F2854]">Transcript Whisper</h3>
                        </div>
                        <div className="mt-3 max-h-72 overflow-y-auto rounded-xl border border-[#82aedb]/55 bg-white p-3">
                          <p className="text-xs leading-[1.7] whitespace-pre-wrap text-[#5b6b84]">
                            {activeCandidate.transcriptWhisper ||
                              'Chưa có transcript để giảng viên đối chiếu.'}
                          </p>
                        </div>
                      </section>

                      <section className="rounded-2xl border border-[#82aedb]/55 bg-white p-4 shadow-[0_8px_26px_rgba(31,61,105,0.045)] sm:p-5">
                        <div className="flex items-center gap-2">
                          <Sparkles className="size-4 text-[#1C4D8D]" />
                          <h3 className="text-sm font-bold text-[#0F2854]">
                            Phân tích AI theo tiêu chí
                          </h3>
                        </div>
                        <div className="mt-3 max-h-72 overflow-y-auto rounded-xl border border-[#82aedb]/55 bg-white p-3">
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
                      persistenceAvailable
                      isSaving={isSavingOverride}
                      onSaveOverride={saveOverride}
                    />
                    <div className="flex flex-wrap justify-end gap-2 rounded-2xl border border-[#82aedb]/55 bg-white p-4">
                      <button
                        type="button"
                        disabled={activeCandidate.isLocked || isLocking}
                        onClick={() => void lockTicket()}
                        className={cn(ds.primaryButton, 'bg-[#b8404b] hover:bg-[#953943]')}
                      >
                        <LockKeyhole className="size-4" />
                        {isLocking ? 'Đang khóa...' : 'Khóa điểm vé thi'}
                      </button>
                    </div>
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
            <div className="rounded-xl border border-[#82aedb]/55 bg-white p-3 text-center">
              <p className="text-xs font-bold text-[#5b6b84] uppercase">Sẽ công bố</p>
              <p className="mt-1 font-mono text-lg font-black text-[#1C4D8D]">{evidence.length}</p>
              <p className="text-xs text-[#5b6b84]">sinh viên</p>
            </div>
            <div className="rounded-xl border border-[#82aedb]/55 bg-white p-3 text-center">
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
