import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { Sidebar } from '@/components/layout/Sidebar'
import { StudentSidebar } from '@/components/layout/StudentSidebar'
import { ExaminationOfficerLayout } from '@/layouts/ExaminationOfficerLayout'
import type { User, UserRole } from '@/types/auth.types'

function createAuthValue(role: UserRole): AuthContextType {
  const user: User = {
    id: `${role}-navigation-user`,
    email: `${role.toLowerCase()}@test.local`,
    fullName: 'Navigation Test User',
    role,
    mustChangePassword: false,
  }

  return {
    user,
    token: 'navigation-test-token',
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

function renderWithAuth(role: UserRole, element: ReactNode) {
  return render(
    <AuthContext.Provider value={createAuthValue(role)}>
      <MemoryRouter>{element}</MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('portal navigation boundaries', () => {
  it.each([
    [
      'Instructor',
      ['/lecturer/dashboard', '/lecturer/classes', '/lecturer/question-bank', '/lecturer/audit', '/lecturer/appeals'],
    ],
    [
      'DepartmentHead',
      ['/department-head/dashboard', '/department-head/question-review', '/department-head/appeals'],
    ],
    ['Proctor', ['/proctor/attendance']],
    ['Admin', ['/admin/dashboard', '/admin/dlq']],
  ] as Array<[UserRole, string[]]>)('%s only exposes its own portal links', (role, allowedPaths) => {
    renderWithAuth(role, <Sidebar collapsed={false} mobileOpen={false} onCollapse={() => undefined} onMobileClose={() => undefined} />)

    const links = screen.getAllByRole('link').map((link) => link.getAttribute('href'))
    expect(new Set(links)).toEqual(new Set([...allowedPaths, '/change-password']))
  })

  it('student navigation contains only student actions', () => {
    renderWithAuth(
      'Student',
      <StudentSidebar collapsed={false} mobileOpen={false} onCollapse={() => undefined} onMobileClose={() => undefined} />,
    )

    const links = screen.getAllByRole('link').map((link) => link.getAttribute('href'))
    expect(links).toEqual(
      expect.arrayContaining(['/student/overview', '/practice', '/mock-exam', '/in-class-exam', '/results', '/change-password']),
    )
    expect(links.some((path) => path?.startsWith('/lecturer/'))).toBe(false)
    expect(links.some((path) => path?.startsWith('/admin/'))).toBe(false)
    expect(links.some((path) => path?.startsWith('/examination-officer/'))).toBe(false)
  })

  it('examination officer navigation contains only MF-04 operations', () => {
    renderWithAuth('ExaminationOfficer', <ExaminationOfficerLayout />)

    const links = screen.getAllByRole('link').map((link) => link.getAttribute('href'))
    expect(links).toEqual(
      expect.arrayContaining([
        '/examination-officer/dashboard',
        '/examination-officer/exams',
        '/examination-officer/tickets/assign',
        '/examination-officer/monitor',
        '/examination-officer/grades',
        '/examination-officer/appeals',
        '/examination-officer/dlq',
      ]),
    )
    expect(links.some((path) => path?.startsWith('/lecturer/'))).toBe(false)
    expect(links.some((path) => path?.startsWith('/admin/'))).toBe(false)
    expect(links.some((path) => path?.startsWith('/department-head/'))).toBe(false)
  })
})
