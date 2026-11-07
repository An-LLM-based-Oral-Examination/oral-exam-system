export type ToastTone = 'success' | 'info' | 'warning' | 'error'

export type ToastItem = {
  id: string
  message: string
  tone: ToastTone
  durationMs: number
}

const DURATION_MS: Record<ToastTone, number> = {
  success: 2800,
  info: 3200,
  warning: 3600,
  error: 4200,
}

const MAX_TOASTS = 3

type Listener = () => void

let toasts: ToastItem[] = []
const listeners = new Set<Listener>()
const timers = new Map<string, number>()
let seed = 0

function emit() {
  for (const listener of listeners) listener()
}

function clearTimer(id: string) {
  const timer = timers.get(id)
  if (timer == null) return
  window.clearTimeout(timer)
  timers.delete(id)
}

export function getToasts() {
  return toasts
}

export function subscribeToasts(listener: Listener) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function dismissToast(id: string) {
  clearTimer(id)
  const next = toasts.filter((item) => item.id !== id)
  if (next.length === toasts.length) return
  toasts = next
  emit()
}

/** Bỏ prefix kỹ thuật kiểu "CONFLICT: ..." / "BAD REQUEST: ...". */
function sanitizeToastMessage(message: string) {
  return message
    .replace(
      /^(CONFLICT|BAD\s*REQUEST|UNAUTHORIZED|FORBIDDEN|NOT\s*FOUND|INTERNAL\s*SERVER\s*ERROR|ERROR|WARNING|INFO)\s*[:\-–—]\s*/i,
      '',
    )
    .trim()
}

function push(message: string, tone: ToastTone, durationMs?: number) {
  const text = sanitizeToastMessage(message)
  if (!text) return

  const duration = durationMs ?? DURATION_MS[tone]
  const id = `toast-${Date.now()}-${seed++}`
  const nextItem: ToastItem = { id, message: text, tone, durationMs: duration }
  const overflow = [...toasts, nextItem]
  const removed = overflow.slice(0, Math.max(0, overflow.length - MAX_TOASTS))
  for (const item of removed) clearTimer(item.id)
  toasts = overflow.slice(-MAX_TOASTS)
  emit()

  timers.set(
    id,
    window.setTimeout(() => dismissToast(id), duration),
  )
}

export const toast = {
  success(message: string, durationMs?: number) {
    push(message, 'success', durationMs)
  },
  info(message: string, durationMs?: number) {
    push(message, 'info', durationMs)
  },
  warning(message: string, durationMs?: number) {
    push(message, 'warning', durationMs)
  },
  error(message: string, durationMs?: number) {
    push(message, 'error', durationMs)
  },
  dismiss: dismissToast,
}
