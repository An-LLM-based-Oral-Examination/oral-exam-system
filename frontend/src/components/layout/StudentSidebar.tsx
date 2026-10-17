import { useEffect, useMemo, useState } from 'react'
import { NavLink } from 'react-router-dom'
import {
  BarChart3,
  CalendarDays,
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
import {
  officialExamService,
  WORKSTATION_MODE_CHANGE_EVENT,
  type WorkstationEnvironmentMode,
} from '@/services/officialExam.service'

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

export function StudentSidebar({
  collapsed,
  mobileOpen,
  onCollapse,
  onMobileClose,
}: StudentSidebarProps) {
  const [envMode, setEnvMode] = useState<WorkstationEnvironmentMode>(() =>
    officialExamService.getWorkstationEnvMode(),
  )
  const [hasDetectedLabTicket, setHasDetectedLabTicket] = useState(false)

  useEffect(() => {
    let isActive = true
    void officialExamService
      .getCurrentWorkstationTicket()
      .then(() => {
        if (isActive) setHasDetectedLabTicket(true)
      })
      .catch(() => {
        if (isActive) setHasDetectedLabTicket(false)
      })

    const handleModeChange = () => {
      setEnvMode(officialExamService.getWorkstationEnvMode())
    }
    window.addEventListener(WORKSTATION_MODE_CHANGE_EVENT, handleModeChange)
    return () => {
      isActive = false
      window.removeEventListener(WORKSTATION_MODE_CHANGE_EVENT, handleModeChange)
    }
  }, [])

  const isLabWorkstation =
    envMode === 'lab-kiosk' || (envMode === 'auto' && hasDetectedLabTicket)

  const navigationItems = useMemo<StudentNavigationItem[]>(
    () => [
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
      isLabWorkstation
        ? {
            label: 'Phòng thi',
            path: '/exam',
            icon: ClipboardCheck,
          }
        : {
            label: 'Lịch thi',
            path: '/exam',
            icon: CalendarDays,
          },
      {
        label: 'Kết quả',
        path: '/results',
        icon: BarChart3,
      },
    ],
    [isLabWorkstation],
  )

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
        aria-label="Điều hướng sinh viên"
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex w-56 flex-col overflow-hidden border-r border-slate-200 bg-white text-slate-900 transition-[width,transform] duration-200 ease-out md:translate-x-0',
          collapsed ? 'md:w-18' : 'md:w-56',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-15 shrink-0 items-center gap-3 border-b border-slate-200 px-4">
          <div className="flex size-8.5 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-slate-200 bg-slate-50 p-1">
            <img
              src={universityTechLogo}
              alt="FPT University"
              className="size-full object-contain"
              draggable={false}
            />
          </div>

          <div className={cn('min-w-0 transition-opacity', collapsed && 'md:hidden')}>
            <p className="truncate text-sm font-semibold text-slate-900">OralExam AI</p>
            <p className="text-xs text-slate-500">Cổng sinh viên</p>
          </div>

          <button
            type="button"
            onClick={onMobileClose}
            className="ml-auto flex size-8 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-[#F26F21] md:hidden"
            aria-label="Đóng menu"
          >
            <X className="size-4" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-3 py-4">
          <nav className="space-y-1">
            {navigationItems.map(({ label, path, icon: Icon, end }) => (
              <NavLink
                key={path}
                to={path}
                end={end}
                onClick={onMobileClose}
                title={collapsed ? label : undefined}
                className={({ isActive }) =>
                  cn(
                    'group flex min-h-10 items-center gap-3 rounded-xl px-3 text-sm transition-colors focus-visible:outline-2 focus-visible:outline-[#F26F21]',
                    isActive
                      ? 'bg-orange-50 text-[#C2410C] font-semibold'
                      : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900 font-medium',
                    collapsed && 'md:justify-center md:px-0',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <Icon
                      className={cn(
                        'size-4.5 shrink-0 transition-colors',
                        isActive ? 'text-[#F26F21]' : 'text-slate-500 group-hover:text-slate-800',
                      )}
                      aria-hidden="true"
                    />
                    <span className={cn('truncate', collapsed && 'md:hidden')}>{label}</span>
                  </>
                )}
              </NavLink>
            ))}
          </nav>
        </div>

        <div className="shrink-0 border-t border-slate-200 p-3">
          <button
            type="button"
            onClick={onCollapse}
            className={cn(
              'hidden min-h-9 w-full items-center justify-center gap-2 rounded-xl text-xs font-medium text-slate-500 transition hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-[#F26F21] md:flex cursor-pointer',
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
                <ChevronLeft className="size-4" />
              </>
            )}
          </button>
        </div>
      </aside>
    </>
  )
}
