import apiClient from './api.client'
import type {
  NotificationListResponse,
  NotificationMetadata,
  StudentNotification,
} from '@/types/notification.types'

type JsonRecord = Record<string, unknown>

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function field(record: JsonRecord, key: string): unknown {
  return record[key] ?? record[key.charAt(0).toUpperCase() + key.slice(1)]
}

function text(value: unknown, fallback = ''): string {
  return typeof value === 'string' && value.trim() ? value : fallback
}

function nullableText(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value : null
}

function booleanValue(value: unknown): boolean {
  return value === true || value === 'true'
}

function numberValue(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function normalizeMetadata(value: unknown): NotificationMetadata {
  if (!isRecord(value)) return {}
  const metadata: NotificationMetadata = {}
  for (const [key, rawValue] of Object.entries(value)) {
    if (typeof rawValue === 'string' || rawValue == null) {
      metadata[key] = rawValue
    } else if (typeof rawValue === 'number' || typeof rawValue === 'boolean') {
      metadata[key] = String(rawValue)
    }
  }
  return metadata
}

function normalizeNotification(value: unknown): StudentNotification {
  if (!isRecord(value)) throw new Error('API thông báo trả về dữ liệu không hợp lệ.')

  return {
    id: text(field(value, 'id')),
    userId: text(field(value, 'userId')),
    type: text(field(value, 'type'), 'UNKNOWN'),
    title: text(field(value, 'title'), 'Thông báo'),
    message: text(field(value, 'message')),
    relatedEntityName: nullableText(field(value, 'relatedEntityName')),
    relatedEntityId: nullableText(field(value, 'relatedEntityId')),
    metadata: normalizeMetadata(field(value, 'metadata')),
    isRead: booleanValue(field(value, 'isRead')),
    readAt: nullableText(field(value, 'readAt')),
    createdAt: text(field(value, 'createdAt'), new Date(0).toISOString()),
  }
}

function unwrapList(value: unknown): JsonRecord {
  if (!isRecord(value)) throw new Error('API thông báo không trả về danh sách hợp lệ.')
  const nested = field(value, 'value') ?? field(value, 'data')
  return isRecord(nested) ? nested : value
}

export const notificationService = {
  async list(page = 1, pageSize = 20): Promise<NotificationListResponse> {
    const response = await apiClient.get<unknown>('/notifications', {
      params: { page, pageSize },
    })
    const record = unwrapList(response.data)
    const rawItems = field(record, 'items')
    const items = Array.isArray(rawItems) ? rawItems.map(normalizeNotification) : []

    return {
      items,
      unreadCount: Math.max(0, numberValue(field(record, 'unreadCount'), 0)),
      totalCount: Math.max(0, numberValue(field(record, 'totalCount'), items.length)),
      page: Math.max(1, numberValue(field(record, 'page'), page)),
      pageSize: Math.max(1, numberValue(field(record, 'pageSize'), pageSize)),
    }
  },

  async markRead(notificationId: string): Promise<void> {
    await apiClient.post(`/notifications/${encodeURIComponent(notificationId)}/read`)
  },

  async markAllRead(): Promise<void> {
    await apiClient.post('/notifications/read-all')
  },
}

export type NotificationService = typeof notificationService
