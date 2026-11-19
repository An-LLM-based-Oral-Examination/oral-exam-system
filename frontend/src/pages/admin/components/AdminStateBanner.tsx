import { AlertTriangle, CheckCircle2, LockKeyhole, RefreshCw, Timer } from 'lucide-react'
import { Link } from 'react-router-dom'
import { cn } from '@/lib/utils'
import type { NormalizedProblem } from '@/lib/problemDetails'

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
  const effectiveError = problem?.detail || errorMessage?.trim() || ''

  if (!effectiveError && !statusMessage?.trim()) return null

  if (effectiveError) {
    const isAuth = problem?.isAuthError
    const isRateLimited = problem?.isRateLimited
    const Icon = isAuth ? LockKeyhole : isRateLimited ? Timer : AlertTriangle

    return (
      <div
        role="alert"
        className={cn(
          'flex flex-col gap-3 rounded-2xl border p-4 sm:flex-row sm:items-center sm:justify-between',
          isRateLimited
            ? 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]'
            : 'border-[#f3c2c8] bg-[#fdecec] text-[#a43d4b]',
          className,
        )}
      >
        <div className="flex items-start gap-3">
          <Icon className="mt-0.5 size-[18px] shrink-0" strokeWidth={1.5} aria-hidden="true" />
          <div className="min-w-0 text-sm">
            <p className="font-medium">
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
              className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#c2415b]/35 bg-white px-3.5 text-xs font-medium text-[#a43d4b] transition-colors duration-150 hover:bg-[#fff5f6] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#c2415b] motion-reduce:transition-none"
            >
              Đăng nhập lại
            </Link>
          ) : null}
          {onRetry ? (
            <button
              type="button"
              onClick={onRetry}
              className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#0F2854] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              <RefreshCw className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
              Thử lại
            </button>
          ) : null}
          {onDismiss ? (
            <button
              type="button"
              onClick={onDismiss}
              className="inline-flex min-h-11 items-center justify-center rounded-xl px-3 text-xs font-medium text-current underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]"
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
        'flex flex-col gap-3 rounded-2xl border border-[#b7e4cf] bg-[#e8f8f1] p-4 text-sm text-[#16836a] sm:flex-row sm:items-center sm:justify-between',
        className,
      )}
    >
      <div className="flex items-start gap-3">
        <CheckCircle2 className="mt-0.5 size-[18px] shrink-0" strokeWidth={1.5} aria-hidden="true" />
        <p className="font-medium">{statusMessage}</p>
      </div>
      {onDismiss ? (
        <button
          type="button"
          onClick={onDismiss}
          className="inline-flex min-h-11 items-center justify-center rounded-xl px-3 text-xs font-medium text-[#16836a] underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#16836a]"
        >
          Đóng
        </button>
      ) : null}
    </div>
  )
}
