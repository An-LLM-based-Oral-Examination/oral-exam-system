import { useLocation, useNavigate } from 'react-router-dom'
import {
  BarChart3,
  Bell,
  ClipboardCheck,
  LayoutDashboard,
  LogOut,
  Menu,
  Mic2,
  TimerReset,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'

interface StudentHeaderProps {
  onOpenSidebar: () => void
}

const PAGE_META: Record<string, { label: string; icon: LucideIcon }> = {
  '/student/overview': { label: 'Tổng quan', icon: LayoutDashboard },
  '/practice': { label: 'Luyện tập', icon: Mic2 },
  '/practice/session': { label: 'Phiên luyện tập', icon: Mic2 },
  '/mock-exam': { label: 'Thi thử', icon: TimerReset },
  '/mock-exam/session': { label: 'Phiên thi thử', icon: TimerReset },
  '/exam': { label: 'Làm bài thi', icon: ClipboardCheck },
  '/results': { label: 'Kết quả', icon: BarChart3 },
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
  const page = PAGE_META[location.pathname] ?? { label: 'Cổng sinh viên', icon: LayoutDashboard }
  const PageIcon = page.icon
  const semesterLabel = getSemesterLabel()

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
            <Menu className="size-5" />
          </button>

          <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(28,77,141,0.28)]">
            <PageIcon className="size-5" aria-hidden="true" />
          </span>

          <div className="min-w-0">
            <p className="text-xs font-bold tracking-[0.12em] text-[#1C4D8D] uppercase">
              Học kỳ {semesterLabel}
            </p>
            <h1 className="truncate text-lg leading-tight font-bold tracking-[-0.03em] text-[#0F2854]">
              {page.label}
            </h1>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <div className="hidden items-center gap-1.5 rounded-full border border-[#b7e4cf] bg-[#e8f8f1] px-3 py-1.5 text-sm font-semibold text-[#16836a] lg:flex">
            <span className="size-1.5 rounded-full bg-[#16836a]" />
            AI sẵn sàng
          </div>

          <button
            type="button"
            className="relative flex size-10 items-center justify-center rounded-xl border border-[#4988C4] bg-white text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
            aria-label="Thông báo"
          >
            <Bell className="size-[17px]" />
            <span className="absolute top-2 right-2 size-1.5 rounded-full bg-[#ef5c72] ring-2 ring-white" />
          </button>

          <div className="flex items-center gap-2 rounded-full border border-[#4988C4] bg-white py-1 pr-1 pl-3">
            <div className="hidden text-right sm:block">
              <p className="max-w-[160px] truncate text-xs font-bold text-[#0F2854]">
                {user?.fullName ?? 'Sinh viên'}
              </p>
              <p className="max-w-[160px] truncate text-xs font-medium text-[#1C4D8D]">
                {user?.studentCode ?? user?.email ?? 'Student'}
              </p>
            </div>
            <div className="flex size-9 items-center justify-center rounded-full bg-[#0F2854] text-xs font-bold text-white">
              {getInitials(user?.fullName ?? 'Sinh viên') || 'SV'}
            </div>
          </div>

          <button
            type="button"
            onClick={handleLogout}
            className="flex size-10 items-center justify-center rounded-xl border border-[#f3c2c8] bg-[#fdecec] text-[#c2415b] transition hover:bg-[#f8d5d8] focus-visible:outline-2 focus-visible:outline-[#c2415b]"
            aria-label="Đăng xuất"
          >
            <LogOut className="size-4" />
          </button>
        </div>
      </div>
    </header>
  )
}
