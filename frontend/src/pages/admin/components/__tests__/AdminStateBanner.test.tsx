import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '@/context/AuthContext'
import { useAuth } from '@/hooks/useAuth'
import type { NormalizedProblem } from '@/lib/problemDetails'
import { LoginPage } from '@/pages/auth/LoginPage'
import apiClient from '@/services/api.client'
import { authService } from '@/services/auth.service'
import { AdminStateBanner } from '../AdminStateBanner'

vi.mock('@/pages/auth/LoginThreeScene', () => ({
  LoginThreeScene: () => null,
}))

const problem401: NormalizedProblem = {
  status: 401,
  title: 'Unauthorized',
  detail: 'Phiên đăng nhập đã hết hạn.',
  errorCode: 'UNAUTHORIZED',
  traceId: null,
  fieldErrors: {},
  fieldMessages: [],
  isAuthError: true,
  isConflict: false,
  isGone: false,
  isRateLimited: false,
}

const adminUser = {
  id: '00000000-0000-0000-0000-000000000099',
  email: 'admin@fpt.edu.vn',
  fullName: 'Quản trị viên',
  role: 'Admin' as const,
  mustChangePassword: false,
}

function AuthenticatedAdminBannerHarness() {
  const { isAuthenticated, isLoading } = useAuth()
  return (
    <div>
      <output data-testid="auth-state">
        {isLoading ? 'loading' : isAuthenticated ? 'authenticated' : 'anonymous'}
      </output>
      <AdminStateBanner problem={problem401} />
    </div>
  )
}

describe('AdminStateBanner 401 re-login', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
  })

  afterEach(() => {
    cleanup()
    vi.restoreAllMocks()
    sessionStorage.clear()
    localStorage.clear()
  })

  it('clears AuthProvider session on 401 re-login link click and stays on /login without bouncing back to /', async () => {
    sessionStorage.setItem('access_token', 'active.admin.token')
    sessionStorage.setItem('user_profile', JSON.stringify(adminUser))

    vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: {
        ...adminUser,
        role: 'admin',
        isActive: true,
      },
      config: { headers: { Authorization: 'Bearer active.admin.token' } },
    })
    vi.spyOn(apiClient, 'post').mockResolvedValue({ data: {} })
    const logoutSpy = vi.spyOn(authService, 'logout')

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/admin/users']}>
          <Routes>
            <Route path="/" element={<div data-testid="portal-home">Portal Home</div>} />
            <Route path="/admin/users" element={<AuthenticatedAdminBannerHarness />} />
            <Route path="/login" element={<LoginPage />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    )

    await waitFor(() => {
      expect(screen.getByTestId('auth-state')).toHaveTextContent('authenticated')
    })

    const reLoginLink = screen.getByRole('link', { name: 'Đăng nhập lại' })
    expect(reLoginLink).toHaveAttribute('href', '/login')

    fireEvent.click(reLoginLink)

    expect(await screen.findByText('Cổng đăng nhập')).toBeInTheDocument()
    expect(screen.queryByTestId('portal-home')).toBeNull()
    expect(logoutSpy).toHaveBeenCalledTimes(1)
    expect(sessionStorage.getItem('access_token')).toBeNull()
    expect(sessionStorage.getItem('user_profile')).toBeNull()
  })

  it('falls back to authService.clearSession without throwing when rendered outside AuthProvider', () => {
    sessionStorage.setItem('access_token', 'stale.admin.token')
    sessionStorage.setItem('user_profile', JSON.stringify(adminUser))
    const clearSessionSpy = vi.spyOn(authService, 'clearSession')

    render(
      <MemoryRouter initialEntries={['/admin/users']}>
        <AdminStateBanner problem={problem401} />
      </MemoryRouter>,
    )

    const reLoginLink = screen.getByRole('link', { name: 'Đăng nhập lại' })
    expect(reLoginLink).toHaveAttribute('href', '/login')

    fireEvent.click(reLoginLink)

    expect(clearSessionSpy).toHaveBeenCalledTimes(1)
    expect(sessionStorage.getItem('access_token')).toBeNull()
    expect(sessionStorage.getItem('user_profile')).toBeNull()
  })
})
