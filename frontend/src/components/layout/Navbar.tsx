import React from 'react'
import { useAuth } from '@/hooks/useAuth'
import { Badge } from '@/components/common/Badge'
import { Button } from '@/components/common/Button'
import { LogOut, User as UserIcon, BookOpenCheck } from 'lucide-react'

export const Navbar: React.FC = () => {
  const { user, logout } = useAuth()

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-slate-200 bg-white px-6 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center space-x-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-orange-600 font-bold text-white shadow-sm">
          <BookOpenCheck className="h-5 w-5" />
        </div>
        <div>
          <h1 className="text-base leading-tight font-bold text-slate-900 dark:text-slate-100">
            Oral Exam AI
          </h1>
          <p className="text-xs text-slate-500">FA26SE166 — FPT University</p>
        </div>
      </div>

      <div className="flex items-center space-x-4">
        {user ? (
          <div className="flex items-center space-x-3">
            <div className="hidden text-right sm:block">
              <p className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                {user.fullName}
              </p>
              <Badge
                variant={
                  user.role === 'Student'
                    ? 'info'
                    : user.role === 'Instructor'
                      ? 'success'
                      : 'warning'
                }
              >
                {user.role}
              </Badge>
            </div>
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200">
              <UserIcon className="h-5 w-5" />
            </div>
            <Button variant="ghost" size="sm" onClick={logout} title="Đăng xuất">
              <LogOut className="h-4 w-4 text-slate-500 transition-colors hover:text-red-600" />
            </Button>
          </div>
        ) : (
          <div className="text-sm text-slate-500">Chưa đăng nhập</div>
        )}
      </div>
    </header>
  )
}

export { Navbar as Header }
