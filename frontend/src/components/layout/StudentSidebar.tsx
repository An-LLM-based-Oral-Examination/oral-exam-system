import { NavLink } from 'react-router-dom'
import {
  BarChart3,
  ChevronLeft,
  ClipboardCheck,
  LayoutDashboard,
  Mic2,
  PanelLeftClose,
  PanelLeftOpen,
  TimerReset,
  X,
  type LucideIcon,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { cn } from '@/lib/utils'

interface StudentSidebarProps {
  collapsed: boolean
  mobileOpen: boolean
  onCollapse: () => void
  onMobileClose: () => void
}

interface StudentNavigationItem {
  label: string
  path: string
  icon: LucideIcon
  end?: boolean
}

const STUDENT_NAVIGATION: StudentNavigationItem[] = [
  {
    label: 'Tổng quan',
    path: '/student/overview',
    icon: LayoutDashboard,
    end: true,
  },
  {
    label: 'Luyện tập',
    path: '/practice',
    icon: Mic2,
  },
  {
    label: 'Thi thử',
    path: '/mock-exam',
    icon: TimerReset,
  },
  {
    label: 'Làm bài thi',
    path: '/exam',
    icon: ClipboardCheck,
  },
  {
    label: 'Kết quả',
    path: '/results',
    icon: BarChart3,
  },
]

export function StudentSidebar({
  collapsed,
  mobileOpen,
  onCollapse,
  onMobileClose,
}: StudentSidebarProps) {
  return (
    <>
      <button
        type="button"
        aria-label="Đóng menu điều hướng"
        className={cn(
          'fixed inset-0 z-40 bg-[#0F2854]/55 backdrop-blur-[2px] transition-opacity md:hidden',
          mobileOpen ? 'pointer-events-auto opacity-100' : 'pointer-events-none opacity-0',
        )}
        onClick={onMobileClose}
      />

      <aside
        aria-label="Điều hướng sinh viên"
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex w-[240px] flex-col overflow-hidden border-r border-[#4988C4] bg-[#D7F1F8] text-[#0F2854] shadow-[16px_0_40px_rgba(15,40,84,0.06)] transition-[width,transform] duration-300 ease-out md:translate-x-0',
          collapsed ? 'md:w-20' : 'md:w-[220px] xl:w-[240px]',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex shrink-0 items-center gap-3 border-b border-[#4988C4] bg-[#D7F1F8] px-4 py-4">
          <div className="flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-[#4988C4] bg-white">
            <img
              src={universityTechLogo}
              alt="University Tech"
              className="size-full object-cover"
              draggable={false}
            />
          </div>

          <div className={cn('min-w-0', collapsed && 'md:hidden')}>
            <p className="text-xs font-bold tracking-[0.14em] text-[#1C4D8D] uppercase">
              Cổng sinh viên
            </p>
            <p className="mt-0.5 truncate text-sm font-bold tracking-[-0.02em] text-[#0F2854]">
              OralExam AI
            </p>
          </div>

          <button
            type="button"
            onClick={onMobileClose}
            className="ml-auto flex size-9 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#4988C4]/10 hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] md:hidden"
            aria-label="Đóng menu"
          >
            <X className="size-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-3 py-5">
          <nav className="space-y-1.5">
            {STUDENT_NAVIGATION.map(({ label, path, icon: Icon, end }) => (
              <NavLink
                key={path}
                to={path}
                end={end}
                onClick={onMobileClose}
                title={collapsed ? label : undefined}
                className={({ isActive }) =>
                  cn(
                    'group relative flex min-h-11 items-center gap-3 overflow-hidden rounded-xl px-3 text-sm font-semibold transition-all duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]',
                    isActive
                      ? 'bg-[#1C4D8D] text-white'
                      : 'bg-[#D7F1F8] text-[#1C4D8D] hover:bg-[#c5e8f3] hover:text-[#0F2854]',
                    collapsed && 'md:justify-center md:px-0',
                  )
                }
              >
                <Icon className="size-[18px] shrink-0" strokeWidth={1.9} aria-hidden="true" />
                <span className={cn('truncate', collapsed && 'md:hidden')}>{label}</span>
              </NavLink>
            ))}
          </nav>
        </div>

        <div className="shrink-0 border-t border-[#4988C4] bg-[#D7F1F8] p-3">
          <div
            className={cn(
              'mb-2 flex items-center gap-2 rounded-xl border border-[#b7e4cf] bg-[#e8f8f1] px-3 py-2.5',
              collapsed && 'md:hidden',
            )}
          >
            <span className="size-2 shrink-0 rounded-full bg-[#16836a]" />
            <div className="min-w-0">
              <p className="text-sm font-bold text-[#16836a]">AI sẵn sàng</p>
              <p className="mt-0.5 text-xs leading-snug text-[#16836a]/80">
                Micro được kiểm tra trước mỗi bài.
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onCollapse}
            className={cn(
              'hidden min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-[#D7F1F8] text-xs font-semibold text-[#0F2854] transition hover:bg-[#c5e8f3] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] md:flex',
              !collapsed && 'justify-between px-3',
            )}
            aria-label={collapsed ? 'Mở rộng thanh điều hướng' : 'Thu gọn thanh điều hướng'}
          >
            {collapsed ? (
              <PanelLeftOpen className="size-4" />
            ) : (
              <>
                <span className="flex items-center gap-2">
                  <PanelLeftClose className="size-4" />
                  Thu gọn
                </span>
                <ChevronLeft className="size-3.5" />
              </>
            )}
          </button>
        </div>
      </aside>
    </>
  )
}
