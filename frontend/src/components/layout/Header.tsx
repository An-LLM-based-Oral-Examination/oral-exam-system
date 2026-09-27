import React from 'react'
import { useAuth } from '@/context/AuthContext'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { LogOut, User as UserIcon, BookOpenCheck } from 'lucide-react'

export const Header: React.FC = () => {
  const { user, logout } = useAuth()

  return (
    <header className="h-16 border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 px-6 flex items-center justify-between sticky top-0 z-30">
      <div className="flex items-center space-x-3">
        <div className="w-9 h-9 rounded-lg bg-orange-600 flex items-center justify-center text-white font-bold shadow-sm">
          <BookOpenCheck className="w-5 h-5" />
        </div>
        <div>
          <h1 className="text-base font-bold text-slate-900 dark:text-slate-100 leading-tight">
            Oral Exam AI
          </h1>
          <p className="text-xs text-slate-500">FA26SE166 — FPT University</p>
        </div>
      </div>

      <div className="flex items-center space-x-4">
        {user ? (
          <div className="flex items-center space-x-3">
            <div className="text-right hidden sm:block">
              <p className="text-sm font-semibold text-slate-900 dark:text-slate-100">{user.fullName}</p>
              <Badge variant={user.role === 'Student' ? 'info' : user.role === 'Instructor' ? 'success' : 'warning'}>
                {user.role}
              </Badge>
            </div>
            <div className="w-9 h-9 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-slate-700 dark:text-slate-200">
              <UserIcon className="w-5 h-5" />
            </div>
            <Button variant="ghost" size="sm" onClick={logout} title="Đăng xuất">
              <LogOut className="w-4 h-4 text-slate-500 hover:text-red-600 transition-colors" />
            </Button>
          </div>
        ) : (
          <div className="text-sm text-slate-500">Chưa đăng nhập</div>
        )}
      </div>
    </header>
  )
}
