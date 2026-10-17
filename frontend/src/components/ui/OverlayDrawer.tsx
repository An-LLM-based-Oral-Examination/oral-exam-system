import { useEffect, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface OverlayDrawerProps {
  open: boolean
  onClose: () => void
  title: string
  subtitle?: string
  badge?: string
  width?: 'md' | 'lg' | 'xl' | '2xl'
  footer?: ReactNode
  children: ReactNode
}

const DRAWER_WIDTH_CLASS: Record<NonNullable<OverlayDrawerProps['width']>, string> = {
  md: 'max-w-xl',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
  '2xl': 'max-w-5xl',
}

export function OverlayDrawer({
  open,
  onClose,
  title,
  subtitle,
  badge,
  width = 'xl',
  footer,
  children,
}: OverlayDrawerProps) {
  useEffect(() => {
    if (!open) return

    const previousOverflow = document.body.style.overflow
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-xs transition-opacity"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div
        className={cn(
          'flex max-h-[90dvh] w-full flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white text-slate-900 shadow-xl transition-all duration-200',
          DRAWER_WIDTH_CLASS[width],
        )}
      >
        <header className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-200 bg-white px-5 py-4 sm:px-6">
          <div className="min-w-0">
            {badge && (
              <span className="inline-flex items-center rounded-full border border-slate-200 bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">
                {badge}
              </span>
            )}
            <h2 className="mt-1 truncate text-base font-semibold text-slate-900">{title}</h2>
            {subtitle && <p className="mt-0.5 text-xs leading-relaxed text-slate-500">{subtitle}</p>}
          </div>

          <button
            type="button"
            onClick={onClose}
            className="flex size-8.5 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-500 transition hover:bg-slate-50 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-[#F26F21] cursor-pointer"
            aria-label="Đóng"
          >
            <X className="size-4" />
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-5 py-5 sm:px-6">{children}</div>

        {footer && (
          <footer className="shrink-0 border-t border-slate-200 bg-slate-50 px-5 py-3.5 sm:px-6">
            {footer}
          </footer>
        )}
      </div>
    </div>
  )
}

export interface OverlayModalProps {
  open: boolean
  onClose: () => void
  title: string
  subtitle?: string
  badge?: string
  size?: 'sm' | 'md' | 'lg' | 'xl' | '2xl' | '3xl'
  footer?: ReactNode
  children: ReactNode
}

const MODAL_SIZE_CLASS: Record<NonNullable<OverlayModalProps['size']>, string> = {
  sm: 'max-w-md',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
  xl: 'max-w-3xl',
  '2xl': 'max-w-4xl',
  '3xl': 'max-w-5xl',
}

export function OverlayModal({
  open,
  onClose,
  title,
  subtitle,
  badge,
  size = 'md',
  footer,
  children,
}: OverlayModalProps) {
  useEffect(() => {
    if (!open) return

    const previousOverflow = document.body.style.overflow
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/35 p-4 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div
        className={cn(
          'flex max-h-[90dvh] w-full flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white text-slate-900 shadow-xl',
          MODAL_SIZE_CLASS[size],
        )}
      >
        <header className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-200 bg-white px-5 py-4 sm:px-6">
          <div className="min-w-0">
            {badge && (
              <span className="inline-flex items-center rounded-full border border-slate-200 bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">
                {badge}
              </span>
            )}
            <h2 className="mt-1 text-base font-semibold text-slate-900">{title}</h2>
            {subtitle && <p className="mt-0.5 text-xs leading-relaxed text-slate-500">{subtitle}</p>}
          </div>

          <button
            type="button"
            onClick={onClose}
            className="flex size-8.5 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-500 transition hover:bg-slate-50 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-[#F26F21] cursor-pointer"
            aria-label="Đóng"
          >
            <X className="size-4" />
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-5 py-5 sm:px-6">{children}</div>

        {footer && (
          <footer className="shrink-0 border-t border-slate-200 bg-slate-50 px-5 py-3.5 sm:px-6">
            {footer}
          </footer>
        )}
      </div>
    </div>
  )
}
