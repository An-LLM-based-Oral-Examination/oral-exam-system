import { useLocation, useNavigate } from 'react-router-dom'
import { Bell, LogOut, Menu } from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'

interface StudentHeaderProps {
  onOpenSidebar: () => void
}

const PAGE_LABELS: Record<string, string> = {
  '/student/overview': 'Tổng quan',
  '/practice': 'Luyện tập',
  '/practice/session': 'Phiên luyện tập',
  '/mock-exam': 'Thi thử',
  '/mock-exam/session': 'Phiên thi thử',
  '/exam': 'Làm bài thi',
  '/results': 'Kết quả',
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

export function StudentHeader({ onOpenSidebar }: StudentHeaderProps) {
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const pageLabel = PAGE_LABELS[location.pathname] ?? 'Cổng sinh viên'
  const semesterLabel = getSemesterLabel()

  const handleLogout = () => {
    logout()
    navigate('/login', { replace: true })
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
          <Menu className="size-5" />
        </button>

        <div className="min-w-0">
          <div className="flex items-center gap-2 text-[10px] font-medium text-[#8492a7]">
            <span className="hidden sm:inline">Trang chủ</span>
            <span className="hidden text-[#c2cad6] sm:inline">/</span>
            <span className="truncate text-[#3d4f69]">{pageLabel}</span>
          </div>
          <p className="mt-0.5 hidden text-[10px] font-semibold text-[#245eea] sm:block">
            Học kỳ {semesterLabel}
          </p>
        </div>
      </div>

      <div className="flex items-center gap-2 sm:gap-3">
        <div className="hidden items-center gap-1.5 rounded-full border border-[#dfe8f4] bg-[#f7faff] px-3 py-1.5 text-[9px] font-semibold text-[#5b6d86] lg:flex">
          <span className="size-1.5 rounded-full bg-[#42cf91] shadow-[0_0_7px_rgba(66,207,145,0.7)]" />
          Hệ thống AI sẵn sàng
        </div>

        <button
          type="button"
          className="relative flex size-9 items-center justify-center rounded-full text-[#64748b] transition hover:bg-[#f1f5fa] hover:text-[#245eea] focus-visible:outline-2 focus-visible:outline-[#245eea]"
          aria-label="Thông báo"
        >
          <Bell className="size-[17px]" />
          <span className="absolute top-1.5 right-1.5 size-1.5 rounded-full bg-[#ef5c72] ring-2 ring-white" />
        </button>

        <div className="hidden h-7 w-px bg-[#e5eaf1] sm:block" />

        <div className="flex items-center gap-2.5">
          <div className="hidden text-right sm:block">
            <p className="max-w-[180px] truncate text-[11px] font-bold text-[#17243a]">
              {user?.fullName ?? 'Sinh viên'}
            </p>
            <p className="mt-0.5 text-[9px] font-medium text-[#8a97aa]">
              {user?.studentCode ?? user?.email ?? 'Student'}
            </p>
          </div>

          <div className="flex size-9 items-center justify-center rounded-full bg-[#07172e] text-[10px] font-bold text-white shadow-[0_4px_14px_rgba(7,23,46,0.18)] ring-2 ring-white">
            {getInitials(user?.fullName ?? 'Sinh viên') || 'SV'}
          </div>

          <button
            type="button"
            onClick={handleLogout}
            className="flex min-h-9 items-center gap-1.5 rounded-lg px-2 text-[10px] font-semibold text-[#66758a] transition hover:bg-red-50 hover:text-red-600 focus-visible:outline-2 focus-visible:outline-[#245eea] sm:px-3"
            aria-label="Đăng xuất"
          >
            <LogOut className="size-4" />
            <span className="hidden lg:inline">Đăng xuất</span>
          </button>
        </div>
      </div>
    </header>
  )
}
