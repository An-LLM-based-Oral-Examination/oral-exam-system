import apiClient from './api.client'
import { asArray, readNullableString, requiredDateString, requiredNumber, requiredString, requireRecord, unwrapPayload } from './mf04.response'
import type { DeadLetterQueueTask, ReplayDeadLetterQueueResponse } from '@/types/deadLetterQueue.types'

function normalizeTask(value: unknown): DeadLetterQueueTask {
  const record = requireRecord(value, 'DLQ task không hợp lệ.')
  return {
    id: requiredString(record, 'id', 'DLQ task thiếu mã.'),
    taskType: requiredString(record, 'taskType', 'DLQ task thiếu loại task.'),
    payloadJson: requiredString(record, 'payloadJson', 'DLQ task thiếu payload.'),
    errorMessage: requiredString(record, 'errorMessage', 'DLQ task thiếu thông báo lỗi.'),
    retryCount: requiredNumber(record, 'retryCount', 'DLQ task thiếu số lần retry.'),
    status: requiredString(record, 'status', 'DLQ task thiếu trạng thái.'),
    createdAt: requiredDateString(record, 'createdAt', 'DLQ task thiếu thời điểm tạo.'),
    resolvedAt: readNullableString(record, 'resolvedAt'),
  }
}

export const deadLetterQueueService = {
  async list(status?: string): Promise<DeadLetterQueueTask[]> {
    const response = await apiClient.get<unknown>('/dlq/tasks', { params: status ? { status } : undefined })
    return asArray(response.data, 'API DLQ không trả về danh sách task.').map(normalizeTask)
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
}
