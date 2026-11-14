import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { ProtectedRoute } from '@/routes/ProtectedRoute'
import type { User, UserRole } from '@/types/auth.types'

function createAuthValue(role: UserRole, overrides: Partial<AuthContextType> = {}): AuthContextType {
  const user: User = {
    id: `${role}-user`,
    email: `${role.toLowerCase()}@example.test`,
    fullName: 'Route Matrix Test User',
    role,
    mustChangePassword: false,
  }

  return {
    user,
    token: 'test-token',
    isAuthenticated: true,
    isStudent: role === 'Student',
    isInstructor: role === 'Instructor',
    isDepartmentHead: role === 'DepartmentHead',
    isProctor: role === 'Proctor',
    isAdmin: role === 'Admin',
    loginWithPassword: async () => undefined,
    loginWithGoogle: async () => undefined,
    changePassword: async () => undefined,
    logout: async () => undefined,
    hasRole: (roles) => roles.includes(role),
    ...overrides,
  }
}

function PortalMarker() {
  return <p>authorized portal</p>
}

function UnauthorizedMarker() {
  return <p>unauthorized</p>
}

function renderProtectedRoute(
  role: UserRole,
  allowedRoles: UserRole[],
  overrides: Partial<AuthContextType> = {},
) {
  return render(
    <AuthContext.Provider value={createAuthValue(role, overrides)}>
      <MemoryRouter initialEntries={['/protected']}>
        <Routes>
          <Route element={<ProtectedRoute allowedRoles={allowedRoles} />}>
            <Route path="/protected" element={<PortalMarker />} />
          </Route>
          <Route path="/unauthorized" element={<UnauthorizedMarker />} />
          <Route path="/login" element={<p>login</p>} />
          <Route path="/change-password" element={<p>change password</p>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('ProtectedRoute RBAC guard', () => {
  it('renders the protected portal for an allowed role', () => {
    renderProtectedRoute('DepartmentHead', ['DepartmentHead'])

    expect(screen.getByText('authorized portal')).toBeInTheDocument()
    expect(screen.queryByText('unauthorized')).not.toBeInTheDocument()
  })

  const forbiddenRoleCases: Array<[UserRole, UserRole[]]> = [
    ['Instructor', ['DepartmentHead']],
    ['DepartmentHead', ['ExaminationOfficer']],
    ['ExaminationOfficer', ['Admin']],
    ['Admin', ['Instructor']],
  ]

  it.each(forbiddenRoleCases)(
    'redirects %s away from a route owned by %s',
    (role, allowedRoles) => {
      renderProtectedRoute(role, allowedRoles)

      expect(screen.getByText('unauthorized')).toBeInTheDocument()
      expect(screen.queryByText('authorized portal')).not.toBeInTheDocument()
    },
  )

  it('redirects an unauthenticated user to login before mounting the protected page', () => {
    renderProtectedRoute('Student', ['Student'], {
      user: null,
      token: null,
      isAuthenticated: false,
    })

    expect(screen.getByText('login')).toBeInTheDocument()
    expect(screen.queryByText('authorized portal')).not.toBeInTheDocument()
  })

  it('forces password change before evaluating the business route', () => {
    const authValue = createAuthValue('Instructor')
    renderProtectedRoute('Instructor', ['Instructor'], {
      user: authValue.user ? { ...authValue.user, mustChangePassword: true } : null,
    })

    expect(screen.getByText('change password')).toBeInTheDocument()
    expect(screen.queryByText('authorized portal')).not.toBeInTheDocument()
  })
})
