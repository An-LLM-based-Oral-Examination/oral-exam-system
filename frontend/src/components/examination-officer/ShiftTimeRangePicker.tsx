import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, CalendarClock, Clock, Zap } from 'lucide-react'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import {
  addMinutesToLocalDateTime,
  computeDurationMinutes,
  formatLocalDateTimeInput,
  formatShiftTimeSummaryVi,
  getLocalTodayDate,
  shiftEndPreservingDuration,
} from './shiftStaffHelpers'

const DURATION_PRESETS = [30, 45, 60, 90, 120] as const
const CLOCK_PRESETS = [
  { label: '07:30', hour: 7, minute: 30 },
  { label: '09:30', hour: 9, minute: 30 },
  { label: '13:30', hour: 13, minute: 30 },
  { label: '15:30', hour: 15, minute: 30 },
] as const

export interface ShiftTimeRangePickerProps {
  startId?: string
  endId?: string
  startLabel?: string
  endLabel?: string
  startAriaLabel?: string
  endAriaLabel?: string
  startTime: string
  endTime: string
  mode?: 'create' | 'edit'
  semesterBounds?: {
    startDate?: string
    endDate?: string
    code?: string
  }
  disabled?: boolean
  onChange: (nextStartTime: string, nextEndTime: string) => void
}

function pad2(n: number) {
  return String(n).padStart(2, '0')
}

function resolveDatePart(startTime: string, semesterBounds?: ShiftTimeRangePickerProps['semesterBounds']) {
  if (/^\d{4}-\d{2}-\d{2}/.test(startTime)) {
    return startTime.slice(0, 10)
  }
  const today = getLocalTodayDate()
  const semStart = semesterBounds?.startDate?.slice(0, 10)
  const semEnd = semesterBounds?.endDate?.slice(0, 10)
  if (semStart && today < semStart) return semStart
  if (semEnd && today > semEnd) return semEnd
  return today
}

function buildLocalDateFromYmd(ymd: string, hour: number, minute: number): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(ymd)
  if (!match) return null
  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]), hour, minute, 0, 0)
}

export function ShiftTimeRangePicker({
  startId,
  endId,
  startLabel = 'Bắt đầu *',
  endLabel = 'Kết thúc *',
  startAriaLabel,
  endAriaLabel,
  startTime,
  endTime,
  mode = 'create',
  semesterBounds,
  disabled = false,
  onChange,
}: ShiftTimeRangePickerProps) {
  const [referenceNowMs, setReferenceNowMs] = useState(() => new Date().getTime())

  useEffect(() => {
    const timer = window.setInterval(() => {
      setReferenceNowMs(new Date().getTime())
    }, 30_000)
    return () => window.clearInterval(timer)
  }, [])

  const currentDuration = useMemo(() => {
    const minutes = computeDurationMinutes(startTime, endTime)
    return minutes && minutes > 0 ? minutes : 60
  }, [startTime, endTime])

  const summaryText = useMemo(
    () => formatShiftTimeSummaryVi(startTime, endTime),
    [startTime, endTime],
  )

  const warnings = useMemo(() => {
    const list: Array<{ tone: 'danger' | 'warning'; text: string }> = []
    if (!startTime || !endTime) return list
    const startDate = new Date(startTime)
    const endDate = new Date(endTime)
    if (Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime())) {
      list.push({ tone: 'danger', text: 'Định dạng thời gian ca thi chưa hợp lệ.' })
      return list
    }
    if (endDate <= startDate) {
      list.push({ tone: 'danger', text: 'Giờ kết thúc phải sau giờ bắt đầu.' })
      return list
    }
    if (mode === 'edit' && startDate.getTime() <= referenceNowMs) {
      list.push({
        tone: 'warning',
        text: 'Khi cập nhật ca đã lên lịch, giờ bắt đầu mới cần ở trong tương lai.',
      })
    } else if (endDate.getTime() <= referenceNowMs) {
      list.push({
        tone: 'warning',
        text: 'Khung giờ này kết thúc trước thời điểm hiện tại — thí sinh sẽ không thể check-in.',
      })
    }
    const semStart = semesterBounds?.startDate?.slice(0, 10)
    const semEnd = semesterBounds?.endDate?.slice(0, 10)
    const startYmd = startTime.slice(0, 10)
    const endYmd = endTime.slice(0, 10)
    if ((semStart && startYmd < semStart) || (semEnd && startYmd > semEnd)) {
      list.push({
        tone: 'warning',
        text: `Ngày bắt đầu ca (${startYmd}) đang nằm ngoài học kỳ ${semesterBounds?.code ?? ''} (${semStart ?? '…'} – ${semEnd ?? '…'}).`,
      })
    } else if (semEnd && endYmd > semEnd) {
      list.push({
        tone: 'warning',
        text: `Thời gian kết thúc ca (${endYmd}) vượt quá ngày kết thúc học kỳ ${semesterBounds?.code ?? ''} (${semEnd}).`,
      })
    }
    return list
  }, [
    endTime,
    mode,
    referenceNowMs,
    semesterBounds?.code,
    semesterBounds?.endDate,
    semesterBounds?.startDate,
    startTime,
  ])

  const applyStartAndKeepDuration = (nextStart: string) => {
    if (!nextStart) {
      onChange('', endTime)
      return
    }
    const nextEnd = shiftEndPreservingDuration(startTime, endTime, nextStart, currentDuration)
    onChange(nextStart, nextEnd)
  }

  const applyNowPreset = () => {
    const now = new Date()
    now.setSeconds(0, 0)
    if (mode === 'edit') {
      now.setMinutes(now.getMinutes() + 10)
    }
    const nextStart = formatLocalDateTimeInput(now)
    const nextEnd = addMinutesToLocalDateTime(nextStart, currentDuration)
    onChange(nextStart, nextEnd)
  }

  const applyClockPreset = (hour: number, minute: number) => {
    const baseYmd = resolveDatePart(startTime, semesterBounds)
    let target = buildLocalDateFromYmd(baseYmd, hour, minute)
    if (!target) return
    const freshNowMs = new Date().getTime()
    setReferenceNowMs(freshNowMs)
    if (mode === 'edit' && target.getTime() <= freshNowMs) {
      const todayTarget = buildLocalDateFromYmd(getLocalTodayDate(new Date(freshNowMs)), hour, minute)
      if (todayTarget) {
        target =
          todayTarget.getTime() > freshNowMs
            ? todayTarget
            : new Date(todayTarget.getTime() + 24 * 60 * 60_000)
      }
    }
    const nextStart = formatLocalDateTimeInput(target)
    const nextEnd = addMinutesToLocalDateTime(nextStart, currentDuration)
    onChange(nextStart, nextEnd)
  }

  const applyDayOffset = (offsetDays: 0 | 1) => {
    const targetDay = new Date()
    targetDay.setDate(targetDay.getDate() + offsetDays)
    const ymd = `${targetDay.getFullYear()}-${pad2(targetDay.getMonth() + 1)}-${pad2(targetDay.getDate())}`
    const timePart = /^\d{4}-\d{2}-\d{2}T(\d{2}:\d{2})/.exec(startTime)?.[1] ?? '08:00'
    const nextStart = `${ymd}T${timePart}`
    const nextEnd = addMinutesToLocalDateTime(nextStart, currentDuration)
    onChange(nextStart, nextEnd)
  }

  const applyDurationPreset = (minutes: number) => {
    let baseStart = startTime
    if (!baseStart) {
      const now = new Date()
      now.setSeconds(0, 0)
      if (mode === 'edit') {
        now.setMinutes(now.getMinutes() + 10)
      }
      baseStart = formatLocalDateTimeInput(now)
    }
    const nextEnd = addMinutesToLocalDateTime(baseStart, minutes)
    onChange(baseStart, nextEnd)
  }

  const activeClock = /^\d{4}-\d{2}-\d{2}T(\d{2}:\d{2})/.exec(startTime)?.[1] ?? ''
  const exactDuration = computeDurationMinutes(startTime, endTime)

  return (
    <div className="space-y-2.5">
      <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[#d9e8f2] bg-[#F8FBFD] px-3 py-2">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="inline-flex items-center gap-1 text-[11px] font-bold text-[#52657d]">
            <Clock className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
            Chọn nhanh:
          </span>
          <button
            type="button"
            disabled={disabled}
            onClick={applyNowPreset}
            className="inline-flex items-center gap-1 rounded-lg border border-[#168b70]/40 bg-[#effbf5] px-2 py-1 text-[11px] font-bold text-[#146c54] transition hover:bg-[#dff6ec] disabled:opacity-50"
          >
            <Zap className="size-3" aria-hidden="true" />
            {mode === 'edit' ? 'Sau 10 phút' : 'Thi ngay'}
          </button>
          {CLOCK_PRESETS.map((preset) => (
            <button
              key={preset.label}
              type="button"
              disabled={disabled}
              onClick={() => applyClockPreset(preset.hour, preset.minute)}
              className={cn(
                'rounded-lg border px-2 py-1 text-[11px] font-bold transition disabled:opacity-50',
                activeClock === preset.label
                  ? 'border-[#1C4D8D] bg-[#1C4D8D] text-white'
                  : 'border-[#cfe2f3] bg-white text-[#1C4D8D] hover:bg-[#eaf4fc]',
              )}
            >
              {preset.label}
            </button>
          ))}
        </div>

        <div className="flex flex-wrap items-center gap-1.5">
          <button
            type="button"
            disabled={disabled}
            onClick={() => applyDayOffset(0)}
            className="rounded-lg border border-[#d9e8f2] bg-white px-2 py-1 text-[11px] font-semibold text-[#52657d] transition hover:border-[#4988C4] hover:text-[#0F2854] disabled:opacity-50"
          >
            Hôm nay
          </button>
          <button
            type="button"
            disabled={disabled}
            onClick={() => applyDayOffset(1)}
            className="rounded-lg border border-[#d9e8f2] bg-white px-2 py-1 text-[11px] font-semibold text-[#52657d] transition hover:border-[#4988C4] hover:text-[#0F2854] disabled:opacity-50"
          >
            Ngày mai
          </button>
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor={startId}>
          {startLabel}
          <input
            id={startId}
            required
            type="datetime-local"
            aria-label={startAriaLabel}
            className={ds.field}
            value={startTime}
            disabled={disabled}
            onChange={(event) => applyStartAndKeepDuration(event.target.value)}
          />
        </label>
        <label className="grid gap-1.5 text-xs font-bold text-[#52657d]" htmlFor={endId}>
          {endLabel}
          <input
            id={endId}
            required
            type="datetime-local"
            aria-label={endAriaLabel}
            className={ds.field}
            value={endTime}
            disabled={disabled}
            onChange={(event) => onChange(startTime, event.target.value)}
          />
        </label>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 text-[11px]">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="font-bold text-[#52657d]">Thời lượng:</span>
          {DURATION_PRESETS.map((minutes) => (
            <button
              key={minutes}
              type="button"
              disabled={disabled}
              onClick={() => applyDurationPreset(minutes)}
              className={cn(
                'rounded-md border px-2 py-0.5 font-bold transition disabled:opacity-50',
                exactDuration === minutes
                  ? 'border-[#1C4D8D] bg-[#eef7fd] text-[#1C4D8D]'
                  : 'border-[#d9e8f2] bg-white text-[#52657d] hover:border-[#82aedb] hover:text-[#0F2854]',
              )}
            >
              {minutes}p
            </button>
          ))}
        </div>

        {summaryText ? (
          <span className="inline-flex items-center gap-1.5 rounded-lg bg-[#f3f8fd] px-2.5 py-1 font-semibold text-[#0F2854]">
            <CalendarClock className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
            {summaryText}
          </span>
        ) : null}
      </div>

      {warnings.length > 0 ? (
        <div className="space-y-1.5">
          {warnings.map((warning) => (
            <div
              key={warning.text}
              role="status"
              className={cn(
                'flex items-start gap-2 rounded-xl border px-3 py-2 text-xs font-semibold',
                warning.tone === 'danger'
                  ? 'border-[#f3c2c8] bg-[#fdecec] text-[#a43d4b]'
                  : 'border-[#f2d091] bg-[#fff9eb] text-[#8c5a00]',
              )}
            >
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
              <span>{warning.text}</span>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  )
}
