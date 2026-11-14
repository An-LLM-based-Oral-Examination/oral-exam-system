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
  { role: 'DepartmentHead', path: '/admin/dashboard' },
  { role: 'ExaminationOfficer', path: '/lecturer/dashboard' },
  { role: 'ExaminationOfficer', path: '/department-head/dashboard' },
  { role: 'Admin', path: '/lecturer/classes' },
  { role: 'Admin', path: '/lecturer/audit' },
  { role: 'Proctor', path: '/student/overview' },
  { role: 'Proctor', path: '/lecturer/dashboard' },
  { role: 'Proctor', path: '/department-head/dashboard' },
  { role: 'Proctor', path: '/examination-officer/dashboard' },
  { role: 'Proctor', path: '/admin/dashboard' },
  { role: 'Student', path: '/lecturer/dashboard' },
  { role: 'Student', path: '/department-head/dashboard' },
  { role: 'Student', path: '/examination-officer/dashboard' },
  { role: 'Student', path: '/proctor/attendance' },
  { role: 'Student', path: '/admin/dashboard' },
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
