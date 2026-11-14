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
import { useAuth } from '@/hooks/useAuth'
import { dlqFlowsFor } from '@/lib/task09Permissions'

function errorMessage(error: unknown) {
  return userMessage(error, 'Không chạy lại được bài chấm lỗi. Vui lòng thử lại.')
}

export function DeadLetterQueuePage() {
  const { user } = useAuth()
  const role = user?.role
  const allowedFlows = dlqFlowsFor(role)
  const canView = allowedFlows.length > 0
  const [tasks, setTasks] = useState<DeadLetterQueueTask[]>([])
  const [status, setStatus] = useState('pending')
  const [selectedFlow, setFlowType] = useState('')
  const flowType = allowedFlows.includes(selectedFlow) ? selectedFlow : allowedFlows[0]
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const load = useCallback(async () => {
    const flows = dlqFlowsFor(role)
    if (flows.length === 0) return
    const requestedFlow = flows.includes(selectedFlow) ? selectedFlow : flows[0]
    setLoading(true)
    setError('')
    try {
      setTasks(await deadLetterQueueService.list(status || undefined, requestedFlow))
    } catch (requestError) {
      const msg = errorMessage(requestError)
      setError(msg)
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }, [status, selectedFlow, role])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const replay = async (task: DeadLetterQueueTask) => {
    if (!canView || !task.flowType || !allowedFlows.includes(task.flowType)) return
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

  if (!canView) return <div role="alert">Vai trò hiện tại không được truy cập hàng đợi lỗi AI.</div>

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={AlertTriangle}
        eyebrow="Dead-Letter Queue (Zero Data Loss)"
        title="Theo dõi & Replay Hàng đợi lỗi AI"
        description="Admin xử lý luyện tập, thi thử và ca lớp; Khảo thí xử lý thi thật; Giảng viên chỉ xử lý ca lớp mình phụ trách. Phạm vi tài nguyên được kiểm tra tại API."
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
                aria-label="Luồng nghiệp vụ"
                className={ds.field}
                value={flowType}
                onChange={(event) => setFlowType(event.target.value)}
              >
                {allowedFlows.includes('ALL') && <option value="ALL">Tất cả luồng được phép</option>}
                {allowedFlows.includes('PRACTICE') && <option value="PRACTICE">Luyện tập (MF-01)</option>}
                {allowedFlows.includes('MOCK_EXAM') && <option value="MOCK_EXAM">Thi thử (MF-02)</option>}
                {allowedFlows.includes('IN_CLASS_EXAM') && <option value="IN_CLASS_EXAM">Ca thi lớp (MF-05)</option>}
                {allowedFlows.includes('OFFICIAL_EXAM') && <option value="OFFICIAL_EXAM">Thi thật Kiosk (MF-04)</option>}
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
                  disabled={Boolean(busyId) || task.status !== 'pending' || !task.flowType || !allowedFlows.includes(task.flowType)}
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
