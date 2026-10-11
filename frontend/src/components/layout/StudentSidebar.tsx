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
          'fixed inset-0 z-40 bg-[#06142b]/55 backdrop-blur-[2px] transition-opacity md:hidden',
          mobileOpen ? 'pointer-events-auto opacity-100' : 'pointer-events-none opacity-0',
        )}
        onClick={onMobileClose}
      />

      <aside
        aria-label="Điều hướng sinh viên"
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex w-[240px] flex-col overflow-hidden border-r border-white/8 bg-[#07172e] text-white shadow-[16px_0_50px_rgba(4,15,35,0.12)] transition-[width,transform] duration-300 ease-out md:translate-x-0',
          collapsed ? 'md:w-20' : 'md:w-[220px] xl:w-[240px]',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-[76px] shrink-0 items-center gap-3 border-b border-white/8 px-4">
          <div className="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-white shadow-[0_6px_24px_rgba(37,99,235,0.24)]">
            <img
              src={universityTechLogo}
              alt="University Tech"
              className="size-full object-cover"
              draggable={false}
            />
          </div>

          <div className={cn('min-w-0 transition-opacity', collapsed && 'md:hidden')}>
            <p className="truncate text-sm font-bold tracking-[-0.02em]">OralExam AI</p>
            <p className="mt-0.5 text-[9px] font-semibold tracking-[0.14em] text-[#91a6c4] uppercase">
              University Tech
            </p>
          </div>

          <button
            type="button"
            onClick={onMobileClose}
            className="ml-auto flex size-9 items-center justify-center rounded-lg text-[#9fb0c9] transition hover:bg-white/10 hover:text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#5ac8fa] md:hidden"
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
                    'group relative flex min-h-11 items-center gap-3 overflow-hidden rounded-xl px-3 text-sm font-semibold transition-all duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#60d8ff]',
                    isActive
                      ? 'bg-[#245eea] text-white shadow-[0_10px_30px_rgba(36,94,234,0.28)]'
                      : 'text-[#b5c2d5] hover:bg-white/7 hover:text-white',
                    collapsed && 'md:justify-center md:px-0',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <span
                      className={cn(
                        'absolute inset-y-2 left-0 w-0.5 rounded-full bg-[#67e8f9] opacity-0 transition-opacity',
                        isActive && 'opacity-100',
                      )}
                    />
                    <Icon className="size-[18px] shrink-0" strokeWidth={1.9} aria-hidden="true" />
                    <span className={cn('truncate', collapsed && 'md:hidden')}>{label}</span>
                  </>
                )}
              </NavLink>
            ))}
          </nav>
        </div>

        <div className="shrink-0 border-t border-white/8 p-3">
          <div
            className={cn(
              'mb-3 rounded-xl border border-[#2b4362] bg-[#0b203d] p-3',
              collapsed && 'md:hidden',
            )}
          >
            <div className="flex items-center gap-2 text-[10px] font-semibold text-[#d3e1f4]">
              <span className="size-1.5 rounded-full bg-[#36d39a] shadow-[0_0_9px_rgba(54,211,154,0.8)]" />
              Hệ thống AI sẵn sàng
            </div>
            <p className="mt-1.5 text-[9px] leading-relaxed text-[#7890af]">
              Micro và kết nối sẽ được kiểm tra trước mỗi bài thi.
            </p>
          </div>

          <button
            type="button"
            onClick={onCollapse}
            className={cn(
              'hidden min-h-10 w-full items-center justify-center gap-2 rounded-lg text-xs font-semibold text-[#8499b6] transition hover:bg-white/7 hover:text-white focus-visible:outline-2 focus-visible:outline-[#60d8ff] md:flex',
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
