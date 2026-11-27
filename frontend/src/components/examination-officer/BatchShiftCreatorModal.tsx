import { useState, type FormEvent } from 'react'
import { Building2, LoaderCircle, Plus } from 'lucide-react'
import { officialExamService } from '@/services/officialExam.service'
import type { ShiftStaffCandidate } from '@/services/course.service'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

interface RoomDraftRow {
  roomLab: string
  maxSeats: number
  proctorUserId: string
  examinerUserId: string
}

interface BatchShiftCreatorModalProps {
  open: boolean
  sessionId: string
  defaultExamDate?: string
  recommendedRooms: number
  proctors: ShiftStaffCandidate[]
  examiners: ShiftStaffCandidate[]
  onClose: () => void
  onCreated: () => Promise<void> | void
}

function buildInitialRooms(count: number): RoomDraftRow[] {
  const safeCount = Math.max(1, Math.min(12, count))
  return Array.from({ length: safeCount }, (_, index) => ({
    roomLab: `Lab ${301 + index}`,
    maxSeats: 24,
    proctorUserId: '',
    examinerUserId: '',
  }))
}

export function BatchShiftCreatorModal({
  open,
  sessionId,
  defaultExamDate,
  recommendedRooms,
  proctors,
  examiners,
  onClose,
  onCreated,
}: BatchShiftCreatorModalProps) {
  const baseDate = defaultExamDate?.slice(0, 10) || new Date().toISOString().slice(0, 10)
  const [shiftGroupName, setShiftGroupName] = useState('Ca 1 - Sáng')
  const [startTime, setStartTime] = useState(`${baseDate}T08:00`)
  const [endTime, setEndTime] = useState(`${baseDate}T09:30`)
  const [rooms, setRooms] = useState<RoomDraftRow[]>(() =>
    buildInitialRooms(recommendedRooms || 1),
  )
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const setRoomCount = (nextCount: number) => {
    const safeCount = Math.max(1, Math.min(12, nextCount))
    setRooms((current) => {
      if (safeCount === current.length) return current
      if (safeCount < current.length) return current.slice(0, safeCount)
      const added = Array.from({ length: safeCount - current.length }, (_, offset) => ({
        roomLab: `Lab ${301 + current.length + offset}`,
        maxSeats: 24,
        proctorUserId: '',
        examinerUserId: '',
      }))
      return [...current, ...added]
    })
  }

  const updateRoom = <K extends keyof RoomDraftRow>(
    index: number,
    key: K,
    value: RoomDraftRow[K],
  ) => {
    setRooms((current) =>
      current.map((room, idx) => (idx === index ? { ...room, [key]: value } : room)),
    )
    if (error) setError('')
  }

  const validateRooms = (): string => {
    if (!shiftGroupName.trim()) return 'Vui lòng nhập tên nhóm ca thi.'
    if (!startTime || !endTime || new Date(endTime) <= new Date(startTime)) {
      return 'Thời gian kết thúc phải sau thời gian bắt đầu.'
    }
    const seenRooms = new Set<string>()
    const seenStaff = new Set<string>()
    for (let i = 0; i < rooms.length; i += 1) {
      const room = rooms[i]
      const lab = room.roomLab.trim()
      if (!lab) return `Phòng #${i + 1}: vui lòng nhập tên phòng Lab.`
      const normalizedLab = lab.toLowerCase()
      if (seenRooms.has(normalizedLab)) return `Phòng "${lab}" bị trùng trong cùng khung giờ.`
      seenRooms.add(normalizedLab)

      if (!Number.isInteger(room.maxSeats) || room.maxSeats < 1 || room.maxSeats > 24) {
        return `Phòng "${lab}": sức chứa batch phải từ 1 đến 24 ghế.`
      }
      if (!room.proctorUserId || !room.examinerUserId) {
        return `Phòng "${lab}": bắt buộc chọn cả Giám thị và Giám khảo từ danh sách.`
      }
      if (room.proctorUserId === room.examinerUserId) {
        return `Phòng "${lab}": Giám thị và Giám khảo không được là cùng một người.`
      }
      if (seenStaff.has(room.proctorUserId)) {
        return `Phòng "${lab}": Giám thị đã được phân công ở phòng khác trong cùng khung giờ.`
      }
      seenStaff.add(room.proctorUserId)
      if (seenStaff.has(room.examinerUserId)) {
        return `Phòng "${lab}": Giám khảo đã được phân công ở phòng khác trong cùng khung giờ.`
      }
      seenStaff.add(room.examinerUserId)
    }
    return ''
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (busy) return
    const validationError = validateRooms()
    if (validationError) {
      setError(validationError)
      return
    }

    setBusy(true)
    setError('')
    try {
      const response = await officialExamService.batchOpenShifts(sessionId, {
        shiftGroupName: shiftGroupName.trim(),
        startTime: new Date(startTime).toISOString(),
        endTime: new Date(endTime).toISOString(),
        rooms: rooms.map((room) => ({
          roomLab: room.roomLab.trim(),
          maxSeats: room.maxSeats,
          proctorUserId: room.proctorUserId,
          examinerUserId: room.examinerUserId,
        })),
      })
      toast.success(
        `Đã mở nguyên tử ${response.createdRoomCount} phòng thi (${response.totalCapacityCreated} ghế).`,
      )
      await Promise.resolve(onCreated())
      onClose()
    } catch (submitError) {
      const message = userMessage(submitError, 'Không thể mở hàng loạt phòng thi.')
      setError(message)
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormDialog
      open={open}
      align="center"
      size="xl"
      variant="calm"
      icon={Building2}
      eyebrow="BƯỚC 4 · MỞ HÀNG LOẠT PHÒNG THI"
      title="Tạo ca thi với K phòng Lab (chuẩn 24 SV/phòng)"
      busy={busy}
      onClose={() => !busy && onClose()}
      footer={
        <DialogFooter variant="calm">
          <button
            type="button"
            className={ds.outlineButton}
            disabled={busy}
            onClick={onClose}
          >
            Hủy
          </button>
          <button
            type="submit"
            form="eo-batch-shift-form"
            disabled={busy}
            className={ds.primaryButton}
          >
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : <Plus className="size-4" />}
            {busy ? 'Đang mở phòng…' : `Mở ${rooms.length} phòng thi`}
          </button>
        </DialogFooter>
      }
    >
      <form id="eo-batch-shift-form" onSubmit={handleSubmit} className="space-y-3">
        {error ? (
          <div
            role="alert"
            className="rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-4 py-3 text-sm font-semibold text-[#a43d4b]"
          >
            {error}
          </div>
        ) : null}

        <div className="grid gap-3 sm:grid-cols-3">
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
            Tên nhóm ca *
            <input
              required
              aria-label="Tên nhóm ca"
              className={ds.field}
              value={shiftGroupName}
              onChange={(event) => setShiftGroupName(event.target.value)}
              placeholder="Ví dụ: Ca 1 - Sáng"
            />
          </label>
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
            Giờ bắt đầu *
            <input
              required
              type="datetime-local"
              aria-label="Giờ bắt đầu nhóm ca"
              className={ds.field}
              value={startTime}
              onChange={(event) => setStartTime(event.target.value)}
            />
          </label>
          <label className="grid gap-1.5 text-xs font-bold text-[#52657d]">
            Giờ kết thúc *
            <input
              required
              type="datetime-local"
              aria-label="Giờ kết thúc nhóm ca"
              className={ds.field}
              value={endTime}
              onChange={(event) => setEndTime(event.target.value)}
            />
          </label>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-4 py-3">
          <span className="text-xs font-bold text-[#0F2854]">
            Số lượng phòng K (đề xuất: {Math.max(1, recommendedRooms)} phòng):
          </span>
          <div className="flex flex-wrap items-center gap-1.5">
            {[1, 2, 3, 4, 5, 6].map((preset) => (
              <button
                key={preset}
                type="button"
                onClick={() => setRoomCount(preset)}
                className={cn(
                  'h-8 min-w-9 rounded-lg border px-2.5 text-xs font-bold transition',
                  rooms.length === preset
                    ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                    : 'border-[#d9e8f2] bg-white text-[#1C4D8D] hover:bg-[#eaf4fc]',
                )}
              >
                {preset}
              </button>
            ))}
          </div>
        </div>

        <div className="space-y-2.5">
          {rooms.map((room, index) => (
            <div
              key={index}
              className="grid gap-2.5 rounded-xl border border-[#d9e8f2] bg-white p-3 sm:grid-cols-[1.1fr_90px_1.3fr_1.3fr]"
            >
              <label className="grid gap-1 text-[11px] font-bold text-[#52657d]">
                Phòng #{index + 1} *
                <input
                  required
                  aria-label={`Tên phòng Lab ${index + 1}`}
                  className={cn(ds.field, 'h-9 text-xs')}
                  value={room.roomLab}
                  onChange={(event) => updateRoom(index, 'roomLab', event.target.value)}
                />
              </label>

              <label className="grid gap-1 text-[11px] font-bold text-[#52657d]">
                Ghế (1–24)
                <input
                  required
                  type="number"
                  min={1}
                  max={24}
                  step={1}
                  aria-label={`Sức chứa phòng ${index + 1}`}
                  className={cn(ds.field, 'h-9 text-xs')}
                  value={room.maxSeats}
                  onChange={(event) => updateRoom(index, 'maxSeats', Number(event.target.value))}
                />
              </label>

              <label className="grid gap-1 text-[11px] font-bold text-[#52657d]">
                Giám thị phòng {index + 1} *
                <select
                  required
                  aria-label={`Giám thị phòng ${index + 1}`}
                  className={cn(ds.field, 'h-9 text-xs')}
                  value={room.proctorUserId}
                  onChange={(event) => updateRoom(index, 'proctorUserId', event.target.value)}
                >
                  <option value="">Chọn Giám thị</option>
                  {proctors.map((candidate) => (
                    <option
                      key={candidate.userId}
                      value={candidate.userId}
                      disabled={candidate.userId === room.examinerUserId}
                    >
                      {candidate.fullName}
                      {candidate.detail ? ` · ${candidate.detail}` : ''}
                    </option>
                  ))}
                </select>
              </label>

              <label className="grid gap-1 text-[11px] font-bold text-[#52657d]">
                Giám khảo phòng {index + 1} *
                <select
                  required
                  aria-label={`Giám khảo phòng ${index + 1}`}
                  className={cn(ds.field, 'h-9 text-xs')}
                  value={room.examinerUserId}
                  onChange={(event) => updateRoom(index, 'examinerUserId', event.target.value)}
                >
                  <option value="">Chọn Giám khảo</option>
                  {examiners.map((candidate) => (
                    <option
                      key={candidate.userId}
                      value={candidate.userId}
                      disabled={candidate.userId === room.proctorUserId}
                    >
                      {candidate.fullName}
                      {candidate.detail ? ` · ${candidate.detail}` : ''}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          ))}
        </div>
      </form>
    </FormDialog>
  )
}
