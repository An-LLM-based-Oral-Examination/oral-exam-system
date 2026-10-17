import { useState } from 'react'
import { Outlet } from 'react-router-dom'
import { Navbar } from './Navbar'
import { Sidebar } from './Sidebar'
import { cn } from '@/lib/utils'

export function AppLayout() {
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)

  return (
    <div className="min-h-dvh bg-[#F8FAFC] text-[#1E293B]">
      <a
        href="#instructor-main-content"
        className="fixed top-3 left-3 z-50 -translate-y-20 rounded-lg bg-[#F26F21] px-4 py-2 text-sm font-bold text-white shadow-lg transition-transform focus:translate-y-0 focus:outline-none"
      >
        Chuyển đến nội dung chính
      </a>

      <Sidebar
        collapsed={isSidebarCollapsed}
        mobileOpen={isMobileSidebarOpen}
        onCollapse={() => setIsSidebarCollapsed((isCollapsed) => !isCollapsed)}
        onMobileClose={() => setIsMobileSidebarOpen(false)}
      />

      <div
        className={cn(
          'min-h-dvh transition-[padding] duration-200 ease-out',
          isSidebarCollapsed ? 'md:pl-20' : 'md:pl-56 xl:pl-60',
        )}
      >
        <Navbar onOpenSidebar={() => setIsMobileSidebarOpen(true)} />

        <main
          id="instructor-main-content"
          tabIndex={-1}
          className="px-4 py-5 focus:outline-none sm:px-6 sm:py-6 xl:px-8"
        >
          <div className="mx-auto w-full max-w-7xl">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
