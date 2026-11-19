import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AxiosHeaders } from 'axios'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './AuthContext'
import { useAuth } from '@/hooks/useAuth'
import apiClient from '@/services/api.client'
import { authService } from '@/services/auth.service'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import { browserAuthSession, type BrowserAuthEvent } from '@/services/browserAuthSession'

vi.mock('@/services/auth.service', () => ({
  authService: {
    getCurrentUser: vi.fn(),
    getToken: vi.fn(),
    synchronizeCurrentUser: vi.fn(),
    clearSession: vi.fn(),
    logout: vi.fn(),
    refreshToken: vi.fn(),
    restoreSession: vi.fn(),
    passwordLogin: vi.fn(),
    googleLogin: vi.fn(),
  },
}))
vi.mock('@/services/browserAuthSession', () => ({
  browserAuthSession: {
    isIdentityAllowed: () => true,
    claimIdentity: () => true,
    subscribe: () => () => {},
    activateIdentity: vi.fn(),
    deactivateIdentity: vi.fn(),
    withRefreshLock: vi.fn((action: () => Promise<unknown>) => action()),
  },
}))
const user = {
  id: 'qa',
  email: 'qa@example.test',
  fullName: 'QA',
  role: 'Student' as const,
  mustChangePassword: false,
}
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((ok, fail) => {
    resolve = ok
    reject = fail
  })
  return { promise, resolve, reject }
}
function Actions() {
  const auth = useAuth()
  return (
    <>
      <button onClick={() => void auth.logout()}>logout</button>
      <output>{auth.user?.fullName ?? 'none'}</output>
    </>
  )
}
function mount() {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={['/private']}>
        <Actions />
        <Routes>
          <Route element={<ProtectedRoute allowedRoles={['Student']} />}>
            <Route path="/private" element={<div>private content</div>} />
            <Route path="/change-password" element={<div>password gate</div>} />
          </Route>
          <Route path="/login" element={<div>login page</div>} />
          <Route path="/unauthorized" element={<div>unauthorized page</div>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}
beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(authService.getCurrentUser).mockReturnValue({ ...user, role: 'Admin' })
  vi.mocked(authService.getToken).mockReturnValue('token')
})
describe('verified current profile gate', () => {
  it('never renders a cached role and waits for the server profile', async () => {
    const request = deferred<typeof user>()
    vi.mocked(authService.synchronizeCurrentUser).mockReturnValue(request.promise)
    mount()
    expect(screen.queryByText('private content')).toBeNull()
    expect(screen.queryByText('unauthorized page')).toBeNull()
    expect(screen.queryByText('login page')).toBeNull()
    await act(async () => request.resolve(user))
    expect(await screen.findByText('private content')).toBeTruthy()
  })
  it('restores the shared browser session when a new tab has no tab-local token', async () => {
    vi.mocked(authService.getCurrentUser).mockReturnValue(null)
    vi.mocked(authService.getToken).mockReturnValue(null)
    vi.mocked(authService.restoreSession).mockResolvedValue({ accessToken: 'restored-token', user })
    mount()
    expect(await screen.findByText('private content')).toBeTruthy()
    expect(browserAuthSession.activateIdentity).toHaveBeenCalledWith(user.id)
  })
  it('fails closed on network errors, retains tokens and offers retry', async () => {
    vi.mocked(authService.synchronizeCurrentUser)
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce(user)
    mount()
    expect(await screen.findByRole('button', { name: /đăng nhập lại/i })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /thử lại/i }))
    expect(await screen.findByText('private content')).toBeTruthy()
    expect(authService.clearSession).not.toHaveBeenCalled()
  })
  it('allows escaping a transient network error back to /login via Đăng nhập lại', async () => {
    vi.mocked(authService.synchronizeCurrentUser).mockRejectedValueOnce(new Error('offline'))
    vi.mocked(authService.logout).mockResolvedValueOnce(undefined)
    mount()
    fireEvent.click(await screen.findByRole('button', { name: /đăng nhập lại/i }))
    expect(await screen.findByText('login page')).toBeTruthy()
    expect(authService.logout).toHaveBeenCalledOnce()
  })
  it('redirects directly to /login when an unauthenticated tab has no active cookie session', async () => {
    vi.mocked(authService.getCurrentUser).mockReturnValue(null)
    vi.mocked(authService.getToken).mockReturnValue(null)
    vi.mocked(authService.restoreSession).mockResolvedValue(null)
    mount()
    expect(await screen.findByText('login page')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })
  it('redirects directly to /login instead of deadlocking when an unauthenticated tab hits a restore error', async () => {
    vi.mocked(authService.getCurrentUser).mockReturnValue(null)
    vi.mocked(authService.getToken).mockReturnValue(null)
    vi.mocked(authService.restoreSession).mockRejectedValue(new Error('network'))
    mount()
    expect(await screen.findByText('login page')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })
  it('clears cached session and redirects to /login when server rejects with 401 or 403', async () => {
    vi.mocked(authService.synchronizeCurrentUser).mockRejectedValueOnce({ status: 401 })
    mount()
    expect(await screen.findByText('login page')).toBeTruthy()
    expect(authService.clearSession).toHaveBeenCalledOnce()
    expect(screen.queryByRole('alert')).toBeNull()
  })
  it('obeys fresh MustChangePassword without triggering token refresh loops', async () => {
    vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue({
      ...user,
      mustChangePassword: true,
    })
    mount()
    expect(await screen.findByText('password gate')).toBeTruthy()
    expect(authService.refreshToken).not.toHaveBeenCalled()
  })
  it('ignores a late profile response after logout', async () => {
    const request = deferred<typeof user>()
    vi.mocked(authService.synchronizeCurrentUser).mockReturnValue(request.promise)
    mount()
    fireEvent.click(screen.getByText('logout'))
    await act(async () => request.resolve(user))
    await waitFor(() => expect(screen.getByText('login page')).toBeTruthy())
    expect(screen.queryByText('private content')).toBeNull()
  })
  it('invalidates in-flight persistence after unmount', async () => {
    const request = deferred<typeof user>()
    let isCurrent!: () => boolean
    vi.mocked(authService.synchronizeCurrentUser).mockImplementation((callback) => {
      isCurrent = callback!
      return request.promise
    })
    const mounted = mount()
    await waitFor(() => expect(isCurrent()).toBe(true))
    mounted.unmount()
    expect(isCurrent()).toBe(false)
    await act(async () => request.resolve(user))
  })
  it('invalidates in-flight verification on a cross-tab account change', async () => {
    const request = deferred<typeof user>()
    const differentUser = { ...user, id: 'different-user', fullName: 'Other QA' }
    let listener!: (event: BrowserAuthEvent) => void
    vi.spyOn(browserAuthSession, 'subscribe').mockImplementation((callback) => {
      listener = callback
      return () => {}
    })
    vi.mocked(authService.synchronizeCurrentUser).mockReturnValue(request.promise)
    vi.mocked(authService.clearSession).mockImplementation(() => {
      vi.mocked(authService.getCurrentUser).mockReturnValue(null)
      vi.mocked(authService.getToken).mockReturnValue(null)
    })
    vi.mocked(authService.restoreSession).mockResolvedValue({
      accessToken: 'different-token',
      user: differentUser,
    })
    mount()
    act(() =>
      listener({
        type: 'LOGIN',
        userId: 'different-user',
        eventId: 'evt',
        sourceTabId: 'another',
        occurredAt: Date.now(),
      }),
    )
    await act(async () => request.resolve(user))
    expect(await screen.findByText('Other QA')).toBeTruthy()
    expect(screen.getByText('private content')).toBeTruthy()
    expect(authService.clearSession).toHaveBeenCalled()
    expect(authService.logout).not.toHaveBeenCalled()
  })
})

describe('verified session lifecycle events', () => {
  it('clears authentication after a session-expired event', async () => {
    vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue(user)
    mount()
    expect(await screen.findByText('private content')).toBeTruthy()
    act(() => window.dispatchEvent(new Event('auth:session-expired')))
    expect(await screen.findByText('login page')).toBeTruthy()
    expect(authService.clearSession).toHaveBeenCalledOnce()
    expect(browserAuthSession.deactivateIdentity).toHaveBeenCalledWith(user.id)
    expect(screen.queryByText('private content')).toBeNull()
  })

  it('applies the must-change-password event to the verified profile', async () => {
    vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue(user)
    mount()
    expect(await screen.findByText('private content')).toBeTruthy()
    act(() => window.dispatchEvent(new Event('auth:must-change-password')))
    expect(await screen.findByText('password gate')).toBeTruthy()
    expect(JSON.parse(sessionStorage.getItem('user_profile') ?? '{}')).toMatchObject({
      id: user.id,
      mustChangePassword: true,
    })
    sessionStorage.clear()
  })
})

function makeJwtWithExp(expSeconds: number): string {
  const toB64Url = (raw: string) =>
    btoa(raw).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  const header = toB64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = toB64Url(JSON.stringify({ sub: user.id, exp: expSeconds }))
  return `${header}.${payload}.sig`
}

function TokenProbe() {
  const auth = useAuth()
  return (
    <>
      <button onClick={() => void auth.logout()}>logout-probe</button>
      <output data-testid="probe-token" data-authenticated={String(auth.isAuthenticated)}>
        {auth.token ?? 'none'}
      </output>
    </>
  )
}

async function waitForAuthenticatedProbe(expectedToken: string) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0)
  })
  const probe = screen.getByTestId('probe-token')
  expect(probe.getAttribute('data-authenticated')).toBe('true')
  expect(probe.textContent).toBe(expectedToken)
}

describe('proactive silent token refresh', () => {
  const originalAdapter = apiClient.defaults.adapter

  afterEach(() => {
    cleanup()
    apiClient.defaults.adapter = originalAdapter
    sessionStorage.clear()
    vi.useRealTimers()
  })

  it('proactively silent-refreshes a 15-minute JWT around minute 12 while active', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-18T12:00:00.000Z'))
    const startSec = Math.floor(Date.now() / 1000)
    const initialJwt = makeJwtWithExp(startSec + 900)
    const rotatedJwt = makeJwtWithExp(startSec + 1800)
    sessionStorage.setItem('access_token', initialJwt)
    vi.mocked(authService.getToken).mockReturnValue(initialJwt)
    vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue(user)

    let refreshCount = 0
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        ++refreshCount
        return {
          data: { accessToken: rotatedJwt },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      }
      return { data: {}, status: 200, statusText: 'OK', headers: new AxiosHeaders(), config }
    }

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/private']}>
          <TokenProbe />
        </MemoryRouter>
      </AuthProvider>,
    )

    await waitForAuthenticatedProbe(initialJwt)
    expect(refreshCount).toBe(0)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(12 * 60_000 + 1_000)
    })

    expect(refreshCount).toBe(1)
    expect(screen.getByTestId('probe-token').textContent).toBe(rotatedJwt)
  })

  it('never triggers proactive refresh when mustChangePassword is true', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-18T12:00:00.000Z'))
    const startSec = Math.floor(Date.now() / 1000)
    const nearExpiryJwt = makeJwtWithExp(startSec + 120)
    sessionStorage.setItem('access_token', nearExpiryJwt)
    vi.mocked(authService.getToken).mockReturnValue(nearExpiryJwt)
    vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue({
      ...user,
      mustChangePassword: true,
    })

    let refreshCount = 0
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') ++refreshCount
      return { data: { accessToken: 'unexpected' }, status: 200, statusText: 'OK', headers: new AxiosHeaders(), config }
    }

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/change-password']}>
          <TokenProbe />
        </MemoryRouter>
      </AuthProvider>,
    )

    await waitForAuthenticatedProbe(nearExpiryJwt)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(13 * 60_000)
      document.dispatchEvent(new Event('visibilitychange'))
      await vi.advanceTimersByTimeAsync(0)
    })

    expect(refreshCount).toBe(0)
  })

  it('cleans up proactive refresh timer on logout and refreshes on visibilitychange only when <= 3m remain', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-18T12:00:00.000Z'))
    const startSec = Math.floor(Date.now() / 1000)
    const jwt15m = makeJwtWithExp(startSec + 900)
    const jwtRotated = makeJwtWithExp(startSec + 1800)
    sessionStorage.setItem('access_token', jwt15m)
    vi.mocked(authService.getToken).mockReturnValue(jwt15m)
    vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue(user)

    let refreshCount = 0
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        ++refreshCount
        return {
          data: { accessToken: jwtRotated },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      }
      return { data: {}, status: 200, statusText: 'OK', headers: new AxiosHeaders(), config }
    }

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/private']}>
          <TokenProbe />
        </MemoryRouter>
      </AuthProvider>,
    )

    await waitForAuthenticatedProbe(jwt15m)

    // Tab quay lại visible khi còn > 3 phút -> KHÔNG gọi refresh
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5 * 60_000)
      document.dispatchEvent(new Event('visibilitychange'))
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(refreshCount).toBe(0)

    // Logout trước phút 12 -> hủy timer
    await act(async () => {
      fireEvent.click(screen.getByText('logout-probe'))
      await vi.advanceTimersByTimeAsync(0)
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(10 * 60_000)
    })
    expect(refreshCount).toBe(0)
  })

  it('refreshes immediately on visibilitychange when <= 3m remain', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-18T12:00:00.000Z'))
    const startSec = Math.floor(Date.now() / 1000)
    // Còn 90s trước khi hết hạn (< 3m)
    const nearExpiryJwt = makeJwtWithExp(startSec + 90)
    const rotatedJwt = makeJwtWithExp(startSec + 990)
    sessionStorage.setItem('access_token', nearExpiryJwt)
    vi.mocked(authService.getToken).mockReturnValue(nearExpiryJwt)
    vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue(user)

    let refreshCount = 0
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        ++refreshCount
        return {
          data: { accessToken: rotatedJwt },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      }
      return { data: {}, status: 200, statusText: 'OK', headers: new AxiosHeaders(), config }
    }

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/private']}>
          <TokenProbe />
        </MemoryRouter>
      </AuthProvider>,
    )

    await waitForAuthenticatedProbe(nearExpiryJwt)
    expect(refreshCount).toBe(0)

    // Dispatch visibilitychange at 0ms elapsed — proves immediate trigger before the 1,000ms fallback timer
    await act(async () => {
      document.dispatchEvent(new Event('visibilitychange'))
      await vi.advanceTimersByTimeAsync(0)
    })

    expect(refreshCount).toBe(1)
    expect(screen.getByTestId('probe-token').textContent).toBe(rotatedJwt)
  })

  it('stops proactive refresh on idle non-exam routes after 15m inactivity across 42m', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-18T12:00:00.000Z'))
    const startSec = Math.floor(Date.now() / 1000)
    const jwt1 = makeJwtWithExp(startSec + 900)
    const jwt2 = makeJwtWithExp(startSec + 1800)
    const jwt3 = makeJwtWithExp(startSec + 2700)
    sessionStorage.setItem('access_token', jwt1)
    vi.mocked(authService.getToken).mockReturnValue(jwt1)
    vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue(user)

    let refreshCount = 0
    apiClient.defaults.adapter = async (config) => {
      if (config.url === '/auth/refresh-token') {
        ++refreshCount
        const nextToken = refreshCount === 1 ? jwt2 : jwt3
        return {
          data: { accessToken: nextToken },
          status: 200,
          statusText: 'OK',
          headers: new AxiosHeaders(),
          config,
        }
      }
      return { data: {}, status: 200, statusText: 'OK', headers: new AxiosHeaders(), config }
    }

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/dashboard']}>
          <TokenProbe />
        </MemoryRouter>
      </AuthProvider>,
    )

    await waitForAuthenticatedProbe(jwt1)

    // Chu kỳ 1 (~phút 12): còn trong cửa sổ hoạt động 15 phút -> refresh 1 lần
    await act(async () => {
      await vi.advanceTimersByTimeAsync(12 * 60_000 + 2_000)
    })
    expect(refreshCount).toBe(1)
    expect(screen.getByTestId('probe-token').textContent).toBe(jwt2)

    // Chu kỳ 2 (~phút 27) & Chu kỳ 3 (~phút 42): tab không có tương tác > 15 phút -> không refresh thêm
    await act(async () => {
      await vi.advanceTimersByTimeAsync(15 * 60_000)
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(15 * 60_000)
    })
    expect(refreshCount).toBe(1)
  })

  it('treats /lecturer/classes/:classId/in-class-shifts/:shiftId/results as an active exam route even when idle', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-18T12:00:00.000Z'))
    const originalPathname = window.location.pathname
    window.history.replaceState({}, '', '/lecturer/classes/cls-1/in-class-shifts/shift-1/results')

    try {
      const startSec = Math.floor(Date.now() / 1000)
      const jwt1 = makeJwtWithExp(startSec + 900)
      const jwt2 = makeJwtWithExp(startSec + 1800)
      const jwt3 = makeJwtWithExp(startSec + 2700)
      sessionStorage.setItem('access_token', jwt1)
      vi.mocked(authService.getToken).mockReturnValue(jwt1)
      vi.mocked(authService.synchronizeCurrentUser).mockResolvedValue({
        ...user,
        role: 'Instructor',
      })

      let refreshCount = 0
      apiClient.defaults.adapter = async (config) => {
        if (config.url === '/auth/refresh-token') {
          ++refreshCount
          const nextToken = refreshCount === 1 ? jwt2 : jwt3
          return {
            data: { accessToken: nextToken },
            status: 200,
            statusText: 'OK',
            headers: new AxiosHeaders(),
            config,
          }
        }
        return { data: {}, status: 200, statusText: 'OK', headers: new AxiosHeaders(), config }
      }

      render(
        <AuthProvider>
          <MemoryRouter initialEntries={['/lecturer/classes/cls-1/in-class-shifts/shift-1/results']}>
            <TokenProbe />
          </MemoryRouter>
        </AuthProvider>,
      )

      await waitForAuthenticatedProbe(jwt1)

      await act(async () => {
        await vi.advanceTimersByTimeAsync(12 * 60_000 + 2_000)
      })
      expect(refreshCount).toBe(1)
      expect(screen.getByTestId('probe-token').textContent).toBe(jwt2)

      await act(async () => {
        await vi.advanceTimersByTimeAsync(15 * 60_000)
      })
      expect(refreshCount).toBe(2)
      expect(screen.getByTestId('probe-token').textContent).toBe(jwt3)
    } finally {
      window.history.replaceState({}, '', originalPathname)
    }
  })
})
