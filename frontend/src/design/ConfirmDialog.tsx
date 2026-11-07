import { useEffect, useRef, type ReactNode } from 'react'
import { AlertTriangle, Info, LoaderCircle, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { lockBodyScroll } from './bodyScrollLock'
import { ds, overlay } from './system'
import { DialogFooter, OverlayCloseButton } from './overlays'

type ConfirmTone = 'info' | 'warning' | 'danger'

const TONE_STYLE: Record<
  ConfirmTone,
  { eyebrow: string; iconWrap: string; confirm: string; icon: LucideIcon }
> = {
  info: {
    eyebrow: 'Thông báo',
    iconWrap: 'border border-[#82aedb]/55 bg-[#F4FBFD] text-[#1C4D8D]',
    confirm: ds.primaryButton,
    icon: Info,
  },
  warning: {
    eyebrow: 'Cảnh báo',
    iconWrap: 'border border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]',
    confirm: ds.primaryButton,
    icon: AlertTriangle,
  },
  danger: {
    eyebrow: 'Cần xác nhận',
    iconWrap: 'border border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]',
    confirm:
      'inline-flex h-10 items-center justify-center gap-2 rounded-xl bg-[#c2415b] px-4 text-sm font-bold text-white shadow-[0_8px_16px_rgba(194,65,91,0.22)] transition duration-200 hover:-translate-y-0.5 hover:bg-[#a4334b] hover:shadow-[0_12px_22px_rgba(164,51,75,0.28)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#c2415b] disabled:pointer-events-none disabled:opacity-45 motion-reduce:transform-none',
    icon: AlertTriangle,
  },
}

export function ConfirmDialog({
  title,
  message,
  tone = 'warning',
  eyebrow,
  icon: Icon,
  confirmLabel,
  cancelLabel,
  onConfirm,
  onCancel,
  pending = false,
  children,
}: {
  title: string
  message?: string
  tone?: ConfirmTone
  eyebrow?: string
  icon?: LucideIcon
  confirmLabel: string
  cancelLabel?: string
  onConfirm: () => void
  onCancel?: () => void
  pending?: boolean
  children?: ReactNode
}) {
  const confirmRef = useRef<HTMLButtonElement>(null)
  const dismissRef = useRef(onCancel ?? onConfirm)
  const style = TONE_STYLE[tone]
  const Glyph = Icon ?? style.icon

  useEffect(() => {
    dismissRef.current = onCancel ?? onConfirm
  }, [onCancel, onConfirm])

  useEffect(() => {
    confirmRef.current?.focus()
    const unlock = lockBodyScroll()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || pending) return
      event.preventDefault()
      dismissRef.current()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => {
      unlock()
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [pending])

  return (
    <div
      className={cn(overlay.scrim, overlay.z.confirm)}
      role="presentation"
      data-space-ignore="true"
      onClick={(event) => {
        if (event.target === event.currentTarget && !pending) dismissRef.current()
      }}
    >
      <section
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        className={cn(overlay.panelCompact, 'max-w-md')}
      >
        <div className="px-5 pt-5 pb-4 sm:px-6">
          <div className="flex items-start gap-3">
            <span
              className={cn(
                'flex size-11 shrink-0 items-center justify-center rounded-2xl',
                style.iconWrap,
              )}
            >
              <Glyph className="size-5" aria-hidden="true" />
            </span>
            <div className="min-w-0 flex-1 pt-0.5">
              <p className="text-xs font-bold tracking-[0.08em] text-[#5b6b84] uppercase">
                {eyebrow ?? style.eyebrow}
              </p>
              <h2
                id="confirm-dialog-title"
                className="mt-1 text-base font-bold tracking-[-0.02em] text-[#0F2854] sm:text-lg"
              >
                {title}
              </h2>
            </div>
            <OverlayCloseButton
              onClose={() => dismissRef.current()}
              busy={pending}
              className="rounded-full"
            />
          </div>
          {message && (
            <p className="mt-4 text-sm leading-relaxed text-[#5b6b84]">{message}</p>
          )}
          {children && <div className="mt-4">{children}</div>}
        </div>
        <DialogFooter>
          {cancelLabel && onCancel && (
            <button
              type="button"
              data-space-ignore="true"
              onClick={onCancel}
              disabled={pending}
              className={ds.outlineButton}
            >
              {cancelLabel}
            </button>
          )}
          <button
            ref={confirmRef}
            type="button"
            data-space-ignore="true"
            onClick={onConfirm}
            disabled={pending}
            className={style.confirm}
          >
            {pending && (
              <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
            )}
            {confirmLabel}
          </button>
        </DialogFooter>
      </section>
    </div>
  )
}
