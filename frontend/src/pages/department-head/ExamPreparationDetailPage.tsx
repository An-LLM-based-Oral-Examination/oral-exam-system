import { useState } from 'react'
import {
  BookOpenCheck,
  CalendarDays,
  Clock3,
  FileCheck2,
  Plus,
  Send,
  Settings2,
  UsersRound,
} from 'lucide-react'
import { useLocation, useParams, useSearchParams } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { useSearchInput } from '@/hooks/useSearchInput'
import { useExamPreparationDetail } from '@/hooks/useExamPreparationDetail'
import { ds } from '@/design/system'
import { SectionHeader } from '@/design/ui'
import { cn } from '@/lib/utils'
import { BackLink } from '@/components/common/BackLink'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { SearchField } from '@/components/common/SearchField'
import { StatusTabs } from '@/components/common/StatusTabs'
import { ProgressBar } from '@/components/common/ProgressBar'
import { ListPagination } from '@/components/common/ListPagination'
import { OfficialFollowUpDialog } from '@/components/official-exams/OfficialFollowUpDialog'
import { ExamPaperReviewDrawer } from '@/components/official-exams/ExamPaperReviewDrawer'
import {
  ExamAssignmentDialog,
  ExamBlueprintDialog,
  ExamDeliveryDialog,
} from '@/components/official-exams/ExamPreparationDialogs'
import { ExamPaperRows, ExamAssignmentRows } from '@/components/official-exams/ExamPreparationLists'
import {
  BlueprintFacts,
  ExamLoadError,
  ExamRefreshButton,
  ExamStatusBadge,
} from '@/components/official-exams/ExamPreparationShared'
import {
  EXAM_PREPARATION_PATH,
  isDeliverable,
  PAPER_LABELS,
  preparationDate,
} from '@/lib/examPreparation'
import type { ExamPaper, PaperStatus } from '@/types/examPreparation.types'
const statuses = [
  '',
  'SUBMITTED_FOR_REVIEW',
  'DRAFT',
  'NEEDS_REVISION',
  'OFFICIAL',
  'REJECTED',
  'HANDED_OVER',
] as const
export function ExamPreparationDetailPage() {
  const { sessionId = '' } = useParams()
  const { user } = useAuth()
  const allowed = user?.role === 'DepartmentHead'
  const { data, loading, error, refresh, updatePaper } = useExamPreparationDetail(
    sessionId,
    allowed,
  )
  const [params, setParams] = useSearchParams()
  const location = useLocation()
  const tab = params.get('tab') === 'assignments' ? 'assignments' : 'papers'
  const filter = params.get('paperStatus') ?? ''
  const authorFilter = params.get('author') ?? ''
  const [dialog, setDialog] = useState<'blueprint' | 'assignment' | 'delivery' | 'followup'>()
  const [selectedId, setSelectedId] = useState<string>()
  const search = useSearchInput((query) => change('q', query), params.get('q') ?? '')
  function change(key: string, value: string) {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    next.delete('page')
    setParams(next, { replace: true })
  }
  const preparation = data?.preparation
  const blueprint = data?.blueprint
  const papers = data?.papers ?? []
  const locked = preparation?.status === 'HANDED_OVER' || data?.session?.status !== 'scheduled'
  const hasBlueprint = Boolean(
    blueprint &&
    blueprint.cloIds.length &&
    blueprint.easyCount + blueprint.mediumCount + blueprint.hardCount === blueprint.totalQuestions,
  )
  const authors = preparation?.assignments ?? []
  function authorName(p: ExamPaper) {
    return (
      authors.find((a) => a.id === p.assignmentId)?.lecturerName ??
      authors.find((a) => a.lecturerId === p.createdBy)?.lecturerName ??
      'Chưa có thông tin người soạn'
    )
  }
  const matching = papers.filter(
    (p) =>
      (!authorFilter || p.createdBy === authorFilter) &&
      `${p.setCode} ${authorName(p)}`
        .toLocaleLowerCase('vi-VN')
        .includes(search.query.toLocaleLowerCase('vi-VN')),
  )
  const filtered = matching.filter((p) => !filter || p.status === filter)
  const selected = papers.find((p) => p.id === selectedId)
  const selectedIndex = filtered.findIndex((p) => p.id === selectedId)
  const pageCount = Math.max(1, Math.ceil(filtered.length / 8))
  const page = Math.min(Math.max(1, Math.trunc(Number(params.get('page')) || 1)), pageCount)
  const eligible = preparation ? papers.filter((p) => isDeliverable(p, preparation)) : []
  function reviewed(id: string, status: PaperStatus, notes: string) {
    const index = filtered.findIndex((p) => p.id === id)
    const next = [...filtered.slice(index + 1), ...filtered.slice(0, index)].find(
      (p) => p.id !== id && p.status === 'SUBMITTED_FOR_REVIEW',
    )
    updatePaper(id, status, notes)
    setSelectedId(next?.id ?? id)
    refresh()
  }
  const state = location.state as { backTo?: string } | null
  const backTo = state?.backTo?.startsWith(EXAM_PREPARATION_PATH)
    ? state.backTo
    : EXAM_PREPARATION_PATH
  if (!allowed) return <ExamLoadError message="Bạn không có quyền quản lý chuẩn bị đề thi." />
  if (!data)
    return (
      <div>
        <BackLink to={backTo} className="mb-3">
          Về danh sách
        </BackLink>
        <section className={ds.section}>
          <SectionHeader icon={FileCheck2} title="Chi tiết yêu cầu" />
          {loading ? (
            <LoadingState />
          ) : (
            <div className="p-5">
              <ExamLoadError
                message={error ?? 'Không tìm thấy yêu cầu chuẩn bị đề.'}
                onRetry={refresh}
              />
            </div>
          )}
        </section>
      </div>
    )
  if (!preparation)
    return (
      <div>
        <BackLink to={backTo} className="mb-3">
          Về danh sách
        </BackLink>
        <EmptyState
          icon={FileCheck2}
          title="Chưa có yêu cầu chuẩn bị đề"
          description="Khảo thí chưa gửi yêu cầu cho kỳ thi này."
        />
      </div>
    )
  const done = preparation.status === 'HANDED_OVER'
  const progress = done ? preparation.handedOverSetCount : preparation.approvedSetCount
  return (
    <div className="min-w-0 space-y-5">
      <div>
        <BackLink to={backTo} className="mb-3">
          Về danh sách
        </BackLink>
        <section className={cn(ds.section, ds.sectionHeader, 'rounded-b-2xl')}>
          <div className="flex min-w-0 flex-wrap items-start justify-between gap-4">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2.5">
                <span className={ds.sectionIconWrap}>
                  <FileCheck2 className="size-4" />
                </span>
                <h1 className={cn(ds.sectionTitle, 'wrap-anywhere')}>{preparation.title}</h1>
                <ExamStatusBadge preparation status={preparation.status} />
              </div>
              <p className="mt-2 text-sm leading-relaxed wrap-anywhere text-[#5b6b84]">
                {data.course?.name ?? preparation.courseCode} ·{' '}
                <strong>{preparation.courseCode}</strong> · {preparation.semesterCode} ·{' '}
                {data.session?.examAttemptType === 'RE' ? 'Thi lại' : 'Thi cuối kỳ'}
              </p>
              <div className="mt-3 flex flex-wrap gap-2">
                <span className={ds.infoChip}>
                  {preparation.requiredSetCount} bộ đề cần chuẩn bị
                </span>
                <span className={ds.infoChip}>
                  <Clock3 className="size-3.5" />
                  Hạn: {preparationDate(preparation.deadline)}
                </span>
                {data.session && (
                  <span className={ds.infoChip}>
                    <CalendarDays className="size-3.5" />
                    Ngày thi: {new Date(data.session.examDate).toLocaleDateString('vi-VN')}
                  </span>
                )}
              </div>
            </div>
            <div className="w-full max-w-xs lg:w-64">
              <ProgressBar
                aria-label="Tiến độ chuẩn bị đề"
                value={progress}
                max={Math.max(1, preparation.requiredSetCount)}
              />
              <p className="mt-2 text-sm font-semibold text-[#1C4D8D]">
                {progress}/{preparation.requiredSetCount} đề {done ? 'đã bàn giao' : 'đã duyệt'}
              </p>
            </div>
          </div>
          {preparation.notes && (
            <p className="mt-3 text-sm leading-relaxed wrap-anywhere whitespace-pre-wrap text-[#5b6b84]">
              <strong>Ghi chú Khảo thí:</strong> {preparation.notes}
            </p>
          )}
          {preparation.handedOverAt && (
            <p className="mt-2 text-sm font-semibold text-[#16836a]">
              Đã bàn giao {preparationDate(preparation.handedOverAt)}
            </p>
          )}
          <div
            role="tablist"
            aria-label="Nội dung yêu cầu chuẩn bị đề"
            className="mt-4 grid grid-cols-2 gap-1 rounded-[18px] border border-[#82aedb]/55 bg-white/80 p-1 shadow-[0_6px_14px_rgba(15,40,84,0.12)]"
          >
            {(
              [
                { id: 'papers', label: 'Bộ đề', icon: BookOpenCheck },
                { id: 'assignments', label: 'Phân công', icon: UsersRound },
              ] as const
            ).map((t) => (
              <button
                key={t.id}
                type="button"
                id={`preparation-tab-${t.id}`}
                role="tab"
                aria-selected={tab === t.id}
                aria-controls="preparation-panel"
                onClick={() => change('tab', t.id)}
                onKeyDown={(e) => {
                  if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
                    e.preventDefault()
                    const next = t.id === 'papers' ? 'assignments' : 'papers'
                    change('tab', next)
                    document.getElementById(`preparation-tab-${next}`)?.focus()
                  }
                }}
                tabIndex={tab === t.id ? 0 : -1}
                className={cn(
                  'flex min-h-10 items-center justify-center gap-2 rounded-[14px] px-3 py-2 text-sm font-bold transition',
                  tab === t.id
                    ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
                    : 'text-[#5f7088] hover:bg-[#F4FBFD]',
                )}
              >
                <t.icon className="size-4" />
                {t.label}
              </button>
            ))}
          </div>
        </section>
      </div>
      {error && <ExamLoadError message={error} onRetry={refresh} />}
      <section className={ds.section}>
        <SectionHeader
          icon={Settings2}
          title="Cấu trúc đề thi"
          hint={blueprint?.name ?? 'Cấu hình trước khi giao giảng viên soạn đề'}
          action={
            <div className="flex flex-wrap gap-2">
              {!locked && data.session && (
                <button
                  type="button"
                  className={ds.outlineButton}
                  disabled={loading}
                  onClick={() => setDialog('followup')}
                >
                  Câu hỏi phụ
                </button>
              )}
              <button
                type="button"
                className={ds.outlineButton}
                disabled={loading || (locked && !blueprint)}
                onClick={() => setDialog('blueprint')}
              >
                <Settings2 className="size-4" />
                {blueprint ? 'Xem cấu trúc' : 'Cấu hình đề'}
              </button>
            </div>
          }
        />
        <div className="p-4 sm:px-5">
          {hasBlueprint && blueprint ? (
            <BlueprintFacts blueprint={blueprint} />
          ) : (
            <p className="text-sm text-[#5b6b84]">
              Chưa cấu hình phân bổ độ khó và phạm vi CLO cho kỳ thi này.
            </p>
          )}
        </div>
      </section>
      <div id="preparation-panel" role="tabpanel" aria-labelledby={`preparation-tab-${tab}`}>
        {tab === 'papers' ? (
          <section className={ds.section}>
            <SectionHeader
              icon={BookOpenCheck}
              title="Danh sách bộ đề"
              badge={`${papers.length} bộ đề`}
              hint="Xem từng câu và quyết định ngay trong bộ đề"
              action={
                <div className="flex min-w-0 flex-wrap items-center justify-end gap-2">
                  <FilterDropMenu
                    label="Tất cả giảng viên"
                    value={authorFilter}
                    options={[
                      { value: '', label: 'Tất cả giảng viên' },
                      ...[
                        ...new Map(
                          authors.map((a) => [
                            a.lecturerId,
                            { value: a.lecturerId, label: a.lecturerName },
                          ]),
                        ).values(),
                      ],
                    ]}
                    onChange={(v) => change('author', v)}
                    className={ds.filterRole}
                    wrapOptions
                    menuMinWidth={0}
                  />
                  <div className={cn('relative', ds.searchControl)}>
                    <SearchField
                      aria-label="Tìm bộ đề"
                      placeholder="Mã đề hoặc giảng viên"
                      value={search.draft}
                      onChange={(e) => search.change(e.target.value)}
                      onKeyDown={search.onKeyDown}
                    />
                  </div>
                  <ExamRefreshButton onClick={refresh} busy={loading} />
                  {!locked && (
                    <button
                      type="button"
                      className={ds.primaryButton}
                      disabled={loading || eligible.length < preparation.requiredSetCount}
                      title={
                        eligible.length < preparation.requiredSetCount
                          ? `Cần ${preparation.requiredSetCount} đề đã duyệt theo cấu trúc hiện tại`
                          : undefined
                      }
                      onClick={() => setDialog('delivery')}
                    >
                      <Send className="size-4" />
                      Bàn giao ({eligible.length}/{preparation.requiredSetCount})
                    </button>
                  )}
                </div>
              }
            />
            <div className="px-4 pt-3 sm:px-5">
              <StatusTabs
                label="Trạng thái bộ đề"
                value={filter}
                options={statuses.map((status) => ({
                  value: status,
                  label: status ? PAPER_LABELS[status] : 'Tất cả',
                  count: matching.filter((p) => !status || p.status === status).length,
                  dotClass: ['OFFICIAL', 'HANDED_OVER'].includes(status)
                    ? 'bg-[#16836a]'
                    : ['REJECTED', 'NEEDS_REVISION'].includes(status)
                      ? 'bg-[#c2415b]'
                      : 'bg-[#d99a10]',
                }))}
                onChange={(v) => change('paperStatus', v)}
              />
            </div>
            {loading ? (
              <LoadingState />
            ) : !filtered.length ? (
              <div className="p-5">
                <EmptyState
                  icon={BookOpenCheck}
                  title={papers.length ? 'Chưa có bộ đề phù hợp' : 'Chưa có bộ đề'}
                  description={
                    papers.length
                      ? 'Thử đổi trạng thái hoặc nội dung tìm kiếm.'
                      : authors.length
                        ? 'Bộ đề sẽ xuất hiện khi giảng viên lưu nháp hoặc gửi duyệt.'
                        : 'Phân công giảng viên để bắt đầu soạn đề.'
                  }
                  action={
                    !authors.length && !locked && hasBlueprint ? (
                      <button
                        type="button"
                        className={ds.primaryButton}
                        onClick={() => setDialog('assignment')}
                      >
                        <Plus className="size-4" />
                        Phân công soạn đề
                      </button>
                    ) : undefined
                  }
                />
              </div>
            ) : (
              <>
                <ExamPaperRows
                  papers={filtered.slice((page - 1) * 8, page * 8)}
                  structureId={preparation.examStructureId}
                  selectedId={selectedId}
                  authorName={authorName}
                  onSelect={setSelectedId}
                />
                <ListPagination
                  label="Phân trang bộ đề"
                  page={page}
                  pageCount={pageCount}
                  onPageChange={(n) => {
                    const next = new URLSearchParams(params)
                    next.set('page', String(n))
                    setParams(next, { replace: true })
                  }}
                />
              </>
            )}
          </section>
        ) : (
          <section className={ds.section}>
            <SectionHeader
              icon={UsersRound}
              title="Phân công soạn đề"
              badge={`${authors.length} phân công`}
              hint="Theo dõi số bộ đề được giao và tiến độ giảng viên"
              action={
                <div className="flex items-center gap-2">
                  <ExamRefreshButton onClick={refresh} busy={loading} />
                  {!locked && (
                    <button
                      type="button"
                      className={ds.primaryButton}
                      disabled={!hasBlueprint || loading}
                      onClick={() => setDialog('assignment')}
                    >
                      <Plus className="size-4" />
                      Phân công soạn đề
                    </button>
                  )}
                </div>
              }
            />
            {loading ? (
              <LoadingState />
            ) : !authors.length ? (
              <div className="p-5">
                <EmptyState
                  icon={UsersRound}
                  title="Chưa có phân công soạn đề"
                  description={
                    hasBlueprint
                      ? 'Chọn giảng viên được phân công môn học để giao soạn đề.'
                      : 'Cấu hình cấu trúc đề trước khi phân công.'
                  }
                  action={
                    !locked ? (
                      <button
                        type="button"
                        className={ds.primaryButton}
                        onClick={() => setDialog(hasBlueprint ? 'assignment' : 'blueprint')}
                      >
                        <Plus className="size-4" />
                        {hasBlueprint ? 'Phân công soạn đề' : 'Cấu hình đề'}
                      </button>
                    ) : undefined
                  }
                />
              </div>
            ) : (
              <ExamAssignmentRows assignments={authors} structureId={preparation.examStructureId} />
            )}
          </section>
        )}
      </div>
      {dialog === 'followup' && data.session && (
        <OfficialFollowUpDialog
          session={data.session}
          onClose={() => setDialog(undefined)}
          onSaved={refresh}
        />
      )}
      {dialog === 'blueprint' && (
        <ExamBlueprintDialog
          sessionId={sessionId}
          current={blueprint}
          clos={data.clos}
          locked={locked}
          onClose={() => setDialog(undefined)}
          onSaved={refresh}
        />
      )}
      {dialog === 'assignment' && (
        <ExamAssignmentDialog
          preparation={preparation}
          offeringId={data.offeringId}
          onClose={() => setDialog(undefined)}
          onSaved={refresh}
        />
      )}
      {dialog === 'delivery' && (
        <ExamDeliveryDialog
          preparation={preparation}
          papers={papers}
          onClose={() => setDialog(undefined)}
          onSaved={() => {
            setSelectedId(undefined)
            refresh()
          }}
        />
      )}
      {selected && (
        <ExamPaperReviewDrawer
          key={selected.id}
          paper={selected}
          author={authorName(selected)}
          clos={data.clos}
          readOnly={locked || !selected.assignmentId}
          onClose={() => setSelectedId(undefined)}
          onPrevious={
            selectedIndex > 0 ? () => setSelectedId(filtered[selectedIndex - 1].id) : undefined
          }
          onNext={
            selectedIndex >= 0 && selectedIndex < filtered.length - 1
              ? () => setSelectedId(filtered[selectedIndex + 1].id)
              : undefined
          }
          onReviewed={reviewed}
        />
      )}
    </div>
  )
}
