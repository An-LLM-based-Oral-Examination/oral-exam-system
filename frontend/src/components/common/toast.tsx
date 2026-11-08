import { useEffect, useRef, useSyncExternalStore } from 'react'
import { createPortal } from 'react-dom'
import { AlertTriangle, Check, Info, X } from 'lucide-react'
import { overlay } from '@/design/system'
import { cn } from '@/lib/utils'
import {
  dismissToast,
  getToasts,
  subscribeToasts,
  toast,
  type ToastItem,
  type ToastTone,
} from '@/components/common/toastStore'

const TONE_META: Record<
  ToastTone,
  {
    Icon: typeof Check
    shell: string
    icon: string
    closeHover: string
  }
> = {
  success: {
    Icon: Check,
    shell: 'border-[#9ed4bc] bg-[#e8f8f1]',
    icon: 'bg-[#16836a]',
    closeHover: 'hover:bg-white/90 hover:text-[#16836a]',
  },
  info: {
    Icon: Info,
    shell: 'border-[#8eb8e0] bg-[#E7F3FB]',
    icon: 'bg-[#1C4D8D]',
    closeHover: 'hover:bg-white/90 hover:text-[#1C4D8D]',
  },
  warning: {
    Icon: AlertTriangle,
    shell: 'border-[#e3bd7b] bg-[#fff4e8]',
    icon: 'bg-[#c26a12]',
    closeHover: 'hover:bg-white/90 hover:text-[#c26a12]',
  },
  error: {
    Icon: X,
    shell: 'border-[#f0b4bc] bg-[#fdecec]',
    icon: 'bg-[#c2415b]',
    closeHover: 'hover:bg-white/90 hover:text-[#c2415b]',
  },
}

function ToastCard({ item }: { item: ToastItem }) {
  const meta = TONE_META[item.tone]
  const Icon = meta.Icon

  return (
    <div
      role={item.tone === 'error' || item.tone === 'warning' ? 'alert' : 'status'}
      className={cn(
        'pointer-events-auto flex items-center gap-3 rounded-xl border px-3.5 py-3 shadow-[0_10px_28px_rgba(15,40,84,0.12)]',
        meta.shell,
      )}
    >
      <span
        className={cn(
          'flex size-7 shrink-0 items-center justify-center rounded-full text-white shadow-[0_1px_0_rgba(255,255,255,0.25)_inset]',
          meta.icon,
        )}
        aria-hidden="true"
      >
        <Icon className="size-3.5" strokeWidth={2.6} />
      </span>

      <p className="min-w-0 flex-1 text-sm leading-snug font-medium text-[#2b3648]">{item.message}</p>

      <button
        type="button"
        aria-label="Đóng thông báo"
        onClick={() => dismissToast(item.id)}
        className={cn(
          'flex size-7 shrink-0 items-center justify-center rounded-md text-[#6b778c] transition',
          meta.closeHover,
        )}
      >
        <X className="size-3.5" strokeWidth={2.4} />
      </button>
    </div>
  )
}

/** Góc phải trên viewport, trên mọi dialog/header. */
export function ToastHost() {
  const items = useSyncExternalStore(subscribeToasts, getToasts, getToasts)

  if (typeof document === 'undefined') return null

  return createPortal(
    <div
      className={cn(
        'pointer-events-none fixed top-3 right-3 flex w-[min(22rem,calc(100vw-1.5rem))] flex-col gap-2.5 sm:top-4 sm:right-4',
        overlay.z.toast,
      )}
      aria-live="polite"
      aria-relevant="additions"
    >
      {items.map((item) => (
        <ToastCard key={item.id} item={item} />
      ))}
    </div>,
    document.body,
  )
}

/** Đồng bộ notice/error local state → toast toàn cục, rồi xóa state. */
export function StatusToast({
  notice,
  error,
  onDismiss,
}: {
  notice?: string | null
  error?: string | null
  onDismiss: () => void
}) {
  const lastNotice = useRef('')
  const lastError = useRef('')
  const onDismissRef = useRef(onDismiss)

  useEffect(() => {
    onDismissRef.current = onDismiss
  }, [onDismiss])

  useEffect(() => {
    const nextError = error?.trim() ?? ''
    if (nextError && nextError !== lastError.current) {
      lastError.current = nextError
      toast.error(nextError)
      onDismissRef.current()
      return
    }
    if (!nextError) lastError.current = ''

    const nextNotice = notice?.trim() ?? ''
    if (nextNotice && nextNotice !== lastNotice.current) {
      lastNotice.current = nextNotice
      toast.success(nextNotice)
      onDismissRef.current()
      return
    }
    if (!nextNotice) lastNotice.current = ''
  }, [error, notice])

  return null
}
