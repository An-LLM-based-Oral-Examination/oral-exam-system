import { courseService, type ShiftStaffCandidate } from '@/services/course.service'
import type { ExamShift } from '@/types/officialExam.types'
import { DEFAULT_SHIFT_CAPACITY } from '@/lib/shiftCapacity'

export interface CreateShiftFormState {
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

export interface EditShiftFormState {
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  proctorUserId: string
  examinerUserId: string
  maxSeats: string
}

export interface CreatedShiftNotice {
  courseCode: string
  shiftName: string
  roomLab: string
  startTime: string
  endTime: string
  maxSeats: number
}

export const INITIAL_CREATE_SHIFT_FORM: CreateShiftFormState = {
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
