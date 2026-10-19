import { useEffect, useRef, type ReactNode } from 'react'
import { AlertTriangle, Info, LoaderCircle, X, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { ds } from './system'

type ConfirmTone = 'info' | 'warning' | 'danger'

const TONE_STYLE: Record<
  ConfirmTone,
  { eyebrow: string; iconWrap: string; confirm: string; icon: LucideIcon }
> = {
  info: {
    eyebrow: 'Thông báo',
    iconWrap: 'border border-[#4988C4]/30 bg-[#F4FBFD] text-[#1C4D8D]',
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
      'inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[#c2415b] px-4 text-sm font-bold text-white shadow-[0_8px_16px_rgba(194,65,91,0.22)] transition hover:bg-[#a4334b] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#c2415b] disabled:opacity-45',
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
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || pending) return
      event.preventDefault()
      dismissRef.current()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [pending])

  return (
    <div
      className="fixed inset-0 z-[90] flex items-end justify-center bg-[#0F2854]/55 px-4 py-6 backdrop-blur-[3px] sm:items-center"
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
        className="w-full max-w-md overflow-hidden rounded-[22px] border border-[#4988C4]/45 bg-white shadow-[0_24px_70px_rgba(15,40,84,0.28)]"
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
            <button
              type="button"
              data-space-ignore="true"
              onClick={() => dismissRef.current()}
              disabled={pending}
              aria-label="Đóng"
              className="flex size-9 shrink-0 items-center justify-center rounded-full border border-[#4988C4]/45 text-[#5b6b84] transition hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:opacity-40"
            >
              <X className="size-4" />
            </button>
          </div>
          {message && (
            <p className="mt-4 text-sm leading-relaxed text-[#5b6b84]">{message}</p>
          )}
          {children && <div className="mt-4">{children}</div>}
        </div>
        <div className="flex flex-col-reverse gap-2 border-t border-[#4988C4]/45 bg-[#F4FBFD] px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
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
        </div>
      </section>
    </div>
  )
}
