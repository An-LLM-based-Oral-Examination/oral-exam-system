import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import {
  ArrowRight,
  CalendarClock,
  CalendarDays,
  CalendarPlus,
  CheckCircle2,
  ClipboardList,
  MapPin,
  Plus,
  RefreshCw,
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
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { SearchField } from '@/components/common/SearchField'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'
import {
  DEFAULT_SHIFT_CAPACITY,
  getShiftCapacityMetrics,
  validateShiftCapacityInput,
} from '@/lib/shiftCapacity'
import { ExamCampaignSetupStep } from '@/components/examination-officer/ExamCampaignSetupStep'
import {
  CreateSingleShiftDialog,
  CreatedShiftSuccessDialog,
  EditSingleShiftDialog,
} from '@/components/examination-officer/SingleShiftDialog'
import {
  INITIAL_CREATE_SHIFT_FORM,
  resolveShiftStaffPools,
  toDateTimeLocalValue,
  toUtcIso,
  type CreateShiftFormState,
  type CreatedShiftNotice,
  type EditShiftFormState,
} from '@/components/examination-officer/shiftStaffHelpers'

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

export function ExaminationOfficerExamManagementPage() {
  const [semesters, setSemesters] = useState<SemesterOption[]>([])
  const [offerings, setOfferings] = useState<SemesterCourseOffering[]>([])
  const [sessions, setSessions] = useState<OfficialExamSession[]>([])
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [proctorOptions, setProctorOptions] = useState<ShiftStaffCandidate[]>([])
  const [examinerOptions, setExaminerOptions] = useState<ShiftStaffCandidate[]>([])
  const [shiftForm, setShiftForm] = useState<CreateShiftFormState>(INITIAL_CREATE_SHIFT_FORM)
  const [campaignSemesterId, setCampaignSemesterId] = useState('')
  const [showCampaignPanel, setShowCampaignPanel] = useState(false)
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
      published: shifts.filter(
        (shift) => shift.isExamPublished || shift.status.toUpperCase() === 'PUBLISHED',
      ).length,
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
      setOfferings([])
      setOfferingsError(
        userMessage(loadError, 'Không tải được danh sách môn đã được cấu hình trong học kỳ này.'),
      )
    } finally {
      setIsLoadingOfferings(false)
    }
  }, [])

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
        const pools = await resolveShiftStaffPools(courseId, semesterId, shifts, academicProgramId)
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
    [shifts],
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
      const currentSemester =
        semesterResult.find(
          (semester) =>
            semester.isActive &&
            today >= semester.startDate.slice(0, 10) &&
            today <= semester.endDate.slice(0, 10),
        ) ?? semesterResult[0]
      if (currentSemester) {
        setCampaignSemesterId((current) => current || currentSemester.id)
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
    const timer = window.setTimeout(() => {
      void loadPage()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [loadPage])

  const handleSemesterChange = async (semesterId: string) => {
    setShiftForm({ ...INITIAL_CREATE_SHIFT_FORM, semesterId })
    setOfferings([])
    setOfferingsError('')
    setError('')
    await loadCourseLecturers('', semesterId)
    await loadOfferings(semesterId)
  }

  const handleCourseChange = (courseId: string) => {
    const chosenOffering = offerings.find((item) => item.courseId === courseId)
    setShiftForm((current) => ({
      ...current,
      courseId,
      sessionId: '',
      proctorUserId: '',
      examinerUserId: '',
    }))
    void loadCourseLecturers(courseId, shiftForm.semesterId, chosenOffering?.academicProgramId)
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
      semesterId: selectedSemester.id,
      examAttemptType: 'FE',
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
        ...INITIAL_CREATE_SHIFT_FORM,
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
      const pools = await resolveShiftStaffPools(session.courseId, shiftForm.semesterId, shifts)
      setEditProctorOptions(pools.proctors)
      setEditExaminerOptions(pools.examiners)
    } else {
      setEditProctorOptions([])
      setEditExaminerOptions([])
    }
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

  return (
    <div className={ds.page}>
      <PageBanner
        icon={CalendarDays}
        eyebrow="CỔNG KHẢO THÍ · MF-04"
        title="Kỳ thi chính thức"
        description="Vận hành quy trình 6 bước: khởi tạo kỳ thi FE/RE theo học kỳ, import DSSV, ước lượng chuẩn 24 SV/phòng, tạo ca hàng loạt và chia phòng ngẫu nhiên cân bằng ca cuối."
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

      {showCampaignPanel ? (
        <ExamCampaignSetupStep
          semesters={semesters}
          selectedSemesterId={campaignSemesterId}
          onSemesterChange={setCampaignSemesterId}
          onSessionsCreated={loadPage}
        />
      ) : null}

      <section className={ds.section}>
        <SectionHeader
          icon={ClipboardList}
          title="Danh sách ca thi"
          badge={isLoading ? '…' : `${visibleShifts.length}/${shifts.length}`}
          hint="Theo dõi thời gian, phòng Lab, nhân sự và trạng thái từng ca thi."
          action={
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => setShowCampaignPanel((current) => !current)}
                className={ds.outlineButton}
              >
                <CalendarPlus className="size-4" />
                {showCampaignPanel
                  ? 'Ẩn chiến dịch FE / RE'
                  : 'Khởi tạo chiến dịch FE / RE (Bước 1)'}
              </button>
              <button
                ref={createShiftButtonRef}
                type="button"
                onClick={openCreateShift}
                className={ds.primaryButton}
              >
                <Plus className="size-4" /> Tạo ca thi
              </button>
            </div>
          }
        />

        <div className="grid gap-3 border-b border-[#d9e8f2] p-5 sm:grid-cols-2 lg:grid-cols-4">
          {[
            { label: 'Tổng ca thi', value: shiftSummary.total, icon: ClipboardList, tone: 'blue' },
            { label: 'Ca hôm nay', value: shiftSummary.today, icon: CalendarDays, tone: 'amber' },
            {
              label: 'Sắp diễn ra',
              value: shiftSummary.upcoming,
              icon: CalendarClock,
              tone: 'green',
            },
            {
              label: 'Đã xuất bản',
              value: shiftSummary.published,
              icon: CheckCircle2,
              tone: 'violet',
            },
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
                  <p className="text-[11px] font-bold tracking-[0.08em] text-[#5b6b84] uppercase">
                    {label}
                  </p>
                  <p className="mt-0.5 text-2xl font-black text-[#0F2854]">{value}</p>
                </div>
              </div>
            </div>
          ))}
        </div>

        <div className="flex flex-col gap-3 border-b border-[#d9e8f2] p-5 sm:flex-row">
          <label className={cn('relative', ds.searchControl)} htmlFor="shift-search">
            <span className="sr-only">Tìm ca thi</span>
            <SearchField
              id="shift-search"
              className={cn(ds.field, 'pl-10')}
              value={shiftSearch}
              placeholder="Tìm theo tên ca, phòng, môn hoặc giảng viên"
              onChange={(event) => setShiftSearch(event.target.value)}
            />
          </label>
          <label className={ds.filterStatus} htmlFor="shift-status-filter">
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
                  ? 'Nhấn “Khởi tạo chiến dịch FE / RE (Bước 1)” hoặc “Tạo ca thi” để mở ca đầu tiên.'
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
                            shift.isExamPublished || shift.status.toUpperCase() === 'PUBLISHED'
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
          title="Đợt tổ chức kỳ thi (FE / RE)"
          badge={sessions.length}
          hint="Chọn một kỳ thi để thực hiện Bước 2–6: Import DSSV, ước tính 24 SV/phòng, tạo ca hàng loạt và chia phòng CSPRNG."
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
        <div className="p-5">
          {sessions.length === 0 && !isLoading ? (
            <EmptyState
              compact
              icon={CalendarDays}
              title="Chưa có đợt tổ chức"
              description="Mở chiến dịch FE / RE theo học kỳ hoặc tạo ca thi đầu tiên cho một môn."
            />
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {sessions.map((session) => {
                const attemptType = session.examAttemptType === 'RE' ? 'RE' : 'FE'
                return (
                  <article
                    key={session.id}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-4 py-3"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span
                            className={
                              attemptType === 'RE'
                                ? 'rounded-full border border-[#d9c8f2] bg-[#f4effc] px-2 py-0.5 text-[11px] font-bold text-[#5a3b8c]'
                                : 'rounded-full border border-[#b7e4cf] bg-[#effaf6] px-2 py-0.5 text-[11px] font-bold text-[#176f58]'
                            }
                          >
                            {attemptType}
                          </span>
                          <strong className="truncate text-sm text-[#0F2854]">
                            {session.title || session.id}
                          </strong>
                        </div>
                        <span className="mt-1 block text-xs text-[#5b6b84]">
                          {session.examDate} · {session.status}
                          {typeof session.candidateCount === 'number'
                            ? ` · ${session.candidateCount} thí sinh`
                            : ''}
                        </span>
                      </div>
                      <Link
                        to={`/examination-officer/exams/${session.id}`}
                        className={cn(ds.outlineButton, 'shrink-0')}
                      >
                        Chi tiết <ArrowRight className="size-3.5" />
                      </Link>
                    </div>
                  </article>
                )
              })}
            </div>
          )}
        </div>
      </section>

      <CreateSingleShiftDialog
        open={isCreateShiftOpen}
        busy={busy}
        isLoading={isLoading}
        isLoadingOfferings={isLoadingOfferings}
        isLoadingLecturers={isLoadingLecturers}
        semesters={semesters}
        offerings={offerings}
        selectedSemester={selectedSemester}
        selectedCourse={selectedCourse}
        selectedCourseSessions={selectedCourseSessions}
        proctorOptions={proctorOptions}
        examinerOptions={examinerOptions}
        shiftForm={shiftForm}
        error={error}
        offeringsError={offeringsError}
        lecturersError={lecturersError}
        onClose={closeCreateShift}
        onSubmit={submitShift}
        onSemesterChange={(semesterId) => void handleSemesterChange(semesterId)}
        onCourseChange={handleCourseChange}
        onChangeField={(key, value) =>
          setShiftForm((current) => ({ ...current, [key]: value }))
        }
      />

      <CreatedShiftSuccessDialog
        notice={createdShiftNotice}
        onClose={() => setCreatedShiftNotice(null)}
      />

      <EditSingleShiftDialog
        shift={editingShift}
        form={editShiftForm}
        proctorOptions={editProctorOptions}
        examinerOptions={editExaminerOptions}
        busy={editBusy}
        error={editError}
        onClose={() => {
          if (editBusy) return
          setEditingShift(null)
          setEditError('')
        }}
        onSubmit={submitEditShift}
        onChangeField={(key, value) =>
          setEditShiftForm((current) => ({ ...current, [key]: value }))
        }
      />
    </div>
  )
}
