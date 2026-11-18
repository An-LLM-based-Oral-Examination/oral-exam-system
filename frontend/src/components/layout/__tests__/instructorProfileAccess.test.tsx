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
  it('renders Instructor navbar profile pill as a link to /profile', () => {
    render(
      <AuthContext.Provider value={createInstructorAuth()}>
        <MemoryRouter initialEntries={['/lecturer/dashboard']}>
          <Navbar onOpenSidebar={vi.fn()} />
        </MemoryRouter>
      </AuthContext.Provider>,
    )

    const profileLink = screen.getByRole('link', { name: 'Mở hồ sơ cá nhân' })
    expect(profileLink).toHaveAttribute('href', '/profile')
  })

  it('allows Instructor to open /profile via ProfileRouteLayout and launch ChangePassword dialog', () => {
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

    expect(screen.queryByText('Lecturer Dashboard Redirect')).toBeNull()
    expect(screen.getByText('Thông tin cá nhân')).toBeTruthy()
    expect(screen.getByText('Bảo mật tài khoản')).toBeTruthy()

    const changePwdBtn = screen.getByRole('button', { name: 'Đổi mật khẩu' })
    fireEvent.click(changePwdBtn)
    expect(screen.getByRole('dialog')).toBeTruthy()
  })
})
