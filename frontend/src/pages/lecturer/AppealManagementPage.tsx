import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useInRouterContext, useLocation, useNavigate, useParams } from 'react-router-dom'
import {
  CheckCircle2,
  ClipboardCheck,
  FileAudio2,
  Gavel,
  LoaderCircle,
  RefreshCw,
  Search,
  Sparkles,
  UserRoundCog,
} from 'lucide-react'
import { appealsService } from '@/services/appeals.service'
import { useAuth } from '@/hooks/useAuth'
import { canonicalRole } from '@/routes/routeAccess'
import type {
  Appeal,
  AppealAllocationProposal,
  LecturerAllocationSummary,
} from '@/types/appeal.types'
import type { AuditEvidenceItem } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { WaveformPlayer } from '@/components/audio/WaveformPlayer'
import { AppealRegradeEvidenceDrawer } from '@/components/appeals/AppealRegradeEvidenceDrawer'
import { toast } from '@/components/common/toastStore'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { ds } from '@/design/system'
import { appealManagementRoles } from '@/lib/task09Permissions'
import { userMessage } from '@/lib/userMessage'

const MAX_APPEALS_PER_LECTURER = 24

const APPEAL_STATUS_OPTIONS = [
  { value: '', label: 'Tất cả trạng thái' },
  { value: 'SUBMITTED_TO_EXAM_OFFICE', label: 'Chờ Khảo thí phân công (SUBMITTED_TO_EXAM_OFFICE)' },
  { value: 'ASSIGNED_TO_LECTURER', label: 'Đã giao GV chấm lại (ASSIGNED_TO_LECTURER)' },
  { value: 'REGRADED_BY_LECTURER', label: 'GV đã chấm lại — chờ duyệt (REGRADED_BY_LECTURER)' },
  { value: 'APPROVED', label: 'Đã chấp thuận (APPROVED)' },
  { value: 'REJECTED', label: 'Đã từ chối (REJECTED)' },
] as const

const APPEAL_STEPS = [
  { key: 'SUBMITTED_TO_EXAM_OFFICE', label: '1. SV nộp đơn' },
  { key: 'NOTIFIED', label: '2. Báo KT & TBM' },
  { key: 'ASSIGNED_TO_LECTURER', label: '3. Phân công GV' },
  { key: 'REGRADED_BY_LECTURER', label: '4. GV chấm lại' },
  { key: 'DECIDED', label: '5. Khảo thí chốt' },
] as const

function getAppealStepIndex(status: string): number {
  switch (status.toUpperCase()) {
    case 'SUBMITTED_TO_EXAM_OFFICE':
      return 1
    case 'ASSIGNED_TO_LECTURER':
      return 2
    case 'REGRADED_BY_LECTURER':
      return 3
    case 'APPROVED':
    case 'REJECTED':
      return 4
    default:
      return 0
  }
}

function getStatusChipClass(status: string): string {
  const normalized = status.toUpperCase()
  if (normalized === 'APPROVED') return ds.successChip
  if (normalized === 'REJECTED' || normalized === 'CANCELLED') return ds.dangerChip
  return ds.infoChip
}

function getErrorMessage(error: unknown) {
  return userMessage(error, 'Không thể hoàn tất yêu cầu phúc khảo. Vui lòng thử lại.')
}

interface AppealManagementContentProps {
  routeAppealId?: string
  onNavigateToAppeal?: (appealId: string) => void
}

function AppealManagementPageContent({
  routeAppealId,
  onNavigateToAppeal,
}: AppealManagementContentProps) {
  const { user } = useAuth()
  const normalizedRole = canonicalRole(user?.role)
  const canView = Boolean(normalizedRole && appealManagementRoles.includes(normalizedRole))
  const canManageAppeals = normalizedRole === 'ExaminationOfficer'
  const isInstructor = normalizedRole === 'Instructor'

  const [appeals, setAppeals] = useState<Appeal[]>([])
  const [selected, setSelected] = useState<Appeal | null>(null)
  const selectedIdRef = useRef<string | null>(null)
  const [statusFilter, setStatusFilter] = useState('')
  const [searchQuery, setSearchQuery] = useState('')

  const [lecturerId, setLecturerId] = useState('')
  const [decision, setDecision] = useState<'APPROVED' | 'REJECTED'>('APPROVED')
  const [score, setScore] = useState('')
  const [notes, setNotes] = useState('')

  const [proposal, setProposal] = useState<AppealAllocationProposal | null>(null)
  const [showProposalPanel, setShowProposalPanel] = useState(false)
  const [proposalOverrides, setProposalOverrides] = useState<Record<string, string>>({})

  const [evidence, setEvidence] = useState<AuditEvidenceItem | null>(null)
  const [evidenceLoading, setEvidenceLoading] = useState(false)
  const [evidenceError, setEvidenceError] = useState('')

  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [scoreError, setScoreError] = useState('')
  const [notesError, setNotesError] = useState('')
  const [lecturerIdError, setLecturerIdError] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const [confirmDecisionOpen, setConfirmDecisionOpen] = useState(false)
  const [confirmBatchAssignOpen, setConfirmBatchAssignOpen] = useState(false)
  const [evidenceDrawerOpen, setEvidenceDrawerOpen] = useState(false)

  const canAssignAppeal = canManageAppeals && selected?.status === 'SUBMITTED_TO_EXAM_OFFICE'
  const canDecideAppeal = canManageAppeals && selected?.status === 'REGRADED_BY_LECTURER'
  const canRegradeAppeal =
    isInstructor &&
    selected?.assignedTo === user?.id &&
    selected?.status === 'ASSIGNED_TO_LECTURER'

  const resetFieldErrors = useCallback(() => {
    setScoreError('')
    setNotesError('')
    setLecturerIdError('')
  }, [])

  const applySelectedAppeal = useCallback(
    (appeal: Appeal | null, syncUrl: boolean) => {
      selectedIdRef.current = appeal?.id ?? null
      setSelected(appeal)
      setLecturerId('')
      setScore(appeal?.proposedScore?.toString() ?? appeal?.originalScore?.toString() ?? '')
      setNotes(appeal?.reviewNotes ?? '')
      setDecision(appeal?.decision === 'REJECTED' ? 'REJECTED' : 'APPROVED')
      resetFieldErrors()
      if (appeal && syncUrl) {
        onNavigateToAppeal?.(appeal.id)
      }
    },
    [onNavigateToAppeal, resetFieldErrors],
  )

  const validateReviewFields = (scoreRequired: boolean) => {
    let nextScoreError = ''
    const scoreText = score.trim()
    if (!scoreText) {
      if (scoreRequired) nextScoreError = 'Vui lòng nhập điểm.'
    } else {
      const value = Number(scoreText)
      if (!Number.isFinite(value) || value < 0 || value > 10) {
        nextScoreError = 'Điểm phải nằm trong khoảng 0 – 10.'
      }
    }
    const nextNotesError = notes.trim().length < 10 ? 'Ghi chú cần ít nhất 10 ký tự.' : ''
    setScoreError(nextScoreError)
    setNotesError(nextNotesError)
    return !nextScoreError && !nextNotesError
  }

  const loadProposalSilently = useCallback(async () => {
    if (!canManageAppeals || typeof appealsService.getAllocationProposal !== 'function') return
    try {
      const result = await appealsService.getAllocationProposal()
      setProposal(result)
    } catch {
      // Không chặn trang chính nếu danh mục phân bổ chưa sẵn sàng.
    }
  }, [canManageAppeals])

  const loadAppeals = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError('')
    try {
      const params = statusFilter ? { status: statusFilter } : {}
      const result = await appealsService.list(params)
      const list = Array.isArray(result) ? result : []

      let matchedRouteAppeal: Appeal | null = null
      if (routeAppealId) {
        matchedRouteAppeal = list.find((item) => item.id === routeAppealId) ?? null
        if (!matchedRouteAppeal && typeof appealsService.getById === 'function') {
          try {
            matchedRouteAppeal = await appealsService.getById(routeAppealId)
          } catch {
            matchedRouteAppeal = null
          }
        }
      }

      const mergedList =
        matchedRouteAppeal && !list.some((item) => item.id === matchedRouteAppeal.id)
          ? [matchedRouteAppeal, ...list]
          : list

      setAppeals(mergedList)
      const previousSelectedId = selectedIdRef.current
      const nextSelected =
        matchedRouteAppeal ??
        (previousSelectedId
          ? (mergedList.find((item) => item.id === previousSelectedId) ?? mergedList[0] ?? null)
          : (mergedList[0] ?? null))

      if ((nextSelected?.id ?? null) !== previousSelectedId) {
        applySelectedAppeal(nextSelected, false)
      } else {
        setSelected(nextSelected)
      }
    } catch (loadError) {
      setError(getErrorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }, [applySelectedAppeal, canView, routeAppealId, statusFilter])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadAppeals()
      void loadProposalSilently()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [loadAppeals, loadProposalSilently])

  useEffect(() => {
    if (!selected || !canView || typeof appealsService.getEvidence !== 'function') {
      const resetTimer = window.setTimeout(() => {
        setEvidence(null)
        setEvidenceError('')
        setEvidenceLoading(false)
      }, 0)
      return () => window.clearTimeout(resetTimer)
    }

    let cancelled = false
    const timer = window.setTimeout(() => {
      setEvidenceLoading(true)
      setEvidenceError('')
      void appealsService
        .getEvidence(selected.id)
        .then((item) => {
          if (!cancelled) setEvidence(item)
        })
        .catch((fetchError: unknown) => {
          if (!cancelled) {
            setEvidence(null)
            setEvidenceError(userMessage(fetchError, 'Chưa thể tải bằng chứng âm thanh của đơn này.'))
          }
        })
        .finally(() => {
          if (!cancelled) setEvidenceLoading(false)
        })
    }, 0)

    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [canView, selected])

  const lecturerPool = useMemo<LecturerAllocationSummary[]>(() => {
    const map = new Map<string, LecturerAllocationSummary>()
    for (const summary of proposal?.lecturerSummaries ?? []) {
      map.set(summary.lecturerId, summary)
    }
    for (const item of proposal?.items ?? []) {
      if (item.proposedLecturerId && !map.has(item.proposedLecturerId)) {
        map.set(item.proposedLecturerId, {
          lecturerId: item.proposedLecturerId,
          lecturerName: item.proposedLecturerName || 'Giảng viên',
          email: '',
          assignedCount: 0,
        })
      }
    }
    return Array.from(map.values())
  }, [proposal])

  const selectedProposalItem = useMemo(() => {
    if (!selected || !proposal) return null
    return proposal.items.find((item) => item.appealId === selected.id) ?? null
  }, [proposal, selected])

  useEffect(() => {
    if (!selected || selected.status !== 'SUBMITTED_TO_EXAM_OFFICE') return
    const suggested = selectedProposalItem?.proposedLecturerId ?? ''
    const initialExaminerId = selectedProposalItem?.initialExaminerUserId ?? null
    const timer = window.setTimeout(() => {
      setLecturerId((current) => {
        if (current && current !== initialExaminerId) return current
        return suggested
      })
    }, 0)
    return () => window.clearTimeout(timer)
  }, [selected, selectedProposalItem])

  const filteredAppeals = useMemo(() => {
    const normalizedSearch = searchQuery.trim().toLowerCase()
    return appeals.filter((appeal) => {
      if (!normalizedSearch) return true
      return [
        appeal.studentCode,
        appeal.studentName,
        appeal.sessionTitle,
        appeal.reason,
        appeal.status,
      ]
        .join(' ')
        .toLowerCase()
        .includes(normalizedSearch)
    })
  }, [appeals, searchQuery])

  const resolvedProposalAssignments = useMemo(() => {
    if (!proposal) return []
    return proposal.items
      .map((item) => {
        const chosenLecturerId = (proposalOverrides[item.appealId] ?? item.proposedLecturerId ?? '').trim()
        return {
          ...item,
          chosenLecturerId,
        }
      })
      .filter((item) => Boolean(item.chosenLecturerId))
  }, [proposal, proposalOverrides])

  const runAction = async (action: () => Promise<unknown>, successMessage: string) => {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await action()
      setNotice(successMessage)
      toast.success(successMessage)
      await loadAppeals()
      if (canManageAppeals) {
        await loadProposalSilently()
      }
    } catch (actionError) {
      const message = getErrorMessage(actionError)
      setError(message)
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  const requestDecision = () => {
    if (!selected || !canDecideAppeal) return
    setLecturerIdError('')
    if (!validateReviewFields(false)) return
    setConfirmDecisionOpen(true)
  }

  const executeDecision = () => {
    if (!selected || !canDecideAppeal) return
    setConfirmDecisionOpen(false)
    void runAction(
      () =>
        appealsService.review(selected.id, {
          decision,
          proposedScore: score.trim() ? Number(score) : undefined,
          reviewNotes: notes.trim(),
        }),
      'Đã ghi nhận quyết định phúc khảo cuối cùng.',
    )
  }

  const assignSingleLecturer = () => {
    if (!selected || !canAssignAppeal) return
    resetFieldErrors()
    if (!lecturerId.trim()) {
      setLecturerIdError('Vui lòng chọn giảng viên chấm lại độc lập từ danh sách.')
      return
    }
    void runAction(
      async () => {
        await appealsService.assignLecturer(selected.id, { lecturerId: lecturerId.trim() })
        setLecturerId('')
      },
      'Đã phân công giảng viên chấm lại độc lập.',
    )
  }

  const submitRegrade = () => {
    if (!selected || !canRegradeAppeal) return
    setLecturerIdError('')
    if (!validateReviewFields(true)) return
    void runAction(
      () =>
        appealsService.regrade(selected.id, {
          newScore: Number(score),
          reviewNotes: notes.trim(),
        }),
      'Đã lưu biên bản và điểm chấm lại phúc khảo.',
    )
  }

  const previewAutoAllocation = async () => {
    if (!canManageAppeals) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const result = await appealsService.getAllocationProposal()
      setProposal(result)
      setProposalOverrides({})
      setShowProposalPanel(true)
      const assignableCount = (result.items ?? []).filter((item) =>
        Boolean(item.proposedLecturerId),
      ).length
      const summary = `Đã tính đề xuất phân bổ tự động: ${assignableCount}/${result.totalAppeals ?? 0} đơn đủ điều kiện (định mức <= ${MAX_APPEALS_PER_LECTURER} bài/GV).`
      setNotice(summary)
    } catch (actionError) {
      const message = getErrorMessage(actionError)
      setError(message)
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  const executeBatchAssign = () => {
    if (!canManageAppeals || resolvedProposalAssignments.length === 0) return
    setConfirmBatchAssignOpen(false)
    void runAction(
      async () => {
        await appealsService.batchAssign({
          assignments: resolvedProposalAssignments.map((item) => ({
            appealId: item.appealId,
            lecturerId: item.chosenLecturerId,
          })),
        })
        setShowProposalPanel(false)
        setProposalOverrides({})
      },
      `Đã phân công đồng loạt ${resolvedProposalAssignments.length} đơn phúc khảo.`,
    )
  }

  if (!canView) {
    return (
      <div
        role="alert"
        className="rounded-xl border border-[#f0c5c9] bg-[#fff5f6] px-4 py-3 text-sm font-semibold text-[#a43c47]"
      >
        Chỉ Cán bộ Khảo thí và Giảng viên được phân công chấm lại mới được truy cập phúc khảo.
      </div>
    )
  }

  const currentStepIndex = selected ? getAppealStepIndex(selected.status) : -1

  return (
    <div className={ds.page}>
      <PageBanner
        icon={ClipboardCheck}
        eyebrow={canManageAppeals ? 'CỔNG KHẢO THÍ · PHÚC KHẢO 5 BƯỚC' : 'GIẢNG VIÊN · CHẤM PHÚC KHẢO'}
        title={canManageAppeals ? 'Điều phối & Phê duyệt Phúc khảo MF-04' : 'Chấm lại đơn Phúc khảo được phân công'}
        description={
          canManageAppeals
            ? 'Tiếp nhận đơn phúc khảo, cân bằng tải phân công giảng viên độc lập (<= 24 bài/GV, loại trừ Giám khảo đầu) và phê duyệt mở khóa One-Way Lock.'
            : 'Nghe lại bản ghi âm Cloudflare R2, đối chiếu transcript & AI CoT và gửi biên bản chấm lại cho đơn được Cán bộ Khảo thí phân công.'
        }
      />

      {(error || notice) && (
        <div
          role={error ? 'alert' : 'status'}
          className={cn(
            'flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm font-semibold',
            error
              ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43c47]'
              : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]',
          )}
        >
          <span>{error || notice}</span>
          {error ? (
            <button type="button" className={ds.outlineButton} onClick={() => void loadAppeals()}>
              <RefreshCw className="size-4" /> Thử lại
            </button>
          ) : null}
        </div>
      )}

      {canManageAppeals && (
        <section className={cn(ds.section, 'p-4')}>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="text-sm font-bold text-[#0F2854]">
                Phân bổ tự động Giảng viên chấm phúc khảo (Định mức &le; {MAX_APPEALS_PER_LECTURER} bài/GV)
              </h2>
              <p className="mt-0.5 text-xs text-[#687a91]">
                Hệ thống tự động cân bằng tải và loại trừ 100% Giám khảo đã chấm ca thi ban đầu của
                sinh viên.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                disabled={busy}
                onClick={() => void previewAutoAllocation()}
                className={ds.outlineButton}
              >
                <Sparkles className="size-4" /> Đề xuất phân bổ tự động
              </button>
              {showProposalPanel && resolvedProposalAssignments.length > 0 && (
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => setConfirmBatchAssignOpen(true)}
                  className={ds.primaryButton}
                >
                  <UserRoundCog className="size-4" /> Xác nhận phân công (
                  {resolvedProposalAssignments.length} đơn)
                </button>
              )}
            </div>
          </div>

          {showProposalPanel && proposal && (
            <div className="mt-4 space-y-3 rounded-xl border border-[#82aedb]/55 bg-[#f8fbfd] p-4 text-xs text-[#52657d]">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="font-semibold text-[#0F2854]">
                  Tổng đơn chờ: {proposal.totalAppeals} · Giảng viên khả dụng:{' '}
                  {proposal.totalLecturers} · Đủ điều kiện phân bổ:{' '}
                  {resolvedProposalAssignments.length}
                </p>
                {proposal.exceedsStandardQuota && (
                  <span className={ds.dangerChip}>
                    Cảnh báo: Số đơn vượt định mức {MAX_APPEALS_PER_LECTURER} bài/GV
                  </span>
                )}
              </div>

              {lecturerPool.length > 0 && (
                <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  {lecturerPool.map((summary) => {
                    const percent = Math.min(
                      100,
                      Math.round((summary.assignedCount / MAX_APPEALS_PER_LECTURER) * 100),
                    )
                    const isFull = summary.assignedCount >= MAX_APPEALS_PER_LECTURER
                    return (
                      <div
                        key={summary.lecturerId}
                        className="rounded-lg border border-[#d6e4f2] bg-white p-2.5"
                      >
                        <div className="flex items-center justify-between gap-2">
                          <span className="truncate font-bold text-[#0F2854]">
                            {summary.lecturerName}
                          </span>
                          <span
                            className={cn(
                              'font-mono font-bold',
                              isFull ? 'text-[#a43c47]' : 'text-[#1C4D8D]',
                            )}
                          >
                            {summary.assignedCount}/{MAX_APPEALS_PER_LECTURER}
                          </span>
                        </div>
                        <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-[#e6eff8]">
                          <div
                            className={cn(
                              'h-full rounded-full',
                              isFull ? 'bg-[#c2415b]' : 'bg-[#1C4D8D]',
                            )}
                            style={{ width: `${percent}%` }}
                          />
                        </div>
                      </div>
                    )
                  })}
                </div>
              )}

              {proposal.items.length > 0 && (
                <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  {proposal.items.map((item) => {
                    const chosenId =
                      proposalOverrides[item.appealId] ?? item.proposedLecturerId ?? ''
                    return (
                      <div
                        key={item.appealId}
                        className="rounded-lg border border-[#d6e4f2] bg-white p-3"
                      >
                        <p className="font-bold text-[#1C4D8D]">
                          {item.studentCode} · {item.studentName}
                        </p>
                        <p className="mt-0.5 truncate text-[#52657d]">{item.sessionTitle}</p>
                        {item.initialExaminerName && (
                          <p className="mt-1 text-[11px] text-[#687a91]">
                            Loại trừ GK đầu: <strong>{item.initialExaminerName}</strong>
                          </p>
                        )}
                        <label className="mt-2 block text-[11px] font-bold text-[#52657d]">
                          GV chấm lại đề xuất
                          <select
                            aria-label={`Chọn giảng viên chấm lại cho ${item.studentCode}`}
                            className={cn(ds.field, 'mt-1 h-9 text-xs')}
                            value={chosenId}
                            onChange={(event) =>
                              setProposalOverrides((current) => ({
                                ...current,
                                [item.appealId]: event.target.value,
                              }))
                            }
                          >
                            <option value="">-- Chưa phân công --</option>
                            {lecturerPool.map((summary) => {
                              const isInitialExaminer =
                                Boolean(item.initialExaminerUserId) &&
                                summary.lecturerId === item.initialExaminerUserId
                              const isAtQuota =
                                summary.assignedCount >= MAX_APPEALS_PER_LECTURER &&
                                summary.lecturerId !== item.proposedLecturerId
                              return (
                                <option
                                  key={summary.lecturerId}
                                  value={summary.lecturerId}
                                  disabled={isInitialExaminer || isAtQuota}
                                >
                                  {summary.lecturerName} ({summary.assignedCount}/
                                  {MAX_APPEALS_PER_LECTURER})
                                  {isInitialExaminer
                                    ? ' · Giám khảo đầu'
                                    : isAtQuota
                                      ? ' · Đủ định mức'
                                      : ''}
                                </option>
                              )
                            })}
                          </select>
                        </label>
                      </div>
                    )
                  })}
                </div>
              )}
            </div>
          )}
        </section>
      )}

      <section className="grid gap-4 lg:grid-cols-[minmax(300px,0.85fr)_minmax(0,1.45fr)]">
        <div className={ds.section}>
          <SectionHeader
            icon={Gavel}
            title="Danh sách đơn"
            badge={`${filteredAppeals.length} đơn`}
            hint="Quy trình phúc khảo 5 bước chuẩn MF-04"
            action={
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => void loadAppeals()}
                disabled={loading}
              >
                <RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới
              </button>
            }
          />
          <div className="grid gap-2 border-b border-[#d9e8f2] bg-[#F8FBFD] p-3">
            <label className="relative block">
              <span className="sr-only">Tìm kiếm đơn phúc khảo</span>
              <Search
                className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#687a91]"
                aria-hidden="true"
              />
              <input
                type="search"
                className={cn(ds.field, 'pl-9')}
                placeholder="Tìm MSSV, họ tên, kỳ thi hoặc lý do…"
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
              />
            </label>
            <label className="block">
              <span className="sr-only">Lọc trạng thái đơn phúc khảo</span>
              <select
                aria-label="Lọc trạng thái đơn phúc khảo"
                className={ds.field}
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value)}
              >
                {APPEAL_STATUS_OPTIONS.map((option) => (
                  <option key={option.value || 'ALL'} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
          </div>
          {loading ? (
            <div className="flex flex-col items-center justify-center gap-2 p-10 text-xs font-semibold text-[#5b6b84]">
              <LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" />
              <span>Đang tải danh sách đơn phúc khảo…</span>
            </div>
          ) : (
            <div className="max-h-[640px] space-y-2 overflow-y-auto p-3">
              {filteredAppeals.map((appeal) => (
                <button
                  key={appeal.id}
                  type="button"
                  onClick={() => applySelectedAppeal(appeal, true)}
                  className={cn(
                    'w-full rounded-xl border p-3 text-left transition',
                    selected?.id === appeal.id
                      ? 'border-[#1C4D8D] bg-[#F4FBFD]'
                      : 'border-[#82aedb]/55 bg-white hover:border-[#1C4D8D]/60',
                  )}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-mono text-xs font-bold text-[#1C4D8D]">
                      {appeal.studentCode || '—'}
                    </span>
                    <span className={getStatusChipClass(appeal.status)}>{appeal.status}</span>
                  </div>
                  <p className="mt-2 text-sm font-bold text-[#0F2854]">
                    {appeal.studentName || 'Sinh viên'}
                  </p>
                  <p className="mt-1 truncate text-xs text-[#687a91]">
                    {appeal.sessionTitle || appeal.reason}
                  </p>
                </button>
              ))}
              {filteredAppeals.length === 0 && (
                <EmptyState
                  compact
                  icon={Gavel}
                  title="Chưa có đơn phúc khảo"
                  description="Đơn mới hoặc đơn khớp bộ lọc sẽ xuất hiện tại đây."
                />
              )}
            </div>
          )}
        </div>

        {selected ? (
          <div className="space-y-4">
            <section className={ds.section}>
              <SectionHeader
                icon={Gavel}
                title={`Chi tiết đơn #${selected.id.slice(0, 8)}`}
                badge={selected.status}
                hint={`${selected.studentName} · ${selected.studentCode}`}
              />
              <div className="space-y-4 p-5">
                <ol
                  aria-label="Tiến trình phúc khảo 5 bước"
                  className="grid gap-2 sm:grid-cols-5"
                >
                  {APPEAL_STEPS.map((step, index) => {
                    const completed = index <= currentStepIndex
                    return (
                      <li
                        key={step.key}
                        className={cn(
                          'flex items-center gap-1.5 rounded-lg border px-2.5 py-2 text-[11px] font-bold',
                          completed
                            ? 'border-[#1C4D8D]/35 bg-[#F4FBFD] text-[#0F2854]'
                            : 'border-[#d9e8f2] bg-white text-[#7a8ca3]',
                        )}
                      >
                        <CheckCircle2
                          className={cn(
                            'size-3.5 shrink-0',
                            completed ? 'text-[#1C4D8D]' : 'text-[#c4d4e4]',
                          )}
                          aria-hidden="true"
                        />
                        <span className="truncate">{step.label}</span>
                      </li>
                    )
                  })}
                </ol>

                <div className="rounded-xl border border-[#82aedb]/55 bg-[#f8fbfd] p-4 text-sm leading-relaxed text-[#52657d]">
                  <p>
                    <strong>Kỳ thi:</strong> {selected.sessionTitle || '—'}
                  </p>
                  <p className="mt-1.5">
                    <strong>Lý do khiếu nại:</strong> {selected.reason}
                  </p>
                  <p className="mt-2">
                    <strong>Điểm gốc:</strong> {selected.originalScore ?? '—'} ·{' '}
                    <strong>Điểm đề xuất chấm lại:</strong> {selected.proposedScore ?? '—'} ·{' '}
                    <strong>Trạng thái:</strong> {selected.status}
                  </p>
                  {selected.assignedToName && (
                    <p className="mt-2">
                      <strong>Giảng viên chấm lại độc lập:</strong> {selected.assignedToName}
                    </p>
                  )}
                  {selected.reviewNotes && (
                    <p className="mt-2 whitespace-pre-wrap">
                      <strong>Biên bản / ghi chú:</strong> {selected.reviewNotes}
                    </p>
                  )}
                </div>

                <div className="rounded-xl border border-[#d9e8f2] bg-white p-4">
                  <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2 text-xs font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">
                      <FileAudio2 className="size-4" aria-hidden="true" />
                      Bằng chứng bài thi (Single-Ticket BOLA)
                      {evidence?.followUpItems && evidence.followUpItems.length > 0 ? (
                        <span className={ds.infoChip}>
                          {evidence.followUpItems.length} câu phụ
                        </span>
                      ) : null}
                    </div>
                    <button
                      type="button"
                      onClick={() => setEvidenceDrawerOpen(true)}
                      className={ds.outlineButton}
                    >
                      <FileAudio2 className="size-4" /> Mở ngăn bằng chứng đầy đủ
                    </button>
                  </div>
                  {evidenceLoading ? (
                    <div className="flex items-center gap-2 text-xs font-semibold text-[#5b6b84]">
                      <LoaderCircle className="size-4 animate-spin text-[#1C4D8D]" />
                      Đang tải âm thanh R2, transcript và lập luận AI…
                    </div>
                  ) : evidence ? (
                    <div className="space-y-3">
                      <WaveformPlayer
                        audioUrl={evidence.audioR2Url}
                        hashSha256={evidence.audioHashSha256}
                        studentCode={evidence.studentCode}
                        seatNumber={evidence.seatNumber}
                      />
                      <div className="grid gap-3 text-xs text-[#52657d] sm:grid-cols-2">
                        <div className="rounded-xl border border-[#d9e8f2] bg-[#f8fbfd] p-3">
                          <p className="font-bold text-[#0F2854]">Transcript Whisper</p>
                          <p className="mt-1 whitespace-pre-wrap leading-relaxed">
                            {evidence.transcriptWhisper || 'Chưa có transcript.'}
                          </p>
                        </div>
                        <div className="rounded-xl border border-[#d9e8f2] bg-[#f8fbfd] p-3">
                          <p className="font-bold text-[#0F2854]">
                            AI Score: {evidence.aiScore ?? '—'} · Final: {evidence.finalScore ?? '—'}
                          </p>
                          <p className="mt-1 whitespace-pre-wrap leading-relaxed">
                            {evidence.cotTrace || 'Chưa có chuỗi lập luận AI CoT.'}
                          </p>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <p className="text-xs text-[#687a91]">
                      {evidenceError || 'Chưa có dữ liệu bằng chứng âm thanh cho đơn này.'}
                    </p>
                  )}
                </div>

                {canAssignAppeal || canDecideAppeal || canRegradeAppeal ? (
                  <>
                    {(canDecideAppeal || canRegradeAppeal) && (
                      <div className="grid gap-3 sm:grid-cols-2">
                        {canDecideAppeal && (
                          <label className="text-xs font-bold text-[#63758e]">
                            Quyết định của Khảo thí
                            <select
                              className={cn(ds.field, 'mt-1')}
                              value={decision}
                              onChange={(event) =>
                                setDecision(
                                  event.target.value === 'REJECTED' ? 'REJECTED' : 'APPROVED',
                                )
                              }
                            >
                              <option value="APPROVED">APPROVED (Chấp thuận cập nhật điểm)</option>
                              <option value="REJECTED">REJECTED (Giữ nguyên điểm gốc)</option>
                            </select>
                          </label>
                        )}
                        <label className={fieldLabelClass(Boolean(scoreError))}>
                          Điểm đề xuất / chấm lại
                          <input
                            type="number"
                            min="0"
                            max="10"
                            step="0.1"
                            className={cn(ds.field, 'mt-1', scoreError && fieldInvalidClass)}
                            value={score}
                            onChange={(event) => {
                              setScore(event.target.value)
                              if (scoreError) setScoreError('')
                            }}
                            aria-invalid={Boolean(scoreError)}
                          />
                          <FieldError message={scoreError} />
                        </label>
                      </div>
                    )}

                    {(canDecideAppeal || canRegradeAppeal) && (
                      <div>
                        <textarea
                          className={cn(ds.field, 'min-h-24 py-2', notesError && fieldInvalidClass)}
                          placeholder="Biên bản giải trình chấm lại / ghi chú phê duyệt (tối thiểu 10 ký tự)"
                          value={notes}
                          onChange={(event) => {
                            setNotes(event.target.value)
                            if (notesError) setNotesError('')
                          }}
                          aria-label="Ghi chú xử lý"
                          aria-invalid={Boolean(notesError)}
                        />
                        <FieldError message={notesError} />
                      </div>
                    )}

                    <div className="flex flex-wrap items-start gap-2">
                      {canManageAppeals && (
                        <>
                          <button
                            type="button"
                            disabled={busy || !canDecideAppeal}
                            onClick={requestDecision}
                            className={ds.primaryButton}
                          >
                            <Gavel className="size-4" /> Phê duyệt quyết định cuối
                          </button>
                          <div className="min-w-[240px] flex-1">
                            <select
                              aria-label="Chọn giảng viên chấm lại độc lập"
                              className={cn(ds.field, lecturerIdError && fieldInvalidClass)}
                              value={lecturerId}
                              disabled={busy || !canAssignAppeal}
                              onChange={(event) => {
                                setLecturerId(event.target.value)
                                if (lecturerIdError) setLecturerIdError('')
                              }}
                              aria-invalid={Boolean(lecturerIdError)}
                            >
                              <option value="">
                                {lecturerPool.length > 0
                                  ? '-- Chọn giảng viên chấm lại độc lập --'
                                  : '-- Nhấn Đề xuất phân bổ để nạp danh sách GV --'}
                              </option>
                              {lecturerPool.map((summary) => {
                                const isInitialExaminer =
                                  Boolean(selectedProposalItem?.initialExaminerUserId) &&
                                  summary.lecturerId === selectedProposalItem?.initialExaminerUserId
                                const isAtQuota = summary.assignedCount >= MAX_APPEALS_PER_LECTURER
                                return (
                                  <option
                                    key={summary.lecturerId}
                                    value={summary.lecturerId}
                                    disabled={isInitialExaminer || isAtQuota}
                                  >
                                    {summary.lecturerName}
                                    {summary.email ? ` (${summary.email})` : ''} ·{' '}
                                    {summary.assignedCount}/{MAX_APPEALS_PER_LECTURER}
                                    {isInitialExaminer
                                      ? ' · Giám khảo đầu'
                                      : isAtQuota
                                        ? ' · Đủ định mức'
                                        : ''}
                                  </option>
                                )
                              })}
                            </select>
                            <FieldError message={lecturerIdError} />
                          </div>
                          <button
                            type="button"
                            disabled={busy || !canAssignAppeal}
                            onClick={assignSingleLecturer}
                            className={ds.outlineButton}
                          >
                            <UserRoundCog className="size-4" /> Phân công GV chấm lại
                          </button>
                        </>
                      )}
                      {canRegradeAppeal && (
                        <button
                          type="button"
                          disabled={busy}
                          onClick={submitRegrade}
                          className={ds.primaryButton}
                        >
                          <Gavel className="size-4" /> Gửi biên bản &amp; điểm chấm lại
                        </button>
                      )}
                    </div>
                  </>
                ) : (
                  <div className="rounded-xl border border-[#d6e4f2] bg-[#f4f8fc] p-3 text-xs font-semibold text-[#1C4D8D]">
                    {isInstructor
                      ? 'Đơn phúc khảo này không ở trạng thái chờ bạn chấm lại (ASSIGNED_TO_LECTURER). Bạn có thể xem lại biên bản và bằng chứng ở chế độ chỉ đọc.'
                      : 'Đơn phúc khảo đang ở trạng thái chờ Giảng viên chấm lại hoặc đã hoàn tất quy trình 5 bước.'}
                  </div>
                )}
              </div>
            </section>
          </div>
        ) : (
          <EmptyState
            className="min-h-[320px] justify-center"
            icon={Gavel}
            title="Chưa chọn đơn"
            description="Chọn một đơn phúc khảo ở danh sách bên trái để xử lý."
          />
        )}
      </section>

      {confirmDecisionOpen && selected ? (
        <ConfirmDialog
          variant="calm"
          tone={decision === 'APPROVED' ? 'warning' : 'info'}
          eyebrow="BƯỚC 5 · QUYẾT ĐỊNH PHÚC KHẢO"
          title={
            decision === 'APPROVED'
              ? 'Xác nhận chấp thuận phúc khảo & mở ngoại lệ One-Way Lock?'
              : 'Xác nhận từ chối phúc khảo & giữ nguyên điểm gốc?'
          }
          message={`Sinh viên ${selected.studentCode} (${selected.studentName}) · Quyết định: ${decision}${score.trim() ? ` · Điểm đề xuất: ${score.trim()}` : ''}.`}
          confirmLabel="Xác nhận quyết định"
          cancelLabel="Kiểm tra lại"
          pending={busy}
          onConfirm={executeDecision}
          onCancel={() => !busy && setConfirmDecisionOpen(false)}
        />
      ) : null}

      {confirmBatchAssignOpen ? (
        <ConfirmDialog
          variant="calm"
          tone="info"
          eyebrow="BƯỚC 3 · PHÂN CÔNG ĐỒNG LOẠT"
          title={`Phân công ${resolvedProposalAssignments.length} đơn phúc khảo cho giảng viên chấm lại?`}
          message={`Các giảng viên được chọn đều tuân thủ định mức <= ${MAX_APPEALS_PER_LECTURER} bài/GV và loại trừ Giám khảo chấm ca ban đầu.`}
          confirmLabel="Xác nhận phân công hàng loạt"
          cancelLabel="Hủy"
          pending={busy}
          onConfirm={executeBatchAssign}
          onCancel={() => !busy && setConfirmBatchAssignOpen(false)}
        />
      ) : null}

      {selected ? (
        <AppealRegradeEvidenceDrawer
          appealId={selected.id}
          studentCode={selected.studentCode}
          studentName={selected.studentName}
          open={evidenceDrawerOpen}
          onClose={() => setEvidenceDrawerOpen(false)}
        />
      ) : null}
    </div>
  )
}

function RoutedAppealManagementPage() {
  const { appealId } = useParams<{ appealId?: string }>()
  const navigate = useNavigate()
  const location = useLocation()

  const handleNavigateToAppeal = useCallback(
    (nextAppealId: string) => {
      if (location.pathname.startsWith('/examination-officer/appeals')) {
        navigate(`/examination-officer/appeals/${nextAppealId}`, { replace: true })
      } else if (location.pathname.startsWith('/lecturer/appeals')) {
        navigate(`/lecturer/appeals/${nextAppealId}`, { replace: true })
      }
    },
    [location.pathname, navigate],
  )

  return (
    <AppealManagementPageContent
      routeAppealId={appealId}
      onNavigateToAppeal={handleNavigateToAppeal}
    />
  )
}

export function AppealManagementPage() {
  const inRouter = useInRouterContext()
  return inRouter ? <RoutedAppealManagementPage /> : <AppealManagementPageContent />
}
