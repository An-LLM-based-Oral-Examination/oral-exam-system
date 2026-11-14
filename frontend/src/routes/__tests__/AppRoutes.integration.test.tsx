import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { appRoutes } from '@/routes/AppRoutes'
import type { User, UserRole } from '@/types/auth.types'
import { officialExamService } from '@/services/officialExam.service'
import { appealsService } from '@/services/appeals.service'

function createAuthValue(role: UserRole, overrides: Partial<AuthContextType> = {}): AuthContextType {
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

async function renderRoute(path: string, role: UserRole, overrides: Partial<AuthContextType> = {}) {
  const router = createMemoryRouter(appRoutes, { initialEntries: [path] })
  render(
    <AuthContext.Provider value={createAuthValue(role, overrides)}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  )
  await router.initialize()
  return router
}

describe('appRoutes integration RBAC', () => {
  const forbiddenCases: Array<[UserRole, string]> = [
    ['Instructor', '/department-head/dashboard'],
    ['Instructor', '/examination-officer/dashboard'],
    ['Instructor', '/admin/dashboard'],
    ['DepartmentHead', '/lecturer/classes'],
    ['DepartmentHead', '/examination-officer/dlq'],
    ['DepartmentHead', '/admin/dashboard'],
    ['ExaminationOfficer', '/lecturer/audit'],
    ['ExaminationOfficer', '/department-head/appeals'],
    ['ExaminationOfficer', '/admin/dashboard'],
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
