import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { authService } from './auth.service'

const post = vi.hoisted(() => vi.fn())
vi.mock('./api.client', () => ({ default: { post } }))
beforeEach(() => { post.mockReset(); sessionStorage.clear() })
afterEach(() => { sessionStorage.clear() })

describe('Google transport contract', () => {
  it('uses credentials for the host-only nonce cookie', async () => {
    post.mockResolvedValue({ data: { nonce: 'n'.repeat(43), expiresIn: 300 } })
    await authService.createGoogleChallenge()
    expect(post).toHaveBeenCalledWith('/auth/google-challenge', null, expect.objectContaining({ withCredentials: true, _skipAuthRefresh: true }))
  })
  it('does not persist a credential response after its login page was aborted', async () => {
    let complete!: (value: unknown) => void
    post.mockReturnValue(new Promise(resolve => { complete = resolve }))
    const controller = new AbortController()
    const login = authService.googleLogin({ idToken: 'qa-token' }, controller.signal)
    controller.abort()
    complete({ data: { accessToken: 'new-token', refreshToken: 'new-refresh', user: { id: 'new-user', email: 'qa@fpt.edu.vn', fullName: 'QA', role: 'student' } } })
    await expect(login).rejects.toMatchObject({ name: 'AbortError' })
    expect(sessionStorage.getItem('access_token')).toBeNull()
    expect(sessionStorage.getItem('user_profile')).toBeNull()
  })
})
