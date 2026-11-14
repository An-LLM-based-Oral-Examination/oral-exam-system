import { test, expect, type Page } from '@playwright/test'

const ticketId = '00000000-0000-4000-8000-000000000003'
const notificationId = '00000000-0000-4000-8000-000000000004'

async function seedStudent(page: Page) {
  await page.addInitScript(() => {
    sessionStorage.setItem('access_token', 'e2e-access-token')
    sessionStorage.setItem('refresh_token', 'e2e-refresh-token')
    sessionStorage.setItem(
      'user_profile',
      JSON.stringify({
        id: '00000000-0000-4000-8000-000000000002',
        email: 'student@e2e.test',
        fullName: 'E2E Student',
        studentCode: 'SE170001',
        role: 'Student',
        mustChangePassword: false,
      }),
    )
  })
}

test.describe('Student notifications', () => {
  test('shows unread badge, marks a notification read, and follows a safe deep-link', async ({
    page,
  }) => {
    await page.route('**/api/v1/notifications**', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: notificationId,
                userId: '00000000-0000-4000-8000-000000000002',
                type: 'OFFICIAL_GRADE_PUBLISHED',
                title: 'Điểm thi chính thức đã được công bố',
                message: 'Mở kết quả để xem chi tiết.',
                relatedEntityName: 'student_exam_ticket',
                relatedEntityId: ticketId,
                metadata: { ticketId },
                isRead: false,
                readAt: null,
                createdAt: '2026-11-14T00:00:00.000Z',
              },
            ],
            unreadCount: 1,
            totalCount: 1,
            page: 1,
            pageSize: 20,
          }),
        })
        return
      }

      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
    })

    await seedStudent(page)
    await page.goto('/student/overview')
    await page.getByRole('button', { name: 'Thông báo', exact: true }).click()

    await expect(page.getByText('Điểm thi chính thức đã được công bố')).toBeVisible()
    await expect(page.getByText('1', { exact: true }).first()).toBeVisible()

    await page.getByRole('button', { name: /Điểm thi chính thức đã được công bố/ }).click()
    await expect(page).toHaveURL(new RegExp(`/results\\?type=official&id=${ticketId}`))
  })
})
