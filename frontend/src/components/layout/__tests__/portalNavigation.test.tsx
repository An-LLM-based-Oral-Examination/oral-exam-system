import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { Sidebar } from '@/components/layout/Sidebar'
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
    isExaminationOfficer: role === 'ExaminationOfficer',
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
      [
        '/lecturer/dashboard',
        '/lecturer/classes',
        '/lecturer/question-bank',
        '/lecturer/exam-authoring',
        '/lecturer/audit',
        '/lecturer/appeals',
      ],
    ],
    [
      'DepartmentHead',
      [
        '/department-head/dashboard',
        '/department-head/courses',
        '/department-head/question-review',
        '/department-head/exam-structures',
      ],
    ],
    ['Proctor', ['/proctor/attendance']],
    [
      'Admin',
      [
        '/admin/dashboard',
        '/admin/users',
        '/admin/semesters',
        '/admin/courses',
        '/admin/configs',
        '/admin/notifications',
        '/admin/audit-logs',
        '/admin/dlq',
      ],
    ],
  ] as Array<[UserRole, string[]]>)('%s only exposes its own portal links', (role, allowedPaths) => {
    renderWithAuth(role, <Sidebar collapsed={false} mobileOpen={false} onCollapse={() => undefined} onMobileClose={() => undefined} />)

    const links = screen.getAllByRole('link').map((link) => link.getAttribute('href'))
    expect(new Set(links)).toEqual(new Set(allowedPaths))
  })

  it('student navigation contains only student actions', () => {
    renderWithAuth(
      'Student',
      <Sidebar collapsed={false} mobileOpen={false} onCollapse={() => undefined} onMobileClose={() => undefined} />,
    )

    const links = screen.getAllByRole('link').map((link) => link.getAttribute('href'))
    expect(links).toEqual(
      expect.arrayContaining(['/student/overview', '/classes', '/practice', '/mock-exam', '/results']),
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

  it('keeps mobile close button non-shrinking at 44px and nav items at min-h-11', () => {
    renderWithAuth(
      'Admin',
      <Sidebar collapsed={false} mobileOpen onCollapse={() => undefined} onMobileClose={() => undefined} />,
    )

    const closeButton = screen.getByRole('button', { name: 'Đóng menu' })
    expect(closeButton).toHaveClass('shrink-0', 'size-11')

    const navLinks = screen.getAllByRole('link')
    expect(navLinks.length).toBeGreaterThan(0)
    for (const link of navLinks) {
      expect(link).toHaveClass('min-h-11')
    }
  })
})
