import React from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import type { UserRole } from '@/types/auth.types'
import {
  LayoutDashboard,
  BookOpen,
  ClipboardCheck,
  ShieldAlert,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/utils/cn'

interface NavItem {
  label: string
  path: string
  icon: LucideIcon
  roles: UserRole[]
}

export const Sidebar: React.FC = () => {
  const { hasRole } = useAuth()

  const navItems: NavItem[] = [
    {
      label: 'Tổng quan Dashboard',
      path: '/dashboard',
      icon: LayoutDashboard,
      roles: ['Instructor', 'Admin'],
    },
    {
      label: 'Ngân hàng đề & Rubric (MF-03)',
      path: '/question-bank',
      icon: BookOpen,
      roles: ['Instructor', 'Admin'],
    },
    {
      label: 'Cổng hậu kiểm (MF-04)',
      path: '/lecturer/audit',
      icon: ClipboardCheck,
      roles: ['Instructor', 'Admin'],
    },
  ]

  return (
    <aside className="flex min-h-[calc(100vh-4rem)] w-64 flex-col justify-between border-r border-slate-200 bg-slate-50 p-4 dark:border-slate-800 dark:bg-slate-950">
      <div className="space-y-6">
        <div>
          <p className="mb-2 px-3 text-xs font-bold tracking-wider text-slate-400 uppercase">
            Điều hướng nghiệp vụ
          </p>
          <nav className="space-y-1">
            {navItems.map((item) => {
              const allowed = hasRole(item.roles)
              if (!allowed) return null

              return (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.path === '/dashboard'}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center space-x-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
                      isActive
                        ? 'bg-orange-600 text-white shadow-sm'
                        : 'text-slate-700 hover:bg-slate-200 dark:text-slate-300 dark:hover:bg-slate-800',
                    )
                  }
                >
                  <item.icon className="h-4 w-4 shrink-0" />
                  <span className="truncate">{item.label}</span>
                </NavLink>
              )
            })}
          </nav>
        </div>

        {hasRole(['Instructor', 'Admin']) && (
          <div className="rounded-lg border border-orange-200 bg-orange-50 p-3 dark:border-orange-900 dark:bg-orange-950/30">
            <div className="mb-1 flex items-center space-x-2 text-xs font-semibold text-orange-800 dark:text-orange-300">
              <ShieldAlert className="h-4 w-4" />
              <span>Chế độ Giảng viên</span>
            </div>
            <p className="text-[11px] text-orange-700 dark:text-orange-400">
              Có quyền tạo Barem Rubric 10.0 và Thẩm định/Khóa điểm một chiều.
            </p>
          </div>
        )}
      </div>

      <div className="border-t border-slate-200 pt-4 text-center text-xs text-slate-400 dark:border-slate-800">
        Phiên bản UI v3.0 (Vite + React 19)
      </div>
    </aside>
  )
}
