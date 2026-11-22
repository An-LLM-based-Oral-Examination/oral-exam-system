import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  ErrorBoundary,
  RoleRouteErrorBoundary,
  RouterErrorElement,
} from '@/components/common/ErrorBoundary'
import { AuthContext, type AuthContextType } from '@/context/auth-context'
import { getRoleHomePath } from '@/routes/routeAccess'
import type { User, UserRole } from '@/types/auth.types'

let shouldThrow = true

function Bomb({ message = 'boom' }: { message?: string }) {
  if (shouldThrow) throw new Error(message)
  return <p>noi-dung-binh-thuong</p>
}

function createAuthValue(role: UserRole): AuthContextType {
  const user: User = {
    id: `boundary-${role.toLowerCase()}`,
    email: `${role.toLowerCase()}@boundary.test`,
    fullName: 'Boundary Test User',
    role,
    mustChangePassword: false,
  }
  return {
    user,
    token: 'boundary-token',
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

describe('ErrorBoundary tiếng Việt', () => {
  beforeEach(() => {
    shouldThrow = true
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('hiển thị thông báo tiếng Việt cùng hai nút khôi phục và không lộ chi tiết kỹ thuật', () => {
    render(
      <ErrorBoundary portalLabel="Cổng thử nghiệm">
        <Bomb message="SECRET_STACK_DETAIL" />
      </ErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText('Đã xảy ra sự cố hiển thị')).toBeInTheDocument()
    expect(screen.getByText('Cổng thử nghiệm')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Tải lại trang/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Về trang chủ/ })).toBeInTheDocument()
    expect(screen.queryByText(/SECRET_STACK_DETAIL/)).not.toBeInTheDocument()
  })

  it('nhận diện lỗi tải gói giao diện và hướng dẫn tải lại trang', () => {
    function ChunkFail(): never {
      throw new Error('Failed to fetch dynamically imported module: /assets/x.js')
    }
    render(
      <ErrorBoundary>
        <ChunkFail />
      </ErrorBoundary>,
    )
    expect(screen.getByText('Không tải được giao diện mới nhất')).toBeInTheDocument()
  })

  it('gọi onReload khi bấm "Tải lại trang"', () => {
    const onReload = vi.fn()
    render(
      <ErrorBoundary onReload={onReload}>
        <Bomb />
      </ErrorBoundary>,
    )
    fireEvent.click(screen.getByRole('button', { name: /Tải lại trang/ }))
    expect(onReload).toHaveBeenCalledTimes(1)
  })

  it('xóa trạng thái lỗi và gọi onNavigateHome khi bấm "Về trang chủ"', () => {
    const onNavigateHome = vi.fn(() => {
      shouldThrow = false
    })
    render(
      <ErrorBoundary onNavigateHome={onNavigateHome}>
        <Bomb />
      </ErrorBoundary>,
    )
    fireEvent.click(screen.getByRole('button', { name: /Về trang chủ/ }))
    expect(onNavigateHome).toHaveBeenCalledTimes(1)
    expect(screen.getByText('noi-dung-binh-thuong')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('tự hồi phục khi resetKey thay đổi (đổi route)', () => {
    const { rerender } = render(
      <ErrorBoundary resetKey="/a">
        <Bomb />
      </ErrorBoundary>,
    )
    expect(screen.getByRole('alert')).toBeInTheDocument()

    shouldThrow = false
    rerender(
      <ErrorBoundary resetKey="/b">
        <Bomb />
      </ErrorBoundary>,
    )
    expect(screen.getByText('noi-dung-binh-thuong')).toBeInTheDocument()
  })

  const roles: UserRole[] = [
    'Student',
    'Instructor',
    'DepartmentHead',
    'ExaminationOfficer',
    'Proctor',
    'Admin',
  ]

  it.each(roles)(
    'vai trò %s: lỗi bị cô lập trong vùng nội dung, giữ Sidebar và "Về trang chủ" đúng trang chủ vai trò',
    async (role) => {
      const homePath = getRoleHomePath(role)
      const router = createMemoryRouter(
        [
          {
            element: (
              <div>
                <nav aria-label="sidebar-vai-tro">Sidebar</nav>
                <Outlet />
              </div>
            ),
            children: [
              {
                element: <RoleRouteErrorBoundary portalLabel={`Cổng ${role}`} />,
                children: [
                  { path: '/bad', element: <Bomb /> },
                  { path: '/ok', element: <p>trang-ok</p> },
                  { path: homePath, element: <p>trang-chu-vai-tro</p> },
                ],
              },
            ],
          },
        ],
        { initialEntries: ['/bad'] },
      )

      render(
        <AuthContext.Provider value={createAuthValue(role)}>
          <RouterProvider router={router} />
        </AuthContext.Provider>,
      )

      expect(await screen.findByRole('alert')).toBeInTheDocument()
      expect(screen.getByLabelText('sidebar-vai-tro')).toBeInTheDocument()

      shouldThrow = false
      fireEvent.click(screen.getByRole('button', { name: /Về trang chủ/ }))

      await waitFor(() => expect(router.state.location.pathname).toBe(homePath))
      expect(await screen.findByText('trang-chu-vai-tro')).toBeInTheDocument()
      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
      expect(screen.getByLabelText('sidebar-vai-tro')).toBeInTheDocument()
    },
  )

  it('đổi mục trên Sidebar (đổi route) tự xóa lỗi mà không cần tải lại trang', async () => {
    const router = createMemoryRouter(
      [
        {
          element: <RoleRouteErrorBoundary portalLabel="Cổng Sinh viên" />,
          children: [
            { path: '/bad', element: <Bomb /> },
            { path: '/ok', element: <p>trang-ok</p> },
          ],
        },
      ],
      { initialEntries: ['/bad'] },
    )
    render(
      <AuthContext.Provider value={createAuthValue('Student')}>
        <RouterProvider router={router} />
      </AuthContext.Provider>,
    )
    expect(await screen.findByRole('alert')).toBeInTheDocument()

    shouldThrow = false
    await router.navigate('/ok')
    expect(await screen.findByText('trang-ok')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('errorElement gốc thay màn lỗi tiếng Anh mặc định của React Router', async () => {
    const router = createMemoryRouter(
      [
        {
          errorElement: <RouterErrorElement />,
          children: [
            { path: '/', element: <p>trang-goc</p> },
            { path: '/bad', element: <Bomb /> },
          ],
        },
      ],
      { initialEntries: ['/bad'] },
    )
    render(<RouterProvider router={router} />)

    expect(await screen.findByText('Đã xảy ra sự cố hiển thị')).toBeInTheDocument()
    expect(screen.queryByText(/Unexpected Application Error/i)).not.toBeInTheDocument()

    shouldThrow = false
    fireEvent.click(screen.getByRole('button', { name: /Về trang chủ/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(await screen.findByText('trang-goc')).toBeInTheDocument()
  })
})
