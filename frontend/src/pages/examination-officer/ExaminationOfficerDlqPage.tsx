import { useCallback, useEffect, useState, type FormEvent } from 'react'
import {
  AlertTriangle,
  CheckCircle2,
  LoaderCircle,
  RefreshCw,
  RotateCcw,
  ServerCog,
} from 'lucide-react'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'
import type { DeadLetterQueueTask } from '@/types/deadLetterQueue.types'
import { PageBanner } from '@/design/ui'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

function formatDate(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN')
}

export function ExaminationOfficerDlqPage() {
  const [tasks, setTasks] = useState<DeadLetterQueueTask[]>([])
  const [status, setStatus] = useState('pending')
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState('')
  const [error, setError] = useState('')
  const [resolvingTask, setResolvingTask] = useState<DeadLetterQueueTask | null>(null)
  const [resolveNotes, setResolveNotes] = useState('')
  const [resolveNotesError, setResolveNotesError] = useState('')
  const [resolveApiError, setResolveApiError] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setTasks(await deadLetterQueueService.listForExaminationOfficer(status || undefined))
    } catch (requestError) {
      const message = userMessage(requestError, 'Không thể tải hàng đợi lỗi MF-04.')
      setError(message)
      toast.error(message)
    } finally {
      setLoading(false)
    }
  }, [status])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const replay = async (task: DeadLetterQueueTask) => {
    setBusyId(task.id)
    setError('')
    try {
      const result = await deadLetterQueueService.replayForExaminationOfficer(task.id)
      toast.success(userMessage(result.message, 'Đã yêu cầu replay task MF-04.'))
      await load()
    } catch (requestError) {
      const message = userMessage(requestError, 'Không thể replay task MF-04.')
      setError(message)
      toast.error(message)
    } finally {
      setBusyId('')
    }
  }

  const openResolveDialog = (task: DeadLetterQueueTask) => {
    setResolvingTask(task)
    setResolveNotes('')
    setResolveNotesError('')
    setResolveApiError('')
  }

  const closeResolveDialog = () => {
    if (busyId) return
    setResolvingTask(null)
    setResolveNotes('')
    setResolveNotesError('')
    setResolveApiError('')
  }

  const submitResolve = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!resolvingTask || busyId) return
    const trimmedNotes = resolveNotes.trim()
    if (trimmedNotes.length < 5) {
      setResolveNotesError('Ghi chú xử lý cần ít nhất 5 ký tự.')
      return
    }
    setResolveNotesError('')
    setResolveApiError('')
    setBusyId(resolvingTask.id)
    try {
      const result = await deadLetterQueueService.resolveForExaminationOfficer(
        resolvingTask.id,
        trimmedNotes,
      )
      toast.success(userMessage(result.message, 'Đã đánh dấu task đã xử lý.'))
      setResolvingTask(null)
      setResolveNotes('')
      await load()
    } catch (requestError) {
      const message = userMessage(requestError, 'Không thể đánh dấu task đã xử lý.')
      setResolveApiError(message)
      toast.error(message)
    } finally {
      setBusyId('')
    }
  }

  return (
    <div className={ds.page}>
      <PageBanner
        icon={ServerCog}
        eyebrow="CỔNG KHẢO THÍ · MF-04"
        title="Dead-Letter Queue thi thật"
        description="Theo dõi, replay và xác nhận xử lý các tác vụ chấm thi thật bị lỗi. Dữ liệu được lọc server-side theo luồng OFFICIAL_EXAM."
        extras={
          <span className={ds.bannerChip}>
            <AlertTriangle className="size-4" /> Zero Data Loss
          </span>
        }
      />

      {error ? (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          <span>{error}</span>
          <button type="button" className={ds.outlineButton} onClick={() => void load()}>
            <RefreshCw className="size-4" /> Thử lại
          </button>
        </div>
      ) : null}

      <section className={ds.section}>
        <div
          className={cn(
            ds.sectionHeader,
            'flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between',
          )}
        >
          <div>
            <h2 className={ds.sectionTitle}>Task lỗi MF-04</h2>
            <p className={ds.sectionHint}>
              Chỉ các task thuộc luồng OFFICIAL_EXAM của Cán bộ Khảo thí.
            </p>
          </div>
          <div className="flex gap-2">
            <select
              aria-label="Lọc trạng thái DLQ"
              className={cn(ds.field, ds.filterStatus)}
              value={status}
              onChange={(event) => setStatus(event.target.value)}
            >
              <option value="pending">Đang chờ</option>
              <option value="failed">Thất bại</option>
              <option value="processing">Đang xử lý</option>
              <option value="resolved">Đã xử lý</option>
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
        </div>
        {loading ? (
          <div className="p-8 text-center text-sm font-semibold text-[#5b6b84]">
            Đang tải task lỗi…
          </div>
        ) : tasks.length === 0 ? (
          <div className="p-6">
            <EmptyState
              compact
              icon={CheckCircle2}
              title="Không có task cần xử lý"
              description="Không tìm thấy task MF-04 theo bộ lọc hiện tại."
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[900px] text-left text-sm">
              <thead className="bg-[#F4FBFD] text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                <tr>
                  <th className="px-5 py-3">Task</th>
                  <th className="px-5 py-3">Lỗi</th>
                  <th className="px-5 py-3">Retry</th>
                  <th className="px-5 py-3">Tạo lúc</th>
                  <th className="px-5 py-3">Trạng thái</th>
                  <th className="px-5 py-3 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#d9e8f2]">
                {tasks.map((task) => (
                  <tr key={task.id} className="hover:bg-[#F8FBFD]">
                    <td
                      className="max-w-[190px] truncate px-5 py-3 font-semibold text-[#0F2854]"
                      title={task.id}
                    >
                      {task.taskType}
                    </td>
                    <td
                      className="max-w-[320px] truncate px-5 py-3 text-[#5b6b84]"
                      title={task.errorMessage}
                    >
                      {task.errorMessage}
                    </td>
                    <td className="px-5 py-3 text-[#5b6b84]">{task.retryCount}</td>
                    <td className="px-5 py-3 whitespace-nowrap text-[#5b6b84]">
                      {formatDate(task.createdAt)}
                    </td>
                    <td className="px-5 py-3">
                      <span
                        className={
                          task.status.toLowerCase() === 'resolved' ? ds.successChip : ds.dangerChip
                        }
                      >
                        {task.status}
                      </span>
                    </td>
                    <td className="px-5 py-3">
                      <div className="flex justify-end gap-2">
                        <button
                          type="button"
                          className={ds.outlineButton}
                          onClick={() => void replay(task)}
                          disabled={busyId === task.id || task.status.toLowerCase() === 'resolved'}
                        >
                          <RotateCcw className="size-4" /> Replay
                        </button>
                        <button
                          type="button"
                          className={ds.primaryButton}
                          onClick={() => openResolveDialog(task)}
                          disabled={busyId === task.id || task.status.toLowerCase() === 'resolved'}
                        >
                          <CheckCircle2 className="size-4" /> Xử lý
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <FormDialog
        open={Boolean(resolvingTask)}
        align="center"
        size="sm"
        variant="calm"
        icon={CheckCircle2}
        eyebrow="XỬ LÝ SỰ CỐ DLQ · MF-04"
        title="Đánh dấu task đã xử lý"
        busy={Boolean(busyId)}
        onClose={closeResolveDialog}
        footer={
          <DialogFooter variant="calm">
            <button
              type="button"
              className={ds.outlineButton}
              onClick={closeResolveDialog}
              disabled={Boolean(busyId)}
            >
              Hủy
            </button>
            <button
              type="submit"
              form="eo-dlq-resolve-form"
              className={ds.primaryButton}
              disabled={Boolean(busyId)}
            >
              {busyId ? <LoaderCircle className="size-4 animate-spin" /> : null}
              {busyId ? 'Đang lưu…' : 'Xác nhận đã xử lý'}
            </button>
          </DialogFooter>
        }
      >
        {resolvingTask ? (
          <form id="eo-dlq-resolve-form" noValidate onSubmit={submitResolve} className="grid gap-4">
            {resolveApiError ? (
              <div
                role="alert"
                className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
              >
                {resolveApiError}
              </div>
            ) : null}
            <div className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] p-3 text-xs text-[#52657d]">
              <p className="font-bold text-[#0F2854]">
                {resolvingTask.taskType} · Retry {resolvingTask.retryCount}
              </p>
              <p className="mt-1 break-words [overflow-wrap:anywhere]">
                {resolvingTask.errorMessage}
              </p>
            </div>
            <label htmlFor="eo-dlq-resolve-notes" className={fieldLabelClass(Boolean(resolveNotesError))}>
              Ghi chú xử lý (tối thiểu 5 ký tự) *
              <textarea
                id="eo-dlq-resolve-notes"
                className={cn(ds.field, 'mt-1.5 min-h-24 py-2', resolveNotesError && fieldInvalidClass)}
                placeholder="Nhập nguyên nhân hoặc biện pháp khắc phục sự cố MF-04…"
                value={resolveNotes}
                onChange={(event) => {
                  setResolveNotes(event.target.value)
                  if (resolveNotesError) setResolveNotesError('')
                }}
                aria-invalid={Boolean(resolveNotesError)}
              />
              <FieldError message={resolveNotesError} />
            </label>
          </form>
        ) : null}
      </FormDialog>
    </div>
  )
}
