import { useLocation, useNavigate } from 'react-router-dom'
import { LogOut, Menu } from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'

interface NavbarProps {
  onOpenSidebar: () => void
}

const PAGE_LABELS: Record<string, string> = {
  '/dashboard': 'Tổng quan',
  '/question-bank': 'Ngân hàng đề & Rubric',
  '/question-bank/create': 'Tạo câu hỏi & Rubric',
  '/lecturer/audit': 'Hậu kiểm & Công bố điểm',
  '/department-head': 'Cổng Trưởng Bộ môn',
  '/proctor': 'Điểm danh & Giám sát Kiosk',
  '/admin': 'Quản trị Hệ thống',
}

function getInitials(fullName: string) {
  const words = fullName.trim().split(/\s+/).filter(Boolean)
  return words
    .slice(-2)
    .map((word) => word.charAt(0).toLocaleUpperCase('vi-VN'))
    .join('')
}

export function Navbar({ onOpenSidebar }: NavbarProps) {
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const pageLabel = PAGE_LABELS[location.pathname] ?? 'Cổng khảo thí'
  const roleLabel =
    user?.role === 'Admin'
      ? 'Quản trị viên'
      : user?.role === 'DepartmentHead'
        ? 'Trưởng Bộ môn'
        : user?.role === 'Proctor'
          ? 'Giám thị Kiosk'
          : 'Giảng viên'

  const workspaceLabel =
    user?.role === 'Admin'
      ? 'Không gian Quản trị viên'
      : user?.role === 'DepartmentHead'
        ? 'Không gian Trưởng Bộ môn'
        : user?.role === 'Proctor'
          ? 'Không gian Giám thị Kiosk'
          : 'Không gian Giảng viên'

  const handleLogout = () => {
    void logout().finally(() => navigate('/login', { replace: true }))
  }

  return (
    <header className="sticky top-0 z-30 flex h-14 items-center justify-between border-b border-[#E2E8F0] bg-white/95 px-4 backdrop-blur-md sm:px-6 xl:px-8">
      <div className="flex min-w-0 items-center gap-3">
        <button
          type="button"
          onClick={onOpenSidebar}
          className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-[#E2E8F0] text-[#1E293B] transition hover:bg-[#FFF7ED] hover:text-[#C2410C] focus-visible:outline-2 focus-visible:outline-[#F26F21] md:hidden"
          aria-label="Mở menu điều hướng"
        >
          <Menu className="size-4" aria-hidden="true" />
        </button>

        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <div className="flex items-center gap-2 text-xs font-medium text-[#64748B]">
            <span className="hidden sm:inline">{workspaceLabel}</span>
            <span className="hidden text-[#CBD5E1] sm:inline" aria-hidden="true">
              /
            </span>
            <span className="truncate text-sm font-bold text-[#1E293B]">{pageLabel}</span>
          </div>
          <span className="hidden items-center rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-0.5 text-xs font-semibold text-[#C2410C] sm:inline-flex">
            FA26
          </span>
        </div>
      </div>

      <div className="flex items-center gap-3">
        <span className="hidden rounded-lg border border-[#FED7AA] bg-[#FFF7ED] px-2.5 py-1 text-xs font-semibold text-[#C2410C] md:inline-flex">
          {roleLabel}
        </span>

        <div className="hidden text-right sm:block">
          <p className="max-w-48 truncate text-xs font-bold text-[#1E293B]">
            {user?.fullName ?? roleLabel}
          </p>
          <p className="text-xs font-medium text-[#64748B]">{user?.email ?? roleLabel}</p>
        </div>

        <div
          className="flex size-8 items-center justify-center rounded-full border border-[#FED7AA] bg-[#FFF7ED] text-xs font-bold text-[#C2410C]"
          aria-label={`Tài khoản ${user?.fullName ?? roleLabel}`}
        >
          {getInitials(user?.fullName ?? roleLabel) || 'GV'}
        </div>

        <button
          type="button"
          onClick={handleLogout}
          className="flex min-h-8 items-center gap-1.5 rounded-lg border border-[#E2E8F0] bg-white px-2.5 text-xs font-semibold text-[#475569] transition hover:border-red-200 hover:bg-red-50 hover:text-[#E11D48] focus-visible:outline-2 focus-visible:outline-[#F26F21]"
          aria-label="Đăng xuất"
        >
          <LogOut className="size-3.5" aria-hidden="true" />
          <span className="hidden lg:inline">Đăng xuất</span>
        </button>
      </div>
    </header>
  )
}

export { Navbar as Header }
