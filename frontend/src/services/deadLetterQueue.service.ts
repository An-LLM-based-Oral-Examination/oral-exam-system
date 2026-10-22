import apiClient from './api.client'
import { asArray, readNullableString, requiredDateString, requiredNumber, requiredString, requireRecord, unwrapPayload } from './mf04.response'
import type { DeadLetterQueueTask, ReplayDeadLetterQueueResponse } from '@/types/deadLetterQueue.types'

function normalizeTask(value: unknown): DeadLetterQueueTask {
  const record = requireRecord(value, 'DLQ task khÃ´ng há»£p lá»‡.')
  return {
    id: requiredString(record, 'id', 'DLQ task thiáº¿u mÃ£.'),
    taskType: requiredString(record, 'taskType', 'DLQ task thiáº¿u loáº¡i task.'),
    payloadJson: requiredString(record, 'payloadJson', 'DLQ task thiáº¿u payload.'),
    errorMessage: requiredString(record, 'errorMessage', 'DLQ task thiáº¿u thÃ´ng bÃ¡o lá»—i.'),
    retryCount: requiredNumber(record, 'retryCount', 'DLQ task thiáº¿u sá»‘ láº§n retry.'),
    status: requiredString(record, 'status', 'DLQ task thiáº¿u tráº¡ng thÃ¡i.'),
    createdAt: requiredDateString(record, 'createdAt', 'DLQ task thiáº¿u thá»i Ä‘iá»ƒm táº¡o.'),
    resolvedAt: readNullableString(record, 'resolvedAt'),
  }
}

export const deadLetterQueueService = {
  async list(status?: string): Promise<DeadLetterQueueTask[]> {
    const response = await apiClient.get<unknown>('/dlq/tasks', { params: status ? { status } : undefined })
    return asArray(response.data, 'API DLQ khÃ´ng tráº£ vá» danh sÃ¡ch task.').map(normalizeTask)
  },

  async replay(id: string): Promise<ReplayDeadLetterQueueResponse> {
    const response = await apiClient.post<unknown>(`/dlq/tasks/${id}/replay`)
    const record = requireRecord(unwrapPayload(response.data), 'Pháº£n há»“i replay DLQ khÃ´ng há»£p lá»‡.')
    return {
      id: requiredString(record, 'id', 'Pháº£n há»“i replay thiáº¿u mÃ£ task.'),
      dlqId: requiredString(record, 'dlqId', 'Pháº£n há»“i replay thiáº¿u mÃ£ DLQ.'),
      status: requiredString(record, 'status', 'Pháº£n há»“i replay thiáº¿u tráº¡ng thÃ¡i.'),
      message: requiredString(record, 'message', 'Pháº£n há»“i replay thiáº¿u thÃ´ng bÃ¡o.'),
    }
  },
}

