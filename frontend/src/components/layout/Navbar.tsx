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
  '/lecturer/audit': 'Hậu kiểm & công bố điểm',
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
  const pageLabel = PAGE_LABELS[location.pathname] ?? 'Cổng giảng viên'
  const semesterLabel = getSemesterLabel()
  const roleLabel = user?.role === 'Admin' ? 'Quản trị viên' : 'Giảng viên'

  const handleLogout = () => {
    void logout().finally(() => navigate('/login', { replace: true }))
  }

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-[#e6ebf3] bg-white/92 px-4 shadow-[0_1px_0_rgba(15,35,65,0.02)] backdrop-blur-xl sm:px-6 xl:px-8">
      <div className="flex min-w-0 items-center gap-3">
        <button
          type="button"
          onClick={onOpenSidebar}
          className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-[#dce4ef] text-[#42536d] transition hover:border-[#b9c9de] hover:bg-[#f3f6fb] focus-visible:outline-2 focus-visible:outline-[#245eea] md:hidden"
          aria-label="Mở menu điều hướng"
        >
          <Menu className="size-5" aria-hidden="true" />
        </button>

        <div className="min-w-0">
          <div className="flex items-center gap-2 text-[10px] font-medium text-[#8492a7]">
            <span className="hidden sm:inline">Không gian giảng viên</span>
            <span className="hidden text-[#c2cad6] sm:inline" aria-hidden="true">
              /
            </span>
            <span className="truncate text-[#3d4f69]">{pageLabel}</span>
          </div>
          <p className="mt-0.5 hidden text-[10px] font-semibold text-[#245eea] sm:block">
            Học kỳ {semesterLabel}
          </p>
        </div>
      </div>

      <div className="flex items-center gap-2 sm:gap-3">
        <div className="hidden h-7 w-px bg-[#e5eaf1] sm:block" />

        <div className="flex items-center gap-2.5">
          <div className="hidden text-right sm:block">
            <p className="max-w-[180px] truncate text-[11px] font-bold text-[#17243a]">
              {user?.fullName ?? roleLabel}
            </p>
            <p className="mt-0.5 text-[9px] font-medium text-[#8a97aa]">
              {user?.email ?? roleLabel}
            </p>
          </div>

          <div
            className="flex size-9 items-center justify-center rounded-full bg-[#07172e] text-[10px] font-bold text-white shadow-[0_4px_14px_rgba(7,23,46,0.18)] ring-2 ring-white"
            aria-label={`Tài khoản ${user?.fullName ?? roleLabel}`}
          >
            {getInitials(user?.fullName ?? roleLabel) || 'GV'}
          </div>

          <button
            type="button"
            onClick={handleLogout}
            className="flex min-h-9 items-center gap-1.5 rounded-lg px-2 text-[10px] font-semibold text-[#66758a] transition hover:bg-red-50 hover:text-red-600 focus-visible:outline-2 focus-visible:outline-[#245eea] sm:px-3"
            aria-label="Đăng xuất"
          >
            <LogOut className="size-4" aria-hidden="true" />
            <span className="hidden lg:inline">Đăng xuất</span>
          </button>
        </div>
      </div>
    </header>
  )
}

export { Navbar as Header }
