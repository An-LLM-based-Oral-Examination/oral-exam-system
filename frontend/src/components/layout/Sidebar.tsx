import { PortalSidebar, PortalSidebarBrand, PortalSidebarFooter } from '@/components/layout/PortalChrome'
import { useEffect } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import {
  BarChart3,
  Bell,
  Mic2,
  TimerReset,
  BookOpenCheck,
  BookOpen,
  CalendarDays,
  ChevronLeft,
  ClipboardCheck,
  FileCheck2,
  FileText,
  LayoutDashboard,
  PanelLeftClose,
  PanelLeftOpen,
  ServerCog,
  SlidersHorizontal,
  UsersRound,
  X,
  type LucideIcon,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { lockBodyScroll } from '@/design/bodyScrollLock'
import { cn } from '@/lib/utils'
import { useAuth } from '@/hooks/useAuth'
import { canonicalRole, type CanonicalRole } from '@/routes/routeAccess'
import { ROUTE_PATHS } from '@/routes/routePaths'

interface SidebarProps {
  collapsed: boolean
  mobileOpen: boolean
  onCollapse: () => void
  onMobileClose: () => void
}

interface InstructorNavigationItem {
  label: string
  path: string
  icon: LucideIcon
  end?: boolean
}

const PORTAL_NAVIGATION: Record<CanonicalRole, InstructorNavigationItem[]> = {
  Student: [
  {
    label: 'Tổng quan',
    path: '/student/overview',
    icon: LayoutDashboard,
    end: true,
  },
  {
    label: 'Quản lý lớp học',
    path: '/classes',
    icon: UsersRound,
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
    label: 'Kết quả',
    path: '/results',
    icon: BarChart3,
  },
  ],
  Instructor: [
    { label: 'Tổng quan', path: ROUTE_PATHS.lecturer.dashboard, icon: LayoutDashboard, end: true },
    { label: 'Quản lý lớp học', path: ROUTE_PATHS.lecturer.classes, icon: UsersRound },
    { label: 'Câu hỏi luyện tập', path: ROUTE_PATHS.lecturer.questionBank, icon: BookOpenCheck, end: true },
    { label: 'Soạn đề thi', path: '/lecturer/exam-authoring', icon: FileCheck2 },
    { label: 'Hậu kiểm thi thật', path: ROUTE_PATHS.lecturer.audit, icon: FileCheck2 },
    { label: 'Chấm phúc khảo', path: ROUTE_PATHS.lecturer.appeals, icon: ClipboardCheck },
  ],
  DepartmentHead: [
    { label: 'Tổng quan', path: ROUTE_PATHS.departmentHead.dashboard, icon: LayoutDashboard, end: true },
    { label: 'Quản lý môn học', path: ROUTE_PATHS.departmentHead.courses, icon: BookOpen },
    { label: 'Quản lý câu hỏi', path: ROUTE_PATHS.departmentHead.questionReview, icon: FileCheck2 },
    { label: 'Chuẩn bị đề thi', path: '/department-head/exam-structures', icon: FileCheck2 },
  ],
  ExaminationOfficer: [],
  Proctor: [
    { label: 'Điểm danh', path: ROUTE_PATHS.proctor.attendance, icon: ClipboardCheck, end: true },
  ],
  Admin: [
    { label: 'Tổng quan', path: ROUTE_PATHS.admin.dashboard, icon: LayoutDashboard, end: true },
    { label: 'Người dùng', path: ROUTE_PATHS.admin.users, icon: UsersRound },
    { label: 'Học kỳ', path: ROUTE_PATHS.admin.semesters, icon: CalendarDays },
    { label: 'Môn học & CLO', path: ROUTE_PATHS.admin.courses, icon: BookOpen },
    { label: 'Cấu hình MF-01', path: ROUTE_PATHS.admin.configs, icon: SlidersHorizontal },
    { label: 'Thông báo 3NF', path: ROUTE_PATHS.admin.notifications, icon: Bell },
    { label: 'Nhật ký kiểm toán', path: ROUTE_PATHS.admin.auditLogs, icon: FileText },
    { label: 'Lỗi chấm MF-01/02/05', path: ROUTE_PATHS.admin.dlq, icon: ServerCog },
  ],
}

export function Sidebar({ collapsed, mobileOpen, onCollapse, onMobileClose }: SidebarProps) {
  const location = useLocation()
  const { user } = useAuth()
  const portalRole = user ? canonicalRole(user.role) : null
  const visibleNavigation = portalRole ? PORTAL_NAVIGATION[portalRole] : []
  const portalTitle =
    portalRole === 'Student' ? 'Cổng sinh viên' : portalRole === 'DepartmentHead'
      ? 'Cổng Trưởng BM'
      : portalRole === 'Admin'
        ? 'Cổng quản trị'
        : portalRole === 'Proctor'
          ? 'Cổng Giám thị'
          : portalRole === 'ExaminationOfficer'
            ? 'Cổng Khảo thí'
            : 'Cổng giảng viên'
  useEffect(() => {
    if (!mobileOpen) return

    const unlock = lockBodyScroll()
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onMobileClose()
    }

    document.addEventListener('keydown', handleKeyDown)

    return () => {
      unlock()
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [mobileOpen, onMobileClose])

  return (
    <>
      <button
        type="button"
        aria-label="Đóng menu điều hướng"
        className={cn(
          'fixed inset-0 z-40 bg-[#0F2854]/55 backdrop-blur-[2px] transition-opacity motion-reduce:transition-none md:hidden',
          mobileOpen ? 'pointer-events-auto opacity-100' : 'pointer-events-none opacity-0',
        )}
        onClick={onMobileClose}
      />

      <PortalSidebar
        aria-label="Điều hướng portal"
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
              {portalTitle}
            </p>
            <p className="mt-0.5 truncate text-base font-bold tracking-[-0.02em] text-[#0F2854]">
              OralExam AI
            </p>
          </div>

          <button
            type="button"
            onClick={onMobileClose}
            className="ml-auto flex size-11 shrink-0 items-center justify-center rounded-lg text-[#1C4D8D] transition-colors duration-150 hover:bg-[#4988C4]/10 hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none md:hidden"
            aria-label="Đóng menu"
          >
            <X className="size-5" strokeWidth={1.5} aria-hidden="true" />
          </button>
        </PortalSidebarBrand>

        <div className="flex-1 overflow-y-auto overscroll-contain px-3 py-3">

          <nav className="space-y-1.5" aria-label="Các phân hệ portal">
            {visibleNavigation.map(({ label, path, icon: Icon, end }) => (
              <NavLink
                key={path}
                to={path}
                end={end}
                onClick={onMobileClose}
                title={collapsed ? label : undefined}
                className={({ isActive }) =>
                  cn(
                    'group relative flex min-h-11 items-center gap-3 overflow-hidden rounded-xl px-3 text-sm font-semibold transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none',
                    (isActive || (path === ROUTE_PATHS.departmentHead.questionReview && location.pathname === ROUTE_PATHS.departmentHead.questionBank))
                      ? 'bg-[#1C4D8D] text-white'
                      : 'bg-[#D7F1F8] text-[#1C4D8D] hover:bg-[#c5e8f3] hover:text-[#0F2854]',
                    collapsed && 'md:justify-center md:px-0',
                  )
                }
              >
                <Icon className="size-[18px] shrink-0" strokeWidth={1.5} aria-hidden="true" />
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
              'hidden min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-[#4988C4] bg-[#D7F1F8] text-xs font-semibold text-[#0F2854] transition motion-reduce:transition-none hover:bg-[#c5e8f3] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] md:flex',
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
