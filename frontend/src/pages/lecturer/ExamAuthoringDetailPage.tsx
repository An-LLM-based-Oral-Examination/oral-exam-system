import { useRef, useState } from 'react'
import { BookOpenCheck, Clock3, FilePenLine, Plus, Send } from 'lucide-react'
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
import {
  BlueprintFacts,
  ExamLoadError,
  ExamRefreshButton,
} from '@/components/official-exams/ExamPreparationShared'
import { ExamPaperRows } from '@/components/official-exams/ExamPreparationLists'
import { ExamPaperReviewDrawer } from '@/components/official-exams/ExamPaperReviewDrawer'
import { ExamPaperAuthoringDialog } from '@/components/official-exams/ExamPaperAuthoringDialog'
import { ExamBlueprintDialog } from '@/components/official-exams/ExamPreparationDialogs'
import { PAPER_LABELS, preparationDate, preparationError } from '@/lib/examPreparation'
import { paperReady } from '@/lib/examPaperAuthoring'
import { examPreparationService as service } from '@/services/examPreparation.service'
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
  const [submitIds, setSubmitIds] = useState<string[]>()
  const [submitError, setSubmitError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
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
  const papers = data.papers.filter((p) => p.assignmentId === assignment.id && p.isActive)
  const approved = papers.filter((p) => ['OFFICIAL', 'HANDED_OVER'].includes(p.status)).length
  const committed = papers.filter((p) =>
    ['OFFICIAL', 'HANDED_OVER', 'SUBMITTED_FOR_REVIEW'].includes(p.status),
  ).length
  const ready = papers.filter((p) => p.status === 'DRAFT' && paperReady(p, blueprint, clos))
  const canSubmit =
    !locked && !loading && ready.length > 0 && committed + ready.length >= assignment.setCount
  const filtered = papers.filter((p) => !status || p.status === status)
  const pageCount = Math.max(1, Math.ceil(filtered.length / 8))
  const currentPage = Math.min(page, pageCount)
  const selected = papers.find((p) => p.id === selectedId)
  const selectedIndex = filtered.findIndex((p) => p.id === selectedId)
  async function submit() {
    if (pending.current || !submitIds?.length) return
    pending.current = true
    setBusy(true)
    setSubmitError('')
    try {
      const result = await service.submitAssignment(assignmentId, submitIds)
      toast.success(`Đã gửi ${result.submittedCount} bộ đề duyệt.`)
      setSubmitIds(undefined)
      refresh()
    } catch (failure) {
      setSubmitError(preparationError(failure))
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return (
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
              <div className="mt-3 flex flex-wrap gap-2">
                <span className={ds.infoChip}>Được giao {assignment.setCount} bộ đề</span>
                <span className={ds.infoChip}>
                  <Clock3 className="size-3.5" />
                  Hạn nộp: {preparationDate(assignment.deadline)}
                </span>
                {locked && <span className={ds.successChip}>Chỉ xem</span>}
              </div>
            </div>
            <div className="w-full max-w-xs lg:w-56">
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
          title="Cấu trúc được giao"
          hint={blueprint.name}
          action={
            <button
              type="button"
              className={ds.outlineButton}
              onClick={() => setShowBlueprint(true)}
            >
              Xem cấu trúc và CLO
            </button>
          }
        />
        <div className="p-4 sm:px-5">
          <BlueprintFacts blueprint={blueprint} />
        </div>
      </section>
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
                      setSubmitIds(ready.map((p) => p.id))
                      setSubmitError('')
                    }}
                  >
                    <Send className="size-4" />
                    Gửi duyệt ({ready.length})
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
        {!locked && (
          <p className="px-5 pt-3 text-xs text-[#5b6b84]">
            Đã có {committed + ready.length}/{assignment.setCount} bộ đề hợp lệ. Có thể soạn thêm;
            chỉ gửi các bản nháp hoàn chỉnh.
          </p>
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
          <p className="mb-3 text-sm text-[#5b6b84]">
            Chọn các bộ đề hoàn chỉnh để gửi trưởng bộ môn. Đề đã gửi hoặc đã duyệt được tính vào số
            lượng phân công.
          </p>
          <div className="divide-y divide-[#82aedb]/45">
            {ready.map((p) => (
              <label key={p.id} className="flex items-center gap-3 py-3">
                <input
                  type="checkbox"
                  className="size-4 accent-[#1C4D8D]"
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
          <p className="mt-3 text-sm font-semibold text-[#1C4D8D]">
            Đã chọn {submitIds.length} bộ đề · {committed} đề đã gửi / duyệt · Yêu cầu ít nhất{' '}
            {assignment.setCount}
          </p>
          {submitError && (
            <div className="mt-3">
              <ExamLoadError message={submitError} />
            </div>
          )}
        </FormDialog>
      )}
    </div>
  )
}
