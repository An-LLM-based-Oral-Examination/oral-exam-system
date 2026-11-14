import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { AuthProvider } from './AuthContext'
import { useAuth } from '@/hooks/useAuth'

function AuthProbe() {
  const { isAuthenticated, user } = useAuth()
  return <output data-testid="auth-state">{isAuthenticated ? user?.role : 'anonymous'}</output>
}

describe('AuthProvider session lifecycle', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
  })

  it('clears the session after an access-token refresh failure event', async () => {
    sessionStorage.setItem('access_token', 'access-token')
    sessionStorage.setItem('refresh_token', 'refresh-token')
    sessionStorage.setItem(
      'user_profile',
      JSON.stringify({
        id: 'student-1',
        email: 'student@test',
        fullName: 'Student',
        role: 'Student',
        mustChangePassword: false,
      }),
    )

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )
    expect(screen.getByTestId('auth-state')).toHaveTextContent('Student')

    window.dispatchEvent(new Event('auth:session-expired'))
    await waitFor(() => expect(screen.getByTestId('auth-state')).toHaveTextContent('anonymous'))
    expect(sessionStorage.getItem('access_token')).toBeNull()
    expect(sessionStorage.getItem('refresh_token')).toBeNull()
    expect(sessionStorage.getItem('user_profile')).toBeNull()
  })

  it('promotes the current profile to must-change-password when the API signals the policy', async () => {
    sessionStorage.setItem('access_token', 'access-token')
    sessionStorage.setItem('refresh_token', 'refresh-token')
    sessionStorage.setItem(
      'user_profile',
      JSON.stringify({
        id: 'student-1',
        email: 'student@test',
        fullName: 'Student',
        role: 'Student',
        mustChangePassword: false,
      }),
    )

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )
    window.dispatchEvent(new Event('auth:must-change-password'))

    await waitFor(() => {
      expect(JSON.parse(sessionStorage.getItem('user_profile') ?? '{}')).toMatchObject({
        mustChangePassword: true,
      })
    })
    expect(screen.getByTestId('auth-state')).toHaveTextContent('Student')
  })

  it('clears the current tab when another tab claims a different identity', async () => {
    sessionStorage.setItem('access_token', 'access-token')
    sessionStorage.setItem('refresh_token', 'refresh-token')
    sessionStorage.setItem(
      'user_profile',
      JSON.stringify({
        id: 'student-1',
        email: 'student@test',
        fullName: 'Student',
        role: 'Student',
        mustChangePassword: false,
      }),
    )
    localStorage.setItem(
      'oes_active_identity',
      JSON.stringify({
        eventId: 'initial-login',
        type: 'LOGIN',
        userId: 'student-1',
        sourceTabId: 'other-tab',
        occurredAt: Date.now(),
      }),
    )

    render(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>,
    )
    expect(screen.getByTestId('auth-state')).toHaveTextContent('Student')

    const nextLogin = JSON.stringify({
      eventId: 'other-login',
      type: 'LOGIN',
      userId: 'instructor-1',
      sourceTabId: 'other-tab',
      occurredAt: Date.now(),
    })
    if (typeof BroadcastChannel !== 'undefined') {
      const channel = new BroadcastChannel('oes-auth')
      channel.postMessage(JSON.parse(nextLogin))
      channel.close()
    } else {
      window.dispatchEvent(
        new StorageEvent('storage', {
          key: 'oes_active_identity',
          oldValue: localStorage.getItem('oes_active_identity'),
          newValue: nextLogin,
        }),
      )
    }

    await waitFor(() => expect(screen.getByTestId('auth-state')).toHaveTextContent('anonymous'))
    await waitFor(() => expect(sessionStorage.getItem('user_profile')).toBeNull())
  })
})
