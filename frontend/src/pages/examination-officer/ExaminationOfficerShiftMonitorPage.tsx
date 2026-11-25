import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  ArrowLeft,
  Ban,
  CheckCircle2,
  FileBarChart2,
  KeyRound,
  Laptop,
  Play,
  Radio,
  RefreshCw,
  ScanSearch,
  ShieldAlert,
} from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { proctorService } from '@/services/proctor.service'
import { officialExamService } from '@/services/officialExam.service'
import { useAntiCheatHub } from '@/hooks/useAntiCheatHub'
import type { ShiftAttendanceMonitor } from '@/types/proctor.types'
import type {
  AuditEvidenceItem,
  ExamAntiCheatLog,
  OfficialShiftStatusValue,
} from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import {
  UnlockDeviceDialog,
  type UnlockDeviceTarget,
} from '@/components/examination-officer/UnlockDeviceDialog'
import {
  SuspendTicketDialog,
  type SuspendTicketTarget,
} from '@/components/examination-officer/SuspendTicketDialog'
import {
  formatEoTimeRange,
  getShiftStatusMeta,
  getTicketStatusMeta,
} from '@/components/examination-officer/eoFormatters'
import { userMessage } from '@/lib/userMessage'

const NON_UNLOCKABLE_TICKET_STATUSES = new Set([
  'SUBMITTED',
  'AI_GRADED',
  'AUDITED',
  'PUBLISHED',
  'LOCKED',
])

const NON_SUSPENDABLE_TICKET_STATUSES = new Set(['SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED', 'LOCKED'])

type SeatFilter = 'all' | 'in_progress' | 'flagged' | 'byod'

function formatDeviceShort(deviceId?: string | null) {
  if (!deviceId) return 'Chưa gắn thiết bị'
  return deviceId.length > 14 ? `${deviceId.slice(0, 12)}…` : deviceId
}

export function ExaminationOfficerShiftMonitorPage() {
  const { shiftId = '' } = useParams<{ shiftId: string }>()
  const [attendance, setAttendance] = useState<ShiftAttendanceMonitor | null>(null)
  const [evidence, setEvidence] = useState<AuditEvidenceItem[]>([])
  const [antiCheatLogs, setAntiCheatLogs] = useState<ExamAntiCheatLog[]>([])
  const [unlockTarget, setUnlockTarget] = useState<UnlockDeviceTarget | null>(null)
  const [suspendTarget, setSuspendTarget] = useState<SuspendTicketTarget | null>(null)
  const [seatFilter, setSeatFilter] = useState<SeatFilter>('all')
  const [confirmShiftAction, setConfirmShiftAction] = useState<OfficialShiftStatusValue | null>(null)
  const [statusBusy, setStatusBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    if (!shiftId) {
      setLoading(false)
      return
    }
    setLoading(true)
    setError('')
    const [attendanceResult, evidenceResult, antiCheatResult] = await Promise.allSettled([
      proctorService.getShiftAttendance(shiftId),
      officialExamService.getAuditEvidence(shiftId),
      officialExamService.getAntiCheatLogs(shiftId),
    ])
    if (attendanceResult.status === 'fulfilled') setAttendance(attendanceResult.value)
    if (evidenceResult.status === 'fulfilled') setEvidence(evidenceResult.value)
    if (antiCheatResult.status === 'fulfilled') setAntiCheatLogs(antiCheatResult.value)
    if (
      attendanceResult.status === 'rejected' &&
      evidenceResult.status === 'rejected' &&
      antiCheatResult.status === 'rejected'
    ) {
      setError(userMessage(attendanceResult.reason, 'Không tải được dữ liệu giám sát.'))
    }
    setLoading(false)
  }, [shiftId])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const antiCheatHub = useAntiCheatHub(shiftId || null, {
    enabled: Boolean(shiftId),
    onFallbackSync: (logs) => {
      if (logs.length > 0) setAntiCheatLogs(logs)
    },
  })

  const mergedLogs = useMemo(() => {
    const seen = new Map<string, ExamAntiCheatLog>()
    for (const item of [...antiCheatHub.alerts, ...antiCheatLogs]) {
      const key = item.clientEventId || item.id
      if (!seen.has(key)) seen.set(key, item)
    }
    return Array.from(seen.values()).sort(
      (left, right) => Date.parse(right.occurredAt) - Date.parse(left.occurredAt),
    )
  }, [antiCheatHub.alerts, antiCheatLogs])

  const liveViolationCountByTicket = useMemo(() => {
    const map = new Map<string, number>()
    for (const item of mergedLogs) {
      if (!item.ticketId) continue
      const prev = map.get(item.ticketId) ?? 0
      map.set(item.ticketId, Math.max(prev, item.violationCount))
    }
    return map
  }, [mergedLogs])

  const seats = useMemo(() => attendance?.seats ?? [], [attendance])

  const filteredSeats = useMemo(() => {
    return seats.filter((seat) => {
      const liveViolations = Math.max(
        seat.violationCount ?? 0,
        seat.ticketId ? (liveViolationCountByTicket.get(seat.ticketId) ?? 0) : 0,
      )
      if (seatFilter === 'in_progress') {
        return (seat.status ?? '').toUpperCase() === 'IN_PROGRESS'
      }
      if (seatFilter === 'flagged') {
        return seat.isSuspended || liveViolations > 0
      }
      if (seatFilter === 'byod') {
        return Boolean(seat.deviceId)
      }
      return true
    })
  }, [liveViolationCountByTicket, seatFilter, seats])

  const normalizedShiftStatus = (attendance?.shiftStatus ?? '').trim().toLowerCase()
  const shiftStatusMeta = getShiftStatusMeta(attendance?.shiftStatus)
  const canSuspendTickets = typeof proctorService.suspendTicket === 'function'
  const canUpdateShiftStatus = typeof officialExamService.updateShiftStatus === 'function'

  const executeShiftStatusChange = async () => {
    if (!confirmShiftAction || !shiftId || statusBusy || !canUpdateShiftStatus) return
    setStatusBusy(true)
    try {
      await officialExamService.updateShiftStatus(shiftId, confirmShiftAction)
      toast.success(
        confirmShiftAction === 'in_progress'
          ? 'Đã bắt đầu ca thi phòng Lab.'
          : 'Đã kết thúc ca thi. Bạn có thể chuyển sang công bố điểm.',
      )
      setConfirmShiftAction(null)
      await load()
    } catch (statusError) {
      toast.error(userMessage(statusError, 'Không thể chuyển trạng thái ca thi.'))
      setConfirmShiftAction(null)
    } finally {
      setStatusBusy(false)
    }
  }

  return (
    <div className={ds.page}>
      <PageBanner
        icon={ScanSearch}
        eyebrow="GIÁM SÁT CA THI"
        title={attendance?.shiftName || 'Giám sát MF-04'}
        description={
          attendance
            ? `${attendance.roomLab} · ${formatEoTimeRange(attendance.startTime, attendance.endTime)} · ${shiftStatusMeta.label}`
            : 'Điểm danh, trạng thái vé, thiết bị BYOD, đình chỉ vi phạm và cảnh báo Anti-Cheat thời gian thực.'
        }
      />

      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <Link to="/examination-officer/monitor" className={ds.outlineButton}>
            <ArrowLeft className="size-4" /> Danh sách ca
          </Link>
          <button
            type="button"
            onClick={() => void load()}
            disabled={loading}
            className={ds.outlineButton}
          >
            <RefreshCw className={loading ? 'size-4 animate-spin' : 'size-4'} /> Làm mới
          </button>
          <span
            className={
              antiCheatHub.status === 'connected'
                ? ds.successChip
                : antiCheatHub.status === 'reconnecting'
                  ? 'rounded-full border border-[#ead9ae] bg-[#fff9ec] px-2.5 py-1 text-xs font-bold text-[#8a5d17]'
                  : ds.infoChip
            }
          >
            <Radio className="mr-1 inline size-3" />
            {antiCheatHub.status === 'connected'
              ? 'Kết nối SignalR trực tiếp'
              : antiCheatHub.status === 'reconnecting'
                ? 'Đang kết nối lại SignalR'
                : 'Đồng bộ dự phòng'}
          </span>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {canUpdateShiftStatus && normalizedShiftStatus === 'scheduled' ? (
            <button
              type="button"
              onClick={() => setConfirmShiftAction('in_progress')}
              disabled={statusBusy}
              className={ds.primaryButton}
            >
              <Play className="size-4" /> Bắt đầu ca thi
            </button>
          ) : null}
          {canUpdateShiftStatus && normalizedShiftStatus === 'in_progress' ? (
            <button
              type="button"
              onClick={() => setConfirmShiftAction('completed')}
              disabled={statusBusy}
              className={ds.outlineButton}
            >
              <CheckCircle2 className="size-4" /> Kết thúc ca thi
            </button>
          ) : null}
          <Link to={`/examination-officer/grades/${shiftId}`} className={ds.outlineButton}>
            <FileBarChart2 className="size-4" /> Công bố điểm
          </Link>
        </div>
      </div>

      {error ? (
        <div
          role="alert"
          className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          {error}
        </div>
      ) : null}

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {[
          ['Tổng vé', attendance?.assignedTicketsCount ?? 0, 'text-[#0F2854]'],
          ['Đã điểm danh', attendance?.checkedInCount ?? 0, 'text-[#16836a]'],
          ['Đang thi', attendance?.inProgressCount ?? 0, 'text-[#1C4D8D]'],
          ['Đã nộp', attendance?.submittedCount ?? 0, 'text-[#0F2854]'],
          ['Đình chỉ', attendance?.suspendedCount ?? 0, 'text-[#c2415b]'],
        ].map(([label, value, colorClass]) => (
          <article
            key={String(label)}
            className="rounded-2xl border border-[#82aedb]/55 bg-white px-4 py-4 shadow-[0_8px_18px_rgba(15,40,84,0.08)]"
          >
            <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
              {label}
            </p>
            <strong className={cn('mt-1 block text-2xl font-black', colorClass)}>{value}</strong>
          </article>
        ))}
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={ShieldAlert}
          title={`Sơ đồ ghế (${attendance?.totalSeats ?? 24} chỗ)`}
          badge={filteredSeats.length}
          hint="Theo dõi điểm danh, thiết bị BYOD, mở khóa máy khi sự cố hoặc đình chỉ thi khi vi phạm quy chế."
          action={
            <div className="flex flex-wrap gap-1.5" role="group" aria-label="Lọc sơ đồ ghế">
              {(
                [
                  ['all', 'Tất cả'],
                  ['in_progress', 'Đang thi'],
                  ['flagged', 'Vi phạm / Đình chỉ'],
                  ['byod', 'Đã gắn máy'],
                ] as Array<[SeatFilter, string]>
              ).map(([key, label]) => (
                <button
                  key={key}
                  type="button"
                  onClick={() => setSeatFilter(key)}
                  className={cn(
                    'rounded-lg px-2.5 py-1.5 text-xs font-bold transition',
                    seatFilter === key
                      ? 'bg-[#1C4D8D] text-white'
                      : 'border border-[#d9e8f2] bg-white text-[#52657d] hover:bg-[#F4FBFD]',
                  )}
                >
                  {label}
                </button>
              ))}
            </div>
          }
        />
        <div className="grid gap-2.5 p-5 sm:grid-cols-2 md:grid-cols-4 lg:grid-cols-6">
          {filteredSeats.map((seat) => {
            const liveViolations = Math.max(
              seat.violationCount ?? 0,
              seat.ticketId ? (liveViolationCountByTicket.get(seat.ticketId) ?? 0) : 0,
            )
            const statusUpper = (seat.status || 'SCHEDULED').toUpperCase()
            const ticketStatusMeta = getTicketStatusMeta(seat.status)
            const canUnlock = Boolean(
              seat.ticketId &&
                seat.deviceId &&
                !seat.isSuspended &&
                !NON_UNLOCKABLE_TICKET_STATUSES.has(statusUpper),
            )
            const canSuspend = Boolean(
              canSuspendTickets &&
                seat.ticketId &&
                !seat.isSuspended &&
                !NON_SUSPENDABLE_TICKET_STATUSES.has(statusUpper),
            )

            return (
              <article
                key={seat.seatNumber}
                className={cn(
                  'flex flex-col justify-between rounded-xl border p-3',
                  seat.isSuspended || liveViolations > 0
                    ? 'border-[#f3c2c8] bg-[#fff4f5]'
                    : seat.isCheckedIn
                      ? 'border-[#b7e4cf] bg-[#effaf6]'
                      : 'border-[#d9e8f2] bg-[#F8FBFD]',
                )}
              >
                <div>
                  <div className="flex items-center justify-between gap-1">
                    <strong className="text-base text-[#0F2854]">
                      #{String(seat.seatNumber).padStart(2, '0')}
                    </strong>
                    <div className="flex items-center gap-1">
                      {liveViolations > 0 ? (
                        <span className="rounded-full bg-[#fdecec] px-1.5 py-0.5 font-mono text-[10px] font-bold text-[#a43d4b]">
                          VP: {liveViolations}
                        </span>
                      ) : null}
                      <span className="text-[11px] font-bold text-[#5b6b84]">
                        {ticketStatusMeta.label}
                      </span>
                    </div>
                  </div>
                  <p className="mt-2 truncate text-xs font-semibold text-[#0F2854]">
                    {seat.studentCode || seat.studentId || 'Chưa cấp vé'}
                  </p>
                  <p className="mt-0.5 truncate text-xs text-[#5b6b84]">{seat.fullName || '—'}</p>
                  <p className="mt-1 truncate text-[11px] font-semibold text-[#245997]">
                    Bộ đề: {seat.setCode || '—'}
                  </p>
                  <p
                    className="mt-1 flex items-center gap-1 truncate font-mono text-[11px] text-[#5b6b84]"
                    title={seat.deviceId ?? undefined}
                  >
                    <Laptop className="size-3 shrink-0" />
                    {formatDeviceShort(seat.deviceId)}
                  </p>
                  {seat.isSuspended && seat.suspensionReason ? (
                    <p
                      className="mt-1.5 line-clamp-2 rounded-lg bg-[#fdecec] px-2 py-1 text-[11px] font-medium text-[#a43d4b]"
                      title={seat.suspensionReason}
                    >
                      {seat.suspensionReason}
                    </p>
                  ) : null}
                </div>

                {canUnlock || canSuspend ? (
                  <div className="mt-2.5 flex flex-col gap-1.5">
                    {canUnlock ? (
                      <button
                        type="button"
                        onClick={() =>
                          setUnlockTarget({
                            ticketId: seat.ticketId!,
                            studentCode: seat.studentCode || seat.studentId || 'Thí sinh',
                            fullName: seat.fullName,
                            seatNumber: seat.seatNumber,
                            deviceId: seat.deviceId,
                            deviceUnlockCount: seat.deviceUnlockCount,
                          })
                        }
                        className="inline-flex w-full items-center justify-center gap-1 rounded-lg border border-[#82aedb]/65 bg-white px-2 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#e9f4fc]"
                      >
                        <KeyRound className="size-3" /> Mở khóa máy
                      </button>
                    ) : null}
                    {canSuspend ? (
                      <button
                        type="button"
                        onClick={() =>
                          setSuspendTarget({
                            ticketId: seat.ticketId!,
                            studentCode: seat.studentCode || seat.studentId || 'Thí sinh',
                            fullName: seat.fullName,
                            seatNumber: seat.seatNumber,
                            violationCount: liveViolations,
                          })
                        }
                        className="inline-flex w-full items-center justify-center gap-1 rounded-lg border border-[#f3c2c8] bg-white px-2 py-1 text-[11px] font-bold text-[#c2415b] transition hover:bg-[#fdecec]"
                      >
                        <Ban className="size-3" /> Đình chỉ thi
                      </button>
                    ) : null}
                  </div>
                ) : null}
              </article>
            )
          })}
          {!loading && filteredSeats.length === 0 ? (
            <div className="col-span-full">
              <EmptyState
                compact
                icon={ScanSearch}
                title={seats.length === 0 ? 'Chưa có dữ liệu ghế' : 'Không có ghế phù hợp bộ lọc'}
                description={
                  seats.length === 0
                    ? 'Ca thi chưa có dữ liệu điểm danh.'
                    : 'Thử chuyển về bộ lọc "Tất cả" để xem toàn bộ sơ đồ ghế.'
                }
              />
            </div>
          ) : null}
        </div>
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={ShieldAlert}
          title="Nhật ký chống gian lận (Realtime + REST)"
          badge={mergedLogs.length}
          hint="Tự động hợp nhất cảnh báo từ /hubs/anti-cheat và lịch sử exam_anti_cheat_logs."
        />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
              <tr>
                <th className="px-5 py-3">Sinh viên</th>
                <th className="px-5 py-3">Sự kiện</th>
                <th className="px-5 py-3">Mức độ</th>
                <th className="px-5 py-3">Lần vi phạm</th>
                <th className="px-5 py-3">Chi tiết</th>
                <th className="px-5 py-3">Thời điểm</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#d9e8f2]">
              {mergedLogs.map((log) => {
                const severityUpper = log.severity.toUpperCase()
                const isSevere = severityUpper === 'HIGH' || severityUpper === 'CRITICAL'
                return (
                  <tr
                    key={log.clientEventId || log.id}
                    className={isSevere ? 'bg-[#fff8f8]' : undefined}
                  >
                    <td className="px-5 py-3 font-semibold text-[#0F2854]">
                      {log.studentCode} · {log.studentName}
                    </td>
                    <td className="px-5 py-3 font-mono text-xs text-[#5b6b84]">{log.eventType}</td>
                    <td className="px-5 py-3">
                      <span className={isSevere ? ds.dangerChip : ds.infoChip}>{log.severity}</span>
                    </td>
                    <td className="px-5 py-3 font-mono text-xs font-bold text-[#0F2854]">
                      {log.violationCount}
                    </td>
                    <td
                      className="max-w-[320px] truncate px-5 py-3 text-[#5b6b84]"
                      title={log.details ?? ''}
                    >
                      {log.details || '—'}
                    </td>
                    <td className="px-5 py-3 whitespace-nowrap text-[#5b6b84]">
                      {new Date(log.occurredAt).toLocaleString('vi-VN')}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        {!loading && mergedLogs.length === 0 ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={ShieldAlert}
              title="Chưa có log vi phạm"
              description="Ca thi chưa có sự kiện chống gian lận được ghi nhận."
            />
          </div>
        ) : null}
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={ShieldAlert}
          title="Bằng chứng hậu kiểm"
          badge={evidence.length}
          hint="Evidence Panel chỉ đọc cho Khảo thí."
        />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
              <tr>
                <th className="px-5 py-3">Sinh viên</th>
                <th className="px-5 py-3">Ghế</th>
                <th className="px-5 py-3">Điểm AI</th>
                <th className="px-5 py-3">Độ tin cậy</th>
                <th className="px-5 py-3">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#d9e8f2]">
              {evidence.map((item) => (
                <tr key={`${item.ticketId}-${item.questionId ?? 'summary'}`}>
                  <td className="px-5 py-3 font-semibold text-[#0F2854]">
                    {item.studentCode} · {item.fullName}
                  </td>
                  <td className="px-5 py-3 text-[#5b6b84]">#{item.seatNumber}</td>
                  <td className="px-5 py-3 text-[#5b6b84]">{item.aiScore ?? '—'}</td>
                  <td className="px-5 py-3 text-[#5b6b84]">{item.confidenceScore ?? '—'}</td>
                  <td className="px-5 py-3">
                    {item.isSuspicious ? (
                      <span className={ds.dangerChip}>Nghi ngờ</span>
                    ) : (
                      <span className={ds.successChip}>Bình thường</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!loading && evidence.length === 0 ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={ShieldAlert}
              title="Chưa có bằng chứng"
              description="Chưa có dữ liệu hậu kiểm cho ca thi này."
            />
          </div>
        ) : null}
      </section>

      <UnlockDeviceDialog
        open={Boolean(unlockTarget)}
        target={unlockTarget}
        onClose={() => setUnlockTarget(null)}
        onUnlocked={() => void load()}
      />

      <SuspendTicketDialog
        open={Boolean(suspendTarget)}
        target={suspendTarget}
        onClose={() => setSuspendTarget(null)}
        onSuspended={() => void load()}
      />

      {confirmShiftAction ? (
        <ConfirmDialog
          variant="calm"
          tone={confirmShiftAction === 'completed' ? 'warning' : 'info'}
          eyebrow="ĐIỀU KHIỂN CA THI"
          title={
            confirmShiftAction === 'in_progress'
              ? 'Bắt đầu ca thi phòng Lab?'
              : 'Kết thúc ca thi hiện tại?'
          }
          message={
            confirmShiftAction === 'in_progress'
              ? 'Ca thi sẽ chuyển sang trạng thái Đang diễn ra và mở đề thi cho các thí sinh đã điểm danh.'
              : 'Ca thi sẽ chuyển sang trạng thái Đã kết thúc để chuyển sang bước hậu kiểm và công bố điểm.'
          }
          confirmLabel={
            confirmShiftAction === 'in_progress' ? 'Xác nhận bắt đầu' : 'Xác nhận kết thúc'
          }
          cancelLabel="Hủy"
          pending={statusBusy}
          onConfirm={() => void executeShiftStatusChange()}
          onCancel={() => !statusBusy && setConfirmShiftAction(null)}
        />
      ) : null}
    </div>
  )
}
