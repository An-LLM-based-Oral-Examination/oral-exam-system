import apiClient from './api.client'
import {
  asArray,
  field,
  readBoolean,
  readNullableNumber,
  readNullableString,
  readNumber,
  requiredDateString,
  requiredString,
  requireRecord,
  unwrapPayload,
} from './mf04.response'
import type {
  Appeal,
  AppealAllocationProposal,
  AppealAssignmentProposalItem,
  AssignAppealLecturerRequest,
  BatchAssignAppealsRequest,
  BatchAssignAppealsResponse,
  CreateAppealRequest,
  GetAppealsParams,
  LecturerAllocationSummary,
  RegradeAppealRequest,
  ReviewAppealRequest,
} from '@/types/appeal.types'

function normalizeAppeal(value: unknown): Appeal {
  const record = requireRecord(value, 'Đơn phúc khảo không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'Đơn phúc khảo thiếu mã đơn.'),
    ticketId: requiredString(record, 'ticketId', 'Đơn phúc khảo thiếu mã vé thi.'),
    studentId: requiredString(record, 'studentId', 'Đơn phúc khảo thiếu mã sinh viên.'),
    studentCode: requiredString(
      record,
      'studentCode',
      'Đơn phúc khảo thiếu mã sinh viên hiển thị.',
    ),
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

function normalizeLecturerSummary(value: unknown): LecturerAllocationSummary {
  const record = requireRecord(value, 'Thông tin phân bổ giảng viên không hợp lệ.')
  return {
    lecturerId: requiredString(record, 'lecturerId', 'Thiếu mã giảng viên.'),
    lecturerName: readNullableString(record, 'lecturerName') ?? 'Giảng viên',
    email: readNullableString(record, 'email') ?? '',
    assignedCount: readNumber(record, 'assignedCount') ?? 0,
  }
}

function normalizeProposalItem(value: unknown): AppealAssignmentProposalItem {
  const record = requireRecord(value, 'Đề xuất phân bổ đơn phúc khảo không hợp lệ.')
  return {
    appealId: requiredString(record, 'appealId', 'Thiếu mã đơn phúc khảo.'),
    ticketId: requiredString(record, 'ticketId', 'Thiếu mã vé thi.'),
    studentCode: readNullableString(record, 'studentCode') ?? '',
    studentName: readNullableString(record, 'studentName') ?? '',
    sessionId: readNullableString(record, 'sessionId') ?? '',
    sessionTitle: readNullableString(record, 'sessionTitle') ?? '',
    initialExaminerUserId: readNullableString(record, 'initialExaminerUserId'),
    initialExaminerName: readNullableString(record, 'initialExaminerName'),
    proposedLecturerId: readNullableString(record, 'proposedLecturerId'),
    proposedLecturerName: readNullableString(record, 'proposedLecturerName'),
    originalScore: readNullableNumber(record, 'originalScore'),
    reason: readNullableString(record, 'reason') ?? '',
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
    const response = await apiClient.post<unknown>(
      `/examination-officer/appeals/${id}/assign-lecturer`,
      payload,
    )
    return normalizeAppeal(response.data)
  },

  async getAllocationProposal(sessionId?: string): Promise<AppealAllocationProposal> {
    const response = await apiClient.get<unknown>(
      '/examination-officer/appeals/propose-allocation',
      {
        params: sessionId ? { sessionId } : undefined,
      },
    )
    const record = requireRecord(unwrapPayload(response.data), 'Đề xuất phân bổ không hợp lệ.')
    const summaries = field(record, 'lecturerSummaries')
    const items = field(record, 'items')
    return {
      totalAppeals: readNumber(record, 'totalAppeals') ?? 0,
      totalLecturers: readNumber(record, 'totalLecturers') ?? 0,
      exceedsStandardQuota: readBoolean(record, 'exceedsStandardQuota') ?? false,
      lecturerSummaries: Array.isArray(summaries) ? summaries.map(normalizeLecturerSummary) : [],
      items: Array.isArray(items) ? items.map(normalizeProposalItem) : [],
    }
  },

  async batchAssign(payload: BatchAssignAppealsRequest): Promise<BatchAssignAppealsResponse> {
    const response = await apiClient.post<unknown>(
      '/examination-officer/appeals/batch-assign',
      payload,
    )
    const record = requireRecord(
      unwrapPayload(response.data),
      'Phản hồi phân công đồng loạt không hợp lệ.',
    )
    const assigned = field(record, 'assignedAppeals')
    const totalAssigned = readNumber(record, 'totalAssigned') ?? 0
    return {
      totalAssigned,
      assignedCount: readNumber(record, 'assignedCount') ?? totalAssigned,
      failedCount: readNumber(record, 'failedCount') ?? 0,
      assignedAppeals: Array.isArray(assigned) ? assigned.map(normalizeAppeal) : [],
    }
  },

  async regrade(id: string, payload: RegradeAppealRequest): Promise<Appeal> {
    const response = await apiClient.put<unknown>(`/appeals/${id}/regrade`, payload)
    return normalizeAppeal(response.data)
  },
}

export type AppealsService = typeof appealsService
