import { useState } from 'react'
import { Outlet } from 'react-router-dom'
import { StudentHeader } from './StudentHeader'
import { StudentSidebar } from './StudentSidebar'
import { cn } from '@/lib/utils'

export function StudentLayout() {
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)

  return (
    <div className="min-h-dvh bg-[#F8FAFC] text-[#1E293B]">
      <StudentSidebar
        collapsed={isSidebarCollapsed}
        mobileOpen={isMobileSidebarOpen}
        onCollapse={() => setIsSidebarCollapsed((isCollapsed) => !isCollapsed)}
        onMobileClose={() => setIsMobileSidebarOpen(false)}
      />

      <div
        className={cn(
          'min-h-dvh transition-[padding] duration-200 ease-out',
          isSidebarCollapsed ? 'md:pl-18' : 'md:pl-56',
        )}
      >
        <StudentHeader onOpenSidebar={() => setIsMobileSidebarOpen(true)} />

        <main className="px-4 py-6 sm:px-6 xl:px-8">
          <div className="mx-auto w-full max-w-6xl">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
