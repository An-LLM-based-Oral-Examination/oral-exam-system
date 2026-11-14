import { describe, expect, it } from 'vitest'
import { resolveStudentNotificationRoute } from './notificationDeepLink'
import type { StudentNotification } from '@/types/notification.types'

function notification(overrides: Partial<StudentNotification> = {}): StudentNotification {
  return {
    id: '00000000-0000-4000-8000-000000000001',
    userId: '00000000-0000-4000-8000-000000000002',
    type: 'OFFICIAL_GRADE_PUBLISHED',
    title: 'Điểm đã công bố',
    message: 'Mở kết quả',
    relatedEntityName: 'student_exam_ticket',
    relatedEntityId: '00000000-0000-4000-8000-000000000003',
    metadata: {},
    isRead: false,
    readAt: null,
    createdAt: '2026-11-14T00:00:00.000Z',
    ...overrides,
  }
}

describe('resolveStudentNotificationRoute', () => {
  it('creates an official result route from a ticket notification', () => {
    expect(resolveStudentNotificationRoute(notification())).toBe(
      '/results?type=official&id=00000000-0000-4000-8000-000000000003',
    )
  })

  it('uses the allowlisted metadata ticket id for appeal notifications', () => {
    expect(
      resolveStudentNotificationRoute(
        notification({
          type: 'APPEAL_DECIDED',
          relatedEntityName: 'appeal_request',
          relatedEntityId: '00000000-0000-4000-8000-000000000004',
          metadata: { ticketId: '00000000-0000-4000-8000-000000000005' },
        }),
      ),
    ).toBe('/results?type=official&id=00000000-0000-4000-8000-000000000005')
  })

  it('does not follow an arbitrary external URL or an invalid id', () => {
    expect(
      resolveStudentNotificationRoute(
        notification({
          relatedEntityName: null,
          relatedEntityId: null,
          metadata: { url: 'https://evil.example/steal-session', ticketId: 'not-a-guid' },
        }),
      ),
    ).toBeNull()
  })
})
