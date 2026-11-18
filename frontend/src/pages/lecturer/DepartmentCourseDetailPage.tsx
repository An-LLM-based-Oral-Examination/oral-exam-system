import { BackLink } from '@/components/common/BackLink'
import { useEffect, useState } from 'react'
import { BookOpen, BookOpenCheck, CalendarDays, GraduationCap, Layers, Settings2, UsersRound } from 'lucide-react'
import { useParams, useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/common/EmptyState'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import DepartmentCourseClosPage from '@/pages/lecturer/DepartmentCourseClosPage'
import { DepartmentLecturerAssignmentPage } from '@/pages/lecturer/DepartmentLecturerAssignmentPage'
import { CourseConfigurationDialog } from '@/pages/lecturer/CourseConfigurationDialog'
import { ROUTE_PATHS } from '@/routes/routePaths'
import { courseCloService } from '@/services/courseClo.service'
import { lecturerAssignmentService } from '@/services/lecturerAssignment.service'
import type { ManagedCloCourse } from '@/types/courseClo.types'
import type { AssignmentOffering } from '@/types/lecturerAssignment.types'

const tabs = [
  { id: 'lecturers', label: 'Giảng viên', icon: UsersRound },
  { id: 'clos', label: 'Chuẩn đầu ra (CLO)', icon: BookOpenCheck },
] as const

export default function DepartmentCourseDetailPage() {
  const { courseId = '' } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = searchParams.get('tab') === 'clos' ? 'clos' : 'lecturers'
  const [configurationOpen, setConfigurationOpen] = useState(false)
  const [result, setResult] = useState<{ id: string; course?: ManagedCloCourse; error?: string }>()
  const [termResult, setTermResult] = useState<{ id: string; offerings: AssignmentOffering[] }>()
  const [assignmentRevision, setAssignmentRevision] = useState(0)
  const [cloRevision, setCloRevision] = useState(0)
  const [assignmentSummary, setAssignmentSummary] = useState<{ key: string; count: number }>()
  const [cloSummary, setCloSummary] = useState<{ key: string; active: number; total: number }>()
  const detail = result?.id === courseId ? result : undefined
  const managedCourseId = detail?.course?.id
  const offerings = termResult?.id === courseId ? termResult.offerings : undefined
  const semesterCodes = [...new Set(offerings?.map((offering) => offering.semesterCode).filter(Boolean))]
  const requestedSemester = searchParams.get('semester')
  const semester = offerings?.find((offering) => offering.semesterId === requestedSemester) ?? offerings?.[0]
  const semesterId = semester?.semesterId
  const assignmentKey = `${courseId}:${semesterId}:${assignmentRevision}`
  const cloKey = `${courseId}:${cloRevision}`

  useEffect(() => {
    let active = true
    void courseCloService.getManagedCourses()
      .then((courses) => {
        if (active) setResult({ id: courseId, course: courses.find((course) => course.id === courseId) })
      })
      .catch((failure) => {
        if (active) setResult({ id: courseId, error: userMessage(failure, 'Không tải được thông tin môn học.') })
      })
    return () => { active = false }
  }, [courseId])

  useEffect(() => {
    if (!managedCourseId) return
    const controller = new AbortController()
    void lecturerAssignmentService.offerings(controller.signal)
      .then((offerings) => {
        if (controller.signal.aborted) return
        setTermResult({
          id: managedCourseId,
          offerings: offerings.filter((offering) => offering.courseId === managedCourseId && offering.isOfferingEnabled),
        })
      })
      .catch(() => {
        if (!controller.signal.aborted) setTermResult(undefined)
      })
    return () => controller.abort()
  }, [managedCourseId])

  useEffect(() => {
    if (!offerings || !semesterId) return
    const controller = new AbortController()
    const semesterOfferings = offerings.filter((offering) => offering.semesterId === semesterId)
    void Promise.all(semesterOfferings.map((offering) =>
      lecturerAssignmentService.assignments(offering.courseOfferingId, controller.signal),
    )).then((rows) => {
      if (controller.signal.aborted) return
      const ids = new Set(rows.flat().filter((row) => row.isActive).map((row) => row.lecturerId))
      setAssignmentSummary({ key: assignmentKey, count: ids.size })
    }).catch(() => {
      if (!controller.signal.aborted) setAssignmentSummary(undefined)
    })
    return () => controller.abort()
  }, [assignmentKey, offerings, semesterId])

  useEffect(() => {
    if (!managedCourseId) return
    let active = true
    void courseCloService.list(managedCourseId).then((clos) => {
      if (active) setCloSummary({ key: cloKey, active: clos.filter((clo) => clo.isActive).length, total: clos.length })
    }).catch(() => {
      if (active) setCloSummary(undefined)
    })
    return () => { active = false }
  }, [cloKey, managedCourseId])

  return (
    <div lang="vi" className={cn(ds.page, 'space-y-3')}>
      <BackLink
        to={ROUTE_PATHS.departmentHead.courses}>Về danh sách</BackLink>
      {!detail ? (
        <p role="status" className={cn(ds.section, 'p-5 text-sm text-[#5b6b84]')}>Đang tải môn học…</p>
      ) : detail.error ? (
        <p role="alert" className={cn(ds.section, 'p-5 text-sm text-[#c2415b]')}>{detail.error}</p>
      ) : !detail.course ? (
        <EmptyState icon={BookOpen} title="Không tìm thấy môn học thuộc bộ môn" />
      ) : (
        <div className="space-y-6">
          <section className={ds.section}>
            <div className={cn(ds.sectionHeader, 'space-y-3')}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}><BookOpen className={ds.sectionIcon} /></span>
                <h1 className={ds.sectionTitle}>{detail.course.name}</h1>
                <span className={ds.sectionBadge}>{detail.course.code}</span>
                <button type="button" className={cn(ds.outlineButton, 'ml-auto')} onClick={() => setConfigurationOpen(true)}>
                  <Settings2 className="size-4" aria-hidden="true" /> Cấu hình môn học
                </button>
              </div>
              {detail.course.description?.trim() && (
                <p className={cn(ds.sectionHint, 'leading-relaxed whitespace-pre-wrap [overflow-wrap:anywhere]')}>{detail.course.description}</p>
              )}
              {(detail.course.academicProgramName || semesterCodes.length > 0) && (
                <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-[#5b6b84]">
                  {detail.course.academicProgramName && (
                    <span className="inline-flex items-center gap-1.5">
                      <GraduationCap className="size-4 shrink-0" aria-hidden="true" />
                      {detail.course.academicProgramName}
                    </span>
                  )}
                  {semesterCodes.length > 0 && (
                    <span className="inline-flex items-center gap-1.5">
                      <CalendarDays className="size-4 shrink-0" aria-hidden="true" />
                      Học kỳ mở môn: {semesterCodes.join(', ')}
                    </span>
                  )}
                </div>
              )}
              <div className="flex flex-wrap items-center gap-2">
                {detail.course.credits != null && (
                  <span className={ds.infoChip}><Layers className="size-3.5" aria-hidden="true" /> {detail.course.credits} tín chỉ</span>
                )}
                {assignmentSummary?.key === assignmentKey && semester && (
                  <span className={ds.infoChip}>
                    <UsersRound className="size-3.5" aria-hidden="true" /> {assignmentSummary.count} giảng viên
                  </span>
                )}
                {cloSummary?.key === cloKey && (
                  <span className={ds.infoChip}>
                    <BookOpenCheck className="size-3.5" aria-hidden="true" /> {cloSummary.active}/{cloSummary.total} CLO hoạt động
                  </span>
                )}
              </div>
            <div role="tablist" aria-label="Quản lý môn học" className="grid grid-cols-2 gap-1 rounded-2xl border border-[#82aedb]/55 bg-white/80 p-1">
              {tabs.map(({ id, label, icon: Icon }) => (
                <button
                  key={id}
                  type="button"
                  id={`course-tab-${id}`}
                  role="tab"
                  aria-selected={tab === id}
                  aria-controls="course-tab-panel"
                  onClick={() => {
                    const params = new URLSearchParams(searchParams)
                    params.set('tab', id)
                    setSearchParams(params, { replace: true })
                  }}
                  className={cn(
                    'group inline-flex min-h-10 items-center justify-center gap-2 rounded-[14px] px-2 py-2 text-center text-sm font-bold transition sm:text-[15px]',
                    tab === id
                      ? 'bg-[#1C4D8D] text-white shadow-[0_8px_18px_rgba(28,77,141,0.28)]'
                      : 'text-[#5f7088] hover:bg-[#F4FBFD] hover:text-[#0F2854]',
                  )}
                >
                  <Icon className={cn('size-4 shrink-0', tab === id ? 'text-[#BDE8F5]' : 'text-[#7d8fa3]')} aria-hidden="true" />
                  {label}
                </button>
              ))}
            </div>
            </div>
          </section>
          <div id="course-tab-panel" role="tabpanel" aria-labelledby={`course-tab-${tab}`}>
          {tab === 'lecturers' ? (
            <DepartmentLecturerAssignmentPage
              key={courseId}
              courseId={courseId}
              initialSemesterId={requestedSemester ?? ''}
              onSemesterChange={(value) => {
                const params = new URLSearchParams(searchParams)
                params.set('semester', value)
                setSearchParams(params, { replace: true })
              }}
              onAssignmentsChanged={() => setAssignmentRevision((revision) => revision + 1)}
            />
          ) : (
            <DepartmentCourseClosPage
              key={courseId}
              managedCourse={detail.course}
              onClosChanged={() => setCloRevision((revision) => revision + 1)}
            />
          )}
          </div>
        </div>
      )}
      {configurationOpen && detail?.course && (
        <CourseConfigurationDialog key={courseId} courseId={courseId} onClose={() => setConfigurationOpen(false)} />
      )}
    </div>
  )
}
