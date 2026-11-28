import { DialogScope } from '@/design/DialogContext'
import { useEffect, useMemo, useRef, useState } from 'react'
import {
  AlertTriangle,
  BookOpenCheck,
  Check,
  Clock3,
  FileCheck2,
  FilePenLine,
  Plus,
  Send,
  Settings2,
  Trash2,
  X,
} from 'lucide-react'
import { useParams } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { useExamPreparationDetail } from '@/hooks/useExamPreparationDetail'
import { ds } from '@/design/system'
import { SectionHeader } from '@/design/ui'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { cn } from '@/lib/utils'
import { BackLink } from '@/components/common/BackLink'
import { StatusTabs } from '@/components/common/StatusTabs'
import { ListPagination } from '@/components/common/ListPagination'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { ProgressBar } from '@/components/common/ProgressBar'
import { toast } from '@/components/common/toastStore'
import { ExamLoadError, ExamRefreshButton } from '@/components/official-exams/ExamPreparationShared'
import { ExamPaperRows } from '@/components/official-exams/ExamPreparationLists'
import { ExamPaperReviewDrawer } from '@/components/official-exams/ExamPaperReviewDrawer'
import { ExamPaperAuthoringDialog } from '@/components/official-exams/ExamPaperAuthoringDialog'
import { ExamBlueprintDialog } from '@/components/official-exams/ExamPreparationDialogs'
import { SamplePaperViewerDialog } from '@/components/official-exams/SamplePaperDialogs'
import { PAPER_LABELS, preparationDate, preparationError } from '@/lib/examPreparation'
import { paperReady } from '@/lib/examPaperAuthoring'
import { examPreparationService as service } from '@/services/examPreparation.service'
import type { AvailablePaperQuestion, ExamPaper } from '@/types/examPreparation.types'
export function ExamAuthoringDetailPage() {
  const { sessionId = '', assignmentId = '' } = useParams()
  const { user } = useAuth()
  const allowed = user?.role === 'Instructor'
  const { data, loading, error, refresh } = useExamPreparationDetail(sessionId, allowed, 'lecturer')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState<string>()
  const [editing, setEditing] = useState<string | null>()
  const [showBlueprint, setShowBlueprint] = useState(false)
  const [showSample, setShowSample] = useState(false)
  const [checkedIds, setCheckedIds] = useState<string[]>([])
  const [submitIds, setSubmitIds] = useState<string[]>()
  const [submitError, setSubmitError] = useState('')
  const [deletingPaper, setDeletingPaper] = useState<ExamPaper>()
  const [deleteError, setDeleteError] = useState('')
  const [busy, setBusy] = useState(false)
  const [availableUsageState, setAvailableUsageState] = useState<{
    scopeKey: string
    rows: AvailablePaperQuestion[]
  }>({ scopeKey: '', rows: [] })
  const pending = useRef(false)
  const loadedAssignment = data?.preparation
    ? data.preparation.assignments.find((a) => a.id === assignmentId && a.lecturerId === user?.id)
    : undefined
  const loadedStructureId = loadedAssignment?.structureId
  const firstDraftId = loadedAssignment
    ? data?.papers.find(
        (p) =>
          !p.isSampleSet &&
          p.assignmentId === loadedAssignment.id &&
          p.isActive &&
          ['DRAFT', 'NEEDS_REVISION'].includes(p.status),
      )?.id
    : undefined
  const usageScopeKey =
    sessionId && loadedStructureId && firstDraftId
      ? `${sessionId}:${loadedStructureId}:${firstDraftId}`
      : ''
  useEffect(() => {
    if (
      !sessionId ||
      !loadedStructureId ||
      !firstDraftId ||
      !usageScopeKey ||
      typeof service.availableQuestions !== 'function'
    ) {
      return
    }
    let active = true
    void service
      .availableQuestions(loadedStructureId, sessionId, undefined, firstDraftId)
      .then((rows) => {
        if (active && Array.isArray(rows)) {
          setAvailableUsageState({ scopeKey: usageScopeKey, rows })
        }
      })
      .catch(() => {
        // Không chặn trang danh sách nếu kiểm tra câu vàng tạm thời thất bại
      })
    return () => {
      active = false
    }
  }, [firstDraftId, loadedStructureId, sessionId, usageScopeKey])
  const yellowUsageMap = useMemo(() => {
    const map = new Map<string, AvailablePaperQuestion>()
    const rows =
      usageScopeKey && availableUsageState.scopeKey === usageScopeKey
        ? availableUsageState.rows
        : []
    for (const row of rows) {
      map.set(row.questionId, row)
      if (row.sourceQuestionId) map.set(row.sourceQuestionId, row)
    }
    return map
  }, [availableUsageState, usageScopeKey])
  const back = (
    <BackLink to="/lecturer/exam-authoring" className="mb-3">
      Về danh sách
    </BackLink>
  )
  if (!allowed) return <ExamLoadError message="Bạn không có quyền soạn đề thi." />
  if (!data)
    return (
      <div>
        {back}
        <section className={ds.section}>
          <SectionHeader icon={FilePenLine} title="Chi tiết phân công" />
          {loading ? (
            <LoadingState />
          ) : (
            <div className="p-5">
              <ExamLoadError message={error ?? 'Không thể tải phân công.'} onRetry={refresh} />
            </div>
          )}
        </section>
      </div>
    )
  const prep = data.preparation
  const assignment = prep?.assignments.find(
    (a) => a.id === assignmentId && a.lecturerId === user?.id,
  )
  if (!prep || !assignment)
    return (
      <div>
        {back}
        <EmptyState
          icon={FilePenLine}
          title="Không tìm thấy phân công"
          description="Phân công không tồn tại hoặc không thuộc tài khoản của bạn."
        />
      </div>
    )
  const blueprint = { ...assignment.blueprint, id: assignment.structureId }
  const clos = data.clos.filter((c) => blueprint.cloIds.includes(c.id))
  const locked = prep.status === 'HANDED_OVER' || data.session?.status !== 'scheduled'
  const samplePaper = data.papers.find(
    (p) =>
      Boolean(p.isSampleSet) &&
      p.structureId === assignment.structureId &&
      p.isActive &&
      p.status !== 'REJECTED',
  )
  const papers = data.papers.filter(
    (p) => !p.isSampleSet && p.assignmentId === assignment.id && p.isActive,
  )
  const approved = papers.filter((p) => ['OFFICIAL', 'HANDED_OVER'].includes(p.status)).length
  const committed = papers.filter((p) =>
    ['OFFICIAL', 'HANDED_OVER', 'SUBMITTED_FOR_REVIEW'].includes(p.status),
  ).length
  const ready = papers.filter((p) => p.status === 'DRAFT' && paperReady(p, blueprint, clos))
  const checkedReady = ready.filter((p) => checkedIds.includes(p.id))
  const sendable = status === 'DRAFT' ? checkedReady : ready
  const canSubmit =
    !locked && !loading && sendable.length > 0 && committed + sendable.length >= assignment.setCount
  const filtered = papers.filter((p) => !status || p.status === status)
  const pageCount = Math.max(1, Math.ceil(filtered.length / 8))
  const currentPage = Math.min(page, pageCount)
  const selected = papers.find((p) => p.id === selectedId)
  const selectedIndex = filtered.findIndex((p) => p.id === selectedId)
  function paperYellowSummary(p: ExamPaper) {
    const total = blueprint.totalQuestions || p.questions.length
    const matchedYellowCount = p.questions.filter((q) => {
      const originKey = q.sourceQuestionId ?? q.questionId
      return Boolean(
        yellowUsageMap.get(originKey)?.isYellowPendingByOtherLecturer ||
          yellowUsageMap.get(q.questionId)?.isYellowPendingByOtherLecturer,
      )
    }).length
    const yellowCount =
      yellowUsageMap.size > 0
        ? matchedYellowCount
        : (p.yellowOverlapQuestionCount ??
          (typeof p.yellowOverlapRatio === 'number'
            ? Math.round(p.yellowOverlapRatio * total)
            : 0))
    const ratio = total > 0 ? yellowCount / total : 0
    return {
      setCode: p.setCode,
      yellowCount,
      total,
      ratio,
      percent: Math.round(ratio * 100),
    }
  }
  const exceedingDraftPapers = papers
    .filter((p) => ['DRAFT', 'NEEDS_REVISION'].includes(p.status))
    .map(paperYellowSummary)
    .filter((summary) => summary.ratio > 0.7)
  const selectedSubmitYellowWarnings = (submitIds ?? [])
    .map((id) => papers.find((p) => p.id === id))
    .filter((p): p is ExamPaper => Boolean(p))
    .map(paperYellowSummary)
    .filter((summary) => summary.yellowCount > 0 && (summary.ratio > 0.7 || summary.ratio >= 0.5))
  async function submit() {
    if (pending.current || !submitIds?.length) return
    pending.current = true
    setBusy(true)
    setSubmitError('')
    try {
      const result = await service.submitAssignment(assignmentId, submitIds)
      toast.success(`Đã gửi ${result.submittedCount} bộ đề duyệt.`)
      setSubmitIds(undefined)
      setCheckedIds([])
      refresh()
    } catch (failure) {
      setSubmitError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  async function archiveDraft() {
    if (pending.current || !deletingPaper) return
    pending.current = true
    setBusy(true)
    setDeleteError('')
    try {
      await service.archiveDraftPaper(deletingPaper.id)
      toast.success(`Đã xóa bản nháp ${deletingPaper.setCode}.`)
      setDeletingPaper(undefined)
      refresh()
    } catch (failure) {
      setDeleteError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return (
    <DialogScope name={prep.title}>
      <div className="min-w-0 space-y-5">
      <div>
        {back}
        <section className={cn(ds.section, ds.sectionHeader, 'rounded-b-2xl')}>
          <div className="flex min-w-0 flex-wrap items-start justify-between gap-4">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className={ds.sectionIconWrap}>
                  <FilePenLine className="size-4" />
                </span>
                <h1 className={cn(ds.sectionTitle, 'wrap-anywhere')}>{prep.title}</h1>
                {assignment.structureId !== prep.examStructureId && (
                  <span className={ds.dangerChip}>Cấu trúc cũ</span>
                )}
              </div>
              <p className="mt-2 text-sm wrap-anywhere text-[#5b6b84]">
                {data.course?.name ?? prep.courseCode} · <strong>{prep.courseCode}</strong> ·{' '}
                {prep.semesterCode}
              </p>
              <p className="mt-2 text-sm leading-relaxed font-semibold text-[#1C4D8D]">
                {blueprint.totalQuestions} câu · {blueprint.durationMinutes} phút · Dễ{' '}
                {blueprint.easyCount} / TB {blueprint.mediumCount} / Khó {blueprint.hardCount} ·{' '}
                {blueprint.cloIds.length} CLO
              </p>
              <div className="mt-3 flex flex-wrap gap-2">
                <span className={ds.infoChip}>Được giao {assignment.setCount} bộ đề</span>
                <span className={ds.infoChip}>
                  <Clock3 className="size-3.5" />
                  Hạn nộp: {preparationDate(assignment.deadline)}
                </span>
                {locked && <span className={ds.successChip}>Chỉ xem</span>}
              </div>
            </div>
            <div className="flex w-full max-w-xs flex-col gap-3 lg:w-56">
              <div className="flex flex-wrap justify-end gap-2">
                {samplePaper && (
                  <button
                    type="button"
                    className={ds.outlineButton}
                    onClick={() => setShowSample(true)}
                  >
                    <FileCheck2 className="size-4" />
                    Xem đề mẫu tham khảo
                  </button>
                )}
                <button
                  type="button"
                  className={ds.outlineButton}
                  onClick={() => setShowBlueprint(true)}
                >
                  <Settings2 className="size-4" />
                  Xem cấu trúc
                </button>
              </div>
              <div>
                <ProgressBar
                  value={approved}
                  max={assignment.setCount}
                  aria-label="Tiến độ phân công"
                />
                <p className="mt-2 text-sm font-semibold text-[#1C4D8D]">
                  {approved}/{assignment.setCount} bộ đề đã duyệt
                </p>
              </div>
            </div>
          </div>
          {assignment.notes && (
            <p className="mt-3 text-sm wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
              <strong>Ghi chú trưởng bộ môn:</strong> {assignment.notes}
            </p>
          )}
        </section>
      </div>
      {error && <ExamLoadError message={error} onRetry={refresh} />}
      <section className={ds.section}>
        <SectionHeader
          icon={BookOpenCheck}
          title="Bộ đề của tôi"
          badge={`${papers.length} bộ đề`}
          hint="Lưu nháp, rà soát và gửi trưởng bộ môn duyệt"
          action={
            <div className="flex min-w-0 flex-wrap justify-end gap-2">
              <ExamRefreshButton busy={loading} onClick={refresh} />
              {!locked && (
                <>
                  <button
                    type="button"
                    className={ds.outlineButton}
                    disabled={!canSubmit}
                    title={
                      !canSubmit
                        ? `Cần ít nhất ${assignment.setCount} bộ đề hợp lệ; hiện có ${committed + ready.length}`
                        : undefined
                    }
                    onClick={() => {
                      setSubmitIds(sendable.map((p) => p.id))
                      setSubmitError('')
                    }}
                  >
                    <Send className="size-4" />
                    Gửi duyệt ({sendable.length})
                  </button>
                  <button
                    type="button"
                    className={ds.primaryButton}
                    disabled={loading}
                    onClick={() => setEditing(null)}
                  >
                    <Plus className="size-4" />
                    Tạo bộ đề
                  </button>
                </>
              )}
            </div>
          }
        />
        <div className="px-4 pt-3 sm:px-5">
          <StatusTabs
            label="Trạng thái bộ đề của tôi"
            value={status}
            onChange={(v) => {
              setStatus(v)
              setCheckedIds([])
              setPage(1)
            }}
            options={[
              '',
              'DRAFT',
              'NEEDS_REVISION',
              'SUBMITTED_FOR_REVIEW',
              'OFFICIAL',
              'REJECTED',
              'HANDED_OVER',
            ].map((value) => ({
              value,
              label: value ? PAPER_LABELS[value] : 'Tất cả',
              count: papers.filter((p) => !value || p.status === value).length,
              dotClass: ['OFFICIAL', 'HANDED_OVER'].includes(value)
                ? 'bg-[#16836a]'
                : ['NEEDS_REVISION', 'REJECTED'].includes(value)
                  ? 'bg-[#c2415b]'
                  : 'bg-[#d99a10]',
            }))}
          />
        </div>
        {!locked && status === 'DRAFT' && ready.length > 0 && (
          <div className="mt-3 flex flex-wrap items-center gap-2 border-y border-[#82aedb]/55 bg-[#F4FBFD] px-5 py-3">
            <button
              type="button"
              disabled={loading}
              onClick={() =>
                setCheckedIds(checkedReady.length === ready.length ? [] : ready.map((p) => p.id))
              }
              className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#5b6b84] transition duration-200 hover:-translate-y-0.5 hover:bg-white disabled:opacity-45 motion-reduce:transform-none"
            >
              {checkedReady.length === ready.length ? (
                <X className="size-3.5" />
              ) : (
                <Check className="size-3.5" />
              )}
              {checkedReady.length === ready.length ? 'Bỏ chọn' : 'Chọn tất cả'}
            </button>
            <span className="text-xs font-semibold text-[#5b6b84]">
              Đã chọn {checkedReady.length} bộ đề hoàn chỉnh
            </span>
          </div>
        )}
        {!locked && (
          <p className="px-5 pt-3 text-xs text-[#5b6b84]">
            Đã có {committed + ready.length}/{assignment.setCount} bộ đề hợp lệ. Có thể soạn thêm;
            chỉ gửi các bản nháp hoàn chỉnh.
          </p>
        )}
        {!locked && exceedingDraftPapers.length > 0 && (
          <div
            role="alert"
            className="mx-5 mt-3 flex items-start gap-2.5 rounded-xl border border-[#e7b45c] bg-[#fff4df] px-4 py-3 text-xs leading-relaxed font-semibold text-[#8c5206]"
          >
            <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <div>
              <strong>Cảnh báo vượt ngưỡng 70% câu hỏi trùng chờ duyệt:</strong>{' '}
              {exceedingDraftPapers
                .map((item) => `${item.setCode} (${item.yellowCount}/${item.total} câu · ${item.percent}%)`)
                .join(', ')}
              . Vui lòng thay thế bớt câu hỏi trùng chờ duyệt trước khi gửi Trưởng bộ môn.
            </div>
          </div>
        )}
        {loading ? (
          <LoadingState />
        ) : !filtered.length ? (
          <div className="p-5">
            <EmptyState
              icon={BookOpenCheck}
              title={papers.length ? 'Chưa có bộ đề phù hợp' : 'Chưa có bộ đề'}
              description={
                papers.length
                  ? 'Chọn trạng thái khác để xem bộ đề.'
                  : 'Tạo bộ đề theo cấu trúc được phân công.'
              }
              action={
                !locked && !papers.length ? (
                  <button
                    type="button"
                    className={ds.primaryButton}
                    onClick={() => setEditing(null)}
                  >
                    <Plus className="size-4" />
                    Tạo bộ đề
                  </button>
                ) : undefined
              }
            />
          </div>
        ) : (
          <>
            <ExamPaperRows
              papers={filtered.slice((currentPage - 1) * 8, currentPage * 8)}
              structureId={assignment.structureId}
              authorName={() => user?.fullName ?? 'Bạn'}
              selectedId={selectedId}
              onSelect={setSelectedId}
              onEdit={locked ? undefined : setEditing}
              onDelete={
                locked
                  ? undefined
                  : (paper) => {
                      setDeletingPaper(paper)
                      setDeleteError('')
                    }
              }
              selection={
                !locked && status === 'DRAFT'
                  ? {
                      ids: checkedReady.map((p) => p.id),
                      eligibleIds: ready.map((p) => p.id),
                      onToggle: (id) =>
                        setCheckedIds((ids) =>
                          ids.includes(id) ? ids.filter((v) => v !== id) : [...ids, id],
                        ),
                    }
                  : undefined
              }
            />
            <ListPagination
              label="Phân trang bộ đề"
              page={currentPage}
              pageCount={pageCount}
              onPageChange={setPage}
            />
          </>
        )}
      </section>
      {showBlueprint && (
        <ExamBlueprintDialog
          sessionId={sessionId}
          session={data.session}
          current={blueprint}
          clos={data.clos}
          locked
          onClose={() => setShowBlueprint(false)}
          onSaved={() => {}}
        />
      )}
      {editing !== undefined && !locked && (
        <ExamPaperAuthoringDialog
          key={editing ?? 'new'}
          preparation={prep}
          assignment={assignment}
          blueprint={blueprint}
          clos={data.clos}
          paper={editing ? papers.find((p) => p.id === editing) : undefined}
          onClose={() => setEditing(undefined)}
          onSaved={refresh}
        />
      )}
      {selected && (
        <ExamPaperReviewDrawer
          key={selected.id}
          paper={selected}
          author={user?.fullName ?? 'Bạn'}
          clos={data.clos}
          readOnly
          reviewer={false}
          onClose={() => setSelectedId(undefined)}
          onReviewed={() => {}}
          onPrevious={
            selectedIndex > 0 ? () => setSelectedId(filtered[selectedIndex - 1].id) : undefined
          }
          onNext={
            selectedIndex >= 0 && selectedIndex < filtered.length - 1
              ? () => setSelectedId(filtered[selectedIndex + 1].id)
              : undefined
          }
        />
      )}
      {submitIds && (
        <FormDialog
          title="Gửi bộ đề duyệt"
          eyebrow={prep.courseCode}
          icon={Send}
          size="md"
          busy={busy}
          onClose={() => setSubmitIds(undefined)}
          footer={
            <DialogFooter>
              <button
                type="button"
                className={ds.outlineButton}
                disabled={busy}
                onClick={() => setSubmitIds(undefined)}
              >
                Hủy
              </button>
              <button
                type="button"
                className={ds.primaryButton}
                disabled={
                  busy || !submitIds.length || committed + submitIds.length < assignment.setCount
                }
                onClick={() => void submit()}
              >
                <Send className="size-4" />
                {busy ? 'Đang gửi…' : 'Xác nhận gửi'}
              </button>
            </DialogFooter>
          }
        >
          <div className="mb-3 grid grid-cols-3 gap-2">
            {[
              {
                label: 'Được giao',
                count: assignment.setCount,
                tone: 'border-[#82aedb]/55 bg-[#E7F3FB] text-[#1C4D8D]',
              },
              {
                label: 'Đã gửi / duyệt',
                count: committed,
                tone: 'border-[#a9dfc7] bg-[#eaf8f1] text-[#16836a]',
              },
              {
                label: 'Đang chọn',
                count: submitIds.length,
                tone: 'border-[#cfc0ef] bg-[#f2edfc] text-[#7046ba]',
              },
            ].map((item) => (
              <div key={item.label} className={cn('rounded-xl border p-3 shadow-sm', item.tone)}>
                <p className="text-xs font-semibold">{item.label}</p>
                <p className="mt-1 text-xl font-bold tabular-nums">
                  {item.count} <span className="text-xs font-medium">bộ đề</span>
                </p>
              </div>
            ))}
          </div>
          <div className="mb-2 flex items-center justify-between gap-2">
            <h3 className="text-sm font-bold text-[#0F2854]">Bản nháp hoàn chỉnh</h3>
            <span className={ds.infoChip}>{ready.length} bộ đề</span>
          </div>
          <p className="mb-3 text-sm text-[#5b6b84]">Chọn bộ đề để gửi trưởng bộ môn duyệt.</p>
          <div className="divide-y divide-[#82aedb]/40 overflow-hidden rounded-xl border border-[#82aedb]/55 bg-white">
            {ready.map((p) => (
              <label
                key={p.id}
                className={cn(
                  'flex cursor-pointer items-center gap-3 p-3 transition-colors',
                  submitIds.includes(p.id) ? 'bg-[#E7F3FB]' : 'bg-white hover:bg-[#F4FBFD]',
                  busy && 'cursor-wait',
                )}
              >
                <input
                  type="checkbox"
                  className="size-4 shrink-0 accent-[#1C4D8D]"
                  checked={submitIds.includes(p.id)}
                  disabled={busy}
                  onChange={(e) =>
                    setSubmitIds((ids) =>
                      e.target.checked ? [...(ids ?? []), p.id] : ids?.filter((id) => id !== p.id),
                    )
                  }
                />
                <span className="min-w-0 text-sm font-bold wrap-anywhere text-[#0F2854]">
                  {p.setCode}
                </span>
                <span className={cn(ds.successChip, 'ml-auto shrink-0')}>
                  {p.questions.length} câu
                </span>
              </label>
            ))}
          </div>
          <div
            className="mt-3 space-y-2 rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] p-3"
            aria-live="polite"
          >
            <div className="flex items-center justify-between gap-2 text-sm">
              <span className="font-semibold text-[#5b6b84]">Sau khi gửi</span>
              <strong className="text-[#1C4D8D]">
                {committed + submitIds.length}/{assignment.setCount} bộ đề
              </strong>
            </div>
            <ProgressBar
              max={100}
              aria-label="Tiến độ gửi bộ đề theo phân công"
              value={Math.min(
                100,
                ((committed + submitIds.length) / Math.max(1, assignment.setCount)) * 100,
              )}
            />
            <p
              className={cn(
                'text-xs',
                committed + submitIds.length >= assignment.setCount
                  ? 'text-[#16836a]'
                  : 'text-[#a5660e]',
              )}
            >
              {committed + submitIds.length >= assignment.setCount
                ? 'Đã đủ số lượng theo phân công.'
                : `Chọn thêm ${assignment.setCount - committed - submitIds.length} bộ đề để đủ số lượng phân công.`}
            </p>
          </div>
          {selectedSubmitYellowWarnings.length > 0 && (
            <div
              role={selectedSubmitYellowWarnings.some((w) => w.ratio > 0.7) ? 'alert' : 'status'}
              className="mt-3 flex items-start gap-2.5 rounded-xl border border-[#e7b45c] bg-[#fff4df] px-4 py-3 text-xs leading-relaxed text-[#8c5206]"
            >
              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
              <div className="space-y-1">
                <p className="font-bold">
                  {selectedSubmitYellowWarnings.some((w) => w.ratio > 0.7)
                    ? 'Cảnh báo vượt ngưỡng trùng chờ duyệt (> 70%)'
                    : 'Lưu ý tỷ lệ câu hỏi trùng chờ duyệt'}
                </p>
                {selectedSubmitYellowWarnings.map((item) => (
                  <p key={item.setCode}>
                    Bộ đề <strong>{item.setCode}</strong>: {item.yellowCount}/{item.total} câu (
                    {item.percent}%) đang được giảng viên khác chọn chờ duyệt
                    {item.ratio > 0.7
                      ? ' — vượt ngưỡng 70%, hệ thống sẽ từ chối khi gửi duyệt. Hãy thay thế câu hỏi trước.'
                      : '.'}
                  </p>
                ))}
              </div>
            </div>
          )}
          {submitError && (
            <div className="mt-3">
              <ExamLoadError message={submitError} />
            </div>
          )}
        </FormDialog>
      )}
      {showSample && samplePaper && (
        <SamplePaperViewerDialog
          paper={samplePaper}
          clos={data.clos}
          onClose={() => setShowSample(false)}
        />
      )}
      {deletingPaper && (
        <FormDialog
          title="Xóa bản nháp bộ đề"
          eyebrow={deletingPaper.setCode}
          icon={Trash2}
          size="sm"
          busy={busy}
          onClose={() => {
            if (!pending.current) setDeletingPaper(undefined)
          }}
          footer={
            <DialogFooter>
              <button
                type="button"
                className={ds.outlineButton}
                disabled={busy}
                onClick={() => setDeletingPaper(undefined)}
              >
                Hủy
              </button>
              <button
                type="button"
                className={cn(ds.primaryButton, 'bg-[#c2415b] hover:bg-[#a83249]')}
                disabled={busy}
                onClick={() => void archiveDraft()}
              >
                <Trash2 className="size-4" />
                {busy ? 'Đang xóa…' : 'Xác nhận xóa nháp'}
              </button>
            </DialogFooter>
          }
        >
          <div className="space-y-3">
            <p className="text-sm leading-relaxed text-[#4a5d75]">
              Bản nháp <strong>{deletingPaper.setCode}</strong> sẽ được hủy kích hoạt và giải phóng
              lượt sử dụng câu hỏi đã chọn. Lịch sử phiên bản vẫn được lưu giữ để đối soát.
            </p>
            {deleteError && <ExamLoadError message={deleteError} />}
          </div>
        </FormDialog>
      )}
    </div>
    </DialogScope>
  )
}
