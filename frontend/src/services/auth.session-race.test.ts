import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AxiosError, AxiosHeaders } from 'axios'
import apiClient from './api.client'
import { authService } from './auth.service'
import { rotateSessionEpoch } from './authSessionState'

const originalAdapter = apiClient.defaults.adapter
const user = { id: 'user-1', email: 'qa@example.test', fullName: 'QA', role: 'Student' }
function setSession(suffix = '1') {
  rotateSessionEpoch()
  sessionStorage.setItem('access_token', `access-${suffix}`)
  sessionStorage.setItem('refresh_token', `refresh-${suffix}`)
  sessionStorage.setItem('user_profile', JSON.stringify({ ...user, id: `user-${suffix}` }))
}
beforeEach(() => {
  sessionStorage.clear()
  localStorage.clear()
  setSession()
  vi.spyOn(console, 'warn').mockImplementation(() => {})
})
afterEach(() => {
  apiClient.defaults.adapter = originalAdapter
  vi.restoreAllMocks()
  sessionStorage.clear()
  localStorage.clear()
})

describe('in-flight authentication session isolation', () => {
  it('shares one refresh for concurrent requests in the same session', async () => {
    let finish!: () => void
    let entered!: () => void
    let refreshCalls = 0
    const refreshing = new Promise<void>((resolve) => {
      entered = resolve
    })
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        ++refreshCalls
        entered()
        await new Promise<void>((resolve) => {
          finish = resolve
        })
        return {
          data: { accessToken: 'rotated', refreshToken: 'rotated-refresh' },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      }
      if (config._retry)
        return {
          data: { ok: true },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      throw new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401,
        statusText: 'Unauthorized',
        data: {},
        headers: new AxiosHeaders(),
        config,
      })
    }
    const first = apiClient.get('/protected-1')
    const second = apiClient.get('/protected-2')
    await refreshing
    finish()
    expect((await Promise.all([first, second])).every((response) => response.status === 200)).toBe(
      true,
    )
    expect(refreshCalls).toBe(1)
  })
  it('retains the existing session on transient refresh network failure', async () => {
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token')
        throw new AxiosError('offline', 'ERR_NETWORK', config)
      throw new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401,
        statusText: 'Unauthorized',
        data: {},
        headers: new AxiosHeaders(),
        config,
      })
    }
    await expect(apiClient.get('/auth/me')).rejects.toMatchObject({ status: 401 })
    expect(sessionStorage.getItem('access_token')).toBe('access-1')
    expect(sessionStorage.getItem('refresh_token')).toBe('refresh-1')
  })
  it('does not refresh the new account for a late 401 from an old account', async () => {
    let release!: () => void
    let entered!: () => void
    const started = new Promise<void>((resolve) => {
      entered = resolve
    })
    const refreshCalls: string[] = []
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        refreshCalls.push(config.url)
        return {
          data: { accessToken: 'unwanted', refreshToken: 'unwanted' },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      }
      entered()
      if (!config._retry)
        await new Promise<void>((resolve) => {
          release = resolve
        })
      throw new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401,
        statusText: 'Unauthorized',
        data: {},
        headers: new AxiosHeaders(),
        config,
      })
    }
    const request = apiClient.get('/auth/me').catch((error) => error)
    await started
    setSession('2')
    release()
    await request
    expect(refreshCalls).toHaveLength(0)
    expect(sessionStorage.getItem('access_token')).toBe('access-2')
  })
  it.each(['logout', 'switch'])(
    'does not restore an old refresh response after %s',
    async (action) => {
      let finish!: () => void
      let entered!: () => void
      const refreshing = new Promise<void>((resolve) => {
        entered = resolve
      })
      apiClient.defaults.adapter = async (config) => {
        if (config.url === '/auth/refresh-token') {
          entered()
          await new Promise<void>((resolve) => {
            finish = resolve
          })
          return {
            data: { accessToken: 'old-rotated', refreshToken: 'old-refresh-rotated' },
            status: 200,
            statusText: 'OK',
            headers: new AxiosHeaders(),
            config,
          }
        }
        throw new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config, null, {
          status: 401,
          statusText: 'Unauthorized',
          data: {},
          headers: new AxiosHeaders(),
          config,
        })
      }
      const request = apiClient.get('/auth/me').catch((error) => error)
      await refreshing
      if (action === 'logout') authService.clearSession()
      else setSession('2')
      finish()
      await request
      expect(sessionStorage.getItem('access_token')).toBe(action === 'logout' ? null : 'access-2')
      expect(sessionStorage.getItem('refresh_token')).toBe(action === 'logout' ? null : 'refresh-2')
    },
  )
  it('does not clear a newer account when an old refresh fails', async () => {
    let fail!: () => void
    let entered!: () => void
    const refreshing = new Promise<void>((resolve) => {
      entered = resolve
    })
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        entered()
        await new Promise<void>((resolve) => {
          fail = resolve
        })
      }
      throw new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401,
        statusText: 'Unauthorized',
        data: {},
        headers: new AxiosHeaders(),
        config,
      })
    }
    const request = apiClient.get('/auth/me').catch((error) => error)
    await refreshing
    setSession('2')
    fail()
    await request
    expect(sessionStorage.getItem('access_token')).toBe('access-2')
  })
  it('does not clear a newer session when old sign-out finishes', async () => {
    let finish!: () => void
    vi.spyOn(apiClient, 'post').mockImplementation(async () => {
      await new Promise<void>((resolve) => {
        finish = resolve
      })
      return { data: {} }
    })
    const logout = authService.logout()
    expect(sessionStorage.getItem('access_token')).toBeNull()
    setSession('2')
    finish()
    await logout
    expect(sessionStorage.getItem('access_token')).toBe('access-2')
  })
})
