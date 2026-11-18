import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, LoaderCircle, RefreshCw, RotateCcw, X } from 'lucide-react'
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
  return userMessage(error, 'Không xử lý được tác vụ hàng đợi lỗi. Vui lòng thử lại.')
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
  const [resolvingTask, setResolvingTask] = useState<DeadLetterQueueTask | null>(null)
  const [resolutionNotes, setResolutionNotes] = useState('')

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

  useEffect(() => {
    if (!resolvingTask) return
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !busyId) {
        setResolvingTask(null)
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [busyId, resolvingTask])

  const canActOnTask = (task: DeadLetterQueueTask) =>
    canView && (!task.flowType || allowedFlows.includes(task.flowType))

  const replay = async (task: DeadLetterQueueTask) => {
    if (busyId || !canActOnTask(task)) return
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

  const handleConfirmResolve = async (event: React.FormEvent) => {
    event.preventDefault()
    if (busyId || !resolvingTask || !canActOnTask(resolvingTask)) {
      return
    }
    setBusyId(resolvingTask.id)
    setError('')
    setNotice('')
    try {
      const result = await deadLetterQueueService.resolve(
        resolvingTask.id,
        resolutionNotes.trim() || undefined,
      )
      const msg = userMessage(result.message, 'Đã đánh dấu xử lý tác vụ DLQ.')
      setNotice(msg)
      toast.success(msg)
      setResolvingTask(null)
      setResolutionNotes('')
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
            {tasks.map((task) => {
              const isActionAllowed =
                !busyId &&
                task.status === 'pending' &&
                canActOnTask(task)
              return (
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
                      Tạo lúc: {new Date(task.createdAt).toLocaleString('vi-VN')}
                      {task.resolvedAt && ` · Xử lý lúc: ${new Date(task.resolvedAt).toLocaleString('vi-VN')}`}
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    <button
                      type="button"
                      disabled={!isActionAllowed}
                      onClick={() => void replay(task)}
                      className={ds.outlineButton}
                    >
                      <RotateCcw className="size-4" />
                      {busyId === task.id ? 'Đang replay...' : 'Replay task'}
                    </button>
                    <button
                      type="button"
                      disabled={!isActionAllowed}
                      onClick={() => {
                        setResolvingTask(task)
                        setResolutionNotes('')
                      }}
                      className="inline-flex min-h-9 items-center gap-1.5 rounded-xl border border-[#16836a]/35 bg-[#ecfdf5] px-3 py-1.5 text-xs font-bold text-[#059669] transition hover:bg-[#d1fae5] disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      <CheckCircle2 className="size-4" />
                      Đánh dấu đã xử lý
                    </button>
                  </div>
                </article>
              )
            })}
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

      {resolvingTask && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Đánh dấu đã xử lý tác vụ DLQ"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
        >
          <div className="w-full max-w-md rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
            <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
              <h3 className="text-base font-bold text-[#0F2854]">Đánh Dấu Đã Xử Lý Tác Vụ DLQ</h3>
              <button
                type="button"
                aria-label="Đóng"
                onClick={() => setResolvingTask(null)}
                className="text-[#94a3b8] transition hover:text-[#0F2854]"
              >
                <X className="size-5" />
              </button>
            </div>
            <p className="mt-3 text-xs text-[#63738a]">
              Luồng: <strong className="text-[#0F2854]">{resolvingTask.flowType || 'Chưa phân luồng'}</strong> · Loại:{' '}
              <strong className="text-[#1C4D8D]">{resolvingTask.taskType}</strong>
            </p>
            <form onSubmit={handleConfirmResolve} className="mt-4 space-y-3 text-xs">
              <div>
                <label htmlFor="dlqResolutionNotes" className="block font-bold text-[#0F2854]">
                  Ghi chú xử lý sự cố (lưu Audit)
                </label>
                <textarea
                  id="dlqResolutionNotes"
                  value={resolutionNotes}
                  onChange={(event) => setResolutionNotes(event.target.value)}
                  placeholder="Nhập nguyên nhân hoặc hướng xử lý thủ công..."
                  className="mt-1 min-h-20 w-full rounded-xl border border-[#82aedb]/50 p-2.5 text-xs outline-none focus:border-[#4988C4]"
                />
              </div>
              <div className="flex items-center justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setResolvingTask(null)}
                  className="rounded-xl border border-[#82aedb]/50 px-4 py-2 font-bold text-[#64748b] transition hover:bg-[#f1f5f9]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={Boolean(busyId)}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-[#059669] px-4 py-2 font-bold text-white transition hover:bg-[#047857] disabled:opacity-50"
                >
                  {busyId === resolvingTask.id && <LoaderCircle className="size-3.5 animate-spin" />}
                  Xác nhận đã xử lý
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
