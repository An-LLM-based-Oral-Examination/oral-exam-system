import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react'
import { AxiosHeaders, type AxiosRequestConfig, type AxiosResponse } from 'axios'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { appRoutes } from '@/routes/AppRoutes'
import type { User, UserRole } from '@/types/auth.types'
import { adminService } from '@/services/admin.service'
import { apiClient } from '@/services/api.client'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'
import { notificationService } from '@/services/notification.service'
import { officialExamService } from '@/services/officialExam.service'
import { appealsService } from '@/services/appeals.service'

function createAuthValue(
  role: UserRole,
  overrides: Partial<AuthContextType> = {},
): AuthContextType {
  const user: User = {
    id: `integration-${role.toLowerCase()}`,
    email: `${role.toLowerCase()}@integration.test`,
    fullName: 'Integration Route User',
    role,
    mustChangePassword: false,
  }

  return {
    user,
    token: 'integration-token',
    isAuthenticated: true,
    isStudent: role === 'Student',
    isInstructor: role === 'Instructor',
    isDepartmentHead: role === 'DepartmentHead',
    isExaminationOfficer: role === 'ExaminationOfficer',
    isProctor: role === 'Proctor',
    isAdmin: role === 'Admin',
    loginWithPassword: async () => undefined,
    loginWithGoogle: async () => undefined,
    changePassword: async () => undefined,
    logout: async () => undefined,
    hasRole: (roles) => roles.includes(role),
    ...overrides,
  }
}

let activeRouter: ReturnType<typeof createMemoryRouter> | undefined

afterEach(() => {
  cleanup()
  activeRouter?.dispose()
  activeRouter = undefined
})

async function renderRoute(path: string, role: UserRole, overrides: Partial<AuthContextType> = {}) {
  cleanup()
  activeRouter?.dispose()
  const router = createMemoryRouter(appRoutes, { initialEntries: [path] })
  activeRouter = router
  render(
    <AuthContext.Provider value={createAuthValue(role, overrides)}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  )
  await act(async () => {
    await router.initialize()
    await vi.dynamicImportSettled()
  })
  return router
}

describe('appRoutes integration RBAC', () => {
  const forbiddenCases: Array<[UserRole, string]> = [
    ['Instructor', '/department-head/dashboard'],
    ['Instructor', '/examination-officer/dashboard'],
    ['Instructor', '/admin/dashboard'],
    ['Instructor', '/admin/users'],
    ['DepartmentHead', '/lecturer/classes'],
    ['DepartmentHead', '/examination-officer/dlq'],
    ['DepartmentHead', '/admin/dashboard'],
    ['DepartmentHead', '/admin/notifications'],
    ['ExaminationOfficer', '/lecturer/audit'],
    ['ExaminationOfficer', '/department-head/appeals'],
    ['ExaminationOfficer', '/admin/dashboard'],
    ['ExaminationOfficer', '/admin/audit-logs'],
    ['Proctor', '/lecturer/dashboard'],
    ['Proctor', '/department-head/dashboard'],
    ['Proctor', '/examination-officer/dashboard'],
    ['Proctor', '/admin/dashboard'],
    ['Proctor', '/admin/users'],
    ['Proctor', '/admin/dlq'],
    ['Student', '/lecturer/dashboard'],
    ['Student', '/department-head/dashboard'],
    ['Student', '/examination-officer/dashboard'],
    ['Student', '/proctor/attendance'],
    ['Student', '/admin/dashboard'],
    ['Admin', '/lecturer/classes'],
    ['Admin', '/lecturer/audit'],
    ['Admin', '/examination-officer/dashboard'],
    ['Admin', '/department-head/question-review'],
    ['Admin', '/department-head/exam-structures'],
    ['Admin', '/lecturer/appeals'],
    ['Admin', '/proctor/attendance'],
    ['Admin', '/examination-officer/dlq'],
  ]

  it.each(forbiddenCases)('redirects %s away from forbidden route %s', async (role, path) => {
    const router = await renderRoute(path, role)
    expect(router.state.location.pathname).toBe('/unauthorized')
  })

  it('redirects an unauthenticated deep link to login', async () => {
    const router = await renderRoute('/admin/dashboard?tab=users#list', 'Admin', {
      user: null,
      token: null,
      isAuthenticated: false,
    })

    expect(router.state.location.pathname).toBe('/login')
  })

  describe('Admin modular routes and redirects', () => {
    beforeEach(() => {
      const originalApiGet: typeof apiClient.get = apiClient.get.bind(apiClient)
      vi.spyOn(apiClient, 'get').mockImplementation(((
        url: string,
        config?: AxiosRequestConfig,
      ) => {
        if (url === '/diagnostics/abuse') {
          const response: AxiosResponse<{
            requests: number
            rejected: number
            loginLockouts: number
            scope: 'api-process'
          }> = {
            data: { requests: 0, rejected: 0, loginLockouts: 0, scope: 'api-process' },
            status: 200,
            statusText: 'OK',
            headers: new AxiosHeaders(),
            config: { ...(config ?? {}), headers: new AxiosHeaders() },
          }
          return Promise.resolve(response)
        }
        return originalApiGet(url, config)
      }) as typeof apiClient.get)
      vi.spyOn(notificationService, 'list').mockResolvedValue({
        items: [],
        unreadCount: 0,
        totalCount: 0,
        page: 1,
        pageSize: 20,
      })
      vi.spyOn(adminService, 'getUsers').mockResolvedValue({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      })
      vi.spyOn(adminService, 'getRuntimeDiagnostics').mockResolvedValue({
        timestamp: '2026-11-19T00:00:00Z',
        uptime: '0.01:00:00',
        boundedChannels: { gradingQueue: { count: 0, capacity: 1000 } },
        deadLetterQueue: {
          totalPending: 0,
          byFlowType: { practice: 0, mockExam: 0, inClassExam: 0 },
        },
        abuseMetrics: { requests: 0, rejected: 0, loginLockouts: 0 },
        performance: {
          totalRequests: 0,
          serverErrors5xx: 0,
          serverErrorRatePercent: 0,
          averageLatencyMs: 0,
          p95LatencyMs: 0,
        },
        system: { workingSetMb: 100, gcTotalMemoryMb: 20, threadCount: 8 },
      })
      vi.spyOn(adminService, 'getNotifications').mockResolvedValue({
        items: [],
        totalCount: 0,
        unreadTotalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 1,
      })
      vi.spyOn(adminService, 'getAuditLogs').mockResolvedValue({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      })
      vi.spyOn(deadLetterQueueService, 'listForAdmin').mockResolvedValue([])
      vi.spyOn(adminService, 'getSemesters').mockResolvedValue([])
      vi.spyOn(adminService, 'getCourses').mockResolvedValue([])
      vi.spyOn(adminService, 'getDepartmentHeads').mockResolvedValue([])
      vi.spyOn(adminService, 'getAcademicPrograms').mockResolvedValue([])
      vi.spyOn(adminService, 'getSystemConfigs').mockResolvedValue([])
    })

    afterEach(() => {
      cleanup()
      vi.restoreAllMocks()
    })

    it('redirects /admin and legacy /admin/dashboard?tab=* to modular Admin routes', async () => {
      const legacyRedirectCases: Array<[string, string]> = [
        ['/admin', '/admin/dashboard'],
        ['/admin/dashboard?tab=users', '/admin/users'],
        ['/admin/dashboard?tab=semesters', '/admin/semesters'],
        ['/admin/dashboard?tab=courses', '/admin/courses'],
        ['/admin/dashboard?tab=configs', '/admin/configs'],
      ]

      for (const [sourcePath, expectedPath] of legacyRedirectCases) {
        const router = await renderRoute(sourcePath, 'Admin')
        expect(router.state.location.pathname).toBe(expectedPath)
        cleanup()
      }
    })

    const adminModularRoutes: Array<{
      path: string
      expectedPath: string
      heading: string
      secondaryHeading?: string
    }> = [
      {
        path: '/admin/dashboard',
        expectedPath: '/admin/dashboard',
        heading: 'Tổng quan & Vận hành hệ thống',
      },
      {
        path: '/admin/users',
        expectedPath: '/admin/users',
        heading: 'Quản lý người dùng',
        secondaryHeading: 'Danh sách tài khoản',
      },
      {
        path: '/admin/notifications',
        expectedPath: '/admin/notifications',
        heading: 'Thông báo hệ thống',
      },
      {
        path: '/admin/audit-logs',
        expectedPath: '/admin/audit-logs',
        heading: 'Nhật ký kiểm toán hệ thống',
      },
      {
        path: '/admin/dlq',
        expectedPath: '/admin/dlq',
        heading: 'Hàng đợi lỗi AI (MF-01 / MF-02 / MF-05)',
      },
      {
        path: '/dlq',
        expectedPath: '/admin/dlq',
        heading: 'Hàng đợi lỗi AI (MF-01 / MF-02 / MF-05)',
      },
      {
        path: '/admin/semesters',
        expectedPath: '/admin/semesters',
        heading: 'Học kỳ & Môn mở',
        secondaryHeading: 'Danh sách học kỳ',
      },
      {
        path: '/admin/courses',
        expectedPath: '/admin/courses',
        heading: 'Môn học, Chương trình ĐT & CLO',
      },
      {
        path: '/admin/configs',
        expectedPath: '/admin/configs',
        heading: 'Cấu hình hệ thống MF-01',
      },
    ]

    it.each(adminModularRoutes)(
      'mounts modular Admin route $path -> $expectedPath for Admin role',
      async ({ path, expectedPath, heading, secondaryHeading }) => {
        const router = await renderRoute(path, 'Admin')
        expect(router.state.location.pathname).toBe(expectedPath)
        await waitFor(
          () => {
            const main = screen.getByRole('main')
            expect(within(main).getAllByRole('heading', { name: heading }).length).toBeGreaterThanOrEqual(1)
            if (secondaryHeading) {
              expect(within(main).getByRole('heading', { name: secondaryHeading })).toBeInTheDocument()
            }
          },
          { timeout: 10000 },
        )
      },
      15000,
    )
  })

  it('forces password change before evaluating a permitted route', async () => {
    const router = await renderRoute('/lecturer/dashboard', 'Instructor', {
      user: {
        id: 'must-change-user',
        email: 'must-change@integration.test',
        fullName: 'Must Change User',
        role: 'Instructor',
        mustChangePassword: true,
      },
    })

    expect(router.state.location.pathname).toBe('/change-password')
  })

  it('does not mount the examination-officer page or call its service for an Instructor', async () => {
    const getSessions = vi.spyOn(officialExamService, 'getSessions').mockResolvedValue([])
    const getShifts = vi.spyOn(officialExamService, 'getShifts').mockResolvedValue([])

    const router = await renderRoute('/examination-officer/exams', 'Instructor')

    expect(router.state.location.pathname).toBe('/unauthorized')
    expect(getSessions).not.toHaveBeenCalled()
    expect(getShifts).not.toHaveBeenCalled()
    getSessions.mockRestore()
    getShifts.mockRestore()
  })

  it('does not mount the lecturer appeal page or call its service for a Student', async () => {
    const listAppeals = vi.spyOn(appealsService, 'list').mockResolvedValue([])

    const router = await renderRoute('/lecturer/appeals', 'Student')

    expect(router.state.location.pathname).toBe('/unauthorized')
    expect(listAppeals).not.toHaveBeenCalled()
    listAppeals.mockRestore()
  })
})
