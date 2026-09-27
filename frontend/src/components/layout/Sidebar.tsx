import React from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '@/context/AuthContext'
import {
  LayoutDashboard,
  Mic,
  Clock,
  BookOpen,
  ClipboardCheck,
  ShieldAlert,
} from 'lucide-react'
import { cn } from '@/utils/cn'

export const Sidebar: React.FC = () => {
  const { hasRole } = useAuth()

  const navItems = [
    {
      label: 'Tổng quan Dashboard',
      path: '/',
      icon: LayoutDashboard,
      roles: ['Student', 'Instructor', 'Admin'],
    },
    {
      label: 'Luyện tập tự do (MF-01)',
      path: '/practice',
      icon: Mic,
      roles: ['Student', 'Instructor', 'Admin'],
    },
    {
      label: 'Thi thử bấm giờ (MF-02)',
      path: '/mock-exam',
      icon: Clock,
      roles: ['Student', 'Instructor', 'Admin'],
    },
    {
      label: 'Ngân hàng đề & Rubric (MF-03)',
      path: '/question-bank',
      icon: BookOpen,
      roles: ['Instructor', 'Admin'],
    },
    {
      label: 'Cổng thẩm định điểm (MF-04)',
      path: '/lecturer/audit',
      icon: ClipboardCheck,
      roles: ['Instructor', 'Admin'],
    },
  ]

  return (
    <aside className="w-64 border-r border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-950 flex flex-col justify-between p-4 min-h-[calc(100vh-4rem)]">
      <div className="space-y-6">
        <div>
          <p className="px-3 text-xs font-bold uppercase tracking-wider text-slate-400 mb-2">
            Điều hướng nghiệp vụ
          </p>
          <nav className="space-y-1">
            {navItems.map((item) => {
              const allowed = hasRole(item.roles as any)
              if (!allowed) return null

              return (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.path === '/'}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center space-x-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
                      isActive
                        ? 'bg-orange-600 text-white shadow-sm'
                        : 'text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-800'
                    )
                  }
                >
                  <item.icon className="w-4 h-4 shrink-0" />
                  <span className="truncate">{item.label}</span>
                </NavLink>
              )
            })}
          </nav>
        </div>

        {hasRole(['Instructor', 'Admin']) && (
          <div className="p-3 bg-orange-50 dark:bg-orange-950/30 border border-orange-200 dark:border-orange-900 rounded-lg">
            <div className="flex items-center space-x-2 text-orange-800 dark:text-orange-300 text-xs font-semibold mb-1">
              <ShieldAlert className="w-4 h-4" />
              <span>Chế độ Giảng viên</span>
            </div>
            <p className="text-[11px] text-orange-700 dark:text-orange-400">
              Có quyền tạo Barem Rubric 10.0 và Thẩm định/Khóa điểm một chiều.
            </p>
          </div>
        )}
      </div>

      <div className="text-center text-xs text-slate-400 pt-4 border-t border-slate-200 dark:border-slate-800">
        Phiên bản UI v3.0 (Vite + React 19)
      </div>
    </aside>
  )
}
