import { type FormEvent, useRef, useState } from 'react'
import {
  AlertCircle,
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  BookOpen,
  CalendarDays,
  CheckCircle2,
  ClipboardCheck,
  Copy,
  DoorOpen,
  FileSpreadsheet,
  GraduationCap,
  LoaderCircle,
  Plus,
  Trash2,
  Upload,
  X,
} from 'lucide-react'
import { type ShiftStaffCandidate } from '@/services/course.service'
import type { SemesterCourseOffering, SemesterOption } from '@/types/class.types'
import type {
  CandidateImportPreviewResponse,
  ExamShift,
  OfficialExamSession,
  SemesterEligibleExamCourse,
} from '@/types/officialExam.types'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import {
  DEFAULT_SHIFT_CAPACITY,
  MAX_SHIFT_CAPACITY,
  MIN_SHIFT_CAPACITY,
  validateShiftCapacityInput,
} from '@/lib/shiftCapacity'
import {
  buildShiftRoomRecommendation,
  type ShiftPlanItem,
  type ShiftRoomRecommendation,
} from './eoFormatters'
import {
  createShiftGroupRow,
  createShiftRoomRow,
  formatExamBlockLabel,
  parseExamWaveKey,
  suggestNextShiftTimes,
  toExamWaveKey,
  type CreateShiftFormState,
  type CreateShiftGroupRow,
  type CreateShiftRoomRow,
  type CreatedShiftNotice,
  type EditShiftFormState,
} from './shiftStaffHelpers'

interface CreateSingleShiftDialogProps {
  open: boolean
  busy: boolean
  isLoading: boolean
  isLoadingOfferings: boolean
  isLoadingLecturers: boolean
  semesters: SemesterOption[]
  offerings: SemesterCourseOffering[]
  selectedSemester: SemesterOption | undefined
  selectedCourse: SemesterCourseOffering | undefined
  selectedEligibleCourse?: SemesterEligibleExamCourse | undefined
  selectedCourseSessions: OfficialExamSession[]
  proctorOptions: ShiftStaffCandidate[]
  examinerOptions: ShiftStaffCandidate[]
  shiftForm: CreateShiftFormState
  error: string
  offeringsError: string
  lecturersError: string
  candidateFile?: File | null
  candidatePreview?: CandidateImportPreviewResponse | null
  candidateImportBusy?: boolean
  candidateImportError?: string
  onSelectCandidateFile?: (file: File | null) => void
  onClose: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onSemesterChange: (semesterId: string) => void
  onCourseChange: (courseId: string) => void
  onChangeField: <K extends keyof CreateShiftFormState>(
    key: K,
    value: CreateShiftFormState[K],
  ) => void
}

export function CreateSingleShiftDialog(props: CreateSingleShiftDialogProps) {
  if (!props.open) return null
  return <CreateShiftWizardContent {...props} />
}

function CreateShiftWizardContent({
  open,
  busy,
  isLoading,
  isLoadingOfferings,
  isLoadingLecturers,
  semesters,
  offerings,
  selectedSemester,
  selectedCourse,
  selectedEligibleCourse,
  selectedCourseSessions,
  proctorOptions,
  examinerOptions,
  shiftForm,
  error,
  offeringsError,
  lecturersError,
  candidateFile = null,
  candidatePreview = null,
  candidateImportBusy = false,
  candidateImportError = '',
  onSelectCandidateFile,
  onClose,
  onSubmit,
  onSemesterChange,
  onCourseChange,
  onChangeField,
}: CreateSingleShiftDialogProps) {
  const [step, setStep] = useState<1 | 2 | 3>(1)
  const [stepError, setStepError] = useState('')
  const [hasUserCustomizedShifts, setHasUserCustomizedShifts] = useState(false)
  const candidateFileInputRef = useRef<HTMLInputElement | null>(null)
  const shifts = shiftForm.shifts.length > 0 ? shiftForm.shifts : [createShiftGroupRow()]
  const activeShiftIndex = Math.max(
    0,
    shifts.findIndex((item) => item.id === shiftForm.activeShiftId),
  )
  const activeShift = shifts[activeShiftIndex] ?? shifts[0]
  const rooms = activeShift.rooms.length > 0 ? activeShift.rooms : [createShiftRoomRow()]
  const roomCount = rooms.length

  const totalShiftsCount = shifts.length
  const totalRoomsAllShifts = shifts.reduce((sum, item) => sum + item.rooms.length, 0)
  const totalSeatsAllShifts = shifts.reduce(
    (sum, item) =>
      sum +
      item.rooms.reduce((roomSum, room) => {
        const parsed = validateShiftCapacityInput(room.maxSeats, {
          defaultWhenEmpty: DEFAULT_SHIFT_CAPACITY,
        })
        return roomSum + (parsed.value ?? 0)
      }, 0),
    0,
  )

  const analyzeShift = (shift: CreateShiftGroupRow) => {
    const roomValidations = shift.rooms.map((room) =>
      validateShiftCapacityInput(room.maxSeats, {
        defaultWhenEmpty: DEFAULT_SHIFT_CAPACITY,
      }),
    )
    const seatsTotal = roomValidations.reduce((sum, item) => sum + (item.value ?? 0), 0)
    const seenLabs = new Set<string>()
    const dupLabs = new Set<string>()
    const seenProcs = new Set<string>()
    const dupProcs = new Set<string>()
    let hasMissingField = !shift.shiftName.trim() || !shift.startTime || !shift.endTime

    if (
      shift.startTime &&
      shift.endTime &&
      new Date(shift.endTime) <= new Date(shift.startTime)
    ) {
      hasMissingField = true
    }

    shift.rooms.forEach((room, idx) => {
      const normalizedLab = room.roomLab.trim().toLowerCase()
      if (!normalizedLab) hasMissingField = true
      else if (seenLabs.has(normalizedLab)) dupLabs.add(normalizedLab)
      else seenLabs.add(normalizedLab)

      if (!room.proctorUserId || !room.examinerUserId) hasMissingField = true
      if (room.proctorUserId && room.proctorUserId === room.examinerUserId) hasMissingField = true
      if (room.proctorUserId) {
        if (seenProcs.has(room.proctorUserId)) dupProcs.add(room.proctorUserId)
        else seenProcs.add(room.proctorUserId)
      }
      if (roomValidations[idx]?.error) hasMissingField = true
    })

    const hasConflict = dupLabs.size > 0 || dupProcs.size > 0
    return {
      roomValidations,
      seatsTotal,
      duplicateRoomLabs: dupLabs,
      duplicateProctors: dupProcs,
      hasConflict,
      isComplete: !hasMissingField && !hasConflict,
    }
  }

  const shiftAnalyses = shifts.map(analyzeShift)
  const activeAnalysis = shiftAnalyses[activeShiftIndex]
  const roomValidations = activeAnalysis.roomValidations
  const totalSeatsInShift = activeAnalysis.seatsTotal
  const duplicateRoomLabs = activeAnalysis.duplicateRoomLabs
  const duplicateProctors = activeAnalysis.duplicateProctors
  const anyShiftHasConflict = shiftAnalyses.some((item) => item.hasConflict)

  const overlappingShiftLabels = shifts.reduce<string[]>((acc, other, idx) => {
    if (other.id === activeShift.id) return acc
    if (!activeShift.startTime || !activeShift.endTime || !other.startTime || !other.endTime) {
      return acc
    }
    const aStart = new Date(activeShift.startTime).getTime()
    const aEnd = new Date(activeShift.endTime).getTime()
    const oStart = new Date(other.startTime).getTime()
    const oEnd = new Date(other.endTime).getTime()
    if (
      Number.isNaN(aStart) ||
      Number.isNaN(aEnd) ||
      Number.isNaN(oStart) ||
      Number.isNaN(oEnd) ||
      aEnd <= aStart ||
      oEnd <= oStart
    ) {
      return acc
    }
    if (aStart < oEnd && oStart < aEnd) {
      acc.push(other.shiftName.trim() || `Ca ${idx + 1}`)
    }
    return acc
  }, [])

  const updateShifts = (updater: (current: CreateShiftGroupRow[]) => CreateShiftGroupRow[]) => {
    setHasUserCustomizedShifts(true)
    setStepError('')
    onChangeField('shifts', updater(shifts))
  }

  const updateActiveShiftField = <K extends 'shiftName' | 'startTime' | 'endTime'>(
    field: K,
    value: string,
  ) => {
    updateShifts((current) => {
      const next = current.map((item) =>
        item.id === activeShift.id ? { ...item, [field]: value } : item,
      )
      // Tự động nối giờ cho các ca kế tiếp nếu ca sau chưa nhập giờ
      if (field === 'startTime' || field === 'endTime') {
        for (let i = activeShiftIndex; i < next.length - 1; i += 1) {
          const prev = next[i]
          const following = next[i + 1]
          if (!following.startTime && !following.endTime && prev.startTime && prev.endTime) {
            const suggested = suggestNextShiftTimes(prev.startTime, prev.endTime)
            if (suggested.startTime && suggested.endTime) {
              next[i + 1] = {
                ...following,
                startTime: suggested.startTime,
                endTime: suggested.endTime,
              }
            }
          }
        }
      }
      return next
    })
  }

  const updateRoomField = <K extends keyof Omit<CreateShiftRoomRow, 'id'>>(
    roomId: string,
    field: K,
    value: CreateShiftRoomRow[K],
  ) => {
    updateShifts((current) =>
      current.map((item) =>
        item.id === activeShift.id
          ? {
              ...item,
              rooms: item.rooms.map((room) =>
                room.id === roomId ? { ...room, [field]: value } : room,
              ),
            }
          : item,
      ),
    )
  }

  const addRoomRow = () => {
    const fallbackSeats = rooms[rooms.length - 1]?.maxSeats || String(DEFAULT_SHIFT_CAPACITY)
    updateShifts((current) =>
      current.map((item) =>
        item.id === activeShift.id
          ? { ...item, rooms: [...item.rooms, createShiftRoomRow({ maxSeats: fallbackSeats })] }
          : item,
      ),
    )
  }

  const removeRoomRow = (roomId: string) => {
    if (rooms.length <= 1) return
    updateShifts((current) =>
      current.map((item) =>
        item.id === activeShift.id
          ? { ...item, rooms: item.rooms.filter((room) => room.id !== roomId) }
          : item,
      ),
    )
  }

  const addShiftTab = () => {
    setHasUserCustomizedShifts(true)
    setStepError('')
    const lastShift = shifts[shifts.length - 1]
    const suggestedTimes = lastShift
      ? suggestNextShiftTimes(lastShift.startTime, lastShift.endTime)
      : { startTime: '', endTime: '' }
    const newShift = createShiftGroupRow({
      shiftName: `Ca ${shifts.length + 1}`,
      startTime: suggestedTimes.startTime,
      endTime: suggestedTimes.endTime,
      rooms: [createShiftRoomRow()],
    })
    onChangeField('shifts', [...shifts, newShift])
    onChangeField('activeShiftId', newShift.id)
  }

  const removeShiftTab = (shiftId: string) => {
    if (shifts.length <= 1) return
    setHasUserCustomizedShifts(true)
    setStepError('')
    const nextShifts = shifts.filter((item) => item.id !== shiftId)
    onChangeField('shifts', nextShifts)
    if (activeShift.id === shiftId) {
      onChangeField('activeShiftId', nextShifts[0].id)
    }
  }

  const copyFromFirstShift = () => {
    const firstShift = shifts[0]
    if (!firstShift || firstShift.id === activeShift.id) return
    updateShifts((current) =>
      current.map((item) => {
        if (item.id !== activeShift.id) return item
        const nextRooms = item.rooms.map((room, idx) => {
          const source = firstShift.rooms[idx]
          if (!source) return room
          return {
            ...room,
            roomLab: source.roomLab,
            proctorUserId: source.proctorUserId,
            examinerUserId: source.examinerUserId,
          }
        })
        return { ...item, rooms: nextRooms }
      }),
    )
  }

  const applyShiftPlan = (plan: ShiftPlanItem) => {
    const nextRooms = plan.roomCapacities.map((capacity, index) => {
      const existing = rooms[index]
      return existing
        ? { ...existing, maxSeats: String(capacity) }
        : createShiftRoomRow({ maxSeats: String(capacity) })
    })
    const baseName = plan.label.replace(/\s*\([^)]*\)\s*/g, '').trim()
    const suggestedShiftName = `[${shiftForm.examBlock}] ${baseName}`
    updateShifts((current) =>
      current.map((item) =>
        item.id === activeShift.id
          ? {
              ...item,
              shiftName:
                !item.shiftName.trim() || /^(\[B(?:10|3)W\]\s*)?Ca \d+/i.test(item.shiftName.trim())
                  ? suggestedShiftName
                  : item.shiftName,
              rooms: nextRooms,
            }
          : item,
      ),
    )
  }

  const applyAllRecommendedShifts = (
    rec: ShiftRoomRecommendation,
    options: { goToStep2?: boolean } = {},
  ) => {
    if (rec.shifts.length === 0) return
    setHasUserCustomizedShifts(true)
    setStepError('')
    const expandedCapacities: number[][] = []
    for (const item of rec.shifts) {
      for (let copy = 0; copy < item.shiftCount; copy += 1) {
        expandedCapacities.push([...item.roomCapacities])
      }
    }
    const baseStart = shifts[0]?.startTime ?? ''
    const baseEnd = shifts[0]?.endTime ?? ''
    let prevStart = baseStart
    let prevEnd = baseEnd

    const nextShifts: CreateShiftGroupRow[] = expandedCapacities.map((capacities, idx) => {
      const existingShift = shifts[idx]
      let startTime = existingShift?.startTime || ''
      let endTime = existingShift?.endTime || ''
      if (idx === 0) {
        startTime = startTime || baseStart
        endTime = endTime || baseEnd
      } else if (!startTime && !endTime && prevStart && prevEnd) {
        const suggested = suggestNextShiftTimes(prevStart, prevEnd)
        startTime = suggested.startTime
        endTime = suggested.endTime
      }
      prevStart = startTime
      prevEnd = endTime

      return createShiftGroupRow({
        shiftName: `[${shiftForm.examBlock}] Ca ${idx + 1}`,
        startTime,
        endTime,
        rooms: capacities.map((cap, roomIdx) => {
          const existingRoom = existingShift?.rooms[roomIdx]
          return existingRoom
            ? { ...existingRoom, maxSeats: String(cap) }
            : createShiftRoomRow({ maxSeats: String(cap) })
        }),
      })
    })

    onChangeField('shifts', nextShifts)
    onChangeField('activeShiftId', nextShifts[0].id)
    if (options.goToStep2) {
      setStep(2)
    }
  }

  const effectiveStudentCount = (() => {
    if (candidatePreview && candidatePreview.validCount > 0) {
      return candidatePreview.validCount
    }
    if (shiftForm.examAttemptType === 'RE') {
      return selectedEligibleCourse?.reSession?.candidateCount ?? 0
    }
    return selectedEligibleCourse?.eligibleStudentCount ?? 0
  })()

  const effectiveStudentSourceLabel = (() => {
    if (candidatePreview && candidatePreview.validCount > 0) {
      return 'từ tệp Excel/CSV vừa tải lên'
    }
    if (shiftForm.examAttemptType === 'RE') {
      return selectedEligibleCourse?.reSession?.candidateCount
        ? 'từ danh sách RE đã import'
        : ''
    }
    return selectedEligibleCourse
      ? `${selectedEligibleCourse.activeClassCount} lớp trong học kỳ`
      : ''
  })()

  const effectiveSeatsForAutoAllocate = shifts.reduce(
    (sum, item) =>
      sum +
      item.rooms.reduce((roomSum, room) => {
        const parsed = validateShiftCapacityInput(room.maxSeats, {
          defaultWhenEmpty: DEFAULT_SHIFT_CAPACITY,
        })
        return roomSum + Math.min(DEFAULT_SHIFT_CAPACITY, parsed.value ?? 0)
      }, 0),
    0,
  )

  const recommendation = buildShiftRoomRecommendation(effectiveStudentCount)

  const resolveStaffName = (userId: string, pool: ShiftStaffCandidate[]) => {
    if (!userId) return 'Chưa chọn'
    const matched = pool.find((item) => item.userId === userId)
    return matched ? matched.fullName : userId
  }

  const goFromStep1ToStep2 = () => {
    setStepError('')
    if (!selectedSemester || !selectedCourse) {
      setStepError('Vui lòng chọn học kỳ và môn thi trước khi tiếp tục.')
      return
    }
    if (candidateFile) {
      if (candidateImportBusy) {
        setStepError('Đang đọc tệp danh sách thí sinh, vui lòng đợi trong giây lát.')
        return
      }
      if (!candidatePreview || !candidatePreview.canCommit || candidatePreview.validCount <= 0) {
        setStepError(
          candidateImportError ||
            'Tệp danh sách thí sinh có dòng lỗi hoặc chưa hợp lệ. Hãy kiểm tra lại hoặc gỡ tệp.',
        )
        return
      }
    }
    if (!hasUserCustomizedShifts && recommendation.recommendedShifts > 0) {
      applyAllRecommendedShifts(recommendation, { goToStep2: true })
      return
    }
    setStep(2)
  }

  const goFromStep2ToStep3 = () => {
    setStepError('')
    for (let sIdx = 0; sIdx < shifts.length; sIdx += 1) {
      const group = shifts[sIdx]
      const groupLabel = group.shiftName.trim() || `Ca ${sIdx + 1}`
      const prefix = shifts.length > 1 ? `${groupLabel}: ` : ''

      if (!group.shiftName.trim()) {
        onChangeField('activeShiftId', group.id)
        setStepError(`${prefix}Vui lòng nhập tên ca thi.`)
        return
      }
      if (
        !group.startTime ||
        !group.endTime ||
        new Date(group.endTime) <= new Date(group.startTime)
      ) {
        onChangeField('activeShiftId', group.id)
        setStepError(`${prefix}Giờ kết thúc phải sau giờ bắt đầu.`)
        return
      }
      if (selectedSemester) {
        const startDate = group.startTime.slice(0, 10)
        const semStart = selectedSemester.startDate.slice(0, 10)
        const semEnd = selectedSemester.endDate.slice(0, 10)
        if (startDate < semStart || startDate > semEnd) {
          onChangeField('activeShiftId', group.id)
          setStepError(`${prefix}Ngày bắt đầu ca phải nằm trong thời gian của học kỳ đã chọn.`)
          return
        }
      }

      const seenLabs = new Set<string>()
      const seenProctors = new Set<string>()
      for (let rIdx = 0; rIdx < group.rooms.length; rIdx += 1) {
        const room = group.rooms[rIdx]
        const roomLabel = `${prefix}Phòng ${rIdx + 1}`
        const trimmedLab = room.roomLab.trim()
        if (!trimmedLab) {
          onChangeField('activeShiftId', group.id)
          setStepError(`${roomLabel}: vui lòng nhập tên Phòng Lab.`)
          return
        }
        const labKey = trimmedLab.toLowerCase()
        if (seenLabs.has(labKey)) {
          onChangeField('activeShiftId', group.id)
          setStepError(`${prefix}Phòng Lab "${trimmedLab}" bị trùng trong cùng ca thi.`)
          return
        }
        seenLabs.add(labKey)

        const capCheck = validateShiftCapacityInput(room.maxSeats, {
          defaultWhenEmpty: DEFAULT_SHIFT_CAPACITY,
        })
        if (capCheck.error || capCheck.value === null) {
          onChangeField('activeShiftId', group.id)
          setStepError(capCheck.error || `${roomLabel}: sức chứa không hợp lệ.`)
          return
        }

        if (!room.proctorUserId || !room.examinerUserId) {
          onChangeField('activeShiftId', group.id)
          setStepError(`${roomLabel}: vui lòng chọn đủ Giám thị và Giám khảo.`)
          return
        }
        if (room.proctorUserId === room.examinerUserId) {
          onChangeField('activeShiftId', group.id)
          setStepError(`${roomLabel}: Giám thị và Giám khảo phải là hai người khác nhau.`)
          return
        }
        if (seenProctors.has(room.proctorUserId)) {
          onChangeField('activeShiftId', group.id)
          setStepError(`${roomLabel}: Giám thị này đã được phân công coi phòng khác trong cùng ca.`)
          return
        }
        seenProctors.add(room.proctorUserId)
      }
    }

    if (
      candidateFile &&
      candidatePreview?.canCommit &&
      effectiveSeatsForAutoAllocate < candidatePreview.validCount
    ) {
      setStepError(
        `Tổng sức chứa chuẩn (${effectiveSeatsForAutoAllocate} ghế, tối đa 24 ghế/phòng) chưa đủ cho ${candidatePreview.validCount} thí sinh trong tệp Excel. Hãy thêm phòng hoặc tăng sức chứa.`,
      )
      return
    }

    setStep(3)
  }

  const handleWizardSubmit = (event: FormEvent<HTMLFormElement>) => {
    if (step === 1) {
      event.preventDefault()
      goFromStep1ToStep2()
      return
    }
    if (step === 2) {
      event.preventDefault()
      goFromStep2ToStep3()
      return
    }
    onSubmit(event)
  }

  const submitLabel = busy
    ? 'Đang tạo ca…'
    : !selectedCourse
      ? 'Chọn môn để tạo ca'
      : 'Tạo ca thi'

  const combinedError = stepError || error

  const wizardSteps: Array<{ id: 1 | 2 | 3; label: string }> = [
    { id: 1, label: 'Chọn môn & DSSV' },
    { id: 2, label: 'Chia ca & Phòng thi' },
    { id: 3, label: 'Xem lại & Tạo' },
  ]

  return (
    <FormDialog
      open={open}
      align="center"
      size="xl"
      maxHeight="max-h-[min(94dvh,900px)]"
      eyebrow="MỞ CA THI MF-04"
      title="Tạo ca thi"
      icon={CalendarDays}
      busy={busy}
      onClose={onClose}
      footer={
        <DialogFooter className="flex-wrap items-center justify-between gap-3 bg-white">
          <div className="text-xs font-semibold text-[#52657d]">
            {step > 1 || totalShiftsCount > 1 || totalRoomsAllShifts > 1 || candidatePreview ? (
              <span>
                Tổng:{' '}
                <strong className="text-[#0F2854]">
                  {totalShiftsCount} ca · {totalRoomsAllShifts} phòng · {totalSeatsAllShifts} ghế
                </strong>
                {effectiveStudentCount > 0 ? (
                  <span
                    className={
                      candidateFile && effectiveSeatsForAutoAllocate < effectiveStudentCount
                        ? 'ml-1 font-bold text-[#a43d4b]'
                        : 'ml-1 text-[#1C4D8D]'
                    }
                  >
                    / {effectiveStudentCount} SV
                  </span>
                ) : null}
              </span>
            ) : (
              <span>Bước 1 / 3 · Chọn kỳ thi, môn học và danh sách thí sinh</span>
            )}
          </div>
          <div className="flex items-center gap-2">
            {step === 1 ? (
              <>
                <button
                  type="button"
                  className={ds.outlineButton}
                  onClick={onClose}
                  disabled={busy}
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={goFromStep1ToStep2}
                  disabled={
                    busy ||
                    candidateImportBusy ||
                    !selectedSemester ||
                    !selectedCourse ||
                    Boolean(candidateFile && !candidatePreview?.canCommit)
                  }
                  className={ds.primaryButton}
                >
                  Tiếp tục: Cấu hình ca &amp; phòng <ArrowRight className="size-4" />
                </button>
              </>
            ) : step === 2 ? (
              <>
                <button
                  type="button"
                  className={ds.outlineButton}
                  onClick={() => {
                    setStepError('')
                    setStep(1)
                  }}
                  disabled={busy}
                >
                  <ArrowLeft className="size-4" /> Quay lại
                </button>
                <button
                  type="button"
                  onClick={goFromStep2ToStep3}
                  disabled={busy || anyShiftHasConflict}
                  className={ds.primaryButton}
                >
                  Tiếp tục: Xem lại <ArrowRight className="size-4" />
                </button>
              </>
            ) : (
              <>
                <button
                  type="button"
                  className={ds.outlineButton}
                  onClick={() => {
                    setStepError('')
                    setStep(2)
                  }}
                  disabled={busy}
                >
                  <ArrowLeft className="size-4" /> Quay lại chỉnh sửa
                </button>
                <button
                  type="submit"
                  form="create-official-shift-form"
                  disabled={
                    busy ||
                    candidateImportBusy ||
                    !selectedSemester ||
                    !selectedCourse ||
                    anyShiftHasConflict
                  }
                  className={ds.primaryButton}
                >
                  {busy ? (
                    <LoaderCircle className="size-4 animate-spin" />
                  ) : (
                    <Plus className="size-4" />
                  )}
                  {submitLabel}
                </button>
              </>
            )}
          </div>
        </DialogFooter>
      }
    >
      <form id="create-official-shift-form" onSubmit={handleWizardSubmit} className="grid gap-5">
        <nav
          aria-label="Các bước tạo ca thi"
          className="grid grid-cols-3 gap-2 rounded-2xl border border-[#d9e8f2] bg-[#F4F9FD] p-2"
        >
          {wizardSteps.map((item) => {
            const isCompleted = step > item.id
            const isCurrent = step === item.id
            const canClick = isCompleted && !busy
            return (
              <button
                key={item.id}
                type="button"
                disabled={!canClick}
                onClick={() => {
                  if (canClick) {
                    setStepError('')
                    setStep(item.id)
                  }
                }}
                className={
                  isCurrent
                    ? 'flex items-center justify-center gap-2 rounded-xl border border-[#1C4D8D] bg-white px-3 py-2 text-xs font-bold text-[#0F2854] shadow-xs'
                    : isCompleted
                      ? 'flex items-center justify-center gap-2 rounded-xl border border-[#b9e7d5] bg-[#effbf5] px-3 py-2 text-xs font-bold text-[#146c54] transition hover:bg-[#e1f6ec]'
                      : 'flex items-center justify-center gap-2 rounded-xl border border-transparent px-3 py-2 text-xs font-semibold text-[#71829a]'
                }
              >
                {isCompleted ? (
                  <CheckCircle2 className="size-4 shrink-0 text-[#168b70]" aria-hidden="true" />
                ) : (
                  <span
                    className={
                      isCurrent
                        ? 'grid size-5 shrink-0 place-items-center rounded-full bg-[#1C4D8D] text-[11px] font-black text-white'
                        : 'grid size-5 shrink-0 place-items-center rounded-full bg-[#dbe7f2] text-[11px] font-bold text-[#52657d]'
                    }
                  >
                    {item.id}
                  </span>
                )}
                <span className="truncate">{item.label}</span>
              </button>
            )
          })}
        </nav>

        {combinedError ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
          >
            {combinedError}
          </div>
        ) : null}

        {step === 1 ? (
        <section className="rounded-2xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 sm:p-5">
          <div className="mb-4 flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-[#dbeefa] text-[#245997]">
              <GraduationCap className="size-5" aria-hidden="true" />
            </span>
            <div>
              <h3 className="text-base font-black text-[#0F2854]">Học kỳ, đợt thi và môn thi</h3>
              <p className="mt-1 text-xs text-[#71829a]">
                Chọn học kỳ và đợt thi (FE / RE), hệ thống tự động tải danh sách môn thi trong học kỳ.
              </p>
            </div>
          </div>
          <div className="grid items-start gap-4 lg:grid-cols-3">
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
                onChange={(event) => onSemesterChange(event.target.value)}
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
              htmlFor="exam-attempt-type"
            >
              Đợt thi *
              <select
                id="exam-attempt-type"
                required
                className={ds.field}
                value={toExamWaveKey(shiftForm.examAttemptType, shiftForm.examBlock)}
                disabled={isLoading}
                onChange={(event) => {
                  const parsed = parseExamWaveKey(event.target.value)
                  onChangeField('examAttemptType', parsed.examAttemptType)
                  onChangeField('examBlock', parsed.examBlock)
                  updateShifts((current) =>
                    current.map((item) =>
                      /^\[B(?:10|3)W\]\s*/i.test(item.shiftName)
                        ? {
                            ...item,
                            shiftName: item.shiftName.replace(
                              /^\[B(?:10|3)W\]\s*/i,
                              `[${parsed.examBlock}] `,
                            ),
                          }
                        : item,
                    ),
                  )
                }}
              >
                <optgroup label="Block 10 tuần (B10W)">
                  <option value="FE_B10W">FE · Block 10 tuần (Thi lần 1)</option>
                  <option value="RE_B10W">RE · Block 10 tuần (Thi lại)</option>
                </optgroup>
                <optgroup label="Block 3 tuần (B3W)">
                  <option value="FE_B3W">FE · Block 3 tuần (Thi lần 1)</option>
                  <option value="RE_B3W">RE · Block 3 tuần (Thi lại)</option>
                </optgroup>
              </select>
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
                onChange={(event) => onCourseChange(event.target.value)}
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
            <div className="mt-4 space-y-3">
              <div className="flex flex-wrap items-center gap-3 rounded-xl border border-[#bcd6ef] bg-white px-4 py-3 text-sm text-[#245997]">
                <CheckCircle2 className="size-5 shrink-0 text-[#168b70]" />
                <span>
                  Đã chọn <strong>{selectedCourse.courseCode}</strong> · {selectedCourse.courseName}
                </span>
                <span className="rounded-full bg-[#f3f8fd] px-3 py-1 text-xs font-semibold text-[#667892]">
                  {selectedCourseSessions.length} đợt kỹ thuật
                </span>
              </div>

              {onSelectCandidateFile ? (
                <div className="rounded-xl border border-[#cfe2f3] bg-white px-4 py-3.5 text-xs text-[#0F2854]">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-start gap-2.5">
                      <span className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg bg-[#eef7fd] text-[#1C4D8D]">
                        <FileSpreadsheet className="size-4" aria-hidden="true" />
                      </span>
                      <div>
                        <p className="font-bold text-[#0F2854]">
                          Danh sách thí sinh dự thi (Excel / CSV)
                        </p>
                        <p className="mt-0.5 text-[11px] text-[#5b6b84]">
                          Tải lên tệp <code>.xlsx</code> hoặc <code>.csv</code> để tính đề xuất chia
                          ca/phòng theo đúng danh sách thực tế và tự động chia ngẫu nhiên vào các
                          phòng sau khi tạo ca.
                        </p>
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <input
                        ref={candidateFileInputRef}
                        type="file"
                        accept=".xlsx,.xls,.csv"
                        className="sr-only"
                        aria-label="Chọn tệp Excel danh sách thí sinh"
                        onChange={(event) => {
                          const picked = event.target.files?.[0] ?? null
                          onSelectCandidateFile(picked)
                          event.target.value = ''
                        }}
                      />
                      <button
                        type="button"
                        disabled={busy || candidateImportBusy}
                        onClick={() => candidateFileInputRef.current?.click()}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-[#4988C4] bg-[#eef7fd] px-3 py-1.5 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#dfeffb] disabled:opacity-50"
                      >
                        {candidateImportBusy ? (
                          <LoaderCircle className="size-3.5 animate-spin" />
                        ) : (
                          <Upload className="size-3.5" />
                        )}
                        {candidateFile ? 'Đổi tệp Excel / CSV' : 'Tải lên Excel / CSV'}
                      </button>
                      {candidateFile ? (
                        <button
                          type="button"
                          disabled={busy || candidateImportBusy}
                          onClick={() => onSelectCandidateFile(null)}
                          className="rounded-lg border border-[#e3c2c7] bg-white px-2.5 py-1.5 text-[11px] font-bold text-[#a43d4b] transition hover:bg-[#fdecec] disabled:opacity-50"
                        >
                          Gỡ tệp
                        </button>
                      ) : null}
                    </div>
                  </div>

                  {candidateFile ? (
                    <div className="mt-2.5 flex flex-wrap items-center gap-2 text-[11px] text-[#52657d]">
                      <span className="rounded-md bg-[#f3f8fd] px-2 py-0.5 font-semibold text-[#0F2854]">
                        {candidateFile.name}
                      </span>
                      {candidateImportBusy ? (
                        <span className="font-semibold text-[#1C4D8D]">
                          Đang đọc và kiểm tra danh sách thí sinh…
                        </span>
                      ) : null}
                    </div>
                  ) : null}

                  {candidateImportError ? (
                    <div
                      role="alert"
                      className="mt-2.5 rounded-lg border border-[#f3c2c8] bg-[#fdecec] px-3 py-2 text-[11px] font-semibold text-[#a43d4b]"
                    >
                      {candidateImportError}
                    </div>
                  ) : null}

                  {candidatePreview && !candidateImportBusy ? (
                    <div
                      className={
                        candidatePreview.canCommit
                          ? 'mt-2.5 rounded-lg border border-[#b9e7d5] bg-[#effbf5] px-3 py-2 text-[11px] text-[#146c54]'
                          : 'mt-2.5 rounded-lg border border-[#f2d091] bg-[#fff9eb] px-3 py-2 text-[11px] text-[#8c5a00]'
                      }
                    >
                      <p className="font-bold">
                        {candidatePreview.canCommit
                          ? `Hợp lệ: ${candidatePreview.validCount} thí sinh · 0 dòng lỗi (sẽ tự động chia ngẫu nhiên vào các phòng sau khi tạo ca).`
                          : `Đọc được ${candidatePreview.totalRows} dòng: ${candidatePreview.validCount} hợp lệ · ${candidatePreview.invalidCount} dòng lỗi${candidatePreview.duplicateCount > 0 ? ` · ${candidatePreview.duplicateCount} trùng lặp` : ''}.`}
                      </p>
                      {candidatePreview.issues.length > 0 ? (
                        <ul className="mt-1 list-disc space-y-0.5 pl-4">
                          {candidatePreview.issues.slice(0, 4).map((issue, idx) => (
                            <li key={`${issue.sourceRow ?? 'row'}-${idx}`}>
                              {issue.sourceRow ? `Dòng ${issue.sourceRow}: ` : ''}
                              {issue.message}
                            </li>
                          ))}
                        </ul>
                      ) : null}
                    </div>
                  ) : null}
                </div>
              ) : null}

              {selectedEligibleCourse || (candidatePreview && candidatePreview.validCount > 0) ? (() => {
                const rec = buildShiftRoomRecommendation(effectiveStudentCount)
                return (
                  <div
                    role="status"
                    className="rounded-xl border border-[#82aedb]/60 bg-[#eef7fd] px-4 py-3.5 text-xs text-[#0F2854]"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="font-bold text-[#1C4D8D]">
                        Đề xuất chia ca &amp; phòng thi ({shiftForm.examAttemptType} ·{' '}
                        {formatExamBlockLabel(shiftForm.examBlock)} · {selectedCourse.courseCode})
                      </p>
                      {rec.recommendedShifts > 0 ? (
                        <button
                          type="button"
                          onClick={() => applyAllRecommendedShifts(rec, { goToStep2: true })}
                          className="rounded-lg border border-[#1C4D8D] bg-[#1C4D8D] px-3 py-1.5 text-[11px] font-bold text-white transition hover:bg-[#163f75]"
                        >
                          Áp dụng toàn bộ {rec.recommendedShifts} ca ({rec.totalRooms} phòng)
                        </button>
                      ) : null}
                    </div>

                    {rec.totalStudents === 0 ? (
                      <p className="mt-2 text-[#52657d]">
                        {shiftForm.examAttemptType === 'RE'
                          ? 'Chưa có danh sách thí sinh thi lại (RE). Hãy tải lên tệp Excel/CSV danh sách thí sinh RE ở trên để hệ thống tính đề xuất chia ca/phòng chính xác, hoặc thêm phòng thủ công bên dưới.'
                          : `Hiện có 0 sinh viên tham gia môn này trong học kỳ ${selectedSemester?.code ?? ''}. Bạn có thể tải tệp Excel/CSV danh sách thí sinh ở trên hoặc mở phòng dự phòng (24 SV/phòng).`}
                      </p>
                    ) : (
                      <div className="mt-2 space-y-2">
                        <p className="leading-relaxed text-[#24436b]">
                          Có{' '}
                          <strong className="text-[#0F2854]">{rec.totalStudents} sinh viên</strong>{' '}
                          {effectiveStudentSourceLabel ? `(${effectiveStudentSourceLabel})` : ''}.
                          Khuyến nghị chia làm{' '}
                          <strong className="text-[#0F2854]">{rec.recommendedShifts} ca thi</strong>,
                          mỗi ca <strong className="text-[#0F2854]">{rec.roomsPerShiftText}</strong>{' '}
                          (tổng <strong className="text-[#0F2854]">{rec.totalRooms} phòng</strong>):
                        </p>

                        <ul className="grid gap-1.5 sm:grid-cols-2">
                          {rec.shifts.map((shift) => (
                            <li
                              key={shift.label}
                              className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-[#cfe2f3] bg-white/90 px-3 py-2"
                            >
                              <div>
                                <span className="font-bold text-[#0F2854]">
                                  {shift.label}
                                  <span className="ml-1.5 font-semibold text-[#52657d]">
                                    ({shift.studentsPerShift} SV{shift.shiftCount > 1 ? '/ca' : ''})
                                  </span>
                                </span>
                                <p className="mt-0.5 font-semibold text-[#1C4D8D]">
                                  {shift.roomDistributionText}
                                </p>
                              </div>
                              <button
                                type="button"
                                onClick={() => {
                                  applyShiftPlan(shift)
                                  setStep(2)
                                }}
                                className="rounded-md border border-[#bcd6ef] bg-[#f4f9fe] px-2 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#e1effb]"
                              >
                                Áp dụng
                              </button>
                            </li>
                          ))}
                        </ul>

                        <p className="text-[11px] text-[#5b6b84]">
                          Trần chuẩn tối đa 24 SV/phòng (khớp thuật toán tự chia phòng ngẫu nhiên);
                          số SV còn lại ở ca cuối được chia đều cho các phòng của ca cuối.
                        </p>
                      </div>
                    )}
                  </div>
                )
              })() : null}
            </div>
          ) : null}
        </section>
        ) : null}

        {step === 2 ? (
        <>
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#bcd6ef] bg-[#F8FBFD] px-4 py-2.5 text-xs text-[#24436b]">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-black text-[#0F2854]">{selectedSemester?.code}</span>
            <span>·</span>
            <span className="rounded-full bg-[#eef7fd] px-2 py-0.5 font-bold text-[#1C4D8D]">
              {shiftForm.examAttemptType} · {formatExamBlockLabel(shiftForm.examBlock)}
            </span>
            <span>·</span>
            <strong className="text-[#0F2854]">{selectedCourse?.courseCode}</strong>
            {effectiveStudentCount > 0 ? (
              <>
                <span>·</span>
                <span>
                  <strong className="text-[#0F2854]">{effectiveStudentCount} SV</strong>
                  {candidateFile ? ' (từ Excel)' : ''}
                </span>
              </>
            ) : null}
          </div>
          {recommendation.recommendedShifts > 0 ? (
            <button
              type="button"
              onClick={() => applyAllRecommendedShifts(recommendation)}
              className="rounded-lg border border-[#bcd6ef] bg-white px-2.5 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#eef7fd]"
            >
              Đặt lại theo đề xuất ({recommendation.recommendedShifts} ca ·{' '}
              {recommendation.totalRooms} phòng)
            </button>
          ) : null}
        </div>

        <div className="flex flex-wrap items-center gap-2 rounded-2xl border border-[#d9e8f2] bg-[#F4F9FD] p-2">
          {shifts.map((shift, idx) => {
            const analysis = shiftAnalyses[idx]
            const isActive = shift.id === activeShift.id
            const tabTitle = shift.shiftName.trim() || `Ca ${idx + 1}`
            return (
              <div
                key={shift.id}
                className={
                  isActive
                    ? 'inline-flex items-center gap-1.5 rounded-xl border border-[#1C4D8D] bg-white px-3 py-1.5 text-xs font-bold text-[#0F2854] shadow-xs'
                    : 'inline-flex items-center gap-1.5 rounded-xl border border-transparent bg-transparent px-3 py-1.5 text-xs font-semibold text-[#52657d] hover:bg-white/70'
                }
              >
                <button
                  type="button"
                  onClick={() => onChangeField('activeShiftId', shift.id)}
                  className="inline-flex items-center gap-1.5"
                >
                  {analysis.isComplete ? (
                    <CheckCircle2 className="size-3.5 text-[#168b70]" aria-hidden="true" />
                  ) : (
                    <AlertCircle className="size-3.5 text-[#c78300]" aria-hidden="true" />
                  )}
                  <span>{tabTitle}</span>
                  <span className="rounded-full bg-[#eef6fd] px-1.5 py-0.5 text-[10px] font-bold text-[#1C4D8D]">
                    {shift.rooms.length} phòng · {analysis.seatsTotal} ghế
                  </span>
                </button>
                {shifts.length > 1 ? (
                  <button
                    type="button"
                    onClick={() => removeShiftTab(shift.id)}
                    disabled={busy}
                    aria-label={`Xóa ${tabTitle}`}
                    className="ml-0.5 rounded-md p-0.5 text-[#71829a] transition hover:bg-[#fdecec] hover:text-[#a43d4b]"
                  >
                    <X className="size-3.5" />
                  </button>
                ) : null}
              </div>
            )
          })}
          <button
            type="button"
            onClick={addShiftTab}
            disabled={busy}
            className="inline-flex items-center gap-1 rounded-xl border border-dashed border-[#4988C4] bg-white/80 px-3 py-1.5 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#e6f3fc] disabled:opacity-50"
          >
            <Plus className="size-3.5" /> Thêm ca
          </button>
        </div>

        <section className="rounded-2xl border border-[#d9e8f2] bg-white p-4 sm:p-5">
          <div className="mb-4 flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-[#dbeefa] text-[#245997]">
              <BookOpen className="size-5" aria-hidden="true" />
            </span>
            <div>
              <h3 className="text-base font-black text-[#0F2854]">
                Thông tin chung của {activeShift.shiftName.trim() || `Ca ${activeShiftIndex + 1}`}
              </h3>
              <p className="mt-1 text-xs text-[#71829a]">
                {selectedCourse
                  ? `Nhập tên ca và khung giờ dùng chung cho tất cả các phòng thuộc ca này (${selectedCourse.courseCode}).`
                  : 'Chọn môn thi trước'}
              </p>
            </div>
          </div>

          {overlappingShiftLabels.length > 0 ? (
            <div
              role="alert"
              className="mb-4 flex items-start gap-2 rounded-xl border border-[#f2d091] bg-[#fff9eb] px-3.5 py-2.5 text-xs font-semibold text-[#8c5a00]"
            >
              <AlertTriangle className="mt-0.5 size-4 shrink-0" />
              <p>
                Khung giờ của ca này đang chồng lấn với{' '}
                <strong>{overlappingShiftLabels.join(', ')}</strong>. Hãy đảm bảo 2 ca chạy song
                song không dùng trùng Phòng Lab hoặc Giám thị.
              </p>
            </div>
          ) : null}

          <div className="grid gap-4 lg:grid-cols-3">
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-name">
              Tên ca *
              <input
                id="shift-name"
                required
                placeholder={`Ví dụ: Ca ${activeShiftIndex + 1}`}
                className={ds.field}
                value={activeShift.shiftName}
                onChange={(event) => updateActiveShiftField('shiftName', event.target.value)}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-start">
              Bắt đầu *
              <input
                id="shift-start"
                required
                type="datetime-local"
                className={ds.field}
                value={activeShift.startTime}
                onChange={(event) => updateActiveShiftField('startTime', event.target.value)}
              />
            </label>
            <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="shift-end">
              Kết thúc *
              <input
                id="shift-end"
                required
                type="datetime-local"
                className={ds.field}
                value={activeShift.endTime}
                onChange={(event) => updateActiveShiftField('endTime', event.target.value)}
              />
            </label>
          </div>
        </section>

        <section className="rounded-2xl border border-[#d9e8f2] bg-white p-4 sm:p-5">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-start gap-3">
              <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-[#dbeefa] text-[#245997]">
                <DoorOpen className="size-5" aria-hidden="true" />
              </span>
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="text-base font-black text-[#0F2854]">
                    Danh sách phòng thi trong {activeShift.shiftName.trim() || `Ca ${activeShiftIndex + 1}`}
                  </h3>
                  <span className="rounded-full bg-[#eef6fd] px-2.5 py-0.5 text-xs font-bold text-[#1C4D8D]">
                    {roomCount} phòng · {totalSeatsInShift} ghế
                  </span>
                </div>
                <p className="mt-1 text-xs text-[#71829a]">
                  Phân công phòng Lab, sức chứa, Giám thị và Giám khảo cho từng phòng chạy song song
                  trong cùng ca.
                </p>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {activeShiftIndex > 0 ? (
                <button
                  type="button"
                  onClick={copyFromFirstShift}
                  disabled={busy}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-[#bcd6ef] bg-white px-3 py-2 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#eef7fd] disabled:opacity-50"
                >
                  <Copy className="size-3.5" /> Dùng lại phòng &amp; nhân sự từ Ca 1
                </button>
              ) : null}
              <button
                type="button"
                onClick={addRoomRow}
                disabled={busy}
                className="inline-flex items-center gap-1.5 rounded-xl border border-[#4988C4] bg-[#eef7fd] px-3 py-2 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#dfeffb] disabled:opacity-50"
              >
                <Plus className="size-3.5" /> Thêm phòng
              </button>
            </div>
          </div>

          {duplicateRoomLabs.size > 0 || duplicateProctors.size > 0 ? (
            <div
              role="alert"
              className="mb-4 flex items-start gap-2 rounded-xl border border-[#f2d091] bg-[#fff9eb] px-3.5 py-2.5 text-xs font-semibold text-[#8c5a00]"
            >
              <AlertTriangle className="mt-0.5 size-4 shrink-0" />
              <div className="space-y-0.5">
                {duplicateRoomLabs.size > 0 ? (
                  <p>Có phòng Lab đang bị nhập trùng trong cùng ca thi. Hãy đặt tên phòng khác nhau.</p>
                ) : null}
                {duplicateProctors.size > 0 ? (
                  <p>
                    Một Giám thị không thể coi 2 phòng cùng khung giờ. Hãy chọn Giám thị khác cho
                    từng phòng.
                  </p>
                ) : null}
              </div>
            </div>
          ) : null}

          <div className="space-y-3">
            {rooms.map((room, index) => {
              const capacityCheck = roomValidations[index]
              const isLabDuplicate =
                room.roomLab.trim() !== '' &&
                duplicateRoomLabs.has(room.roomLab.trim().toLowerCase())
              const isProctorDuplicate =
                room.proctorUserId !== '' && duplicateProctors.has(room.proctorUserId)
              const labInputId = index === 0 ? 'shift-room' : `shift-room-${index}`
              const seatsInputId = index === 0 ? 'shift-max-seats' : `shift-max-seats-${index}`
              const proctorSelectId = index === 0 ? 'shift-proctor' : `shift-proctor-${index}`
              const examinerSelectId = index === 0 ? 'shift-examiner' : `shift-examiner-${index}`

              return (
                <div
                  key={room.id}
                  className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3.5"
                >
                  <div className="mb-3 flex items-center justify-between gap-2">
                    <span className="text-xs font-black tracking-[0.04em] text-[#1C4D8D] uppercase">
                      Phòng {index + 1}
                    </span>
                    {roomCount > 1 ? (
                      <button
                        type="button"
                        onClick={() => removeRoomRow(room.id)}
                        disabled={busy}
                        aria-label={`Xóa phòng ${index + 1}`}
                        className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-[11px] font-bold text-[#a43d4b] transition hover:bg-[#fdecec] disabled:opacity-50"
                      >
                        <Trash2 className="size-3.5" /> Xóa
                      </button>
                    ) : null}
                  </div>

                  <div className="grid items-start gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,0.7fr)_minmax(0,1.2fr)_minmax(0,1.2fr)]">
                    <label
                      className="flex min-w-0 flex-col gap-1.5 text-xs font-bold text-[#52657d]"
                      htmlFor={labInputId}
                    >
                      <span className="whitespace-nowrap">Phòng Lab *</span>
                      <input
                        id={labInputId}
                        required
                        placeholder={`VD: AL-R20${index + 1}`}
                        className={ds.field}
                        value={room.roomLab}
                        onChange={(event) => updateRoomField(room.id, 'roomLab', event.target.value)}
                      />
                      {isLabDuplicate ? (
                        <span className="text-[11px] font-semibold text-[#a43d4b]">
                          Trùng tên phòng trong ca
                        </span>
                      ) : null}
                    </label>

                    <label
                      className="flex min-w-0 flex-col gap-1.5 text-xs font-bold text-[#52657d]"
                      htmlFor={seatsInputId}
                    >
                      <span className="whitespace-nowrap">
                        Sức chứa<span className="sr-only"> tối đa (maxSeats)</span> *
                      </span>
                      <input
                        id={seatsInputId}
                        type="number"
                        min={MIN_SHIFT_CAPACITY}
                        max={MAX_SHIFT_CAPACITY}
                        step={1}
                        className={ds.field}
                        value={room.maxSeats}
                        onChange={(event) =>
                          updateRoomField(room.id, 'maxSeats', event.target.value)
                        }
                      />
                      {room.maxSeats.trim() !== '' && capacityCheck.error ? (
                        <span role="alert" className="text-[11px] font-semibold text-[#a43d4b]">
                          {capacityCheck.error}
                        </span>
                      ) : null}
                    </label>

                    <label
                      className="flex min-w-0 flex-col gap-1.5 text-xs font-bold text-[#52657d]"
                      htmlFor={proctorSelectId}
                    >
                      <span className="whitespace-nowrap">Giám thị *</span>
                      <select
                        id={proctorSelectId}
                        required
                        className={ds.field}
                        value={room.proctorUserId}
                        onChange={(event) =>
                          updateRoomField(room.id, 'proctorUserId', event.target.value)
                        }
                        disabled={!selectedCourse || isLoadingLecturers || proctorOptions.length === 0}
                      >
                        <option value="">
                          {isLoadingLecturers
                            ? 'Đang tải…'
                            : selectedCourse
                              ? 'Chọn giám thị'
                              : 'Chọn môn thi trước'}
                        </option>
                        {proctorOptions.map((candidate) => {
                          const assignedRoomIndex = rooms.findIndex(
                            (other) => other.id !== room.id && other.proctorUserId === candidate.userId,
                          )
                          const isTakenInOtherRoom = assignedRoomIndex !== -1
                          return (
                            <option
                              key={`proctor-${room.id}-${candidate.userId}`}
                              value={candidate.userId}
                              disabled={candidate.userId === room.examinerUserId || isTakenInOtherRoom}
                            >
                              {candidate.fullName}
                              {candidate.detail ? ` · ${candidate.detail}` : ''}
                              {isTakenInOtherRoom ? ` (Đã coi Phòng ${assignedRoomIndex + 1})` : ''}
                            </option>
                          )
                        })}
                      </select>
                      {isProctorDuplicate ? (
                        <span className="text-[11px] font-semibold text-[#a43d4b]">
                          Giám thị trùng phòng khác
                        </span>
                      ) : null}
                    </label>

                    <label
                      className="flex min-w-0 flex-col gap-1.5 text-xs font-bold text-[#52657d]"
                      htmlFor={examinerSelectId}
                    >
                      <span className="whitespace-nowrap">Giám khảo *</span>
                      <select
                        id={examinerSelectId}
                        required
                        className={ds.field}
                        value={room.examinerUserId}
                        onChange={(event) =>
                          updateRoomField(room.id, 'examinerUserId', event.target.value)
                        }
                        disabled={
                          !selectedCourse || isLoadingLecturers || examinerOptions.length === 0
                        }
                      >
                        <option value="">
                          {isLoadingLecturers
                            ? 'Đang tải…'
                            : selectedCourse
                              ? 'Chọn giám khảo'
                              : 'Chọn môn thi trước'}
                        </option>
                        {examinerOptions.map((candidate) => (
                          <option
                            key={`examiner-${room.id}-${candidate.userId}`}
                            value={candidate.userId}
                            disabled={candidate.userId === room.proctorUserId}
                          >
                            {candidate.fullName}
                            {candidate.detail ? ` · ${candidate.detail}` : ''}
                          </option>
                        ))}
                      </select>
                    </label>
                  </div>
                </div>
              )
            })}
          </div>

          <p className="mt-3 text-[11px] font-medium text-[#71829a]">
            Mặc định {DEFAULT_SHIFT_CAPACITY} ghế/phòng · Giá trị hợp lệ: số nguyên từ{' '}
            {MIN_SHIFT_CAPACITY} đến {MAX_SHIFT_CAPACITY} (trần cứng {MAX_SHIFT_CAPACITY}).
          </p>

          {selectedCourse ? (
            <p
              className="mt-2 text-xs font-medium text-[#71829a]"
              role={lecturersError ? 'alert' : undefined}
            >
              {isLoadingLecturers
                ? 'Đang tải danh sách nhân sự coi thi và chấm thi cho môn đã chọn…'
                : lecturersError ||
                  `Sẵn sàng phân công: ${proctorOptions.length} giám thị · ${examinerOptions.length} giám khảo đủ điều kiện.`}
            </p>
          ) : null}
        </section>
        </>
        ) : null}

        {step === 3 ? (
          <div className="space-y-4">
            <section className="rounded-2xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 sm:p-5">
              <div className="mb-3 flex items-start gap-3">
                <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-[#dbeefa] text-[#245997]">
                  <ClipboardCheck className="size-5" aria-hidden="true" />
                </span>
                <div>
                  <h3 className="text-base font-black text-[#0F2854]">
                    Xác nhận thông tin trước khi tạo
                  </h3>
                  <p className="mt-0.5 text-xs text-[#71829a]">
                    Kiểm tra lại toàn bộ ca thi, phòng Lab và nhân sự trước khi hệ thống lưu và chia
                    phòng.
                  </p>
                </div>
              </div>

              <dl className="grid gap-3 rounded-xl border border-[#d9e8f2] bg-white p-4 text-xs sm:grid-cols-2 lg:grid-cols-4">
                <div>
                  <dt className="font-bold text-[#71829a]">Học kỳ</dt>
                  <dd className="mt-1 font-bold text-[#0F2854]">
                    {selectedSemester?.name || selectedSemester?.code} ({selectedSemester?.code})
                  </dd>
                </div>
                <div>
                  <dt className="font-bold text-[#71829a]">Đợt thi &amp; Block</dt>
                  <dd className="mt-1 font-bold text-[#0F2854]">
                    {shiftForm.examAttemptType} · {formatExamBlockLabel(shiftForm.examBlock)}
                  </dd>
                </div>
                <div>
                  <dt className="font-bold text-[#71829a]">Môn thi</dt>
                  <dd className="mt-1 font-bold text-[#0F2854]">
                    {selectedCourse?.courseCode} · {selectedCourse?.courseName}
                  </dd>
                </div>
                <div>
                  <dt className="font-bold text-[#71829a]">Danh sách thí sinh</dt>
                  <dd className="mt-1 font-bold text-[#146c54]">
                    {candidateFile && candidatePreview?.canCommit
                      ? `${candidatePreview.validCount} SV (${candidateFile.name}) · Tự chia ngẫu nhiên`
                      : 'Không đính kèm Excel (mở phòng trống)'}
                  </dd>
                </div>
              </dl>
            </section>

            <section className="space-y-3 rounded-2xl border border-[#d9e8f2] bg-white p-4 sm:p-5">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="text-sm font-black text-[#0F2854]">
                  Danh sách ca &amp; phòng sẽ tạo ({totalShiftsCount} ca · {totalRoomsAllShifts}{' '}
                  phòng · {totalSeatsAllShifts} ghế)
                </h3>
                <button
                  type="button"
                  onClick={() => setStep(2)}
                  disabled={busy}
                  className="text-xs font-bold text-[#1C4D8D] hover:underline"
                >
                  Chỉnh sửa ca / phòng
                </button>
              </div>

              <div className="space-y-3">
                {shifts.map((shift, sIdx) => (
                  <div
                    key={shift.id}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3.5 text-xs"
                  >
                    <div className="mb-2.5 flex flex-wrap items-center justify-between gap-2">
                      <span className="font-black text-[#0F2854]">
                        {shift.shiftName.trim() || `Ca ${sIdx + 1}`}
                      </span>
                      <span className="font-semibold text-[#52657d]">
                        {shift.startTime.replace('T', ' ')} → {shift.endTime.replace('T', ' ')}
                      </span>
                    </div>
                    <div className="overflow-x-auto rounded-lg border border-[#e2edf5] bg-white">
                      <table className="w-full text-left text-xs">
                        <thead className="bg-[#F4F9FD] text-[11px] font-bold text-[#5b6b84]">
                          <tr>
                            <th className="px-3 py-2">Phòng Lab</th>
                            <th className="px-3 py-2">Sức chứa</th>
                            <th className="px-3 py-2">Giám thị</th>
                            <th className="px-3 py-2">Giám khảo</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[#eaf1f7]">
                          {shift.rooms.map((room) => (
                            <tr key={room.id}>
                              <td className="px-3 py-2 font-bold text-[#0F2854]">{room.roomLab}</td>
                              <td className="px-3 py-2 font-semibold text-[#1C4D8D]">
                                {room.maxSeats} ghế
                              </td>
                              <td className="px-3 py-2 text-[#24436b]">
                                {resolveStaffName(room.proctorUserId, proctorOptions)}
                              </td>
                              <td className="px-3 py-2 text-[#24436b]">
                                {resolveStaffName(room.examinerUserId, examinerOptions)}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ))}
              </div>
            </section>
          </div>
        ) : null}
      </form>
    </FormDialog>
  )
}

interface CreatedShiftSuccessDialogProps {
  notice: CreatedShiftNotice | null
  onClose: () => void
}

export function CreatedShiftSuccessDialog({ notice, onClose }: CreatedShiftSuccessDialogProps) {
  if (!notice) return null
  return (
    <FormDialog
      open
      align="center"
      size="md"
      titleId="created-shift-success-title"
      eyebrow="Cập nhật lịch thi"
      title="Tạo ca thi thành công"
      icon={CheckCircle2}
      onClose={onClose}
      footer={
        <DialogFooter className="justify-end bg-white">
          <button type="button" className={ds.primaryButton} onClick={onClose}>
            Đã hiểu
          </button>
        </DialogFooter>
      }
    >
      <div className="space-y-4">
        <div className="rounded-xl border border-[#b9e7d5] bg-[#effbf5] px-4 py-3 text-sm font-semibold text-[#14795f]">
          {notice.allocatedCount && notice.allocatedCount > 0
            ? `Đã tạo ca thi và chia ngẫu nhiên ${notice.allocatedCount} thí sinh vào các phòng.`
            : 'Ca thi đã được lưu và sẵn sàng để tiếp tục phân bổ vé thi.'}
        </div>
        {notice.allocatedRooms && notice.allocatedRooms.length > 0 ? (
          <div className="rounded-xl border border-[#d9e8f2] bg-white p-3.5 text-xs">
            <p className="mb-2 font-bold text-[#1C4D8D]">Kết quả chia phòng ngẫu nhiên</p>
            <ul className="grid gap-1.5 sm:grid-cols-2">
              {notice.allocatedRooms.map((room, idx) => (
                <li
                  key={`${room.roomLab}-${idx}`}
                  className="flex items-center justify-between rounded-lg border border-[#e3eef7] bg-[#F8FBFD] px-3 py-1.5"
                >
                  <span className="font-bold text-[#0F2854]">Phòng {room.roomLab}</span>
                  <span className="font-semibold text-[#14795f]">
                    {room.assignedNow}/{room.maxSeats} SV
                  </span>
                </li>
              ))}
            </ul>
          </div>
        ) : null}
        <dl className="grid gap-3 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs font-bold text-[#71829a]">Môn thi</dt>
            <dd className="mt-1 font-bold text-[#0F2854]">{notice.courseCode}</dd>
          </div>
          <div>
            <dt className="text-xs font-bold text-[#71829a]">
              {notice.shiftCount && notice.shiftCount > 1
                ? `Các ca thi (${notice.shiftCount} ca)`
                : 'Tên ca'}
            </dt>
            <dd className="mt-1 font-bold text-[#0F2854]">{notice.shiftName}</dd>
          </div>
          <div>
            <dt className="text-xs font-bold text-[#71829a]">
              {notice.roomCount && notice.roomCount > 1
                ? `Phòng Lab (${notice.roomCount} phòng)`
                : 'Phòng Lab'}
            </dt>
            <dd className="mt-1 font-bold text-[#0F2854]">{notice.roomLab}</dd>
          </div>
          <div>
            <dt className="text-xs font-bold text-[#71829a]">Thời gian</dt>
            <dd className="mt-1 font-bold text-[#0F2854]">
              {notice.startTime} – {notice.endTime}
            </dd>
          </div>
          <div>
            <dt className="text-xs font-bold text-[#71829a]">
              {notice.roomCount && notice.roomCount > 1 ? 'Tổng sức chứa' : 'Sức chứa tối đa'}
            </dt>
            <dd className="mt-1 font-bold text-[#0F2854]">{notice.maxSeats} ghế</dd>
          </div>
        </dl>
      </div>
    </FormDialog>
  )
}

interface EditSingleShiftDialogProps {
  shift: ExamShift | null
  form: EditShiftFormState
  proctorOptions: ShiftStaffCandidate[]
  examinerOptions: ShiftStaffCandidate[]
  busy: boolean
  error: string
  onClose: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onChangeField: <K extends keyof EditShiftFormState>(
    key: K,
    value: EditShiftFormState[K],
  ) => void
}

export function EditSingleShiftDialog({
  shift,
  form,
  proctorOptions,
  examinerOptions,
  busy,
  error,
  onClose,
  onSubmit,
  onChangeField,
}: EditSingleShiftDialogProps) {
  if (!shift) return null
  const editCapacityValidation = validateShiftCapacityInput(form.maxSeats, {
    defaultWhenEmpty: shift.maxSeats ?? DEFAULT_SHIFT_CAPACITY,
  })

  return (
    <FormDialog
      open
      align="center"
      size="lg"
      eyebrow="CẤU HÌNH CA THI MF-04"
      title="Cập nhật ca thi"
      icon={CalendarDays}
      busy={busy}
      onClose={onClose}
      footer={
        <DialogFooter className="justify-end bg-white">
          <button type="button" className={ds.outlineButton} onClick={onClose} disabled={busy}>
            Hủy
          </button>
          <button
            type="submit"
            form="update-official-shift-form"
            disabled={busy}
            className={ds.primaryButton}
          >
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : null}
            {busy ? 'Đang lưu…' : 'Lưu cập nhật ca thi'}
          </button>
        </DialogFooter>
      }
    >
      <form id="update-official-shift-form" onSubmit={onSubmit} className="grid gap-4">
        {error ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
          >
            {error}
          </div>
        ) : null}

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="edit-shift-name">
            Tên ca *
            <input
              id="edit-shift-name"
              required
              className={ds.field}
              value={form.shiftName}
              onChange={(event) => onChangeField('shiftName', event.target.value)}
            />
          </label>
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="edit-shift-room">
            Phòng Lab *
            <input
              id="edit-shift-room"
              required
              className={ds.field}
              value={form.roomLab}
              onChange={(event) => onChangeField('roomLab', event.target.value)}
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
              value={form.maxSeats}
              onChange={(event) => onChangeField('maxSeats', event.target.value)}
            />
            <span className="text-[11px] font-medium text-[#71829a]">
              Sức chứa hiện tại: {shift.maxSeats ?? DEFAULT_SHIFT_CAPACITY} · Đã phát:{' '}
              {shift.assignedTicketsCount ?? 0} vé · Trần hệ thống: {MAX_SHIFT_CAPACITY}
            </span>
            {form.maxSeats.trim() !== '' && editCapacityValidation.error ? (
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
              value={form.startTime}
              onChange={(event) => onChangeField('startTime', event.target.value)}
            />
          </label>
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor="edit-shift-end">
            Kết thúc *
            <input
              id="edit-shift-end"
              required
              type="datetime-local"
              className={ds.field}
              value={form.endTime}
              onChange={(event) => onChangeField('endTime', event.target.value)}
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
              value={form.proctorUserId}
              onChange={(event) => onChangeField('proctorUserId', event.target.value)}
            >
              <option value="">Chọn giám thị</option>
              {shift.proctorUserId &&
              !proctorOptions.some((item) => item.userId === shift.proctorUserId) ? (
                <option value={shift.proctorUserId}>
                  {shift.proctorName || shift.proctorUserId}
                </option>
              ) : null}
              {proctorOptions.map((candidate) => (
                <option
                  key={`edit-proctor-${candidate.userId}`}
                  value={candidate.userId}
                  disabled={candidate.userId === form.examinerUserId}
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
              value={form.examinerUserId}
              onChange={(event) => onChangeField('examinerUserId', event.target.value)}
            >
              <option value="">Chọn giám khảo</option>
              {shift.examinerUserId &&
              !examinerOptions.some((item) => item.userId === shift.examinerUserId) ? (
                <option value={shift.examinerUserId}>
                  {shift.examinerName || shift.examinerUserId}
                </option>
              ) : null}
              {examinerOptions.map((candidate) => (
                <option
                  key={`edit-examiner-${candidate.userId}`}
                  value={candidate.userId}
                  disabled={candidate.userId === form.proctorUserId}
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
  )
}
