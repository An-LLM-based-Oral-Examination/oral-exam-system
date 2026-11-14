import apiClient from './api.client'
import {
  asArray,
  readNullableString,
  requiredDateString,
  requiredNumber,
  requiredString,
  requireRecord,
  unwrapPayload,
} from './mf04.response'
import type {
  DeadLetterQueueTask,
  ReplayDeadLetterQueueResponse,
  ResolveDeadLetterQueueResponse,
} from '@/types/deadLetterQueue.types'

function normalizeTask(value: unknown): DeadLetterQueueTask {
  const record = requireRecord(value, 'DLQ task không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'DLQ task thiếu mã.'),
    taskType: requiredString(record, 'taskType', 'DLQ task thiếu loại task.'),
    flowType: readNullableString(record, 'flowType'),
    managedByRole: readNullableString(record, 'managedByRole'),
    payloadJson: requiredString(record, 'payloadJson', 'DLQ task thiếu payload.'),
    errorMessage: requiredString(record, 'errorMessage', 'DLQ task thiếu thông báo lỗi.'),
    retryCount: requiredNumber(record, 'retryCount', 'DLQ task thiếu số lần retry.'),
    status: requiredString(record, 'status', 'DLQ task thiếu trạng thái.'),
    createdAt: requiredDateString(record, 'createdAt', 'DLQ task thiếu thời điểm tạo.'),
    resolvedAt: readNullableString(record, 'resolvedAt'),
  }
}

export const deadLetterQueueService = {
  async list(status?: string, flowType?: string): Promise<DeadLetterQueueTask[]> {
    const params: Record<string, string> = {}
    if (status) params.status = status
    if (flowType && flowType !== 'ALL') params.flowType = flowType
    const response = await apiClient.get<unknown>('/dlq/tasks', { params })
    return asArray(response.data, 'API DLQ không trả về danh sách task.').map(normalizeTask)
  },

  async listForExaminationOfficer(status?: string): Promise<DeadLetterQueueTask[]> {
    const response = await apiClient.get<unknown>('/examination-officer/dlq/tasks', {
      params: status ? { status } : undefined,
    })
    return asArray(response.data, 'API DLQ Khảo thí không trả về danh sách task.').map(
      normalizeTask,
    )
  },

  async replay(id: string): Promise<ReplayDeadLetterQueueResponse> {
    const response = await apiClient.post<unknown>(`/dlq/tasks/${id}/replay`)
    const record = requireRecord(unwrapPayload(response.data), 'Phản hồi replay DLQ không hợp lệ.')
    return {
      id: requiredString(record, 'id', 'Phản hồi replay thiếu mã task.'),
      dlqId: requiredString(record, 'dlqId', 'Phản hồi replay thiếu mã DLQ.'),
      status: requiredString(record, 'status', 'Phản hồi replay thiếu trạng thái.'),
      message: requiredString(record, 'message', 'Phản hồi replay thiếu thông báo.'),
    }
  },

  async replayForExaminationOfficer(id: string): Promise<ReplayDeadLetterQueueResponse> {
    const response = await apiClient.post<unknown>(`/examination-officer/dlq/tasks/${id}/replay`)
    const record = requireRecord(
      unwrapPayload(response.data),
      'Phản hồi replay DLQ Khảo thí không hợp lệ.',
    )
    return {
      id: requiredString(record, 'id', 'Phản hồi replay thiếu mã task.'),
      dlqId: requiredString(record, 'dlqId', 'Phản hồi replay thiếu mã DLQ.'),
      status: requiredString(record, 'status', 'Phản hồi replay thiếu trạng thái.'),
      message: requiredString(record, 'message', 'Phản hồi replay thiếu thông báo.'),
    }
  },

  async resolve(id: string, resolutionNotes?: string): Promise<ResolveDeadLetterQueueResponse> {
    const response = await apiClient.post<unknown>(`/dlq/tasks/${id}/resolve`, {
      resolutionNotes: resolutionNotes?.trim() || undefined,
    })
    const record = requireRecord(unwrapPayload(response.data), 'Phản hồi xử lý DLQ không hợp lệ.')
    return {
      id: requiredString(record, 'id', 'Phản hồi xử lý DLQ thiếu mã task.'),
      dlqId: requiredString(record, 'dlqId', 'Phản hồi xử lý DLQ thiếu mã DLQ.'),
      status: requiredString(record, 'status', 'Phản hồi xử lý DLQ thiếu trạng thái.'),
      message: requiredString(record, 'message', 'Phản hồi xử lý DLQ thiếu thông báo.'),
    }
  },

  async resolveForExaminationOfficer(
    id: string,
    resolutionNotes?: string,
  ): Promise<ResolveDeadLetterQueueResponse> {
    const response = await apiClient.post<unknown>(`/examination-officer/dlq/tasks/${id}/resolve`, {
      resolutionNotes: resolutionNotes?.trim() || undefined,
    })
    const record = requireRecord(
      unwrapPayload(response.data),
      'Phản hồi xử lý DLQ Khảo thí không hợp lệ.',
    )
    return {
      id: requiredString(record, 'id', 'Phản hồi xử lý DLQ thiếu mã task.'),
      dlqId: requiredString(record, 'dlqId', 'Phản hồi xử lý DLQ thiếu mã DLQ.'),
      status: requiredString(record, 'status', 'Phản hồi xử lý DLQ thiếu trạng thái.'),
      message: requiredString(record, 'message', 'Phản hồi xử lý DLQ thiếu thông báo.'),
    }
  },
}
