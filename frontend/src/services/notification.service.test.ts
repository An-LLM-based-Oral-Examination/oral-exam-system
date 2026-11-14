import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { notificationService } from './notification.service'

describe('notificationService', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('normalizes the paged notification response and metadata', async () => {
    vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: {
        items: [
          {
            Id: 'notification-1',
            UserId: 'student-1',
            Type: 'OFFICIAL_GRADE_PUBLISHED',
            Title: 'Điểm đã công bố',
            Message: 'Mở kết quả',
            Metadata: { ticketId: 'ticket-1', attempts: 2 },
            IsRead: false,
            CreatedAt: '2026-11-14T00:00:00.000Z',
          },
        ],
        unreadCount: 1,
        totalCount: 4,
        page: 1,
        pageSize: 20,
      },
    } as never)

    await expect(notificationService.list()).resolves.toMatchObject({
      unreadCount: 1,
      totalCount: 4,
      items: [
        expect.objectContaining({
          id: 'notification-1',
          userId: 'student-1',
          metadata: { ticketId: 'ticket-1', attempts: '2' },
        }),
      ],
    })
  })

  it('uses the ownership-scoped read endpoints', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({ data: {} } as never)

    await notificationService.markRead('notification/1')
    await notificationService.markAllRead()

    expect(post).toHaveBeenNthCalledWith(1, '/notifications/notification%2F1/read')
    expect(post).toHaveBeenNthCalledWith(2, '/notifications/read-all')
  })
})
