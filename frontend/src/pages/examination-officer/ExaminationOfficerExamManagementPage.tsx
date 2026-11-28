import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import {
  ArrowRight,
  CalendarClock,
  CalendarDays,
  CalendarPlus,
  CheckCircle2,
  ClipboardList,
  Plus,
  RefreshCw,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import { classService } from '@/services/class.service'
import { courseService, type ShiftStaffCandidate } from '@/services/course.service'
import { officialExamService } from '@/services/officialExam.service'
import type { SemesterCourseOffering, SemesterOption } from '@/types/class.types'
import type {
  CandidateImportPreviewResponse,
  CreateOfficialExamSessionRequest,
  ExamShift,
  OfficialExamSession,
  SemesterEligibleExamCourse,
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
  validateShiftCapacityInput,
} from '@/lib/shiftCapacity'
import {
  CreateSingleShiftDialog,
  CreatedShiftSuccessDialog,
} from '@/components/examination-officer/SingleShiftDialog'
import { ExamCampaignSetupStep } from '@/components/examination-officer/ExamCampaignSetupStep'
import { getShiftStatusMeta } from '@/components/examination-officer/eoFormatters'
import {
  createInitialShiftForm,
  getDefaultShiftWindow,
  getLocalTodayDate,
  INITIAL_CREATE_SHIFT_FORM,
  resolveShiftStaffPools,
  toUtcIso,
  type CreateShiftFormState,
  type CreatedShiftNotice,
} from '@/components/examination-officer/shiftStaffHelpers'

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
  const [eligibleCourses, setEligibleCourses] = useState<SemesterEligibleExamCourse[]>([])
  const [sessions, setSessions] = useState<OfficialExamSession[]>([])
  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [proctorOptions, setProctorOptions] = useState<ShiftStaffCandidate[]>([])
  const [examinerOptions, setExaminerOptions] = useState<ShiftStaffCandidate[]>([])
  const [shiftForm, setShiftForm] = useState<CreateShiftFormState>(INITIAL_CREATE_SHIFT_FORM)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingOfferings, setIsLoadingOfferings] = useState(false)
  const [isLoadingLecturers, setIsLoadingLecturers] = useState(false)
  const [busy, setBusy] = useState(false)
  const [isCreateShiftOpen, setIsCreateShiftOpen] = useState(false)
  const [showCampaignSetup, setShowCampaignSetup] = useState(false)
  const [sessionSearch, setSessionSearch] = useState('')
  const [attemptFilter, setAttemptFilter] = useState<'ALL' | 'FE' | 'RE'>('ALL')
  const [referenceTimestamp, setReferenceTimestamp] = useState(() => Date.now())
  const [createdShiftNotice, setCreatedShiftNotice] = useState<CreatedShiftNotice | null>(null)
  const [error, setError] = useState('')
  const [offeringsError, setOfferingsError] = useState('')
  const [lecturersError, setLecturersError] = useState('')
  const [candidateFile, setCandidateFile] = useState<File | null>(null)
  const [candidatePreview, setCandidatePreview] = useState<CandidateImportPreviewResponse | null>(
    null,
  )
  const [candidateImportBusy, setCandidateImportBusy] = useState(false)
  const [candidateImportError, setCandidateImportError] = useState('')
  const [candidateSessionId, setCandidateSessionId] = useState<string | null>(null)
  const lecturerRequestVersion = useRef(0)
  const offeringsEpochRef = useRef(0)
  const pageEpochRef = useRef(0)
  const createShiftButtonRef = useRef<HTMLButtonElement>(null)
  const createdShiftIdByRoomIdRef = useRef<Map<string, string>>(new Map())
  const committedCandidateKeyRef = useRef<string | null>(null)

  const selectedSemester = useMemo(
    () => semesters.find((semester) => semester.id === shiftForm.semesterId),
    [semesters, shiftForm.semesterId],
  )
  const selectedCourse = useMemo(
    () => offerings.find((offering) => offering.courseId === shiftForm.courseId),
    [offerings, shiftForm.courseId],
  )
  const selectedEligibleCourse = useMemo(
    () => eligibleCourses.find((course) => course.courseId === shiftForm.courseId),
    [eligibleCourses, shiftForm.courseId],
  )
  const selectedCourseSessions = useMemo(
    () =>
      sessions.filter(
        (session) =>
          session.courseId === shiftForm.courseId &&
          (!shiftForm.semesterId || !session.semesterId || session.semesterId === shiftForm.semesterId),
      ),
    [sessions, shiftForm.courseId, shiftForm.semesterId],
  )

  const shiftsBySessionId = useMemo(() => {
    const map = new Map<string, ExamShift[]>()
    for (const shift of shifts) {
      const list = map.get(shift.sessionId)
      if (list) list.push(shift)
      else map.set(shift.sessionId, [shift])
    }
    return map
  }, [shifts])

  const visibleSessions = useMemo(() => {
    const normalizedSearch = sessionSearch.trim().toLowerCase()
    return sessions.filter((session) => {
      const attemptType = session.examAttemptType === 'RE' ? 'RE' : 'FE'
      if (attemptFilter !== 'ALL' && attemptType !== attemptFilter) return false
      if (!normalizedSearch) return true
      const sessionShifts = shiftsBySessionId.get(session.id) ?? []
      const searchable = [
        session.title,
        session.examDate,
        session.status,
        attemptType,
        ...sessionShifts.flatMap((s) => [s.shiftName, s.roomLab]),
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()
      return searchable.includes(normalizedSearch)
    })
  }, [attemptFilter, sessionSearch, sessions, shiftsBySessionId])

  const examSummary = useMemo(() => {
    return {
      totalSessions: sessions.length,
      totalRooms: shifts.length,
      upcoming: shifts.filter((shift) => {
        const timestamp = new Date(shift.startTime).getTime()
        return Number.isFinite(timestamp) && timestamp > referenceTimestamp
      }).length,
      published: shifts.filter(
        (shift) => shift.isExamPublished || shift.status.toUpperCase() === 'PUBLISHED',
      ).length,
    }
  }, [referenceTimestamp, sessions.length, shifts])

  const loadOfferings = useCallback(async (semesterId: string) => {
    const epoch = ++offeringsEpochRef.current
    if (!semesterId) {
      setOfferings([])
      setEligibleCourses([])
      setIsLoadingOfferings(false)
      return
    }
    setIsLoadingOfferings(true)
    setOfferingsError('')
    try {
      let result: SemesterCourseOffering[] = []
      if (typeof officialExamService.getEligibleCourses === 'function') {
        try {
          const eligible = await officialExamService.getEligibleCourses(semesterId)
          if (epoch !== offeringsEpochRef.current) return
          setEligibleCourses(eligible)
          result = eligible.map((course) => ({
            courseOfferingId: `${course.semesterId}-${course.courseId}`,
            courseId: course.courseId,
            courseCode: course.courseCode,
            courseName: course.courseName,
            description: null,
            credits: 3,
            academicProgramId: null,
            academicProgramCode: null,
            isCourseActive: true,
            isSelected: true,
          }))
        } catch {
          if (epoch !== offeringsEpochRef.current) return
          setEligibleCourses([])
          result = await classService.listSemesterCourseOfferings(semesterId)
        }
      } else {
        setEligibleCourses([])
        result = await classService.listSemesterCourseOfferings(semesterId)
      }
      if (epoch !== offeringsEpochRef.current) return
      setOfferings(result)
    } catch (loadError) {
      if (epoch !== offeringsEpochRef.current) return
      setOfferings([])
      setEligibleCourses([])
      setOfferingsError(
        userMessage(loadError, 'Không tải được danh sách môn đã được cấu hình trong học kỳ này.'),
      )
    } finally {
      if (epoch === offeringsEpochRef.current) {
        setIsLoadingOfferings(false)
      }
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
        if (pools.proctors.length === 0) {
          setLecturersError(
            'Chưa có tài khoản Giám thị (proctor) đang hoạt động. Vui lòng liên hệ Quản trị viên để kích hoạt tài khoản Giám thị.',
          )
        } else if (pools.examiners.length === 0) {
          setLecturersError('Chưa có Giảng viên chấm thi khả dụng cho môn học này.')
        } else {
          const hasDistinctPair =
            pools.proctors.length > 1 ||
            pools.examiners.length > 1 ||
            pools.proctors[0]?.userId !== pools.examiners[0]?.userId
          if (!hasDistinctPair) {
            setLecturersError(
              'Chưa có đủ nhân sự Giám thị và Giám khảo độc lập khả dụng cho môn học này.',
            )
          }
        }
      } catch (staffError) {
        if (requestVersion !== lecturerRequestVersion.current) return
        setLecturersError(
          userMessage(
            staffError,
            'Không tải được danh sách Giám thị và Giám khảo cho môn học này.',
          ),
        )
      } finally {
        if (requestVersion === lecturerRequestVersion.current) setIsLoadingLecturers(false)
      }
    },
    [shifts],
  )

  const loadPage = useCallback(async () => {
    const epoch = ++pageEpochRef.current
    setIsLoading(true)
    setError('')
    try {
      const [semesterResult, sessionResult, shiftResult] = await Promise.all([
        classService.listSemesters(),
        officialExamService.getSessions(),
        officialExamService.getShifts(),
      ])
      if (epoch !== pageEpochRef.current) return
      setSemesters(semesterResult)
      setSessions(sessionResult)
      setShifts(shiftResult)
      setReferenceTimestamp(Date.now())

      const today = getLocalTodayDate()
      const currentSemester =
        semesterResult.find(
          (semester) =>
            semester.isActive &&
            today >= semester.startDate.slice(0, 10) &&
            today <= semester.endDate.slice(0, 10),
        ) ?? semesterResult[0]
      if (currentSemester) {
        await loadOfferings(currentSemester.id)
        if (epoch !== pageEpochRef.current) return
        const defaultWindow = getDefaultShiftWindow(currentSemester)
        setShiftForm((current) => ({
          ...current,
          semesterId: currentSemester.id,
          shifts: current.shifts.map((item, idx) =>
            idx === 0 &&
            (!item.startTime ||
              !item.endTime ||
              !isDateInsideSemester(item.startTime, currentSemester))
              ? {
                  ...item,
                  shiftName: item.shiftName || 'Ca 1',
                  startTime: defaultWindow.startTime,
                  endTime: defaultWindow.endTime,
                }
              : item,
          ),
        }))
      }
    } catch (loadError) {
      if (epoch !== pageEpochRef.current) return
      const message = userMessage(loadError, 'Không tải được dữ liệu học kỳ và ca thi.')
      setError(message)
      toast.error(message)
    } finally {
      if (epoch === pageEpochRef.current) {
        setIsLoading(false)
      }
    }
  }, [loadOfferings])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadPage()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      pageEpochRef.current += 1
      offeringsEpochRef.current += 1
    }
  }, [loadPage])

  const resetCandidateImport = useCallback(() => {
    setCandidateFile(null)
    setCandidatePreview(null)
    setCandidateImportError('')
    setCandidateSessionId(null)
    committedCandidateKeyRef.current = null
  }, [])

  const resetCreationProgress = useCallback(() => {
    createdShiftIdByRoomIdRef.current.clear()
    committedCandidateKeyRef.current = null
  }, [])

  const handleSemesterChange = async (semesterId: string) => {
    const nextSemester = semesters.find((item) => item.id === semesterId)
    setShiftForm({ ...createInitialShiftForm(nextSemester), semesterId })
    setOfferings([])
    setOfferingsError('')
    setError('')
    resetCandidateImport()
    resetCreationProgress()
    await loadCourseLecturers('', semesterId)
    await loadOfferings(semesterId)
  }

  const handleCourseChange = (courseId: string) => {
    const chosenOffering = offerings.find((item) => item.courseId === courseId)
    resetCandidateImport()
    resetCreationProgress()
    setShiftForm((current) => ({
      ...current,
      courseId,
      sessionId: '',
      shifts: current.shifts.map((shift) => ({
        ...shift,
        rooms: shift.rooms.map((room) => ({
          ...room,
          proctorUserId: '',
          examinerUserId: '',
        })),
      })),
    }))
    void loadCourseLecturers(courseId, shiftForm.semesterId, chosenOffering?.academicProgramId)
  }

  const openCreateShift = () => {
    setError('')
    resetCreationProgress()
    setIsCreateShiftOpen(true)
  }

  const closeCreateShift = () => {
    if (busy) return
    setIsCreateShiftOpen(false)
    setError('')
    resetCreationProgress()
    createShiftButtonRef.current?.focus()
  }

  const resolveSession = async (referenceStartTime: string): Promise<OfficialExamSession> => {
    if (!selectedCourse || !selectedSemester || !referenceStartTime) {
      throw new Error('Vui lòng chọn học kỳ, môn thi và thời gian bắt đầu.')
    }
    const examDate = referenceStartTime.slice(0, 10)
    const matchingAttemptSessions = selectedCourseSessions.filter(
      (session) =>
        session.status.toLowerCase() === 'scheduled' &&
        (session.examAttemptType ?? 'FE') === shiftForm.examAttemptType,
    )
    const exactDateMatches = matchingAttemptSessions.filter(
      (session) => getSessionDate(session) === examDate,
    )
    if (exactDateMatches.length === 1) return exactDateMatches[0]
    if (exactDateMatches.length > 1) {
      throw new Error(
        'Môn này đang có nhiều đợt tổ chức trùng ngày. Vui lòng liên hệ quản trị viên.',
      )
    }
    if (matchingAttemptSessions.length === 1) return matchingAttemptSessions[0]

    const [configuration, structures] = await Promise.all([
      courseService.getConfiguration(selectedCourse.courseId),
      officialExamService.getExamStructures({ courseId: selectedCourse.courseId, isActive: true }),
    ])
    const payload: CreateOfficialExamSessionRequest = {
      courseId: selectedCourse.courseId,
      semesterId: selectedSemester.id,
      examAttemptType: shiftForm.examAttemptType,
      examStructureId: structures[0]?.id ?? null,
      title: `${selectedCourse.courseCode} · ${selectedSemester.code} · Thi thật ${examDate}`,
      examDate,
      hasFollowUp: false,
      maxFollowUpQuestions: 2,
      examInputMode: configuration.examInputMode,
      transcriptBufferSeconds: configuration.transcriptBufferSeconds,
    }
    const createdSession = await officialExamService.createSession(payload)
    setSessions((current) => [...current, createdSession])
    return createdSession
  }

  const handleSelectCandidateFile = async (file: File | null) => {
    setCandidateImportError('')
    setCandidatePreview(null)
    setCandidateFile(file)
    setCandidateSessionId(null)

    if (!file) return
    if (!selectedSemester || !selectedCourse) {
      setCandidateImportError('Vui lòng chọn học kỳ và môn thi trước khi tải danh sách thí sinh.')
      return
    }

    setCandidateImportBusy(true)
    try {
      const refStart =
        shiftForm.shifts[0]?.startTime ||
        selectedSemester.startDate.slice(0, 10) ||
        getLocalTodayDate()
      const session = await resolveSession(refStart)
      setCandidateSessionId(session.id)
      const preview = await officialExamService.previewCandidateImport(session.id, file, 'REPLACE')
      setCandidatePreview(preview)
    } catch (previewError) {
      const message = userMessage(
        previewError,
        'Không đọc được tệp danh sách thí sinh. Vui lòng kiểm tra định dạng .xlsx / .csv.',
      )
      setCandidateImportError(message)
      toast.error(message)
    } finally {
      setCandidateImportBusy(false)
    }
  }

  const submitShift = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!selectedSemester || !selectedCourse) {
      const selectionError = 'Vui lòng chọn học kỳ và môn thi trước khi tạo ca.'
      setError(selectionError)
      toast.error(selectionError)
      return
    }

    const shiftGroups = shiftForm.shifts
    if (shiftGroups.length === 0) {
      const emptyShiftError = 'Cần ít nhất 1 ca thi.'
      setError(emptyShiftError)
      toast.error(emptyShiftError)
      return
    }

    const validatedShifts: Array<{
      groupId: string
      shiftName: string
      startTimeLocal: string
      endTimeLocal: string
      startIso: string
      endIso: string
      rooms: Array<{
        rowId: string
        roomLab: string
        maxSeats: number
        proctorUserId: string
        examinerUserId: string
      }>
    }> = []

    const failAtShift = (shiftId: string, message: string) => {
      setShiftForm((current) => ({ ...current, activeShiftId: shiftId }))
      setError(message)
      toast.error(message)
    }

    for (let sIdx = 0; sIdx < shiftGroups.length; sIdx += 1) {
      const group = shiftGroups[sIdx]
      const groupLabel = group.shiftName.trim() || `Ca ${sIdx + 1}`
      const prefix = shiftGroups.length > 1 ? `${groupLabel}: ` : ''

      if (!group.shiftName.trim()) {
        failAtShift(group.id, `${prefix}Vui lòng nhập tên ca thi.`)
        return
      }
      const startMs = group.startTime ? new Date(group.startTime).getTime() : Number.NaN
      const endMs = group.endTime ? new Date(group.endTime).getTime() : Number.NaN
      if (Number.isNaN(startMs) || Number.isNaN(endMs) || endMs <= startMs) {
        failAtShift(group.id, `${prefix}Giờ kết thúc phải sau giờ bắt đầu.`)
        return
      }
      if (!isDateInsideSemester(group.startTime, selectedSemester)) {
        failAtShift(
          group.id,
          `${prefix}Ngày bắt đầu ca phải nằm trong thời gian của học kỳ đã chọn.`,
        )
        return
      }
      if (group.rooms.length === 0) {
        failAtShift(group.id, `${prefix}Cần ít nhất 1 phòng thi trong ca.`)
        return
      }

      const validatedRooms: Array<{
        rowId: string
        roomLab: string
        maxSeats: number
        proctorUserId: string
        examinerUserId: string
      }> = []
      const seenLabs = new Set<string>()
      const seenProctors = new Set<string>()
      const seenExaminers = new Set<string>()
      const seenStaff = new Set<string>()

      for (let rIdx = 0; rIdx < group.rooms.length; rIdx += 1) {
        const room = group.rooms[rIdx]
        const roomLabel = `${prefix}Phòng ${rIdx + 1}`
        const trimmedLab = room.roomLab.trim()
        if (!trimmedLab) {
          failAtShift(group.id, `${roomLabel}: vui lòng nhập tên Phòng Lab.`)
          return
        }
        const labKey = trimmedLab.toLowerCase()
        if (seenLabs.has(labKey)) {
          failAtShift(group.id, `${prefix}Phòng Lab "${trimmedLab}" bị trùng trong cùng ca thi.`)
          return
        }
        seenLabs.add(labKey)

        const capacityCheck = validateShiftCapacityInput(room.maxSeats, {
          defaultWhenEmpty: DEFAULT_SHIFT_CAPACITY,
        })
        if (capacityCheck.error || capacityCheck.value === null) {
          failAtShift(group.id, capacityCheck.error)
          return
        }

        if (!room.proctorUserId || !room.examinerUserId) {
          failAtShift(group.id, `${roomLabel}: vui lòng chọn đủ Giám thị và Giám khảo.`)
          return
        }
        if (room.proctorUserId === room.examinerUserId) {
          failAtShift(
            group.id,
            `${roomLabel}: Giám thị và Giám khảo phải là hai người khác nhau.`,
          )
          return
        }
        if (seenProctors.has(room.proctorUserId)) {
          failAtShift(
            group.id,
            `${roomLabel}: Giám thị này đã được phân công coi phòng khác trong cùng ca.`,
          )
          return
        }
        if (seenStaff.has(room.proctorUserId)) {
          failAtShift(
            group.id,
            `${roomLabel}: Cán bộ này đã được phân công làm Giám khảo ở phòng khác trong cùng ca.`,
          )
          return
        }
        seenProctors.add(room.proctorUserId)
        seenStaff.add(room.proctorUserId)

        if (seenExaminers.has(room.examinerUserId)) {
          failAtShift(
            group.id,
            `${roomLabel}: Giám khảo này đã được phân công chấm phòng khác trong cùng ca.`,
          )
          return
        }
        if (seenStaff.has(room.examinerUserId)) {
          failAtShift(
            group.id,
            `${roomLabel}: Cán bộ này đã được phân công làm Giám thị ở phòng khác trong cùng ca.`,
          )
          return
        }
        seenExaminers.add(room.examinerUserId)
        seenStaff.add(room.examinerUserId)

        validatedRooms.push({
          rowId: room.id,
          roomLab: trimmedLab,
          maxSeats: capacityCheck.value,
          proctorUserId: room.proctorUserId,
          examinerUserId: room.examinerUserId,
        })
      }

      validatedShifts.push({
        groupId: group.id,
        shiftName: group.shiftName.trim(),
        startTimeLocal: group.startTime,
        endTimeLocal: group.endTime,
        startIso: toUtcIso(group.startTime),
        endIso: toUtcIso(group.endTime),
        rooms: validatedRooms,
      })
    }

    if (candidateFile) {
      if (candidateImportBusy) {
        const waitMessage = 'Đang kiểm tra tệp danh sách thí sinh, vui lòng đợi trong giây lát.'
        setError(waitMessage)
        toast.error(waitMessage)
        return
      }
      if (!candidatePreview || !candidatePreview.canCommit || candidatePreview.validCount <= 0) {
        const fileError =
          candidateImportError ||
          'Tệp danh sách thí sinh có dòng lỗi hoặc chưa hợp lệ. Hãy kiểm tra lại hoặc gỡ tệp trước khi tạo ca.'
        setError(fileError)
        toast.error(fileError)
        return
      }
      const totalEffectiveSeats = validatedShifts.reduce(
        (sum, item) =>
          sum +
          item.rooms.reduce(
            (rSum, r) => rSum + Math.min(DEFAULT_SHIFT_CAPACITY, r.maxSeats),
            0,
          ),
        0,
      )
      if (totalEffectiveSeats < candidatePreview.validCount) {
        const seatError = `Tổng sức chứa chuẩn (${totalEffectiveSeats} ghế, tối đa 24 ghế/phòng) chưa đủ để chia ngẫu nhiên ${candidatePreview.validCount} thí sinh trong tệp Excel. Hãy bấm "Áp dụng toàn bộ" hoặc thêm phòng.`
        setError(seatError)
        toast.error(seatError)
        return
      }
    }

    setBusy(true)
    setError('')
    let currentProcessingGroupId: string | null = null
    let createdAnyRoomInThisAttempt = false
    try {
      const session = candidateSessionId
        ? sessions.find((item) => item.id === candidateSessionId) ??
          (await resolveSession(validatedShifts[0].startTimeLocal))
        : await resolveSession(validatedShifts[0].startTimeLocal)

      if (candidateFile && candidatePreview?.canCommit) {
        const candidateKey = `${session.id}:${candidateFile.name}:${candidateFile.size}:${candidateFile.lastModified}`
        if (committedCandidateKeyRef.current !== candidateKey) {
          await officialExamService.commitCandidateImport(session.id, candidateFile, 'REPLACE')
          committedCandidateKeyRef.current = candidateKey
        }
      }

      const createdShiftIds: string[] = []

      for (const shiftItem of validatedShifts) {
        currentProcessingGroupId = shiftItem.groupId

        // Bỏ qua các phòng đã tạo thành công ở lần bấm trước để tuyệt đối không tạo lặp khi retry
        const pendingRooms = shiftItem.rooms.filter((room) => {
          const alreadyCreatedId = createdShiftIdByRoomIdRef.current.get(room.rowId)
          if (alreadyCreatedId) {
            createdShiftIds.push(alreadyCreatedId)
            return false
          }
          return true
        })

        if (pendingRooms.length === 0) {
          continue
        }

        const allBatchStaffUnique = (() => {
          const staffSet = new Set<string>()
          for (const room of pendingRooms) {
            if (staffSet.has(room.proctorUserId)) return false
            staffSet.add(room.proctorUserId)
            if (staffSet.has(room.examinerUserId)) return false
            staffSet.add(room.examinerUserId)
          }
          return true
        })()
        const canUseBatchOpen =
          pendingRooms.length > 1 &&
          allBatchStaffUnique &&
          pendingRooms.every((room) => room.maxSeats <= DEFAULT_SHIFT_CAPACITY) &&
          typeof officialExamService.batchOpenShifts === 'function'

        if (shiftItem.rooms.length === 1 && pendingRooms.length === 1) {
          const onlyRoom = pendingRooms[0]
          const created = await officialExamService.createShift({
            sessionId: session.id,
            shiftName: shiftItem.shiftName,
            roomLab: onlyRoom.roomLab,
            startTime: shiftItem.startIso,
            endTime: shiftItem.endIso,
            proctorUserId: onlyRoom.proctorUserId,
            examinerUserId: onlyRoom.examinerUserId,
            maxSeats: onlyRoom.maxSeats,
          })
          createdAnyRoomInThisAttempt = true
          if (created?.id) {
            createdShiftIdByRoomIdRef.current.set(onlyRoom.rowId, created.id)
            createdShiftIds.push(created.id)
          }
        } else if (canUseBatchOpen) {
          const batchResult = await officialExamService.batchOpenShifts(session.id, {
            shiftGroupName: shiftItem.shiftName,
            startTime: shiftItem.startIso,
            endTime: shiftItem.endIso,
            rooms: pendingRooms.map((room) => ({
              roomLab: room.roomLab,
              maxSeats: room.maxSeats,
              proctorUserId: room.proctorUserId,
              examinerUserId: room.examinerUserId,
            })),
          })
          createdAnyRoomInThisAttempt = true
          const returnedShifts = batchResult.shifts ?? []
          pendingRooms.forEach((room, idx) => {
            const matched =
              returnedShifts.find(
                (item) => item.roomLab?.trim().toLowerCase() === room.roomLab.toLowerCase(),
              ) ?? returnedShifts[idx]
            if (matched?.id) {
              createdShiftIdByRoomIdRef.current.set(room.rowId, matched.id)
              createdShiftIds.push(matched.id)
            }
          })
        } else {
          for (const room of pendingRooms) {
            const created = await officialExamService.createShift({
              sessionId: session.id,
              shiftName: `${shiftItem.shiftName} - ${room.roomLab}`,
              roomLab: room.roomLab,
              startTime: shiftItem.startIso,
              endTime: shiftItem.endIso,
              proctorUserId: room.proctorUserId,
              examinerUserId: room.examinerUserId,
              maxSeats: room.maxSeats,
            })
            createdAnyRoomInThisAttempt = true
            if (created?.id) {
              createdShiftIdByRoomIdRef.current.set(room.rowId, created.id)
              createdShiftIds.push(created.id)
            }
          }
        }
      }

      let allocatedCount = 0
      let allocatedRooms: Array<{ roomLab: string; assignedNow: number; maxSeats: number }> = []
      if (
        candidateFile &&
        candidatePreview?.canCommit &&
        typeof officialExamService.autoAllocateStudents === 'function'
      ) {
        const allocation = await officialExamService.autoAllocateStudents(session.id, {
          shiftIds: createdShiftIds.length > 0 ? createdShiftIds : null,
          assignHandedOverExamSets: shiftForm.assignHandedOverExamSets,
          dryRun: false,
        })
        allocatedCount = allocation.newlyAllocatedCount
        allocatedRooms = allocation.shiftGroups.flatMap((group) =>
          group.rooms.map((room) => ({
            roomLab: room.roomLab,
            assignedNow: room.assignedNow,
            maxSeats: room.maxSeats,
          })),
        )
      }

      const totalRooms = validatedShifts.reduce((sum, item) => sum + item.rooms.length, 0)
      const totalSeats = validatedShifts.reduce(
        (sum, item) => sum + item.rooms.reduce((rSum, r) => rSum + r.maxSeats, 0),
        0,
      )
      const allLabNames = Array.from(
        new Set(validatedShifts.flatMap((item) => item.rooms.map((r) => r.roomLab))),
      )

      setCreatedShiftNotice({
        courseCode: selectedCourse.courseCode,
        shiftName: validatedShifts.map((item) => item.shiftName).join(', '),
        roomLab: allLabNames.join(', '),
        shiftCount: validatedShifts.length,
        roomCount: totalRooms,
        startTime: validatedShifts[0].startTimeLocal,
        endTime: validatedShifts[validatedShifts.length - 1].endTimeLocal,
        maxSeats: totalSeats,
        allocatedCount: allocatedCount > 0 ? allocatedCount : undefined,
        allocatedRooms: allocatedRooms.length > 0 ? allocatedRooms : undefined,
      })
      resetCandidateImport()
      resetCreationProgress()
      const freshForm = createInitialShiftForm(selectedSemester)
      setShiftForm({
        ...freshForm,
        semesterId: shiftForm.semesterId,
        examAttemptType: shiftForm.examAttemptType,
        examBlock: shiftForm.examBlock,
        courseId: shiftForm.courseId,
        sessionId: session.id,
      })
      setIsCreateShiftOpen(false)
      toast.success(
        allocatedCount > 0
          ? `Đã tạo ${validatedShifts.length} ca (${totalRooms} phòng) và chia ngẫu nhiên ${allocatedCount} thí sinh cho ${selectedCourse.courseCode}.`
          : validatedShifts.length > 1
            ? `Đã tạo ${validatedShifts.length} ca (${totalRooms} phòng · ${totalSeats} ghế) cho ${selectedCourse.courseCode}.`
            : totalRooms > 1
              ? `Đã tạo ${totalRooms} phòng (${totalSeats} ghế) cho ${selectedCourse.courseCode}.`
              : `Đã tạo ca thi cho ${selectedCourse.courseCode}.`,
      )
      const [sessionResult, shiftResult] = await Promise.all([
        officialExamService.getSessions(),
        officialExamService.getShifts(),
      ])
      setSessions(sessionResult)
      setShifts(shiftResult)
    } catch (submitError) {
      if (currentProcessingGroupId) {
        setShiftForm((current) => ({ ...current, activeShiftId: currentProcessingGroupId! }))
      }
      if (createdAnyRoomInThisAttempt) {
        void Promise.all([officialExamService.getSessions(), officialExamService.getShifts()])
          .then(([sessionResult, shiftResult]) => {
            setSessions(sessionResult)
            setShifts(shiftResult)
          })
          .catch(() => {
            // Bỏ qua lỗi refresh phụ sau lỗi chính.
          })
      }
      const message = userMessage(submitError, 'Không tạo được ca thi.')
      setError(message)
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={ds.page}>
      <PageBanner
        icon={CalendarDays}
        eyebrow="CỔNG KHẢO THÍ · MF-04"
        title="Kỳ thi chính thức"
        description="Quản lý các đợt tổ chức kỳ thi (FE / RE) theo môn học, tạo nhiều ca thi & phòng Lab kèm chia phòng ngẫu nhiên cho thí sinh."
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

      {showCampaignSetup ? (
        <ExamCampaignSetupStep
          semesters={semesters}
          selectedSemesterId={shiftForm.semesterId}
          onSemesterChange={(semesterId) => void handleSemesterChange(semesterId)}
          onSessionsCreated={loadPage}
        />
      ) : null}

      <section className={ds.section}>
        <SectionHeader
          icon={ClipboardList}
          title="Đợt tổ chức kỳ thi (FE / RE)"
          badge={isLoading ? '…' : `${visibleSessions.length}/${sessions.length}`}
          hint="Chọn một đợt thi để xem chi tiết từng phòng Lab, cập nhật ca, phát bộ đề, giám sát và công bố điểm."
          action={
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => setShowCampaignSetup((current) => !current)}
                className={ds.outlineButton}
              >
                <CalendarPlus className="size-4" />{' '}
                {showCampaignSetup ? 'Ẩn khởi tạo đợt thi' : 'Khởi tạo đợt thi (Bước 1)'}
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
            {
              label: 'Tổng đợt thi',
              value: examSummary.totalSessions,
              icon: ClipboardList,
              tone: 'blue',
            },
            {
              label: 'Phòng đã mở',
              value: examSummary.totalRooms,
              icon: CalendarDays,
              tone: 'amber',
            },
            {
              label: 'Sắp diễn ra',
              value: examSummary.upcoming,
              icon: CalendarClock,
              tone: 'green',
            },
            {
              label: 'Đã xuất bản',
              value: examSummary.published,
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
          <label className={cn('relative', ds.searchControl)} htmlFor="session-search">
            <span className="sr-only">Tìm đợt thi</span>
            <SearchField
              id="session-search"
              className={cn(ds.field, 'pl-10')}
              value={sessionSearch}
              placeholder="Tìm theo tên môn, đợt thi (FE / RE) hoặc phòng Lab"
              onChange={(event) => setSessionSearch(event.target.value)}
            />
          </label>
          <label className={ds.filterStatus} htmlFor="session-attempt-filter">
            <span className="sr-only">Lọc đợt thi FE / RE</span>
            <select
              id="session-attempt-filter"
              className={ds.field}
              value={attemptFilter}
              onChange={(event) =>
                setAttemptFilter(
                  event.target.value === 'FE' || event.target.value === 'RE'
                    ? event.target.value
                    : 'ALL',
                )
              }
            >
              <option value="ALL">Tất cả đợt thi</option>
              <option value="FE">FE · Thi lần 1</option>
              <option value="RE">RE · Thi lại</option>
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

        <div className="p-5">
          {visibleSessions.length === 0 && !isLoading ? (
            <EmptyState
              compact
              icon={CalendarDays}
              title={sessions.length === 0 ? 'Chưa có đợt tổ chức' : 'Không tìm thấy đợt thi phù hợp'}
              description={
                sessions.length === 0
                  ? 'Nhấn “Tạo ca thi” để chọn học kỳ, đợt thi (FE / RE), tải danh sách thí sinh và mở ca thi đầu tiên.'
                  : 'Thử thay đổi từ khóa hoặc bộ lọc FE / RE.'
              }
            />
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {visibleSessions.map((session) => {
                const attemptType = session.examAttemptType === 'RE' ? 'RE' : 'FE'
                const sessionShifts = shiftsBySessionId.get(session.id) ?? []
                const statusMeta = getShiftStatusMeta(session.status)
                const labList = Array.from(
                  new Set(sessionShifts.map((item) => item.roomLab).filter(Boolean)),
                )
                return (
                  <article
                    key={session.id}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-4 py-3.5 transition hover:border-[#82aedb]"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0 space-y-1.5">
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
                        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-[#5b6b84]">
                          <span>{session.examDate}</span>
                          <span>·</span>
                          <span className="font-semibold text-[#1C4D8D]">
                            {sessionShifts.length} phòng
                          </span>
                          {typeof session.candidateCount === 'number' ? (
                            <>
                              <span>·</span>
                              <span className="font-semibold text-[#0F2854]">
                                {session.candidateCount} thí sinh
                              </span>
                            </>
                          ) : null}
                          <span>·</span>
                          <span className={statusMeta.chipClass}>{statusMeta.label}</span>
                        </div>
                        {labList.length > 0 ? (
                          <p className="truncate text-[11px] text-[#71829a]">
                            Phòng Lab: {labList.slice(0, 6).join(', ')}
                            {labList.length > 6 ? ` (+${labList.length - 6})` : ''}
                          </p>
                        ) : null}
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
        selectedEligibleCourse={selectedEligibleCourse}
        selectedCourseSessions={selectedCourseSessions}
        proctorOptions={proctorOptions}
        examinerOptions={examinerOptions}
        shiftForm={shiftForm}
        error={error}
        offeringsError={offeringsError}
        lecturersError={lecturersError}
        candidateFile={candidateFile}
        candidatePreview={candidatePreview}
        candidateImportBusy={candidateImportBusy}
        candidateImportError={candidateImportError}
        onSelectCandidateFile={(file) => void handleSelectCandidateFile(file)}
        onClose={closeCreateShift}
        onSubmit={submitShift}
        onSemesterChange={(semesterId) => void handleSemesterChange(semesterId)}
        onCourseChange={handleCourseChange}
        onChangeField={(key, value) => {
          if (key === 'examAttemptType' && value !== shiftForm.examAttemptType) {
            resetCandidateImport()
          }
          setShiftForm((current) => ({ ...current, [key]: value }))
        }}
      />

      <CreatedShiftSuccessDialog
        notice={createdShiftNotice}
        onClose={() => setCreatedShiftNotice(null)}
      />
    </div>
  )
}
