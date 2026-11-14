import type { StudentNotification } from '@/types/notification.types'

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

function isGuid(value: string | null | undefined): value is string {
  return value != null && GUID_PATTERN.test(value)
}
function readMetadata(notification: StudentNotification, key: string): string | null {
  const value =
    notification.metadata[key] ?? notification.metadata[key.charAt(0).toUpperCase() + key.slice(1)]
  return typeof value === 'string' && value.trim() ? value : null
}
function ticketIdFrom(notification: StudentNotification): string | null {
  const metadataTicketId = readMetadata(notification, 'ticketId')
  if (isGuid(metadataTicketId)) return metadataTicketId

  const entityName = notification.relatedEntityName?.toLowerCase()
  if (
    (entityName === 'student_exam_ticket' || entityName === 'student_exam_tickets') &&
    isGuid(notification.relatedEntityId)
  ) {
    return notification.relatedEntityId
  }

  return null
}
export function resolveStudentNotificationRoute(notification: StudentNotification): string | null {
  const type = notification.type.toUpperCase()
  const ticketId = ticketIdFrom(notification)

  if (
    (type === 'OFFICIAL_GRADE_PUBLISHED' ||
      type === 'APPEAL_FORWARDED' ||
      type === 'APPEAL_ASSIGNED' ||
      type === 'APPEAL_REGRADED' ||
      type === 'APPEAL_DECIDED') &&
    ticketId
  ) {
    return `/results?type=official&id=${encodeURIComponent(ticketId)}`
  }

  return null
}
