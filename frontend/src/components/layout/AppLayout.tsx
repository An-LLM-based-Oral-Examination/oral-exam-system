import { useEffect, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { resetBodyScrollLock } from '@/design/bodyScrollLock'
import { Navbar } from './Navbar'
import { Sidebar } from './Sidebar'
import { cn } from '@/lib/utils'

export function AppLayout() {
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)

  useEffect(() => {
    resetBodyScrollLock()
  }, [])

  return (
    <div className="min-h-dvh bg-[#FFFFFF] text-[#0F2854]">
      <Sidebar
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
        <Navbar onOpenSidebar={() => setIsMobileSidebarOpen(true)} />

        <main
          id="portal-main-content"
          tabIndex={-1}
          className="px-4 pt-3 pb-5 focus:outline-none sm:px-6 xl:px-8"
        >
          <div className="mx-auto w-full max-w-[1380px]">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
