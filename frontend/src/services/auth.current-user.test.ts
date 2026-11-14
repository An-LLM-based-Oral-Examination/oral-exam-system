import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { authService } from './auth.service'

const cached = { id: 'user-1', email: 'qa@example.test', fullName: 'Old', role: 'Admin' }
const profile = { ...cached, fullName: 'Fresh', role: 'student', username: 'qa', studentCode: null, isActive: true, mustChangePassword: true }
function session() {
  sessionStorage.setItem('access_token', 'token-1')
  sessionStorage.setItem('refresh_token', 'refresh-1')
  sessionStorage.setItem('user_profile', JSON.stringify(cached))
}
beforeEach(() => { sessionStorage.clear(); vi.restoreAllMocks(); session() })

describe('current server profile synchronization', () => {
  it.each([true, false])('retains fresh /me MustChangePassword=%s across later cache reads despite an older JWT claim', async mustChangePassword => {
    const token = `header.${btoa(JSON.stringify({ sub: cached.id, must_change_password: !mustChangePassword }))}.signature`
    sessionStorage.setItem('access_token', token)
    vi.spyOn(apiClient, 'get').mockResolvedValue({ data: { ...profile, mustChangePassword } })

    expect(await authService.synchronizeCurrentUser()).toMatchObject({ mustChangePassword })
    expect(authService.getCurrentUser()).toMatchObject({ mustChangePassword })
    expect(JSON.parse(sessionStorage.getItem('user_profile')!).mustChangePassword).toBe(mustChangePassword)
  })
  it('falls back to the JWT password-change flag only when the cached profile has no boolean flag', () => {
    sessionStorage.setItem('access_token', `header.${btoa(JSON.stringify({ must_change_password: true }))}.signature`)
    expect(authService.getCurrentUser()).toMatchObject({ mustChangePassword: true })
  })
  it.each(['student', 'lecturer', 'department_head', 'examination_officer', 'proctor', 'admin'])('normalizes %s from the server', async role => {
    vi.spyOn(apiClient, 'get').mockResolvedValue({ data: { ...profile, role } })
    const user = await authService.synchronizeCurrentUser()
    expect(user?.role).toBe(({ student: 'Student', lecturer: 'Instructor', department_head: 'DepartmentHead', examination_officer: 'ExaminationOfficer', proctor: 'Proctor', admin: 'Admin' })[role])
    expect(user?.mustChangePassword).toBe(true)
    expect(JSON.parse(sessionStorage.getItem('user_profile')!)).toMatchObject({ fullName: 'Fresh', role: user?.role })
  })
  it.each([
    { ...profile, isActive: false }, { ...profile, id: 'other-user' },
    { ...profile, role: 'unknown' }, { ...profile, fullName: null },
    { ...profile, mustChangePassword: 'false' }, { ...profile, isActive: undefined },
  ])('rejects inactive, wrong-identity or malformed profiles without overwriting cache', async data => {
    vi.spyOn(apiClient, 'get').mockResolvedValue({ data })
    await expect(authService.synchronizeCurrentUser()).rejects.toBeTruthy()
    expect(JSON.parse(sessionStorage.getItem('user_profile')!)).toEqual(cached)
  })
  it('retains tokens on network error for an explicit retry', async () => {
    vi.spyOn(apiClient, 'get').mockRejectedValue(new Error('offline'))
    await expect(authService.synchronizeCurrentUser()).rejects.toThrow('offline')
    expect(sessionStorage.getItem('access_token')).toBe('token-1')
  })
  it('drops a response after logout or a new account/session', async () => {
    vi.spyOn(apiClient, 'get').mockImplementation(async () => {
      sessionStorage.setItem('access_token', 'new-session')
      return { data: profile }
    })
    expect(await authService.synchronizeCurrentUser()).toBeNull()
    expect(JSON.parse(sessionStorage.getItem('user_profile')!)).toEqual(cached)
  })
  it('accepts an interceptor-refreshed token without confusing it with a new login', async () => {
    vi.spyOn(apiClient, 'get').mockImplementation(async () => {
      sessionStorage.setItem('access_token', 'rotated-token')
      return { data: profile, config: { headers: { Authorization: 'Bearer rotated-token' } } }
    })
    expect(await authService.synchronizeCurrentUser()).toMatchObject({ fullName: 'Fresh' })
  })
  it('does not persist after its caller invalidates the request', async () => {
    vi.spyOn(apiClient, 'get').mockResolvedValue({ data: profile })
    expect(await authService.synchronizeCurrentUser(() => false)).toBeNull()
    expect(JSON.parse(sessionStorage.getItem('user_profile')!)).toEqual(cached)
  })
})
