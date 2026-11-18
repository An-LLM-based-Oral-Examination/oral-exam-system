import { PortalHeader, PortalSidebar, PortalSidebarBrand, PortalSidebarFooter } from '@/components/layout/PortalChrome'
import { StudentNotificationDrawer } from '@/components/student/StudentNotificationDrawer'
import { toast } from '@/components/common/toastStore'
import { useStudentNotifications } from '@/hooks/useStudentNotifications'
import { useEffect, useState, type ReactNode } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  Bell,
  CalendarDays,
  ChevronLeft,
  ClipboardCheck,
  FileBarChart2,
  LayoutDashboard,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  ScanSearch,
  ServerCog,
  TicketCheck,
  UserRound,
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
  { prefix: '/profile', label: 'Hồ sơ cá nhân', icon: UserRound },
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
  const [isNotificationOpen, setIsNotificationOpen] = useState(false)
  const hasNotifications = Boolean(user)
  const notifications = useStudentNotifications(hasNotifications && isNotificationOpen, hasNotifications)
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

  const handleNotificationSelect = async (notification: (typeof notifications.items)[number]) => {
    try {
      await notifications.markRead(notification)
    } catch {
      toast.error('Không thể cập nhật trạng thái thông báo.')
      return
    }
    setIsNotificationOpen(false)
  }

  return (
    <PortalHeader
      overlays={
        hasNotifications && (
          <div id="officer-notification-drawer">
            <StudentNotificationDrawer
              open={isNotificationOpen}
              onClose={() => setIsNotificationOpen(false)}
              notifications={notifications.items}
              unreadCount={notifications.unreadCount}
              isLoading={notifications.isLoading}
              isLoadingMore={notifications.isLoadingMore}
              error={notifications.error}
              hasMore={notifications.hasMore}
              portalLabel="Cổng Khảo thí"
              onRetry={() => void notifications.refresh()}
              onLoadMore={() => void notifications.loadMore()}
              onMarkAllRead={() => {
                void notifications.markAllRead().catch(() => {
                  toast.error('Không thể đánh dấu tất cả thông báo đã đọc.')
                })
              }}
              onSelect={(notification) => void handleNotificationSelect(notification)}
            />
          </div>
        )
      }
    >
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
          {hasNotifications && (
            <button
              type="button"
              onClick={() => setIsNotificationOpen(true)}
              className="relative flex size-10 items-center justify-center rounded-xl border border-[#4988C4] bg-white text-[#1C4D8D] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D]"
              aria-label="Thông báo"
              aria-expanded={isNotificationOpen}
              aria-controls="officer-notification-drawer"
            >
              <Bell className="size-[17px]" />
              {notifications.unreadCount > 0 ? (
                <span className="absolute -top-1 -right-1 flex min-h-4 min-w-4 items-center justify-center rounded-full bg-[#ef5c72] px-1 text-[9px] leading-none font-bold text-white ring-2 ring-white">
                  {notifications.unreadCount > 99 ? '99+' : notifications.unreadCount}
                </span>
              ) : null}
            </button>
          )}
          <button
            type="button"
            onClick={() => navigate('/profile')}
            className="flex items-center gap-2 rounded-full border border-[#4988C4] bg-white py-1 pr-1 pl-3 text-left transition hover:-translate-y-0.5 hover:bg-[#F4FBFD] hover:shadow-[0_8px_16px_rgba(15,40,84,0.12)] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] motion-reduce:transform-none"
            aria-label="Mở hồ sơ cá nhân"
          >
            <div className="hidden text-right sm:block">
              <p className="max-w-[180px] truncate text-xs font-bold text-[#0F2854]">
                {user?.fullName ?? 'Cán bộ Khảo thí'}
              </p>
              <p className="max-w-[180px] truncate text-xs font-medium text-[#1C4D8D]">
                {user?.email ?? 'examination_officer'}
              </p>
            </div>
            <div className="flex size-9 items-center justify-center rounded-full bg-[#0F2854] text-xs font-bold text-white">
              {initials || 'KT'}
            </div>
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
    </PortalHeader>
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
      <PortalSidebar
        aria-label="Điều hướng Cán bộ Khảo thí"
        className={cn(
          collapsed ? 'md:w-20' : 'md:w-[220px] xl:w-[240px]',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <PortalSidebarBrand>
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
        </PortalSidebarBrand>
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
        <PortalSidebarFooter>
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
        </PortalSidebarFooter>
      </PortalSidebar>
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
          className="px-4 pt-3 pb-5 focus:outline-none sm:px-6 xl:px-8"
        >
          <div className="mx-auto w-full max-w-[1380px]">{children ?? <Outlet />}</div>
        </main>
      </div>
    </div>
  )
}
