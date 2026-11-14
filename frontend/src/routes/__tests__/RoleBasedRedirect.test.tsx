import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { RoleBasedRedirect } from '@/routes/RoleBasedRedirect'
import type { User, UserRole } from '@/types/auth.types'

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{location.pathname}</output>
}

function createAuthValue(role: UserRole): AuthContextType {
  const user: User = {
    id: 'user-1',
    email: `${role.toLowerCase()}@example.test`,
    fullName: 'Test User',
    role,
    mustChangePassword: false,
  }

  return {
    user,
    token: 'token',
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
  }
}

describe('RoleBasedRedirect', () => {
  it.each([
    ['Instructor', '/lecturer/dashboard'],
    ['DepartmentHead', '/department-head/dashboard'],
    ['ExaminationOfficer', '/examination-officer/dashboard'],
    ['Proctor', '/proctor/attendance'],
    ['Admin', '/admin/dashboard'],
  ] as const)('redirects %s to %s', async (role, expectedPath) => {
    render(
      <AuthContext.Provider value={createAuthValue(role)}>
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route
              path="*"
              element={
                <>
                  <RoleBasedRedirect />
                  <LocationProbe />
                </>
              }
            />
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>,
    )

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(expectedPath))
  })
})
