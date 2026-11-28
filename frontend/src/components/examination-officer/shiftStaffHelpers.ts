import { courseService, type ShiftStaffCandidate } from '@/services/course.service'
import { officialExamService } from '@/services/officialExam.service'
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

export interface SemesterDateBounds {
  startDate?: string
  endDate?: string
}

let roomRowCounter = 1
let shiftGroupCounter = 1

function pad2(n: number): string {
  return String(n).padStart(2, '0')
}

export function getLocalTodayDate(now: Date = new Date()): string {
  return `${now.getFullYear()}-${pad2(now.getMonth() + 1)}-${pad2(now.getDate())}`
}

export function formatLocalDateTimeInput(date: Date): string {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}T${pad2(date.getHours())}:${pad2(date.getMinutes())}`
}

export function addMinutesToLocalDateTime(localDateTime: string, minutes: number): string {
  if (!localDateTime) return ''
  const parsed = new Date(localDateTime)
  if (Number.isNaN(parsed.getTime())) return ''
  return formatLocalDateTimeInput(new Date(parsed.getTime() + minutes * 60_000))
}

export function computeDurationMinutes(startTime: string, endTime: string): number | null {
  if (!startTime || !endTime) return null
  const start = new Date(startTime)
  const end = new Date(endTime)
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return null
  const diffMinutes = Math.round((end.getTime() - start.getTime()) / 60_000)
  return diffMinutes > 0 ? diffMinutes : null
}

export function shiftEndPreservingDuration(
  prevStartTime: string,
  prevEndTime: string,
  nextStartTime: string,
  fallbackDurationMinutes = 60,
): string {
  if (!nextStartTime) return ''
  const prevDuration = computeDurationMinutes(prevStartTime, prevEndTime)
  const durationMinutes =
    prevDuration && prevDuration >= 5 && prevDuration <= 720 ? prevDuration : fallbackDurationMinutes
  return addMinutesToLocalDateTime(nextStartTime, durationMinutes)
}

export function getDefaultShiftWindow(
  semester?: SemesterDateBounds,
  durationMinutes = 60,
  now: Date = new Date(),
): { startTime: string; endTime: string } {
  const todayLocal = getLocalTodayDate(now)
  const semStart = semester?.startDate?.slice(0, 10)
  const semEnd = semester?.endDate?.slice(0, 10)

  if (semStart && todayLocal < semStart) {
    const startTime = `${semStart}T08:00`
    return { startTime, endTime: addMinutesToLocalDateTime(startTime, durationMinutes) }
  }
  if (semEnd && todayLocal > semEnd) {
    const startTime = `${semEnd}T08:00`
    return { startTime, endTime: addMinutesToLocalDateTime(startTime, durationMinutes) }
  }

  // Làm tròn lên mốc 15 phút gần nhất theo giờ địa phương (GMT+7 trên máy VN)
  const rounded = new Date(now.getTime())
  rounded.setSeconds(0, 0)
  const remainder = rounded.getMinutes() % 15
  if (remainder > 0) {
    rounded.setMinutes(rounded.getMinutes() + (15 - remainder))
  } else {
    rounded.setMinutes(rounded.getMinutes() + 15)
  }

  const roundedYmd = getLocalTodayDate(rounded)
  if (semEnd && roundedYmd > semEnd) {
    const startTime = `${semEnd}T08:00`
    return { startTime, endTime: addMinutesToLocalDateTime(startTime, durationMinutes) }
  }

  const startTime = formatLocalDateTimeInput(rounded)
  return {
    startTime,
    endTime: addMinutesToLocalDateTime(startTime, durationMinutes),
  }
}

export function formatShiftTimeSummaryVi(startTime: string, endTime: string): string {
  if (!startTime) return ''
  const start = new Date(startTime)
  if (Number.isNaN(start.getTime())) return ''

  const weekdayFormatter = new Intl.DateTimeFormat('vi-VN', { weekday: 'short' })
  const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
  const clockFormatter = new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })

  const startLabel = `${weekdayFormatter.format(start)}, ${dateFormatter.format(start)} · ${clockFormatter.format(start)}`
  if (!endTime) return startLabel

  const end = new Date(endTime)
  if (Number.isNaN(end.getTime())) return startLabel

  const sameDay =
    start.getFullYear() === end.getFullYear() &&
    start.getMonth() === end.getMonth() &&
    start.getDate() === end.getDate()
  const endLabel = sameDay
    ? clockFormatter.format(end)
    : `${dateFormatter.format(end)} ${clockFormatter.format(end)}`

  const duration = computeDurationMinutes(startTime, endTime)
  return duration ? `${startLabel} → ${endLabel} (${duration} phút)` : `${startLabel} → ${endLabel}`
}

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
  assignHandedOverExamSets: boolean
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

export function createInitialShiftForm(semester?: SemesterDateBounds): CreateShiftFormState {
  const defaultWindow = getDefaultShiftWindow(semester)
  const firstShift = createShiftGroupRow({
    shiftName: 'Ca 1',
    startTime: defaultWindow.startTime,
    endTime: defaultWindow.endTime,
  })
  return {
    semesterId: '',
    examAttemptType: 'FE',
    examBlock: 'B10W',
    courseId: '',
    sessionId: '',
    activeShiftId: firstShift.id,
    assignHandedOverExamSets: true,
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
  return formatLocalDateTimeInput(parsed)
}

export async function resolveShiftStaffPools(
  courseId: string,
  semesterId: string,
  existingShifts: ExamShift[],
  academicProgramId?: string | null,
): Promise<{ proctors: ShiftStaffCandidate[]; examiners: ShiftStaffCandidate[] }> {
  // Nguồn sự thật duy nhất từ Backend: GET /api/v1/official-exams/shift-staff
  // - Proctors: chỉ tài khoản đang hoạt động có vai trò Giám thị (proctor)
  // - Examiners: ưu tiên Giảng viên phụ trách đúng môn & Trưởng BM của môn đó
  if (typeof officialExamService.getShiftStaff === 'function') {
    const staffResponse = await officialExamService.getShiftStaff({
      courseId: courseId || undefined,
      semesterId: semesterId || undefined,
    })

    return {
      proctors: staffResponse.proctors.map((member) => ({
        userId: member.userId,
        fullName: member.fullName,
        detail: member.detail || member.email || undefined,
      })),
      examiners: staffResponse.examiners.map((member) => ({
        userId: member.userId,
        fullName: member.fullName,
        detail: member.detail || member.email || undefined,
      })),
    }
  }

  // Fallback tối giản cho các test harness cũ chưa mock officialExamService.getShiftStaff
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
    if (shift.examinerUserId && shift.examinerName && !examinerMap.has(shift.examinerUserId)) {
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
