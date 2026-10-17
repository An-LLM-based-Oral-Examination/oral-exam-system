import { useEffect } from 'react'
import { NavLink } from 'react-router-dom'
import {
  BookOpenCheck,
  Building2,
  ChevronLeft,
  ClipboardCheck,
  FilePlus2,
  LayoutDashboard,
  MonitorCheck,
  PanelLeftClose,
  PanelLeftOpen,
  Scale,
  ShieldCheck,
  X,
  type LucideIcon,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { useAuth } from '@/hooks/useAuth'
import type { UserRole } from '@/types/auth.types'
import { cn } from '@/lib/utils'

interface SidebarProps {
  collapsed: boolean
  mobileOpen: boolean
  onCollapse: () => void
  onMobileClose: () => void
}

interface RoleNavigationItem {
  label: string
  path: string
  icon: LucideIcon
  end?: boolean
}

const NAVIGATION_BY_ROLE: Record<Exclude<UserRole, 'Student'>, RoleNavigationItem[]> = {
  Instructor: [
    {
      label: 'Tổng quan',
      path: '/dashboard',
      icon: LayoutDashboard,
      end: true,
    },
    {
      label: 'Ngân hàng đề & Rubric',
      path: '/question-bank',
      icon: BookOpenCheck,
      end: true,
    },
    {
      label: 'Tạo câu hỏi',
      path: '/question-bank/create',
      icon: FilePlus2,
    },
    {
      label: 'Hậu kiểm & Công bố',
      path: '/lecturer/audit',
      icon: ClipboardCheck,
    },
  ],
  DepartmentHead: [
    {
      label: 'Cổng Trưởng Bộ môn',
      path: '/department-head',
      icon: Building2,
      end: true,
    },
    {
      label: 'Ngân hàng đề & Duyệt đề',
      path: '/question-bank',
      icon: BookOpenCheck,
      end: true,
    },
    {
      label: 'Tạo đề thi',
      path: '/question-bank/create',
      icon: FilePlus2,
    },
    {
      label: 'Hậu kiểm & Công bố',
      path: '/lecturer/audit',
      icon: ClipboardCheck,
    },
    {
      label: 'Tổng quan bộ môn',
      path: '/dashboard',
      icon: LayoutDashboard,
      end: true,
    },
  ],
  Proctor: [
    {
      label: 'Điểm danh & Giám sát Kiosk',
      path: '/proctor',
      icon: MonitorCheck,
      end: true,
    },
    {
      label: 'Tra cứu ca thi',
      path: '/lecturer/audit',
      icon: ClipboardCheck,
    },
  ],
  Admin: [
    {
      label: 'Quản trị Hệ thống',
      path: '/admin',
      icon: ShieldCheck,
      end: true,
    },
    {
      label: 'Cổng Trưởng Bộ môn',
      path: '/department-head',
      icon: Building2,
      end: true,
    },
    {
      label: 'Ngân hàng đề',
      path: '/question-bank',
      icon: BookOpenCheck,
      end: true,
    },
    {
      label: 'Hậu kiểm ca thi',
      path: '/lecturer/audit',
      icon: ClipboardCheck,
    },
  ],
}

const WORKSPACE_LABEL_BY_ROLE: Record<Exclude<UserRole, 'Student'>, string> = {
  Instructor: 'Không gian Giảng viên',
  DepartmentHead: 'Không gian Trưởng Bộ môn',
  Proctor: 'Không gian Giám thị Kiosk',
  Admin: 'Không gian Quản trị viên',
}

const WORKSPACE_FOOTER_BY_ROLE: Record<
  Exclude<UserRole, 'Student'>,
  { title: string; subtitle: string }
> = {
  Instructor: {
    title: 'Không gian Giảng viên',
    subtitle: 'Biên soạn đề thi & chấm duyệt bài làm.',
  },
  DepartmentHead: {
    title: 'Quản trị Bộ môn KTPM',
    subtitle: 'Duyệt ngân hàng đề, lịch thi & phúc khảo.',
  },
  Proctor: {
    title: 'Điều hành Phòng thi Lab',
    subtitle: 'Điểm danh thí sinh & giám sát ca thi.',
  },
  Admin: {
    title: 'Quản trị Hệ thống',
    subtitle: 'Quản lý tài khoản, học kỳ & cấu hình.',
  },
}

export function Sidebar({ collapsed, mobileOpen, onCollapse, onMobileClose }: SidebarProps) {
  const { user } = useAuth()
  const activeRole: Exclude<UserRole, 'Student'> =
    user?.role && user.role !== 'Student' ? user.role : 'Instructor'
  const navigationItems = NAVIGATION_BY_ROLE[activeRole]
  const workspaceLabel = WORKSPACE_LABEL_BY_ROLE[activeRole]
  const footerMeta = WORKSPACE_FOOTER_BY_ROLE[activeRole]

  useEffect(() => {
    if (!mobileOpen) return

    const previousOverflow = document.body.style.overflow
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onMobileClose()
    }

    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [mobileOpen, onMobileClose])

  return (
    <>
      <button
        type="button"
        aria-label="Đóng menu điều hướng"
        className={cn(
          'fixed inset-0 z-40 bg-slate-900/30 backdrop-blur-xs transition-opacity md:hidden',
          mobileOpen ? 'pointer-events-auto opacity-100' : 'pointer-events-none opacity-0',
        )}
        onClick={onMobileClose}
      />

      <aside
        aria-label={workspaceLabel}
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex w-60 flex-col overflow-hidden border-r border-[#E2E8F0] bg-white text-[#1E293B] shadow-xs transition-[width,transform] duration-200 ease-out md:translate-x-0',
          collapsed ? 'md:w-20' : 'md:w-56 xl:w-60',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-16 shrink-0 items-center gap-3 border-b border-[#E2E8F0] px-4">
          <div className="flex size-9 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-1">
            <img
              src={universityTechLogo}
              alt="FPT University"
              className="size-full object-contain"
              draggable={false}
            />
          </div>

          <div className={cn('min-w-0 transition-opacity', collapsed && 'md:hidden')}>
            <p className="truncate text-sm font-bold tracking-tight text-[#1E293B]">OralExam AI</p>
            <p className="truncate text-xs font-semibold text-[#F26F21]">{workspaceLabel}</p>
          </div>

          <button
            type="button"
            onClick={onMobileClose}
            className="ml-auto flex size-8 items-center justify-center rounded-lg text-[#64748B] transition hover:bg-[#FFF7ED] hover:text-[#C2410C] focus-visible:outline-2 focus-visible:outline-[#F26F21] md:hidden"
            aria-label="Đóng menu"
          >
            <X className="size-4" aria-hidden="true" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-3 py-4">
          <p
            className={cn(
              'mb-2 px-3 text-xs font-semibold tracking-wider text-[#94A3B8] uppercase',
              collapsed && 'md:hidden',
            )}
          >
            Nghiệp vụ khảo thí
          </p>

          <nav className="space-y-1" aria-label={`Các phân hệ ${workspaceLabel}`}>
            {navigationItems.map(({ label, path, icon: Icon, end }) => (
              <NavLink
                key={path}
                to={path}
                end={end}
                onClick={onMobileClose}
                title={collapsed ? label : undefined}
                className={({ isActive }) =>
                  cn(
                    'group relative flex min-h-10 items-center gap-3 overflow-hidden rounded-xl px-3 text-sm font-semibold transition-all duration-150 focus-visible:outline-2 focus-visible:outline-[#F26F21]',
                    isActive
                      ? 'bg-[#F26F21] text-white shadow-xs'
                      : 'text-[#475569] hover:bg-[#FFF7ED] hover:text-[#C2410C]',
                    collapsed && 'md:justify-center md:px-0',
                  )
                }
              >
                <Icon className="size-4 shrink-0" strokeWidth={2} aria-hidden="true" />
                <span className={cn('min-w-0 flex-1 truncate', collapsed && 'md:hidden')}>
                  {label}
                </span>
              </NavLink>
            ))}
          </nav>
        </div>

        <div className="shrink-0 border-t border-[#E2E8F0] p-3">
          <div
            className={cn(
              'mb-2 rounded-xl border border-[#FED7AA] bg-[#FFF7ED]/70 px-3 py-2.5',
              collapsed && 'md:hidden',
            )}
          >
            <div className="flex items-center gap-2 text-xs font-semibold text-[#9A3412]">
              <Scale className="size-3.5 text-[#F26F21]" aria-hidden="true" />
              {footerMeta.title}
            </div>
            <p className="mt-0.5 text-xs text-[#64748B]">{footerMeta.subtitle}</p>
          </div>

          <button
            type="button"
            onClick={onCollapse}
            className={cn(
              'hidden min-h-9 w-full items-center justify-center gap-2 rounded-lg text-xs font-semibold text-[#64748B] transition hover:bg-[#F8FAFC] hover:text-[#1E293B] focus-visible:outline-2 focus-visible:outline-[#F26F21] md:flex',
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
                <ChevronLeft className="size-4" aria-hidden="true" />
              </>
            )}
          </button>
        </div>
      </aside>
    </>
  )
}
