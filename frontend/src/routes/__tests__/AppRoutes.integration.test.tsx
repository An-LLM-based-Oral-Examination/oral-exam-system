import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { appRoutes } from '@/routes/AppRoutes'
import type { User, UserRole } from '@/types/auth.types'
import { adminService } from '@/services/admin.service'
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

  it('redirects /admin and legacy /admin/dashboard?tab=* to modular Admin routes', async () => {
    const adminRootRouter = await renderRoute('/admin', 'Admin')
    expect(adminRootRouter.state.location.pathname).toBe('/admin/dashboard')

    const legacyUsersRouter = await renderRoute('/admin/dashboard?tab=users', 'Admin')
    expect(legacyUsersRouter.state.location.pathname).toBe('/admin/users')

    const legacySemestersRouter = await renderRoute('/admin/dashboard?tab=semesters', 'Admin')
    expect(legacySemestersRouter.state.location.pathname).toBe('/admin/semesters')

    const legacyCoursesRouter = await renderRoute('/admin/dashboard?tab=courses', 'Admin')
    expect(legacyCoursesRouter.state.location.pathname).toBe('/admin/courses')

    const legacyConfigsRouter = await renderRoute('/admin/dashboard?tab=configs', 'Admin')
    expect(legacyConfigsRouter.state.location.pathname).toBe('/admin/configs')
  })

  it('mounts modular AdminSemestersPage, AdminCoursesPage, and AdminConfigsPage for Admin role', async () => {
    const getSemesters = vi.spyOn(adminService, 'getSemesters').mockResolvedValue([])
    const getCourses = vi.spyOn(adminService, 'getCourses').mockResolvedValue([])
    const getDepartmentHeads = vi.spyOn(adminService, 'getDepartmentHeads').mockResolvedValue([])
    const getAcademicPrograms = vi.spyOn(adminService, 'getAcademicPrograms').mockResolvedValue([])
    const getSystemConfigs = vi.spyOn(adminService, 'getSystemConfigs').mockResolvedValue([])

    const semRouter = await renderRoute('/admin/semesters', 'Admin')
    expect(semRouter.state.location.pathname).toBe('/admin/semesters')
    await waitFor(() => {
      expect(
        within(screen.getByRole('main')).getByRole('heading', { name: 'Học kỳ & Môn mở' }),
      ).toBeInTheDocument()
    })

    const courseRouter = await renderRoute('/admin/courses', 'Admin')
    expect(courseRouter.state.location.pathname).toBe('/admin/courses')
    await waitFor(() => {
      expect(
        within(screen.getByRole('main')).getByRole('heading', {
          name: 'Môn học, Chương trình ĐT & CLO',
        }),
      ).toBeInTheDocument()
    })

    const cfgRouter = await renderRoute('/admin/configs', 'Admin')
    expect(cfgRouter.state.location.pathname).toBe('/admin/configs')
    await waitFor(() => {
      expect(
        within(screen.getByRole('main')).getByRole('heading', { name: 'Cấu hình hệ thống MF-01' }),
      ).toBeInTheDocument()
    })

    getSemesters.mockRestore()
    getCourses.mockRestore()
    getDepartmentHeads.mockRestore()
    getAcademicPrograms.mockRestore()
    getSystemConfigs.mockRestore()
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
