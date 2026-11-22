import { useCallback, useEffect, useMemo, useState } from 'react'
import { ArrowLeft, KeyRound, Laptop, Radio, RefreshCw, ScanSearch, ShieldAlert } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { proctorService } from '@/services/proctor.service'
import { officialExamService } from '@/services/officialExam.service'
import { useAntiCheatHub } from '@/hooks/useAntiCheatHub'
import type { ShiftAttendanceMonitor } from '@/types/proctor.types'
import type { AuditEvidenceItem, ExamAntiCheatLog } from '@/types/officialExam.types'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { EmptyState } from '@/components/common/EmptyState'
import {
  UnlockDeviceDialog,
  type UnlockDeviceTarget,
} from '@/components/examination-officer/UnlockDeviceDialog'
import { userMessage } from '@/lib/userMessage'

const NON_UNLOCKABLE_TICKET_STATUSES = new Set([
  'SUBMITTED',
  'AI_GRADED',
  'AUDITED',
  'PUBLISHED',
  'LOCKED',
])

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

  const seats = attendance?.seats ?? []

  return (
    <div className={ds.page}>
      <PageBanner
        icon={ScanSearch}
        eyebrow="GIÁM SÁT CA THI"
        title={attendance?.shiftName || 'Giám sát MF-04'}
        description={
          attendance
            ? `${attendance.roomLab} · ${attendance.shiftStatus}`
            : 'Điểm danh, trạng thái vé, thiết bị BYOD và cảnh báo Anti-Cheat thời gian thực của ca thi.'
        }
      />
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
          ['Tổng vé', attendance?.assignedTicketsCount ?? 0],
          ['Đã điểm danh', attendance?.checkedInCount ?? 0],
          ['Đang thi', attendance?.inProgressCount ?? 0],
          ['Đã nộp', attendance?.submittedCount ?? 0],
          ['Đình chỉ', attendance?.suspendedCount ?? 0],
        ].map(([label, value]) => (
          <article
            key={String(label)}
            className="rounded-2xl border border-[#82aedb]/55 bg-white px-4 py-4 shadow-[0_8px_18px_rgba(15,40,84,0.1)]"
          >
            <p className="text-[11px] font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
              {label}
            </p>
            <strong className="mt-1 block text-2xl font-black text-[#0F2854]">{value}</strong>
          </article>
        ))}
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
              description="Ca thi chưa có sự kiện chống gian lận được backend trả về."
            />
          </div>
        ) : null}
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={ShieldAlert}
          title="Sơ đồ ghế (24 chỗ · 4×6)"
          badge={seats.length}
          hint="Dữ liệu điểm danh, thiết bị BYOD và số vi phạm theo từng ghế."
        />
        <div className="grid gap-2 p-5 sm:grid-cols-2 md:grid-cols-4 lg:grid-cols-6">
          {seats.map((seat) => {
            const liveViolations = Math.max(
              seat.violationCount ?? 0,
              seat.ticketId ? (liveViolationCountByTicket.get(seat.ticketId) ?? 0) : 0,
            )
            return (
              <article
                key={seat.seatNumber}
                className={`rounded-xl border p-3 ${
                  seat.isSuspended || liveViolations > 0
                    ? 'border-[#f3c2c8] bg-[#fff4f5]'
                    : seat.isCheckedIn
                      ? 'border-[#b7e4cf] bg-[#effaf6]'
                      : 'border-[#d9e8f2] bg-[#F8FBFD]'
                }`}
              >
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
                      {seat.status || 'SCHEDULED'}
                    </span>
                  </div>
                </div>
                <p className="mt-2 truncate text-xs font-semibold text-[#0F2854]">
                  {seat.studentCode || seat.studentId || 'Chưa cấp vé'}
                </p>
                <p className="mt-1 truncate text-xs text-[#5b6b84]">{seat.fullName || '—'}</p>
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
                {seat.ticketId &&
                seat.deviceId &&
                !seat.isSuspended &&
                !NON_UNLOCKABLE_TICKET_STATUSES.has((seat.status || '').toUpperCase()) ? (
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
                    className="mt-2 inline-flex w-full items-center justify-center gap-1 rounded-lg border border-[#82aedb]/65 bg-white px-2 py-1 text-[11px] font-bold text-[#1C4D8D] transition hover:bg-[#e9f4fc]"
                  >
                    <KeyRound className="size-3" /> Mở khóa máy
                  </button>
                ) : null}
              </article>
            )
          })}
          {!loading && seats.length === 0 ? (
            <EmptyState
              compact
              icon={ScanSearch}
              title="Chưa có dữ liệu ghế"
              description="Ca thi chưa có dữ liệu điểm danh."
            />
          ) : null}
        </div>
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={ShieldAlert}
          title="Bằng chứng hậu kiểm"
          badge={evidence.length}
          hint="Evidence Panel chỉ đọc cho Khảo thí"
        />
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
              <tr>
                <th className="px-5 py-3">Sinh viên</th>
                <th className="px-5 py-3">Ghế</th>
                <th className="px-5 py-3">AI score</th>
                <th className="px-5 py-3">Confidence</th>
                <th className="px-5 py-3">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#d9e8f2]">
              {evidence.map((item) => (
                <tr key={`${item.ticketId}-${item.questionId ?? 'summary'}`}>
                  <td className="px-5 py-3 font-semibold text-[#0F2854]">
                    {item.studentCode} · {item.fullName}
                  </td>
                  <td className="px-5 py-3 text-[#5b6b84]">{item.seatNumber}</td>
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
              description="Backend chưa trả về evidence cho ca thi này."
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
    </div>
  )
}
