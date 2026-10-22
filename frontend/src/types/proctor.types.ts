export interface ProctorSeatItem {
  seatNumber: number
  isOccupied: boolean
  ticketId: string | null
  studentId: string | null
  studentCode: string | null
  fullName: string | null
  ipAddress: string | null
  status: string | null
  isCheckedIn: boolean
  isSuspended: boolean
  suspensionReason: string | null
  checkedInAt: string | null
  submittedAt: string | null
}

export interface ShiftAttendanceMonitor {
  shiftId: string
  shiftName: string
  roomLab: string
  shiftStatus: string
  startTime: string
  endTime: string
  totalSeats: number
  assignedTicketsCount: number
  checkedInCount: number
  inProgressCount: number
  submittedCount: number
  suspendedCount: number
  seats: ProctorSeatItem[]
}

export interface UpdateTicketAttendanceRequest {
  isPresent?: boolean
  ipAddress?: string | null
  note?: string | null
}

export interface SuspendExamTicketRequest {
  reason: string
  blurViolationCount?: number | null
}

export interface TicketProctorActionResponse {
  ticketId: string
  shiftId: string
  studentId: string
  studentCode: string
  fullName: string
  seatNumber: number
  ipAddress: string | null
  status: string
  isSuspended: boolean
  suspensionReason: string | null
  updatedAt: string
}
