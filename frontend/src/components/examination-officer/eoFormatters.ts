import { ds } from '@/design/system'

export interface StatusMeta {
  label: string
  chipClass: string
}

export function formatEoDateTime(value?: string | null): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date)
}

export function formatEoDate(value?: string | null): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
}

export function formatEoTimeRange(startTime?: string | null, endTime?: string | null): string {
  if (!startTime) return '—'
  const start = new Date(startTime)
  if (Number.isNaN(start.getTime())) return startTime
  const datePart = new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(start)
  const startClock = new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(start)
  if (!endTime) return `${datePart} · ${startClock}`
  const end = new Date(endTime)
  if (Number.isNaN(end.getTime())) return `${datePart} · ${startClock}`
  const endClock = new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(end)
  return `${datePart} · ${startClock}–${endClock}`
}

export function getShiftStatusMeta(status?: string | null): StatusMeta {
  switch ((status ?? '').trim().toLowerCase()) {
    case 'in_progress':
      return {
        label: 'Đang diễn ra',
        chipClass:
          'inline-flex items-center rounded-full border border-[#edca8c] bg-[#fff8e7] px-2.5 py-0.5 text-xs font-bold text-[#946200]',
      }
    case 'completed':
      return { label: 'Đã kết thúc', chipClass: ds.successChip }
    case 'cancelled':
      return { label: 'Đã hủy', chipClass: ds.dangerChip }
    case 'scheduled':
    default:
      return { label: 'Đã lên lịch', chipClass: ds.infoChip }
  }
}

export function getTicketStatusMeta(status?: string | null): StatusMeta {
  switch ((status ?? '').trim().toUpperCase()) {
    case 'IN_PROGRESS':
      return {
        label: 'Đang thi',
        chipClass:
          'inline-flex items-center rounded-full border border-[#edca8c] bg-[#fff8e7] px-2 py-0.5 text-[11px] font-bold text-[#946200]',
      }
    case 'SUBMITTED':
      return { label: 'Đã nộp', chipClass: ds.infoChip }
    case 'AI_GRADED':
      return { label: 'Đã chấm AI', chipClass: ds.infoChip }
    case 'AUDITED':
      return { label: 'Đã hậu kiểm', chipClass: ds.successChip }
    case 'PUBLISHED':
      return { label: 'Đã công bố', chipClass: ds.successChip }
    case 'LOCKED':
      return { label: 'Đình chỉ', chipClass: ds.dangerChip }
    case 'SCHEDULED':
    default:
      return { label: 'Chưa vào thi', chipClass: ds.infoChip }
  }
}

export function getExamSetStatusLabel(status?: string | null): string {
  switch ((status ?? '').trim().toUpperCase()) {
    case 'HANDED_OVER':
      return 'Đã bàn giao'
    case 'OFFICIAL':
      return 'Chính thức'
    default:
      return status || '—'
  }
}

export interface ShiftPlanItem {
  label: string
  shiftCount: number
  isLastShift: boolean
  studentsPerShift: number
  roomsCount: number
  minStudentsPerRoom: number
  maxStudentsPerRoom: number
  roomCapacities: number[]
  roomDistributionText: string
}

export interface ShiftRoomRecommendation {
  totalStudents: number
  standardStudentsPerRoom: number
  recommendedShifts: number
  totalRooms: number
  minRoomsPerShift: number
  maxRoomsPerShift: number
  roomsPerShiftText: string
  suggestedMaxSeats: number
  shifts: ShiftPlanItem[]
}

export const STANDARD_STUDENTS_PER_ROOM = 24
const MAX_ROOMS_PER_SHIFT = 3

function formatRoomSplit(students: number, rooms: number): {
  roomsCount: number
  minStudentsPerRoom: number
  maxStudentsPerRoom: number
  roomCapacities: number[]
  roomDistributionText: string
} {
  const activeRooms = Math.max(1, Math.min(rooms, students))
  const minStudentsPerRoom = Math.floor(students / activeRooms)
  const maxStudentsPerRoom = Math.ceil(students / activeRooms)
  const roomsWithMax = students % activeRooms
  const roomsWithMin = activeRooms - roomsWithMax

  const roomCapacities = Array.from({ length: activeRooms }, (_, idx) =>
    idx < roomsWithMax ? maxStudentsPerRoom : minStudentsPerRoom,
  )

  const roomDistributionText =
    minStudentsPerRoom === maxStudentsPerRoom
      ? `${activeRooms} phòng × ${maxStudentsPerRoom} SV`
      : `${roomsWithMax} phòng × ${maxStudentsPerRoom} SV & ${roomsWithMin} phòng × ${minStudentsPerRoom} SV`

  return {
    roomsCount: activeRooms,
    minStudentsPerRoom,
    maxStudentsPerRoom,
    roomCapacities,
    roomDistributionText,
  }
}

/**
 * Đề xuất chia ca và phòng thi theo trần cứng chuẩn 24 SV/phòng (khớp 100% với auto-allocate Backend):
 * 1. Tổng số phòng = ceil(tổng SV / 24).
 * 2. Chia đều tổng số phòng vào các ca (tối đa 3 phòng/ca, ví dụ 5 phòng -> Ca 1: 3 phòng, Ca 2: 2 phòng).
 * 3. Các ca trước ca cuối xếp đủ 24 SV/phòng; số sinh viên còn lại ở ca cuối chia đều cho tất cả
 *    các phòng của ca cuối (chênh lệch <= 1 SV/phòng).
 */
export function buildShiftRoomRecommendation(totalStudents: number): ShiftRoomRecommendation {
  const safeStudents = Math.max(0, Math.floor(Number.isFinite(totalStudents) ? totalStudents : 0))

  if (safeStudents === 0) {
    return {
      totalStudents: 0,
      standardStudentsPerRoom: STANDARD_STUDENTS_PER_ROOM,
      recommendedShifts: 0,
      totalRooms: 0,
      minRoomsPerShift: 0,
      maxRoomsPerShift: 0,
      roomsPerShiftText: '0 phòng/ca',
      suggestedMaxSeats: STANDARD_STUDENTS_PER_ROOM,
      shifts: [],
    }
  }

  const totalRooms = Math.ceil(safeStudents / STANDARD_STUDENTS_PER_ROOM)
  const recommendedShifts = Math.ceil(totalRooms / MAX_ROOMS_PER_SHIFT)
  const baseRoomsPerShift = Math.floor(totalRooms / recommendedShifts)
  const shiftsWithExtraRoom = totalRooms % recommendedShifts

  const roomsPerShiftPlan: number[] = Array.from({ length: recommendedShifts }, (_, index) =>
    index < shiftsWithExtraRoom ? baseRoomsPerShift + 1 : baseRoomsPerShift,
  )

  const minRoomsPerShift = baseRoomsPerShift
  const maxRoomsPerShift = shiftsWithExtraRoom > 0 ? baseRoomsPerShift + 1 : baseRoomsPerShift
  const roomsPerShiftText =
    minRoomsPerShift === maxRoomsPerShift
      ? `${maxRoomsPerShift} phòng/ca`
      : `${minRoomsPerShift}–${maxRoomsPerShift} phòng/ca`

  const rawShifts: ShiftPlanItem[] = []
  let remainingStudents = safeStudents

  roomsPerShiftPlan.forEach((roomsInShift, idx) => {
    const isLast = idx === recommendedShifts - 1
    const studentsInShift = isLast
      ? remainingStudents
      : Math.min(remainingStudents, roomsInShift * STANDARD_STUDENTS_PER_ROOM)
    remainingStudents -= studentsInShift

    const split = formatRoomSplit(studentsInShift, roomsInShift)

    rawShifts.push({
      label:
        recommendedShifts === 1
          ? 'Ca 1 (ca duy nhất)'
          : isLast
            ? `Ca ${idx + 1} (ca cuối)`
            : `Ca ${idx + 1}`,
      shiftCount: 1,
      isLastShift: isLast,
      studentsPerShift: studentsInShift,
      ...split,
    })
  })

  // Gom các ca đầu giống hệt nhau nếu có >= 3 ca để giao diện gọn gàng
  const shifts: ShiftPlanItem[] = []
  if (rawShifts.length >= 3) {
    const first = rawShifts[0]
    let samePrefixCount = 1
    while (
      samePrefixCount < rawShifts.length - 1 &&
      rawShifts[samePrefixCount].studentsPerShift === first.studentsPerShift &&
      rawShifts[samePrefixCount].roomDistributionText === first.roomDistributionText
    ) {
      samePrefixCount += 1
    }

    if (samePrefixCount >= 2) {
      shifts.push({
        ...first,
        label: `Ca 1 – Ca ${samePrefixCount}`,
        shiftCount: samePrefixCount,
      })
      shifts.push(...rawShifts.slice(samePrefixCount))
    } else {
      shifts.push(...rawShifts)
    }
  } else {
    shifts.push(...rawShifts)
  }

  return {
    totalStudents: safeStudents,
    standardStudentsPerRoom: STANDARD_STUDENTS_PER_ROOM,
    recommendedShifts,
    totalRooms,
    minRoomsPerShift,
    maxRoomsPerShift,
    roomsPerShiftText,
    suggestedMaxSeats: STANDARD_STUDENTS_PER_ROOM,
    shifts,
  }
}
