import { useEffect, useState } from 'react'
import { ArrowRight, CalendarDays, FileCheck2 } from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { useSearchInput } from '@/hooks/useSearchInput'
import { SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { SearchField } from '@/components/common/SearchField'
import { StatusTabs } from '@/components/common/StatusTabs'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { DividedList } from '@/components/common/DividedList'
import { ListPagination } from '@/components/common/ListPagination'
import { ProgressBar } from '@/components/common/ProgressBar'
import {
  ExamLoadError,
  ExamRefreshButton,
  ExamStatusBadge,
} from '@/components/official-exams/ExamPreparationShared'
import { examPreparationService } from '@/services/examPreparation.service'
import { officialExamService } from '@/services/officialExam.service'
import { classService } from '@/services/class.service'
import { courseCloService } from '@/services/courseClo.service'
import { pickCurrentSemester } from '@/lib/semester'
import {
  EXAM_PREPARATION_PATH,
  preparationDate,
  preparationError,
  preparationMatches,
} from '@/lib/examPreparation'
import type { ExamPreparation } from '@/types/examPreparation.types'
import type { OfficialExamSession } from '@/types/officialExam.types'
import type { SemesterOption } from '@/types/class.types'
import type { ManagedCloCourse } from '@/types/courseClo.types'
const filters = [
  { value: '', label: 'Tất cả', dotClass: 'bg-[#4988C4]' },
  { value: 'REQUESTED', label: 'Chưa cấu hình', dotClass: 'bg-[#d99a10]' },
  { value: 'PREPARING', label: 'Đang chuẩn bị', dotClass: 'bg-[#4988C4]' },
  { value: 'READY_FOR_HANDOVER', label: 'Đủ đề bàn giao', dotClass: 'bg-[#16836a]' },
  { value: 'HANDED_OVER', label: 'Đã bàn giao', dotClass: 'bg-[#16836a]' },
]
interface PageData {
  preparations: ExamPreparation[]
  sessions: OfficialExamSession[]
  semesters: SemesterOption[]
  courses: ManagedCloCourse[]
}
export function ExamPreparationPage() {
  const { user } = useAuth()
  const allowed = user?.role === 'DepartmentHead'
  const [params, setParams] = useSearchParams()
  const [now] = useState(Date.now)
  const [revision, setRevision] = useState(0)
  const [result, setResult] = useState<{ revision: number; data?: PageData; error?: string }>()
  const search = useSearchInput((query) => change('q', query), params.get('q') ?? '')
  function change(key: string, value: string) {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    next.delete('page')
    setParams(next, { replace: true })
  }
  useEffect(() => {
    if (!allowed) return
    const controller = new AbortController()
    void Promise.all([
      examPreparationService.list(undefined, controller.signal),
      officialExamService.getSessions(),
      classService.listSemesters(),
      courseCloService.getManagedCourses(),
    ])
      .then(([preparations, sessions, semesters, courses]) => {
        if (!controller.signal.aborted)
          setResult({ revision, data: { preparations, sessions, semesters, courses } })
      })
      .catch((error) => {
        if (!controller.signal.aborted) setResult({ revision, error: preparationError(error) })
      })
    return () => controller.abort()
  }, [allowed, revision])
  if (!allowed) return <ExamLoadError message="Bạn không có quyền quản lý chuẩn bị đề thi." />
  const loading = result?.revision !== revision
  const data = result?.data
  const semesterId = params.get('semester') ?? pickCurrentSemester(data?.semesters ?? [])?.id ?? ''
  const courseId = params.get('course') ?? ''
  const status = params.get('status') ?? ''
  const termRows = (data?.preparations ?? []).filter((p) => p.semesterId === semesterId)
  const courseRows = termRows.filter(
    (p) =>
      (!courseId || p.courseId === courseId) &&
      `${p.title} ${p.courseCode} ${data?.courses.find((c) => c.id === p.courseId)?.name ?? ''}`
        .toLocaleLowerCase('vi-VN')
        .includes(search.query.toLocaleLowerCase('vi-VN')),
  )
  const rows = courseRows.filter((p) => preparationMatches(p, status))
  const pageCount = Math.max(1, Math.ceil(rows.length / 8))
  const page = Math.min(Math.max(1, Number(params.get('page')) || 1), pageCount)
  const courseOptions = (data?.courses ?? [])
    .filter((c) => termRows.some((p) => p.courseId === c.id))
    .map((c) => ({ value: c.id, label: `${c.code} · ${c.name}` }))
  return (
    <section className={ds.section}>
      <SectionHeader
        icon={FileCheck2}
        title="Chuẩn bị đề thi"
        badge={`${termRows.length} yêu cầu`}
        hint="Cấu hình, phân công và duyệt đề theo kỳ thi"
        action={
          <div className="flex min-w-0 flex-wrap items-center justify-end gap-2">
            <FilterDropMenu
              label="Học kỳ"
              value={semesterId}
              options={(data?.semesters ?? []).map((s) => ({ value: s.id, label: s.name }))}
              onChange={(v) => {
                const next = new URLSearchParams(params)
                next.set('semester', v)
                next.delete('course')
                next.delete('page')
                setParams(next, { replace: true })
              }}
              className={ds.filterSemester}
              menuMinWidth={0}
              disabled={loading}
            />
            <FilterDropMenu
              label="Tất cả môn"
              value={courseId}
              options={[{ value: '', label: 'Tất cả môn' }, ...courseOptions]}
              onChange={(v) => change('course', v)}
              className={ds.filterCourse}
              menuMinWidth={0}
              wrapOptions
              disabled={loading}
            />
            <div className={cn('relative', ds.searchControl)}>
              <SearchField
                aria-label="Tìm kỳ thi"
                placeholder="Tên kỳ thi, tên hoặc mã môn"
                value={search.draft}
                onChange={(e) => search.change(e.target.value)}
                onKeyDown={search.onKeyDown}
              />
            </div>
            <ExamRefreshButton onClick={() => setRevision((n) => n + 1)} busy={loading} />
          </div>
        }
      />
      <div className="px-4 pt-3 sm:px-5">
        <StatusTabs
          label="Trạng thái chuẩn bị đề"
          value={status}
          options={filters.map((f) => ({
            ...f,
            count: courseRows.filter((p) => preparationMatches(p, f.value)).length,
          }))}
          onChange={(v) => change('status', v)}
        />
      </div>
      {loading ? (
        <LoadingState />
      ) : result?.error ? (
        <div className="p-5">
          <ExamLoadError message={result.error} onRetry={() => setRevision((n) => n + 1)} />
        </div>
      ) : !rows.length ? (
        <div className="p-5">
          <EmptyState
            icon={FileCheck2}
            title={termRows.length ? 'Chưa có yêu cầu phù hợp' : 'Chưa có yêu cầu chuẩn bị đề'}
            description={
              termRows.length
                ? 'Thử đổi bộ lọc hoặc nội dung tìm kiếm.'
                : 'Yêu cầu chuẩn bị đề do Khảo thí gửi sẽ xuất hiện tại đây.'
            }
          />
        </div>
      ) : (
        <>
          <DividedList as="div" className="mt-2 px-4 sm:px-5">
            {rows.slice((page - 1) * 8, page * 8).map((item) => {
              const session = data?.sessions.find((s) => s.id === item.sessionId)
              const finished = item.status === 'HANDED_OVER'
              const approved = finished ? item.handedOverSetCount : item.approvedSetCount
              const late = !finished && Date.parse(item.deadline) < now
              return (
                <Link
                  key={item.id}
                  to={`${EXAM_PREPARATION_PATH}/${item.sessionId}`}
                  state={{ backTo: `${EXAM_PREPARATION_PATH}?${params.toString()}` }}
                  className="grid min-w-0 items-center gap-4 py-4 focus-visible:outline-2 focus-visible:outline-[#1C4D8D] md:grid-cols-2 xl:grid-cols-[minmax(0,1.5fr)_90px_150px_180px_auto]"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={ds.infoChip}>{item.courseCode}</span>
                      <span className={ds.infoChip}>
                        {session?.examAttemptType === 'RE' ? 'Thi lại' : 'Cuối kỳ'}
                      </span>
                      <ExamStatusBadge status={item.status} preparation />
                    </div>
                    <h3 className="mt-2 text-base font-bold wrap-anywhere text-[#0F2854]">
                      {item.title}
                    </h3>
                    <p className="mt-1 text-sm wrap-anywhere text-[#5b6b84]">
                      {data?.courses.find((c) => c.id === item.courseId)?.name ?? item.courseCode} ·{' '}
                      {item.semesterCode}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-[#78879a]">Cần chuẩn bị</p>
                    <p className="mt-1 text-base font-bold text-[#0F2854]">
                      {item.requiredSetCount} bộ đề
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-[#78879a]">Hạn bàn giao</p>
                    <p
                      className={cn(
                        'mt-1 text-sm font-semibold',
                        late ? 'text-[#c2415b]' : 'text-[#0F2854]',
                      )}
                    >
                      <CalendarDays className="mr-1 inline size-3.5" />
                      {preparationDate(item.deadline)}
                    </p>
                    {late && <p className="mt-1 text-xs font-bold text-[#c2415b]">Đã quá hạn</p>}
                  </div>
                  <div className="min-w-0">
                    <ProgressBar
                      value={approved}
                      max={Math.max(1, item.requiredSetCount)}
                      aria-label={`Tiến độ ${item.title}`}
                    />
                    <p className="mt-2 text-xs font-semibold text-[#5b6b84]">
                      {approved}/{item.requiredSetCount} đề {finished ? 'đã bàn giao' : 'đã duyệt'}
                    </p>
                  </div>
                  <span className={cn(ds.outlineButton, 'justify-self-end')}>
                    Chi tiết
                    <ArrowRight className="size-4" />
                  </span>
                </Link>
              )
            })}
          </DividedList>
          <ListPagination
            label="Phân trang yêu cầu chuẩn bị đề"
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
  )
}
