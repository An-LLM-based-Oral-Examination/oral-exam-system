import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
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
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingState } from '@/components/common/LoadingState'
import { FilterDropMenu, type FilterDropMenuOption } from '@/components/common/FilterDropMenu'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { toast } from '@/components/common/toastStore'
import { BannerFact, PageBanner, SectionHeader } from '@/design/ui'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { userMessage } from '@/lib/userMessage'
import { extractProblemDetails, type NormalizedProblem } from '@/lib/problemDetails'
import { useAuth } from '@/hooks/useAuth'
import { dlqFlowsFor } from '@/lib/task09Permissions'
import { canonicalRole } from '@/routes/routeAccess'
import { useUrlQueryState } from '@/hooks/useUrlQueryState'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'

const STATUS_OPTIONS: FilterDropMenuOption[] = [
  { value: 'pending', label: 'Đang chờ (pending)' },
  { value: 'resolved', label: 'Đã xử lý (resolved)' },
  { value: 'abandoned', label: 'Đã hủy (abandoned)' },
  { value: 'all', label: 'Tất cả trạng thái' },
]

const ADMIN_ALLOWED_TASK_FLOWS = new Set(['PRACTICE', 'MOCK_EXAM', 'IN_CLASS_EXAM'])

const FLOW_LABELS: Record<string, string> = {
  ALL: 'Tất cả luồng được phép',
  PRACTICE: 'Luyện tập (MF-01)',
  MOCK_EXAM: 'Thi thử (MF-02)',
  IN_CLASS_EXAM: 'Ca thi trong lớp (MF-05)',
  OFFICIAL_EXAM: 'Thi thật Kiosk (MF-04)',
}

function formatDate(value: string): string {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString('vi-VN')
}

export function DeadLetterQueuePage() {
  const { user } = useAuth()
  const role = user?.role
  const normalizedRole = canonicalRole(role)
  const allowedFlows = useMemo(() => dlqFlowsFor(role), [role])
  const canView = allowedFlows.length > 0
  const isAdmin = normalizedRole === 'Admin'
  const isExaminationOfficer = normalizedRole === 'ExaminationOfficer'

  const defaultFlow = allowedFlows[0] ?? ''
  const { searchParams, setParams } = useUrlQueryState({
    status: 'pending',
    flow: defaultFlow,
  })

  const rawStatus = searchParams.get('status')
  const status =
    rawStatus === null
      ? 'pending'
      : rawStatus === ''
        ? 'all'
        : STATUS_OPTIONS.some((item) => item.value === rawStatus)
          ? rawStatus
          : 'pending'

  const rawFlow = searchParams.get('flow') ?? ''
  const flowType = allowedFlows.includes(rawFlow) ? rawFlow : defaultFlow

  const [tasks, setTasks] = useState<DeadLetterQueueTask[]>([])
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState('')
  const [problem, setProblem] = useState<NormalizedProblem | null>(null)
  const [notice, setNotice] = useState('')
  const [resolvingTask, setResolvingTask] = useState<DeadLetterQueueTask | null>(null)
  const [resolutionNotes, setResolutionNotes] = useState('')
  const [resolveProblem, setResolveProblem] = useState<NormalizedProblem | null>(null)
  const requestIdRef = useRef(0)

  const flowOptions: FilterDropMenuOption[] = useMemo(
    () =>
      allowedFlows
        .filter((flow) => Object.prototype.hasOwnProperty.call(FLOW_LABELS, flow))
        .map((flow) => ({
          value: flow,
          label: FLOW_LABELS[flow],
        })),
    [allowedFlows],
  )

  const load = useCallback(async () => {
    if (allowedFlows.length === 0) {
      setLoading(false)
      return
    }
    const requestId = ++requestIdRef.current
    setLoading(true)
    setProblem(null)
    const effectiveStatus = status === 'all' ? undefined : status
    try {
      const result = isAdmin
        ? await deadLetterQueueService.listForAdmin(effectiveStatus, flowType)
        : isExaminationOfficer
          ? await deadLetterQueueService.listForExaminationOfficer(effectiveStatus)
          : await deadLetterQueueService.list(effectiveStatus, flowType)
      if (requestId !== requestIdRef.current) return
      const scopedResult = isAdmin
        ? result.filter(
            (task) =>
              task.taskType !== 'OfficialExamGrading' &&
              (!task.flowType || ADMIN_ALLOWED_TASK_FLOWS.has(task.flowType)),
          )
        : result
      setTasks(scopedResult)
    } catch (requestError) {
      if (requestId !== requestIdRef.current) return
      const parsed = extractProblemDetails(
        requestError,
        'Không xử lý được tác vụ hàng đợi lỗi. Vui lòng thử lại.',
      )
      setProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [allowedFlows.length, flowType, isAdmin, isExaminationOfficer, status])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [load])

  const canActOnTask = (task: DeadLetterQueueTask) =>
    canView &&
    (!isAdmin || task.taskType !== 'OfficialExamGrading') &&
    (!task.flowType || allowedFlows.includes(task.flowType))

  const replay = async (task: DeadLetterQueueTask) => {
    if (busyId || !canActOnTask(task)) return
    setBusyId(task.id)
    setProblem(null)
    setNotice('')
    try {
      const result = isAdmin
        ? await deadLetterQueueService.replayForAdmin(task.id)
        : isExaminationOfficer
          ? await deadLetterQueueService.replayForExaminationOfficer(task.id)
          : await deadLetterQueueService.replay(task.id)
      const msg = userMessage(result.message, 'Đã chạy lại bài chấm lỗi.')
      setNotice(msg)
      toast.success(msg)
      await load()
    } catch (requestError) {
      const parsed = extractProblemDetails(
        requestError,
        'Không xử lý được tác vụ hàng đợi lỗi. Vui lòng thử lại.',
      )
      setProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setBusyId('')
    }
  }

  const handleConfirmResolve = async (event: FormEvent) => {
    event.preventDefault()
    if (busyId || !resolvingTask || !canActOnTask(resolvingTask)) {
      return
    }
    setBusyId(resolvingTask.id)
    setResolveProblem(null)
    setNotice('')
    try {
      const notes = resolutionNotes.trim() || undefined
      const result = isAdmin
        ? await deadLetterQueueService.resolveForAdmin(resolvingTask.id, notes)
        : isExaminationOfficer
          ? await deadLetterQueueService.resolveForExaminationOfficer(resolvingTask.id, notes)
          : await deadLetterQueueService.resolve(resolvingTask.id, notes)
      const msg = userMessage(result.message, 'Đã đánh dấu xử lý tác vụ DLQ.')
      setNotice(msg)
      toast.success(msg)
      setResolvingTask(null)
      setResolutionNotes('')
      setResolveProblem(null)
      await load()
    } catch (requestError) {
      const parsed = extractProblemDetails(
        requestError,
        'Không xử lý được tác vụ hàng đợi lỗi. Vui lòng thử lại.',
      )
      setResolveProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setBusyId('')
    }
  }

  if (!canView) {
    return (
      <div className={ds.page}>
        <AdminStateBanner errorMessage="Vai trò hiện tại không được truy cập hàng đợi lỗi AI." />
      </div>
    )
  }

  return (
    <div className={ds.page}>
      <PageBanner
        variant={isAdmin ? 'calm' : 'default'}
        icon={ServerCog}
        eyebrow={isAdmin ? 'Dead-Letter Queue · MF-01, MF-02 & MF-05' : 'Dead-Letter Queue · MF-04'}
        title={isAdmin ? 'Hàng đợi lỗi AI (MF-01 / MF-02 / MF-05)' : 'Theo dõi & Replay Hàng đợi lỗi AI'}
        description={
          isAdmin
            ? 'Giám sát, replay và xác nhận xử lý tác vụ chấm điểm lỗi thuộc luồng Luyện tập (MF-01), Thi thử (MF-02) và Ca thi trong lớp (MF-05). Các tác vụ thi thật (MF-04) do Khảo thí quản lý riêng.'
            : 'Giám sát, replay và xác nhận xử lý các tác vụ chấm điểm thuộc luồng Thi thật Kiosk (MF-04).'
        }
        extras={
          <>
            <BannerFact icon={AlertTriangle} surface={isAdmin ? 'calm' : 'banner'}>
              Cách ly task lỗi
            </BannerFact>
            <BannerFact icon={RotateCcw} tone="green" surface={isAdmin ? 'calm' : 'banner'}>
              Replay chủ động
            </BannerFact>
            <BannerFact icon={RefreshCw} tone="violet" surface={isAdmin ? 'calm' : 'banner'}>
              Zero Data Loss
            </BannerFact>
          </>
        }
      />

      <AdminStateBanner
        problem={problem}
        statusMessage={notice}
        onRetry={problem ? () => void load() : undefined}
        onDismiss={() => {
          setProblem(null)
          setNotice('')
        }}
      />

      <section
        className={cn(
          'overflow-hidden rounded-2xl border bg-white',
          isAdmin ? 'border-[#0F2854]/10' : 'border-[#82aedb]/55',
        )}
      >
        <SectionHeader
          variant={isAdmin ? 'calm' : 'default'}
          icon={AlertTriangle}
          title="Tác vụ lỗi cần xử lý"
          hint={
            isAdmin
              ? 'Chỉ hiển thị các luồng MF-01 (PRACTICE), MF-02 (MOCK_EXAM) và MF-05 (IN_CLASS_EXAM)'
              : 'Chỉ hiển thị luồng MF-04 (OFFICIAL_EXAM)'
          }
          action={
            <div className="flex flex-wrap items-center gap-2">
              <FilterDropMenu
                label="Luồng nghiệp vụ"
                value={flowType}
                options={flowOptions}
                onChange={(nextFlow) => setParams({ flow: nextFlow })}
                className={ds.filterFlow}
                buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
              />
              <FilterDropMenu
                label="Trạng thái"
                value={status}
                options={STATUS_OPTIONS}
                onChange={(nextStatus) => {
                  setParams({ status: nextStatus || 'all' })
                }}
                className={ds.filterStatus}
                buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
              />
              <button
                type="button"
                className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
                onClick={() => void load()}
                disabled={loading}
              >
                <RefreshCw
                  className={cn('size-4 motion-reduce:animate-none', loading && 'animate-spin')}
                  strokeWidth={1.5}
                  aria-hidden="true"
                />
                Làm mới
              </button>
            </div>
          }
        />

        {loading ? (
          <LoadingState label="Đang tải danh sách tác vụ lỗi…" className="min-h-48" />
        ) : problem && tasks.length === 0 ? null : tasks.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={AlertTriangle}
              title="Không có tác vụ lỗi phù hợp"
              description="Không tìm thấy tác vụ DLQ nào theo bộ lọc luồng nghiệp vụ và trạng thái hiện tại."
              action={
                status !== 'pending' || flowType !== defaultFlow ? (
                  <button
                    type="button"
                    onClick={() => setParams({ status: 'pending', flow: defaultFlow })}
                    className="inline-flex min-h-11 items-center rounded-xl border border-[#0F2854]/15 bg-white px-4 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                  >
                    Đặt lại bộ lọc mặc định
                  </button>
                ) : undefined
              }
            />
          </div>
        ) : (
          <div className="divide-y divide-[#0F2854]/8">
            {tasks.map((task) => {
              const isActionAllowed = !busyId && task.status === 'pending' && canActOnTask(task)
              const isTaskBusy = busyId === task.id
              return (
                <article
                  key={task.id}
                  className="grid gap-4 p-4 sm:p-5 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span
                        className={cn(
                          'inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-medium',
                          task.status === 'resolved'
                            ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                            : task.status === 'abandoned'
                              ? 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]'
                              : 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]',
                        )}
                      >
                        {task.status}
                      </span>
                      {task.flowType ? (
                        <span className="rounded-md border border-[#0F2854]/10 bg-[#F4FBFD] px-2 py-0.5 text-xs font-medium text-[#1C4D8D]">
                          {task.flowType}
                        </span>
                      ) : null}
                      <span className="font-mono text-xs font-medium text-[#0F2854]">
                        {task.taskType}
                      </span>
                      <span className="text-xs text-[#5b6b84] tabular-nums">
                        Retry {task.retryCount}
                      </span>
                    </div>
                    <p className="mt-2 text-sm font-medium text-[#0F2854] break-words">
                      {task.errorMessage}
                    </p>
                    <p className="mt-1 text-xs text-[#5b6b84] tabular-nums">
                      Tạo lúc: {formatDate(task.createdAt)}
                      {task.resolvedAt ? ` · Xử lý lúc: ${formatDate(task.resolvedAt)}` : ''}
                    </p>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    <button
                      type="button"
                      disabled={!isActionAllowed}
                      onClick={() => void replay(task)}
                      className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-45 motion-reduce:transition-none"
                    >
                      {isTaskBusy ? (
                        <LoaderCircle
                          className="size-4 animate-spin motion-reduce:animate-none"
                          strokeWidth={1.5}
                          aria-hidden="true"
                        />
                      ) : (
                        <RotateCcw className="size-4" strokeWidth={1.5} aria-hidden="true" />
                      )}
                      {isTaskBusy ? 'Đang replay...' : 'Replay task'}
                    </button>
                    <button
                      type="button"
                      disabled={!isActionAllowed}
                      onClick={() => {
                        setResolvingTask(task)
                        setResolutionNotes('')
                        setResolveProblem(null)
                      }}
                      className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-[#b7e4cf] bg-[#e8f8f1] px-3.5 text-xs font-medium text-[#16836a] transition-colors duration-150 hover:bg-[#d5f2e6] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#16836a] disabled:cursor-not-allowed disabled:opacity-45 motion-reduce:transition-none"
                    >
                      <CheckCircle2 className="size-4" strokeWidth={1.5} aria-hidden="true" />
                      Đánh dấu đã xử lý
                    </button>
                  </div>
                </article>
              )
            })}
          </div>
        )}
      </section>

      <FormDialog
        open={Boolean(resolvingTask)}
        onClose={() => {
          if (!busyId) {
            setResolvingTask(null)
            setResolveProblem(null)
          }
        }}
        busy={Boolean(busyId)}
        variant={isAdmin ? 'calm' : 'default'}
        icon={CheckCircle2}
        eyebrow="Xác nhận xử lý sự cố DLQ"
        title="Đánh dấu đã xử lý tác vụ DLQ"
        size="sm"
        footer={
          <SubmitDialogFooter
            form="dlq-resolve-form"
            onCancel={() => {
              setResolvingTask(null)
              setResolveProblem(null)
            }}
            label="Xác nhận đã xử lý"
            busyLabel="Đang lưu…"
            busy={Boolean(busyId)}
            icon={CheckCircle2}
            variant={isAdmin ? 'calm' : 'default'}
          />
        }
      >
        {resolvingTask ? (
          <form id="dlq-resolve-form" noValidate onSubmit={handleConfirmResolve} className="space-y-3">
            {resolveProblem ? (
              <AdminStateBanner
                problem={resolveProblem}
                onDismiss={() => setResolveProblem(null)}
              />
            ) : null}

            <div className="rounded-xl border border-[#0F2854]/10 bg-white p-3.5 text-xs text-[#5b6b84]">
              <p>
                Luồng:{' '}
                <strong className="font-semibold text-[#0F2854]">
                  {resolvingTask.flowType || 'Chưa phân luồng'}
                </strong>{' '}
                · Loại:{' '}
                <strong className="font-semibold text-[#1C4D8D]">{resolvingTask.taskType}</strong>
              </p>
              <p className="mt-1.5 break-words [overflow-wrap:anywhere] text-[#0F2854]">
                {resolvingTask.errorMessage}
              </p>
            </div>

            <div>
              <label
                htmlFor="dlqResolutionNotes"
                className="block text-xs font-medium text-[#0F2854]"
              >
                Ghi chú xử lý sự cố (lưu Audit)
              </label>
              <textarea
                id="dlqResolutionNotes"
                value={resolutionNotes}
                onChange={(event) => setResolutionNotes(event.target.value)}
                placeholder="Nhập nguyên nhân hoặc hướng xử lý thủ công…"
                className="mt-1.5 min-h-24 w-full rounded-xl border border-[#0F2854]/15 bg-white p-3 text-sm text-[#0F2854] outline-none placeholder:text-[#7c8ca1] focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/15"
              />
            </div>
          </form>
        ) : null}
      </FormDialog>
    </div>
  )
}
