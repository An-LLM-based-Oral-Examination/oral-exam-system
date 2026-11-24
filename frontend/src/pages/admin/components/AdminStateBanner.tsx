import { useContext } from 'react'
import { AlertTriangle, CheckCircle2, LockKeyhole, RefreshCw, Timer } from 'lucide-react'
import { Link } from 'react-router-dom'
import { AuthContext } from '@/context/auth-context'
import { cn } from '@/lib/utils'
import type { NormalizedProblem } from '@/lib/problemDetails'
import { authService } from '@/services/auth.service'

interface AdminStateBannerProps {
  problem?: NormalizedProblem | null
  errorMessage?: string | null
  statusMessage?: string | null
  onRetry?: () => void
  onDismiss?: () => void
  className?: string
}

export function AdminStateBanner({
  problem,
  errorMessage,
  statusMessage,
  onRetry,
  onDismiss,
  className,
}: AdminStateBannerProps) {
  const auth = useContext(AuthContext)
  const effectiveError = problem?.detail || errorMessage?.trim() || ''

  const handleReLogin = () => {
    if (auth?.logout) {
      void Promise.resolve(auth.logout())
      return
    }
    authService.clearSession()
  }

  if (!effectiveError && !statusMessage?.trim()) return null

  if (effectiveError) {
    const isAuth = problem?.isAuthError
    const isRateLimited = problem?.isRateLimited
    const Icon = isAuth ? LockKeyhole : isRateLimited ? Timer : AlertTriangle

    return (
      <div
        role="alert"
        className={cn(
          'flex flex-col gap-3 rounded-2xl border p-4 shadow-[0_1px_2px_rgba(15,40,84,0.04)] sm:flex-row sm:items-center sm:justify-between',
          isRateLimited
            ? 'border-amber-400 bg-[#fff8eb] text-amber-900'
            : 'border-rose-300 bg-rose-50 text-rose-900',
          className,
        )}
      >
        <div className="flex items-start gap-3">
          <Icon className="mt-0.5 size-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          <div className="min-w-0 text-sm">
            <p className="font-semibold">
              {problem?.status ? (
                <span className="mr-1.5 font-mono text-xs tabular-nums">[{problem.status}]</span>
              ) : null}
              {effectiveError}
            </p>
            {problem?.traceId ? (
              <p className="mt-1 font-mono text-xs opacity-80">Trace ID: {problem.traceId}</p>
            ) : null}
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {problem?.status === 401 ? (
            <Link
              to="/login"
              onClick={handleReLogin}
              className="inline-flex min-h-11 items-center justify-center rounded-xl border border-rose-400 bg-white px-3.5 text-sm font-semibold text-rose-800 transition-colors duration-150 hover:bg-rose-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rose-600 motion-reduce:transition-none"
            >
              Đăng nhập lại
            </Link>
          ) : null}
          {onRetry ? (
            <button
              type="button"
              onClick={onRetry}
              className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-slate-300 bg-white px-3.5 text-sm font-semibold text-[#0F2854] transition-colors duration-150 hover:border-[#1C4D8D] hover:bg-[#eef6fd] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              <RefreshCw className="size-4" strokeWidth={1.75} aria-hidden="true" />
              Thử lại
            </button>
          ) : null}
          {onDismiss ? (
            <button
              type="button"
              onClick={onDismiss}
              className="inline-flex min-h-11 items-center justify-center rounded-xl px-3 text-sm font-semibold text-current underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
            >
              Đóng
            </button>
          ) : null}
        </div>
      </div>
    )
  }

  return (
    <div
      role="status"
      className={cn(
        'flex flex-col gap-3 rounded-2xl border border-emerald-300 bg-emerald-50 p-4 text-sm text-emerald-900 shadow-[0_1px_2px_rgba(15,40,84,0.04)] sm:flex-row sm:items-center sm:justify-between',
        className,
      )}
    >
      <div className="flex items-start gap-3">
        <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-700" strokeWidth={1.75} aria-hidden="true" />
        <p className="font-semibold">{statusMessage}</p>
      </div>
      {onDismiss ? (
        <button
          type="button"
          onClick={onDismiss}
          className="inline-flex min-h-11 items-center justify-center rounded-xl px-3 text-sm font-semibold text-emerald-800 underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700"
        >
          Đóng
        </button>
      ) : null}
    </div>
  )
}
