import { useCallback, useEffect, useState } from 'react'
import {
  AlertCircle,
  Clock3,
  KeyRound,
  LoaderCircle,
  Presentation,
  RefreshCw,
  Shield,
  UsersRound,
  X,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { lockBodyScroll } from '@/design/bodyScrollLock'
import { overlay } from '@/design/system'
import { inClassShiftService } from '@/services/inClassShift.service'
import type { ActiveTotpPassword, InClassShift } from '@/types/inClassShift.types'

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  if (error instanceof Error && error.message) return error.message
  return 'Không thể tải mã OTP.'
}

/** Popup chiếu mã OTP cho sinh viên vào ca. */
export function InClassShiftProjectorDialog({
  open,
  shiftId,
  onClose,
}: {
  open: boolean
  shiftId: string | null
  onClose: () => void
}) {
  const [shift, setShift] = useState<InClassShift | null>(null)
  const [otp, setOtp] = useState<ActiveTotpPassword | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [tick, setTick] = useState(0)

  const refreshPassword = useCallback(async () => {
    if (!shiftId) return
    try {
      const password = await inClassShiftService.getActivePassword(shiftId)
      setOtp(password)
      setError('')
    } catch (loadError) {
      setOtp(null)
      setError(getErrorMessage(loadError))
    }
  }, [shiftId])

  useEffect(() => {
    if (!open || !shiftId) return
    let cancelled = false
    const boot = async () => {
      setLoading(true)
      setError('')
      setOtp(null)
      setShift(null)
      try {
        const [shiftResult, password] = await Promise.all([
          inClassShiftService.getShift(shiftId),
          inClassShiftService.getActivePassword(shiftId),
        ])
        if (cancelled) return
        setShift(shiftResult)
        setOtp(password)
      } catch (bootError) {
        if (!cancelled) setError(getErrorMessage(bootError))
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void boot()
    return () => {
      cancelled = true
    }
  }, [open, shiftId])

  useEffect(() => {
    if (!open) return
    const unlock = lockBodyScroll()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => {
      unlock()
      window.removeEventListener('keydown', onKey)
    }
  }, [open, onClose])

  useEffect(() => {
    if (!open) return
    const timer = window.setInterval(() => setTick((value) => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [open])

  useEffect(() => {
    if (!open || !otp) return
    const expiresAt = new Date(otp.expiresAt).getTime()
    const remaining = Math.max(0, Math.ceil((expiresAt - Date.now()) / 1000))
    if (remaining <= 1) void refreshPassword()
  }, [tick, otp, open, refreshPassword])

  if (!open) return null

  const remainingSeconds = otp
    ? Math.max(0, Math.ceil((new Date(otp.expiresAt).getTime() - Date.now()) / 1000))
    : 0
  const rotationTotal = shift?.passwordRotationSeconds || 300
  const progress = Math.min(100, Math.max(0, (remainingSeconds / rotationTotal) * 100))

  return (
    <div
      className={cn(overlay.scrim, overlay.z.modal, 'bg-[#0F2854]/55')}
      role="presentation"
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Chiếu mã OTP ca thi"
        className="flex max-h-[min(92dvh,720px)] w-full max-w-2xl flex-col overflow-hidden rounded-t-3xl border border-[#82aedb]/55 bg-[linear-gradient(160deg,#0F2854_0%,#1C4D8D_58%,#4988C4_100%)] text-white shadow-[0_28px_80px_rgba(15,40,84,0.35)] sm:rounded-3xl"
      >
        <header className="shrink-0 border-b border-white/15 px-5 py-4 sm:px-6">
          <div className="flex items-start justify-between gap-3">
            <div className="flex min-w-0 items-start gap-3">
              <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-white/15 text-white shadow-[inset_0_1px_0_rgba(255,255,255,0.18)]">
                <Presentation className="size-5" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="text-[11px] font-bold tracking-[0.1em] text-[#BDE8F5] uppercase">
                  Chiếu mã vào lớp
                </p>
                <h2 className="mt-0.5 truncate text-xl font-bold tracking-[-0.03em]">
                  {shift?.title ?? 'Ca thi trên lớp'}
                </h2>
              </div>
            </div>
            <button
              type="button"
              aria-label="Đóng"
              onClick={onClose}
              className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-white/25 bg-white/10 text-white transition hover:bg-white/20"
            >
              <X className="size-4" />
            </button>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <span className="inline-flex max-w-full items-center gap-1.5 rounded-full border border-white/25 bg-white/10 px-2.5 py-1 text-xs font-semibold text-white/90">
              <UsersRound className="size-3.5 shrink-0 text-[#BDE8F5]" aria-hidden="true" />
              <span className="truncate">
                {shift ? `${shift.className}${shift.courseCode ? ` · ${shift.courseCode}` : ''}` : 'Đang tải lớp...'}
              </span>
            </span>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-white/25 bg-white/10 px-2.5 py-1 text-xs font-semibold text-white/90">
              <Clock3 className="size-3.5 shrink-0 text-[#BDE8F5]" aria-hidden="true" />
              Xoay mỗi {Math.round(rotationTotal / 60)} phút
            </span>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-white/25 bg-white/10 px-2.5 py-1 text-xs font-semibold text-white/90">
              <KeyRound className="size-3.5 shrink-0 text-[#BDE8F5]" aria-hidden="true" />
              6 ký tự
            </span>
          </div>
        </header>

        <div className="relative flex min-h-0 flex-1 flex-col items-center justify-center px-5 py-8 sm:px-8 sm:py-10">
          <div className="pointer-events-none absolute inset-0 [background-image:radial-gradient(rgba(189,232,245,0.16)_1px,transparent_1px)] [background-size:22px_22px]" />

          {loading ? (
            <div className="relative z-10 flex flex-col items-center gap-3">
              <LoaderCircle className="size-8 animate-spin motion-reduce:animate-none" />
              <p className="text-sm font-semibold text-white/80">Đang lấy mã OTP...</p>
            </div>
          ) : error && !otp ? (
            <div className="relative z-10 max-w-sm text-center">
              <AlertCircle className="mx-auto size-8 text-[#BDE8F5]" />
              <p className="mt-3 text-sm font-semibold text-white/85">{error}</p>
              <button
                type="button"
                onClick={() => void refreshPassword()}
                className="mt-4 inline-flex min-h-10 items-center gap-2 rounded-xl bg-white px-4 text-sm font-bold text-[#0F2854]"
              >
                <RefreshCw className="size-3.5" />
                Thử lại
              </button>
            </div>
          ) : (
            <div className="relative z-10 flex w-full flex-col items-center">
              <p className="text-xs font-bold tracking-[0.18em] text-[#BDE8F5] uppercase">
                Mã vào ca · 6 ký tự
              </p>
              <p
                className="mt-5 font-mono text-[clamp(2.75rem,10vw,4.75rem)] leading-none font-black tracking-[0.22em] text-white drop-shadow-[0_10px_28px_rgba(0,0,0,0.28)]"
                aria-live="polite"
              >
                {otp?.currentOtpCode ?? '------'}
              </p>

              <div className="mt-8 w-full max-w-sm">
                <div className="mb-2 flex items-center justify-between text-sm font-semibold text-white/80">
                  <span>Còn lại</span>
                  <span className="font-mono text-base text-white">
                    {String(Math.floor(remainingSeconds / 60)).padStart(2, '0')}:
                    {String(remainingSeconds % 60).padStart(2, '0')}
                  </span>
                </div>
                <div className="h-2.5 overflow-hidden rounded-full bg-white/20">
                  <div
                    className={cn(
                      'h-full rounded-full transition-[width] duration-1000 ease-linear',
                      remainingSeconds <= 30 ? 'bg-[#ffb4bf]' : 'bg-[#BDE8F5]',
                    )}
                    style={{ width: `${progress}%` }}
                  />
                </div>
              </div>

              <div className="mt-5 flex flex-wrap items-center justify-center gap-2 text-[11px] font-semibold text-white/75">
                <span className="inline-flex items-center gap-1.5 rounded-full border border-white/25 bg-white/10 px-2.5 py-1">
                  <Shield className="size-3" />
                  Grace {otp?.gracePeriodSeconds ?? 60}s
                </span>
                {otp?.expiresAt && (
                  <span className="rounded-full border border-white/25 bg-white/10 px-2.5 py-1">
                    Hết hạn{' '}
                    {new Date(otp.expiresAt).toLocaleTimeString('vi-VN', {
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit',
                    })}
                  </span>
                )}
              </div>

              {error && (
                <p className="mt-4 flex items-center gap-2 text-xs text-[#ffd0d6]" role="alert">
                  <AlertCircle className="size-3.5" />
                  {error}
                </p>
              )}
            </div>
          )}
        </div>

        <footer className="flex justify-end border-t border-white/15 bg-black/15 px-5 py-3 sm:px-6">
          <button
            type="button"
            onClick={onClose}
            className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-white px-4 text-sm font-bold text-[#0F2854] transition duration-200 hover:-translate-y-0.5 hover:bg-[#BDE8F5] motion-reduce:transform-none"
          >
            Đóng
          </button>
        </footer>
      </div>
    </div>
  )
}
