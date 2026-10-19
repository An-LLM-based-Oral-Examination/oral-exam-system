import { useLocation, useNavigate } from 'react-router-dom'
import {
  BookOpenCheck,
  ClipboardCheck,
  FilePlus2,
  LayoutDashboard,
  LogOut,
  Menu,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'

interface NavbarProps {
  onOpenSidebar: () => void
}

const PAGE_META: Record<string, { label: string; icon: LucideIcon }> = {
  '/dashboard': { label: 'Tổng quan', icon: LayoutDashboard },
  '/question-bank': { label: 'Ngân hàng đề & Rubric', icon: BookOpenCheck },
  '/question-bank/create': { label: 'Tạo câu hỏi & Rubric', icon: FilePlus2 },
  '/lecturer/audit': { label: 'Hậu kiểm & công bố điểm', icon: ClipboardCheck },
}

function getInitials(fullName: string) {
  const words = fullName.trim().split(/\s+/).filter(Boolean)
  return words
    .slice(-2)
    .map((word) => word.charAt(0).toLocaleUpperCase('vi-VN'))
    .join('')
}

function getSemesterLabel() {
  const now = new Date()
  const month = now.getMonth() + 1
  const semester = month <= 4 ? 'Spring' : month <= 8 ? 'Summer' : 'Fall'
  return `${semester} ${now.getFullYear()}`
}

export function Navbar({ onOpenSidebar }: NavbarProps) {
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const page = PAGE_META[location.pathname] ?? { label: 'Cổng giảng viên', icon: LayoutDashboard }
  const PageIcon = page.icon
  const semesterLabel = getSemesterLabel()
  const roleLabel =
    user?.role === 'Admin'
      ? 'Quản trị viên'
      : user?.role === 'DepartmentHead'
        ? 'Trưởng Bộ môn'
        : user?.role === 'Proctor'
          ? 'Giám thị'
          : 'Giảng viên'

  const handleLogout = () => {
    void logout().finally(() => navigate('/login', { replace: true }))
  }

  return (
    <header className="sticky top-0 z-30 bg-white px-4 pt-4 pb-3 sm:px-6 xl:px-8">
      <div className="flex h-16 items-center justify-between gap-3 rounded-2xl border border-[#4988C4] bg-white px-3 sm:px-4">
        <div className="flex min-w-0 items-center gap-3">
          <button
            type="button"
            onClick={onOpenSidebar}
            className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-[#4988C4]/30 bg-[#BDE8F5] text-[#0F2854] transition hover:bg-[#4988C4]/20 focus-visible:outline-2 focus-visible:outline-[#1C4D8D] md:hidden"
            aria-label="Mở menu điều hướng"
          >
            <Menu className="size-5" aria-hidden="true" />
          </button>

          <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(28,77,141,0.28)]">
            <PageIcon className="size-5" aria-hidden="true" />
          </span>

          <div className="min-w-0">
            <p className="text-xs font-bold tracking-[0.12em] text-[#1C4D8D] uppercase">
              {roleLabel} · Học kỳ {semesterLabel}
            </p>
            <h1 className="truncate text-lg leading-tight font-bold tracking-[-0.03em] text-[#0F2854]">
              {page.label}
            </h1>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <div className="flex items-center gap-2 rounded-full border border-[#4988C4] bg-white py-1 pr-1 pl-3">
            <div className="hidden text-right sm:block">
              <p className="max-w-[180px] truncate text-xs font-bold text-[#0F2854]">
                {user?.fullName ?? roleLabel}
              </p>
              <p className="max-w-[180px] truncate text-xs font-medium text-[#1C4D8D]">
                {user?.email ?? roleLabel}
              </p>
            </div>
            <div
              className="flex size-9 items-center justify-center rounded-full bg-[#0F2854] text-xs font-bold text-white"
              aria-label={`Tài khoản ${user?.fullName ?? roleLabel}`}
            >
              {getInitials(user?.fullName ?? roleLabel) || 'GV'}
            </div>
          </div>

          <button
            type="button"
            onClick={handleLogout}
            className="flex size-10 items-center justify-center rounded-xl border border-[#f3c2c8] bg-[#fdecec] text-[#c2415b] transition hover:bg-[#f8d5d8] focus-visible:outline-2 focus-visible:outline-[#c2415b]"
            aria-label="Đăng xuất"
          >
            <LogOut className="size-4" aria-hidden="true" />
          </button>
        </div>
      </div>
    </header>
  )
}

export { Navbar as Header }
