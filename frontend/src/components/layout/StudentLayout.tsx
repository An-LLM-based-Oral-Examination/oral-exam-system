import { useState } from 'react'
import { Outlet } from 'react-router-dom'
import { StudentHeader } from './StudentHeader'
import { StudentSidebar } from './StudentSidebar'
import { cn } from '@/lib/utils'

export function StudentLayout() {
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)

  return (
    <div className="min-h-dvh bg-[#f4f7fc] text-[#10203d]">
      <StudentSidebar
        collapsed={isSidebarCollapsed}
        mobileOpen={isMobileSidebarOpen}
        onCollapse={() => setIsSidebarCollapsed((isCollapsed) => !isCollapsed)}
        onMobileClose={() => setIsMobileSidebarOpen(false)}
      />

      <div
        className={cn(
          'min-h-dvh transition-[padding] duration-300 ease-out',
          isSidebarCollapsed ? 'md:pl-20' : 'md:pl-[220px] xl:pl-[240px]',
        )}
      >
        <StudentHeader onOpenSidebar={() => setIsMobileSidebarOpen(true)} />

        <main className="px-4 py-5 sm:px-6 sm:py-6 xl:px-8">
          <div className="mx-auto w-full max-w-[1380px]">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
