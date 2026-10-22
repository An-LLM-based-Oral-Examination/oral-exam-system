import apiClient from './api.client'
import {
  field,
  readBoolean,
  readNullableString,
  readNumber,
  requiredBoolean,
  requiredDateString,
  requiredNumber,
  requiredString,
  requireRecord,
  unwrapPayload,
} from './mf04.response'
import type {
  ProctorSeatItem,
  ShiftAttendanceMonitor,
  SuspendExamTicketRequest,
  TicketProctorActionResponse,
  UpdateTicketAttendanceRequest,
} from '@/types/proctor.types'

function normalizeSeat(value: unknown): ProctorSeatItem {
  const record = requireRecord(value, 'Sơ đồ ghế coi thi không hợp lệ.')
  return {
    seatNumber: requiredNumber(record, 'seatNumber', 'Sơ đồ ghế thiếu số ghế.'),
    isOccupied: requiredBoolean(record, 'isOccupied', 'Sơ đồ ghế thiếu trạng thái sử dụng.'),
    ticketId: readNullableString(record, 'ticketId'),
    studentId: readNullableString(record, 'studentId'),
    studentCode: readNullableString(record, 'studentCode'),
    fullName: readNullableString(record, 'fullName'),
    ipAddress: readNullableString(record, 'ipAddress'),
    status: readNullableString(record, 'status'),
    isCheckedIn: readBoolean(record, 'isCheckedIn') ?? false,
    isSuspended: readBoolean(record, 'isSuspended') ?? false,
    suspensionReason: readNullableString(record, 'suspensionReason'),
    checkedInAt: readNullableString(record, 'checkedInAt'),
    submittedAt: readNullableString(record, 'submittedAt'),
  }
}

function normalizeAttendance(payload: unknown): ShiftAttendanceMonitor {
  const record = requireRecord(unwrapPayload(payload), 'Thông tin điểm danh ca thi không hợp lệ.')
  const seats = field(record, 'seats')
  return {
    shiftId: requiredString(record, 'shiftId', 'Thông tin điểm danh thiếu mã ca thi.'),
    shiftName: requiredString(record, 'shiftName', 'Thông tin điểm danh thiếu tên ca.'),
    roomLab: requiredString(record, 'roomLab', 'Thông tin điểm danh thiếu phòng thi.'),
    shiftStatus: requiredString(record, 'shiftStatus', 'Thông tin điểm danh thiếu trạng thái ca.'),
    startTime: requiredDateString(record, 'startTime', 'Thông tin điểm danh thiếu giờ bắt đầu.'),
    endTime: requiredDateString(record, 'endTime', 'Thông tin điểm danh thiếu giờ kết thúc.'),
    totalSeats: readNumber(record, 'totalSeats') ?? 40,
    assignedTicketsCount: readNumber(record, 'assignedTicketsCount') ?? 0,
    checkedInCount: readNumber(record, 'checkedInCount') ?? 0,
    inProgressCount: readNumber(record, 'inProgressCount') ?? 0,
    submittedCount: readNumber(record, 'submittedCount') ?? 0,
    suspendedCount: readNumber(record, 'suspendedCount') ?? 0,
    seats: Array.isArray(seats) ? seats.map(normalizeSeat) : [],
  }
}

function normalizeAction(payload: unknown): TicketProctorActionResponse {
  const record = requireRecord(unwrapPayload(payload), 'Phản hồi thao tác coi thi không hợp lệ.')
  return {
    ticketId: requiredString(record, 'ticketId', 'Phản hồi coi thi thiếu mã vé.'),
    shiftId: requiredString(record, 'shiftId', 'Phản hồi coi thi thiếu mã ca thi.'),
    studentId: requiredString(record, 'studentId', 'Phản hồi coi thi thiếu mã sinh viên.'),
    studentCode: requiredString(record, 'studentCode', 'Phản hồi coi thi thiếu mã sinh viên hiển thị.'),
    fullName: requiredString(record, 'fullName', 'Phản hồi coi thi thiếu họ tên.'),
    seatNumber: requiredNumber(record, 'seatNumber', 'Phản hồi coi thi thiếu số ghế.'),
    ipAddress: readNullableString(record, 'ipAddress'),
    status: requiredString(record, 'status', 'Phản hồi coi thi thiếu trạng thái vé.'),
    isSuspended: readBoolean(record, 'isSuspended') ?? false,
    suspensionReason: readNullableString(record, 'suspensionReason'),
    updatedAt: requiredDateString(record, 'updatedAt', 'Phản hồi coi thi thiếu thời điểm cập nhật.'),
  }
}

export const proctorService = {
  async getShiftAttendance(shiftId: string): Promise<ShiftAttendanceMonitor> {
    const response = await apiClient.get<unknown>(`/proctor/shifts/${shiftId}/attendance`)
    return normalizeAttendance(response.data)
  },

  async updateTicketAttendance(
    ticketId: string,
    payload: UpdateTicketAttendanceRequest = {},
  ): Promise<TicketProctorActionResponse> {
    const response = await apiClient.put<unknown>(`/proctor/tickets/${ticketId}/attendance`, payload)
    return normalizeAction(response.data)
  },

  async suspendTicket(
    ticketId: string,
    payload: SuspendExamTicketRequest,
  ): Promise<TicketProctorActionResponse> {
    const response = await apiClient.post<unknown>(`/proctor/tickets/${ticketId}/suspend`, payload)
    return normalizeAction(response.data)
  },
}

export type ProctorService = typeof proctorService
