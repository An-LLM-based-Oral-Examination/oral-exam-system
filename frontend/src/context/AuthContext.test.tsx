import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './AuthContext'
import { useAuth } from '@/hooks/useAuth'
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
    fireEvent.click(await screen.findByRole('button', { name: /thử lại/i }))
    expect(await screen.findByText('private content')).toBeTruthy()
    expect(authService.clearSession).not.toHaveBeenCalled()
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
