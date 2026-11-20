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
    expect(apiClient.get).toHaveBeenCalledWith(
      '/auth/me',
      expect.objectContaining({
        _skipAuthRefresh: true,
        _silentAuthFailure: true,
      }),
    )
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

  it.each([401, 403, 409])(
    'returns null without throwing when /auth/refresh-token responds with HTTP %i',
    async (status) => {
      vi.spyOn(apiClient, 'post').mockRejectedValue({ status, detail: 'expired or rotated' })

      const restored = await authService.restoreSession()

      expect(restored).toBeNull()
      expect(sessionStorage.getItem('access_token')).toBeNull()
    },
  )

  it('cleans up orphaned access_token and returns null when /auth/me rejects with 401', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: { accessToken: 'ephemeral-access', tokenType: 'Bearer', expiresIn: 900 },
    })
    vi.spyOn(apiClient, 'get').mockRejectedValue({ status: 401, detail: 'revoked' })

    const restored = await authService.restoreSession()

    expect(restored).toBeNull()
    expect(sessionStorage.getItem('access_token')).toBeNull()
    expect(sessionStorage.getItem('user_profile')).toBeNull()
  })

  it('cleans up orphaned access_token and rethrows when /auth/me fails with 500', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: { accessToken: 'ephemeral-access', tokenType: 'Bearer', expiresIn: 900 },
    })
    vi.spyOn(apiClient, 'get').mockRejectedValue({ status: 500, detail: 'server error' })

    await expect(authService.restoreSession()).rejects.toMatchObject({ status: 500 })
    expect(sessionStorage.getItem('access_token')).toBeNull()
  })

  it('cleans up orphaned access_token when verification is invalidated while /auth/me is in flight', async () => {
    let active = true
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: { accessToken: 'stale-access', tokenType: 'Bearer', expiresIn: 900 },
    })
    vi.spyOn(apiClient, 'get').mockImplementation(async () => {
      active = false
      return { data: user }
    })

    const restored = await authService.restoreSession(() => active)

    expect(restored).toBeNull()
    expect(sessionStorage.getItem('access_token')).toBeNull()
    expect(sessionStorage.getItem('user_profile')).toBeNull()
  })
})
