import { useEffect, useMemo, useRef, useState } from 'react'
import { ArchiveX, BookOpenCheck, Check, CheckSquare, Database, Square } from 'lucide-react'
import { SectionHeader } from '@/design/ui'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { StatusTabs } from '@/components/common/StatusTabs'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { TextareaField } from '@/components/common/TextareaField'
import { toast } from '@/components/common/toastStore'
import { courseCloService } from '@/services/courseClo.service'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { DIFFICULTY_LABELS, preparationDate, preparationError } from '@/lib/examPreparation'
import type { ManagedCloCourse } from '@/types/courseClo.types'
import type {
  BankCurationBand,
  BankCurationCandidate,
  BankCurationCandidatesResponse,
  BankCurationDecision,
} from '@/types/examPreparation.types'
import { ExamLoadError, ExamRefreshButton } from './ExamPreparationShared'
import { QuestionDifficultyBadge } from './QuestionDifficultyBadge'

type BandFilter = 'ALL' | BankCurationBand

function bandLabel(band: string) {
  if (band === 'RED_80_PLUS') return 'Trùng cao (≥ 80%)'
  if (band === 'YELLOW_70_79') return 'Cần đối chiếu (70%–79%)'
  return 'An toàn (< 70%)'
}

function bandChipClass(band: string) {
  if (band === 'RED_80_PLUS') {
    return 'inline-flex items-center rounded-full border border-[#e6a6b2] bg-[#fff1f3] px-2.5 py-0.5 text-xs font-bold text-[#a83249]'
  }
  if (band === 'YELLOW_70_79') {
    return 'inline-flex items-center rounded-full border border-[#edd08b] bg-[#fff9e9] px-2.5 py-0.5 text-xs font-bold text-[#9a630a]'
  }
  return 'inline-flex items-center rounded-full border border-[#a9dfc7] bg-[#eaf8f1] px-2.5 py-0.5 text-xs font-bold text-[#16836a]'
}

export function CourseBankCurationPanel({
  courses: initialCourses,
}: {
  courses?: ManagedCloCourse[]
}) {
  const [courseRevision, setCourseRevision] = useState(0)
  const [fetchedCourses, setFetchedCourses] = useState<{
    revision: number
    items?: ManagedCloCourse[]
    error?: string
  }>()
  const [selectedCourseId, setSelectedCourseId] = useState('')
  const [band, setBand] = useState<BandFilter>('ALL')
  const [revision, setRevision] = useState(0)
  const [resultState, setResultState] = useState<{
    key: string
    data?: BankCurationCandidatesResponse
    error?: string
  }>()
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [confirmState, setConfirmState] = useState<{
    questionIds: string[]
    decision: BankCurationDecision
  }>()
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)
  const [decisionError, setDecisionError] = useState('')
  const pending = useRef(false)

  useEffect(() => {
    if (initialCourses !== undefined) return
    let active = true
    void courseCloService
      .getManagedCourses()
      .then((items) => {
        if (active) setFetchedCourses({ revision: courseRevision, items })
      })
      .catch((failure) => {
        if (active) setFetchedCourses({ revision: courseRevision, error: preparationError(failure) })
      })
    return () => {
      active = false
    }
  }, [initialCourses, courseRevision])

  const courses = useMemo(
    () => initialCourses ?? fetchedCourses?.items ?? [],
    [initialCourses, fetchedCourses?.items],
  )
  const loadingCourses =
    initialCourses === undefined && fetchedCourses?.revision !== courseRevision
  const coursesError =
    initialCourses === undefined && fetchedCourses?.revision === courseRevision
      ? (fetchedCourses.error ?? '')
      : ''

  const activeCourseId = useMemo(() => {
    if (selectedCourseId && courses.some((c) => c.id === selectedCourseId)) {
      return selectedCourseId
    }
    return courses[0]?.id ?? ''
  }, [courses, selectedCourseId])

  const requestKey = `${activeCourseId}:${band}:${revision}`
  const loading = Boolean(activeCourseId) && resultState?.key !== requestKey
  const data = resultState?.data?.courseId === activeCourseId ? resultState.data : undefined
  const error =
    coursesError || (resultState?.key === requestKey ? (resultState.error ?? '') : '')

  useEffect(() => {
    if (!activeCourseId) return
    const controller = new AbortController()
    let active = true

    void service
      .getBankCurationCandidates(activeCourseId, band, controller.signal)
      .then((response) => {
        if (!active || controller.signal.aborted) return
        setResultState({ key: requestKey, data: response })
        setSelectedIds([])
      })
      .catch((failure) => {
        if (!active || controller.signal.aborted) return
        setResultState({ key: requestKey, error: preparationError(failure) })
      })

    return () => {
      active = false
      controller.abort()
    }
  }, [activeCourseId, band, requestKey])

  const items = data?.items ?? []
  const allVisibleSelected =
    items.length > 0 && items.every((item) => selectedIds.includes(item.questionId))

  function toggleOne(questionId: string) {
    setSelectedIds((current) =>
      current.includes(questionId)
        ? current.filter((id) => id !== questionId)
        : [...current, questionId],
    )
  }

  function toggleAll() {
    if (allVisibleSelected) {
      setSelectedIds([])
      return
    }
    setSelectedIds(items.map((item) => item.questionId))
  }

  function openConfirm(questionIds: string[], decision: BankCurationDecision) {
    setConfirmState({ questionIds, decision })
    setNotes('')
    setDecisionError('')
  }

  async function submitDecision() {
    if (pending.current || !confirmState || !activeCourseId) return
    pending.current = true
    setBusy(true)
    setDecisionError('')
    try {
      const result = await service.decideBankCuration(activeCourseId, {
        questionIds: confirmState.questionIds,
        decision: confirmState.decision,
        notes: notes.trim() || undefined,
      })
      toast.success(
        confirmState.decision === 'CURATED_INTO_BANK'
          ? `Đã nạp ${result.processedCount} câu vào ngân hàng môn học.`
          : `Đã loại ${result.processedCount} câu khỏi danh sách nạp kho.`,
      )
      setConfirmState(undefined)
      setSelectedIds([])
      setRevision((current) => current + 1)
    } catch (failure) {
      setDecisionError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }

  return (
    <section
      id="dh-exam-panel-curation"
      role="tabpanel"
      aria-labelledby="dh-exam-tab-curation"
      className={ds.section}
    >
      <SectionHeader
        icon={Database}
        title="Tổng hợp & lọc câu hỏi nạp kho kỳ sau"
        badge={loading || loadingCourses ? 'Đang tải…' : `${data?.totalPendingCount ?? 0} câu chờ lọc`}
        hint="Đối chiếu câu hỏi tạo tay đã duyệt trong các bộ đề trước khi nạp vào ngân hàng dùng chung"
        action={
          <div className="flex min-w-0 flex-wrap items-center justify-end gap-2">
            {courses.length > 0 && (
              <FilterDropMenu
                label="Chọn môn học"
                value={activeCourseId}
                options={courses.map((c) => ({
                  value: c.id,
                  label: `${c.code} · ${c.name}`,
                }))}
                onChange={(value) => {
                  setSelectedCourseId(value)
                  setSelectedIds([])
                }}
                className={ds.filterCourse}
                wrapOptions
                menuMinWidth={0}
                disabled={loading || loadingCourses}
              />
            )}
            <ExamRefreshButton
              onClick={() => {
                if (initialCourses === undefined && (coursesError || courses.length === 0)) {
                  setCourseRevision((current) => current + 1)
                } else {
                  setRevision((current) => current + 1)
                }
              }}
              busy={loading || loadingCourses}
            />
            {selectedIds.length > 0 && (
              <>
                <button
                  type="button"
                  className={ds.primaryButton}
                  disabled={loading}
                  onClick={() => openConfirm(selectedIds, 'CURATED_INTO_BANK')}
                >
                  <Check className="size-4" />
                  Nạp kho ({selectedIds.length})
                </button>
                <button
                  type="button"
                  className={cn(
                    ds.outlineButton,
                    'border-[#c2415b] text-[#c2415b] hover:border-[#a83249] hover:bg-[#fff1f3]',
                  )}
                  disabled={loading}
                  onClick={() => openConfirm(selectedIds, 'EXCLUDED_FROM_BANK')}
                >
                  <ArchiveX className="size-4" />
                  Loại khỏi kho ({selectedIds.length})
                </button>
              </>
            )}
          </div>
        }
      />

      {coursesError ? (
        <div className="p-5">
          <ExamLoadError
            message={coursesError}
            onRetry={() => setCourseRevision((current) => current + 1)}
          />
        </div>
      ) : !loadingCourses && courses.length === 0 ? (
        <div className="p-5">
          <EmptyState
            icon={Database}
            title="Chưa có môn học được phân công"
            description="Bạn chưa quản lý môn học nào để tổng hợp câu hỏi nạp kho."
          />
        </div>
      ) : (
        <>
          <div className="px-4 pt-3 sm:px-5">
            <StatusTabs
              label="Mức độ tương đồng câu hỏi chờ lọc"
              value={band}
              onChange={(value) => setBand(value as BandFilter)}
              options={[
                {
                  value: 'ALL',
                  label: 'Tất cả',
                  count: loading || loadingCourses ? undefined : (data?.totalPendingCount ?? 0),
                  dotClass: 'bg-[#4988C4]',
                },
                {
                  value: 'RED_80_PLUS',
                  label: 'Trùng cao (≥ 80%)',
                  count: loading || loadingCourses ? undefined : (data?.redCount ?? 0),
                  dotClass: 'bg-[#c2415b]',
                },
                {
                  value: 'YELLOW_70_79',
                  label: 'Cảnh báo (70%–79%)',
                  count: loading || loadingCourses ? undefined : (data?.yellowCount ?? 0),
                  dotClass: 'bg-[#d99a10]',
                },
                {
                  value: 'CLEAR',
                  label: 'Không trùng (< 70%)',
                  count: loading || loadingCourses ? undefined : (data?.clearCount ?? 0),
                  dotClass: 'bg-[#16836a]',
                },
              ]}
            />
          </div>

          {!loading && !error && items.length > 0 && (
            <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-y border-[#82aedb]/45 bg-[#F4FBFD] px-5 py-2.5">
              <button
                type="button"
                onClick={toggleAll}
                className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#0F2854] transition hover:bg-[#E7F3FB]"
              >
                {allVisibleSelected ? (
                  <CheckSquare className="size-3.5 text-[#1C4D8D]" />
                ) : (
                  <Square className="size-3.5 text-[#5b6b84]" />
                )}
                {allVisibleSelected ? 'Bỏ chọn tất cả' : 'Chọn tất cả'}
              </button>
              <span className="text-xs font-semibold text-[#5b6b84]">
                Đã chọn {selectedIds.length}/{items.length} câu trong nhóm hiện tại
              </span>
            </div>
          )}

          {loading || loadingCourses ? (
            <LoadingState label="Đang đối chiếu độ tương đồng câu hỏi với ngân hàng…" />
          ) : error ? (
            <div className="p-5">
              <ExamLoadError
                message={error}
                onRetry={() => setRevision((current) => current + 1)}
              />
            </div>
          ) : items.length === 0 ? (
            <div className="p-5">
              <EmptyState
                icon={BookOpenCheck}
                title="Không có câu hỏi chờ lọc"
                description={
                  band === 'ALL'
                    ? 'Tất cả câu hỏi tạo tay đã duyệt của môn này đều đã được xử lý.'
                    : 'Không có câu hỏi nào thuộc nhóm độ tương đồng đang chọn.'
                }
              />
            </div>
          ) : (
            <div className="space-y-4 p-4 sm:p-5">
              {items.map((candidate) => (
                <CandidateComparisonCard
                  key={candidate.questionId}
                  candidate={candidate}
                  selected={selectedIds.includes(candidate.questionId)}
                  onToggle={() => toggleOne(candidate.questionId)}
                  onDecide={(decision) => openConfirm([candidate.questionId], decision)}
                />
              ))}
            </div>
          )}
        </>
      )}

      {confirmState && (
        <FormDialog
          title={
            confirmState.decision === 'CURATED_INTO_BANK'
              ? 'Xác nhận nạp câu hỏi vào ngân hàng'
              : 'Xác nhận loại khỏi danh sách nạp kho'
          }
          eyebrow={`${data?.courseCode ?? ''} · ${confirmState.questionIds.length} câu hỏi`}
          icon={confirmState.decision === 'CURATED_INTO_BANK' ? Check : ArchiveX}
          size="md"
          busy={busy}
          onClose={() => {
            if (!pending.current) setConfirmState(undefined)
          }}
          footer={
            <DialogFooter>
              <button
                type="button"
                className={ds.outlineButton}
                disabled={busy}
                onClick={() => setConfirmState(undefined)}
              >
                Hủy
              </button>
              <button
                type="button"
                className={
                  confirmState.decision === 'CURATED_INTO_BANK'
                    ? ds.primaryButton
                    : cn(ds.primaryButton, 'bg-[#c2415b] hover:bg-[#a83249]')
                }
                disabled={busy}
                onClick={() => void submitDecision()}
              >
                {confirmState.decision === 'CURATED_INTO_BANK' ? (
                  <Check className="size-4" />
                ) : (
                  <ArchiveX className="size-4" />
                )}
                {busy
                  ? 'Đang xử lý…'
                  : confirmState.decision === 'CURATED_INTO_BANK'
                    ? 'Nạp vào kho'
                    : 'Loại khỏi kho'}
              </button>
            </DialogFooter>
          }
        >
          <div className="space-y-3">
            <p className="text-sm leading-relaxed text-[#4a5d75]">
              {confirmState.decision === 'CURATED_INTO_BANK'
                ? `Bạn sắp nạp ${confirmState.questionIds.length} câu hỏi vào ngân hàng dùng chung cho các kỳ tiếp theo.`
                : `Bạn sắp loại ${confirmState.questionIds.length} câu hỏi khỏi danh sách nạp kho. Dữ liệu trong các bài thi lịch sử vẫn được giữ nguyên.`}
            </p>
            <TextareaField
              id="curation-decision-notes"
              label="Ghi chú quyết định (tùy chọn)"
              placeholder="Ghi lý do nạp hoặc loại bỏ câu hỏi…"
              rows={3}
              value={notes}
              disabled={busy}
              onChange={(e) => setNotes(e.target.value)}
            />
            {decisionError && <ExamLoadError message={decisionError} />}
          </div>
        </FormDialog>
      )}
    </section>
  )
}

function CandidateComparisonCard({
  candidate,
  selected,
  onToggle,
  onDecide,
}: {
  candidate: BankCurationCandidate
  selected: boolean
  onToggle: () => void
  onDecide: (decision: BankCurationDecision) => void
}) {
  return (
    <article
      className={cn(
        'rounded-2xl border bg-white p-4 transition-colors',
        selected ? 'border-[#1C4D8D] bg-[#F7FBFF]' : 'border-[#82aedb]/55',
      )}
    >
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2 border-b border-[#82aedb]/35 pb-3">
        <div className="flex flex-wrap items-center gap-2">
          <input
            type="checkbox"
            aria-label={`Chọn câu hỏi ${candidate.title}`}
            checked={selected}
            onChange={onToggle}
            className="size-4 shrink-0 accent-[#1C4D8D]"
          />
          <span className={bandChipClass(candidate.highestBand)}>
            {bandLabel(candidate.highestBand)} · {Math.round(candidate.maxSimilarityPercent)}%
          </span>
          {candidate.sourceSetCodes.map((code) => (
            <span key={code} className={ds.infoChip}>
              Đề {code}
            </span>
          ))}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className={ds.primaryButton}
            onClick={() => onDecide('CURATED_INTO_BANK')}
          >
            <Check className="size-4" />
            Nạp vào kho
          </button>
          <button
            type="button"
            className={cn(
              ds.outlineButton,
              'border-[#c2415b] text-[#c2415b] hover:border-[#a83249] hover:bg-[#fff1f3]',
            )}
            onClick={() => onDecide('EXCLUDED_FROM_BANK')}
          >
            <ArchiveX className="size-4" />
            Loại khỏi kho
          </button>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-xl border border-[#82aedb]/45 bg-[#F9FCFF] p-3.5">
          <p className="text-xs font-bold tracking-[0.06em] text-[#1C4D8D] uppercase">
            Câu hỏi tạo tay đề xuất
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <h3 className="text-sm font-bold wrap-anywhere text-[#0F2854]">{candidate.title}</h3>
            <QuestionDifficultyBadge difficulty={candidate.difficulty} />
            {candidate.cloCode && <span className={ds.infoChip}>{candidate.cloCode}</span>}
            <span className={ds.infoChip}>{candidate.bloomLevel}</span>
          </div>
          <p className="mt-2 text-sm leading-relaxed whitespace-pre-wrap wrap-anywhere text-[#0F2854]">
            {candidate.content}
          </p>
          <p className="mt-3 text-xs text-[#5b6b84]">
            {candidate.submittedByName ? `Người soạn: ${candidate.submittedByName} · ` : ''}
            {DIFFICULTY_LABELS[candidate.difficulty.toLowerCase()] ?? candidate.difficulty} ·{' '}
            {preparationDate(candidate.createdAt)}
          </p>
        </div>

        <div className="rounded-xl border border-[#82aedb]/45 bg-white p-3.5">
          <p className="text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
            Câu hỏi tương đồng ({candidate.matches.length})
          </p>
          {candidate.matches.length === 0 ? (
            <div className="mt-3 rounded-xl border border-[#a9dfc7] bg-[#eaf8f1] px-3 py-2.5 text-sm text-[#16836a]">
              Không phát hiện câu hỏi trùng lặp trong ngân hàng hoặc các đề tạo tay khác (&lt; 70%).
            </div>
          ) : (
            <div className="mt-2 space-y-2.5">
              {candidate.matches.map((match) => (
                <div
                  key={match.comparedQuestionId}
                  className="rounded-xl border border-[#82aedb]/45 bg-[#F9FCFF] p-3"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-sm font-bold wrap-anywhere text-[#0F2854]">
                      {match.title}
                    </span>
                    <span className={bandChipClass(match.band)}>
                      {Math.round(match.similarityPercent)}%
                    </span>
                  </div>
                  <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                    <span className={ds.infoChip}>
                      {match.isFromExistingBank ? 'Trong ngân hàng' : 'Câu tạo tay khác'}
                    </span>
                    {match.sourceSetCodes.map((code) => (
                      <span key={code} className={ds.infoChip}>
                        Đề {code}
                      </span>
                    ))}
                  </div>
                  <p className="mt-2 text-xs leading-relaxed whitespace-pre-wrap wrap-anywhere text-[#4a5d75]">
                    {match.content}
                  </p>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </article>
  )
}
