import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ProfilePage } from './ProfilePage'

const mockUseAuth = vi.fn()

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}))

vi.mock('@/pages/auth/ChangePasswordPage', () => ({
  ChangePasswordPage: () => <div data-testid="change-password-dialog" />,
}))

describe('ProfilePage layout & identity rendering (A1-T1-07)', () => {
  it('renders student heading in its own header row above the 3-column metadata grid so long emails never crush the name', () => {
    mockUseAuth.mockReturnValue({
      user: {
        id: 'student-1',
        fullName: 'Le Vu Hoang',
        email: 'hoanglvse170001@fpt.edu.vn',
        studentCode: 'SE170001',
        role: 'Student',
        loginMethod: 'password',
        createdAt: '2026-10-15T00:00:00Z',
      },
    })

    const { container } = render(<ProfilePage />)

    const heading = screen.getByRole('heading', { level: 2, name: 'Le Vu Hoang' })
    expect(heading).toBeVisible()
    expect(screen.getByText('hoanglvse170001@fpt.edu.vn')).toBeVisible()
    expect(screen.getByText('SE170001')).toBeVisible()
    expect(screen.getByText('Sinh viên')).toBeVisible()
    expect(screen.getByText('Tài khoản đang hoạt động')).toBeVisible()

    // Ensure the old side-by-side lg:min-w-[620px] grid that crushed the name column at 1280x720 is absent
    expect(container.querySelector('[class*="lg:min-w-[620px]"]')).toBeNull()
    const metadataGrid = screen.getByText('hoanglvse170001@fpt.edu.vn').closest('.grid')
    expect(metadataGrid).not.toBeNull()
    expect(metadataGrid?.contains(heading)).toBe(false)
  })
})
