import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { Navbar } from '@/components/layout/Navbar'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { ProfilePage } from '@/pages/profile/ProfilePage'
import { ProfileRouteLayout } from '@/routes/ProfileRouteLayout'
import type { User } from '@/types/auth.types'

vi.mock('@/components/layout/AppLayout', async () => {
  const { Outlet } = await import('react-router-dom')
  return {
    AppLayout: () => (
      <div data-testid="app-layout">
        <Outlet />
      </div>
    ),
  }
})

vi.mock('@/layouts/ExaminationOfficerLayout', async () => {
  const { Outlet } = await import('react-router-dom')
  return {
    ExaminationOfficerLayout: () => (
      <div data-testid="eo-layout">
        <Outlet />
      </div>
    ),
  }
})

function createInstructorAuth(): AuthContextType {
  const user: User = {
    id: 'lec-1',
    email: 'lecturer@fpt.edu.vn',
    fullName: 'Nguyễn Văn Giảng Viên',
    role: 'Instructor',
    mustChangePassword: false,
  }
  return {
    user,
    token: 'token-123',
    isAuthenticated: true,
    isStudent: false,
    isInstructor: true,
    isDepartmentHead: false,
    isExaminationOfficer: false,
    isProctor: false,
    isAdmin: false,
    loginWithPassword: async () => undefined,
    loginWithGoogle: async () => undefined,
    changePassword: async () => undefined,
    logout: async () => undefined,
    hasRole: (roles) => roles.includes('Instructor'),
  }
}

describe('Instructor profile & password access', () => {
  it('shows Instructor identity without a profile link', () => {
    render(
      <AuthContext.Provider value={createInstructorAuth()}>
        <MemoryRouter initialEntries={['/lecturer/dashboard']}>
          <Navbar onOpenSidebar={vi.fn()} />
        </MemoryRouter>
      </AuthContext.Provider>,
    )

    expect(screen.queryByRole('link', { name: 'Mở hồ sơ cá nhân' })).toBeNull()
    expect(screen.getByText('Nguyễn Văn Giảng Viên')).toBeInTheDocument()
  })

  it('redirects Instructor away from the student profile', () => {
    render(
      <AuthContext.Provider value={createInstructorAuth()}>
        <MemoryRouter initialEntries={['/profile']}>
          <Routes>
            <Route element={<ProfileRouteLayout />}>
              <Route path="/profile" element={<ProfilePage />} />
            </Route>
            <Route path="/lecturer/dashboard" element={<div>Lecturer Dashboard Redirect</div>} />
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>,
    )

    expect(screen.getByText('Lecturer Dashboard Redirect')).toBeInTheDocument()
    expect(screen.queryByText('Bảo mật tài khoản')).toBeNull()
  })

  it.each(['google', 'password'] as const)('keeps the student profile and disables password changes only for Google (%s)', (loginMethod) => {
    const auth = createInstructorAuth()
    auth.user = { ...auth.user!, role: 'Student', loginMethod }
    auth.isStudent = true
    render(<AuthContext.Provider value={auth}><MemoryRouter><ProfilePage /></MemoryRouter></AuthContext.Provider>)
    const changePassword = screen.getByRole('button', { name: 'Đổi mật khẩu' })
    if (loginMethod === 'google') {
      expect(changePassword).toBeDisabled()
      fireEvent.click(changePassword)
      expect(screen.queryByRole('dialog')).toBeNull()
    } else {
      expect(changePassword).toBeEnabled()
      fireEvent.click(changePassword)
      expect(screen.getByRole('dialog')).toBeInTheDocument()
    }
  })
})
