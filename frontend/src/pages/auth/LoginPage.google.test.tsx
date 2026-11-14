import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LoginPage } from './LoginPage'

const mocks = vi.hoisted(() => ({ challenge: vi.fn(), initialize: vi.fn(), googleLogin: vi.fn(), navigate: vi.fn() }))
vi.mock('@/hooks/useAuth', () => ({ useAuth: () => ({ loginWithGoogle: mocks.googleLogin, loginWithPassword: vi.fn() }) }))
vi.mock('@/services/auth.service', () => ({ authService: { createGoogleChallenge: mocks.challenge, getCurrentUser: () => null } }))
vi.mock('@/services/googleAuth.service', () => ({ googleAuthManager: {
  isLoaded: () => true, initialize: mocks.initialize,
  renderButton: (element: HTMLElement) => { element.replaceChildren(document.createElement('span')) },
} }))
vi.mock('react-router-dom', () => ({ useNavigate: () => mocks.navigate }))
vi.mock('@/components/common/toastStore', () => ({ toast: { error: vi.fn(), info: vi.fn() } }))
vi.mock('./LoginThreeScene', () => ({ LoginThreeScene: () => null }))

beforeEach(() => {
  vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'qa-client')
  mocks.challenge.mockReset(); mocks.initialize.mockReset(); mocks.googleLogin.mockReset(); mocks.navigate.mockReset()
  mocks.challenge.mockResolvedValue({ nonce: 'n'.repeat(43), expiresIn: 300 })
})
afterEach(() => { vi.unstubAllEnvs() })

describe('Google login challenge UI', () => {
  it('fetches the backend challenge before initializing GIS with the nonce', async () => {
    render(<LoginPage />)
    await waitFor(() => expect(mocks.initialize).toHaveBeenCalledWith('qa-client', expect.any(Function), 'n'.repeat(43)))
    expect(mocks.challenge).toHaveBeenCalledTimes(1)
  })
  it('shows a retryable challenge error instead of submitting an unbound credential', async () => {
    mocks.challenge.mockRejectedValueOnce(new Error('backend unavailable'))
    render(<LoginPage />)
    await screen.findByText('Không tạo được phiên đăng nhập Google. Vui lòng thử lại.')
    expect(mocks.initialize).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại Google' }))
    await waitFor(() => expect(mocks.initialize).toHaveBeenCalled())
  })
  it('renews the single-use challenge after a rejected credential', async () => {
    mocks.googleLogin.mockRejectedValue({ detail: 'Nonce expired' })
    mocks.challenge.mockResolvedValueOnce({ nonce: 'a'.repeat(43), expiresIn: 300 })
    render(<LoginPage />)
    await waitFor(() => expect(mocks.initialize).toHaveBeenCalled())
    await act(async () => { mocks.initialize.mock.calls[0][1]({ credential: 'qa-credential' }) })
    await waitFor(() => expect(mocks.challenge).toHaveBeenCalledTimes(2))
    expect(mocks.initialize.mock.calls.at(-1)?.[2]).toBe('n'.repeat(43))
  })
  it('does not navigate after a pending credential resolves following unmount', async () => {
    let complete!: () => void
    mocks.googleLogin.mockReturnValue(new Promise<void>(resolve => { complete = resolve }))
    const view = render(<LoginPage />)
    await waitFor(() => expect(mocks.initialize).toHaveBeenCalled())
    act(() => { mocks.initialize.mock.calls[0][1]({ credential: 'qa-credential' }) })
    view.unmount()
    await act(async () => { complete() })
    expect(mocks.navigate).not.toHaveBeenCalled()
  })
})
