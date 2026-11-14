export type NotificationMetadata = Record<string, string | null | undefined>

export type StudentNotificationType =
  | 'OFFICIAL_GRADE_PUBLISHED'
  | 'APPEAL_SUBMITTED'
  | 'APPEAL_FORWARDED'
  | 'APPEAL_ASSIGNED'
  | 'APPEAL_REGRADED'
  | 'APPEAL_DECIDED'
  | string

export interface StudentNotification {
  id: string
  userId: string
  type: StudentNotificationType
  title: string
  message: string
  relatedEntityName: string | null
  relatedEntityId: string | null
  metadata: NotificationMetadata
  isRead: boolean
  readAt: string | null
  createdAt: string
}
export interface NotificationListResponse {
  items: StudentNotification[]
  unreadCount: number
  totalCount: number
  page: number
  pageSize: number
}
