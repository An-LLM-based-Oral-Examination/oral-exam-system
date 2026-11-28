import { useEffect, useState } from 'react'
import { Info, LoaderCircle } from 'lucide-react'
import { NumberStepper } from '@/components/common/NumberStepper'
import { cn } from '@/lib/utils'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { preparationError } from '@/lib/examPreparation'
import type { BlueprintInput, ExamCapacityReport } from '@/types/examPreparation.types'
import { ExamLoadError } from './ExamPreparationShared'

export function ExamReuseField({
  sessionId,
  input,
  value,
  onChange,
  disabled,
  requiredSetCount,
}: {
  sessionId: string
  input: BlueprintInput
  value: number
  onChange: (value: number) => void
  disabled: boolean
  requiredSetCount?: number
}) {
  const [request, setRequest] = useState<{
    key: string
    data?: ExamCapacityReport
    error?: string
    loading?: boolean
  }>()
  const [retry, setRetry] = useState(0)
  const key = JSON.stringify({ ...input, reusePercent: value })
  const valid =
    input.cloIds.length > 0 &&
    [input.easyCount, input.mediumCount, input.hardCount].every(
      (n) => Number.isInteger(n) && n >= 0,
    ) &&
    input.totalQuestions > 0 &&
    value >= 10 &&
    value <= 50
  useEffect(() => {
    if (!valid) return
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      setRequest((previous) => ({ key, data: previous?.data, loading: true }))
      void service
        .capacity(sessionId, JSON.parse(key) as BlueprintInput, controller.signal)
        .then((data) => {
          if (!controller.signal.aborted) setRequest({ key, data })
        })
        .catch((error) => {
          if (!controller.signal.aborted) setRequest({ key, error: preparationError(error) })
        })
    }, 400)
    return () => {
      window.clearTimeout(timer)
      controller.abort()
    }
  }, [sessionId, key, valid, retry])
  const data = request?.key === key && !request.loading ? request.data : undefined
  const error = request?.key === key ? request.error : undefined
  const notice = request?.data
  const loading = valid && !data && !error
  const count = data?.requiredSetCount ?? requiredSetCount ?? notice?.requiredSetCount
  return (
    <section className="space-y-3 rounded-2xl border border-[#82aedb]/55 bg-white p-3.5">
      <div className="mb-1! flex items-center justify-between gap-3">
        <span className="text-sm font-bold text-[#0F2854]">Tỷ lệ tái sử dụng câu</span>
        <NumberStepper
          label="Tỷ lệ tái sử dụng"
          value={value}
          min={10}
          max={50}
          unit="%"
          disabled={disabled}
          onChange={(v) => onChange(Math.max(10, Math.min(50, Number(v) || 10)))}
        />
      </div>
      <input
        type="range"
        min={10}
        max={50}
        step={1}
        aria-label="Tỷ lệ tái sử dụng thanh trượt"
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
        className="mb-2! h-2 w-full accent-[#1C4D8D]"
      />
      <div className="flex justify-between text-xs text-[#6f8198]">
        <span>10%</span>
        <span>30% khuyến nghị</span>
        <span>50%</span>
      </div>
      <div
        className={cn(
          'rounded-xl border p-3 text-sm',
          value <= 20
            ? 'border-[#b8d8ee] bg-[#edf7fd] text-[#1C4D8D]'
            : value <= 35
              ? 'border-[#b8e3d5] bg-[#edf9f4] text-[#16836a]'
              : 'border-[#efd092] bg-[#fff7e6] text-[#94600e]',
        )}
      >
        <p className="font-bold">
          {value <= 20 ? 'Tái sử dụng thấp' : value <= 35 ? 'Mức cân bằng' : 'Tái sử dụng cao'}
        </p>
        <p className="mt-1 text-xs leading-relaxed">
          {value <= 20
            ? 'Cần ngân hàng lớn hơn để phân tán câu hỏi.'
            : value <= 35
              ? 'Cân bằng độ đa dạng và khả năng đáp ứng của ngân hàng.'
              : 'Các bộ đề có thể dùng chung nhiều câu hơn.'}
        </p>
        {count !== undefined && (
          <p className="mt-2 text-xs font-semibold">
            {count} đề yêu cầu · Mỗi câu dùng tối đa{' '}
            {data?.maxUses ?? Math.max(1, Math.ceil((count * value) / 100))} đề.
          </p>
        )}
      </div>
      <div className="flex min-w-0 flex-col items-stretch" aria-busy={valid && !data && !error}>
        {!valid ? (
          <p className="text-xs text-[#6f8198]">
            Chọn CLO và phân bổ độ khó để kiểm tra ngân hàng.
          </p>
        ) : error ? (
          <ExamLoadError message={error} onRetry={() => setRetry((n) => n + 1)} />
        ) : !notice ? (
          <div
            role="status"
            className="flex items-start gap-2 rounded-xl border border-[#82aedb]/55 bg-[#F4FBFD] p-3 text-sm text-[#5b6b84]"
          >
            <LoaderCircle className="mt-0.5 size-4 shrink-0 animate-spin" />
            Đang kiểm tra ngân hàng…
          </div>
        ) : notice.warning ? (
          <div
            role="status"
            className="flex items-start gap-2 rounded-xl border border-[#efd092] bg-[#fff7e6] p-3 text-sm text-[#94600e]"
          >
            {loading ? (
              <LoaderCircle
                aria-label="Đang cập nhật kết quả kiểm tra"
                className="mt-0.5 size-4 shrink-0 animate-spin"
              />
            ) : (
              <Info className="mt-0.5 size-4 shrink-0" />
            )}
            <div>
              <p className={cn('leading-relaxed', loading && 'opacity-60')}>{notice.warning}</p>
              {notice.suggestedReusePercent !== null &&
                notice.suggestedReusePercent > notice.reusePercent && (
                  <button
                    type="button"
                    disabled={disabled || loading}
                    className="mt-2 font-bold underline underline-offset-4"
                    onClick={() => onChange(notice.suggestedReusePercent!)}
                  >
                    Dùng tỷ lệ {notice.suggestedReusePercent}% để đáp ứng ngân hàng hiện tại
                  </button>
                )}
            </div>
          </div>
        ) : (
          <p role="status" className="flex items-center gap-2 text-xs font-semibold text-[#16836a]">
            {loading && (
              <LoaderCircle
                aria-label="Đang cập nhật kết quả kiểm tra"
                className="size-3.5 shrink-0 animate-spin"
              />
            )}
            Ngân hàng hiện đáp ứng cấu hình này.
          </p>
        )}
      </div>
    </section>
  )
}
