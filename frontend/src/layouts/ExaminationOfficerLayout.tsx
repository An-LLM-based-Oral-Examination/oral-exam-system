import { useEffect, useState, type ReactNode } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  CalendarDays,
  ChevronLeft,
  ClipboardCheck,
  FileBarChart2,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  ScanSearch,
  ServerCog,
  TicketCheck,
  X,
  type LucideIcon,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { useAuth } from '@/hooks/useAuth'
import { lockBodyScroll } from '@/design/bodyScrollLock'
import { cn } from '@/lib/utils'

interface OfficerNavItem {
  label: string
  path: string
  icon: LucideIcon
  end?: boolean
}

const navigation: OfficerNavItem[] = [
  { label: 'Tổng quan', path: '/examination-officer/dashboard', icon: LayoutDashboard, end: true },
  { label: 'Kỳ thi chính thức', path: '/examination-officer/exams', icon: CalendarDays },
  { label: 'Cấp vé thi', path: '/examination-officer/tickets/assign', icon: TicketCheck },
  { label: 'Giám sát ca thi', path: '/examination-officer/monitor', icon: ScanSearch },
  { label: 'Công bố điểm', path: '/examination-officer/grades', icon: FileBarChart2 },
  { label: 'Phúc khảo', path: '/examination-officer/appeals', icon: ClipboardCheck },
  { label: 'Lỗi chấm AI', path: '/examination-officer/dlq', icon: ServerCog },
]

const pageLabels: Array<{ prefix: string; label: string; icon: LucideIcon }> = [
  { prefix: '/examination-officer/dashboard', label: 'Tổng quan Khảo thí', icon: LayoutDashboard },
  { prefix: '/examination-officer/exams', label: 'Kỳ thi chính thức', icon: CalendarDays },
  { prefix: '/examination-officer/tickets', label: 'Cấp vé thi', icon: TicketCheck },
  { prefix: '/examination-officer/monitor', label: 'Giám sát ca thi', icon: ScanSearch },
  { prefix: '/examination-officer/grades', label: 'Công bố điểm', icon: FileBarChart2 },
  { prefix: '/examination-officer/appeals', label: 'Phúc khảo', icon: ClipboardCheck },
  { prefix: '/examination-officer/dlq', label: 'Lỗi chấm AI', icon: ServerCog },
]

function OfficerNavbar({ onOpenSidebar }: { onOpenSidebar: () => void }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const page = pageLabels.find((item) => location.pathname.startsWith(item.prefix)) ?? pageLabels[0]
  const Icon = page.icon
  const initials = (user?.fullName ?? 'Khảo thí')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((word) => word.charAt(0).toLocaleUpperCase('vi-VN'))
    .join('')

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
            className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-[#4988C4]/30 bg-[#BDE8F5] text-[#0F2854] md:hidden"
            aria-label="Mở menu Khảo thí"
          >
            <Menu className="size-5" aria-hidden="true" />
          </button>
          <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(28,77,141,0.28)]">
            <Icon className="size-5" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <p className="text-xs font-bold tracking-[0.12em] text-[#1C4D8D] uppercase">
              Cán bộ Khảo thí
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
                {user?.fullName ?? 'Cán bộ Khảo thí'}
              </p>
              <p className="max-w-[180px] truncate text-xs font-medium text-[#1C4D8D]">
                {user?.email ?? 'examination_officer'}
              </p>
            </div>
            <div
              className="flex size-9 items-center justify-center rounded-full bg-[#0F2854] text-xs font-bold text-white"
              aria-label={`Tài khoản ${user?.fullName ?? 'Cán bộ Khảo thí'}`}
            >
              {initials || 'KT'}
            </div>
          </div>
          <button
            type="button"
            onClick={() => navigate('/change-password')}
            className="flex h-10 items-center justify-center gap-1.5 rounded-xl border border-[#4988C4] bg-white px-3 text-xs font-bold text-[#1C4D8D]"
            aria-label="Đổi mật khẩu"
          >
            <KeyRound className="size-4" aria-hidden="true" />
            <span className="hidden lg:inline">Đổi mật khẩu</span>
          </button>
          <button
            type="button"
            onClick={handleLogout}
            className="flex size-10 items-center justify-center rounded-xl border border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]"
            aria-label="Đăng xuất"
          >
            <LogOut className="size-4" aria-hidden="true" />
          </button>
        </div>
      </div>
    </header>
  )
}

function OfficerSidebar({
  collapsed,
  mobileOpen,
  onCollapse,
  onMobileClose,
}: {
  collapsed: boolean
  mobileOpen: boolean
  onCollapse: () => void
  onMobileClose: () => void
}) {
  const { hasRole } = useAuth()
  useEffect(() => {
    if (!mobileOpen) return
    const unlock = lockBodyScroll()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onMobileClose()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => {
      unlock()
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [mobileOpen, onMobileClose])

  return (
    <>
      <button
        type="button"
        aria-label="Đóng menu Khảo thí"
        className={cn(
          'fixed inset-0 z-40 bg-[#0F2854]/55 backdrop-blur-[2px] transition-opacity md:hidden',
          mobileOpen ? 'pointer-events-auto opacity-100' : 'pointer-events-none opacity-0',
        )}
        onClick={onMobileClose}
      />
      <aside
        aria-label="Điều hướng Cán bộ Khảo thí"
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex w-[240px] flex-col overflow-hidden border-r border-[#4988C4] bg-[#D7F1F8] text-[#0F2854] shadow-[16px_0_40px_rgba(15,40,84,0.06)] transition-[width,transform] duration-300 ease-out md:translate-x-0',
          collapsed ? 'md:w-20' : 'md:w-[220px] xl:w-[240px]',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-[77px] shrink-0 items-center gap-3 border-b border-[#4988C4] bg-[#D7F1F8] px-4">
          <div className="flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-[#4988C4] bg-white p-0.5">
            <img
              src={universityTechLogo}
              alt="University Tech"
              className="size-full object-contain"
              draggable={false}
            />
          </div>
          <div className={cn('min-w-0', collapsed && 'md:hidden')}>
            <p className="text-xs font-bold tracking-[0.14em] text-[#1C4D8D] uppercase">
              Cổng Khảo thí
            </p>
            <p className="mt-0.5 truncate text-sm font-bold tracking-[-0.02em] text-[#0F2854]">
              OralExam AI
            </p>
          </div>
          <button
            type="button"
            onClick={onMobileClose}
            className="ml-auto flex size-9 items-center justify-center rounded-lg text-[#1C4D8D] md:hidden"
            aria-label="Đóng menu"
          >
            <X className="size-5" aria-hidden="true" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto overscroll-contain px-3 py-5">
          <nav className="space-y-1.5" aria-label="Các phân hệ Khảo thí">
            {navigation
              .filter(() => hasRole(['ExaminationOfficer']))
              .map(({ label, path, icon: Icon, end }) => (
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
                  <span className={cn('min-w-0 flex-1 truncate', collapsed && 'md:hidden')}>
                    {label}
                  </span>
                </NavLink>
              ))}
          </nav>
        </div>
        <div className="shrink-0 border-t border-[#4988C4] bg-[#D7F1F8] p-3">
          <button
            type="button"
            onClick={onCollapse}
            className={cn(
              'hidden min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-[#D7F1F8] text-xs font-semibold text-[#0F2854] md:flex',
              !collapsed && 'justify-between px-3',
            )}
            aria-label={collapsed ? 'Mở rộng thanh điều hướng' : 'Thu gọn thanh điều hướng'}
            aria-expanded={!collapsed}
          >
            {collapsed ? (
              <PanelLeftOpen className="size-4" aria-hidden="true" />
            ) : (
              <>
                <span className="flex items-center gap-2">
                  <PanelLeftClose className="size-4" aria-hidden="true" />
                  Thu gọn
                </span>
                <ChevronLeft className="size-3.5" aria-hidden="true" />
              </>
            )}
          </button>
        </div>
      </aside>
    </>
  )
}

export function ExaminationOfficerLayout({ children }: { children?: ReactNode }) {
  const [collapsed, setCollapsed] = useState(false)
  const [mobileOpen, setMobileOpen] = useState(false)
  return (
    <div className="min-h-dvh bg-[#FFFFFF] text-[#0F2854]">
      <a
        href="#examination-officer-main-content"
        className="fixed top-3 left-3 z-[70] -translate-y-20 rounded-lg bg-[#1C4D8D] px-4 py-2 text-sm font-bold text-white transition-transform focus:translate-y-0 focus:outline-none"
      >
        Chuyển đến nội dung chính
      </a>
      <OfficerSidebar
        collapsed={collapsed}
        mobileOpen={mobileOpen}
        onCollapse={() => setCollapsed((value) => !value)}
        onMobileClose={() => setMobileOpen(false)}
      />
      <div
        className={cn(
          'min-h-dvh transition-[padding] duration-300 ease-out',
          collapsed ? 'md:pl-20' : 'md:pl-[220px] xl:pl-[240px]',
        )}
      >
        <OfficerNavbar onOpenSidebar={() => setMobileOpen(true)} />
        <main
          id="examination-officer-main-content"
          tabIndex={-1}
          className="px-4 py-5 focus:outline-none sm:px-6 sm:py-5 xl:px-8"
        >
          <div className="mx-auto w-full max-w-[1380px]">{children ?? <Outlet />}</div>
        </main>
      </div>
    </div>
  )
}
