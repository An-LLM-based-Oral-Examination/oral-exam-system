import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { appRoutes } from '@/routes/AppRoutes'
import { getRoleHomePath } from '@/routes/routeAccess'
import type { User, UserRole } from '@/types/auth.types'

// Layout giả: giữ khung điều hướng để kiểm tra lỗi trang con KHÔNG làm mất Sidebar/Header.
vi.mock('@/components/layout/AppLayout', async () => {
  const { createElement } = await import('react')
  const { Outlet } = await import('react-router-dom')
  return {
    AppLayout: () =>
      createElement(
        'div',
        null,
        createElement('nav', { 'data-testid': 'portal-chrome' }, 'Sidebar'),
        createElement(Outlet),
      ),
  }
})
vi.mock('@/layouts/ExaminationOfficerLayout', async () => {
  const { createElement } = await import('react')
  const { Outlet } = await import('react-router-dom')
  return {
    ExaminationOfficerLayout: () =>
      createElement(
        'div',
        null,
        createElement('nav', { 'data-testid': 'portal-chrome' }, 'Sidebar'),
        createElement(Outlet),
      ),
  }
})

// Mọi trang chủ của 6 vai trò đều ném lỗi lúc render.
vi.mock('@/pages/student/StudentDashboardPage', () => ({
  StudentDashboardPage: () => {
    throw new Error('boom-student')
  },
}))
vi.mock('@/pages/lecturer/LecturerDashboardPage', () => ({
  LecturerDashboardPage: () => {
    throw new Error('boom-instructor')
  },
}))
vi.mock('@/pages/department-head/DepartmentHeadDashboardPage', () => ({
  DepartmentHeadDashboardPage: () => {
    throw new Error('boom-department-head')
  },
}))
vi.mock('@/pages/examination-officer/ExaminationOfficerDashboardPage', () => ({
  ExaminationOfficerDashboardPage: () => {
    throw new Error('boom-examination-officer')
  },
}))
vi.mock('@/pages/proctor/ProctorAttendancePage', () => ({
  ProctorAttendancePage: () => {
    throw new Error('boom-proctor')
  },
}))
vi.mock('@/pages/admin/AdminDashboardPage', () => ({
  AdminDashboardPage: () => {
    throw new Error('boom-admin')
  },
}))
vi.mock('@/pages/admin/AdminOverviewPage', () => ({
  AdminOverviewPage: () => {
    throw new Error('boom-admin-overview')
  },
}))

function createAuthValue(role: UserRole): AuthContextType {
  const user: User = {
    id: `eb-${role.toLowerCase()}`,
    email: `${role.toLowerCase()}@eb.test`,
    fullName: 'Error Boundary User',
    role,
    mustChangePassword: false,
  }
  return {
    user,
    token: 'eb-token',
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
  }
}

describe('appRoutes: ErrorBoundary cô lập lỗi từng nhánh của 6 vai trò', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  const roles: UserRole[] = [
    'Student',
    'Instructor',
    'DepartmentHead',
    'ExaminationOfficer',
    'Proctor',
    'Admin',
  ]

  it.each(roles)(
    'vai trò %s: trang chủ lỗi → hiện thông báo tiếng Việt, vẫn giữ Sidebar, giữ nguyên URL được phép',
    async (role) => {
      const homePath = getRoleHomePath(role)
      const router = createMemoryRouter(appRoutes, { initialEntries: [homePath] })
      render(
        <AuthContext.Provider value={createAuthValue(role)}>
          <RouterProvider router={router} />
        </AuthContext.Provider>,
      )

      expect(await screen.findByText('Đã xảy ra sự cố hiển thị')).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /Tải lại trang/ })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /Về trang chủ/ })).toBeInTheDocument()
      expect(screen.getByTestId('portal-chrome')).toBeInTheDocument()
      expect(screen.queryByText(/Unexpected Application Error/i)).not.toBeInTheDocument()
      expect(router.state.location.pathname).toBe(homePath)
    },
  )

  it('không làm thay đổi kiểm tra quyền: Student vào cổng Admin vẫn bị chuyển sang /unauthorized trước khi mount trang', async () => {
    const router = createMemoryRouter(appRoutes, { initialEntries: ['/admin/dashboard'] })
    render(
      <AuthContext.Provider value={createAuthValue('Student')}>
        <RouterProvider router={router} />
      </AuthContext.Provider>,
    )
    await screen.findByText(/403/)
    expect(router.state.location.pathname).toBe('/unauthorized')
    expect(screen.queryByText('Đã xảy ra sự cố hiển thị')).not.toBeInTheDocument()
  })
})
