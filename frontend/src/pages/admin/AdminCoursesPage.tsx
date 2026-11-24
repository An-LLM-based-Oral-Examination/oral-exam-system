import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import {
  BookOpen,
  BookOpenCheck,
  GraduationCap,
  Pencil,
  Plus,
  RefreshCw,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu, type FilterDropMenuOption } from '@/components/common/FilterDropMenu'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { LoadingState } from '@/components/common/LoadingState'
import { SearchField } from '@/components/common/SearchField'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { toast } from '@/components/common/toastStore'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { useSearchInput } from '@/hooks/useSearchInput'
import { useUrlQueryState } from '@/hooks/useUrlQueryState'
import {
  extractProblemDetails,
  mapProblemFieldErrors,
  type NormalizedProblem,
} from '@/lib/problemDetails'
import { cn } from '@/lib/utils'
import { AdminCourseClosModal } from '@/pages/admin/AdminCourseClosModal'
import { AdminPageHeader } from '@/pages/admin/components/AdminPageHeader'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { adminUi } from '@/pages/admin/components/adminUi'
import {
  adminService,
  type AcademicProgramItem,
  type AdminCourseItem,
  type AdminUserItem,
  type SemesterItem,
} from '@/services/admin.service'

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

const COURSE_QUERY_DEFAULTS = {
  q: '',
  program: '',
  semester: '',
  status: 'all',
} as const

const STATUS_VALUES = ['all', 'active', 'inactive'] as const
type CourseStatusFilter = (typeof STATUS_VALUES)[number]

const STATUS_FILTER_OPTIONS: FilterDropMenuOption[] = [
  { value: 'all', label: 'Tất cả trạng thái' },
  { value: 'active', label: 'Đang mở giảng dạy' },
  { value: 'inactive', label: 'Tạm khóa' },
]

type CourseField =
  | 'code'
  | 'name'
  | 'credits'
  | 'maxMockExamsPerDay'
  | 'academicProgramId'
  | 'semesterId'
  | 'departmentHeadUserId'
const COURSE_FIELDS: readonly CourseField[] = [
  'code',
  'name',
  'credits',
  'maxMockExamsPerDay',
  'academicProgramId',
  'semesterId',
  'departmentHeadUserId',
]

type ProgramField = 'code' | 'name' | 'departmentHeadUserId'
const PROGRAM_FIELDS: readonly ProgramField[] = ['code', 'name', 'departmentHeadUserId']

export function AdminCoursesPage() {
  const { getString, getEnum, setParams, resetParams } =
    useUrlQueryState(COURSE_QUERY_DEFAULTS)

  const urlQuery = getString('q')
  const programFilter = getString('program')
  const semesterFilter = getString('semester')
  const statusFilter: CourseStatusFilter = getEnum('status', STATUS_VALUES, 'all')

  const searchInput = useSearchInput((nextQuery) => {
    setParams({ q: nextQuery })
  }, urlQuery)

  const [courses, setCourses] = useState<AdminCourseItem[]>([])
  const [departmentHeads, setDepartmentHeads] = useState<AdminUserItem[]>([])
  const [semesters, setSemesters] = useState<SemesterItem[]>([])
  const [programs, setPrograms] = useState<AcademicProgramItem[]>([])
  const [loading, setLoading] = useState(true)
  const [listProblem, setListProblem] = useState<NormalizedProblem | null>(null)
  const [auxiliaryProblem, setAuxiliaryProblem] = useState<NormalizedProblem | null>(null)
  const [programsProblem, setProgramsProblem] = useState<NormalizedProblem | null>(null)
  const [statusMessage, setStatusMessage] = useState('')
  const requestIdRef = useRef(0)

  // Create / Edit Course modal
  const [showCourseModal, setShowCourseModal] = useState(false)
  const [editingCourse, setEditingCourse] = useState<AdminCourseItem | null>(null)
  const [courseCode, setCourseCode] = useState('')
  const [courseName, setCourseName] = useState('')
  const [courseCreditsInput, setCourseCreditsInput] = useState('3')
  const [courseMaxMockInput, setCourseMaxMockInput] = useState('3')
  const [courseDescription, setCourseDescription] = useState('')
  const [courseProgramId, setCourseProgramId] = useState('')
  const [courseDeptHeadId, setCourseDeptHeadId] = useState('')
  const [courseSemesterId, setCourseSemesterId] = useState('')
  const [courseIsActive, setCourseIsActive] = useState(true)
  const [courseFieldErrors, setCourseFieldErrors] = useState<
    Partial<Record<CourseField, string>>
  >({})
  const [courseDialogProblem, setCourseDialogProblem] = useState<NormalizedProblem | null>(null)
  const [isSavingCourse, setIsSavingCourse] = useState(false)

  // Create / Edit Academic Program modal
  const [showProgramModal, setShowProgramModal] = useState(false)
  const [editingProgram, setEditingProgram] = useState<AcademicProgramItem | null>(null)
  const [programCode, setProgramCode] = useState('')
  const [programName, setProgramName] = useState('')
  const [programDeptHeadId, setProgramDeptHeadId] = useState('')
  const [programIsActive, setProgramIsActive] = useState(true)
  const [programFieldErrors, setProgramFieldErrors] = useState<
    Partial<Record<ProgramField, string>>
  >({})
  const [programDialogProblem, setProgramDialogProblem] = useState<NormalizedProblem | null>(null)
  const [isSavingProgram, setIsSavingProgram] = useState(false)

  // Assign Course Department Head modal
  const [assignHeadCourse, setAssignHeadCourse] = useState<AdminCourseItem | null>(null)
  const [selectedHeadUserId, setSelectedHeadUserId] = useState('')
  const [assignHeadError, setAssignHeadError] = useState('')
  const [assignHeadProblem, setAssignHeadProblem] = useState<NormalizedProblem | null>(null)
  const [isAssigningHead, setIsAssigningHead] = useState(false)

  // Assign Program Department Head modal
  const [assignProgram, setAssignProgram] = useState<AcademicProgramItem | null>(null)
  const [selectedProgramHeadId, setSelectedProgramHeadId] = useState('')
  const [assignProgramError, setAssignProgramError] = useState('')
  const [assignProgramProblem, setAssignProgramProblem] = useState<NormalizedProblem | null>(null)
  const [isAssigningProgram, setIsAssigningProgram] = useState(false)

  // CLOs modal
  const [closCourse, setClosCourse] = useState<AdminCourseItem | null>(null)

  const loadData = useCallback(async () => {
    const requestId = ++requestIdRef.current
    setLoading(true)
    setListProblem(null)
    setAuxiliaryProblem(null)
    setProgramsProblem(null)

    const [coursesRes, headsRes, semestersRes, programsRes] = await Promise.allSettled([
      adminService.getCourses(true),
      adminService.getDepartmentHeads(),
      adminService.getSemesters(),
      adminService.getAcademicPrograms(),
    ])

    if (requestId !== requestIdRef.current) return

    if (coursesRes.status === 'fulfilled') {
      setCourses(coursesRes.value)
    } else {
      setListProblem(extractProblemDetails(coursesRes.reason, 'Không thể tải danh sách môn học.'))
    }

    const auxProblems: NormalizedProblem[] = []
    if (headsRes.status === 'fulfilled') {
      setDepartmentHeads(headsRes.value)
    } else {
      auxProblems.push(
        extractProblemDetails(headsRes.reason, 'Không thể tải danh sách Trưởng bộ môn.'),
      )
    }

    if (semestersRes.status === 'fulfilled') {
      setSemesters(semestersRes.value)
    } else {
      auxProblems.push(
        extractProblemDetails(semestersRes.reason, 'Không thể tải danh sách học kỳ.'),
      )
    }

    if (auxProblems.length === 1) {
      setAuxiliaryProblem(auxProblems[0])
    } else if (auxProblems.length > 1) {
      const first = auxProblems[0]
      const uniqueDetails = Array.from(new Set(auxProblems.map((p) => p.detail)))
      setAuxiliaryProblem({
        ...first,
        detail: uniqueDetails.join(' '),
        isAuthError: auxProblems.some((p) => p.isAuthError),
        isRateLimited: auxProblems.some((p) => p.isRateLimited),
      })
    }

    if (programsRes.status === 'fulfilled') {
      setPrograms(programsRes.value)
    } else {
      setProgramsProblem(
        extractProblemDetails(programsRes.reason, 'Không thể tải danh sách chương trình đào tạo.'),
      )
    }

    setLoading(false)
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [loadData])

  const programMap = useMemo(() => {
    const map = new Map<string, AcademicProgramItem>()
    for (const p of programs) map.set(p.id, p)
    return map
  }, [programs])

  const semesterMap = useMemo(() => {
    const map = new Map<string, SemesterItem>()
    for (const s of semesters) map.set(s.id, s)
    return map
  }, [semesters])

  const semesterFilterOptions = useMemo<FilterDropMenuOption[]>(() => {
    return [
      { value: '', label: 'Tất cả học kỳ' },
      ...semesters.map((s) => ({ value: s.id, label: `${s.code} — ${s.name}` })),
    ]
  }, [semesters])

  const programFilterOptions = useMemo<FilterDropMenuOption[]>(() => {
    return [
      { value: '', label: 'Tất cả chương trình ĐT' },
      ...programs.map((p) => ({ value: p.id, label: `${p.code} — ${p.name}` })),
    ]
  }, [programs])

  const filteredCourses = useMemo(() => {
    const keyword = urlQuery.trim().toLowerCase()
    return courses.filter((c) => {
      if (statusFilter === 'active' && !c.isActive) return false
      if (statusFilter === 'inactive' && c.isActive) return false
      if (programFilter && c.academicProgramId !== programFilter) return false
      if (semesterFilter && c.semesterId !== semesterFilter) return false
      if (keyword) {
        const matchesCode = c.code.toLowerCase().includes(keyword)
        const matchesName = c.name.toLowerCase().includes(keyword)
        const matchesHead = (c.departmentHeadName ?? '').toLowerCase().includes(keyword)
        const programCode = c.academicProgramId
          ? (programMap.get(c.academicProgramId)?.code ?? '').toLowerCase()
          : ''
        if (!matchesCode && !matchesName && !matchesHead && !programCode.includes(keyword)) {
          return false
        }
      }
      return true
    })
  }, [courses, programFilter, programMap, semesterFilter, statusFilter, urlQuery])

  const hasActiveFilters = Boolean(
    urlQuery || programFilter || semesterFilter || statusFilter !== 'all',
  )

  const selectedCourseProgram = courseProgramId ? programMap.get(courseProgramId) : undefined

  const handleOpenCreateProgram = () => {
    setEditingProgram(null)
    setProgramCode('')
    setProgramName('')
    setProgramDeptHeadId('')
    setProgramIsActive(true)
    setProgramFieldErrors({})
    setProgramDialogProblem(null)
    setShowProgramModal(true)
  }

  const handleOpenEditProgram = (program: AcademicProgramItem) => {
    setEditingProgram(program)
    setProgramCode(program.code)
    setProgramName(program.name)
    setProgramDeptHeadId(program.departmentHeadUserId || '')
    setProgramIsActive(program.isActive)
    setProgramFieldErrors({})
    setProgramDialogProblem(null)
    setShowProgramModal(true)
  }

  const handleSaveProgram = async (event: FormEvent) => {
    event.preventDefault()
    if (isSavingProgram) return

    const errors: Partial<Record<ProgramField, string>> = {}
    const trimmedCode = programCode.trim().toUpperCase()
    const trimmedName = programName.trim()

    if (!trimmedCode) {
      errors.code = 'Vui lòng nhập mã chương trình đào tạo.'
    } else if (trimmedCode.length < 2 || trimmedCode.length > 20 || !/^[A-Z0-9_-]+$/.test(trimmedCode)) {
      errors.code = 'Mã chương trình đào tạo dài 2–20 ký tự, chỉ gồm chữ cái, số, dấu "-" hoặc "_".'
    }

    if (!trimmedName) {
      errors.name = 'Vui lòng nhập tên chương trình đào tạo.'
    } else if (trimmedName.length < 2 || trimmedName.length > 200) {
      errors.name = 'Tên chương trình đào tạo phải từ 2 đến 200 ký tự.'
    }

    if (Object.keys(errors).length > 0) {
      setProgramFieldErrors(errors)
      return
    }

    setIsSavingProgram(true)
    setProgramFieldErrors({})
    setProgramDialogProblem(null)
    try {
      const payload = {
        code: trimmedCode,
        name: trimmedName,
        departmentHeadUserId: editingProgram
          ? programDeptHeadId.trim() || EMPTY_GUID
          : programDeptHeadId.trim() || undefined,
        isActive: programIsActive,
      }

      if (editingProgram) {
        await adminService.updateAcademicProgram(editingProgram.id, payload)
        const msg = `Đã cập nhật chương trình đào tạo [${payload.code}] thành công.`
        toast.success(msg)
        setStatusMessage(msg)
      } else {
        await adminService.createAcademicProgram(payload)
        const msg = `Đã tạo chương trình đào tạo [${payload.code}] thành công.`
        toast.success(msg)
        setStatusMessage(msg)
      }
      setShowProgramModal(false)
      void loadData()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Lưu chương trình đào tạo thất bại.')
      setProgramFieldErrors(
        mapProblemFieldErrors(err, PROGRAM_FIELDS, {
          code: 'code',
          name: 'name',
          departmentHeadUserId: 'departmentHeadUserId',
        }),
      )
      setProgramDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsSavingProgram(false)
    }
  }

  const handleOpenCreateCourse = () => {
    const activeFilteredProgram =
      programFilter && programMap.get(programFilter)?.isActive ? programFilter : ''
    setEditingCourse(null)
    setCourseCode('')
    setCourseName('')
    setCourseCreditsInput('3')
    setCourseMaxMockInput('3')
    setCourseDescription('')
    setCourseProgramId(activeFilteredProgram)
    setCourseDeptHeadId('')
    setCourseSemesterId(semesters.length > 0 ? semesters[0].id : '')
    setCourseIsActive(true)
    setCourseFieldErrors({})
    setCourseDialogProblem(null)
    setShowCourseModal(true)
  }

  const handleOpenEditCourse = (c: AdminCourseItem) => {
    setEditingCourse(c)
    setCourseCode(c.code)
    setCourseName(c.name)
    setCourseCreditsInput(String(c.credits))
    setCourseMaxMockInput(String(c.maxMockExamsPerDay))
    setCourseDescription(c.description || '')
    setCourseProgramId(c.academicProgramId || '')
    setCourseDeptHeadId(c.departmentHeadUserId || '')
    setCourseSemesterId(c.semesterId || (semesters.length > 0 ? semesters[0].id : ''))
    setCourseIsActive(c.isActive)
    setCourseFieldErrors({})
    setCourseDialogProblem(null)
    setShowCourseModal(true)
  }

  const handleSaveCourse = async (event: FormEvent) => {
    event.preventDefault()
    if (isSavingCourse) return

    const errors: Partial<Record<CourseField, string>> = {}
    const trimmedCode = courseCode.trim().toUpperCase()
    const trimmedName = courseName.trim()
    const creditsTrimmed = courseCreditsInput.trim()
    const maxMockTrimmed = courseMaxMockInput.trim()
    const credits = /^-?\d+$/.test(creditsTrimmed)
      ? Number.parseInt(creditsTrimmed, 10)
      : Number.NaN
    const maxMock = /^-?\d+$/.test(maxMockTrimmed)
      ? Number.parseInt(maxMockTrimmed, 10)
      : Number.NaN

    if (!trimmedCode) errors.code = 'Vui lòng nhập mã môn học.'
    if (!trimmedName) errors.name = 'Vui lòng nhập tên môn học.'
    if (Number.isNaN(credits) || credits < 1 || credits > 10) {
      errors.credits = 'Số tín chỉ phải là số nguyên từ 1 đến 10.'
    }
    if (Number.isNaN(maxMock) || maxMock < 1 || maxMock > 20) {
      errors.maxMockExamsPerDay =
        'Hạn ngạch thi thử mỗi ngày (max_mock_exams_per_day) phải từ 1 đến 20 lượt.'
    }
    if (!courseSemesterId.trim()) {
      errors.semesterId = 'Vui lòng chọn học kỳ áp dụng cho môn học.'
    }

    if (Object.keys(errors).length > 0) {
      setCourseFieldErrors(errors)
      return
    }

    setIsSavingCourse(true)
    setCourseFieldErrors({})
    setCourseDialogProblem(null)
    try {
      const trimmedDesc = courseDescription.trim()
      const trimmedProgramId = courseProgramId.trim()
      const shouldInheritProgramHead =
        !editingCourse &&
        !courseDeptHeadId.trim() &&
        Boolean(selectedCourseProgram?.departmentHeadUserId)

      const payload = {
        code: trimmedCode,
        name: trimmedName,
        credits,
        description: editingCourse ? trimmedDesc : trimmedDesc || undefined,
        academicProgramId: trimmedProgramId || EMPTY_GUID,
        departmentHeadUserId: shouldInheritProgramHead
          ? undefined
          : courseDeptHeadId.trim() || EMPTY_GUID,
        semesterId: courseSemesterId.trim(),
        maxMockExamsPerDay: maxMock,
        mockExamDurationMinutes: editingCourse?.mockExamDurationMinutes ?? 30,
        mockExamTotalQuestions: editingCourse?.mockExamTotalQuestions ?? 5,
        isActive: courseIsActive,
      }

      if (editingCourse) {
        await adminService.updateCourse(editingCourse.id, payload)
        const msg = `Đã cập nhật môn học [${payload.code}] thành công.`
        toast.success(msg)
        setStatusMessage(msg)
      } else {
        await adminService.createCourse(payload)
        const msg = `Đã tạo môn học [${payload.code}] thành công.`
        toast.success(msg)
        setStatusMessage(msg)
      }
      setShowCourseModal(false)
      void loadData()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Lưu môn học thất bại.')
      setCourseFieldErrors(
        mapProblemFieldErrors(err, COURSE_FIELDS, {
          code: 'code',
          name: 'name',
          credits: 'credits',
          maxMockExamsPerDay: 'maxMockExamsPerDay',
          academicProgramId: 'academicProgramId',
          semesterId: 'semesterId',
          departmentHeadUserId: 'departmentHeadUserId',
        }),
      )
      setCourseDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsSavingCourse(false)
    }
  }

  const handleOpenAssignCourseHead = (c: AdminCourseItem) => {
    setAssignHeadCourse(c)
    setSelectedHeadUserId(c.departmentHeadUserId || '')
    setAssignHeadError('')
    setAssignHeadProblem(null)
  }

  const handleAssignCourseHead = async (event: FormEvent) => {
    event.preventDefault()
    if (isAssigningHead || !assignHeadCourse) return
    if (!selectedHeadUserId.trim()) {
      setAssignHeadError('Vui lòng chọn Trưởng bộ môn cần gán.')
      return
    }

    setIsAssigningHead(true)
    setAssignHeadError('')
    setAssignHeadProblem(null)
    try {
      await adminService.assignDepartmentHead(assignHeadCourse.id, selectedHeadUserId)
      const msg = `Đã gán Trưởng bộ môn cho môn [${assignHeadCourse.code}] thành công.`
      toast.success(msg)
      setStatusMessage(msg)
      setAssignHeadCourse(null)
      void loadData()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Gán Trưởng bộ môn thất bại.')
      setAssignHeadProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsAssigningHead(false)
    }
  }

  const handleOpenAssignProgramHead = (p: AcademicProgramItem) => {
    setAssignProgram(p)
    setSelectedProgramHeadId(p.departmentHeadUserId || '')
    setAssignProgramError('')
    setAssignProgramProblem(null)
  }

  const handleAssignProgramHead = async (event: FormEvent) => {
    event.preventDefault()
    if (isAssigningProgram || !assignProgram) return
    if (!selectedProgramHeadId.trim()) {
      setAssignProgramError('Vui lòng chọn Trưởng bộ môn phụ trách chương trình đào tạo.')
      return
    }

    setIsAssigningProgram(true)
    setAssignProgramError('')
    setAssignProgramProblem(null)
    try {
      await adminService.assignProgramDepartmentHead(assignProgram.id, selectedProgramHeadId)
      const msg = `Đã gán Trưởng bộ môn cho chương trình [${assignProgram.code}] thành công.`
      toast.success(msg)
      setStatusMessage(msg)
      setAssignProgram(null)
      void loadData()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Gán Trưởng bộ môn cho chương trình thất bại.')
      setAssignProgramProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsAssigningProgram(false)
    }
  }

  const topProblem = useMemo<NormalizedProblem | null>(() => {
    if (listProblem && auxiliaryProblem) {
      if (listProblem.detail === auxiliaryProblem.detail) {
        return listProblem
      }
      return {
        ...listProblem,
        detail: `${listProblem.detail} ${auxiliaryProblem.detail}`,
        isAuthError: listProblem.isAuthError || auxiliaryProblem.isAuthError,
        isRateLimited: listProblem.isRateLimited || auxiliaryProblem.isRateLimited,
      }
    }
    return listProblem ?? auxiliaryProblem
  }, [auxiliaryProblem, listProblem])

  return (
    <div className={ds.page}>
      <AdminPageHeader
        icon={BookOpen}
        title="Môn học, Chương trình ĐT & CLO"
        count={courses.length}
        description="Quản lý chương trình đào tạo, danh mục môn học, hạn ngạch thi thử mỗi ngày, Trưởng bộ môn và chuẩn đầu ra."
        actions={
          <>
            <button
              type="button"
              onClick={() => void loadData()}
              disabled={loading}
              className={adminUi.btnSecondary}
            >
              <RefreshCw
                className={cn('size-4 motion-reduce:animate-none', loading && 'animate-spin')}
                strokeWidth={1.75}
                aria-hidden="true"
              />
              Làm mới
            </button>
            <button type="button" onClick={handleOpenCreateCourse} className={adminUi.btnPrimary}>
              <Plus className="size-4" strokeWidth={1.75} aria-hidden="true" />
              Thêm môn học mới
            </button>
          </>
        }
      />

      <AdminStateBanner
        problem={topProblem}
        statusMessage={statusMessage}
        onRetry={topProblem ? () => void loadData() : undefined}
        onDismiss={() => {
          setListProblem(null)
          setAuxiliaryProblem(null)
          setStatusMessage('')
        }}
      />

      {/* Section 1: Academic Programs */}
      <section className={adminUi.card}>
        <div className={adminUi.sectionBar}>
          <div className="flex items-center gap-2.5">
            <span className="flex size-8 items-center justify-center rounded-lg border border-[#94b8da] bg-white text-[#1C4D8D]">
              <GraduationCap className="size-4" strokeWidth={1.75} aria-hidden="true" />
            </span>
            <h2 className={adminUi.sectionTitle}>Chương trình đào tạo</h2>
            <span className={adminUi.sectionCount}>{programs.length}</span>
          </div>
          <button type="button" onClick={handleOpenCreateProgram} className={adminUi.btnSecondary}>
            <Plus className="size-4" strokeWidth={1.75} aria-hidden="true" />
            Thêm chương trình ĐT
          </button>
        </div>

        <div className="space-y-4 p-4 sm:p-5">
          <AdminStateBanner
            problem={programsProblem}
            onRetry={programsProblem ? () => void loadData() : undefined}
            onDismiss={() => setProgramsProblem(null)}
          />

          {loading ? (
            <LoadingState label="Đang tải chương trình đào tạo…" className="min-h-28" />
          ) : programs.length === 0 ? (
            programsProblem ? null : (
              <EmptyState
                compact
                icon={GraduationCap}
                title="Chưa có chương trình đào tạo nào"
                description="Tạo chương trình đào tạo (ví dụ SE, ENT) để phân nhóm môn học và gán Trưởng bộ môn theo ngành."
                action={
                  <button
                    type="button"
                    onClick={handleOpenCreateProgram}
                    className={adminUi.btnPrimary}
                  >
                    <Plus className="size-4" strokeWidth={1.75} aria-hidden="true" />
                    Thêm chương trình ĐT
                  </button>
                }
              />
            )
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {programs.map((program) => (
                <article
                  key={program.id}
                  className="flex flex-col justify-between gap-3 rounded-xl border border-slate-300 bg-[#f7fafd] p-4 sm:flex-row sm:items-center"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-sm font-bold text-[#1C4D8D]">
                        {program.code}
                      </span>
                      <span
                        className={program.isActive ? adminUi.badgeSuccess : adminUi.badgeNeutral}
                      >
                        {program.isActive ? 'Hoạt động' : 'Tạm khóa'}
                      </span>
                      <span className="text-xs font-medium text-slate-600 tabular-nums">
                        {program.coursesCount} môn học
                      </span>
                    </div>
                    <p className="mt-1 text-sm font-semibold text-[#0F2854]">{program.name}</p>
                    <p className="mt-1 text-[13px] text-slate-600">
                      Trưởng BM ngành:{' '}
                      <span className="font-semibold text-[#0F2854]">
                        {program.departmentHeadName || 'Chưa phân công'}
                      </span>
                    </p>
                  </div>

                  <div className="flex shrink-0 flex-wrap items-center gap-2">
                    <button
                      type="button"
                      aria-label={`Sửa chương trình ĐT ${program.code}`}
                      onClick={() => handleOpenEditProgram(program)}
                      className={cn(adminUi.btnRowNeutral, 'min-h-11 md:min-h-9')}
                    >
                      <Pencil className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
                      Sửa
                    </button>
                    <button
                      type="button"
                      aria-label={`Gán Trưởng BM ngành ${program.code}`}
                      onClick={() => handleOpenAssignProgramHead(program)}
                      className={cn(adminUi.btnRowPrimary, 'min-h-11 md:min-h-9')}
                    >
                      <GraduationCap className="size-4" strokeWidth={1.75} aria-hidden="true" />
                      Gán Trưởng BM ngành
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>
      </section>

      {/* Section 2: Courses */}
      <section className={adminUi.card}>
        <div className={adminUi.sectionBar}>
          <div className="flex items-center gap-2.5">
            <span className="flex size-8 items-center justify-center rounded-lg border border-[#94b8da] bg-white text-[#1C4D8D]">
              <BookOpen className="size-4" strokeWidth={1.75} aria-hidden="true" />
            </span>
            <h2 className={adminUi.sectionTitle}>Danh sách môn học</h2>
            <span className={adminUi.sectionCount}>{filteredCourses.length}</span>
          </div>
        </div>

        {/* Toolbar */}
        <div className={adminUi.toolbar}>
          <div className="relative min-w-0 flex-1 sm:min-w-[220px]">
            <SearchField
              aria-label="Tìm môn học theo mã, tên môn hoặc Trưởng bộ môn"
              placeholder="Tìm mã môn, tên môn, Trưởng BM…"
              value={searchInput.draft}
              onChange={(event) => searchInput.change(event.target.value)}
              onKeyDown={searchInput.onKeyDown}
              className={adminUi.input}
            />
          </div>

          <FilterDropMenu
            label="Chương trình ĐT"
            value={programFilter}
            options={programFilterOptions}
            onChange={(value) => setParams({ program: value })}
            className="w-full sm:w-52"
            buttonClassName={adminUi.filterButton}
          />

          <FilterDropMenu
            label="Học kỳ"
            value={semesterFilter}
            options={semesterFilterOptions}
            onChange={(value) => setParams({ semester: value })}
            className="w-full sm:w-52"
            buttonClassName={adminUi.filterButton}
          />

          <FilterDropMenu
            label="Trạng thái môn học"
            value={statusFilter}
            options={STATUS_FILTER_OPTIONS}
            onChange={(value) => setParams({ status: value })}
            className="w-full sm:w-48"
            buttonClassName={adminUi.filterButton}
          />

          {hasActiveFilters ? (
            <button
              type="button"
              onClick={() => {
                searchInput.reset('')
                resetParams(['q', 'program', 'semester', 'status'])
              }}
              className={adminUi.btnSubtle}
            >
              Xóa bộ lọc
            </button>
          ) : null}
        </div>

        {loading ? (
          <LoadingState label="Đang tải danh sách môn học…" className="min-h-56" />
        ) : filteredCourses.length === 0 ? (
          listProblem ? null : (
            <div className="p-5">
              <EmptyState
                icon={BookOpen}
                title={
                  hasActiveFilters
                    ? 'Không tìm thấy môn học phù hợp'
                    : 'Chưa có môn học nào trong hệ thống'
                }
                description={
                  hasActiveFilters
                    ? 'Thử thay đổi từ khóa tìm kiếm, chương trình ĐT, học kỳ hoặc trạng thái môn học.'
                    : 'Tạo môn học đầu tiên để thiết lập CLO và hạn ngạch thi thử.'
                }
                action={
                  hasActiveFilters ? (
                    <button
                      type="button"
                      onClick={() => {
                        searchInput.reset('')
                        resetParams(['q', 'program', 'semester', 'status'])
                      }}
                      className={adminUi.btnSecondary}
                    >
                      Xóa bộ lọc
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={handleOpenCreateCourse}
                      className={adminUi.btnPrimary}
                    >
                      <Plus className="size-4" strokeWidth={1.75} aria-hidden="true" />
                      Thêm môn học mới
                    </button>
                  )
                }
              />
            </div>
          )
        ) : (
          <>
            {/* Desktop Table */}
            <div className="hidden overflow-x-auto md:block">
              <table className={cn(adminUi.table, 'min-w-[920px]')}>
                <thead className={adminUi.thead}>
                  <tr>
                    <th className="px-5 py-3.5">Mã môn</th>
                    <th className="px-4 py-3.5">Tên môn học</th>
                    <th className="px-4 py-3.5">Tín chỉ</th>
                    <th className="px-4 py-3.5">Trưởng bộ môn</th>
                    <th className="px-4 py-3.5">Hạn ngạch thi thử</th>
                    <th className="px-4 py-3.5">Trạng thái</th>
                    <th className="px-5 py-3.5 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className={adminUi.tbody}>
                  {filteredCourses.map((c) => {
                    const program = c.academicProgramId
                      ? programMap.get(c.academicProgramId)
                      : undefined
                    const semCode =
                      c.semesterCode ||
                      (c.semesterId ? semesterMap.get(c.semesterId)?.code : undefined)

                    return (
                      <tr key={c.id} className={adminUi.tr}>
                        <td className="px-5 py-3.5">
                          <span className="font-mono text-sm font-bold text-[#0F2854]">
                            {c.code}
                          </span>
                          <div className="mt-1 flex flex-wrap gap-1">
                            {program ? (
                              <span className={adminUi.badgeInfo}>{program.code}</span>
                            ) : null}
                            {semCode ? (
                              <span className={cn(adminUi.badgeNeutral, 'font-mono')}>
                                {semCode}
                              </span>
                            ) : null}
                          </div>
                        </td>
                        <td className="px-4 py-3.5 text-sm font-semibold text-[#0F2854]">
                          {c.name}
                        </td>
                        <td className="px-4 py-3.5 text-[13px] font-semibold text-[#0F2854] tabular-nums">
                          {c.credits} TC
                        </td>
                        <td className="px-4 py-3.5 text-[13px]">
                          {c.departmentHeadName ? (
                            <span className="inline-flex items-center gap-1.5 font-semibold text-[#0F2854]">
                              <GraduationCap
                                className="size-4 text-[#1C4D8D]"
                                strokeWidth={1.75}
                                aria-hidden="true"
                              />
                              {c.departmentHeadName}
                            </span>
                          ) : (
                            <span className="text-slate-500">Chưa phân công</span>
                          )}
                        </td>
                        <td className="px-4 py-3.5 font-mono text-[13px] font-semibold text-[#1C4D8D] tabular-nums">
                          {c.maxMockExamsPerDay} lượt/ngày (GMT+7)
                        </td>
                        <td className="px-4 py-3.5">
                          <span className={c.isActive ? adminUi.badgeSuccess : adminUi.badgeNeutral}>
                            {c.isActive ? 'Hoạt động' : 'Tạm khóa'}
                          </span>
                        </td>
                        <td className="px-5 py-3.5">
                          <div className="flex flex-wrap items-center justify-end gap-1.5">
                            <button
                              type="button"
                              aria-label={`Quản lý CLO ${c.code}`}
                              onClick={() => setClosCourse(c)}
                              className={adminUi.btnRowPrimary}
                            >
                              <BookOpenCheck
                                className="size-3.5"
                                strokeWidth={1.75}
                                aria-hidden="true"
                              />
                              CLO
                            </button>
                            <button
                              type="button"
                              aria-label={`Gán Trưởng BM ${c.code}`}
                              onClick={() => handleOpenAssignCourseHead(c)}
                              className={adminUi.btnRowNeutral}
                            >
                              Gán Trưởng BM
                            </button>
                            <button
                              type="button"
                              aria-label={`Sửa môn học ${c.code}`}
                              onClick={() => handleOpenEditCourse(c)}
                              className={adminUi.btnRowNeutral}
                            >
                              <Pencil className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
                              Sửa
                            </button>
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* Mobile Card List */}
            <div className="space-y-3 p-4 md:hidden">
              {filteredCourses.map((c) => {
                const program = c.academicProgramId
                  ? programMap.get(c.academicProgramId)
                  : undefined
                const semCode =
                  c.semesterCode ||
                  (c.semesterId ? semesterMap.get(c.semesterId)?.code : undefined)

                return (
                  <article key={c.id} className={adminUi.mobileCard}>
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-mono text-sm font-bold text-[#0F2854]">
                            {c.code}
                          </span>
                          <span className="text-xs font-semibold text-slate-600 tabular-nums">
                            {c.credits} TC
                          </span>
                          {program ? (
                            <span className={adminUi.badgeInfo}>{program.code}</span>
                          ) : null}
                          {semCode ? (
                            <span className={cn(adminUi.badgeNeutral, 'font-mono')}>
                              {semCode}
                            </span>
                          ) : null}
                        </div>
                        <p className="mt-1 text-sm font-bold text-[#1C4D8D]">{c.name}</p>
                      </div>
                      <span
                        className={cn(
                          'shrink-0',
                          c.isActive ? adminUi.badgeSuccess : adminUi.badgeNeutral,
                        )}
                      >
                        {c.isActive ? 'Hoạt động' : 'Tạm khóa'}
                      </span>
                    </div>

                    <div className="mt-3 space-y-1 text-[13px] text-slate-600">
                      <p>
                        Trưởng BM:{' '}
                        <span className="font-semibold text-[#0F2854]">
                          {c.departmentHeadName || 'Chưa phân công'}
                        </span>
                      </p>
                      <p className="font-mono font-semibold text-[#1C4D8D] tabular-nums">
                        Quota thi thử: {c.maxMockExamsPerDay} lượt/ngày (GMT+7)
                      </p>
                    </div>

                    <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-3">
                      <button
                        type="button"
                        aria-label={`Quản lý CLO ${c.code}`}
                        onClick={() => setClosCourse(c)}
                        className={adminUi.btnMobilePrimary}
                      >
                        <BookOpenCheck className="size-4" strokeWidth={1.75} aria-hidden="true" />
                        CLO
                      </button>
                      <button
                        type="button"
                        aria-label={`Gán Trưởng BM ${c.code}`}
                        onClick={() => handleOpenAssignCourseHead(c)}
                        className={adminUi.btnMobileNeutral}
                      >
                        Gán Trưởng BM
                      </button>
                      <button
                        type="button"
                        aria-label={`Chỉnh sửa môn ${c.code}`}
                        onClick={() => handleOpenEditCourse(c)}
                        className={adminUi.btnMobileNeutral}
                      >
                        <Pencil className="size-4" strokeWidth={1.75} aria-hidden="true" />
                        Sửa
                      </button>
                    </div>
                  </article>
                )
              })}
            </div>
          </>
        )}
      </section>

      {/* Create / Edit Course FormDialog */}
      <FormDialog
        open={showCourseModal}
        variant="calm"
        size="lg"
        busy={isSavingCourse}
        onClose={() => {
          if (!isSavingCourse) setShowCourseModal(false)
        }}
        icon={BookOpen}
        title={editingCourse ? 'Chỉnh sửa môn học' : 'Tạo môn học mới'}
        footer={
          <SubmitDialogFooter
            variant="calm"
            form="admin-course-form"
            onCancel={() => {
              if (!isSavingCourse) setShowCourseModal(false)
            }}
            label={editingCourse ? 'Cập nhật môn học' : 'Tạo môn học'}
            busyLabel="Đang lưu…"
            busy={isSavingCourse}
          />
        }
      >
        <form
          id="admin-course-form"
          onSubmit={handleSaveCourse}
          noValidate
          className="space-y-4"
        >
          <AdminStateBanner problem={courseDialogProblem} />

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div>
              <label htmlFor="course-code" className={adminUi.label}>
                Mã môn
              </label>
              <input
                id="course-code"
                type="text"
                value={courseCode}
                onChange={(event) => {
                  setCourseCode(event.target.value)
                  setCourseFieldErrors((prev) => ({ ...prev, code: undefined }))
                }}
                placeholder="ví dụ: PRN231"
                aria-invalid={Boolean(courseFieldErrors.code)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 font-mono font-semibold uppercase',
                  courseFieldErrors.code && fieldInvalidClass,
                )}
              />
              <FieldError message={courseFieldErrors.code} />
            </div>

            <div className="sm:col-span-2">
              <label htmlFor="course-name" className={adminUi.label}>
                Tên môn học
              </label>
              <input
                id="course-name"
                type="text"
                value={courseName}
                onChange={(event) => {
                  setCourseName(event.target.value)
                  setCourseFieldErrors((prev) => ({ ...prev, name: undefined }))
                }}
                placeholder="ví dụ: Cross-Platform .NET"
                aria-invalid={Boolean(courseFieldErrors.name)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5',
                  courseFieldErrors.name && fieldInvalidClass,
                )}
              />
              <FieldError message={courseFieldErrors.name} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label htmlFor="course-credits" className={adminUi.label}>
                Số tín chỉ (1–10)
              </label>
              <input
                id="course-credits"
                type="number"
                min={1}
                max={10}
                step={1}
                value={courseCreditsInput}
                onChange={(event) => {
                  setCourseCreditsInput(event.target.value)
                  setCourseFieldErrors((prev) => ({ ...prev, credits: undefined }))
                }}
                aria-invalid={Boolean(courseFieldErrors.credits)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 font-mono tabular-nums',
                  courseFieldErrors.credits && fieldInvalidClass,
                )}
              />
              <FieldError message={courseFieldErrors.credits} />
            </div>

            <div>
              <label htmlFor="course-dept-head" className={adminUi.label}>
                Trưởng bộ môn phụ trách
              </label>
              <select
                id="course-dept-head"
                value={courseDeptHeadId}
                onChange={(event) => {
                  setCourseDeptHeadId(event.target.value)
                  setCourseFieldErrors((prev) => ({ ...prev, departmentHeadUserId: undefined }))
                }}
                aria-invalid={Boolean(courseFieldErrors.departmentHeadUserId)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 font-medium',
                  courseFieldErrors.departmentHeadUserId && fieldInvalidClass,
                )}
              >
                <option value="">
                  {!editingCourse && selectedCourseProgram?.departmentHeadName
                    ? `-- Kế thừa Trưởng BM ngành ${selectedCourseProgram.code} (${selectedCourseProgram.departmentHeadName}) --`
                    : '-- Chưa gán Trưởng BM --'}
                </option>
                {editingCourse?.departmentHeadUserId &&
                !departmentHeads.some((dh) => dh.id === editingCourse.departmentHeadUserId) ? (
                  <option value={editingCourse.departmentHeadUserId}>
                    {editingCourse.departmentHeadName || editingCourse.departmentHeadUserId} (Không hoạt động / ngoài danh sách)
                  </option>
                ) : null}
                {departmentHeads.map((dh) => (
                  <option key={dh.id} value={dh.id}>
                    {dh.fullName} ({dh.email})
                  </option>
                ))}
              </select>
              <FieldError message={courseFieldErrors.departmentHeadUserId} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label htmlFor="course-program" className={adminUi.label}>
                Chương trình đào tạo
              </label>
              <select
                id="course-program"
                value={courseProgramId}
                onChange={(event) => {
                  setCourseProgramId(event.target.value)
                  setCourseFieldErrors((prev) => ({ ...prev, academicProgramId: undefined }))
                }}
                aria-invalid={Boolean(courseFieldErrors.academicProgramId)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 font-medium',
                  courseFieldErrors.academicProgramId && fieldInvalidClass,
                )}
              >
                <option value="">-- Chưa gắn chương trình ĐT --</option>
                {programs
                  .filter(
                    (p) =>
                      p.isActive ||
                      (Boolean(editingCourse) && p.id === editingCourse?.academicProgramId),
                  )
                  .map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.code} - {p.name}
                      {!p.isActive ? ' (Tạm khóa)' : ''}
                    </option>
                  ))}
              </select>
              <FieldError message={courseFieldErrors.academicProgramId} />
            </div>

            <div>
              <label htmlFor="course-semester" className={adminUi.label}>
                Học kỳ áp dụng
              </label>
              <select
                id="course-semester"
                value={courseSemesterId}
                onChange={(event) => {
                  setCourseSemesterId(event.target.value)
                  setCourseFieldErrors((prev) => ({ ...prev, semesterId: undefined }))
                }}
                aria-invalid={Boolean(courseFieldErrors.semesterId)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 font-medium',
                  courseFieldErrors.semesterId && fieldInvalidClass,
                )}
              >
                <option value="">-- Chọn học kỳ --</option>
                {semesters.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.code} - {s.name}
                  </option>
                ))}
              </select>
              <FieldError message={courseFieldErrors.semesterId} />
            </div>
          </div>

          <div>
            <label htmlFor="course-description" className={adminUi.label}>
              Mô tả tóm tắt môn học
            </label>
            <textarea
              id="course-description"
              value={courseDescription}
              onChange={(event) => setCourseDescription(event.target.value)}
              placeholder="Mô tả nội dung môn học…"
              className={adminUi.textarea}
            />
          </div>

          <div className={adminUi.infoBox}>
            <label htmlFor="course-max-mock" className={adminUi.label}>
              Hạn ngạch thi thử mỗi ngày (1–20 lượt, GMT+7)
            </label>
            <input
              id="course-max-mock"
              type="number"
              min={1}
              max={20}
              step={1}
              value={courseMaxMockInput}
              onChange={(event) => {
                setCourseMaxMockInput(event.target.value)
                setCourseFieldErrors((prev) => ({ ...prev, maxMockExamsPerDay: undefined }))
              }}
              aria-invalid={Boolean(courseFieldErrors.maxMockExamsPerDay)}
              className={cn(
                ds.field,
                adminUi.input,
                'mt-2 w-44 font-mono font-semibold tabular-nums',
                courseFieldErrors.maxMockExamsPerDay && fieldInvalidClass,
              )}
            />
            <FieldError message={courseFieldErrors.maxMockExamsPerDay} />
            <p className={adminUi.helperText}>
              Số câu, phân bổ Bloom và thời lượng thi thử do Trưởng bộ môn cấu hình ở mục Cấu trúc đề thi.
            </p>
          </div>

          <label className="inline-flex min-h-11 items-center gap-2 text-sm font-medium text-[#0F2854]">
            <input
              type="checkbox"
              id="course-is-active"
              checked={courseIsActive}
              onChange={(event) => setCourseIsActive(event.target.checked)}
              className="size-4 rounded border-slate-400 text-[#1C4D8D] focus:ring-[#4988C4]"
            />
            Đang mở giảng dạy
          </label>
        </form>
      </FormDialog>

      {/* Assign Course Department Head FormDialog */}
      <FormDialog
        open={Boolean(assignHeadCourse)}
        variant="calm"
        size="sm"
        busy={isAssigningHead}
        onClose={() => {
          if (!isAssigningHead) setAssignHeadCourse(null)
        }}
        icon={GraduationCap}
        title="Gán Trưởng bộ môn"
        footer={
          <SubmitDialogFooter
            variant="calm"
            form="admin-assign-course-head-form"
            onCancel={() => {
              if (!isAssigningHead) setAssignHeadCourse(null)
            }}
            label="Lưu phân công"
            busyLabel="Đang lưu…"
            busy={isAssigningHead}
          />
        }
      >
        {assignHeadCourse ? (
          <form
            id="admin-assign-course-head-form"
            onSubmit={handleAssignCourseHead}
            noValidate
            className="space-y-4"
          >
            <AdminStateBanner problem={assignHeadProblem} />

            <div className={adminUi.infoBox}>
              <p className="text-slate-600">Phân công Trưởng bộ môn phụ trách học thuật cho môn:</p>
              <p className="mt-1 text-sm font-bold text-[#0F2854]">
                {assignHeadCourse.code} — {assignHeadCourse.name}
              </p>
            </div>

            <div>
              <label htmlFor="assign-course-head-select" className={adminUi.label}>
                Chọn Trưởng bộ môn
              </label>
              <select
                id="assign-course-head-select"
                value={selectedHeadUserId}
                onChange={(event) => {
                  setSelectedHeadUserId(event.target.value)
                  setAssignHeadError('')
                }}
                aria-invalid={Boolean(assignHeadError)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 font-medium',
                  assignHeadError && fieldInvalidClass,
                )}
              >
                <option value="">-- Chọn Trưởng bộ môn --</option>
                {departmentHeads.map((dh) => (
                  <option key={dh.id} value={dh.id}>
                    {dh.fullName} ({dh.email})
                  </option>
                ))}
              </select>
              <FieldError message={assignHeadError} />
            </div>
          </form>
        ) : null}
      </FormDialog>

      {/* Assign Program Department Head FormDialog */}
      <FormDialog
        open={Boolean(assignProgram)}
        variant="calm"
        size="sm"
        busy={isAssigningProgram}
        onClose={() => {
          if (!isAssigningProgram) setAssignProgram(null)
        }}
        icon={GraduationCap}
        title="Gán Trưởng BM ngành"
        footer={
          <SubmitDialogFooter
            variant="calm"
            form="admin-assign-program-head-form"
            onCancel={() => {
              if (!isAssigningProgram) setAssignProgram(null)
            }}
            label="Lưu phân công ngành"
            busyLabel="Đang lưu…"
            busy={isAssigningProgram}
          />
        }
      >
        {assignProgram ? (
          <form
            id="admin-assign-program-head-form"
            onSubmit={handleAssignProgramHead}
            noValidate
            className="space-y-4"
          >
            <AdminStateBanner problem={assignProgramProblem} />

            <div className={adminUi.infoBox}>
              <p className="text-slate-600">
                Trưởng BM được gán sẽ đồng bộ quyền cho các môn thuộc chương trình:
              </p>
              <p className="mt-1 text-sm font-bold text-[#0F2854]">
                {assignProgram.code} — {assignProgram.name}
              </p>
            </div>

            <div>
              <label htmlFor="assign-program-head-select" className={adminUi.label}>
                Chọn Trưởng bộ môn
              </label>
              <select
                id="assign-program-head-select"
                value={selectedProgramHeadId}
                onChange={(event) => {
                  setSelectedProgramHeadId(event.target.value)
                  setAssignProgramError('')
                }}
                aria-invalid={Boolean(assignProgramError)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 font-medium',
                  assignProgramError && fieldInvalidClass,
                )}
              >
                <option value="">-- Chọn Trưởng bộ môn --</option>
                {departmentHeads.map((dh) => (
                  <option key={dh.id} value={dh.id}>
                    {dh.fullName} ({dh.email})
                  </option>
                ))}
              </select>
              <FieldError message={assignProgramError} />
            </div>
          </form>
        ) : null}
      </FormDialog>

      {/* Create / Edit Academic Program FormDialog */}
      <FormDialog
        open={showProgramModal}
        variant="calm"
        size="md"
        busy={isSavingProgram}
        onClose={() => {
          if (!isSavingProgram) setShowProgramModal(false)
        }}
        icon={GraduationCap}
        title={editingProgram ? 'Chỉnh sửa chương trình đào tạo' : 'Tạo chương trình đào tạo mới'}
        footer={
          <SubmitDialogFooter
            variant="calm"
            form="admin-program-form"
            onCancel={() => {
              if (!isSavingProgram) setShowProgramModal(false)
            }}
            label={editingProgram ? 'Cập nhật chương trình ĐT' : 'Tạo chương trình ĐT'}
            busyLabel="Đang lưu…"
            busy={isSavingProgram}
          />
        }
      >
        <form
          id="admin-program-form"
          onSubmit={handleSaveProgram}
          noValidate
          className="space-y-4"
        >
          <AdminStateBanner problem={programDialogProblem} />

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div>
              <label htmlFor="program-code" className={adminUi.label}>
                Mã chương trình ĐT
              </label>
              <input
                id="program-code"
                type="text"
                maxLength={20}
                value={programCode}
                onChange={(event) => {
                  setProgramCode(event.target.value)
                  setProgramFieldErrors((prev) => ({ ...prev, code: undefined }))
                }}
                placeholder="ví dụ: SE, ENT, AI"
                aria-invalid={Boolean(programFieldErrors.code)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 font-mono font-semibold uppercase',
                  programFieldErrors.code && fieldInvalidClass,
                )}
              />
              <FieldError message={programFieldErrors.code} />
            </div>

            <div className="sm:col-span-2">
              <label htmlFor="program-name" className={adminUi.label}>
                Tên chương trình đào tạo
              </label>
              <input
                id="program-name"
                type="text"
                maxLength={200}
                value={programName}
                onChange={(event) => {
                  setProgramName(event.target.value)
                  setProgramFieldErrors((prev) => ({ ...prev, name: undefined }))
                }}
                placeholder="ví dụ: Bộ môn Tiếng Anh (ENT)"
                aria-invalid={Boolean(programFieldErrors.name)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5',
                  programFieldErrors.name && fieldInvalidClass,
                )}
              />
              <FieldError message={programFieldErrors.name} />
            </div>
          </div>

          <div>
            <label htmlFor="program-dept-head" className={adminUi.label}>
              Trưởng bộ môn phụ trách ngành
            </label>
            <select
              id="program-dept-head"
              value={programDeptHeadId}
              onChange={(event) => {
                setProgramDeptHeadId(event.target.value)
                setProgramFieldErrors((prev) => ({ ...prev, departmentHeadUserId: undefined }))
              }}
              aria-invalid={Boolean(programFieldErrors.departmentHeadUserId)}
              className={cn(
                ds.field,
                adminUi.input,
                'mt-1.5 font-medium',
                programFieldErrors.departmentHeadUserId && fieldInvalidClass,
              )}
            >
              <option value="">-- Chưa phân công Trưởng BM ngành --</option>
              {editingProgram?.departmentHeadUserId &&
              !departmentHeads.some((dh) => dh.id === editingProgram.departmentHeadUserId) ? (
                <option value={editingProgram.departmentHeadUserId}>
                  {editingProgram.departmentHeadName || editingProgram.departmentHeadUserId} (Không hoạt động / ngoài danh sách)
                </option>
              ) : null}
              {departmentHeads.map((dh) => (
                <option key={dh.id} value={dh.id}>
                  {dh.fullName} ({dh.email})
                </option>
              ))}
            </select>
            <FieldError message={programFieldErrors.departmentHeadUserId} />
            <p className={adminUi.helperText}>
              Khi đổi Trưởng bộ môn của ngành, hệ thống đồng bộ quyền quản trị học thuật xuống các
              môn thuộc chương trình này.
            </p>
          </div>

          <label className="inline-flex min-h-11 items-center gap-2 text-sm font-medium text-[#0F2854]">
            <input
              type="checkbox"
              id="program-is-active"
              checked={programIsActive}
              onChange={(event) => setProgramIsActive(event.target.checked)}
              className="size-4 rounded border-slate-400 text-[#1C4D8D] focus:ring-[#4988C4]"
            />
            Đang hoạt động
          </label>
        </form>
      </FormDialog>

      {closCourse ? (
        <AdminCourseClosModal course={closCourse} onClose={() => setClosCourse(null)} />
      ) : null}
    </div>
  )
}
