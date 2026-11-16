/* eslint-disable react-hooks/set-state-in-effect */
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import {
  ArrowRight,
  BookOpen,
  CalendarDays,
  CalendarClock,
  CheckCircle2,
  ClipboardList,
  GraduationCap,
  LoaderCircle,
  MapPin,
  Plus,
  RefreshCw,
  Search,
  Users,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import { classService } from '@/services/class.service'
import { courseService, type ShiftStaffCandidate } from '@/services/course.service'
import { officialExamService } from '@/services/officialExam.service'
import type { SemesterCourseOffering, SemesterOption } from '@/types/class.types'
import type {
  CreateOfficialExamSessionRequest,
  ExamShift,
  OfficialExamSession,
} from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'
import {
  DEFAULT_SHIFT_CAPACITY,
  MAX_SHIFT_CAPACITY,
  MIN_SHIFT_CAPACITY,
  getShiftCapacityMetrics,
  validateShiftCapacityInput,
} from '@/lib/shiftCapacity'

interface ShiftFormState {
  semesterId: string
  courseId: string
  sessionId: string
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  proctorUserId: string
  examinerUserId: string
  maxSeats: string
}

interface EditShiftFormState {
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  proctorUserId: string
  examinerUserId: string
  maxSeats: string
}

interface CreatedShiftNotice {
  courseCode: string
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  maxSeats: number
}

const initialShift: ShiftFormState = {
  semesterId: '',
  courseId: '',
  sessionId: '',
  shiftName: '',
  roomLab: '',
  startTime: '',
  endTime: '',
  proctorUserId: '',
  examinerUserId: '',
  maxSeats: String(DEFAULT_SHIFT_CAPACITY),
}

function getTodayDate() {
  return new Date().toISOString().slice(0, 10)
}

function isDateInsideSemester(date: string, semester: SemesterOption | undefined) {
  if (!semester || !date || !semester.startDate || !semester.endDate) return true
  const normalizedDate = date.slice(0, 10)
  return (
    normalizedDate >= semester.startDate.slice(0, 10) &&
    normalizedDate <= semester.endDate.slice(0, 10)
  )
}

function getSessionDate(session: OfficialExamSession) {
  return session.examDate.slice(0, 10)
}

function toUtcIso(localDateTime: string) {
  const parsedDate = new Date(localDateTime)
  if (Number.isNaN(parsedDate.getTime())) {
    throw new Error('Thời gian ca thi không hợp lệ.')
  }
  return parsedDate.toISOString()
}

function toDateTimeLocalValue(value: string) {
  if (!value) return ''
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) return value
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value.slice(0, 16)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}T${pad(parsed.getHours())}:${pad(parsed.getMinutes())}`
}

export function ExaminationOfficerExamManagementPage() {
  const [semesters, setSemesters] = useState<SemesterOption[]>([])
  const [offerings, setOfferings] = useState<SemesterCourseOffering[]>([])
  const [sessions, setSessions] = useState<OfficialExamSession[]>([])
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [proctorOptions, setProctorOptions] = useState<ShiftStaffCandidate[]>([])
  const [examinerOptions, setExaminerOptions] = useState<ShiftStaffCandidate[]>([])
  const [shiftForm, setShiftForm] = useState(initialShift)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingOfferings, setIsLoadingOfferings] = useState(false)
  const [isLoadingLecturers, setIsLoadingLecturers] = useState(false)
  const [busy, setBusy] = useState(false)
  const [isCreateShiftOpen, setIsCreateShiftOpen] = useState(false)
  const [editingShift, setEditingShift] = useState<ExamShift | null>(null)
  const [editShiftForm, setEditShiftForm] = useState<EditShiftFormState>({
    shiftName: '',
    roomLab: '',
    startTime: '',
    endTime: '',
    proctorUserId: '',
    examinerUserId: '',
    maxSeats: String(DEFAULT_SHIFT_CAPACITY),
  })
  const [editProctorOptions, setEditProctorOptions] = useState<ShiftStaffCandidate[]>([])
  const [editExaminerOptions, setEditExaminerOptions] = useState<ShiftStaffCandidate[]>([])
  const [editBusy, setEditBusy] = useState(false)
  const [editError, setEditError] = useState('')
  const [shiftSearch, setShiftSearch] = useState('')
  const [shiftStatusFilter, setShiftStatusFilter] = useState('ALL')
  const [referenceTimestamp, setReferenceTimestamp] = useState(() => Date.now())
  const [createdShiftNotice, setCreatedShiftNotice] = useState<CreatedShiftNotice | null>(null)
  const [error, setError] = useState('')
  const [offeringsError, setOfferingsError] = useState('')
  const [lecturersError, setLecturersError] = useState('')
  const lecturerRequestVersion = useRef(0)
  const createShiftButtonRef = useRef<HTMLButtonElement>(null)

  const selectedSemester = useMemo(
    () => semesters.find((semester) => semester.id === shiftForm.semesterId),
    [semesters, shiftForm.semesterId],
  )
  const selectedCourse = useMemo(
    () => offerings.find((offering) => offering.courseId === shiftForm.courseId),
    [offerings, shiftForm.courseId],
  )
  const selectedCourseSessions = useMemo(
    () => sessions.filter((session) => session.courseId === shiftForm.courseId),
    [sessions, shiftForm.courseId],
  )
  const visibleShifts = useMemo(() => {
    const normalizedSearch = shiftSearch.trim().toLowerCase()

    return shifts.filter((shift) => {
      const session = sessions.find((item) => item.id === shift.sessionId)
      const searchableText = [
        shift.shiftName,
        shift.roomLab,
        shift.examinerName,
        shift.proctorName,
        session?.title,
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()

      const matchesSearch = !normalizedSearch || searchableText.includes(normalizedSearch)
      const matchesStatus =
        shiftStatusFilter === 'ALL' || shift.status.toUpperCase() === shiftStatusFilter

      return matchesSearch && matchesStatus
    })
  }, [sessions, shiftSearch, shiftStatusFilter, shifts])

  const shiftSummary = useMemo(() => {
    const today = new Date(referenceTimestamp).toISOString().slice(0, 10)

    return {
      total: shifts.length,
      today: shifts.filter((shift) => shift.startTime.slice(0, 10) === today).length,
      upcoming: shifts.filter((shift) => {
        const timestamp = new Date(shift.startTime).getTime()
        return Number.isFinite(timestamp) && timestamp > referenceTimestamp
      }).length,
      published: shifts.filter((shift) => shift.status.toUpperCase() === 'PUBLISHED').length,
    }
  }, [referenceTimestamp, shifts])

  const shiftStatuses = useMemo(
    () => Array.from(new Set(shifts.map((shift) => shift.status.toUpperCase()))).sort(),
    [shifts],
  )

  const loadOfferings = useCallback(async (semesterId: string) => {
    if (!semesterId) {
      setOfferings([])
      return
    }
    setIsLoadingOfferings(true)
    setOfferingsError('')
    try {
      const result = await classService.listSemesterCourseOfferings(semesterId)
      setOfferings(result)
    } catch (loadError) {
      const message = userMessage(
        loadError,
        'Không tải được danh sách môn đã được cấu hình trong học kỳ này.',
      )
      setOfferings([])
      setOfferingsError(message)
    } finally {
      setIsLoadingOfferings(false)
    }
  }, [])

  const resolveStaffPools = useCallback(
    async (
      courseId: string,
      semesterId: string,
      academicProgramId?: string | null,
    ): Promise<{ proctors: ShiftStaffCandidate[]; examiners: ShiftStaffCandidate[] }> => {
      const proctorMap = new Map<string, ShiftStaffCandidate>()
      const examinerMap = new Map<string, ShiftStaffCandidate>()
      const departmentHeadIds = new Set<string>()
      let directCourseError = ''

      try {
        const assignedLecturers = await courseService.getCourseLecturers(courseId)
        const matching = assignedLecturers.filter(
          (lecturer) => !semesterId || !lecturer.semesterId || lecturer.semesterId === semesterId,
        )
        for (const lecturer of matching) {
          const item: ShiftStaffCandidate = {
            userId: lecturer.lecturerId,
            fullName: lecturer.fullName,
            detail: lecturer.email || undefined,
          }
          proctorMap.set(item.userId, item)
          examinerMap.set(item.userId, item)
        }
      } catch (loadError) {
        directCourseError = userMessage(
          loadError,
          'Không tải được danh sách giảng viên của môn này.',
        )
      }

      if (
        (proctorMap.size < 1 || examinerMap.size < 2 || directCourseError) &&
        typeof courseService.getShiftStaffCandidates === 'function'
      ) {
        try {
          const fallbackPool = await courseService.getShiftStaffCandidates({
            courseId,
            semesterId,
            academicProgramId,
          })
          for (const headId of fallbackPool.departmentHeadIds) {
            departmentHeadIds.add(headId)
          }
          for (const candidate of fallbackPool.proctors) {
            if (!proctorMap.has(candidate.userId)) {
              proctorMap.set(candidate.userId, candidate)
            }
          }
          for (const candidate of fallbackPool.examiners) {
            if (!examinerMap.has(candidate.userId)) {
              examinerMap.set(candidate.userId, candidate)
            }
          }
        } catch {
          // Keep whatever candidates were already resolved
        }
      }

      for (const shift of shifts) {
        if (
          shift.proctorUserId &&
          shift.proctorName &&
          !departmentHeadIds.has(shift.proctorUserId) &&
          !proctorMap.has(shift.proctorUserId)
        ) {
          proctorMap.set(shift.proctorUserId, {
            userId: shift.proctorUserId,
            fullName: shift.proctorName,
            detail: 'Giám thị ca thi',
          })
        }
        if (
          shift.examinerUserId &&
          shift.examinerName &&
          !examinerMap.has(shift.examinerUserId)
        ) {
          examinerMap.set(shift.examinerUserId, {
            userId: shift.examinerUserId,
            fullName: shift.examinerName,
            detail: 'Giám khảo chấm thi',
          })
        }
      }

      for (const headId of departmentHeadIds) {
        proctorMap.delete(headId)
      }

      return {
        proctors: Array.from(proctorMap.values()),
        examiners: Array.from(examinerMap.values()),
      }
    },
    [shifts],
  )

  const loadCourseLecturers = useCallback(
    async (courseId: string, semesterId: string, academicProgramId?: string | null) => {
      const requestVersion = lecturerRequestVersion.current + 1
      lecturerRequestVersion.current = requestVersion
      setProctorOptions([])
      setExaminerOptions([])
      setLecturersError('')

      if (!courseId || !semesterId) {
        setIsLoadingLecturers(false)
        return
      }

      setIsLoadingLecturers(true)
      try {
        const pools = await resolveStaffPools(courseId, semesterId, academicProgramId)
        if (requestVersion !== lecturerRequestVersion.current) return

        setProctorOptions(pools.proctors)
        setExaminerOptions(pools.examiners)
        const hasDistinctPair =
          pools.proctors.length > 0 &&
          pools.examiners.length > 0 &&
          (pools.proctors.length > 1 ||
            pools.examiners.length > 1 ||
            pools.proctors[0]?.userId !== pools.examiners[0]?.userId)
        if (!hasDistinctPair) {
          setLecturersError(
            'Chưa có đủ nhân sự Giám thị và Giám khảo độc lập khả dụng cho môn học này.',
          )
        }
      } finally {
        if (requestVersion === lecturerRequestVersion.current) setIsLoadingLecturers(false)
      }
    },
    [resolveStaffPools],
  )

  const loadPage = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const [semesterResult, sessionResult, shiftResult] = await Promise.all([
        classService.listSemesters(),
        officialExamService.getSessions(),
        officialExamService.getShifts(),
      ])
      setSemesters(semesterResult)
      setSessions(sessionResult)
      setShifts(shiftResult)
      setReferenceTimestamp(Date.now())

      const today = getTodayDate()
      const currentSemester = semesterResult.find(
        (semester) =>
          semester.isActive &&
          today >= semester.startDate.slice(0, 10) &&
          today <= semester.endDate.slice(0, 10),
      )
      if (currentSemester) {
        await loadOfferings(currentSemester.id)
        setShiftForm((current) => ({ ...current, semesterId: currentSemester.id }))
      }
    } catch (loadError) {
      const message = userMessage(loadError, 'Không tải được dữ liệu học kỳ và ca thi.')
      setError(message)
      toast.error(message)
    } finally {
      setIsLoading(false)
    }
  }, [loadOfferings])

  useEffect(() => {
    void loadPage()
  }, [loadPage])

  const handleSemesterChange = async (semesterId: string) => {
    setShiftForm({ ...initialShift, semesterId })
    setOfferings([])
    setOfferingsError('')
    setError('')
    await loadCourseLecturers('', semesterId)
    await loadOfferings(semesterId)
  }

  const openCreateShift = () => {
    setError('')
    setIsCreateShiftOpen(true)
  }

  const closeCreateShift = () => {
    if (busy) return
    setIsCreateShiftOpen(false)
    setError('')
    createShiftButtonRef.current?.focus()
  }

  const resolveSession = async (): Promise<OfficialExamSession> => {
    if (!selectedCourse || !selectedSemester || !shiftForm.startTime) {
      throw new Error('Vui lòng chọn học kỳ, môn thi và thời gian bắt đầu.')
    }

    const examDate = shiftForm.startTime.slice(0, 10)
    const candidates = selectedCourseSessions.filter(
      (session) =>
        session.status.toLowerCase() === 'scheduled' && getSessionDate(session) === examDate,
    )

    if (candidates.length === 1) return candidates[0]
    if (candidates.length > 1) {
      throw new Error(
        'Môn này đang có nhiều đợt tổ chức trùng ngày. Vui lòng liên hệ quản trị viên.',
      )
    }

    const [configuration, structures] = await Promise.all([
      courseService.getConfiguration(selectedCourse.courseId),
      officialExamService.getExamStructures({ courseId: selectedCourse.courseId, isActive: true }),
    ])
    const payload: CreateOfficialExamSessionRequest = {
      courseId: selectedCourse.courseId,
      examStructureId: structures[0]?.id ?? null,
      title: `${selectedCourse.courseCode} · ${selectedSemester.code} · Thi thật ${examDate}`,
      examDate,
      hasFollowUp: configuration.hasFollowUp,
      maxFollowUpQuestions: configuration.maxFollowUpQuestions,
      examInputMode: configuration.examInputMode,
      transcriptBufferSeconds: configuration.transcriptBufferSeconds,
    }
    const createdSession = await officialExamService.createSession(payload)
    setSessions((current) => [...current, createdSession])
    return createdSession
  }

  const submitShift = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!selectedSemester || !selectedCourse) {
      const selectionError = 'Vui lòng chọn học kỳ và môn thi trước khi tạo ca.'
      setError(selectionError)
      toast.error(selectionError)
      return
    }
    const capacityCheck = validateShiftCapacityInput(shiftForm.maxSeats, {
      defaultWhenEmpty: DEFAULT_SHIFT_CAPACITY,
    })
    if (capacityCheck.error || capacityCheck.value === null) {
      setError(capacityCheck.error)
      toast.error(capacityCheck.error)
      return
    }
    if (
      !shiftForm.startTime ||
      !shiftForm.endTime ||
      new Date(shiftForm.endTime) <= new Date(shiftForm.startTime)
    ) {
      const timeError = 'Giờ kết thúc phải sau giờ bắt đầu.'
      setError(timeError)
      toast.error(timeError)
      return
    }
    if (!isDateInsideSemester(shiftForm.startTime, selectedSemester)) {
      const dateError = 'Ngày bắt đầu ca phải nằm trong thời gian của học kỳ đã chọn.'
      setError(dateError)
      toast.error(dateError)
      return
    }

    setBusy(true)
    setError('')
    try {
      const session = await resolveSession()
      await officialExamService.createShift({
        sessionId: session.id,
        shiftName: shiftForm.shiftName,
        roomLab: shiftForm.roomLab,
        startTime: toUtcIso(shiftForm.startTime),
        endTime: toUtcIso(shiftForm.endTime),
        proctorUserId: shiftForm.proctorUserId,
        examinerUserId: shiftForm.examinerUserId,
        maxSeats: capacityCheck.value,
      })
      setCreatedShiftNotice({
        courseCode: selectedCourse.courseCode,
        shiftName: shiftForm.shiftName,
        roomLab: shiftForm.roomLab,
        startTime: shiftForm.startTime,
        endTime: shiftForm.endTime,
        maxSeats: capacityCheck.value,
      })
      setShiftForm((current) => ({
        ...initialShift,
        semesterId: current.semesterId,
        courseId: current.courseId,
        sessionId: session.id,
      }))
      setIsCreateShiftOpen(false)
      toast.success(`Đã tạo ca thi cho ${selectedCourse.courseCode}.`)
      const [sessionResult, shiftResult] = await Promise.all([
        officialExamService.getSessions(),
        officialExamService.getShifts(),
      ])
      setSessions(sessionResult)
      setShifts(shiftResult)
    } catch (submitError) {
      const message = userMessage(submitError, 'Không tạo được ca thi.')
      setError(message)
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  const openEditShift = async (shift: ExamShift) => {
    setEditingShift(shift)
    setEditError('')
    setEditShiftForm({
      shiftName: shift.shiftName || '',
      roomLab: shift.roomLab || '',
      startTime: toDateTimeLocalValue(shift.startTime),
      endTime: toDateTimeLocalValue(shift.endTime),
      proctorUserId: shift.proctorUserId || '',
      examinerUserId: shift.examinerUserId || '',
      maxSeats: String(shift.maxSeats ?? DEFAULT_SHIFT_CAPACITY),
    })
    const session = sessions.find((item) => item.id === shift.sessionId)
    if (session?.courseId) {
      const pools = await resolveStaffPools(session.courseId, shiftForm.semesterId)
      setEditProctorOptions(pools.proctors)
      setEditExaminerOptions(pools.examiners)
    } else {
      setEditProctorOptions([])
      setEditExaminerOptions([])
    }
  }

  const closeEditShift = () => {
    if (editBusy) return
    setEditingShift(null)
    setEditError('')
  }

  const submitEditShift = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!editingShift) return

    const capacityCheck = validateShiftCapacityInput(editShiftForm.maxSeats, {
      defaultWhenEmpty: editingShift.maxSeats ?? DEFAULT_SHIFT_CAPACITY,
    })
    if (capacityCheck.error || capacityCheck.value === null) {
      setEditError(capacityCheck.error)
      toast.error(capacityCheck.error)
      return
    }
    if (
      !editShiftForm.startTime ||
      !editShiftForm.endTime ||
      new Date(editShiftForm.endTime) <= new Date(editShiftForm.startTime)
    ) {
      const timeError = 'Giờ kết thúc phải sau giờ bắt đầu.'
      setEditError(timeError)
      toast.error(timeError)
      return
    }

    setEditBusy(true)
    setEditError('')
    try {
      await officialExamService.updateShift(editingShift.id, {
        shiftName: editShiftForm.shiftName.trim(),
        roomLab: editShiftForm.roomLab.trim(),
        startTime: toUtcIso(editShiftForm.startTime),
        endTime: toUtcIso(editShiftForm.endTime),
        proctorUserId: editShiftForm.proctorUserId,
        examinerUserId: editShiftForm.examinerUserId,
        maxSeats: capacityCheck.value,
      })
      toast.success('Đã cập nhật ca thi thành công.')
      setEditingShift(null)
      const [sessionResult, shiftResult] = await Promise.all([
        officialExamService.getSessions(),
        officialExamService.getShifts(),
      ])
      setSessions(sessionResult)
      setShifts(shiftResult)
    } catch (updateError) {
      const message = userMessage(updateError, 'Không cập nhật được ca thi.')
      setEditError(message)
      toast.error(message)
    } finally {
      setEditBusy(false)
    }
  }

  const createCapacityValidation = validateShiftCapacityInput(shiftForm.maxSeats, {
    defaultWhenEmpty: DEFAULT_SHIFT_CAPACITY,
  })
  const editCapacityValidation = validateShiftCapacityInput(editShiftForm.maxSeats, {
    defaultWhenEmpty: editingShift?.maxSeats ?? DEFAULT_SHIFT_CAPACITY,
  })

  return (
    <div className={ds.page}>
      <PageBanner
        icon={CalendarDays}
        eyebrow="CỔNG KHẢO THÍ · MF-04"
        title="Kỳ thi chính thức"
        description="Theo dõi ca thi thật trong phòng Lab và mở ca mới từ danh mục học kỳ, môn học đã được Admin cấu hình."
      />
      {error && !isCreateShiftOpen ? (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          <span>{error}</span>
          <button type="button" onClick={() => void loadPage()} className={ds.outlineButton}>
            <RefreshCw className="size-4" /> Thử lại
          </button>
        </div>
      ) : null}

      <section className={ds.section}>
        <SectionHeader
          icon={ClipboardList}
          title="Danh sách ca thi"
          badge={isLoading ? '…' : `${visibleShifts.length}/${shifts.length}`}
          hint="Theo dõi thời gian, phòng Lab, nhân sự và trạng thái từng ca thi."
          action={
            <button
              ref={createShiftButtonRef}
              type="button"
              onClick={openCreateShift}
              className={ds.primaryButton}
            >
              <Plus className="size-4" /> Tạo ca thi
            </button>
          }
        />
        <div className="grid gap-3 border-b border-[#d9e8f2] p-5 sm:grid-cols-2 lg:grid-cols-4">
          {[
            { label: 'Tổng ca thi', value: shiftSummary.total, icon: ClipboardList, tone: 'blue' },
            { label: 'Ca hôm nay', value: shiftSummary.today, icon: CalendarDays, tone: 'amber' },
            { label: 'Sắp diễn ra', value: shiftSummary.upcoming, icon: CalendarClock, tone: 'green' },
            { label: 'Đã xuất bản', value: shiftSummary.published, icon: CheckCircle2, tone: 'violet' },
          ].map(({ label, value, icon: Icon, tone }) => (
            <div
              key={label}
              className={cn(
                'rounded-2xl border px-4 py-3 transition-transform duration-200 hover:-translate-y-0.5 motion-reduce:transition-none motion-reduce:hover:transform-none',
                tone === 'blue' && 'border-[#88b2de]/55 bg-[#e9f5fc]',
                tone === 'amber' && 'border-[#edca8c]/55 bg-[#fff4e2]',
                tone === 'green' && 'border-[#9ddec4]/55 bg-[#effbf5]',
                tone === 'violet' && 'border-[#c4afe8]/55 bg-[#f2eafc]',
              )}
            >
              <div className="flex items-center gap-3">
                <span className="grid size-9 place-items-center rounded-xl bg-white/70 text-[#245997]">
                  <Icon className="size-5" aria-hidden="true" />
                </span>
                <div>
                  <p className="text-[11px] font-bold tracking-[0.08em] text-[#5b6b84] uppercase">{label}</p>
                  <p className="mt-0.5 text-2xl font-black text-[#0F2854]">{value}</p>
                </div>
              </div>
            </div>
          ))}
        </div>
        <div className="flex flex-col gap-3 border-b border-[#d9e8f2] p-5 sm:flex-row">
          <label className="relative min-w-0 flex-1" htmlFor="shift-search">
            <span className="sr-only">Tìm ca thi</span>
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#71829a]" />
            <input
              id="shift-search"
              className={cn(ds.field, 'pl-10')}
              value={shiftSearch}
              placeholder="Tìm theo tên ca, phòng, môn hoặc giảng viên"
              onChange={(event) => setShiftSearch(event.target.value)}
            />
          </label>
          <label className="sm:w-52" htmlFor="shift-status-filter">
            <span className="sr-only">Lọc trạng thái ca thi</span>
            <select
              id="shift-status-filter"
              className={ds.field}
              value={shiftStatusFilter}
              onChange={(event) => setShiftStatusFilter(event.target.value)}
            >
              <option value="ALL">Tất cả trạng thái</option>
              {shiftStatuses.map((status) => (
                <option key={status} value={status}>
                  {status}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            onClick={() => void loadPage()}
            disabled={isLoading}
            className={ds.outlineButton}
          >
            <RefreshCw className={cn('size-4', isLoading && 'animate-spin')} /> Làm mới
          </button>
        </div>
        {visibleShifts.length === 0 && !isLoading ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={CalendarDays}
              title={shifts.length === 0 ? 'Chưa có ca thi' : 'Không tìm thấy ca phù hợp'}
              description={
                shifts.length === 0
                  ? 'Nhấn “Tạo ca thi” để mở ca đầu tiên cho môn đã được cấu hình.'
                  : 'Thử thay đổi từ khóa hoặc bộ lọc trạng thái.'
              }
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[980px] text-left text-sm">
              <thead className="bg-[#F4FBFD] text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                <tr>
                  <th className="px-5 py-3">Ca thi</th>
                  <th className="px-5 py-3">Kỳ thi / môn</th>
                  <th className="px-5 py-3">Phòng</th>
                  <th className="px-5 py-3">Sức chứa / Vé</th>
                  <th className="px-5 py-3">Thời gian</th>
                  <th className="px-5 py-3">Nhân sự</th>
                  <th className="px-5 py-3">Trạng thái</th>
                  <th className="px-5 py-3 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#d9e8f2]">
                {visibleShifts.map((shift) => {
                  const session = sessions.find((item) => item.id === shift.sessionId)
                  const capacity = getShiftCapacityMetrics(shift)
                  return (
                    <tr key={shift.id} className="hover:bg-[#F8FBFD]">
                      <td className="px-5 py-3 font-bold text-[#0F2854]">
                        {shift.shiftName || shift.id}
                      </td>
                      <td className="max-w-[230px] px-5 py-3 text-[#5b6b84]">
                        <span className="block truncate" title={session?.title}>
                          {session?.title || 'Chưa cập nhật'}
                        </span>
                      </td>
                      <td className="px-5 py-3 text-[#5b6b84]">
                        <span className="inline-flex items-center gap-1.5">
                          <MapPin className="size-3.5" /> {shift.roomLab || 'Chưa cập nhật'}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-5 py-3 text-xs text-[#5b6b84]">
                        <span className="block font-semibold text-[#0F2854]">
                          Sức chứa tối đa: {capacity.maxSeats ?? '—'}
                        </span>
                        <span className="mt-0.5 block">
                          Đã phát: {capacity.assignedTicketsCount} · Còn lại:{' '}
                          {capacity.remainingSeats ?? '—'}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-5 py-3 text-[#5b6b84]">
                        <span className="inline-flex items-center gap-1.5">
                          <CalendarClock className="size-3.5" />
                          {shift.startTime} – {shift.endTime}
                        </span>
                      </td>
                      <td className="px-5 py-3 text-[#5b6b84]">
                        <span className="flex items-center gap-1.5">
                          <Users className="size-3.5" />
                          {shift.examinerName || shift.examinerUserId || 'Chưa gán'}
                        </span>
                        <span className="mt-1 block text-xs text-[#71829a]">
                          Giám thị: {shift.proctorName || shift.proctorUserId || 'Chưa gán'}
                        </span>
                      </td>
                      <td className="px-5 py-3">
                        <span
                          className={
                            shift.status.toUpperCase() === 'PUBLISHED'
                              ? ds.successChip
                              : ds.infoChip
                          }
                        >
                          {shift.status}
                        </span>
                      </td>
                      <td className="px-5 py-3 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => void openEditShift(shift)}
                            className={cn(ds.outlineButton, 'whitespace-nowrap px-3')}
                          >
                            Cập nhật ca
                          </button>
                          <Link
                            to={`/examination-officer/exams/${shift.sessionId}`}
                            className={cn(ds.outlineButton, 'whitespace-nowrap px-3')}
                          >
                            Xem chi tiết <ArrowRight className="size-3.5" />
                          </Link>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={ClipboardList}
          title="Dữ liệu đã cấu hình"
          hint="Các đợt tổ chức và ca thi được đọc từ backend"
          action={
            <button
              type="button"
              onClick={() => void loadPage()}
              disabled={isLoading}
              className={ds.outlineButton}
            >
              <RefreshCw className={cn('size-4', isLoading && 'animate-spin')} /> Làm mới
            </button>
          }
        />
        <div className="grid gap-4 p-5 lg:grid-cols-2">
          <div>
            <h3 className="mb-3 text-sm font-bold text-[#0F2854]">
              Đợt tổ chức ({sessions.length})
            </h3>
            {sessions.length === 0 && !isLoading ? (
              <EmptyState
                compact
                icon={CalendarDays}
                title="Chưa có đợt tổ chức"
                description="Đợt tổ chức kỹ thuật sẽ được tạo tự động khi mở ca đầu tiên cho một môn."
              />
            ) : (
              <div className="grid gap-2">
                {sessions.map((session) => (
                  <article
                    key={session.id}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-3"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <strong className="block text-sm text-[#0F2854]">
                          {session.title || session.id}
                        </strong>
                        <span className="mt-1 block text-xs text-[#5b6b84]">
                          {session.examDate} · {session.status}
                        </span>
                      </div>
                      <Link
                        to={`/examination-officer/exams/${session.id}`}
                        className={ds.outlineButton}
                      >
                        Chi tiết <ArrowRight className="size-3.5" />
                      </Link>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>
          <div>
            <h3 className="mb-3 text-sm font-bold text-[#0F2854]">
              Ca thi ({visibleShifts.length})
            </h3>
            {visibleShifts.length === 0 && !isLoading ? (
              <EmptyState
                compact
                icon={CalendarDays}
                title="Chưa có ca thi"
                description="Chọn học kỳ, môn thi và nhập thông tin ca ở phía trên."
              />
            ) : (
              <div className="grid gap-2">
                {visibleShifts.map((shift) => {
                  const capacity = getShiftCapacityMetrics(shift)
                  return (
                    <article
                      key={shift.id}
                      className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-3"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <strong className="block text-sm text-[#0F2854]">
                            {shift.shiftName || shift.id}
                          </strong>
                          <span className="mt-1 block text-xs text-[#5b6b84]">
                            {shift.roomLab} · {shift.startTime}
                          </span>
                          <span className="mt-1 block text-xs text-[#5b6b84]">
                            Sức chứa tối đa: {capacity.maxSeats ?? '—'} · Số vé đã phát:{' '}
                            {capacity.assignedTicketsCount} · Số chỗ còn lại:{' '}
                            {capacity.remainingSeats ?? '—'}
                          </span>
                        </div>
                        <span className={ds.infoChip}>{shift.status}</span>
                      </div>
                    </article>
                  )
                })}
              </div>
            )}
          </div>
        </div>
      </section>

      <FormDialog
        open={isCreateShiftOpen}
        align="center"
        size="xl"
        maxHeight="max-h-[min(94dvh,900px)]"
        eyebrow="MỞ CA THI MF-04"
        title="Tạo ca thi"
        icon={CalendarDays}
        busy={busy}
        onClose={closeCreateShift}
        footer={
          <DialogFooter className="justify-end bg-white">
            <button
              type="button"
              className={ds.outlineButton}
              onClick={closeCreateShift}
              disabled={busy}
            >
              Hủy
            </button>
            <button
              type="submit"
              form="create-official-shift-form"
              disabled={busy || !selectedSemester || !selectedCourse}
              className={ds.primaryButton}
            >
              {busy ? <LoaderCircle className="size-4 animate-spin" /> : <Plus className="size-4" />}
              {busy
                ? 'Đang tạo ca…'
                : selectedCourse
                  ? `Tạo ca thi cho ${selectedCourse.courseCode}`
                  : 'Chọn môn để tạo ca'}
            </button>
          </DialogFooter>
        }
      >
        <form id="create-official-shift-form" onSubmit={submitShift} className="grid gap-5">
          {error ? (
            <div
              role="alert"
              className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
            >
              {error}
            </div>
          ) : null}

          <section className="rounded-2xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 sm:p-5">
            <div className="mb-4 flex items-start gap-3">
              <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-[#dbeefa] text-[#245997]">
                <GraduationCap className="size-5" aria-hidden="true" />
              </span>
              <div>
                <h3 className="text-base font-black text-[#0F2854]">Học kỳ và môn thi</h3>
                <p className="mt-1 text-xs text-[#71829a]">
                  Chỉ chọn các môn đã được Admin đưa vào học kỳ.
                </p>
              </div>
            </div>
            <div className="grid items-start gap-4 lg:grid-cols-2">
              <label
                className="flex min-w-0 flex-col gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="exam-semester"
              >
                Học kỳ *
                <select
                  id="exam-semester"
                  required
                  className={ds.field}
                  value={shiftForm.semesterId}
                  disabled={isLoading}
                  onChange={(event) => void handleSemesterChange(event.target.value)}
                >
                  <option value="">Chọn học kỳ đang học</option>
                  {semesters.map((semester) => (
                    <option key={semester.id} value={semester.id}>
                      {semester.name || semester.code} · {semester.code}
                    </option>
                  ))}
                </select>
                {selectedSemester ? (
                  <span className="text-[11px] font-medium text-[#71829a]">
                    {selectedSemester.startDate.slice(0, 10)} – {selectedSemester.endDate.slice(0, 10)}
                  </span>
                ) : null}
              </label>

              <label
                className="flex min-w-0 flex-col gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="exam-course"
              >
                Môn thi trong học kỳ *
                <select
                  id="exam-course"
                  required
                  className={ds.field}
                  value={shiftForm.courseId}
                  disabled={!shiftForm.semesterId || isLoadingOfferings}
                  onChange={(event) => {
                    const courseId = event.target.value
                    const chosenOffering = offerings.find((item) => item.courseId === courseId)
                    setShiftForm((current) => ({
                      ...current,
                      courseId,
                      sessionId: '',
                      proctorUserId: '',
                      examinerUserId: '',
                    }))
                    void loadCourseLecturers(
                      courseId,
                      shiftForm.semesterId,
                      chosenOffering?.academicProgramId,
                    )
                  }}
                >
                  <option value="">
                    {isLoadingOfferings
                      ? 'Đang tải danh sách môn…'
                      : 'Chọn môn đã được đưa vào học kỳ'}
                  </option>
                  {offerings.map((offering) => (
                    <option key={offering.courseId} value={offering.courseId}>
                      {offering.courseCode} — {offering.courseName}
                    </option>
                  ))}
                </select>
                {offeringsError ? (
                  <span className="text-[11px] font-semibold text-[#a43d4b]">{offeringsError}</span>
                ) : null}
                {!offeringsError &&
                shiftForm.semesterId &&
                !isLoadingOfferings &&
                offerings.length === 0 ? (
                  <span className="text-[11px] font-medium text-[#71829a]">
                    Học kỳ này chưa có môn được Admin đưa vào.
                  </span>
                ) : null}
              </label>
            </div>
            {selectedCourse ? (
              <div className="mt-4 flex flex-wrap items-center gap-3 rounded-xl border border-[#bcd6ef] bg-white px-4 py-3 text-sm text-[#245997]">
                <CheckCircle2 className="size-5 text-[#168b70]" />
                <span>
                  Đã chọn <strong>{selectedCourse.courseCode}</strong> · {selectedCourse.courseName}
                </span>
                <span className="rounded-full bg-[#f3f8fd] px-3 py-1 text-xs font-semibold text-[#667892]">
                  {selectedCourseSessions.length} đợt kỹ thuật
                </span>
              </div>
            ) : null}
          </section>

          <section className="rounded-2xl border border-[#d9e8f2] bg-white p-4 sm:p-5">
            <div className="mb-4 flex items-start gap-3">
              <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-[#dbeefa] text-[#245997]">
                <BookOpen className="size-5" aria-hidden="true" />
              </span>
              <div>
                <h3 className="text-base font-black text-[#0F2854]">Thông tin ca thi</h3>
                <p className="mt-1 text-xs text-[#71829a]">
                  {selectedCourse ? `Tạo ca cho ${selectedCourse.courseCode}` : 'Chọn môn thi trước'}
                </p>
              </div>
            </div>
            <div className="grid gap-4 lg:grid-cols-2">
              <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-name">
                Tên ca *
                <input
                  id="shift-name"
                  required
                  className={ds.field}
                  value={shiftForm.shiftName}
                  onChange={(event) =>
                    setShiftForm((current) => ({ ...current, shiftName: event.target.value }))
                  }
                />
              </label>
              <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-room">
                Phòng Lab *
                <input
                  id="shift-room"
                  required
                  className={ds.field}
                  value={shiftForm.roomLab}
                  onChange={(event) =>
                    setShiftForm((current) => ({ ...current, roomLab: event.target.value }))
                  }
                />
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d] lg:col-span-2"
                htmlFor="shift-max-seats"
              >
                Sức chứa tối đa (maxSeats) *
                <input
                  id="shift-max-seats"
                  type="number"
                  min={MIN_SHIFT_CAPACITY}
                  max={MAX_SHIFT_CAPACITY}
                  step={1}
                  className={ds.field}
                  value={shiftForm.maxSeats}
                  onChange={(event) =>
                    setShiftForm((current) => ({ ...current, maxSeats: event.target.value }))
                  }
                />
                <span className="text-[11px] font-medium text-[#71829a]">
                  Mặc định {DEFAULT_SHIFT_CAPACITY} ghế · Giá trị hợp lệ: số nguyên từ{' '}
                  {MIN_SHIFT_CAPACITY} đến {MAX_SHIFT_CAPACITY} (trần cứng {MAX_SHIFT_CAPACITY}).
                </span>
                {shiftForm.maxSeats.trim() !== '' && createCapacityValidation.error ? (
                  <span role="alert" className="text-[11px] font-semibold text-[#a43d4b]">
                    {createCapacityValidation.error}
                  </span>
                ) : null}
              </label>
              <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-start">
                Bắt đầu *
                <input
                  id="shift-start"
                  required
                  type="datetime-local"
                  className={ds.field}
                  value={shiftForm.startTime}
                  onChange={(event) =>
                    setShiftForm((current) => ({ ...current, startTime: event.target.value }))
                  }
                />
              </label>
              <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-end">
                Kết thúc *
                <input
                  id="shift-end"
                  required
                  type="datetime-local"
                  className={ds.field}
                  value={shiftForm.endTime}
                  onChange={(event) =>
                    setShiftForm((current) => ({ ...current, endTime: event.target.value }))
                  }
                />
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="shift-proctor"
              >
                Giám thị *
                <select
                  id="shift-proctor"
                  required
                  className={ds.field}
                  value={shiftForm.proctorUserId}
                  onChange={(event) =>
                    setShiftForm((current) => ({ ...current, proctorUserId: event.target.value }))
                  }
                  disabled={!selectedCourse || isLoadingLecturers || proctorOptions.length === 0}
                >
                  <option value="">
                    {isLoadingLecturers
                      ? 'Đang tải danh sách giám thị…'
                      : selectedCourse
                        ? 'Chọn giám thị'
                        : 'Chọn môn thi trước'}
                  </option>
                  {proctorOptions.map((candidate) => (
                    <option
                      key={`proctor-${candidate.userId}`}
                      value={candidate.userId}
                      disabled={candidate.userId === shiftForm.examinerUserId}
                    >
                      {candidate.fullName}
                      {candidate.detail ? ` · ${candidate.detail}` : ''}
                    </option>
                  ))}
                </select>
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="shift-examiner"
              >
                Giám khảo *
                <select
                  id="shift-examiner"
                  required
                  className={ds.field}
                  value={shiftForm.examinerUserId}
                  onChange={(event) =>
                    setShiftForm((current) => ({ ...current, examinerUserId: event.target.value }))
                  }
                  disabled={!selectedCourse || isLoadingLecturers || examinerOptions.length === 0}
                >
                  <option value="">
                    {isLoadingLecturers
                      ? 'Đang tải danh sách giám khảo…'
                      : selectedCourse
                        ? 'Chọn giám khảo'
                        : 'Chọn môn thi trước'}
                  </option>
                  {examinerOptions.map((candidate) => (
                    <option
                      key={`examiner-${candidate.userId}`}
                      value={candidate.userId}
                      disabled={candidate.userId === shiftForm.proctorUserId}
                    >
                      {candidate.fullName}
                      {candidate.detail ? ` · ${candidate.detail}` : ''}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            {selectedCourse ? (
              <p className="mt-4 text-xs font-medium text-[#71829a]" role={lecturersError ? 'alert' : undefined}>
                {isLoadingLecturers
                  ? 'Đang tải danh sách nhân sự coi thi và chấm thi cho môn đã chọn…'
                  : lecturersError ||
                    `Sẵn sàng phân công: ${proctorOptions.length} giám thị · ${examinerOptions.length} giám khảo đủ điều kiện.`}
              </p>
            ) : null}
          </section>
        </form>
      </FormDialog>

      {createdShiftNotice ? (
        <FormDialog
          open
          align="center"
          size="md"
          titleId="created-shift-success-title"
          eyebrow="Cập nhật lịch thi"
          title="Tạo ca thi thành công"
          icon={CheckCircle2}
          onClose={() => setCreatedShiftNotice(null)}
          footer={
            <DialogFooter className="justify-end bg-white">
              <button
                type="button"
                className={ds.primaryButton}
                onClick={() => setCreatedShiftNotice(null)}
              >
                Đã hiểu
              </button>
            </DialogFooter>
          }
        >
          <div className="space-y-4">
            <div className="rounded-xl border border-[#b9e7d5] bg-[#effbf5] px-4 py-3 text-sm font-semibold text-[#14795f]">
              Ca thi đã được lưu và sẵn sàng để tiếp tục phân bổ vé thi.
            </div>
            <dl className="grid gap-3 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-xs font-bold text-[#71829a]">Môn thi</dt>
                <dd className="mt-1 font-bold text-[#0F2854]">{createdShiftNotice.courseCode}</dd>
              </div>
              <div>
                <dt className="text-xs font-bold text-[#71829a]">Tên ca</dt>
                <dd className="mt-1 font-bold text-[#0F2854]">{createdShiftNotice.shiftName}</dd>
              </div>
              <div>
                <dt className="text-xs font-bold text-[#71829a]">Phòng Lab</dt>
                <dd className="mt-1 font-bold text-[#0F2854]">{createdShiftNotice.roomLab}</dd>
              </div>
              <div>
                <dt className="text-xs font-bold text-[#71829a]">Thời gian</dt>
                <dd className="mt-1 font-bold text-[#0F2854]">
                  {createdShiftNotice.startTime} – {createdShiftNotice.endTime}
                </dd>
              </div>
              <div>
                <dt className="text-xs font-bold text-[#71829a]">Sức chứa tối đa</dt>
                <dd className="mt-1 font-bold text-[#0F2854]">{createdShiftNotice.maxSeats} ghế</dd>
              </div>
            </dl>
          </div>
        </FormDialog>
      ) : null}

      {editingShift ? (
        <FormDialog
          open
          align="center"
          size="lg"
          eyebrow="CẤU HÌNH CA THI MF-04"
          title="Cập nhật ca thi"
          icon={CalendarDays}
          busy={editBusy}
          onClose={closeEditShift}
          footer={
            <DialogFooter className="justify-end bg-white">
              <button
                type="button"
                className={ds.outlineButton}
                onClick={closeEditShift}
                disabled={editBusy}
              >
                Hủy
              </button>
              <button
                type="submit"
                form="update-official-shift-form"
                disabled={editBusy}
                className={ds.primaryButton}
              >
                {editBusy ? <LoaderCircle className="size-4 animate-spin" /> : null}
                {editBusy ? 'Đang lưu…' : 'Lưu cập nhật ca thi'}
              </button>
            </DialogFooter>
          }
        >
          <form id="update-official-shift-form" onSubmit={submitEditShift} className="grid gap-4">
            {editError ? (
              <div
                role="alert"
                className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
              >
                {editError}
              </div>
            ) : null}

            <div className="grid gap-4 sm:grid-cols-2">
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="edit-shift-name"
              >
                Tên ca *
                <input
                  id="edit-shift-name"
                  required
                  className={ds.field}
                  value={editShiftForm.shiftName}
                  onChange={(event) =>
                    setEditShiftForm((current) => ({ ...current, shiftName: event.target.value }))
                  }
                />
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="edit-shift-room"
              >
                Phòng Lab *
                <input
                  id="edit-shift-room"
                  required
                  className={ds.field}
                  value={editShiftForm.roomLab}
                  onChange={(event) =>
                    setEditShiftForm((current) => ({ ...current, roomLab: event.target.value }))
                  }
                />
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d] sm:col-span-2"
                htmlFor="edit-shift-max-seats"
              >
                Sức chứa tối đa (maxSeats) *
                <input
                  id="edit-shift-max-seats"
                  type="number"
                  min={MIN_SHIFT_CAPACITY}
                  max={MAX_SHIFT_CAPACITY}
                  step={1}
                  className={ds.field}
                  value={editShiftForm.maxSeats}
                  onChange={(event) =>
                    setEditShiftForm((current) => ({ ...current, maxSeats: event.target.value }))
                  }
                />
                <span className="text-[11px] font-medium text-[#71829a]">
                  Sức chứa hiện tại: {editingShift.maxSeats ?? DEFAULT_SHIFT_CAPACITY} · Đã phát:{' '}
                  {editingShift.assignedTicketsCount ?? 0} vé · Trần hệ thống: {MAX_SHIFT_CAPACITY}
                </span>
                {editShiftForm.maxSeats.trim() !== '' && editCapacityValidation.error ? (
                  <span role="alert" className="text-[11px] font-semibold text-[#a43d4b]">
                    {editCapacityValidation.error}
                  </span>
                ) : null}
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="edit-shift-start"
              >
                Bắt đầu *
                <input
                  id="edit-shift-start"
                  required
                  type="datetime-local"
                  className={ds.field}
                  value={editShiftForm.startTime}
                  onChange={(event) =>
                    setEditShiftForm((current) => ({ ...current, startTime: event.target.value }))
                  }
                />
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="edit-shift-end"
              >
                Kết thúc *
                <input
                  id="edit-shift-end"
                  required
                  type="datetime-local"
                  className={ds.field}
                  value={editShiftForm.endTime}
                  onChange={(event) =>
                    setEditShiftForm((current) => ({ ...current, endTime: event.target.value }))
                  }
                />
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="edit-shift-proctor"
              >
                Giám thị *
                <select
                  id="edit-shift-proctor"
                  required
                  className={ds.field}
                  value={editShiftForm.proctorUserId}
                  onChange={(event) =>
                    setEditShiftForm((current) => ({
                      ...current,
                      proctorUserId: event.target.value,
                    }))
                  }
                >
                  <option value="">Chọn giám thị</option>
                  {editingShift.proctorUserId &&
                  !editProctorOptions.some((item) => item.userId === editingShift.proctorUserId) ? (
                    <option value={editingShift.proctorUserId}>
                      {editingShift.proctorName || editingShift.proctorUserId}
                    </option>
                  ) : null}
                  {editProctorOptions.map((candidate) => (
                    <option
                      key={`edit-proctor-${candidate.userId}`}
                      value={candidate.userId}
                      disabled={candidate.userId === editShiftForm.examinerUserId}
                    >
                      {candidate.fullName}
                      {candidate.detail ? ` · ${candidate.detail}` : ''}
                    </option>
                  ))}
                </select>
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="edit-shift-examiner"
              >
                Giám khảo *
                <select
                  id="edit-shift-examiner"
                  required
                  className={ds.field}
                  value={editShiftForm.examinerUserId}
                  onChange={(event) =>
                    setEditShiftForm((current) => ({
                      ...current,
                      examinerUserId: event.target.value,
                    }))
                  }
                >
                  <option value="">Chọn giám khảo</option>
                  {editingShift.examinerUserId &&
                  !editExaminerOptions.some((item) => item.userId === editingShift.examinerUserId) ? (
                    <option value={editingShift.examinerUserId}>
                      {editingShift.examinerName || editingShift.examinerUserId}
                    </option>
                  ) : null}
                  {editExaminerOptions.map((candidate) => (
                    <option
                      key={`edit-examiner-${candidate.userId}`}
                      value={candidate.userId}
                      disabled={candidate.userId === editShiftForm.proctorUserId}
                    >
                      {candidate.fullName}
                      {candidate.detail ? ` · ${candidate.detail}` : ''}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          </form>
        </FormDialog>
      ) : null}
    </div>
  )
}
