import { useEffect } from 'react'
import { NavLink } from 'react-router-dom'
import {
  BookOpenCheck,
  ChevronLeft,
  ClipboardCheck,
  FilePlus2,
  LayoutDashboard,
  PanelLeftClose,
  PanelLeftOpen,
  Scale,
  X,
  type LucideIcon,
} from 'lucide-react'
import universityTechLogo from '@/assets/university-tech-logo.png'
import { cn } from '@/lib/utils'

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

const INSTRUCTOR_NAVIGATION: InstructorNavigationItem[] = [
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
    label: 'Hậu kiểm & công bố',
    path: '/lecturer/audit',
    icon: ClipboardCheck,
  },
]

export function Sidebar({ collapsed, mobileOpen, onCollapse, onMobileClose }: SidebarProps) {
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
          'fixed inset-0 z-40 bg-[#0F2854]/55 backdrop-blur-[2px] transition-opacity md:hidden',
          mobileOpen ? 'pointer-events-auto opacity-100' : 'pointer-events-none opacity-0',
        )}
        onClick={onMobileClose}
      />

      <aside
        aria-label="Điều hướng giảng viên"
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
              Cổng giảng viên
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
            <X className="size-5" aria-hidden="true" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-3 py-5">
          <p
            className={cn(
              'mb-2 px-3 text-xs font-bold tracking-[0.15em] text-[#1C4D8D] uppercase',
              collapsed && 'md:hidden',
            )}
          >
            Nghiệp vụ giảng viên
          </p>

          <nav className="space-y-1.5" aria-label="Các phân hệ giảng viên">
            {INSTRUCTOR_NAVIGATION.map(({ label, path, icon: Icon, end }) => (
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
          <div
            className={cn(
              'mb-2 flex items-start gap-2 rounded-xl border border-[#4988C4] bg-[#D7F1F8] px-3 py-2.5',
              collapsed && 'md:hidden',
            )}
          >
            <Scale className="mt-0.5 size-3.5 shrink-0 text-[#1C4D8D]" aria-hidden="true" />
            <div className="min-w-0">
              <p className="text-sm font-bold text-[#0F2854]">Chốt chặn học thuật</p>
              <p className="mt-0.5 text-xs leading-snug text-[#1C4D8D]">
                Rubric đủ 10.0 điểm và cần Trưởng Bộ môn phê duyệt.
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
