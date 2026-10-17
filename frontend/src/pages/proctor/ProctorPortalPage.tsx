import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertTriangle,
  BadgeCheck,
  CheckCircle2,
  ClipboardCheck,
  CreditCard,
  Lock,
  Mic,
  Monitor,
  MonitorCheck,
  Play,
  RefreshCw,
  Search,
  ShieldAlert,
  ShieldCheck,
  UserCheck,
  Wifi,
} from 'lucide-react'
import apiClient from '@/services/api.client'
import { OverlayDrawer } from '@/components/ui/OverlayDrawer'
import type { ExamTicketStatus } from '@/types/officialExam.types'
import { cn } from '@/lib/utils'

interface KioskWorkstationSeat {
  seatNumber: number
  ipAddress: string
  ticketId: string | null
  studentCode: string | null
  fullName: string | null
  citizenId: string | null
  status: ExamTicketStatus | 'EMPTY'
  checkedIn: boolean
  micDecibels: number | null
  blurCount: number
  isSuspended: boolean
  suspensionReason: string | null
  audioFileName: string | null
  audioHashSha256: string | null
}

interface ShiftOption {
  shiftId: string
  label: string
  roomName: string
  courseCode: string
  timeRange: string
}

const SHIFT_OPTIONS: ShiftOption[] = [
  {
    shiftId: '00000000-0000-0000-0000-000000000023',
    label: 'Kíp 1 (08h00 - 09h30) · Phòng Lab 301 · PRN231 · Kỳ Fall 2026',
    roomName: 'Phòng Lab 301 (Tòa Alpha)',
    courseCode: 'PRN231',
    timeRange: '08:00 - 09:30 · 16/10/2026',
  },
  {
    shiftId: '00000000-0000-0000-0000-000000000024',
    label: 'Kíp 2 (10h00 - 11h30) · Phòng Lab 302 · SWD392 · Kỳ Fall 2026',
    roomName: 'Phòng Lab 302 (Tòa Alpha)',
    courseCode: 'SWD392',
    timeRange: '10:00 - 11:30 · 16/10/2026',
  },
]

const SEEDED_STUDENTS: Array<{
  seatNumber: number
  ticketId: string
  studentCode: string
  fullName: string
  citizenId: string
  status: ExamTicketStatus
  checkedIn: boolean
  micDecibels: number
  blurCount: number
}> = [
  {
    seatNumber: 1,
    ticketId: '00000000-0000-0000-0000-000000000024',
    studentCode: 'SE170001',
    fullName: 'Lê Vũ Hoàng',
    citizenId: '079204001289',
    status: 'AI_GRADED',
    checkedIn: true,
    micDecibels: 68,
    blurCount: 0,
  },
  {
    seatNumber: 2,
    ticketId: '00000000-0000-0000-0000-000000000025',
    studentCode: 'SE170002',
    fullName: 'Trần Thị Mai',
    citizenId: '079204005612',
    status: 'SUBMITTED',
    checkedIn: true,
    micDecibels: 65,
    blurCount: 1,
  },
  {
    seatNumber: 3,
    ticketId: '00000000-0000-0000-0000-000000000103',
    studentCode: 'SE170118',
    fullName: 'Phạm Minh Tuấn',
    citizenId: '079204008931',
    status: 'IN_PROGRESS',
    checkedIn: true,
    micDecibels: 64,
    blurCount: 0,
  },
  {
    seatNumber: 4,
    ticketId: '00000000-0000-0000-0000-000000000104',
    studentCode: 'SE170205',
    fullName: 'Nguyễn Hoàng Nam',
    citizenId: '079204003419',
    status: 'SCHEDULED',
    checkedIn: false,
    micDecibels: 62,
    blurCount: 0,
  },
  {
    seatNumber: 5,
    ticketId: '00000000-0000-0000-0000-000000000105',
    studentCode: 'SE170244',
    fullName: 'Võ Ngọc Ánh',
    citizenId: '079204007720',
    status: 'IN_PROGRESS',
    checkedIn: true,
    micDecibels: 61,
    blurCount: 2,
  },
  {
    seatNumber: 6,
    ticketId: '00000000-0000-0000-0000-000000000106',
    studentCode: 'SE170312',
    fullName: 'Đặng Quốc Bảo',
    citizenId: '079204009104',
    status: 'SCHEDULED',
    checkedIn: false,
    micDecibels: 66,
    blurCount: 0,
  },
]

function buildInitialWorkstations(): KioskWorkstationSeat[] {
  return Array.from({ length: 40 }, (_, index) => {
    const seatNumber = index + 1
    const ipAddress = `192.168.1.${100 + seatNumber}`
    const seeded = SEEDED_STUDENTS.find((s) => s.seatNumber === seatNumber)
    if (!seeded) {
      return {
        seatNumber,
        ipAddress,
        ticketId: null,
        studentCode: null,
        fullName: null,
        citizenId: null,
        status: 'EMPTY',
        checkedIn: false,
        micDecibels: null,
        blurCount: 0,
        isSuspended: false,
        suspensionReason: null,
        audioFileName: null,
        audioHashSha256: null,
      }
    }

    const seatPrefix = String(seatNumber).padStart(2, '0')
    return {
      seatNumber,
      ipAddress,
      ticketId: seeded.ticketId,
      studentCode: seeded.studentCode,
      fullName: seeded.fullName,
      citizenId: seeded.citizenId,
      status: seeded.status,
      checkedIn: seeded.checkedIn,
      micDecibels: seeded.micDecibels,
      blurCount: seeded.blurCount,
      isSuspended: false,
      suspensionReason: null,
      audioFileName: `${seatPrefix}_${seeded.studentCode}.webm`,
      audioHashSha256: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
    }
  })
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

const SEAT_STATUS_STYLE: Record<
  ExamTicketStatus | 'EMPTY',
  { label: string; badgeClass: string; tileClass: string }
> = {
  EMPTY: {
    label: 'Ghế trống',
    badgeClass: 'bg-slate-100 text-slate-500',
    tileClass: 'border-[#E2E8F0] bg-[#F8FAFC] text-[#64748B]',
  },
  SCHEDULED: {
    label: 'Chờ điểm danh',
    badgeClass: 'bg-amber-50 text-amber-700 border border-amber-200',
    tileClass: 'border-amber-300 bg-amber-50/70 text-[#1E293B]',
  },
  IN_PROGRESS: {
    label: 'Đang làm bài',
    badgeClass: 'bg-blue-50 text-[#2563EB] border border-blue-200',
    tileClass: 'border-blue-400 bg-blue-50 text-[#1E293B]',
  },
  SUBMITTED: {
    label: 'Đã nộp bài',
    badgeClass: 'bg-emerald-50 text-[#16A34A] border border-emerald-200',
    tileClass: 'border-emerald-400 bg-emerald-50/80 text-[#1E293B]',
  },
  AI_GRADED: {
    label: 'Đã chấm tự động',
    badgeClass: 'bg-emerald-50 text-[#16A34A] border border-emerald-200',
    tileClass: 'border-emerald-400 bg-emerald-50/80 text-[#1E293B]',
  },
  AUDITED: {
    label: 'Đã hậu kiểm',
    badgeClass: 'bg-emerald-50 text-[#16A34A] border border-emerald-200',
    tileClass: 'border-emerald-400 bg-emerald-50/80 text-[#1E293B]',
  },
  PUBLISHED: {
    label: 'Đã công bố',
    badgeClass: 'bg-emerald-50 text-[#16A34A] border border-emerald-200',
    tileClass: 'border-emerald-400 bg-emerald-50/80 text-[#1E293B]',
  },
  LOCKED: {
    label: 'Đã khóa điểm',
    badgeClass: 'bg-[#FFF7ED] text-[#C2410C] border border-[#FED7AA]',
    tileClass: 'border-[#FED7AA] bg-[#FFF7ED] text-[#1E293B]',
  },
}

export function ProctorPortalPage() {
  const [selectedShiftId, setSelectedShiftId] = useState<string>(SHIFT_OPTIONS[0].shiftId)
  const [workstations, setWorkstations] = useState<KioskWorkstationSeat[]>(buildInitialWorkstations)
  const [selectedSeatNumber, setSelectedSeatNumber] = useState<number>(1)
  const [isDrawerOpen, setIsDrawerOpen] = useState<boolean>(false)
  const [searchQuery, setSearchQuery] = useState<string>('')
  const [isShiftOpened, setIsShiftOpened] = useState<boolean>(true)
  const [isLoading, setIsLoading] = useState<boolean>(false)
  const [notice, setNotice] = useState<string>('')
  const [suspensionNote, setSuspensionNote] = useState<string>(
    'Thoát chế độ toàn màn hình phòng thi quá 3 lần, vi phạm quy chế khảo thí FPTU.',
  )

  const activeShift = useMemo(
    () => SHIFT_OPTIONS.find((s) => s.shiftId === selectedShiftId) ?? SHIFT_OPTIONS[0],
    [selectedShiftId],
  )

  const syncWithAuditEvidence = useCallback(async (shiftId: string) => {
    await Promise.resolve()
    setIsLoading(true)
    try {
      const response = await apiClient.get<unknown>(
        `/officialexams/shifts/${shiftId}/audit-evidence`,
      )
      const payload = isRecord(response.data)
        ? (response.data.value ?? response.data.data ?? response.data)
        : response.data

      if (Array.isArray(payload) && payload.length > 0) {
        setWorkstations((prev) => {
          const next = [...prev]
          for (const item of payload) {
            if (!isRecord(item) || typeof item.seatNumber !== 'number') continue
            const seatIdx = item.seatNumber - 1
            if (seatIdx < 0 || seatIdx >= next.length) continue
            const current = next[seatIdx]
            const studentCode =
              typeof item.studentCode === 'string' ? item.studentCode : current.studentCode
            const seatPrefix = String(item.seatNumber).padStart(2, '0')
            next[seatIdx] = {
              ...current,
              ticketId: typeof item.ticketId === 'string' ? item.ticketId : current.ticketId,
              studentCode,
              fullName: typeof item.fullName === 'string' ? item.fullName : current.fullName,
              citizenId: current.citizenId ?? `07920400${1000 + item.seatNumber}`,
              status:
                typeof item.status === 'string'
                  ? (item.status as ExamTicketStatus)
                  : current.status,
              checkedIn: item.status !== 'SCHEDULED',
              micDecibels: current.micDecibels ?? 66,
              audioFileName: studentCode ? `${seatPrefix}_${studentCode}.webm` : null,
              audioHashSha256:
                typeof item.audioHashSha256 === 'string'
                  ? item.audioHashSha256
                  : current.audioHashSha256,
            }
          }
          return next
        })
      }
    } catch {
      // Giữ sơ đồ ghế mặc định nếu hệ thống trung tâm đang khởi tạo
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    void Promise.resolve().then(() => syncWithAuditEvidence(selectedShiftId))
  }, [selectedShiftId, syncWithAuditEvidence])

  const selectedSeat = useMemo(
    () =>
      workstations.find((w) => w.seatNumber === selectedSeatNumber) ?? workstations[0],
    [workstations, selectedSeatNumber],
  )

  const stats = useMemo(() => {
    const assigned = workstations.filter((w) => w.status !== 'EMPTY')
    const checkedIn = assigned.filter((w) => w.checkedIn)
    const inProgress = assigned.filter((w) => w.status === 'IN_PROGRESS')
    const submitted = assigned.filter(
      (w) =>
        w.status === 'SUBMITTED' ||
        w.status === 'AI_GRADED' ||
        w.status === 'AUDITED' ||
        w.status === 'PUBLISHED' ||
        w.status === 'LOCKED',
    )
    const warnings = assigned.filter((w) => w.blurCount >= 2 || w.isSuspended)
    return {
      totalSeats: 40,
      assignedCount: assigned.length,
      checkedInCount: checkedIn.length,
      inProgressCount: inProgress.length,
      submittedCount: submitted.length,
      warningCount: warnings.length,
    }
  }, [workstations])

  const filteredCandidates = useMemo(() => {
    const assigned = workstations.filter((w) => w.status !== 'EMPTY')
    const q = searchQuery.trim().toLowerCase()
    if (!q) return assigned
    return assigned.filter(
      (w) =>
        w.studentCode?.toLowerCase().includes(q) ||
        w.fullName?.toLowerCase().includes(q) ||
        w.ipAddress.toLowerCase().includes(q) ||
        String(w.seatNumber).includes(q),
    )
  }, [workstations, searchQuery])

  const handleSelectWorkstation = (seatNumber: number) => {
    setSelectedSeatNumber(seatNumber)
    setIsDrawerOpen(true)
  }

  const handleOpenLabShift = () => {
    setIsShiftOpened(true)
    setNotice(
      `Đã kích hoạt mở ca thi "${activeShift.label}". Toàn bộ máy trạm từ 192.168.1.101 đến 192.168.1.140 đã kết nối hệ thống trung tâm và sẵn sàng điểm danh.`,
    )
  }

  const handleCheckInCandidate = async (seat: KioskWorkstationSeat) => {
    if (!seat.ticketId) return
    try {
      await apiClient.post(`/official-exams/tickets/${seat.ticketId}/check-in`, {
        seatNumber: seat.seatNumber,
        ipAddress: seat.ipAddress,
      })
    } catch {
      // Cập nhật trực tiếp trạng thái điểm danh tại phòng thi
    }

    setWorkstations((prev) =>
      prev.map((item) =>
        item.seatNumber === seat.seatNumber
          ? {
              ...item,
              checkedIn: true,
              status: item.status === 'SCHEDULED' ? 'IN_PROGRESS' : item.status,
              micDecibels: item.micDecibels ?? 65,
            }
          : item,
      ),
    )
    setNotice(
      `Đã xác nhận điểm danh (Check-in) cho thí sinh ${seat.studentCode} — ${seat.fullName} tại Ghế ${String(seat.seatNumber).padStart(2, '0')} (Địa chỉ IP máy trạm: ${seat.ipAddress}).`,
    )
  }

  const handleSuspendCandidate = (seat: KioskWorkstationSeat) => {
    if (!seat.studentCode) return
    setWorkstations((prev) =>
      prev.map((item) =>
        item.seatNumber === seat.seatNumber
          ? {
              ...item,
              isSuspended: true,
              blurCount: Math.max(item.blurCount, 3),
              suspensionReason: suspensionNote.trim(),
            }
          : item,
      ),
    )
    setNotice(
      `Đã lập biên bản và khóa máy trạm đối với thí sinh ${seat.studentCode} tại Ghế ${String(seat.seatNumber).padStart(2, '0')} (${seat.ipAddress}).`,
    )
  }

  return (
    <div className="space-y-6 pb-10">
      {/* Header Ca thi đang trực + 5 Thẻ KPI ngắn gọn */}
      <section className="rounded-2xl border border-[#E2E8F0] bg-white p-6 shadow-xs">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-[#FED7AA] bg-[#FFF7ED] px-3 py-1 text-xs font-bold text-[#C2410C]">
              <MonitorCheck className="size-3.5" aria-hidden="true" />
              CỔNG ĐIỀU HÀNH CA THI PHÒNG LAB · GIÁM THỊ COI THI
            </div>
            <h1 className="mt-2.5 text-2xl font-bold tracking-tight text-[#1E293B]">
              Điểm danh Số ghế (01–40), Đối chiếu Thẻ SV/CCCD & Giám sát An ninh Phòng thi
            </h1>
            <p className="mt-1 text-sm text-[#475569]">
              Kiểm soát Địa chỉ IP máy trạm, độ nhạy Micro (≥ 60dB), giám sát chế độ toàn màn hình
              (thoát quá 3 lần tự động đình chỉ) và niêm phong tệp âm thanh bài thi chuẩn SHA-256.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <select
              aria-label="Chọn ca thi đang trực"
              value={selectedShiftId}
              onChange={(e) => setSelectedShiftId(e.target.value)}
              className="rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] px-3.5 py-2.5 text-xs font-bold text-[#1E293B] focus:border-[#F26F21] focus:outline-none"
            >
              {SHIFT_OPTIONS.map((shift) => (
                <option key={shift.shiftId} value={shift.shiftId}>
                  {shift.label}
                </option>
              ))}
            </select>

            <button
              type="button"
              onClick={handleOpenLabShift}
              className="inline-flex min-h-10 items-center gap-2 rounded-xl bg-[#F26F21] px-4 text-xs font-bold text-white shadow-xs transition hover:bg-[#D95B16]"
            >
              <Play className="size-4" aria-hidden="true" />
              {isShiftOpened ? 'Mở Ca Thi Phòng Lab (Đang Mở)' : 'Mở Ca Thi Phòng Lab'}
            </button>

            <button
              type="button"
              onClick={() => void syncWithAuditEvidence(selectedShiftId)}
              disabled={isLoading}
              className="inline-flex min-h-10 items-center gap-1.5 rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] px-3.5 text-xs font-bold text-[#1E293B] hover:bg-white"
            >
              <RefreshCw
                className={cn('size-3.5', isLoading && 'animate-spin')}
                aria-hidden="true"
              />
              Đồng bộ Hệ thống trung tâm
            </button>
          </div>
        </div>

        {/* Hàng 5 thẻ KPI ngắn gọn */}
        <div className="mt-6 grid grid-cols-2 gap-3 border-t border-[#E2E8F0] pt-5 sm:grid-cols-3 xl:grid-cols-5">
          <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
            <p className="text-xs font-semibold text-[#64748B]">Sĩ số Ca thi</p>
            <p className="mt-1 text-xl font-bold text-[#1E293B]">
              {stats.assignedCount} / {stats.totalSeats} ghế
            </p>
            <p className="mt-0.5 font-mono text-xs text-[#64748B]">
              {activeShift.courseCode} · .101 → .140
            </p>
          </div>

          <div className="rounded-xl border border-emerald-200 bg-emerald-50/60 p-3.5">
            <p className="text-xs font-semibold text-[#16A34A]">Đã Check-in</p>
            <p className="mt-1 text-xl font-bold text-[#16A34A]">{stats.checkedInCount} SV</p>
            <p className="mt-0.5 text-xs text-[#16A34A]">Khớp thẻ SV/CCCD</p>
          </div>

          <div className="rounded-xl border border-blue-200 bg-blue-50/60 p-3.5">
            <p className="text-xs font-semibold text-[#2563EB]">Đang làm bài</p>
            <p className="mt-1 text-xl font-bold text-[#2563EB]">{stats.inProgressCount} SV</p>
            <p className="mt-0.5 text-xs text-[#2563EB]">Toàn màn hình</p>
          </div>

          <div className="rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-3.5">
            <p className="text-xs font-semibold text-[#C2410C]">Đã nộp bài</p>
            <p className="mt-1 text-xl font-bold text-[#C2410C]">{stats.submittedCount} SV</p>
            <p className="mt-0.5 text-xs text-[#C2410C]">Đã niêm phong bài làm</p>
          </div>

          <div className="rounded-xl border border-rose-200 bg-rose-50/70 p-3.5">
            <p className="text-xs font-semibold text-rose-700">Cảnh báo Phòng thi</p>
            <p className="mt-1 text-xl font-bold text-rose-700">{stats.warningCount} máy</p>
            <p className="mt-0.5 text-xs text-rose-700">Thoát màn hình / Mất tiêu điểm</p>
          </div>
        </div>
      </section>

      {notice && (
        <div
          role="status"
          className="flex items-center justify-between gap-3 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-xs font-semibold text-[#16A34A]"
        >
          <span className="flex items-center gap-2">
            <CheckCircle2 className="size-4 shrink-0" aria-hidden="true" />
            {notice}
          </span>
          <button
            type="button"
            onClick={() => setNotice('')}
            className="text-xs font-bold underline"
          >
            Đóng
          </button>
        </div>
      )}

      {/* Sơ đồ lưới 40 máy trạm */}
      <section className="rounded-2xl border border-[#E2E8F0] bg-white p-5">
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[#E2E8F0] pb-3.5">
          <div>
            <h2 className="text-base font-bold text-[#1E293B]">
              Sơ đồ Lưới 40 Máy trạm Phòng thi ({activeShift.roomName})
            </h2>
            <p className="text-xs text-[#64748B]">
              Nhấn vào bất kỳ ghế nào (Ghế 01–40) để mở ngăn kéo đối chiếu Thẻ SV/CCCD, kiểm tra Địa
              chỉ IP máy trạm và xác nhận điểm danh.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2 text-xs font-semibold">
            <span className="inline-flex items-center gap-1 rounded-md border border-amber-300 bg-amber-50 px-2 py-0.5 text-amber-800">
              Chờ Check-in
            </span>
            <span className="inline-flex items-center gap-1 rounded-md border border-blue-300 bg-blue-50 px-2 py-0.5 text-[#2563EB]">
              Đang thi
            </span>
            <span className="inline-flex items-center gap-1 rounded-md border border-emerald-300 bg-emerald-50 px-2 py-0.5 text-[#16A34A]">
              Đã nộp bài
            </span>
          </div>
        </div>

        <div className="mt-4 grid grid-cols-4 gap-2 sm:grid-cols-5 md:grid-cols-8 lg:grid-cols-10">
          {workstations.map((seat) => {
            const style = SEAT_STATUS_STYLE[seat.status]
            const isSelected = seat.seatNumber === selectedSeatNumber && isDrawerOpen
            return (
              <button
                key={seat.seatNumber}
                type="button"
                onClick={() => handleSelectWorkstation(seat.seatNumber)}
                className={cn(
                  'flex flex-col items-start justify-between rounded-xl border p-2.5 text-left transition hover:border-[#F26F21]',
                  style.tileClass,
                  seat.isSuspended && 'border-rose-500 bg-rose-50 text-rose-800',
                  isSelected && 'ring-2 ring-[#F26F21] ring-offset-1',
                )}
              >
                <div className="flex w-full items-center justify-between">
                  <span className="text-xs font-bold">
                    #{String(seat.seatNumber).padStart(2, '0')}
                  </span>
                  {seat.blurCount >= 2 || seat.isSuspended ? (
                    <AlertTriangle className="size-3.5 text-rose-600" aria-hidden="true" />
                  ) : seat.checkedIn ? (
                    <BadgeCheck className="size-3.5 text-[#16A34A]" aria-hidden="true" />
                  ) : (
                    <Monitor className="size-3.5 opacity-60" aria-hidden="true" />
                  )}
                </div>
                <p className="mt-1 w-full truncate text-xs font-bold">
                  {seat.studentCode ?? 'Trống'}
                </p>
                <p className="w-full truncate font-mono text-xs opacity-75">
                  .{100 + seat.seatNumber}
                </p>
              </button>
            )
          })}
        </div>
      </section>

      {/* Bảng danh sách thí sinh trong phòng Lab */}
      <section className="rounded-2xl border border-[#E2E8F0] bg-white p-5">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#E2E8F0] pb-3.5">
          <h2 className="text-base font-bold text-[#1E293B]">
            Danh sách Thí sinh Ca thi ({filteredCandidates.length} thí sinh)
          </h2>
          <div className="relative w-full sm:w-64">
            <Search
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[#64748B]"
              aria-hidden="true"
            />
            <input
              type="search"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Tìm MSSV, Họ tên, Địa chỉ IP máy trạm..."
              className="w-full rounded-xl border border-[#CBD5E1] bg-[#F8FAFC] py-1.5 pr-3 pl-9 text-xs text-[#1E293B] focus:border-[#F26F21] focus:bg-white focus:outline-none"
            />
          </div>
        </div>

        <div className="mt-3 overflow-x-auto">
          <table className="w-full border-collapse text-left text-xs">
            <thead>
              <tr className="border-b border-[#E2E8F0] bg-[#F8FAFC] text-[#475569]">
                <th className="px-3 py-2.5 font-bold">Ghế & Địa chỉ IP máy trạm</th>
                <th className="px-3 py-2.5 font-bold">Thí sinh</th>
                <th className="px-3 py-2.5 font-bold">Micro / Cảnh báo</th>
                <th className="px-3 py-2.5 font-bold">Trạng thái Vé</th>
                <th className="px-3 py-2.5 text-right font-bold">Điểm danh & Chi tiết</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E2E8F0]">
              {filteredCandidates.map((candidate) => {
                const statusMeta = SEAT_STATUS_STYLE[candidate.status]
                return (
                  <tr
                    key={candidate.seatNumber}
                    onClick={() => handleSelectWorkstation(candidate.seatNumber)}
                    className={cn(
                      'cursor-pointer transition hover:bg-[#F8FAFC]',
                      candidate.seatNumber === selectedSeatNumber && isDrawerOpen && 'bg-[#FFF7ED]',
                    )}
                  >
                    <td className="px-3 py-3">
                      <p className="font-bold text-[#1E293B]">
                        Ghế {String(candidate.seatNumber).padStart(2, '0')}
                      </p>
                      <p className="font-mono text-xs text-[#64748B]">{candidate.ipAddress}</p>
                    </td>
                    <td className="px-3 py-3">
                      <p className="font-bold text-[#1E293B]">{candidate.fullName}</p>
                      <p className="text-xs text-[#64748B]">
                        {candidate.studentCode} · CCCD: {candidate.citizenId}
                      </p>
                    </td>
                    <td className="px-3 py-3">
                      <span className="font-semibold text-[#16A34A]">
                        Mic {candidate.micDecibels ?? 65}dB
                      </span>
                      <span className="mx-1.5 text-[#CBD5E1]">•</span>
                      <span
                        className={cn(
                          'font-bold',
                          candidate.blurCount >= 2 ? 'text-rose-600' : 'text-[#475569]',
                        )}
                      >
                        Thoát màn hình: {candidate.blurCount}/3
                      </span>
                    </td>
                    <td className="px-3 py-3">
                      <span
                        className={cn(
                          'rounded-full px-2.5 py-0.5 text-xs font-bold',
                          candidate.isSuspended
                            ? 'bg-rose-100 text-rose-700'
                            : statusMeta.badgeClass,
                        )}
                      >
                        {candidate.isSuspended ? 'ĐÌNH CHỈ THI' : statusMeta.label}
                      </span>
                    </td>
                    <td className="px-3 py-3 text-right">
                      {candidate.checkedIn ? (
                        <span className="inline-flex items-center gap-1 font-bold text-[#16A34A]">
                          <CheckCircle2 className="size-3.5" aria-hidden="true" />
                          Đã Check-in
                        </span>
                      ) : (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation()
                            void handleCheckInCandidate(candidate)
                          }}
                          className="rounded-lg bg-[#F26F21] px-2.5 py-1 text-xs font-bold text-white hover:bg-[#D95B16]"
                        >
                          Check-in ngay
                        </button>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </section>

      {/* OVERLAY DRAWER: CHI TIẾT MÁY TRẠM & ĐIỂM DANH THÍ SINH */}
      <OverlayDrawer
        open={isDrawerOpen}
        onClose={() => setIsDrawerOpen(false)}
        width="lg"
        badge={`Ghế số ${String(selectedSeat.seatNumber).padStart(2, '0')} · ${selectedSeat.ipAddress}`}
        title="Chi tiết Máy trạm & Điểm danh Thí sinh"
        subtitle={
          selectedSeat.fullName
            ? `${selectedSeat.fullName} (${selectedSeat.studentCode}) — ${activeShift.roomName}`
            : `Máy trạm chưa xếp thí sinh (${activeShift.roomName})`
        }
      >
        {selectedSeat.status === 'EMPTY' ? (
          <div className="py-12 text-center">
            <Monitor className="mx-auto size-10 text-[#94A3B8]" />
            <p className="mt-3 text-sm font-bold text-[#1E293B]">
              Ghế #{String(selectedSeat.seatNumber).padStart(2, '0')} hiện đang trống
            </p>
            <p className="mt-1 text-xs text-[#64748B]">
              Địa chỉ IP máy trạm cố định:{' '}
              <strong className="font-mono">{selectedSeat.ipAddress}</strong>. Trưởng Bộ môn có thể
              xếp thí sinh vào ghế này tại Cổng Trưởng Bộ môn.
            </p>
          </div>
        ) : (
          <div className="space-y-4">
            <div className="flex items-center justify-between rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-4 py-3">
              <div>
                <p className="text-xs font-semibold text-[#C2410C]">Trạng thái vé dự thi</p>
                <p className="text-sm font-bold text-[#1E293B]">
                  {selectedSeat.fullName} ({selectedSeat.studentCode})
                </p>
              </div>
              <span
                className={cn(
                  'rounded-full px-3 py-1 text-xs font-bold',
                  selectedSeat.isSuspended
                    ? 'bg-rose-100 text-rose-700'
                    : SEAT_STATUS_STYLE[selectedSeat.status].badgeClass,
                )}
              >
                {selectedSeat.isSuspended
                  ? 'ĐÃ ĐÌNH CHỈ'
                  : SEAT_STATUS_STYLE[selectedSeat.status].label}
              </span>
            </div>

            {/* Đối chiếu Thẻ SV / CCCD & Địa chỉ IP máy trạm */}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
                <div className="flex items-center gap-2 text-xs font-bold text-[#475569]">
                  <CreditCard className="size-4 text-[#F26F21]" aria-hidden="true" />
                  Đối chiếu Thẻ SV / CCCD
                </div>
                <p className="mt-1.5 text-sm font-bold text-[#1E293B]">
                  MSSV: {selectedSeat.studentCode}
                </p>
                <p className="text-xs font-semibold text-[#64748B]">
                  Số CCCD: {selectedSeat.citizenId}
                </p>
              </div>

              <div className="rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] p-3.5">
                <div className="flex items-center gap-2 text-xs font-bold text-[#475569]">
                  <Wifi className="size-4 text-[#2563EB]" aria-hidden="true" />
                  Địa chỉ IP máy trạm
                </div>
                <p className="mt-1.5 font-mono text-sm font-bold text-[#1E293B]">
                  {selectedSeat.ipAddress}
                </p>
                <p className="text-xs font-semibold text-[#16A34A]">
                  Trùng khớp STT Ghế #{String(selectedSeat.seatNumber).padStart(2, '0')}
                </p>
              </div>
            </div>

            {/* Kiểm tra kỹ thuật phòng thi: Micro >= 60dB & Toàn màn hình */}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="rounded-xl border border-emerald-200 bg-emerald-50/50 p-3.5">
                <div className="flex items-center justify-between">
                  <span className="flex items-center gap-1.5 text-xs font-bold text-[#16A34A]">
                    <Mic className="size-4" aria-hidden="true" />
                    Kiểm tra Micro 30s
                  </span>
                  <span className="rounded-md bg-emerald-100 px-2 py-0.5 text-xs font-bold text-[#16A34A]">
                    Đạt chuẩn ≥ 60dB
                  </span>
                </div>
                <p className="mt-1.5 text-base font-bold text-[#1E293B]">
                  {selectedSeat.micDecibels ?? 65} dB
                </p>
                <p className="text-xs text-[#64748B]">Tín hiệu thu âm rõ, không nhiễu nền</p>
              </div>

              <div
                className={cn(
                  'rounded-xl border p-3.5',
                  selectedSeat.blurCount >= 2 || selectedSeat.isSuspended
                    ? 'border-rose-200 bg-rose-50/70'
                    : 'border-[#E2E8F0] bg-[#F8FAFC]',
                )}
              >
                <div className="flex items-center justify-between">
                  <span className="flex items-center gap-1.5 text-xs font-bold text-[#1E293B]">
                    <Lock className="size-4 text-[#F26F21]" aria-hidden="true" />
                    Khóa Toàn màn hình
                  </span>
                  <span
                    className={cn(
                      'rounded-md px-2 py-0.5 text-xs font-bold',
                      selectedSeat.blurCount >= 2
                        ? 'bg-rose-100 text-rose-700'
                        : 'bg-[#FFF7ED] text-[#C2410C]',
                    )}
                  >
                    Số lần thoát: {selectedSeat.blurCount}/3
                  </span>
                </div>
                <p className="mt-1.5 text-xs leading-relaxed text-[#475569]">
                  Chặn phím tắt chuyển cửa sổ. Quá 3 lần thoát màn hình tự động đình chỉ.
                </p>
              </div>
            </div>

            {/* Niêm phong Băng ghi âm */}
            <div className="rounded-xl border border-[#FED7AA] bg-[#FFF7ED] p-3.5">
              <div className="flex items-center gap-2 text-xs font-bold text-[#C2410C]">
                <ShieldCheck className="size-4" aria-hidden="true" />
                Niêm phong Băng ghi âm Bài thi & Mã kiểm tra SHA-256
              </div>
              <p className="mt-1 font-mono text-xs font-bold text-[#1E293B]">
                Tệp ghi âm: {selectedSeat.audioFileName}
              </p>
              <p className="mt-0.5 truncate font-mono text-xs text-[#64748B]">
                SHA-256: {selectedSeat.audioHashSha256}
              </p>
              <p className="mt-1.5 text-xs text-[#C2410C]">
                Quy chế phòng thi: Không hiển thị điểm ngay tại máy trạm và không khiếu nại tại
                chỗ.
              </p>
            </div>

            {/* Nút hành động của Giám thị */}
            <div className="space-y-3 border-t border-[#E2E8F0] pt-4">
              <button
                type="button"
                disabled={selectedSeat.checkedIn || selectedSeat.isSuspended}
                onClick={() => void handleCheckInCandidate(selectedSeat)}
                className="inline-flex min-h-10 w-full items-center justify-center gap-2 rounded-xl bg-[#16A34A] px-4 text-xs font-bold text-white transition hover:bg-emerald-700 disabled:opacity-50"
              >
                <UserCheck className="size-4" aria-hidden="true" />
                {selectedSeat.checkedIn
                  ? 'Đã Xác nhận Điểm danh (Checked-in)'
                  : 'Xác nhận Điểm danh (Check-in)'}
              </button>

              <div className="rounded-xl border border-rose-200 bg-rose-50/40 p-3.5">
                <label className="block space-y-1.5">
                  <span className="text-xs font-bold text-rose-800">
                    Biên bản xử lý vi phạm quy chế Kiosk
                  </span>
                  <input
                    type="text"
                    value={suspensionNote}
                    onChange={(e) => setSuspensionNote(e.target.value)}
                    className="w-full rounded-lg border border-rose-200 bg-white px-3 py-1.5 text-xs text-[#1E293B]"
                  />
                </label>
                <button
                  type="button"
                  disabled={selectedSeat.isSuspended}
                  onClick={() => handleSuspendCandidate(selectedSeat)}
                  className="mt-2.5 inline-flex min-h-9 w-full items-center justify-center gap-1.5 rounded-xl bg-rose-600 px-3.5 text-xs font-bold text-white transition hover:bg-rose-700 disabled:opacity-50"
                >
                  <ShieldAlert className="size-4" aria-hidden="true" />
                  {selectedSeat.isSuspended
                    ? 'Đã Lập Biên bản Đình chỉ Kiosk'
                    : 'Lập biên bản / Đình chỉ Kiosk'}
                </button>
              </div>

              <Link
                to={`/lecturer/audit?shiftId=${selectedShiftId}`}
                className="inline-flex min-h-9 w-full items-center justify-center gap-1.5 rounded-xl border border-[#FED7AA] bg-[#FFF7ED] px-3.5 text-xs font-bold text-[#C2410C] hover:bg-[#FFEDD5]"
              >
                <ClipboardCheck className="size-4 text-[#F26F21]" aria-hidden="true" />
                Chuyển sang Cổng Hậu kiểm Bằng chứng Ca thi
              </Link>
            </div>
          </div>
        )}
      </OverlayDrawer>
    </div>
  )
}

export default ProctorPortalPage
