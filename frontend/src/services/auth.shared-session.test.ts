import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { authService } from './auth.service'
import { browserAuthSession } from './browserAuthSession'

const user = {
  id: 'user-1',
  email: 'qa@example.test',
  fullName: 'QA User',
  role: 'student',
  studentCode: null,
  username: 'qa',
  isActive: true,
  mustChangePassword: false,
}

beforeEach(() => {
  sessionStorage.clear()
  localStorage.clear()
  vi.restoreAllMocks()
  vi.spyOn(browserAuthSession, 'withRefreshLock').mockImplementation((action) => action())
})

describe('shared browser authentication session', () => {
  it('does not persist the refresh token returned by a legacy login response', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        accessToken: 'access-login',
        refreshToken: 'legacy-refresh-token',
        tokenType: 'Bearer',
        expiresIn: 900,
        user,
      },
    })

    await authService.passwordLogin({ username: 'qa', password: 'not-a-real-password' })

    expect(sessionStorage.getItem('access_token')).toBe('access-login')
    expect(sessionStorage.getItem('refresh_token')).toBeNull()
    expect(authService.getCurrentUser()).toMatchObject({ id: user.id, role: 'Student' })
  })

  it('restores a new tab from the HttpOnly cookie and verifies the server profile', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: { accessToken: 'restored-access', tokenType: 'Bearer', expiresIn: 900 },
    })
    vi.spyOn(apiClient, 'get').mockResolvedValue({ data: user })

    const restored = await authService.restoreSession()

    expect(apiClient.post).toHaveBeenCalledWith(
      '/auth/refresh-token',
      {},
      expect.objectContaining({
        _skipAuthRefresh: true,
        _silentAuthFailure: true,
        withCredentials: true,
      }),
    )
    expect(apiClient.get).toHaveBeenCalledWith('/auth/me')
    expect(restored).toEqual({
      accessToken: 'restored-access',
      user: expect.objectContaining({ id: user.id, role: 'Student' }),
    })
    expect(sessionStorage.getItem('refresh_token')).toBeNull()
  })

  it('calls server sign-out even though no refresh token is readable by JavaScript', async () => {
    sessionStorage.setItem('access_token', 'access-login')
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({ data: {} })

    await authService.logout()

    expect(post).toHaveBeenCalledWith(
      '/auth/sign-out',
      {},
      expect.objectContaining({ withCredentials: true }),
    )
    expect(sessionStorage.getItem('access_token')).toBeNull()
  })
})
