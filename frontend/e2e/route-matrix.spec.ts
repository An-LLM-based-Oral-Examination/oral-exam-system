import { test, expect, type Page } from '@playwright/test'

type E2eRole =
  | 'Student'
  | 'Instructor'
  | 'DepartmentHead'
  | 'ExaminationOfficer'
  | 'Proctor'
  | 'Admin'

const forbiddenRouteCases: Array<{ role: E2eRole; path: string }> = [
  { role: 'Instructor', path: '/admin/dashboard' },
  { role: 'DepartmentHead', path: '/lecturer/dashboard' },
  { role: 'ExaminationOfficer', path: '/admin/dashboard' },
  { role: 'Admin', path: '/examination-officer/dashboard' },
  { role: 'DepartmentHead', path: '/question-bank/create' },
  { role: 'Instructor', path: '/official-exams/manage' },
  { role: 'Admin', path: '/appeals' },
  { role: 'DepartmentHead', path: '/dlq' },
  { role: 'Instructor', path: '/department-head/dashboard' },
  { role: 'Instructor', path: '/department-head/question-review' },
  { role: 'Instructor', path: '/department-head/appeals' },
  { role: 'DepartmentHead', path: '/admin/dashboard' },
  { role: 'ExaminationOfficer', path: '/lecturer/dashboard' },
  { role: 'ExaminationOfficer', path: '/department-head/dashboard' },
  { role: 'ExaminationOfficer', path: '/lecturer/classes' },
  { role: 'ExaminationOfficer', path: '/department-head/question-review' },
  { role: 'ExaminationOfficer', path: '/department-head/appeals' },
  { role: 'Admin', path: '/lecturer/classes' },
  { role: 'Admin', path: '/lecturer/audit' },
  { role: 'Admin', path: '/lecturer/appeals' },
  { role: 'Admin', path: '/department-head/appeals' },
  { role: 'Admin', path: '/examination-officer/exams' },
  { role: 'Proctor', path: '/student/overview' },
  { role: 'Proctor', path: '/lecturer/dashboard' },
  { role: 'Proctor', path: '/lecturer/classes' },
  { role: 'Proctor', path: '/lecturer/question-bank' },
  { role: 'Proctor', path: '/lecturer/audit' },
  { role: 'Proctor', path: '/lecturer/appeals' },
  { role: 'Proctor', path: '/department-head/dashboard' },
  { role: 'Proctor', path: '/department-head/question-review' },
  { role: 'Proctor', path: '/department-head/appeals' },
  { role: 'Proctor', path: '/examination-officer/dashboard' },
  { role: 'Proctor', path: '/examination-officer/exams' },
  { role: 'Proctor', path: '/examination-officer/tickets/assign' },
  { role: 'Proctor', path: '/examination-officer/appeals' },
  { role: 'Proctor', path: '/examination-officer/dlq' },
  { role: 'Proctor', path: '/admin/dashboard' },
  { role: 'Proctor', path: '/admin/dlq' },
  { role: 'Student', path: '/lecturer/dashboard' },
  { role: 'Student', path: '/lecturer/classes' },
  { role: 'Student', path: '/lecturer/question-bank' },
  { role: 'Student', path: '/lecturer/audit' },
  { role: 'Student', path: '/lecturer/appeals' },
  { role: 'Student', path: '/department-head/dashboard' },
  { role: 'Student', path: '/department-head/question-review' },
  { role: 'Student', path: '/department-head/appeals' },
  { role: 'Student', path: '/examination-officer/dashboard' },
  { role: 'Student', path: '/examination-officer/exams' },
  { role: 'Student', path: '/examination-officer/tickets/assign' },
  { role: 'Student', path: '/examination-officer/appeals' },
  { role: 'Student', path: '/examination-officer/dlq' },
  { role: 'Student', path: '/proctor/attendance' },
  { role: 'Student', path: '/admin/dashboard' },
  { role: 'Student', path: '/admin/dlq' },
]

const allowedHomeCases: Array<{ role: E2eRole; path: string }> = [
  { role: 'Student', path: '/student/overview' },
  { role: 'Instructor', path: '/lecturer/dashboard' },
  { role: 'DepartmentHead', path: '/department-head/dashboard' },
  { role: 'ExaminationOfficer', path: '/examination-officer/dashboard' },
  { role: 'Proctor', path: '/proctor/attendance' },
  { role: 'Admin', path: '/admin/dashboard' },
]

async function seedAuthenticatedRole(page: Page, role: E2eRole): Promise<void> {
  await page.addInitScript((seededRole: E2eRole) => {
    const user = {
      id: `e2e-${seededRole.toLowerCase()}`,
      email: `${seededRole.toLowerCase()}@e2e.test`,
      fullName: 'E2E Route Matrix User',
      role: seededRole,
      mustChangePassword: false,
    }

    sessionStorage.setItem('access_token', 'e2e-access-token')
    sessionStorage.setItem('refresh_token', 'e2e-refresh-token')
    sessionStorage.setItem('user_profile', JSON.stringify(user))
  }, role)
}

test.describe('RBAC route matrix', () => {
  for (const routeCase of forbiddenRouteCases) {
    test(`${routeCase.role} cannot open ${routeCase.path}`, async ({ page }) => {
      const apiRequests: string[] = []
      page.on('request', (request) => {
        if (request.url().includes('/api/')) apiRequests.push(request.url())
      })
      await seedAuthenticatedRole(page, routeCase.role)
      await page.goto(routeCase.path)

      await expect(page).toHaveURL(/\/unauthorized$/)
      expect(apiRequests).toEqual([])
    })
  }

  for (const routeCase of allowedHomeCases) {
    test(`${routeCase.role} can open its own portal`, async ({ page }) => {
      await seedAuthenticatedRole(page, routeCase.role)
      await page.goto(routeCase.path)

      await expect(page).toHaveURL(new RegExp(`${routeCase.path.replaceAll('/', '\\/')}$`))
      await expect(page).not.toHaveURL(/\/unauthorized$/)
    })
  }

  test('an unauthenticated user is redirected to login', async ({ page }) => {
    await page.goto('/admin/dashboard')
    await expect(page).toHaveURL(/\/login$/)
  })

  test('an invalid role stored in the session cannot mount any protected portal', async ({ page }) => {
    await page.addInitScript(() => {
      sessionStorage.setItem('access_token', 'e2e-access-token')
      sessionStorage.setItem('refresh_token', 'e2e-refresh-token')
      sessionStorage.setItem(
        'user_profile',
        JSON.stringify({
          id: 'e2e-invalid-role',
          email: 'invalid-role@e2e.test',
          fullName: 'Invalid Role User',
          role: 'UnknownRole',
          mustChangePassword: false,
        }),
      )
    })

    await page.goto('/admin/dashboard')
    await expect(page).toHaveURL(/\/login$/)
  })

  test('a failed refresh after token expiry clears the session and returns to login', async ({ page }) => {
    await seedAuthenticatedRole(page, 'Instructor')
    await page.route('**/api/v1/**', async (route) => {
      await route.fulfill({
        status: 401,
        contentType: 'application/problem+json',
        body: JSON.stringify({ status: 401, title: 'Unauthorized', detail: 'Token expired.' }),
      })
    })

    await page.goto('/lecturer/dashboard')
    await expect(page).toHaveURL(/\/login$/)
    expect(await page.evaluate(() => sessionStorage.getItem('access_token'))).toBeNull()
    expect(await page.evaluate(() => sessionStorage.getItem('refresh_token'))).toBeNull()
    expect(await page.evaluate(() => sessionStorage.getItem('user_profile'))).toBeNull()
  })

  test('keeps an authenticated role on a dynamic deep link with query and hash', async ({ page }) => {
    await seedAuthenticatedRole(page, 'ExaminationOfficer')
    await page.route('**/api/v1/**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    })

    await page.goto('/examination-officer/exams/session-123?tab=shifts#tickets')
    await expect(page).toHaveURL(/\/examination-officer\/exams\/session-123\?tab=shifts#tickets$/)
    await expect(page).not.toHaveURL(/\/unauthorized$/)
  })

  test('maps a legacy official-exam URL with query and hash to the officer portal', async ({ page }) => {
    await seedAuthenticatedRole(page, 'ExaminationOfficer')

    await page.goto('/official-exams/manage?tab=shifts#tickets')
    await expect(page).toHaveURL(/\/examination-officer\/exams$/)
  })

  test('mustChangePassword takes precedence over a permitted business route', async ({ page }) => {
    await seedAuthenticatedRole(page, 'Instructor')
    await page.addInitScript(() => {
      const profile = JSON.parse(sessionStorage.getItem('user_profile') ?? '{}')
      sessionStorage.setItem('user_profile', JSON.stringify({ ...profile, mustChangePassword: true }))
    })
    await page.goto('/lecturer/dashboard')
    await expect(page).toHaveURL(/\/change-password$/)
  })
})
