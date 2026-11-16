/* eslint-disable react-hooks/set-state-in-effect */
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import {
  CalendarClock,
  ClipboardList,
  FileSpreadsheet,
  LoaderCircle,
  MapPin,
  RefreshCw,
  TicketCheck,
  Users,
} from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type { ExamShift, ExamTicket, OfficialExamSession } from '@/types/officialExam.types'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { PageBanner, SectionHeader } from '@/design/ui'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'
import { DEFAULT_SHIFT_CAPACITY, getShiftCapacityMetrics } from '@/lib/shiftCapacity'
import { BulkTicketImportPanel } from './BulkTicketImportPanel'

interface TicketFormState {
  shiftId: string
  studentCode: string
  seatNumber: number
  ipAddress: string
}

const initialTicket: TicketFormState = {
  shiftId: '',
  studentCode: '',
  seatNumber: 1,
  ipAddress: '',
}

function formatShiftDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value

  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
}

function formatShiftTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  return new Intl.DateTimeFormat('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date)
}

function formatShiftLabel(shift: ExamShift, session?: OfficialExamSession) {
  const sessionTitle = session?.title?.trim()
  const courseLabel = sessionTitle?.split('·')[0]?.trim() || sessionTitle
  const startTime = formatShiftTime(shift.startTime)
  const endTime = formatShiftTime(shift.endTime)
  const timeRange = startTime && endTime ? `${startTime}–${endTime}` : undefined

  return [
    formatShiftDate(shift.startTime),
    courseLabel,
    timeRange,
    shift.roomLab ? `Phòng ${shift.roomLab}` : undefined,
    shift.shiftName || shift.id,
  ]
    .filter((value): value is string => Boolean(value))
    .join(' · ')
}

function formatShiftStatus(status: string) {
  return status.replaceAll('_', ' ')
}

export function ExaminationOfficerTicketAssignmentPage() {
  type TicketMode = 'single' | 'bulk'

  const [shifts, setShifts] = useState<ExamShift[]>([])
  const [sessions, setSessions] = useState<OfficialExamSession[]>([])
  const [issuedTickets, setIssuedTickets] = useState<ExamTicket[]>([])
  const [ticketForm, setTicketForm] = useState(initialTicket)
  const [isLoading, setIsLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [bulkBusy, setBulkBusy] = useState(false)
  const [ticketMode, setTicketMode] = useState<TicketMode>('single')
  const [isTicketDialogOpen, setIsTicketDialogOpen] = useState(false)
  const [error, setError] = useState('')
  const ticketDialogButtonRef = useRef<HTMLButtonElement>(null)

  const loadShifts = async () => {
    setIsLoading(true)
    setError('')
    try {
      const [loadedShifts, loadedSessions] = await Promise.all([
        officialExamService.getShifts(),
        officialExamService.getSessions().catch(() => []),
      ])
      setShifts(loadedShifts)
      setSessions(loadedSessions)
    } catch (loadError) {
      const message = userMessage(loadError, 'Không tải được danh sách ca thi.')
      setError(message)
      toast.error(message)
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadShifts()
  }, [])

  const sessionsById = useMemo(
    () => new Map(sessions.map((session) => [session.id, session])),
    [sessions],
  )
  const selectedShift = shifts.find((shift) => shift.id === ticketForm.shiftId)
  const selectedShiftTickets = issuedTickets.filter(
    (ticket) => ticket.shiftId === ticketForm.shiftId,
  )
  const shiftSummary = useMemo(
    () => ({
      total: shifts.length,
      upcoming: shifts.filter((shift) => new Date(shift.startTime).getTime() > Date.now()).length,
      issued: issuedTickets.length,
      selected: selectedShift?.shiftName || 'Chưa chọn',
    }),
    [issuedTickets.length, selectedShift?.shiftName, shifts],
  )

  const openTicketDialog = (shiftId?: string, mode: TicketMode = 'single') => {
    const nextShiftId = shiftId || ticketForm.shiftId || shifts[0]?.id || ''
    setTicketForm((current) => ({
      ...current,
      shiftId: nextShiftId,
      studentCode: '',
      ipAddress: '',
      seatNumber: shiftId && shiftId !== current.shiftId ? 1 : current.seatNumber,
    }))
    setTicketMode(mode)
    setError('')
    setIsTicketDialogOpen(true)
  }

  const closeTicketDialog = () => {
    if (busy || bulkBusy) return
    setIsTicketDialogOpen(false)
    setTicketMode('single')
    setError('')
    window.requestAnimationFrame(() => ticketDialogButtonRef.current?.focus())
  }

  const submitTicket = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const ticket = await officialExamService.assignTicket({
        ...ticketForm,
        ipAddress: ticketForm.ipAddress || null,
      })
      setIssuedTickets((currentTickets) => [ticket, ...currentTickets])
      setTicketForm({
        ...initialTicket,
        shiftId: ticketForm.shiftId,
        seatNumber: ticketForm.seatNumber + 1,
      })
      toast.success('Đã cấp vé thi cho sinh viên.')
      void loadShifts()
    } catch (submitError) {
      const message = userMessage(submitError, 'Không cấp được vé thi.')
      setError(message)
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={ds.page}>
      <PageBanner
        icon={TicketCheck}
        title="Cấp vé thi"
        description="Chọn ca thi để cấp vé lẻ hoặc mở luồng import roster phát vé hàng loạt."
      />

      {error && !isTicketDialogOpen ? (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
        >
          <span>{error}</span>
          <button type="button" onClick={() => void loadShifts()} className={ds.outlineButton}>
            <RefreshCw className="size-4" /> Thử lại
          </button>
        </div>
      ) : null}

      <section className={ds.section} aria-label="Tổng quan cấp vé thi">
        <SectionHeader
          icon={ClipboardList}
          title="Tổng quan cấp vé"
          hint="Theo dõi ca thi, trạng thái tổ chức và các vé vừa cấp trong phiên làm việc này."
          action={
            <div className="flex flex-wrap items-center justify-end gap-2">
              <button
                ref={ticketDialogButtonRef}
                type="button"
                onClick={() => openTicketDialog()}
                disabled={isLoading || shifts.length === 0}
                className={ds.primaryButton}
              >
                <TicketCheck className="size-4" /> Cấp vé thi
              </button>
              <button
                type="button"
                onClick={() => openTicketDialog(undefined, 'bulk')}
                disabled={isLoading || shifts.length === 0}
                className={ds.outlineButton}
              >
                <FileSpreadsheet className="size-4" /> Import hàng loạt
              </button>
              <button
                type="button"
                onClick={() => void loadShifts()}
                disabled={isLoading}
                className={ds.outlineButton}
              >
                <RefreshCw className={cn('size-4', isLoading && 'animate-spin')} /> Làm mới
              </button>
            </div>
          }
        />
        <div className="grid gap-3 p-5 sm:grid-cols-2 xl:grid-cols-4">
          {[
            {
              label: 'Ca thi khả dụng',
              value: shiftSummary.total,
              icon: ClipboardList,
              tone: 'blue',
            },
            {
              label: 'Ca sắp diễn ra',
              value: shiftSummary.upcoming,
              icon: CalendarClock,
              tone: 'amber',
            },
            { label: 'Vé vừa cấp', value: shiftSummary.issued, icon: TicketCheck, tone: 'green' },
            {
              label: 'Ca đang thao tác',
              value: shiftSummary.selected,
              icon: Users,
              tone: 'violet',
            },
          ].map(({ label, value, icon: Icon, tone }) => (
            <div
              key={label}
              className={cn(
                'rounded-2xl border px-4 py-3 transition-transform duration-200 hover:-translate-y-0.5 motion-reduce:transition-none motion-reduce:hover:transform-none',
                tone === 'blue' && 'border-[#88b2de]/55 bg-[#e9f5fc]',
                tone === 'amber' && 'border-[#edca8c]/55 bg-[#fff4e2]',
                tone === 'green' && 'border-[#9ddec4]/55 bg-[#effbf5]',
                tone === 'violet' && 'border-[#c4afe8]/55 bg-[#f2eafc]',
              )}
            >
              <div className="flex items-center gap-3">
                <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-white/70 text-[#245997]">
                  <Icon className="size-5" aria-hidden="true" />
                </span>
                <div className="min-w-0">
                  <p className="text-[11px] font-bold tracking-[0.08em] text-[#5b6b84] uppercase">
                    {label}
                  </p>
                  <p className="mt-0.5 truncate text-xl font-black text-[#0F2854]">{value}</p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={ClipboardList}
          title="Danh sách ca thi"
          badge={isLoading ? '…' : `${shifts.length} ca`}
          hint="Chọn đúng ca thi để mở popup cấp vé hoặc chuyển sang import roster hàng loạt."
        />
        {isLoading ? (
          <div className="flex items-center justify-center gap-2 p-10 text-sm font-semibold text-[#5b6b84]">
            <LoaderCircle className="size-5 animate-spin" /> Đang tải danh sách ca thi…
          </div>
        ) : shifts.length === 0 ? (
          <div className="p-5">
            <EmptyState
              compact
              icon={ClipboardList}
              title="Chưa có ca thi"
              description="Các ca thi được tạo tại trang Kỳ thi chính thức sẽ xuất hiện tại đây."
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[980px] text-left text-sm">
              <thead className="bg-[#F4FBFD] text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase">
                <tr>
                  <th className="px-5 py-3">Ca thi</th>
                  <th className="px-5 py-3">Kỳ thi / môn</th>
                  <th className="px-5 py-3">Phòng</th>
                  <th className="px-5 py-3">Sức chứa / Vé</th>
                  <th className="px-5 py-3">Thời gian</th>
                  <th className="px-5 py-3">Nhân sự</th>
                  <th className="px-5 py-3">Trạng thái</th>
                  <th className="px-5 py-3 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#d9e8f2]">
                {shifts.map((shift) => {
                  const session = sessionsById.get(shift.sessionId)
                  const capacity = getShiftCapacityMetrics(shift)
                  return (
                    <tr key={shift.id} className="transition-colors hover:bg-[#F8FBFD]">
                      <td className="px-5 py-4 font-bold text-[#0F2854]">
                        {shift.shiftName || shift.id}
                      </td>
                      <td className="max-w-[250px] px-5 py-4 text-[#5b6b84]">
                        <span className="block truncate" title={session?.title}>
                          {session?.title || 'Chưa cập nhật'}
                        </span>
                      </td>
                      <td className="px-5 py-4 text-[#5b6b84]">
                        <span className="inline-flex items-center gap-1.5">
                          <MapPin className="size-3.5" /> {shift.roomLab || 'Chưa cập nhật'}
                        </span>
                      </td>
                      <td className="px-5 py-4 whitespace-nowrap text-xs text-[#5b6b84]">
                        <span className="block font-semibold text-[#0F2854]">
                          Sức chứa tối đa: {capacity.maxSeats ?? '—'}
                        </span>
                        <span className="mt-0.5 block">
                          Đã phát: {capacity.assignedTicketsCount} · Còn lại:{' '}
                          {capacity.remainingSeats ?? '—'}
                        </span>
                      </td>
                      <td className="px-5 py-4 whitespace-nowrap text-[#5b6b84]">
                        <span className="inline-flex items-center gap-1.5">
                          <CalendarClock className="size-3.5" />
                          {formatShiftDate(shift.startTime)} · {formatShiftTime(shift.startTime)}–
                          {formatShiftTime(shift.endTime)}
                        </span>
                      </td>
                      <td className="px-5 py-4 text-xs text-[#5b6b84]">
                        <span className="block">Giám thị: {shift.proctorName || 'Chưa gán'}</span>
                        <span className="mt-1 block">
                          Giám khảo: {shift.examinerName || 'Chưa gán'}
                        </span>
                      </td>
                      <td className="px-5 py-4">
                        <span
                          className={
                            shift.status.toUpperCase() === 'PUBLISHED'
                              ? ds.successChip
                              : ds.infoChip
                          }
                        >
                          {formatShiftStatus(shift.status)}
                        </span>
                      </td>
                      <td className="px-5 py-4 text-right">
                        <button
                          type="button"
                          onClick={() => openTicketDialog(shift.id, 'single')}
                          className={ds.outlineButton}
                        >
                          <TicketCheck className="size-4" /> Cấp vé
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className={ds.section}>
        <SectionHeader
          icon={TicketCheck}
          title="Vé vừa cấp trong phiên"
          badge={`${issuedTickets.length} vé`}
          hint="Các vé cấp thành công trong phiên làm việc hiện tại sẽ được theo dõi tại đây."
        />
        <div className="p-5">
          {issuedTickets.length === 0 ? (
            <EmptyState
              compact
              icon={TicketCheck}
              title="Chưa có vé nào trong phiên này"
              description="Mở popup Cấp vé thi hoặc import roster để bắt đầu phát vé cho sinh viên."
            />
          ) : (
            <div className="grid gap-2 md:grid-cols-2">
              {issuedTickets.map((ticket) => {
                const shift = shifts.find((item) => item.id === ticket.shiftId)
                return (
                  <article
                    key={ticket.id}
                    className="rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-3"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <strong className="block text-sm text-[#0F2854]">
                          MSSV: {ticket.studentCode || ticket.studentId}
                        </strong>
                        <span className="mt-1 block text-xs text-[#5b6b84]">
                          {shift
                            ? formatShiftLabel(shift, sessionsById.get(shift.sessionId))
                            : ticket.shiftId}{' '}
                          · Ghế {ticket.seatNumber} · IP {ticket.ipAddress || 'Chưa gán'}
                        </span>
                      </div>
                      <span className={ds.infoChip}>{ticket.status}</span>
                    </div>
                  </article>
                )
              })}
            </div>
          )}
        </div>
      </section>

      <FormDialog
        open={isTicketDialogOpen}
        align="center"
        size="lg"
        maxHeight="max-h-[min(92dvh,720px)]"
        eyebrow="CẤP VÉ MF-04"
        title={
          selectedShift
            ? `${ticketMode === 'bulk' ? 'Import hàng loạt' : 'Cấp vé'} · ${selectedShift.shiftName || selectedShift.id}`
            : ticketMode === 'bulk'
              ? 'Import hàng loạt'
              : 'Cấp vé thi'
        }
        icon={TicketCheck}
        busy={busy || bulkBusy}
        onClose={closeTicketDialog}
        headerExtra={
          <div
            className="hidden rounded-xl border border-[#bcd6ef] bg-[#f5fbff] p-1 sm:flex"
            role="tablist"
            aria-label="Phương thức cấp vé"
          >
            <button
              type="button"
              role="tab"
              aria-selected={ticketMode === 'single'}
              onClick={() => setTicketMode('single')}
              className={cn(
                'rounded-lg px-3 py-2 text-xs font-bold transition-colors',
                ticketMode === 'single'
                  ? 'bg-white text-[#0F2854] shadow-sm'
                  : 'text-[#5b6b84] hover:text-[#245997]',
              )}
            >
              Cấp vé cá nhân
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={ticketMode === 'bulk'}
              onClick={() => setTicketMode('bulk')}
              className={cn(
                'rounded-lg px-3 py-2 text-xs font-bold transition-colors',
                ticketMode === 'bulk'
                  ? 'bg-white text-[#0F2854] shadow-sm'
                  : 'text-[#5b6b84] hover:text-[#245997]',
              )}
            >
              Import hàng loạt
            </button>
          </div>
        }
        footer={
          <DialogFooter className="justify-end bg-white">
            <button
              type="button"
              className={ds.outlineButton}
              onClick={closeTicketDialog}
              disabled={busy || bulkBusy}
            >
              Hủy
            </button>
            {ticketMode === 'single' ? (
              <button
                type="submit"
                form="ticket-assignment-form"
                disabled={busy || bulkBusy || !selectedShift}
                className={ds.primaryButton}
              >
                {busy ? (
                  <LoaderCircle className="size-4 animate-spin" />
                ) : (
                  <TicketCheck className="size-4" />
                )}
                {busy ? 'Đang cấp vé…' : 'Xác nhận cấp vé'}
              </button>
            ) : null}
          </DialogFooter>
        }
      >
        <div className="grid gap-4">
          <div
            className="flex rounded-xl border border-[#bcd6ef] bg-[#f5fbff] p-1 sm:hidden"
            role="tablist"
            aria-label="Phương thức cấp vé"
          >
            <button
              type="button"
              role="tab"
              aria-selected={ticketMode === 'single'}
              onClick={() => setTicketMode('single')}
              className={cn(
                'flex-1 rounded-lg px-3 py-2 text-xs font-bold',
                ticketMode === 'single' ? 'bg-white text-[#0F2854] shadow-sm' : 'text-[#5b6b84]',
              )}
            >
              Cấp vé cá nhân
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={ticketMode === 'bulk'}
              onClick={() => setTicketMode('bulk')}
              className={cn(
                'flex-1 rounded-lg px-3 py-2 text-xs font-bold',
                ticketMode === 'bulk' ? 'bg-white text-[#0F2854] shadow-sm' : 'text-[#5b6b84]',
              )}
            >
              Import hàng loạt
            </button>
          </div>
          {ticketMode === 'bulk' ? (
            <>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="bulk-ticket-shift"
              >
                Ca thi *
                <select
                  id="bulk-ticket-shift"
                  required
                  className={ds.field}
                  value={ticketForm.shiftId}
                  onChange={(event) =>
                    setTicketForm((current) => ({
                      ...current,
                      shiftId: event.target.value,
                      seatNumber: 1,
                    }))
                  }
                >
                  <option value="">Chọn ca thi</option>
                  {shifts.map((shift) => (
                    <option key={shift.id} value={shift.id}>
                      {formatShiftLabel(shift, sessionsById.get(shift.sessionId))}
                    </option>
                  ))}
                </select>
              </label>
              <BulkTicketImportPanel
                key={ticketForm.shiftId}
                embedded
                shiftId={ticketForm.shiftId}
                shift={selectedShift ?? null}
                onBusyChange={setBulkBusy}
                onRefreshShift={loadShifts}
                onCompleted={(result) =>
                  setIssuedTickets((currentTickets) => [...result.tickets, ...currentTickets])
                }
              />
            </>
          ) : (
            <form id="ticket-assignment-form" onSubmit={submitTicket} className="grid gap-4">
              {error ? (
                <div
                  role="alert"
                  className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
                >
                  {error}
                </div>
              ) : null}
              <div className="rounded-2xl border border-[#bcd6ef] bg-white px-4 py-3 text-sm text-[#245997]">
                <p className="text-[11px] font-bold tracking-[0.08em] text-[#5b6b84] uppercase">
                  Ca đang cấp vé
                </p>
                <p className="mt-1 font-bold text-[#0F2854]">
                  {selectedShift
                    ? formatShiftLabel(selectedShift, sessionsById.get(selectedShift.sessionId))
                    : 'Chọn ca thi'}
                </p>
                <p className="mt-1 text-xs text-[#71829a]">
                  {selectedShiftTickets.length} vé vừa cấp trong phiên này · Ghế kế tiếp:{' '}
                  {ticketForm.seatNumber}
                </p>
              </div>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="ticket-shift"
              >
                Ca thi *
                <select
                  id="ticket-shift"
                  required
                  className={ds.field}
                  value={ticketForm.shiftId}
                  onChange={(event) =>
                    setTicketForm((current) => ({
                      ...current,
                      shiftId: event.target.value,
                      seatNumber: current.shiftId === event.target.value ? current.seatNumber : 1,
                    }))
                  }
                >
                  <option value="">Chọn ca thi</option>
                  {shifts.map((shift) => (
                    <option key={shift.id} value={shift.id}>
                      {formatShiftLabel(shift, sessionsById.get(shift.sessionId))}
                    </option>
                  ))}
                </select>
              </label>
              <label
                className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                htmlFor="ticket-student-code"
              >
                Mã số sinh viên *
                <input
                  id="ticket-student-code"
                  required
                  className={ds.field}
                  value={ticketForm.studentCode}
                  onChange={(event) =>
                    setTicketForm((current) => ({ ...current, studentCode: event.target.value }))
                  }
                />
              </label>
              <div className="grid gap-3 sm:grid-cols-2">
                <label
                  className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                  htmlFor="ticket-seat-number"
                >
                  Số ghế *
                  <input
                    id="ticket-seat-number"
                    required
                    type="number"
                    min="1"
                    max={selectedShift?.maxSeats ?? DEFAULT_SHIFT_CAPACITY}
                    className={ds.field}
                    value={ticketForm.seatNumber}
                    onChange={(event) =>
                      setTicketForm((current) => ({
                        ...current,
                        seatNumber: Number(event.target.value),
                      }))
                    }
                  />
                </label>
                <label
                  className="grid gap-1.5 text-xs font-bold text-[#52657d]"
                  htmlFor="ticket-ip-address"
                >
                  IP máy
                  <input
                    id="ticket-ip-address"
                    className={ds.field}
                    value={ticketForm.ipAddress}
                    onChange={(event) =>
                      setTicketForm((current) => ({ ...current, ipAddress: event.target.value }))
                    }
                  />
                </label>
              </div>
            </form>
          )}
        </div>
      </FormDialog>
    </div>
  )
}
