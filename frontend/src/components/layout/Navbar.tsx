import { useState } from 'react'
import { useStudentNotifications } from '@/hooks/useStudentNotifications'
import { StudentNotificationDrawer } from '@/components/student/StudentNotificationDrawer'
import { toast } from '@/components/common/toastStore'
import { resolveStudentNotificationRoute } from '@/utils/notificationDeepLink'
import { PortalHeader } from '@/components/layout/PortalChrome'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { canonicalRole } from '@/routes/routeAccess'
import {
  BarChart3,
  Bell,
  Mic2,
  TimerReset,
  BookOpenCheck,
  BookOpen,
  CalendarDays,
  FileCheck2,
  FileText,
  ClipboardCheck,
  LayoutDashboard,
  LogOut,
  Menu,
  ServerCog,
  ShieldAlert,
  SlidersHorizontal,
  TicketCheck,
  UserRound,
  UsersRound,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { ROUTE_PATHS } from '@/routes/routePaths'

interface NavbarProps {
  onOpenSidebar: () => void
}

const PAGE_META: Record<string, { label: string; icon: LucideIcon }> = {
  '/student/overview': { label: 'Tổng quan học tập', icon: LayoutDashboard },
  '/in-class-exam': { label: 'Làm kiểm tra', icon: ClipboardCheck },
  '/classes': { label: 'Lớp học của tôi', icon: UsersRound },
  '/practice': { label: 'Luyện tập vấn đáp', icon: Mic2 },
  '/practice/session': { label: 'Phiên luyện tập', icon: Mic2 },
  '/mock-exam': { label: 'Thi thử tính giờ', icon: TimerReset },
  '/mock-exam/session': { label: 'Phiên thi thử', icon: TimerReset },
  '/exam': { label: 'Phòng thi chính thức', icon: ClipboardCheck },
  '/results': { label: 'Lịch sử & Kết quả', icon: BarChart3 },
  '/dashboard': { label: 'Tổng quan', icon: LayoutDashboard },
  [ROUTE_PATHS.lecturer.dashboard]: { label: 'Tổng quan', icon: LayoutDashboard },
  '/lecturer/classes': { label: 'Quản lý lớp học', icon: UsersRound },
  [ROUTE_PATHS.lecturer.questionBank]: { label: 'Câu hỏi luyện tập', icon: BookOpenCheck },
  [ROUTE_PATHS.lecturer.createQuestion]: { label: 'Câu hỏi luyện tập', icon: BookOpenCheck },
  '/question-bank': { label: 'Câu hỏi luyện tập', icon: BookOpenCheck },
  '/question-bank/create': { label: 'Câu hỏi luyện tập', icon: BookOpenCheck },
  '/lecturer/audit': { label: 'Hậu kiểm điểm', icon: ClipboardCheck },
  [ROUTE_PATHS.lecturer.appeals]: { label: 'Phúc khảo được giao', icon: ClipboardCheck },
  [ROUTE_PATHS.departmentHead.dashboard]: { label: 'Tổng quan', icon: LayoutDashboard },
  [ROUTE_PATHS.departmentHead.questionReview]: { label: 'Quản lý câu hỏi', icon: BookOpenCheck },
  [ROUTE_PATHS.departmentHead.questionBank]: { label: 'Quản lý câu hỏi', icon: BookOpenCheck },
  [ROUTE_PATHS.departmentHead.appeals]: { label: 'Quản lý phúc khảo', icon: ClipboardCheck },
  [ROUTE_PATHS.departmentHead.courses]: { label: 'Quản lý môn học', icon: BookOpen },
  '/department-head/lecturer-assignments': { label: 'Phân công giảng viên', icon: UsersRound },
  '/department-head/clos': { label: 'Chuẩn đầu ra (CLO)', icon: BookOpenCheck },
  '/lecturer/exam-authoring': { label: 'Soạn đề thi', icon: FileCheck2 },
  '/department-head/exam-structures': { label: 'Chuẩn bị đề thi', icon: FileCheck2 },
  '/official-exams/manage': { label: 'Kỳ thi chính thức', icon: TicketCheck },
  '/appeals': { label: 'Phúc khảo', icon: ClipboardCheck },
  '/proctor/attendance': { label: 'Điểm danh', icon: UsersRound },
  '/dlq': { label: 'Lỗi chấm AI', icon: ServerCog },
  '/admin': { label: 'Tổng quan quản trị', icon: LayoutDashboard },
  [ROUTE_PATHS.admin.dashboard]: { label: 'Tổng quan quản trị', icon: LayoutDashboard },
  [ROUTE_PATHS.admin.users]: { label: 'Quản lý người dùng', icon: UsersRound },
  [ROUTE_PATHS.admin.semesters]: { label: 'Học kỳ & Môn mở', icon: CalendarDays },
  [ROUTE_PATHS.admin.courses]: { label: 'Môn học, CTĐT & CLO', icon: BookOpen },
  [ROUTE_PATHS.admin.configs]: { label: 'Cấu hình MF-01', icon: SlidersHorizontal },
  [ROUTE_PATHS.admin.notifications]: { label: 'Thông báo 3NF', icon: Bell },
  [ROUTE_PATHS.admin.auditLogs]: { label: 'Nhật ký kiểm toán', icon: FileText },
  [ROUTE_PATHS.admin.dlq]: { label: 'Lỗi chấm MF-01/02/05', icon: ServerCog },
  '/profile': { label: 'Hồ sơ cá nhân', icon: UserRound },
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

export function Navbar({ onOpenSidebar }: NavbarProps) {
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const normalizedRole = user ? canonicalRole(user.role) : null
  const isStudent = normalizedRole === 'Student'
  const hasNotifications = Boolean(user)
  const [isNotificationOpen, setIsNotificationOpen] = useState(false)
  const notifications = useStudentNotifications(hasNotifications && isNotificationOpen, hasNotifications)
  const page =
    PAGE_META[location.pathname] ??
    (location.pathname.startsWith('/lecturer/exam-authoring/') ? PAGE_META['/lecturer/exam-authoring'] : undefined) ??
    (normalizedRole === 'DepartmentHead'
      ? Object.entries(PAGE_META)
        .filter(([path]) => path.startsWith('/department-head/') && location.pathname.startsWith(`${path}/`))
        .sort(([left], [right]) => right.length - left.length)[0]?.[1]
      : undefined) ??
    (location.pathname.startsWith('/admin')
      ? { label: 'Cổng quản trị', icon: ShieldAlert }
      : undefined) ??
    (isStudent ? { label: location.pathname.startsWith('/classes/') ? 'Chi tiết lớp học' : 'Cổng sinh viên', icon: isStudent && location.pathname.startsWith('/classes/') ? UsersRound : LayoutDashboard } : null) ??
    (location.pathname.startsWith('/lecturer/classes') || location.pathname.startsWith('/in-class-shifts')
      ? { label: 'Quản lý lớp học', icon: UsersRound }
      : normalizedRole === 'Admin'
        ? { label: 'Cổng quản trị', icon: ShieldAlert }
        : normalizedRole === 'DepartmentHead'
          ? { label: 'Cổng Trưởng BM', icon: LayoutDashboard }
          : normalizedRole === 'ExaminationOfficer'
            ? { label: 'Cổng Khảo thí', icon: TicketCheck }
            : normalizedRole === 'Proctor'
              ? { label: 'Cổng Giám thị', icon: ClipboardCheck }
              : { label: 'Cổng giảng viên', icon: LayoutDashboard })
  const PageIcon = page.icon
  const semesterLabel = getSemesterLabel()
  const roleLabel =
    normalizedRole === 'Student' ? 'Sinh viên' : normalizedRole === 'Admin'
      ? 'Quản trị viên'
      : normalizedRole === 'DepartmentHead'
        ? 'Trưởng Bộ môn'
        : normalizedRole === 'ExaminationOfficer'
          ? 'Cán bộ Khảo thí'
          : normalizedRole === 'Proctor'
            ? 'Giám thị'
            : 'Giảng viên'
  const roleInitialsFallback =
    normalizedRole === 'Admin'
      ? 'QT'
      : normalizedRole === 'Student'
        ? 'SV'
        : normalizedRole === 'DepartmentHead'
          ? 'TB'
          : normalizedRole === 'ExaminationOfficer'
            ? 'KT'
            : normalizedRole === 'Proctor'
              ? 'GT'
              : 'GV'

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
    if (isStudent) {
      const route = resolveStudentNotificationRoute(notification)
      if (route) navigate(route)
    }
  }

  return (
    <PortalHeader overlays={hasNotifications && (

      <div id="student-notification-drawer">
        <StudentNotificationDrawer
          open={isNotificationOpen}
          onClose={() => setIsNotificationOpen(false)}
          notifications={notifications.items}
          unreadCount={notifications.unreadCount}
          isLoading={notifications.isLoading}
          isLoadingMore={notifications.isLoadingMore}
          error={notifications.error}
          hasMore={notifications.hasMore}
          portalLabel={`Cổng ${roleLabel}`}
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
    )}>

        <div className="flex min-w-0 items-center gap-3">
          <button
            type="button"
            onClick={onOpenSidebar}
            className="flex size-11 shrink-0 items-center justify-center rounded-xl border border-[#4988C4]/30 bg-[#BDE8F5] text-[#0F2854] transition-colors duration-150 hover:bg-[#4988C4]/20 focus-visible:outline-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none md:hidden"
            aria-label="Mở menu điều hướng"
          >
            <Menu className="size-5" strokeWidth={1.5} aria-hidden="true" />
          </button>

          <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(28,77,141,0.28)]">
            <PageIcon className="size-5" aria-hidden="true" />
          </span>

          <div className="min-w-0">
            <p className="text-xs font-bold tracking-[0.12em] text-[#1C4D8D] uppercase">
              {isStudent ? '' : `${roleLabel} · `}Học kỳ {semesterLabel}
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
            className="relative flex size-11 items-center justify-center rounded-xl border border-[#4988C4] bg-white text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            aria-label="Thông báo"
            aria-expanded={isNotificationOpen}
            aria-controls="student-notification-drawer"
          >
            <Bell className="size-[17px]" strokeWidth={1.5} />
            {notifications.unreadCount > 0 ? (
              <span className="absolute -top-1 -right-1 flex min-h-4 min-w-4 items-center justify-center rounded-full bg-[#ef5c72] px-1 text-[9px] leading-none font-bold text-white tabular-nums ring-2 ring-white">
                {notifications.unreadCount > 99 ? '99+' : notifications.unreadCount}
              </span>
            ) : null}
          </button>
          )}
          {isStudent ? <Link
            to="/profile"
            className="flex min-h-11 items-center gap-2 rounded-full border border-[#4988C4] bg-white py-1 pr-1 pl-3 text-left transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            aria-label="Mở hồ sơ cá nhân"
          >
            <div className="hidden text-right sm:block">
              <p className="max-w-[180px] truncate text-xs font-bold text-[#0F2854]">
                {user?.fullName ?? roleLabel}
              </p>
              <p className="max-w-[180px] truncate text-xs font-medium text-[#1C4D8D]">
                {isStudent ? user?.studentCode || 'Xem hồ sơ' : user?.email ?? roleLabel}
              </p>
            </div>
            <div
              className="flex size-9 items-center justify-center rounded-full bg-[#0F2854] text-xs font-bold text-white"
            >
              {getInitials(user?.fullName ?? roleLabel) || roleInitialsFallback}
            </div>
          </Link> : <div className="flex items-center gap-2 rounded-full border border-[#4988C4] bg-white py-1 pr-1 pl-3 text-left">
            <div className="hidden text-right sm:block">
              <p className="max-w-[180px] truncate text-xs font-bold text-[#0F2854]">{user?.fullName ?? roleLabel}</p>
              <p className="max-w-[180px] truncate text-xs font-medium text-[#1C4D8D]">{user?.email ?? roleLabel}</p>
            </div>
            <div className="flex size-9 items-center justify-center rounded-full bg-[#0F2854] text-xs font-bold text-white">{getInitials(user?.fullName ?? roleLabel) || 'GV'}</div>
          </div>}


          <button
            type="button"
            onClick={handleLogout}
            className="flex size-11 items-center justify-center rounded-xl border border-[#f3c2c8] bg-[#fdecec] text-[#c2415b] transition-colors duration-150 hover:bg-[#f8d5d8] focus-visible:outline-2 focus-visible:outline-[#c2415b] motion-reduce:transition-none"
            aria-label="Đăng xuất"
          >
            <LogOut className="size-4" strokeWidth={1.5} aria-hidden="true" />
          </button>
        </div>
    </PortalHeader>
  )
}

export { Navbar as Header }
