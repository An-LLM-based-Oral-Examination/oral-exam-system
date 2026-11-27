import { useEffect, useState } from 'react'
import { ArrowRight, FilePenLine } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { useSearchInput } from '@/hooks/useSearchInput'
import { ds } from '@/design/system'
import { SectionHeader } from '@/design/ui'
import { cn } from '@/lib/utils'
import { classService } from '@/services/class.service'
import { courseService } from '@/services/course.service'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { pickCurrentSemester } from '@/lib/semester'
import { preparationDate, preparationError } from '@/lib/examPreparation'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { SearchField } from '@/components/common/SearchField'
import { StatusTabs } from '@/components/common/StatusTabs'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { DividedList } from '@/components/common/DividedList'
import { ListPagination } from '@/components/common/ListPagination'
import { ProgressBar } from '@/components/common/ProgressBar'
import { ExamLoadError, ExamRefreshButton } from '@/components/official-exams/ExamPreparationShared'
import type { ExamPreparation } from '@/types/examPreparation.types'
import type { SemesterOption } from '@/types/class.types'
const root = '/lecturer/exam-authoring'
export function ExamAuthoringPage() {
  const { user } = useAuth()
  const allowed = user?.role === 'Instructor'
  const [resource, setResource] = useState<{
    preparations: ExamPreparation[]
    semesters: SemesterOption[]
    courses: { id: string; code: string; name: string }[]
  }>()
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(true)
  const [semester, setSemester] = useState('')
  const [course, setCourse] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const search = useSearchInput(() => setPage(1))
  useEffect(() => {
    if (!allowed) return
    const controller = new AbortController()
    void Promise.all([
      service.list(undefined, controller.signal),
      classService.listSemesters(),
      courseService.getLecturerCourses(),
    ])
      .then(([preparations, semesters, courses]) => {
        if (!controller.signal.aborted) {
          setResource({ preparations, semesters, courses })
          setError('')
          setLoading(false)
        }
      })
      .catch((failure) => {
        if (!controller.signal.aborted) {
          setError(preparationError(failure))
          setLoading(false)
        }
      })
    return () => controller.abort()
  }, [allowed, revision])
  if (!allowed) return <ExamLoadError message="Bạn không có quyền soạn đề thi." />
  const semesterId = semester || pickCurrentSemester(resource?.semesters ?? [])?.id || ''
  const rows = (resource?.preparations ?? [])
    .filter((p) => p.semesterId === semesterId)
    .flatMap((p) =>
      p.assignments
        .filter((a) => a.lecturerId === user?.id)
        .map((a) => ({
          preparation: p,
          assignment: a,
          state:
            p.status === 'HANDED_OVER' || a.approved + a.handedOver >= a.setCount
              ? 'complete'
              : a.pending + a.approved + a.handedOver >= a.setCount
                ? 'pending'
                : 'working',
        })),
    )
  const matching = rows.filter(
    ({ preparation: p }) =>
      (!course || p.courseId === course) &&
      `${p.title} ${p.courseCode}`.toLowerCase().includes(search.query.toLowerCase()),
  )
  const filtered = matching.filter((r) => !status || r.state === status)
  const pages = Math.max(1, Math.ceil(filtered.length / 8))
  const current = Math.min(page, pages)
  return (
    <section className={ds.section}>
      <SectionHeader
        icon={FilePenLine}
        title="Phân công soạn đề"
        badge={`${rows.length} phân công`}
        hint="Soạn bộ đề theo cấu trúc được giao"
        action={
          <div className="flex min-w-0 flex-wrap justify-end gap-2">
            <FilterDropMenu
              label="Học kỳ"
              value={semesterId}
              options={(resource?.semesters ?? []).map((s) => ({ value: s.id, label: s.name }))}
              onChange={(v) => {
                setSemester(v)
                setCourse('')
                setPage(1)
              }}
              className={ds.filterSemester}
              menuMinWidth={0}
            />
            <FilterDropMenu
              label="Môn học"
              value={course}
              options={[
                { value: '', label: 'Tất cả môn' },
                ...(resource?.courses ?? [])
                  .filter((c) => rows.some((r) => r.preparation.courseId === c.id))
                  .map((c) => ({ value: c.id, label: `${c.code} · ${c.name}` })),
              ]}
              onChange={(v) => {
                setCourse(v)
                setPage(1)
              }}
              className={ds.filterCourse}
              menuMinWidth={0}
              wrapOptions
            />
            <div className={ds.searchControl}>
              <SearchField
                aria-label="Tìm phân công"
                placeholder="Tên kỳ thi hoặc mã môn"
                value={search.draft}
                onChange={(e) => search.change(e.target.value)}
                onKeyDown={search.onKeyDown}
              />
            </div>
            <ExamRefreshButton
              busy={loading}
              onClick={() => {
                setLoading(true)
                setRevision((n) => n + 1)
              }}
            />
          </div>
        }
      />
      <div className="px-4 pt-3 sm:px-5">
        <StatusTabs
          label="Trạng thái phân công"
          value={status}
          onChange={(v) => {
            setStatus(v)
            setPage(1)
          }}
          options={[
            { value: '', label: 'Tất cả' },
            { value: 'working', label: 'Cần soạn / chỉnh sửa' },
            { value: 'pending', label: 'Chờ duyệt' },
            { value: 'complete', label: 'Hoàn thành' },
          ].map((t) => ({
            ...t,
            count: matching.filter((r) => !t.value || r.state === t.value).length,
            dotClass: t.value === 'complete' ? 'bg-[#16836a]' : 'bg-[#4988C4]',
          }))}
        />
      </div>
      {error && (
        <div className="p-5">
          <ExamLoadError
            message={error}
            onRetry={() => {
              setLoading(true)
              setRevision((n) => n + 1)
            }}
          />
        </div>
      )}
      {loading ? (
        <LoadingState />
      ) : !error && !filtered.length ? (
        <div className="p-5">
          <EmptyState
            icon={FilePenLine}
            title="Chưa có phân công phù hợp"
            description="Các kỳ thi được giao soạn đề sẽ xuất hiện tại đây."
          />
        </div>
      ) : (
        !error && (
          <>
            <DividedList as="div" className="px-4 sm:px-5">
              {filtered
                .slice((current - 1) * 8, current * 8)
                .map(({ preparation: p, assignment: a }) => (
                  <Link
                    key={a.id}
                    to={`${root}/${p.sessionId}/${a.id}`}
                    className="grid min-w-0 items-center gap-4 py-5 focus-visible:outline-2 focus-visible:outline-[#1C4D8D] md:grid-cols-[minmax(0,1fr)_125px_160px_auto]"
                  >
                    <div className="min-w-0">
                      <div className="mb-2 flex flex-wrap gap-2">
                        <span className={ds.infoChip}>{p.courseCode}</span>
                        <span className={ds.infoChip}>{p.semesterCode}</span>
                        {a.structureId !== p.examStructureId && (
                          <span className={ds.dangerChip}>Cấu trúc cũ</span>
                        )}
                      </div>
                      <h3 className="text-base font-bold wrap-anywhere text-[#0F2854]">
                        {p.title}
                      </h3>
                      {a.notes && (
                        <p className="mt-1 line-clamp-2 text-sm wrap-anywhere text-[#5b6b84]">
                          {a.notes}
                        </p>
                      )}
                    </div>
                    <div>
                      <p className="text-xs text-[#78879a]">Hạn nộp</p>
                      <p className="mt-1 text-sm font-semibold text-[#0F2854]">
                        {preparationDate(a.deadline)}
                      </p>
                    </div>
                    <div>
                      <ProgressBar
                        value={a.approved + a.handedOver}
                        max={a.setCount}
                        aria-label={`Tiến độ ${p.courseCode}`}
                      />
                      <p className="mt-2 text-xs font-semibold text-[#5b6b84]">
                        {a.approved + a.handedOver}/{a.setCount} đã duyệt · {a.pending} chờ duyệt
                      </p>
                    </div>
                    <span className={cn(ds.outlineButton, 'justify-self-end')}>
                      Soạn đề
                      <ArrowRight className="size-4" />
                    </span>
                  </Link>
                ))}
            </DividedList>
            <ListPagination
              label="Phân trang phân công"
              page={current}
              pageCount={pages}
              onPageChange={setPage}
            />
          </>
        )
      )}
    </section>
  )
}
