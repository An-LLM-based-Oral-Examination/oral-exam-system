import { courseService, type ShiftStaffCandidate } from '@/services/course.service'
import type { ExamAttemptType, ExamShift } from '@/types/officialExam.types'
import { DEFAULT_SHIFT_CAPACITY } from '@/lib/shiftCapacity'

export interface CreateShiftRoomRow {
  id: string
  roomLab: string
  maxSeats: string
  proctorUserId: string
  examinerUserId: string
}

export interface CreateShiftGroupRow {
  id: string
  shiftName: string
  startTime: string
  endTime: string
  rooms: CreateShiftRoomRow[]
}

let roomRowCounter = 1
let shiftGroupCounter = 1

export function createShiftRoomRow(
  overrides: Partial<Omit<CreateShiftRoomRow, 'id'>> = {},
): CreateShiftRoomRow {
  roomRowCounter += 1
  return {
    id: `room-row-${roomRowCounter}`,
    roomLab: overrides.roomLab ?? '',
    maxSeats: overrides.maxSeats ?? String(DEFAULT_SHIFT_CAPACITY),
    proctorUserId: overrides.proctorUserId ?? '',
    examinerUserId: overrides.examinerUserId ?? '',
  }
}

export function createShiftGroupRow(
  overrides: Partial<Omit<CreateShiftGroupRow, 'id'>> = {},
): CreateShiftGroupRow {
  shiftGroupCounter += 1
  return {
    id: `shift-group-${shiftGroupCounter}`,
    shiftName: overrides.shiftName ?? '',
    startTime: overrides.startTime ?? '',
    endTime: overrides.endTime ?? '',
    rooms: overrides.rooms && overrides.rooms.length > 0 ? overrides.rooms : [createShiftRoomRow()],
  }
}

export function suggestNextShiftTimes(
  prevStartTime: string,
  prevEndTime: string,
  gapMinutes = 15,
): { startTime: string; endTime: string } {
  if (!prevStartTime || !prevEndTime) return { startTime: '', endTime: '' }
  const prevStart = new Date(prevStartTime)
  const prevEnd = new Date(prevEndTime)
  if (Number.isNaN(prevStart.getTime()) || Number.isNaN(prevEnd.getTime()) || prevEnd <= prevStart) {
    return { startTime: '', endTime: '' }
  }
  const durationMs = prevEnd.getTime() - prevStart.getTime()
  const nextStart = new Date(prevEnd.getTime() + gapMinutes * 60_000)
  const nextEnd = new Date(nextStart.getTime() + durationMs)
  return {
    startTime: toDateTimeLocalValue(nextStart.toISOString()),
    endTime: toDateTimeLocalValue(nextEnd.toISOString()),
  }
}

export type ExamBlockType = 'B10W' | 'B3W'
export type ExamWaveKey = 'FE_B10W' | 'RE_B10W' | 'FE_B3W' | 'RE_B3W'

export function formatExamBlockLabel(block: ExamBlockType): string {
  return block === 'B3W' ? 'Block 3 tuần' : 'Block 10 tuần'
}

export function parseExamWaveKey(value: string): {
  examAttemptType: ExamAttemptType
  examBlock: ExamBlockType
} {
  switch (value) {
    case 'RE_B10W':
      return { examAttemptType: 'RE', examBlock: 'B10W' }
    case 'FE_B3W':
      return { examAttemptType: 'FE', examBlock: 'B3W' }
    case 'RE_B3W':
      return { examAttemptType: 'RE', examBlock: 'B3W' }
    case 'FE_B10W':
    default:
      return { examAttemptType: 'FE', examBlock: 'B10W' }
  }
}

export function toExamWaveKey(attempt: ExamAttemptType, block: ExamBlockType): ExamWaveKey {
  return `${attempt}_${block}` as ExamWaveKey
}

export function extractShiftBlockTag(shiftName?: string | null): ExamBlockType | null {
  if (!shiftName) return null
  if (/\[B3W\]|\bB3W\b|Block\s*3/i.test(shiftName)) return 'B3W'
  if (/\[B10W\]|\bB10W\b|Block\s*10/i.test(shiftName)) return 'B10W'
  return null
}

export interface CreateShiftFormState {
  semesterId: string
  examAttemptType: ExamAttemptType
  examBlock: ExamBlockType
  courseId: string
  sessionId: string
  activeShiftId: string
  shifts: CreateShiftGroupRow[]
}

export interface EditShiftFormState {
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  proctorUserId: string
  examinerUserId: string
  maxSeats: string
}

export interface AllocatedRoomSummary {
  roomLab: string
  assignedNow: number
  maxSeats: number
}

export interface CreatedShiftNotice {
  courseCode: string
  shiftName: string
  roomLab: string
  shiftCount?: number
  roomCount?: number
  startTime: string
  endTime: string
  maxSeats: number
  allocatedCount?: number
  allocatedRooms?: AllocatedRoomSummary[]
}

export function createInitialShiftForm(): CreateShiftFormState {
  const firstShift = createShiftGroupRow()
  return {
    semesterId: '',
    examAttemptType: 'FE',
    examBlock: 'B10W',
    courseId: '',
    sessionId: '',
    activeShiftId: firstShift.id,
    shifts: [firstShift],
  }
}

export const INITIAL_CREATE_SHIFT_FORM: CreateShiftFormState = createInitialShiftForm()

export function toUtcIso(localDateTime: string) {
  const parsedDate = new Date(localDateTime)
  if (Number.isNaN(parsedDate.getTime())) {
    throw new Error('Thời gian ca thi không hợp lệ.')
  }
  return parsedDate.toISOString()
}

export function toDateTimeLocalValue(value: string) {
  if (!value) return ''
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) return value
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value.slice(0, 16)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}T${pad(parsed.getHours())}:${pad(parsed.getMinutes())}`
}

export async function resolveShiftStaffPools(
  courseId: string,
  semesterId: string,
  existingShifts: ExamShift[],
  academicProgramId?: string | null,
): Promise<{ proctors: ShiftStaffCandidate[]; examiners: ShiftStaffCandidate[] }> {
  const proctorMap = new Map<string, ShiftStaffCandidate>()
  const examinerMap = new Map<string, ShiftStaffCandidate>()
  const departmentHeadIds = new Set<string>()
  let directCourseError = false

  if (typeof courseService.getCourseLecturers === 'function') {
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
    } catch {
      directCourseError = true
    }
  } else {
    directCourseError = true
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
      // Giữ danh sách nhân sự đã phân giải được trước đó.
    }
  }

  for (const shift of existingShifts) {
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
}
