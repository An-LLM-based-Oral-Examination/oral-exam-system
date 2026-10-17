import { useLocation, useNavigate } from 'react-router-dom'
import { ChevronRight, LogOut, Menu } from 'lucide-react'
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
  '/exam': 'Lịch thi',
  '/results': 'Kết quả',
}

function getInitials(fullName: string) {
  const words = fullName.trim().split(/\s+/).filter(Boolean)
  return words
    .slice(-2)
    .map((word) => word.charAt(0).toLocaleUpperCase('vi-VN'))
    .join('')
}

export function StudentHeader({ onOpenSidebar }: StudentHeaderProps) {
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const pageLabel = PAGE_LABELS[location.pathname] ?? 'Cổng sinh viên'

  const handleLogout = () => {
    void logout().finally(() => navigate('/login', { replace: true }))
  }

  return (
    <header className="sticky top-0 z-30 border-b border-slate-200 bg-white px-4 sm:px-6 xl:px-8">
      <div className="mx-auto flex h-15 w-full max-w-6xl items-center justify-between">
        <div className="flex min-w-0 items-center gap-3">
          <button
            type="button"
            onClick={onOpenSidebar}
            className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-700 transition hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-[#F26F21] md:hidden cursor-pointer"
            aria-label="Mở menu điều hướng"
          >
            <Menu className="size-4" />
          </button>

          <nav aria-label="Đường dẫn trang" className="flex min-w-0 items-center gap-2 text-sm">
            <span className="hidden font-semibold text-slate-700 sm:inline">Cổng sinh viên</span>
            <ChevronRight className="hidden size-3.5 text-slate-400 sm:inline-block" />
            <span className="truncate font-bold text-slate-900">{pageLabel}</span>
          </nav>
        </div>

        <div className="flex items-center gap-2.5">
          <div className="flex items-center gap-2.5 rounded-xl border border-slate-200 bg-slate-50 py-1 pl-3 pr-1.5">
            <div className="hidden text-right sm:block">
              <p className="max-w-44 truncate text-xs font-bold text-slate-900">
                {user?.fullName ?? 'Sinh viên FPT'}
              </p>
              <p className="text-xs font-semibold text-slate-700">
                {user?.studentCode ?? user?.email ?? 'Student'}
              </p>
            </div>

            <div className="flex size-7.5 shrink-0 items-center justify-center rounded-lg bg-[#F26F21] text-xs font-bold text-white">
              {getInitials(user?.fullName ?? 'Sinh viên') || 'SV'}
            </div>
          </div>

          <button
            type="button"
            onClick={handleLogout}
            className="flex min-h-9 items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3 text-xs font-semibold text-slate-800 transition hover:bg-slate-50 hover:text-slate-950 focus-visible:outline-2 focus-visible:outline-[#F26F21] cursor-pointer"
            aria-label="Đăng xuất khỏi hệ thống"
            title="Đăng xuất"
          >
            <LogOut className="size-3.5" />
            <span className="hidden sm:inline">Đăng xuất</span>
          </button>
        </div>
      </div>
    </header>
  )
}
