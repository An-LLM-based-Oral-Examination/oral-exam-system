import apiClient from './api.client'
import {
  asArray,
  readNullableNumber,
  readNullableString,
  requiredDateString,
  requiredString,
  requireRecord,
  unwrapPayload,
} from './mf04.response'
import type {
  Appeal,
  AssignAppealLecturerRequest,
  CreateAppealRequest,
  GetAppealsParams,
  RegradeAppealRequest,
  ReviewAppealRequest,
} from '@/types/appeal.types'

function normalizeAppeal(value: unknown): Appeal {
  const record = requireRecord(value, 'Đơn phúc khảo không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Đơn phúc khảo thiếu mã đơn.'),
    ticketId: requiredString(record, 'ticketId', 'Đơn phúc khảo thiếu mã vé thi.'),
    studentId: requiredString(record, 'studentId', 'Đơn phúc khảo thiếu mã sinh viên.'),
    studentCode: requiredString(record, 'studentCode', 'Đơn phúc khảo thiếu mã sinh viên hiển thị.'),
    studentName: requiredString(record, 'studentName', 'Đơn phúc khảo thiếu tên sinh viên.'),
    sessionId: requiredString(record, 'sessionId', 'Đơn phúc khảo thiếu mã kỳ thi.'),
    sessionTitle: requiredString(record, 'sessionTitle', 'Đơn phúc khảo thiếu tên kỳ thi.'),
    submissionId: readNullableString(record, 'submissionId'),
    reason: requiredString(record, 'reason', 'Đơn phúc khảo thiếu lý do.'),
    status: requiredString(record, 'status', 'Đơn phúc khảo thiếu trạng thái.'),
    assignedTo: readNullableString(record, 'assignedTo') ?? undefined,
    assignedToName: readNullableString(record, 'assignedToName') ?? '',
    reviewedBy: readNullableString(record, 'reviewedBy'),
    reviewedByName: readNullableString(record, 'reviewedByName'),
    decision: readNullableString(record, 'decision'),
    originalScore: readNullableNumber(record, 'originalScore'),
    proposedScore: readNullableNumber(record, 'proposedScore'),
    reviewNotes: readNullableString(record, 'reviewNotes'),
    createdAt: requiredDateString(record, 'createdAt', 'Đơn phúc khảo thiếu thời điểm tạo.'),
    updatedAt: requiredDateString(record, 'updatedAt', 'Đơn phúc khảo thiếu thời điểm cập nhật.'),
    resolvedAt: readNullableString(record, 'resolvedAt'),
  }
}

export const appealsService = {
  async create(payload: CreateAppealRequest): Promise<Appeal> {
    const response = await apiClient.post<unknown>('/appeals', payload)
    return normalizeAppeal(response.data)
  },

  async list(params: GetAppealsParams = {}): Promise<Appeal[]> {
    const response = await apiClient.get<unknown>('/appeals', { params })
    return asArray(response.data, 'API phúc khảo không trả về danh sách đơn.').map(normalizeAppeal)
  },

  async getById(id: string): Promise<Appeal> {
    const response = await apiClient.get<unknown>(`/appeals/${id}`)
    return normalizeAppeal(unwrapPayload(response.data))
  },

  async review(id: string, payload: ReviewAppealRequest): Promise<Appeal> {
    const response = await apiClient.put<unknown>(`/appeals/${id}/review`, payload)
    return normalizeAppeal(response.data)
  },

  async assignLecturer(id: string, payload: AssignAppealLecturerRequest): Promise<Appeal> {
    const response = await apiClient.put<unknown>(`/appeals/${id}/assign-lecturer`, payload)
    return normalizeAppeal(response.data)
  },

  async regrade(id: string, payload: RegradeAppealRequest): Promise<Appeal> {
    const response = await apiClient.put<unknown>(`/appeals/${id}/regrade`, payload)
    return normalizeAppeal(response.data)
  },
}

export type AppealsService = typeof appealsService
