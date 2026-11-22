import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AlertTriangle,
  Armchair,
  CheckCircle2,
  Laptop,
  LoaderCircle,
  MonitorCheck,
  Radio,
  RefreshCw,
  ShieldAlert,
  UserCheck,
  UserX,
} from 'lucide-react'
import { proctorService } from '@/services/proctor.service'
import { officialExamService } from '@/services/officialExam.service'
import { useAuth } from '@/hooks/useAuth'
import { useAntiCheatHub } from '@/hooks/useAntiCheatHub'
import type { ExamShift } from '@/types/officialExam.types'
import type { ProctorSeatItem, ShiftAttendanceMonitor } from '@/types/proctor.types'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { BannerFact, LecturerPageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { attendanceRoles } from '@/lib/task09Permissions'

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  return 'Không thể hoàn tất yêu cầu. Vui lòng thử lại.'
}

function formatDeviceShort(deviceId?: string | null) {
  if (!deviceId) return 'Chưa gắn thiết bị'
  return deviceId.length > 14 ? `${deviceId.slice(0, 12)}…` : deviceId
}

export function ProctorAttendancePage() {
  const { user } = useAuth()
  const [availableShifts, setAvailableShifts] = useState<ExamShift[]>([])
  const [loadingShifts, setLoadingShifts] = useState(false)
  const [shiftId, setShiftId] = useState('')
  const [monitor, setMonitor] = useState<ShiftAttendanceMonitor | null>(null)
  const [selectedSeat, setSelectedSeat] = useState<ProctorSeatItem | null>(null)
  const [violationEventType, setViolationEventType] = useState('PROCTOR_REPORTED_VIOLATION')
  const [violationSeverity, setViolationSeverity] = useState('HIGH')
  const [violationDetails, setViolationDetails] = useState('')
  const [violationDetailsError, setViolationDetailsError] = useState('')
  const [suspendReason, setSuspendReason] = useState('')
  const [blurViolationCount, setBlurViolationCount] = useState(0)
  const [shiftIdError, setShiftIdError] = useState('')
  const [suspendReasonError, setSuspendReasonError] = useState('')
  const [blurCountError, setBlurCountError] = useState('')
  const [targetSeatInput, setTargetSeatInput] = useState('')
  const [seatChangeError, setSeatChangeError] = useState('')
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const canView = Boolean(user && attendanceRoles.includes(user.role))
  const canRecordAttendance = user?.role === 'Proctor'
  const canSuspendTicket = user?.role === 'ExaminationOfficer'

  const loadAttendanceFor = useCallback(
    async (targetShiftId: string) => {
      if (!canView) return
      const trimmed = targetShiftId.trim()
      if (!trimmed) {
        setShiftIdError('Vui lòng chọn ca thi cần điểm danh.')
        return
      }
      setShiftIdError('')
      setLoading(true)
      setError('')
      try {
        const result = await proctorService.getShiftAttendance(trimmed)
        setMonitor(result)
        setSelectedSeat(null)
      } catch (loadError) {
        setMonitor(null)
        setError(getErrorMessage(loadError))
      } finally {
        setLoading(false)
      }
    },
    [canView],
  )

  const antiCheatHub = useAntiCheatHub(monitor?.shiftId ?? null, {
    enabled: Boolean(canView && monitor?.shiftId),
  })

  useEffect(() => {
    if (!canView) return
    const timer = window.setTimeout(() => {
      const params = new URLSearchParams(window.location.search)
      const initialShiftId = params.get('shiftId') ?? ''
      if (initialShiftId) {
        setShiftId(initialShiftId)
        void loadAttendanceFor(initialShiftId)
      }

      if (typeof officialExamService.getShifts === 'function') {
        setLoadingShifts(true)
        const query =
          user?.role === 'Proctor' && user?.id ? { proctorUserId: user.id } : undefined
        void officialExamService
          .getShifts(query)
          .then((list) => {
            setAvailableShifts(Array.isArray(list) ? list : [])
            if (!initialShiftId && Array.isArray(list) && list.length === 1) {
              setShiftId(list[0].id)
            }
          })
          .catch(() => {
            setAvailableShifts([])
          })
          .finally(() => setLoadingShifts(false))
      }
    }, 0)
    return () => window.clearTimeout(timer)
  }, [canView, loadAttendanceFor, user?.id, user?.role])

  const liveViolationCountByTicket = useMemo(() => {
    const map = new Map<string, number>()
    for (const alert of antiCheatHub.alerts) {
      if (!alert.ticketId) continue
      const prev = map.get(alert.ticketId) ?? 0
      map.set(alert.ticketId, Math.max(prev, alert.violationCount))
    }
    return map
  }, [antiCheatHub.alerts])

  const updateSeat = async (seat: ProctorSeatItem, present: boolean) => {
    if (!seat.ticketId || !canRecordAttendance) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await proctorService.updateTicketAttendance(seat.ticketId, {
        isPresent: present,
      })
      setNotice(
        present
          ? `Đã điểm danh ghế ${seat.seatNumber}.`
          : `Đã ghi nhận ghế ${seat.seatNumber} vắng.`,
      )
      await loadAttendanceFor(shiftId)
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
  }

  const changeSeat = async () => {
    if (!monitor || !selectedSeat?.ticketId || !canRecordAttendance) return
    const trimmed = targetSeatInput.trim()
    const parsedSeat = Number(trimmed)
    if (!trimmed || !Number.isInteger(parsedSeat) || /[.,]/.test(trimmed)) {
      setSeatChangeError(`Số ghế mới phải là số nguyên từ 1 đến ${monitor.totalSeats}.`)
      return
    }
    if (parsedSeat < 1 || parsedSeat > monitor.totalSeats) {
      setSeatChangeError(
        `Không thể đổi vé sang ghế ngoài phạm vi 1–${monitor.totalSeats} của ca thi.`,
      )
      return
    }
    if (parsedSeat === selectedSeat.seatNumber) {
      setSeatChangeError('Vui lòng chọn số ghế khác với ghế hiện tại.')
      return
    }
    const existingAtTarget = monitor.seats.find(
      (item) =>
        item.seatNumber === parsedSeat &&
        item.isOccupied &&
        item.ticketId !== selectedSeat.ticketId,
    )
    if (existingAtTarget) {
      setSeatChangeError(`Ghế #${parsedSeat} đã được phân cho sinh viên khác.`)
      return
    }

    setSeatChangeError('')
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await proctorService.updateTicketAttendance(selectedSeat.ticketId, {
        isPresent: selectedSeat.isCheckedIn,
        seatNumber: parsedSeat,
      })
      setNotice(`Đã chuyển vé từ ghế #${selectedSeat.seatNumber} sang ghế #${parsedSeat}.`)
      setTargetSeatInput('')
      await loadAttendanceFor(shiftId)
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
  }

  const reportViolation = async () => {
    if (!monitor?.shiftId || !selectedSeat?.ticketId || !canRecordAttendance) return
    if (violationDetails.trim().length < 5) {
      setViolationDetailsError('Chi tiết biên bản vi phạm cần ít nhất 5 ký tự.')
      return
    }
    setViolationDetailsError('')
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await proctorService.recordViolation(monitor.shiftId, {
        ticketId: selectedSeat.ticketId,
        eventType: violationEventType,
        severity: violationSeverity,
        details: violationDetails.trim(),
      })
      setNotice(
        `Đã ghi nhận biên bản vi phạm tại ghế #${selectedSeat.seatNumber} vào exam_anti_cheat_logs.`,
      )
      setViolationDetails('')
      await loadAttendanceFor(shiftId)
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
  }

  const suspendSeat = async () => {
    if (!selectedSeat?.ticketId || !canSuspendTicket) return
    const reasonError =
      suspendReason.trim().length < 5 ? 'Lý do đình chỉ cần ít nhất 5 ký tự.' : ''
    const countError =
      !Number.isInteger(blurViolationCount) || blurViolationCount < 0
        ? 'Số lần blur phải là số nguyên không âm.'
        : ''
    setSuspendReasonError(reasonError)
    setBlurCountError(countError)
    if (reasonError || countError) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await proctorService.suspendTicket(selectedSeat.ticketId, {
        reason: suspendReason.trim(),
        blurViolationCount,
      })
      setNotice(`Đã đình chỉ vé thi tại ghế ${selectedSeat.seatNumber}.`)
      setSuspendReason('')
      setSuspendReasonError('')
      setBlurCountError('')
      setSelectedSeat(null)
      await loadAttendanceFor(shiftId)
    } catch (actionError) {
      setError(getErrorMessage(actionError))
    } finally {
      setBusy(false)
    }
  }

  if (!canView) {
    return (
      <div role="alert">Chỉ Giám thị được phân công và Khảo thí được truy cập ca thi.</div>
    )
  }

  const seatCapacity =
    typeof monitor?.totalSeats === 'number' && monitor.totalSeats > 0
      ? monitor.totalSeats
      : monitor?.seats.length

  const renderedSeats: ProctorSeatItem[] = monitor
    ? Array.from({ length: seatCapacity ?? 0 }, (_, index) => {
        const seatNumber = index + 1
        const found = monitor.seats.find((item) => item.seatNumber === seatNumber)
        return (
          found ?? {
            seatNumber,
            isOccupied: false,
            ticketId: null,
            studentId: null,
            studentCode: null,
            fullName: null,
            ipAddress: null,
            deviceId: null,
            violationCount: 0,
            setCode: null,
            status: null,
            isCheckedIn: false,
            isSuspended: false,
            suspensionReason: null,
            checkedInAt: null,
            submittedAt: null,
          }
        )
      })
    : []

  const hasCurrentShiftInList = availableShifts.some((item) => item.id === shiftId)

  return (
    <div className={ds.page}>
      <LecturerPageBanner
        icon={MonitorCheck}
        eyebrow={
          seatCapacity ? `Giám thị phòng Lab (${seatCapacity} ghế)` : 'Giám thị phòng Lab'
        }
        title="Điểm danh & Báo cáo vi phạm phòng thi"
        description={
          seatCapacity
            ? `Giám thị đối chiếu trực tiếp thẻ SV/CCCD điểm danh STT (SeatNumber 1–${seatCapacity}, không lưu CCCD trên trình duyệt), giám sát thiết bị BYOD & cảnh báo Anti-Cheat thời gian thực.`
            : 'Chọn ca thi được phân công để đối chiếu thẻ SV/CCCD, theo dõi sơ đồ 24 ghế phòng Lab và nhận cảnh báo Anti-Cheat thời gian thực.'
        }
        extras={
          <>
            <BannerFact icon={Armchair}>
              {seatCapacity ? `Sơ đồ ${seatCapacity} ghế Lab` : 'Sơ đồ 24 ghế (4×6)'}
            </BannerFact>
            <BannerFact icon={UserCheck} tone="green">
              Điểm danh thẻ SV/CCCD
            </BannerFact>
            <BannerFact icon={ShieldAlert} tone="violet">
              Cảnh báo Anti-Cheat Live
            </BannerFact>
          </>
        }
      />

      <form
        className={cn(ds.section, 'flex flex-col gap-3 p-4 sm:flex-row sm:items-start')}
        onSubmit={(event) => {
          event.preventDefault()
          void loadAttendanceFor(shiftId)
        }}
      >
        <div className="min-w-0 flex-1 sm:max-w-lg">
          <label className="sr-only" htmlFor="proctor-shift-select">
            Chọn ca thi
          </label>
          <select
            id="proctor-shift-select"
            aria-label="Chọn ca thi"
            className={cn(ds.field, shiftIdError && fieldInvalidClass)}
            value={shiftId}
            disabled={loadingShifts}
            onChange={(event) => {
              const nextId = event.target.value
              setShiftId(nextId)
              if (shiftIdError) setShiftIdError('')
              if (nextId) {
                const url = new URL(window.location.href)
                url.searchParams.set('shiftId', nextId)
                window.history.replaceState({}, '', `${url.pathname}${url.search}`)
              }
            }}
            aria-invalid={Boolean(shiftIdError)}
          >
            <option value="">
              {loadingShifts
                ? 'Đang tải danh sách ca thi được phân công…'
                : 'Chọn ca thi phòng Lab'}
            </option>
            {shiftId && !hasCurrentShiftInList ? (
              <option value={shiftId}>
                {monitor?.shiftName ? `${monitor.shiftName} · ${monitor.roomLab}` : `Ca thi (${shiftId})`}
              </option>
            ) : null}
            {availableShifts.map((shift) => (
              <option key={shift.id} value={shift.id}>
                {shift.shiftName} · {shift.roomLab} ({shift.status})
              </option>
            ))}
          </select>
          <FieldError message={shiftIdError} />
        </div>
        <button type="submit" disabled={loading} className={ds.primaryButton}>
          Tải sơ đồ ca thi
        </button>
      </form>

      {(error || notice) && (
        <div
          role={error ? 'alert' : 'status'}
          className={cn(
            'rounded-xl border px-4 py-3 text-sm font-semibold',
            error
              ? 'border-[#f0c5c9] bg-[#fff5f6] text-[#a43c47]'
              : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]',
          )}
        >
          {error || notice}
        </div>
      )}

      {loading ? (
        <div className="flex min-h-[300px] items-center justify-center rounded-2xl bg-white">
          <LoaderCircle className="size-8 animate-spin text-[#1C4D8D]" />
        </div>
      ) : monitor ? (
        <>
          <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
            {[
              ['Sức chứa ca', monitor.totalSeats],
              ['Đã cấp vé', monitor.assignedTicketsCount],
              ['Đã điểm danh', monitor.checkedInCount],
              ['Đang thi', monitor.inProgressCount],
              ['Đã nộp', monitor.submittedCount],
              ['Đình chỉ', monitor.suspendedCount],
            ].map(([label, value]) => (
              <div
                key={String(label)}
                className="rounded-2xl border border-[#82aedb]/55 bg-white p-4"
              >
                <p className="text-xs font-bold text-[#708097]">{label}</p>
                <p className="mt-2 font-mono text-2xl font-black text-[#0F2854]">{value}</p>
              </div>
            ))}
          </section>

          <section className={ds.section}>
            <SectionHeader
              icon={UserCheck}
              title={`${monitor.shiftName} · ${monitor.roomLab}`}
              badge={`${monitor.assignedTicketsCount}/${monitor.totalSeats} ghế`}
              hint={`Điểm danh ghế Lab (1–${monitor.totalSeats}) · Còn trống ${Math.max(0, monitor.totalSeats - monitor.assignedTicketsCount)} ghế · trạng thái: ${monitor.shiftStatus}`}
              action={
                <div className="flex flex-wrap items-center gap-2">
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
                      ? 'Anti-Cheat Live'
                      : antiCheatHub.status === 'reconnecting'
                        ? 'Đang kết nối lại'
                        : 'Đồng bộ dự phòng'}
                  </span>
                  <button
                    type="button"
                    className={ds.outlineButton}
                    onClick={() => void loadAttendanceFor(shiftId)}
                  >
                    <RefreshCw className="size-4" /> Làm mới
                  </button>
                </div>
              }
            />
            <div className="grid grid-cols-2 gap-3 p-4 sm:grid-cols-4 lg:grid-cols-6">
              {renderedSeats.map((seat) => {
                const liveViolations = Math.max(
                  seat.violationCount ?? 0,
                  seat.ticketId ? (liveViolationCountByTicket.get(seat.ticketId) ?? 0) : 0,
                )
                const hasAlert = liveViolations > 0
                return (
                  <button
                    key={seat.seatNumber}
                    type="button"
                    onClick={() => {
                      setSelectedSeat(seat)
                      setTargetSeatInput(String(seat.seatNumber))
                      setSeatChangeError('')
                      setViolationDetailsError('')
                      setSuspendReasonError('')
                      setBlurCountError('')
                    }}
                    className={cn(
                      'min-h-32 rounded-2xl border p-3 text-left transition hover:-translate-y-0.5',
                      seat.isSuspended
                        ? 'border-[#f0c5c9] bg-[#fff5f6]'
                        : hasAlert
                          ? 'border-[#e5989b] bg-[#fff5f6]'
                          : seat.isCheckedIn
                            ? 'border-[#b7e4cf] bg-[#effaf6]'
                            : seat.isOccupied
                              ? 'border-[#ead9ae] bg-[#fff9ec]'
                              : 'border-[#82aedb]/55 bg-white',
                    )}
                  >
                    <div className="flex items-center justify-between gap-1">
                      <span className="font-mono text-sm font-black text-[#1C4D8D]">
                        #{String(seat.seatNumber).padStart(2, '0')}
                      </span>
                      <div className="flex items-center gap-1">
                        {liveViolations > 0 ? (
                          <span className="rounded-full bg-[#fdecec] px-1.5 py-0.5 font-mono text-[10px] font-bold text-[#a43d4b]">
                            VP: {liveViolations}
                          </span>
                        ) : null}
                        {seat.isSuspended ? (
                          <AlertTriangle className="size-4 text-[#b8404b]" />
                        ) : seat.isCheckedIn ? (
                          <CheckCircle2 className="size-4 text-[#16836a]" />
                        ) : (
                          <UserX className="size-4 text-[#9aa8ba]" />
                        )}
                      </div>
                    </div>
                    <p className="mt-2.5 truncate text-xs font-bold text-[#0F2854]">
                      {seat.fullName || 'Ghế trống'}
                    </p>
                    <p className="mt-1 truncate font-mono text-[10px] text-[#718298]">
                      {seat.studentCode || 'Chưa cấp vé'}
                    </p>
                    {seat.isOccupied ? (
                      <>
                        <p className="mt-1 truncate text-[10px] font-semibold text-[#245997]">
                          Đề: {seat.setCode || '—'} · {seat.status || 'SCHEDULED'}
                        </p>
                        <p
                          className="mt-1 flex items-center gap-1 truncate font-mono text-[10px] text-[#5b6b84]"
                          title={seat.deviceId ?? undefined}
                        >
                          <Laptop className="size-3 shrink-0" />
                          {formatDeviceShort(seat.deviceId)}
                        </p>
                      </>
                    ) : null}
                  </button>
                )
              })}
            </div>
          </section>

          <section className={ds.section} aria-label="Cảnh báo chống gian lận thời gian thực">
            <SectionHeader
              icon={ShieldAlert}
              title="Cảnh báo Anti-Cheat thời gian thực"
              badge={antiCheatHub.alerts.length}
              hint="Nhận sự kiện trực tiếp từ /hubs/anti-cheat khi thí sinh thoát toàn màn hình hoặc vi phạm Kiosk."
            />
            {antiCheatHub.alerts.length === 0 ? (
              <div className="p-4 text-xs font-medium text-[#687a91]">
                Chưa có cảnh báo chống gian lận thời gian thực trong ca thi này.
              </div>
            ) : (
              <ul className="divide-y divide-[#d9e8f2] px-4 py-2">
                {antiCheatHub.alerts.slice(0, 12).map((alert) => (
                  <li
                    key={alert.clientEventId || alert.id}
                    className="flex flex-wrap items-center justify-between gap-2 py-2.5 text-xs"
                  >
                    <div className="min-w-0">
                      <strong className="text-[#0F2854]">
                        {alert.studentCode || alert.studentId} · {alert.studentName || 'Thí sinh'}
                      </strong>{' '}
                      <span className="font-mono text-[#52657d]">[{alert.eventType}]</span>
                      {alert.details ? (
                        <span className="ml-2 text-[#5b6b84]">— {alert.details}</span>
                      ) : null}
                    </div>
                    <div className="flex items-center gap-2">
                      <span
                        className={
                          alert.severity.toUpperCase() === 'CRITICAL' ||
                          alert.severity.toUpperCase() === 'HIGH'
                            ? ds.dangerChip
                            : 'rounded-full border border-[#ead9ae] bg-[#fff9ec] px-2.5 py-1 text-xs font-bold text-[#8a5d17]'
                        }
                      >
                        {alert.severity} · Lần {alert.violationCount}
                      </span>
                      <span className="font-mono text-[11px] text-[#7c8ca1]">
                        {new Date(alert.occurredAt).toLocaleTimeString('vi-VN')}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </section>

          {selectedSeat?.ticketId && (
            <section className="space-y-4 rounded-2xl border border-[#82aedb]/55 bg-white p-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h2 className="font-bold text-[#0F2854]">
                    Ghế #{selectedSeat.seatNumber} ·{' '}
                    {selectedSeat.fullName || selectedSeat.studentCode}
                  </h2>
                  <p className="mt-1 text-xs text-[#687a91]">
                    MSSV: {selectedSeat.studentCode || '—'} · Trạng thái:{' '}
                    {selectedSeat.status || 'SCHEDULED'} · Thiết bị:{' '}
                    {selectedSeat.deviceId || 'Chưa gắn'} · Vi phạm:{' '}
                    {Math.max(
                      selectedSeat.violationCount ?? 0,
                      liveViolationCountByTicket.get(selectedSeat.ticketId) ?? 0,
                    )}{' '}
                    · Bộ đề: {selectedSeat.setCode || '—'}
                  </p>
                </div>
                {canRecordAttendance && (
                  <div className="flex gap-2">
                    <button
                      type="button"
                      disabled={busy || selectedSeat.isCheckedIn}
                      onClick={() => void updateSeat(selectedSeat, true)}
                      className={ds.primaryButton}
                    >
                      <UserCheck className="size-4" /> Điểm danh có mặt
                    </button>
                    <button
                      type="button"
                      disabled={busy || !selectedSeat.isCheckedIn}
                      onClick={() => void updateSeat(selectedSeat, false)}
                      className={ds.outlineButton}
                    >
                      <UserX className="size-4" /> Ghi vắng
                    </button>
                  </div>
                )}
              </div>

              {canRecordAttendance && (
                <div className="rounded-xl border border-[#d6e4f2] bg-[#f8fbfd] p-4">
                  <h3 className="text-xs font-bold tracking-wider text-[#1C4D8D] uppercase">
                    Đổi ghế trong phạm vi sức chứa ca thi (1–{monitor.totalSeats})
                  </h3>
                  <div className="mt-3 flex flex-wrap items-start gap-3">
                    <label
                      className={cn(fieldLabelClass(Boolean(seatChangeError)), 'min-w-[200px]')}
                    >
                      Số ghế mới (1–{monitor.totalSeats})
                      <input
                        type="number"
                        min={1}
                        max={monitor.totalSeats}
                        step={1}
                        aria-label="Số ghế mới"
                        className={cn(ds.field, 'mt-1', seatChangeError && fieldInvalidClass)}
                        value={targetSeatInput}
                        onChange={(event) => {
                          setTargetSeatInput(event.target.value)
                          if (seatChangeError) setSeatChangeError('')
                        }}
                      />
                      <FieldError message={seatChangeError} />
                    </label>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => void changeSeat()}
                      className={cn(ds.outlineButton, 'mt-5')}
                    >
                      <Armchair className="size-4" /> Đổi ghế
                    </button>
                  </div>
                </div>
              )}

              {canRecordAttendance && (
                <div className="rounded-xl border border-[#d6e4f2] bg-[#f8fbfd] p-4">
                  <h3 className="text-xs font-bold tracking-wider text-[#1C4D8D] uppercase">
                    Báo cáo vi phạm quy chế phòng thi (Lưu vào exam_anti_cheat_logs)
                  </h3>
                  <div className="mt-3 grid gap-3 sm:grid-cols-[180px_140px_1fr_auto] sm:items-start">
                    <label className="text-xs font-bold text-[#63758e]">
                      Loại vi phạm
                      <select
                        className={cn(ds.field, 'mt-1')}
                        value={violationEventType}
                        onChange={(event) => setViolationEventType(event.target.value)}
                      >
                        <option value="PROCTOR_REPORTED_VIOLATION">
                          Vi phạm quy chế phòng thi
                        </option>
                        <option value="TAB_SWITCH_OR_BLUR">Chuyển tab / thoát Kiosk</option>
                        <option value="UNAUTHORIZED_DEVICE">
                          Sử dụng tài liệu / thiết bị cấm
                        </option>
                        <option value="IDENTITY_MISMATCH">Sai lệch thẻ SV / CCCD</option>
                      </select>
                    </label>
                    <label className="text-xs font-bold text-[#63758e]">
                      Mức độ
                      <select
                        className={cn(ds.field, 'mt-1')}
                        value={violationSeverity}
                        onChange={(event) => setViolationSeverity(event.target.value)}
                      >
                        <option value="MEDIUM">MEDIUM</option>
                        <option value="HIGH">HIGH</option>
                        <option value="CRITICAL">CRITICAL</option>
                      </select>
                    </label>
                    <label className={fieldLabelClass(Boolean(violationDetailsError))}>
                      Nội dung biên bản vi phạm
                      <input
                        className={cn(
                          ds.field,
                          'mt-1',
                          violationDetailsError && fieldInvalidClass,
                        )}
                        value={violationDetails}
                        onChange={(event) => {
                          setViolationDetails(event.target.value)
                          if (violationDetailsError) setViolationDetailsError('')
                        }}
                        placeholder="Mô tả hành vi vi phạm tại máy trạm..."
                        aria-invalid={Boolean(violationDetailsError)}
                      />
                      <FieldError message={violationDetailsError} />
                    </label>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => void reportViolation()}
                      className={cn(ds.primaryButton, 'bg-[#b8404b] hover:bg-[#953943] sm:mt-5')}
                    >
                      <ShieldAlert className="size-4" /> Báo cáo vi phạm
                    </button>
                  </div>
                </div>
              )}

              {canSuspendTicket && (
                <div className="grid gap-3 border-t border-[#e8edf3] pt-4 sm:grid-cols-[1fr_auto_auto] sm:items-start">
                  <label className={fieldLabelClass(Boolean(suspendReasonError))}>
                    Lý do đình chỉ vé thi (Thẩm quyền Cán bộ Khảo thí)
                    <input
                      className={cn(ds.field, 'mt-1', suspendReasonError && fieldInvalidClass)}
                      value={suspendReason}
                      onChange={(event) => {
                        setSuspendReason(event.target.value)
                        if (suspendReasonError) setSuspendReasonError('')
                      }}
                      placeholder="Ví dụ: blur quá 3 lần hoặc gian lận nghiêm trọng"
                      aria-invalid={Boolean(suspendReasonError)}
                    />
                    <FieldError message={suspendReasonError} />
                  </label>
                  <label className={fieldLabelClass(Boolean(blurCountError))}>
                    Số lần blur
                    <input
                      type="number"
                      min="0"
                      className={cn(ds.field, 'mt-1 w-32', blurCountError && fieldInvalidClass)}
                      value={blurViolationCount}
                      onChange={(event) => {
                        setBlurViolationCount(Number(event.target.value))
                        if (blurCountError) setBlurCountError('')
                      }}
                      aria-invalid={Boolean(blurCountError)}
                    />
                    <FieldError message={blurCountError} className="w-32" />
                  </label>
                  <button
                    type="button"
                    disabled={busy || selectedSeat.isSuspended}
                    onClick={() => void suspendSeat()}
                    className={cn(ds.primaryButton, 'bg-[#b8404b] hover:bg-[#953943] sm:mt-5')}
                  >
                    <AlertTriangle className="size-4" /> Đình chỉ vé
                  </button>
                </div>
              )}
            </section>
          )}
        </>
      ) : (
        <EmptyState
          icon={Armchair}
          title="Chưa mở ca điểm danh"
          description="Chọn ca thi phòng Lab được phân công ở thanh phía trên để xem sơ đồ 24 ghế và điểm danh."
        />
      )}
    </div>
  )
}
