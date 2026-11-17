import { SearchField } from '@/components/common/SearchField'
import { useEffect, useState } from 'react'
import { ArrowRight, BookOpen, BookOpenCheck, GraduationCap, Layers, LoaderCircle, UsersRound } from 'lucide-react'
import { Link } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { ds } from '@/design/system'
import { useSearchInput } from '@/hooks/useSearchInput'
import { cn } from '@/lib/utils'
import { courseCodeTone } from '@/lib/courseCodeTone'
import { pickCurrentSemester } from '@/lib/semester'
import { userMessage } from '@/lib/userMessage'
import { ROUTE_PATHS } from '@/routes/routePaths'
import { courseCloService } from '@/services/courseClo.service'
import { classService } from '@/services/class.service'
import { lecturerAssignmentService } from '@/services/lecturerAssignment.service'
import type { ManagedCloCourse } from '@/types/courseClo.types'
import type { SemesterOption } from '@/types/class.types'
import type { AssignmentOffering } from '@/types/lecturerAssignment.types'

interface DepartmentCourses {
  courses: ManagedCloCourse[]
  offerings: AssignmentOffering[]
  semesters: SemesterOption[]
}

export default function DepartmentCoursesPage() {
  const [data, setData] = useState<DepartmentCourses | null>(null)
  const [error, setError] = useState('')
  const [semesterId, setSemesterId] = useState('')
  const [lecturerCounts, setLecturerCounts] = useState<{ semesterId: string; counts: Map<string, number> }>()
  const search = useSearchInput()

  useEffect(() => {
    const controller = new AbortController()
    void Promise.all([
      courseCloService.getManagedCourses(),
      lecturerAssignmentService.offerings(controller.signal),
      classService.listSemesters(),
    ]).then(([courses, offerings, semesters]) => {
      if (controller.signal.aborted) return
      setData({ courses, offerings, semesters })
      setSemesterId(pickCurrentSemester(semesters)?.id ?? '')
    }).catch((failure) => {
      if (!controller.signal.aborted) {
        setError(userMessage(failure, 'Không tải được danh sách môn học của bộ môn.'))
      }
    })

    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (!data || !semesterId) return
    const controller = new AbortController()
    const offerings = data.offerings.filter((offering) => offering.semesterId === semesterId && offering.isOfferingEnabled)
    void Promise.allSettled(offerings.map(async (offering) => ({
      courseId: offering.courseId,
      lecturers: await lecturerAssignmentService.assignments(offering.courseOfferingId, controller.signal),
    }))).then((results) => {
      if (controller.signal.aborted) return
      const idsByCourse = new Map<string, Set<string>>()
      const failedCourses = new Set<string>()
      results.forEach((result, index) => {
        if (result.status === 'rejected') {
          failedCourses.add(offerings[index].courseId)
          return
        }
        const ids = idsByCourse.get(result.value.courseId) ?? new Set<string>()
        result.value.lecturers.filter((lecturer) => lecturer.isActive)
          .forEach((lecturer) => ids.add(lecturer.lecturerId))
        idsByCourse.set(result.value.courseId, ids)
      })
      const counts = new Map(Array.from(idsByCourse)
        .filter(([id]) => !failedCourses.has(id))
        .map(([id, lecturers]) => [id, lecturers.size]))
      setLecturerCounts({ semesterId, counts })
    })
    return () => controller.abort()
  }, [data, semesterId])

  const semestersByCourse = new Map<string, Map<string, string>>()
  const departmentCourseIds = new Set(data?.courses.map((course) => course.id))
  for (const offering of data?.offerings ?? []) {
    if (!departmentCourseIds.has(offering.courseId)) continue
    const courseSemesters = semestersByCourse.get(offering.courseId) ?? new Map<string, string>()
    courseSemesters.set(offering.semesterId, offering.semesterCode)
    semestersByCourse.set(offering.courseId, courseSemesters)
  }

  const keyword = search.query.toLocaleLowerCase('vi')
  const courses = (data?.courses ?? []).filter((course) =>
    semestersByCourse.get(course.id)?.has(semesterId) &&
    (course.code.toLocaleLowerCase('vi').includes(keyword) ||
      course.name.toLocaleLowerCase('vi').includes(keyword)),
  )
  const semesterOptions = (data?.semesters ?? []).map((semester) => ({
    value: semester.id,
    label: semester.name || semester.code,
  }))

  return (
    <div lang="vi" className={ds.page}>
      <section className={ds.section}>
        <div className={cn(ds.sectionHeader, 'flex flex-wrap items-center justify-between gap-3')}>
          <div className={ds.sectionHeading}>
            <div className={ds.sectionTitleRow}>
              <span className={ds.sectionIconWrap}><BookOpen className={ds.sectionIcon} /></span>
              <h1 className={ds.sectionTitle}>Môn học của bộ môn</h1>
              {data && <span className={ds.sectionBadge}>{courses.length} môn</span>}
            </div>
            <p className={ds.sectionHint}>Danh sách môn học thuộc bộ môn phụ trách</p>
          </div>
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <FilterDropMenu
              label="Lọc theo học kỳ"
              options={semesterOptions}
              emptyText="Chưa có học kỳ"
              value={semesterId}
              onChange={setSemesterId}
              className={ds.filterSemester}
              menuMinWidth={ds.filterMenuMinWidth}
              wrapOptions
            />
            <div className={cn('relative', ds.searchControl)}>
              <SearchField
                type="search"
                aria-label="Tìm theo tên hoặc mã môn"
                placeholder="Tìm theo tên hoặc mã môn"
                value={search.draft}
                onChange={(event) => search.change(event.target.value)}
                onKeyDown={search.onKeyDown}
                className={cn(ds.field, 'h-10 min-h-10 rounded-full pl-9 shadow-[0_2px_8px_rgba(23,48,96,0.04)]')}
              />
            </div>
          </div>
        </div>
        <div className={ds.sectionBody}>
          {error ? (
            <p role="alert" className="text-sm text-[#c2415b]">{error}</p>
          ) : !data ? (
            <p role="status" className="flex min-h-32 items-center justify-center gap-2 text-sm text-[#5b6b84]">
              <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />
              Đang tải môn học…
            </p>
          ) : courses.length === 0 ? (
            <EmptyState
              icon={BookOpen}
              title={keyword ? 'Không tìm thấy môn học phù hợp' : semesterId ? 'Chưa có môn học trong học kỳ này' : 'Chưa có học kỳ'}
              description={keyword ? 'Thử chọn học kỳ khác hoặc đổi từ khóa tìm kiếm.' : undefined}
            />
          ) : (
            <ul className="grid grid-cols-1 gap-3 lg:grid-cols-3">
              {courses.map((course) => {
                const courseSemesters = Array.from(semestersByCourse.get(course.id) ?? [])
                  .filter(([id]) => id === semesterId)
                return (
                  <li key={course.id}>
                    <Link
                      to={{
                        pathname: `${ROUTE_PATHS.departmentHead.courses}/${encodeURIComponent(course.id)}`,
                        search: semesterId ? `?semester=${encodeURIComponent(semesterId)}` : '',
                      }}
                      className={cn(ds.softCard, ds.softCardHover, ds.panelShadow, 'group flex flex-col p-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]')}
                    >
                      <div className={ds.softCardAccentWrap} aria-hidden="true">
                        <div className={ds.softCardAccentGlow} />
                        <div className={ds.softCardAccentLine} />
                      </div>
                      <div className="relative flex flex-1 flex-col">
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span className={cn('inline-flex h-6 items-center rounded-md px-2 text-xs font-bold tracking-[0.04em]', courseCodeTone(course.code))}>{course.code}</span>
                            {course.credits != null && (
                              <span className={ds.infoChip}><Layers className="size-3.5" aria-hidden="true" /> {course.credits} tín chỉ</span>
                            )}
                          </div>
                          {courseSemesters.map(([id, label]) => (
                            <span key={id} className={ds.infoChip}>{label}</span>
                          ))}
                        </div>
                        <h2 className="mt-3 line-clamp-2 text-lg leading-snug font-bold text-[#0F2854] sm:text-[19px]">{course.name}</h2>
                        {course.academicProgramName && (
                          <p className="mt-2 flex items-start gap-1.5 text-xs font-semibold text-[#5b6b84]">
                            <GraduationCap className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
                            {course.academicProgramName}
                          </p>
                        )}
                        {course.description?.trim() && (
                          <p className="mt-2 line-clamp-3 text-sm leading-relaxed text-[#5b6b84]">{course.description}</p>
                        )}
                        <div className="mt-auto flex flex-wrap gap-2 pt-2.5">
                          {course.activeCloCount != null && course.totalCloCount != null && (
                            <span className={ds.infoChip}>
                              <BookOpenCheck className="size-3.5" aria-hidden="true" />
                              {course.activeCloCount}/{course.totalCloCount} CLO
                            </span>
                          )}
                          <span className={ds.infoChip}>
                            <UsersRound className="size-3.5" aria-hidden="true" />
                            {lecturerCounts?.semesterId === semesterId
                              ? lecturerCounts.counts.has(course.id)
                                ? `${lecturerCounts.counts.get(course.id)} giảng viên`
                                : 'Chưa tải được số giảng viên'
                              : 'Đang tải giảng viên…'}
                          </span>
                        </div>
                        <div className="mt-0.5 flex items-center justify-between gap-2">
                          {course.isActive !== undefined && (
                            <span className={cn('inline-flex items-center gap-1.5 text-xs font-semibold', course.isActive ? 'text-[#16836a]' : 'text-[#5b6b84]')}>
                              <span className={cn('size-1.5 shrink-0 rounded-full', course.isActive ? 'bg-[#16836a]' : 'bg-[#8aa0b8]')} aria-hidden="true" />
                              {course.isActive ? 'Hoạt động' : 'Ngừng hoạt động'}
                            </span>
                          )}
                          <span className="ml-auto inline-flex h-9 items-center gap-1 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition group-hover:border-[#1C4D8D] group-hover:bg-[#E7F3FB]">
                            Chi tiết <ArrowRight className="size-3.5 transition group-hover:translate-x-0.5" aria-hidden="true" />
                          </span>
                        </div>
                      </div>
                    </Link>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      </section>
    </div>
  )
}
