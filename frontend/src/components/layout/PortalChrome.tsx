import type { ComponentProps, ReactNode } from 'react'
import { cn } from '@/lib/utils'

export function PortalHeader({ children, overlays }: { children: ReactNode; overlays?: ReactNode }) {
  return (
    <header className="sticky top-0 z-30 flex h-[77px] items-stretch bg-white px-4 py-2 sm:px-6 xl:px-8">
      <div className="flex min-h-0 w-full flex-1 items-center justify-between gap-3 rounded-2xl border border-[#4988C4] bg-white px-3 sm:px-4">
        {children}
      </div>
      {overlays}
    </header>
  )
}

export function PortalSidebar({ className, ...props }: ComponentProps<'aside'>) {
  return (
    <aside
      {...props}
      className={cn(
        'fixed inset-y-0 left-0 z-50 flex w-[240px] flex-col overflow-hidden border-r border-[#4988C4] bg-[#D7F1F8] text-[#0F2854] shadow-[16px_0_40px_rgba(15,40,84,0.06)] transition-[width,transform] duration-300 ease-out motion-reduce:transition-none md:translate-x-0',
        className,
      )}
    />
  )
}

export function PortalSidebarBrand({ children }: { children: ReactNode }) {
  return <div className="flex h-[77px] shrink-0 items-center gap-3 border-b border-[#4988C4] px-4">{children}</div>
}

export function PortalSidebarFooter({ children }: { children: ReactNode }) {
  return <div className="shrink-0 border-t border-[#4988C4] p-3">{children}</div>
}
