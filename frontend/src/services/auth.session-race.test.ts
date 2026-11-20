import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AxiosError, AxiosHeaders } from 'axios'
import apiClient, { ensureFreshAccessToken, getJwtExpiryMs } from './api.client'
import { authService } from './auth.service'
import { rotateSessionEpoch } from './authSessionState'

function toBase64Url(value: string): string {
  const bytes = new TextEncoder().encode(value)
  let binary = ''
  for (const byte of bytes) {
    binary += String.fromCharCode(byte)
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function makeTestJwt(payload: Record<string, unknown>): string {
  const header = toBase64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const body = toBase64Url(JSON.stringify(payload))
  return `${header}.${body}.test-sig`
}

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
  it.each([500, 409])('returns the original protected-resource 401 when refresh fails with %s, without logging out', async (refreshStatus) => {
    const expiredJwt = makeTestJwt({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) - 60 })
    sessionStorage.setItem('access_token', expiredJwt)
    const expired = vi.fn()
    window.addEventListener('auth:session-expired', expired)
    let refreshCalls = 0
    apiClient.defaults.adapter = async (config) => {
      const refreshing = config.url === '/auth/refresh-token'
      if (refreshing) ++refreshCalls
      throw new AxiosError('request failed', 'ERR_BAD_RESPONSE', config, null, {
        status: refreshing ? refreshStatus : 401,
        statusText: 'Failure',
        data: { detail: refreshing ? 'refresh failed' : 'Cần access token hợp lệ để truy cập tài nguyên này.' },
        headers: new AxiosHeaders(),
        config,
      })
    }
    try {
      await expect(apiClient.get('/protected-diagnostic')).rejects.toMatchObject({
        status: 401,
        detail: 'Cần access token hợp lệ để truy cập tài nguyên này.',
      })
      expect(refreshCalls).toBe(1)
      expect(sessionStorage.getItem('access_token')).toBe(expiredJwt)
      expect(expired).not.toHaveBeenCalled()
    } finally {
      window.removeEventListener('auth:session-expired', expired)
    }
  })

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

  it('decodes JWT expiry safely with UTF-8 claims and returns null for opaque tokens', () => {
    const expSeconds = Math.floor(Date.now() / 1000) + 900
    const jwt = makeTestJwt({ sub: 'user-1', name: 'Nguyễn Văn A', exp: expSeconds })
    expect(getJwtExpiryMs(jwt)).toBe(expSeconds * 1000)
    expect(getJwtExpiryMs('token')).toBeNull()
    expect(getJwtExpiryMs('access-1')).toBeNull()
    expect(getJwtExpiryMs('')).toBeNull()
    expect(getJwtExpiryMs(`a.${toBase64Url(JSON.stringify({ exp: expSeconds }))}`)).toBeNull()
    expect(getJwtExpiryMs(makeTestJwt({ sub: 'user-1' }))).toBeNull()
  })

  it('retries an in-flight 401 using the newer same-epoch token after concurrent ensureFreshAccessToken rotation without duplicate refresh', async () => {
    const nearExpiryJwt = makeTestJwt({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 30 })
    const rotatedJwt = makeTestJwt({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 900 })
    sessionStorage.setItem('access_token', nearExpiryJwt)

    let releaseInFlightRequest!: () => void
    let inFlightStarted!: () => void
    const inFlightGate = new Promise<void>((resolve) => {
      inFlightStarted = resolve
    })
    let refreshCalls = 0
    const authHeadersObserved: string[] = []

    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        ++refreshCalls
        return {
          data: { accessToken: rotatedJwt },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      }
      const authHeader =
        typeof config.headers?.get === 'function'
          ? String(config.headers.get('Authorization') ?? '')
          : String(config.headers?.Authorization ?? '')
      authHeadersObserved.push(authHeader)

      if (!config._retry) {
        inFlightStarted()
        await new Promise<void>((resolve) => {
          releaseInFlightRequest = resolve
        })
        throw new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config, null, {
          status: 401,
          statusText: 'Unauthorized',
          data: {},
          headers: new AxiosHeaders(),
          config,
        })
      }

      return {
        data: { submitted: true },
        status: 200,
        statusText: 'OK',
        headers: new AxiosHeaders(),
        config,
      }
    }

    const inFlightPost = apiClient.post('/student/in-class-shifts/1/submit', { transcript: 'test' })
    await inFlightGate

    // ensureFreshAccessToken hoàn tất xoay vòng access_token TRƯỚC khi request cũ nhận về 401
    const refreshedToken = await ensureFreshAccessToken(90)
    expect(refreshedToken).toBe(rotatedJwt)
    expect(sessionStorage.getItem('access_token')).toBe(rotatedJwt)

    releaseInFlightRequest()
    const response = await inFlightPost

    expect(response.status).toBe(200)
    expect(response.data).toEqual({ submitted: true })
    expect(refreshCalls).toBe(1)
    expect(authHeadersObserved).toEqual([`Bearer ${nearExpiryJwt}`, `Bearer ${rotatedJwt}`])
  })

  it('returns existing token immediately in ensureFreshAccessToken when above minValiditySeconds or non-JWT', async () => {
    const freshJwt = makeTestJwt({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 600 })
    sessionStorage.setItem('access_token', freshJwt)

    let refreshCalls = 0
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        ++refreshCalls
      }
      throw new Error('should not be called')
    }

    expect(await ensureFreshAccessToken(90)).toBe(freshJwt)
    sessionStorage.setItem('access_token', 'access-opaque')
    expect(await ensureFreshAccessToken(90)).toBe('access-opaque')
    expect(refreshCalls).toBe(0)
  })

  it('deduplicates concurrent ensureFreshAccessToken and 401 retry calls onto a single refresh request', async () => {
    const nearExpiryJwt = makeTestJwt({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 30 })
    const rotatedJwt = makeTestJwt({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 900 })
    sessionStorage.setItem('access_token', nearExpiryJwt)

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
          data: { accessToken: rotatedJwt },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      }
      if (config._retry) {
        return {
          data: { ok: true },
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

    const p1 = ensureFreshAccessToken(90)
    const p2 = ensureFreshAccessToken(90)
    const p3 = apiClient.get('/protected')
    await refreshing
    finish()

    const [t1, t2, res3] = await Promise.all([p1, p2, p3])
    expect(t1).toBe(rotatedJwt)
    expect(t2).toBe(rotatedJwt)
    expect(res3.status).toBe(200)
    expect(refreshCalls).toBe(1)
    expect(sessionStorage.getItem('access_token')).toBe(rotatedJwt)
  })

  it('falls back to non-expired token on transient 500 error and clears session on terminal 401', async () => {
    const nearExpiryJwt = makeTestJwt({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 45 })
    sessionStorage.setItem('access_token', nearExpiryJwt)

    apiClient.defaults.adapter = async (config) => {
      throw new AxiosError('server error', 'ERR_BAD_RESPONSE', config, null, {
        status: 500,
        statusText: 'Internal Server Error',
        data: {},
        headers: new AxiosHeaders(),
        config,
      })
    }
    expect(await ensureFreshAccessToken(90)).toBe(nearExpiryJwt)
    expect(sessionStorage.getItem('access_token')).toBe(nearExpiryJwt)

    const expiredListener = vi.fn()
    window.addEventListener('auth:session-expired', expiredListener)
    apiClient.defaults.adapter = async (config) => {
      throw new AxiosError('unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401,
        statusText: 'Unauthorized',
        data: {},
        headers: new AxiosHeaders(),
        config,
      })
    }
    expect(await ensureFreshAccessToken(90)).toBe('')
    expect(sessionStorage.getItem('access_token')).toBeNull()
    expect(expiredListener).toHaveBeenCalledTimes(1)
    window.removeEventListener('auth:session-expired', expiredListener)
  })
})
