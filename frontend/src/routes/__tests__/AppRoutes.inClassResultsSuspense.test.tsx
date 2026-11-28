import { cleanup, render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { appRoutes } from '@/routes/AppRoutes'
import type { User } from '@/types/auth.types'

vi.mock('@/pages/lecturer/InClassShiftResultsPage', () => ({
  InClassShiftResultsPage: () => (
    <main data-testid="in-class-shift-results-cold">Kết quả ca thi trên lớp</main>
  ),
}))

function createInstructorAuth(): AuthContextType {
  const user: User = {
    id: 'lecturer-suspense-1',
    email: 'lecturer@fe.edu.vn',
    fullName: 'Giảng viên kiểm thử',
    role: 'Instructor',
    mustChangePassword: false,
  }
  return {
    user,
    token: 'test-token',
    isAuthenticated: true,
    isStudent: false,
    isInstructor: true,
    isDepartmentHead: false,
    isExaminationOfficer: false,
    isProctor: false,
    isAdmin: false,
    loginWithPassword: async () => undefined,
    loginWithGoogle: async () => undefined,
    changePassword: async () => undefined,
    logout: async () => undefined,
    hasRole: (roles) => roles.includes('Instructor'),
  }
}

let activeRouter: ReturnType<typeof createMemoryRouter> | undefined

afterEach(() => {
  cleanup()
  activeRouter?.dispose()
  activeRouter = undefined
})

describe('appRoutes cold lazy render for /in-class-shifts/:shiftId/results', () => {
  it('renders /in-class-shifts/:shiftId/results cleanly behind Suspense without crashing into ErrorBoundary', async () => {
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ['/in-class-shifts/shift-cold-1/results'],
    })
    activeRouter = router

    render(
      <AuthContext.Provider value={createInstructorAuth()}>
        <RouterProvider router={router} />
      </AuthContext.Provider>,
    )

    expect(await screen.findByTestId('in-class-shift-results-cold')).toBeInTheDocument()
    expect(screen.queryByText('Đã xảy ra sự cố hiển thị')).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/in-class-shifts/shift-cold-1/results')
  })

  it('redirects /in-class-shifts/:shiftId/live into /results and renders cleanly without ErrorBoundary', async () => {
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ['/in-class-shifts/shift-live-2/live'],
    })
    activeRouter = router

    render(
      <AuthContext.Provider value={createInstructorAuth()}>
        <RouterProvider router={router} />
      </AuthContext.Provider>,
    )

    expect(await screen.findByTestId('in-class-shift-results-cold')).toBeInTheDocument()
    expect(screen.queryByText('Đã xảy ra sự cố hiển thị')).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/in-class-shifts/shift-live-2/results')
  })
})
