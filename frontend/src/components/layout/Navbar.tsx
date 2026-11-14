import { useLocation, useNavigate } from 'react-router-dom'
import {
  BookOpenCheck,
  ClipboardCheck,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Menu,
  ServerCog,
  ShieldAlert,
  TicketCheck,
  UsersRound,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'

interface NavbarProps {
  onOpenSidebar: () => void
}

const PAGE_META: Record<string, { label: string; icon: LucideIcon }> = {
  '/dashboard': { label: 'Tổng quan', icon: LayoutDashboard },
  '/lecturer/classes': { label: 'Quản lý lớp học', icon: UsersRound },
  '/question-bank': { label: 'Câu hỏi luyện tập', icon: BookOpenCheck },
  '/question-bank/create': { label: 'Câu hỏi luyện tập', icon: BookOpenCheck },
  '/lecturer/audit': { label: 'Hậu kiểm điểm', icon: ClipboardCheck },
  '/official-exams/manage': { label: 'Kỳ thi chính thức', icon: TicketCheck },
  '/appeals': { label: 'Phúc khảo', icon: ClipboardCheck },
  '/proctor/attendance': { label: 'Điểm danh', icon: UsersRound },
  '/dlq': { label: 'Lỗi chấm AI', icon: ServerCog },
  '/admin': { label: 'Quản trị hệ thống', icon: ShieldAlert },
  '/change-password': { label: 'Đổi mật khẩu', icon: KeyRound },
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
  const page =
    PAGE_META[location.pathname] ??
    (location.pathname.startsWith('/lecturer/classes') || location.pathname.startsWith('/in-class-shifts')
      ? { label: 'Quản lý lớp học', icon: UsersRound }
      : { label: 'Cổng giảng viên', icon: LayoutDashboard })
  const PageIcon = page.icon
  const semesterLabel = getSemesterLabel()
  const roleLabel =
    user?.role === 'Admin'
      ? 'Quản trị viên'
      : user?.role === 'DepartmentHead'
        ? 'Trưởng Bộ môn'
        : user?.role === 'ExaminationOfficer'
          ? 'Cán bộ Khảo thí'
          : user?.role === 'Proctor'
            ? 'Giám thị'
            : 'Giảng viên'

  const handleLogout = () => {
    void logout().finally(() => navigate('/login', { replace: true }))
  }

  return (
    <header className="sticky top-0 z-30 flex h-[77px] items-stretch bg-white px-4 py-2 sm:px-6 xl:px-8">
      <div className="flex min-h-0 w-full flex-1 items-center justify-between gap-3 rounded-2xl border border-[#4988C4] bg-white px-3 sm:px-4">
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
            onClick={() => navigate('/change-password')}
            title="Đổi mật khẩu"
            className="flex h-10 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
            aria-label="Đổi mật khẩu"
          >
            <KeyRound className="size-4 shrink-0" aria-hidden="true" />
            <span className="hidden lg:inline">Đổi mật khẩu</span>
          </button>

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
