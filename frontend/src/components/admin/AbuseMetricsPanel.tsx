import { useCallback, useEffect, useState } from 'react'
import { RefreshCw, ShieldCheck } from 'lucide-react'
import { apiClient } from '@/services/api.client'
import { extractProblemDetails, type NormalizedProblem } from '@/lib/problemDetails'
import { LoadingState } from '@/components/common/LoadingState'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { cn } from '@/lib/utils'

interface AbuseMetrics {
  requests: number
  rejected: number
  loginLockouts: number
  scope: 'api-process'
}

export function AbuseMetricsPanel() {
  const [data, setData] = useState<AbuseMetrics | null>(null)
  const [error, setError] = useState<NormalizedProblem | null>(null)
  const [loading, setLoading] = useState(true)
  const [revision, setRevision] = useState(0)

  const handleRefresh = useCallback(() => {
    setLoading(true)
    setError(null)
    setRevision((value) => value + 1)
  }, [])

  useEffect(() => {
    let active = true
    async function load() {
      try {
        const response = await apiClient.get<AbuseMetrics>('/diagnostics/abuse')
        if (active) {
          setData(response.data)
          setError(null)
        }
      } catch (err: unknown) {
        if (active) {
          setData(null)
          setError(extractProblemDetails(err, 'Không đọc được chỉ số bảo vệ. Vui lòng thử lại.'))
        }
      } finally {
        if (active) setLoading(false)
      }
    }
    void load()
    return () => {
      active = false
    }
  }, [revision])

  return (
    <section
      aria-label="Chỉ số chống lạm dụng"
      className="rounded-2xl border border-[#0F2854]/10 bg-white p-5 text-[#0F2854]"
    >
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#0F2854]/8 pb-4">
        <div className="flex items-center gap-2">
          <span className="flex size-8 items-center justify-center rounded-lg border border-[#0F2854]/10 bg-[#F4FBFD] text-[#1C4D8D]">
            <ShieldCheck className="size-4" strokeWidth={1.5} aria-hidden="true" />
          </span>
          <h2 className="text-base font-semibold text-[#0F2854]">Bảo vệ API và đăng nhập</h2>
        </div>

        <button
          type="button"
          disabled={loading}
          className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
          onClick={handleRefresh}
        >
          <RefreshCw
            className={cn('size-4 motion-reduce:animate-none', loading && 'animate-spin')}
            strokeWidth={1.5}
            aria-hidden="true"
          />
          Làm mới chỉ số bảo vệ
        </button>
      </div>

      {loading && <LoadingState label="Đang tải chỉ số…" className="min-h-28" />}

      {!loading && error && (
        <div className="mt-4">
          <AdminStateBanner problem={error} onRetry={handleRefresh} />
        </div>
      )}

      {!loading && !error && data && (
        <dl className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div className="rounded-xl border border-[#0F2854]/10 bg-[#F8FBFD] p-3.5">
            <dt className="text-xs font-medium text-[#5b6b84]">Yêu cầu</dt>
            <dd className="mt-1.5 text-xl font-semibold text-[#0F2854] tabular-nums">
              {data.requests}
            </dd>
          </div>
          <div className="rounded-xl border border-[#0F2854]/10 bg-[#F8FBFD] p-3.5">
            <dt className="text-xs font-medium text-[#5b6b84]">Bị giới hạn</dt>
            <dd
              className={cn(
                'mt-1.5 text-xl font-semibold tabular-nums',
                data.rejected > 0 ? 'text-[#9a6b12]' : 'text-[#0F2854]',
              )}
            >
              {data.rejected}
            </dd>
          </div>
          <div className="rounded-xl border border-[#0F2854]/10 bg-[#F8FBFD] p-3.5">
            <dt className="text-xs font-medium text-[#5b6b84]">Khóa đăng nhập</dt>
            <dd
              className={cn(
                'mt-1.5 text-xl font-semibold tabular-nums',
                data.loginLockouts > 0 ? 'text-[#c2415b]' : 'text-[#0F2854]',
              )}
            >
              {data.loginLockouts}
            </dd>
          </div>
        </dl>
      )}

      <p className="mt-3 text-xs text-[#5b6b84]">
        Phạm vi một tiến trình API. Đặt lại khi API khởi động lại; không phải tổng toàn hệ thống.
      </p>
    </section>
  )
}
