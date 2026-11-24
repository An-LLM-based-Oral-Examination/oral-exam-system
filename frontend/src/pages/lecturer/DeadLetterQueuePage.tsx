import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, LoaderCircle, RefreshCw, RotateCcw } from 'lucide-react'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'
import type { DeadLetterQueueTask } from '@/types/deadLetterQueue.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'

function errorMessage(error: unknown) {
  return userMessage(error, 'Không chạy lại được bài chấm lỗi. Vui lòng thử lại.')
}

export function DeadLetterQueuePage() {
  const [tasks, setTasks] = useState<DeadLetterQueueTask[]>([])
  const [status, setStatus] = useState('pending')
  const [flowType, setFlowType] = useState('ALL')
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setTasks(await deadLetterQueueService.list(status || undefined, flowType))
    } catch (requestError) {
      const msg = errorMessage(requestError)
      setError(msg)
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }, [status, flowType])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const replay = async (task: DeadLetterQueueTask) => {
    setBusyId(task.id)
    setError('')
    setNotice('')
    try {
      const result = await deadLetterQueueService.replay(task.id)
      const msg = userMessage(result.message, 'Đã chạy lại bài chấm lỗi.')
      setNotice(msg)
      toast.success(msg)
      await load()
    } catch (requestError) {
      const msg = errorMessage(requestError)
      setError(msg)
      toast.error(msg)
    } finally {
      setBusyId('')
    }
  }

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={AlertTriangle}
        eyebrow="Dead-Letter Queue (Zero Data Loss)"
        title="Theo dõi & Replay Hàng đợi lỗi AI"
        description="Admin giám sát & replay DLQ cho Luyện tập (MF-01) và Thi thử (MF-02); Cán bộ Khảo thí giám sát & replay DLQ cho Thi thật (MF-04)."
        extras={
          <>
            <BannerFact icon={AlertTriangle}>Cách ly task lỗi</BannerFact>
            <BannerFact icon={RotateCcw} tone="green">
              Replay chủ động
            </BannerFact>
            <BannerFact icon={RefreshCw} tone="violet">
              Zero Data Loss
            </BannerFact>
          </>
        }
      />
      {(error || notice) && (
        <div
          role={error ? 'alert' : 'status'}
          className={cn(
            'rounded-xl border px-4 py-3 text-sm font-semibold',
            error
              ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43d4b]'
              : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]',
          )}
        >
          {error || notice}
        </div>
      )}
      <section className={ds.section}>
        <SectionHeader
          icon={AlertTriangle}
          title="Task lỗi cần xử lý"
          hint="Task chấm điểm bị lỗi cần xử lý"
          action={
            <div className="flex flex-wrap items-center gap-2">
              <select
                className={ds.field}
                value={flowType}
                onChange={(event) => setFlowType(event.target.value)}
              >
                <option value="ALL">Tất cả luồng</option>
                <option value="PRACTICE">Luyện tập (MF-01)</option>
                <option value="MOCK_EXAM">Thi thử (MF-02)</option>
                <option value="IN_CLASS_EXAM">Ca thi lớp (MF-05)</option>
                <option value="OFFICIAL_EXAM">Thi thật Kiosk (MF-04)</option>
              </select>
              <select
                className={ds.field}
                value={status}
                onChange={(event) => setStatus(event.target.value)}
              >
                <option value="pending">pending</option>
                <option value="resolved">resolved</option>
                <option value="abandoned">abandoned</option>
                <option value="">Tất cả trạng thái</option>
              </select>
              <button
                type="button"
                className={ds.outlineButton}
                onClick={() => void load()}
                disabled={loading}
              >
                <RefreshCw className={cn('size-4', loading && 'animate-spin')} /> Làm mới
              </button>
            </div>
          }
        />
        {loading ? (
          <div className="flex items-center justify-center p-10">
            <LoaderCircle className="size-7 animate-spin text-[#1C4D8D]" />
          </div>
        ) : (
          <div className="divide-y divide-[#e8edf3]">
            {tasks.map((task) => (
              <article
                key={task.id}
                className="grid gap-3 p-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center"
              >
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className={ds.infoChip}>{task.status}</span>
                    {task.flowType && (
                      <span className="rounded-md bg-[#EBF4FA] px-2 py-0.5 text-xs font-semibold text-[#1C4D8D]">
                        {task.flowType}
                      </span>
                    )}
                    <span className="text-xs font-bold text-[#1C4D8D]">{task.taskType}</span>
                    <span className="text-xs text-[#6c7d93]">Retry {task.retryCount}</span>
                  </div>
                  <p className="mt-2 text-sm font-semibold text-[#0F2854]">{task.errorMessage}</p>
                  <p className="mt-1 text-xs text-[#6c7d93]">
                    {new Date(task.createdAt).toLocaleString('vi-VN')}
                  </p>
                </div>
                <button
                  type="button"
                  disabled={Boolean(busyId) || task.status !== 'pending'}
                  onClick={() => void replay(task)}
                  className={ds.outlineButton}
                >
                  <RotateCcw className="size-4" />
                  {busyId === task.id ? 'Đang replay...' : 'Replay task'}
                </button>
              </article>
            ))}
            {tasks.length === 0 && (
              <div className="p-4">
                <EmptyState
                  icon={AlertTriangle}
                  title="Không có task phù hợp"
                  description="Thử đổi bộ lọc luồng nghiệp vụ hoặc trạng thái để xem task."
                />
              </div>
            )}
          </div>
        )}
      </section>
    </div>
  )
}
