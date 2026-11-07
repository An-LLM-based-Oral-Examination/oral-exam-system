import { useEffect, useLayoutEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import {
  AlertCircle,
  CalendarDays,
  Check,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CircleStop,
  ClipboardList,
  Clock3,
  LoaderCircle,
  Lock,
  Plus,
  Presentation,
  RefreshCw,
  Search,
  Timer,
  UsersRound,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError, fieldInvalidClass, fieldLabelClass } from '@/components/common/formField'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { InClassShiftPageBanner } from '@/pages/lecturer/InClassShiftPageBanner'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { inClassShiftService } from '@/services/inClassShift.service'
import { questionService } from '@/services/question.service'
import { InClassShiftProjectorDialog } from '@/pages/lecturer/InClassShiftProjectorDialog'
import type {
  CreateInClassShiftRequest,
  InClassQuestionPoolMode,
  InClassShift,
  InClassShiftStatus,
  LecturerClassOption,
} from '@/types/inClassShift.types'

type StatusFilter = 'ALL' | InClassShiftStatus

const STATUS_FILTERS: Array<{
  value: StatusFilter
  label: string
  countIdleClass: string
  dotClass: string
}> = [
  {
    value: 'ALL',
    label: 'Tất cả',
    countIdleClass: 'bg-[#e8eef6] text-[#1C4D8D]',
    dotClass: 'bg-[#1C4D8D]',
  },
  {
    value: 'SCHEDULED',
    label: 'Chờ kích hoạt',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'ACTIVE',
    label: 'Đang diễn ra',
    countIdleClass: 'bg-[#e8f7f1] text-[#197158]',
    dotClass: 'bg-[#1a9a72]',
  },
  {
    value: 'CLOSED',
    label: 'Đã kết thúc',
    countIdleClass: 'bg-[#fff4df] text-[#8d6118]',
    dotClass: 'bg-[#d4a017]',
  },
  {
    value: 'FINALIZED',
    label: 'Đã chốt',
    countIdleClass: 'bg-[#eef1f5] text-[#52647d]',
    dotClass: 'bg-[#7a8aa0]',
  },
]

function getErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail
    if (typeof detail === 'string' && detail.trim()) return detail
  }
  if (error instanceof Error && error.message) return error.message
  return 'Không thể tải dữ liệu ca thi trên lớp.'
}

function statusChip(status: InClassShiftStatus) {
  if (status === 'SCHEDULED') {
    return (
      <span className={ds.infoChip}>
        <Clock3 className="size-3" />
        Chờ kích hoạt
      </span>
    )
  }
  if (status === 'ACTIVE') {
    return (
      <span className={ds.successChip}>
        <span className="size-1.5 rounded-full bg-[#16836a]" />
        Đang mở
      </span>
    )
  }
  if (status === 'FINALIZED') {
    return (
      <span className={cn(ds.infoChip, 'border-[#82aedb]/55 bg-[#f3f6fa] text-[#5b6b84]')}>
        <Lock className="size-3" />
        Đã chốt
      </span>
    )
  }
  return <span className={ds.infoChip}>Đã kết thúc</span>
}

function pad2(value: number) {
  return String(value).padStart(2, '0')
}

function toDateKey(date: Date) {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
}

function toClock(date: Date) {
  return `${pad2(date.getHours())}:${pad2(date.getMinutes())}`
}

/** ISO local có offset — backend so sánh UTC không bị lệch múi giờ. */
function toOffsetIso(dateKey: string, clock: string) {
  const [year, month, day] = dateKey.split('-').map(Number)
  const [hour, minute] = clock.split(':').map(Number)
  const local = new Date(year, (month || 1) - 1, day || 1, hour || 0, minute || 0, 0, 0)
  if (Number.isNaN(local.getTime())) return ''
  const offsetMinutes = -local.getTimezoneOffset()
  const sign = offsetMinutes >= 0 ? '+' : '-'
  const abs = Math.abs(offsetMinutes)
  return `${dateKey}T${pad2(hour || 0)}:${pad2(minute || 0)}:00${sign}${pad2(Math.floor(abs / 60))}:${pad2(abs % 60)}`
}

function parseDateKey(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, (month || 1) - 1, day || 1)
}

function shiftDateKey(value: string, days: number) {
  const date = parseDateKey(value)
  date.setDate(date.getDate() + days)
  return toDateKey(date)
}

function formatExamDay(value: string) {
  return parseDateKey(value).toLocaleDateString('vi-VN', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
}

function formatShiftPoint(value?: string | null) {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

function formatShiftDate(date: Date) {
  return date.toLocaleDateString('vi-VN')
}

function formatShiftClock(date: Date) {
  return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
}

function formatDuration(minutes: number) {
  if (minutes <= 0) return '0 phút'
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  if (hours === 0) return `${rest} phút`
  if (rest === 0) return `${hours} giờ`
  return `${hours} giờ ${rest} phút`
}

const CLOCK_HOURS = Array.from({ length: 24 }, (_, index) => pad2(index))
const CLOCK_MINUTES = Array.from({ length: 60 }, (_, index) => pad2(index))

function ClockField({
  label,
  value,
  invalid = false,
  onChange,
}: {
  label: string
  value: string
  invalid?: boolean
  onChange: (value: string) => void
}) {
  const [open, setOpen] = useState(false)
  const [box, setBox] = useState<{ left: number; width: number; top?: number; bottom?: number } | null>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)
  const hourListRef = useRef<HTMLDivElement>(null)
  const minuteListRef = useRef<HTMLDivElement>(null)
  const [hour = '00', minute = '00'] = (value || '00:00').split(':')

  useEffect(() => {
    if (!open) return
    const onPointer = (event: MouseEvent) => {
      const target = event.target as Node
      if (buttonRef.current?.contains(target) || panelRef.current?.contains(target)) return
      setOpen(false)
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      event.stopPropagation()
      setOpen(false)
    }
    document.addEventListener('mousedown', onPointer)
    window.addEventListener('keydown', onKey, true)
    return () => {
      document.removeEventListener('mousedown', onPointer)
      window.removeEventListener('keydown', onKey, true)
    }
  }, [open])

  useLayoutEffect(() => {
    if (!open) {
      setBox(null)
      return
    }
    const place = () => {
      const rect = buttonRef.current?.getBoundingClientRect()
      if (!rect) return
      const gap = 6
      const pad = 8
      const width = Math.max(rect.width, 196)
      const panelHeight = 236
      const openUp = rect.bottom + gap + panelHeight > window.innerHeight - pad
      const left = Math.min(Math.max(pad, rect.left), window.innerWidth - width - pad)
      setBox(
        openUp
          ? { bottom: window.innerHeight - rect.top + gap, left, width }
          : { top: rect.bottom + gap, left, width },
      )
    }
    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open])

  useEffect(() => {
    if (!open) return
    hourListRef.current?.querySelector<HTMLElement>(`[data-value="${hour}"]`)?.scrollIntoView({ block: 'center' })
    minuteListRef.current?.querySelector<HTMLElement>(`[data-value="${minute}"]`)?.scrollIntoView({ block: 'center' })
  }, [open, hour, minute])

  return (
    <div>
      <span className={fieldLabelClass(invalid)}>{label}</span>
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label={label}
        onClick={() => setOpen((current) => !current)}
        aria-invalid={invalid || undefined}
        className={cn(ds.field, 'mt-1.5 flex items-center justify-between gap-2 px-3 text-left', invalid && fieldInvalidClass)}
      >
        <span className="text-base font-black tracking-wide tabular-nums">{value || '--:--'}</span>
        <Clock3 className="size-4 text-[#4988C4]" aria-hidden="true" />
      </button>
      {open && box
        ? createPortal(
            <div
              ref={panelRef}
              role="dialog"
              aria-label={`Chọn giờ ${label}`}
              style={{ top: box.top, bottom: box.bottom, left: box.left, width: box.width }}
              className="fixed z-[120] overflow-hidden rounded-2xl border border-[#4988C4] bg-white shadow-[0_16px_40px_rgba(15,40,84,0.18)]"
            >
              <div className="grid grid-cols-2 border-b border-[#4988C4] bg-[#f4fbfd] px-2 py-2 text-center text-[11px] font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                <span>Giờ</span>
                <span>Phút</span>
              </div>
              <div className="grid grid-cols-2">
                <div ref={hourListRef} className="max-h-48 overflow-y-auto py-1">
                  {CLOCK_HOURS.map((item) => (
                    <button
                      key={item}
                      type="button"
                      data-value={item}
                      onClick={() => onChange(`${item}:${minute}`)}
                      className={cn(
                        'block w-full py-1.5 text-center text-sm font-bold tabular-nums',
                        item === hour ? 'bg-[#1C4D8D] text-white' : 'text-[#0F2854] hover:bg-[#e7f1fb]',
                      )}
                    >
                      {item}
                    </button>
                  ))}
                </div>
                <div ref={minuteListRef} className="max-h-48 overflow-y-auto border-l border-[#82aedb]/55 py-1">
                  {CLOCK_MINUTES.map((item) => (
                    <button
                      key={item}
                      type="button"
                      data-value={item}
                      onClick={() => onChange(`${hour}:${item}`)}
                      className={cn(
                        'block w-full py-1.5 text-center text-sm font-bold tabular-nums',
                        item === minute ? 'bg-[#1C4D8D] text-white' : 'text-[#0F2854] hover:bg-[#e7f1fb]',
                      )}
                    >
                      {item}
                    </button>
                  ))}
                </div>
              </div>
            </div>,
            document.body,
          )
        : null}
    </div>
  )
}

const WEEKDAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']

function monthCells(year: number, month: number) {
  const offset = (new Date(year, month, 1).getDay() + 6) % 7
  const daysInMonth = new Date(year, month + 1, 0).getDate()
  const cells: Array<{ key: string; day: number } | null> = Array.from({ length: offset }, () => null)
  for (let day = 1; day <= daysInMonth; day += 1) {
    cells.push({ key: toDateKey(new Date(year, month, day)), day })
  }
  return cells
}

function DateField({
  label,
  value,
  invalid = false,
  onChange,
}: {
  label: string
  value: string
  invalid?: boolean
  onChange: (value: string) => void
}) {
  const [open, setOpen] = useState(false)
  const [box, setBox] = useState<{ left: number; top?: number; bottom?: number } | null>(null)
  const [cursor, setCursor] = useState(() => new Date())
  const buttonRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)
  const todayKey = toDateKey(new Date())
  const cells = monthCells(cursor.getFullYear(), cursor.getMonth())
  const monthLabel = cursor.toLocaleDateString('vi-VN', { month: 'long', year: 'numeric' })

  useEffect(() => {
    if (!open || !value) return
    const date = parseDateKey(value)
    setCursor(new Date(date.getFullYear(), date.getMonth(), 1))
  }, [open, value])

  useEffect(() => {
    if (!open) return
    const onPointer = (event: MouseEvent) => {
      const target = event.target as Node
      if (buttonRef.current?.contains(target) || panelRef.current?.contains(target)) return
      setOpen(false)
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      event.stopPropagation()
      setOpen(false)
    }
    document.addEventListener('mousedown', onPointer)
    window.addEventListener('keydown', onKey, true)
    return () => {
      document.removeEventListener('mousedown', onPointer)
      window.removeEventListener('keydown', onKey, true)
    }
  }, [open])

  useLayoutEffect(() => {
    if (!open) {
      setBox(null)
      return
    }
    const place = () => {
      const rect = buttonRef.current?.getBoundingClientRect()
      if (!rect) return
      const gap = 6
      const pad = 8
      const width = 292
      const panelHeight = 340
      const openUp = rect.bottom + gap + panelHeight > window.innerHeight - pad
      const left = Math.min(Math.max(pad, rect.left), window.innerWidth - width - pad)
      setBox(
        openUp
          ? { bottom: window.innerHeight - rect.top + gap, left }
          : { top: rect.bottom + gap, left },
      )
    }
    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open])

  const pick = (key: string) => {
    onChange(key)
    setOpen(false)
  }

  return (
    <div>
      <span className={fieldLabelClass(invalid)}>{label}</span>
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label={label}
        onClick={() => setOpen((current) => !current)}
        aria-invalid={invalid || undefined}
        className={cn(ds.field, 'mt-1.5 flex items-center justify-between gap-2 px-3 text-left', invalid && fieldInvalidClass)}
      >
        <span className="truncate text-sm font-bold capitalize">{value ? formatExamDay(value) : 'Chọn ngày'}</span>
        <CalendarDays className="size-4 shrink-0 text-[#4988C4]" aria-hidden="true" />
      </button>
      {open && box
        ? createPortal(
            <div
              ref={panelRef}
              role="dialog"
              aria-label={label}
              style={{ top: box.top, bottom: box.bottom, left: box.left, width: 292 }}
              className="fixed z-[120] overflow-hidden rounded-2xl border border-[#4988C4] bg-white shadow-[0_16px_40px_rgba(15,40,84,0.18)]"
            >
              <div className="flex items-center justify-between gap-2 border-b border-[#4988C4] bg-[#f4fbfd] px-2 py-2">
                <button
                  type="button"
                  aria-label="Tháng trước"
                  onClick={() => setCursor((current) => new Date(current.getFullYear(), current.getMonth() - 1, 1))}
                  className="flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] hover:bg-white"
                >
                  <ChevronLeft className="size-4" />
                </button>
                <p className="text-sm font-bold text-[#0F2854] capitalize">{monthLabel}</p>
                <button
                  type="button"
                  aria-label="Tháng sau"
                  onClick={() => setCursor((current) => new Date(current.getFullYear(), current.getMonth() + 1, 1))}
                  className="flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] hover:bg-white"
                >
                  <ChevronRight className="size-4" />
                </button>
              </div>
              <div className="grid grid-cols-7 gap-0.5 px-2 pt-2">
                {WEEKDAYS.map((day) => (
                  <span key={day} className="py-1 text-center text-[11px] font-bold text-[#5b6b84]">{day}</span>
                ))}
              </div>
              <div className="grid grid-cols-7 gap-0.5 px-2 pb-2">
                {cells.map((cell, index) => cell ? (
                  <button
                    key={cell.key}
                    type="button"
                    onClick={() => pick(cell.key)}
                    className={cn(
                      'flex h-8 items-center justify-center rounded-lg text-sm font-bold',
                      cell.key === value
                        ? 'bg-[#1C4D8D] text-white'
                        : cell.key === todayKey
                          ? 'text-[#1C4D8D] ring-1 ring-[#4988C4] hover:bg-[#e7f1fb]'
                          : 'text-[#0F2854] hover:bg-[#e7f1fb]',
                    )}
                  >
                    {cell.day}
                  </button>
                ) : (
                  <span key={`empty-${index}`} />
                ))}
              </div>
              <div className="border-t border-[#82aedb]/55 px-2 py-2">
                <button
                  type="button"
                  onClick={() => pick(todayKey)}
                  className="flex h-8 w-full items-center justify-center rounded-lg text-sm font-bold text-[#1C4D8D] hover:bg-[#e7f1fb]"
                >
                  Hôm nay
                </button>
              </div>
            </div>,
            document.body,
          )
        : null}
    </div>
  )
}

function SliderField({
  label,
  value,
  min,
  max,
  suffix,
  disabled = false,
  invalid = false,
  onChange,
}: {
  label: string
  value: number
  min: number
  max: number
  suffix?: string
  disabled?: boolean
  invalid?: boolean
  onChange: (value: number) => void
}) {
  const clamp = (next: number) => {
    if (!Number.isFinite(next)) return min
    return Math.min(max, Math.max(min, Math.round(next)))
  }
  return (
    <div className={cn(disabled && 'opacity-45')}>
      <div className="flex items-center justify-between gap-2">
        <span className={fieldLabelClass(invalid)}>{label}</span>
        <span className="flex items-center gap-1.5">
          <input
            type="number"
            min={min}
            max={max}
            value={value}
            disabled={disabled}
            aria-label={label}
            aria-invalid={invalid || undefined}
            onChange={(event) => onChange(clamp(Number(event.target.value)))}
            className={cn(
              'h-9 w-14 rounded-lg border border-[#4988C4] bg-white px-1.5 text-center text-sm font-bold text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30 disabled:cursor-not-allowed',
              invalid && fieldInvalidClass,
            )}
          />
          {suffix ? <span className="text-xs font-semibold text-[#5b6b84]">{suffix}</span> : null}
        </span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        disabled={disabled}
        value={Math.min(max, Math.max(min, value))}
        aria-label={`${label} thanh trượt`}
        onChange={(event) => onChange(clamp(Number(event.target.value)))}
        className="mt-2 h-2 w-full cursor-pointer accent-[#1C4D8D] disabled:cursor-not-allowed"
      />
    </div>
  )
}

type FieldErrors = Partial<Record<'classId' | 'title' | 'examDate' | 'time' | 'examMinutes' | 'pool', string>>

function CreateInClassShiftDialog({
  open,
  classes,
  onClose,
  onCreated,
}: {
  open: boolean
  classes: LecturerClassOption[]
  onClose: () => void
  onCreated: (shift: InClassShift) => void
}) {
  const [step, setStep] = useState<1 | 2>(1)
  const [classId, setClassId] = useState('')
  const [title, setTitle] = useState('')
  const [examDate, setExamDate] = useState('')
  const [startClock, setStartClock] = useState('')
  const [endClock, setEndClock] = useState('')
  const [examMinutes, setExamMinutes] = useState(20)
  const [description, setDescription] = useState('')
  const [questionCount, setQuestionCount] = useState(1)
  const [hasFollowUp, setHasFollowUp] = useState(false)
  const [maxFollowUpQuestions, setMaxFollowUpQuestions] = useState(2)
  const [poolMode, setPoolMode] = useState<InClassQuestionPoolMode>('ALL_COURSE_BANK')
  const [selectedQuestionIds, setSelectedQuestionIds] = useState<string[]>([])
  const [questionOptions, setQuestionOptions] = useState<Array<{ id: string; title: string }>>([])
  const [questionsLoading, setQuestionsLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  useEffect(() => {
    if (!open) return
    // Cửa sổ ca mặc định 2 giờ — đủ thời gian cấu hình đề trước khi kích hoạt.
    const start = new Date()
    const end = new Date(start.getTime() + 2 * 60 * 60_000)
    setStep(1)
    setClassId(classes[0]?.id ?? '')
    setTitle('')
    setExamDate(toDateKey(start))
    setStartClock(toClock(start))
    setEndClock(toClock(end))
    setExamMinutes(20)
    setDescription('')
    setQuestionCount(1)
    setHasFollowUp(false)
    setMaxFollowUpQuestions(2)
    setPoolMode('ALL_COURSE_BANK')
    setSelectedQuestionIds([])
    setQuestionOptions([])
    setFieldErrors({})
    setBusy(false)
  }, [open, classes])

  const selectedCourseId = classes.find((item) => item.id === classId)?.courseId

  useEffect(() => {
    if (!open || poolMode !== 'SELECTED_POOL') return
    if (!selectedCourseId) {
      setQuestionOptions([])
      return
    }
    let cancelled = false
    setQuestionsLoading(true)
    void questionService
      .getQuestions({
        courseId: selectedCourseId,
        approvalStatus: 'APPROVED',
        pageNumber: 1,
        pageSize: 100,
      })
      .then((result) => {
        if (cancelled) return
        setQuestionOptions(result.items.map((item) => ({ id: item.id, title: item.title })))
      })
      .catch(() => {
        if (!cancelled) setQuestionOptions([])
      })
      .finally(() => {
        if (!cancelled) setQuestionsLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [open, poolMode, selectedCourseId])

  if (!open) return null

  const selectedClass = classes.find((item) => item.id === classId) ?? null
  const classOptions = classes.map((item) => ({
    value: item.id,
    label: item.name,
  }))
  const crossesMidnight = Boolean(examDate && startClock && endClock && endClock < startClock)
  const endDateKey = examDate ? (crossesMidnight ? shiftDateKey(examDate, 1) : examDate) : ''
  const startTime = examDate && startClock ? `${examDate}T${startClock}` : ''
  const endTime = endDateKey && endClock ? `${endDateKey}T${endClock}` : ''
  const startDate = startTime ? new Date(startTime) : null
  const endDate = endTime ? new Date(endTime) : null
  const timeValid = Boolean(
    startDate &&
      endDate &&
      !Number.isNaN(startDate.getTime()) &&
      !Number.isNaN(endDate.getTime()) &&
      endDate.getTime() >= startDate.getTime(),
  )
  const windowMinutes = timeValid && startDate && endDate
    ? Math.round((endDate.getTime() - startDate.getTime()) / 60_000)
    : 0
  const examMinutesValid = examMinutes >= 1 && examMinutes <= 60 && (windowMinutes === 0 || examMinutes <= windowMinutes)

  const clearFieldError = (...keys: Array<keyof FieldErrors>) => {
    setFieldErrors((current) => {
      if (!keys.some((key) => current[key])) return current
      const next = { ...current }
      for (const key of keys) delete next[key]
      return next
    })
  }

  const validateStep1 = (): FieldErrors => {
    const next: FieldErrors = {}
    const trimmedTitle = title.trim()
    if (!classId) next.classId = 'Vui lòng chọn lớp.'
    if (!trimmedTitle) next.title = 'Vui lòng nhập tên ca.'
    else if (trimmedTitle.length > 255) next.title = 'Tên ca tối đa 255 ký tự.'
    if (!examDate) next.examDate = 'Vui lòng chọn ngày kiểm tra.'
    if (!startClock || !endClock) next.time = 'Vui lòng chọn giờ mở và giờ đóng ca.'
    else if (examDate && !timeValid) next.time = 'Khung giờ ca không hợp lệ. Hãy chọn lại ngày và giờ.'
    return next
  }

  const validateStep2 = (): FieldErrors => {
    const next: FieldErrors = {}
    if (examMinutes < 1 || examMinutes > 60) {
      next.examMinutes = 'Thời gian làm bài phải từ 1 đến 60 phút.'
    } else if (examMinutes > windowMinutes) {
      next.examMinutes = `Thời gian làm bài không được dài hơn khoảng ca đang mở (${formatDuration(windowMinutes)}).`
    }
    if (poolMode === 'SELECTED_POOL' && selectedQuestionIds.length < questionCount) {
      next.pool = `Chọn ít nhất ${questionCount} câu đã duyệt (đã chọn ${selectedQuestionIds.length}).`
    }
    return next
  }

  const goNext = () => {
    if (busy) return
    const next = validateStep1()
    setFieldErrors(next)
    if (Object.keys(next).length > 0) return
    setStep(2)
  }

  const goBack = () => {
    if (busy) return
    setFieldErrors({})
    setStep(1)
  }

  const createShift = async () => {
    if (step !== 2 || busy) return
    const step1Errors = validateStep1()
    if (Object.keys(step1Errors).length > 0) {
      setFieldErrors(step1Errors)
      setStep(1)
      return
    }
    const step2Errors = validateStep2()
    if (Object.keys(step2Errors).length > 0) {
      setFieldErrors(step2Errors)
      return
    }
    if (!examDate || !startClock || !endClock) return
    const startIso = toOffsetIso(examDate, startClock)
    const endIso = toOffsetIso(endDateKey, endClock)
    const startMs = startIso ? new Date(startIso).getTime() : NaN
    const endMs = endIso ? new Date(endIso).getTime() : NaN
    if (!startIso || !endIso || Number.isNaN(startMs) || Number.isNaN(endMs)) {
      setFieldErrors({ time: 'Khung giờ ca không hợp lệ. Hãy chọn lại ngày và giờ.' })
      setStep(1)
      return
    }
    if (endMs <= Date.now()) {
      setFieldErrors({ time: 'Giờ đóng ca đã qua. Hãy chọn lại khung giờ (cửa sổ ca phải còn ở tương lai).' })
      setStep(1)
      return
    }
    if (endMs - startMs < examMinutes * 60_000) {
      setFieldErrors({ time: `Khung giờ ca phải dài hơn thời gian làm bài (${examMinutes} phút).` })
      setStep(1)
      return
    }
    setBusy(true)
    setFieldErrors({})
    const payload: CreateInClassShiftRequest = {
      classId,
      shiftName: title.trim(),
      startTime: startIso,
      endTime: endIso,
      description: description.trim(),
      questionPoolMode: poolMode,
      selectedQuestionIds: poolMode === 'SELECTED_POOL' ? selectedQuestionIds : [],
      totalQuestionsPerStudent: questionCount,
      hasFollowUp,
      maxFollowUpQuestions: hasFollowUp ? maxFollowUpQuestions : 0,
      isAutoPublishGrades: true,
      examDurationMinutes: examMinutes,
    }
    try {
      const shift = await inClassShiftService.createShift(payload)
      onCreated(shift)
    } catch (createError) {
      toast.error(getErrorMessage(createError))
      setBusy(false)
    }
  }

  const onFormSubmit = (event: FormEvent) => {
    event.preventDefault()
    // Enter trên bước 1 chỉ chuyển bước — không tạo ca / không mở OTP.
    if (step === 1) {
      goNext()
      return
    }
    void createShift()
  }

  const steps = [
    { id: 1 as const, title: 'Lớp và thời gian' },
    { id: 2 as const, title: 'Cấu trúc đề' },
  ]

  return (
    <FormDialog
      titleId="create-inclass-title"
      eyebrow="Ca kiểm tra"
      title="Tạo ca kiểm tra"
      subtitle={step === 1 ? 'Chọn lớp, tên ca và khung giờ.' : 'Chọn số câu và thời gian làm bài.'}
      icon={ClipboardList}
      onClose={onClose}
      busy={busy}
      beforeBody={
        <nav className="shrink-0 border-b border-[#82aedb]/55 bg-white px-5 py-3" aria-label="Tiến độ tạo ca">
          <ol className="relative flex w-full items-start justify-between">
            <span aria-hidden="true" className="absolute top-3.5 right-[22%] left-[22%] h-0.5 rounded-full bg-[#c5d6ea]" />
            <span
              aria-hidden="true"
              className={cn(
                'absolute top-3.5 left-[22%] h-0.5 rounded-full bg-[#1C4D8D] transition-all duration-300',
                step >= 2 ? 'w-[56%]' : 'w-0',
              )}
            />
            {steps.map((item) => {
              const visual = step > item.id ? 'done' : step === item.id ? 'current' : 'upcoming'
              return (
                <li key={item.id} className="relative z-[1] flex w-[38%] flex-col items-center">
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => {
                      if (item.id === 1) goBack()
                      else goNext()
                    }}
                    aria-current={visual === 'current' ? 'step' : undefined}
                    className={cn(
                      'flex size-7 items-center justify-center rounded-full text-xs font-black transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed',
                      visual === 'done' && 'bg-[#1C4D8D] text-white',
                      visual === 'current' && 'bg-[#1C4D8D] text-white ring-[3px] ring-[#1C4D8D]/22',
                      visual === 'upcoming' && 'border-2 border-[#82aedb]/55 bg-white text-[#1C4D8D]',
                    )}
                  >
                    {visual === 'done' ? <Check className="size-3.5 stroke-[3]" aria-hidden="true" /> : item.id}
                  </button>
                  <p className={cn('mt-2 text-center text-xs font-bold', visual === 'current' ? 'text-[#1C4D8D]' : 'text-[#5b6b84]')}>
                    {item.title}
                  </p>
                </li>
              )
            })}
          </ol>
        </nav>
      }
      footer={(
        <DialogFooter className="bg-white sm:items-center sm:justify-between">
          <button type="button" disabled={busy} onClick={onClose} className={cn(ds.outlineButton, 'min-h-10')}>
            Hủy
          </button>
          <div className="flex flex-col-reverse gap-2 sm:flex-row">
            {step === 2 && (
              <button type="button" disabled={busy} onClick={goBack} className={cn(ds.outlineButton, 'min-h-10')}>
                Quay lại
              </button>
            )}
            {step === 1 ? (
              <button
                key="create-shift-next"
                type="button"
                disabled={busy}
                onClick={goNext}
                className={cn(ds.primaryButton, 'min-h-10')}
              >
                Tiếp tục
                <ChevronRight className="size-4" />
              </button>
            ) : (
              <button
                key="create-shift-submit"
                type="button"
                disabled={busy}
                onClick={() => void createShift()}
                className={cn(ds.primaryButton, 'min-h-10')}
              >
                {busy ? <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" /> : <Plus className="size-3.5" />}
                {busy ? 'Đang tạo...' : 'Tạo ca'}
              </button>
            )}
          </div>
        </DialogFooter>
      )}
    >
      <form id="create-inclass-form" onSubmit={onFormSubmit} className="space-y-3">
            {step === 1 ? (
              <section className="space-y-3">
                <label className="block">
                  <span className={fieldLabelClass(Boolean(fieldErrors.classId))}>Lớp</span>
                  <FilterDropMenu
                    label="Chọn lớp"
                    value={classId}
                    onChange={(value) => {
                      setClassId(value)
                      clearFieldError('classId')
                    }}
                    options={classOptions}
                    emptyText="Chưa có lớp để chọn"
                    className="mt-1.5 w-full"
                    invalid={Boolean(fieldErrors.classId)}
                  />
                  <FieldError message={fieldErrors.classId} />
                </label>
                <label className="block">
                  <span className={fieldLabelClass(Boolean(fieldErrors.title))}>Tên ca</span>
                  <input
                    value={title}
                    onChange={(event) => {
                      setTitle(event.target.value)
                      clearFieldError('title')
                    }}
                    maxLength={255}
                    placeholder="Ví dụ: Kiểm tra tiến độ 1"
                    aria-invalid={Boolean(fieldErrors.title)}
                    className={cn(ds.field, 'mt-1.5', fieldErrors.title && fieldInvalidClass)}
                    autoFocus
                  />
                  <FieldError message={fieldErrors.title} />
                </label>
                <div>
                  <DateField
                    label="Ngày kiểm tra"
                    value={examDate}
                    invalid={Boolean(fieldErrors.examDate)}
                    onChange={(value) => {
                      setExamDate(value)
                      clearFieldError('examDate', 'time', 'examMinutes')
                    }}
                  />
                  <FieldError message={fieldErrors.examDate} />
                </div>
                <div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <ClockField
                      label="Ca mở"
                      value={startClock}
                      invalid={Boolean(fieldErrors.time)}
                      onChange={(value) => {
                        setStartClock(value)
                        clearFieldError('time', 'examMinutes')
                      }}
                    />
                    <ClockField
                      label="Ca đóng"
                      value={endClock}
                      invalid={Boolean(fieldErrors.time)}
                      onChange={(value) => {
                        setEndClock(value)
                        clearFieldError('time', 'examMinutes')
                      }}
                    />
                  </div>
                  <FieldError message={fieldErrors.time} />
                </div>
                {fieldErrors.time ? null : crossesMidnight ? (
                  <p className="text-xs font-semibold text-[#5b6b84]">Ca đóng vào ngày hôm sau.</p>
                ) : null}
              </section>
            ) : (
              <>
                <div className={cn('flex items-center gap-3 rounded-2xl border border-[#82aedb]/55 bg-white px-3 py-2.5', ds.panelShadow)}>
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#e7f1fb] text-[#1C4D8D]">
                    <UsersRound className="size-4" aria-hidden="true" />
                  </span>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-bold text-[#0F2854]">{title.trim() || 'Chưa đặt tên'}</p>
                    <p className="truncate text-xs font-semibold text-[#5b6b84]">
                      {selectedClass?.name || 'Chưa chọn lớp'}
                      {examDate ? ` · ${examDate.split('-').reverse().join('/')}` : ''}
                      {startClock && endClock ? ` · Ca ${startClock}–${endClock}` : ''}
                      {examMinutesValid ? ` · Làm bài ${formatDuration(examMinutes)}` : ''}
                    </p>
                  </div>
                </div>

                <section className={cn('space-y-4 rounded-2xl border border-[#82aedb]/55 bg-white p-3.5', ds.panelShadow)}>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <SliderField
                      label="Thời gian"
                      value={examMinutes}
                      min={1}
                      max={60}
                      suffix="phút"
                      invalid={Boolean(fieldErrors.examMinutes)}
                      onChange={(value) => {
                        setExamMinutes(value)
                        clearFieldError('examMinutes')
                      }}
                    />
                    <SliderField
                      label="Số câu chính"
                      value={questionCount}
                      min={1}
                      max={10}
                      suffix="câu"
                      onChange={(value) => {
                        setQuestionCount(value)
                        clearFieldError('pool')
                      }}
                    />
                  </div>
                  <FieldError message={fieldErrors.examMinutes} className="-mt-2" />
                  <div className="grid items-center gap-4 sm:grid-cols-2">
                    <div className="flex items-center justify-between gap-3 rounded-xl border border-[#82aedb]/55 bg-[#F8FBFD] px-3 py-2.5">
                      <span>
                        <span className="block text-sm font-bold text-[#0F2854]">Câu hỏi phụ</span>
                        <span className="text-xs text-[#5b6b84]">Hỏi thêm sau câu chính</span>
                      </span>
                      <button
                        type="button"
                        role="switch"
                        aria-checked={hasFollowUp}
                        aria-label="Câu hỏi phụ"
                        onClick={() => {
                          setHasFollowUp((current) => {
                            if (!current) setMaxFollowUpQuestions((count) => Math.min(3, Math.max(1, count || 1)))
                            return !current
                          })
                        }}
                        className={cn('relative h-6 w-11 shrink-0 rounded-full transition', hasFollowUp ? 'bg-[#1C4D8D]' : 'bg-[#c5d0e0]')}
                      >
                        <span className={cn('absolute top-0.5 size-5 rounded-full bg-white shadow transition', hasFollowUp ? 'left-[22px]' : 'left-0.5')} />
                      </button>
                    </div>
                    <SliderField
                      label="Số câu phụ"
                      value={Math.min(3, Math.max(1, maxFollowUpQuestions || 1))}
                      min={1}
                      max={3}
                      suffix="câu"
                      disabled={!hasFollowUp}
                      onChange={setMaxFollowUpQuestions}
                    />
                  </div>
                  <label className="block">
                    <span className="text-xs font-bold tracking-[0.04em] text-[#5b6b84] uppercase">Mô tả</span>
                    <textarea
                      value={description}
                      onChange={(event) => setDescription(event.target.value)}
                      rows={2}
                      placeholder="Ví dụ: Nội dung chương 1 đến chương 3"
                      className="mt-1.5 min-h-16 w-full resize-y rounded-xl border border-[#4988C4] bg-white px-3 py-2.5 text-sm font-semibold text-[#0F2854] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:ring-2 focus:ring-[#4988C4]/30"
                    />
                  </label>
                  <div className="block">
                    <span className="text-xs font-bold tracking-[0.04em] text-[#5b6b84] uppercase">Nguồn câu hỏi</span>
                    <FilterDropMenu
                      label="Chọn nguồn câu hỏi"
                      value={poolMode}
                      onChange={(value) => {
                        setPoolMode(value as InClassQuestionPoolMode)
                        clearFieldError('pool')
                      }}
                      options={[
                        { value: 'ALL_COURSE_BANK', label: 'Cả ngân hàng môn' },
                        { value: 'SELECTED_POOL', label: 'Chọn bộ câu' },
                      ]}
                      className="mt-1.5 w-full"
                    />
                  </div>
                  {poolMode === 'SELECTED_POOL' && (
                    <div
                      className={cn(
                        'rounded-xl border border-[#82aedb]/55 bg-[#F8FBFD] p-3',
                        fieldErrors.pool && fieldInvalidClass,
                      )}
                    >
                      <p className="text-xs font-bold text-[#5b6b84]">
                        Chọn ít nhất {questionCount} câu đã duyệt
                        {selectedQuestionIds.length > 0 ? ` · đã chọn ${selectedQuestionIds.length}` : ''}
                      </p>
                      {questionsLoading ? (
                        <p className="mt-2 text-xs text-[#5b6b84]">Đang tải câu hỏi...</p>
                      ) : questionOptions.length === 0 ? (
                        <p className="mt-2 text-xs text-[#5b6b84]">Không có câu đã duyệt cho môn của lớp này.</p>
                      ) : (
                        <ul className="mt-2 max-h-40 space-y-1 overflow-y-auto">
                          {questionOptions.map((question) => {
                            const checked = selectedQuestionIds.includes(question.id)
                            return (
                              <li key={question.id}>
                                <label className="flex items-start gap-2 rounded-lg px-1 py-1 text-sm text-[#0F2854]">
                                  <input
                                    type="checkbox"
                                    checked={checked}
                                    onChange={() => {
                                      setSelectedQuestionIds((current) =>
                                        checked ? current.filter((id) => id !== question.id) : [...current, question.id],
                                      )
                                      clearFieldError('pool')
                                    }}
                                    className="mt-1 size-4 accent-[#1C4D8D]"
                                  />
                                  <span>{question.title}</span>
                                </label>
                              </li>
                            )
                          })}
                        </ul>
                      )}
                      <FieldError message={fieldErrors.pool} />
                    </div>
                  )}
                </section>
              </>
            )}
      </form>
    </FormDialog>
  )
}

export function InClassShiftHubPage() {
  const [classes, setClasses] = useState<LecturerClassOption[]>([])
  const [shifts, setShifts] = useState<InClassShift[]>([])
  const [classFilter, setClassFilter] = useState('ALL')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('ALL')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [projectorShiftId, setProjectorShiftId] = useState<string | null>(null)
  const [activatingId, setActivatingId] = useState<string | null>(null)
  const [closingShift, setClosingShift] = useState<InClassShift | null>(null)
  const [closing, setClosing] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let cancelled = false
    const load = async () => {
      setLoading(true)
      setError('')
      try {
        const [classResult, shiftResult] = await Promise.all([
          inClassShiftService.getLecturerClasses(),
          inClassShiftService.listShifts(),
        ])
        if (cancelled) return
        setClasses(classResult)
        setShifts(shiftResult)
      } catch (loadError) {
        if (!cancelled) setError(getErrorMessage(loadError))
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [reloadKey])

  useEffect(() => {
    const timer = window.setInterval(() => {
      void inClassShiftService.listShifts().then(setShifts).catch(() => undefined)
    }, 15000)
    return () => window.clearInterval(timer)
  }, [])

  const classOptions = useMemo(() => {
    if (classes.length === 0) return []
    return [
      { value: 'ALL', label: 'Tất cả lớp' },
      ...classes.map((item) => ({ value: item.id, label: item.name })),
    ]
  }, [classes])

  const classScopedShifts = useMemo(() => {
    if (classFilter === 'ALL') return shifts
    return shifts.filter((shift) => shift.classId === classFilter)
  }, [shifts, classFilter])

  const statusCounts = useMemo(() => {
    const counts: Record<StatusFilter, number> = {
      ALL: classScopedShifts.length,
      SCHEDULED: 0,
      ACTIVE: 0,
      CLOSED: 0,
      FINALIZED: 0,
    }
    for (const shift of classScopedShifts) {
      counts[shift.status] += 1
    }
    return counts
  }, [classScopedShifts])

  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('vi-VN')
    return classScopedShifts.filter((shift) => {
      if (statusFilter !== 'ALL' && shift.status !== statusFilter) return false
      if (!query) return true
      return (
        shift.title.toLocaleLowerCase('vi-VN').includes(query) ||
        shift.className.toLocaleLowerCase('vi-VN').includes(query) ||
        shift.courseCode.toLocaleLowerCase('vi-VN').includes(query)
      )
    })
  }, [classScopedShifts, statusFilter, search])

  const activate = async (shift: InClassShift) => {
    setActivatingId(shift.id)
    setError('')
    setNotice('')
    try {
      await inClassShiftService.activateShift(shift.id)
      setShifts((current) =>
        current.map((item) => (item.id === shift.id ? { ...item, status: 'ACTIVE' } : item)),
      )
      setNotice(`Đã kích hoạt “${shift.title}”. Có thể chiếu mã OTP cho sinh viên.`)
    } catch (activateError) {
      setError(getErrorMessage(activateError))
    } finally {
      setActivatingId(null)
    }
  }

  const confirmClose = async () => {
    if (!closingShift) return
    setClosing(true)
    setError('')
    setNotice('')
    try {
      await inClassShiftService.closeShift(closingShift.id)
      setShifts((current) =>
        current.map((item) => (item.id === closingShift.id ? { ...item, status: 'CLOSED' } : item)),
      )
      if (projectorShiftId === closingShift.id) setProjectorShiftId(null)
      setNotice(`Đã đóng ca “${closingShift.title}”. Sinh viên không vào ca hoặc nộp bài được nữa.`)
      setClosingShift(null)
    } catch (closeError) {
      setError(getErrorMessage(closeError))
    } finally {
      setClosing(false)
    }
  }

  return (
    <div lang="vi" className={cn(ds.page, 'pb-8')}>
      <InClassShiftPageBanner />

      {(error || notice) && (
        <div
          className={cn(
            'flex items-center justify-between gap-3 rounded-2xl border px-4 py-3 text-sm',
            error
              ? 'border-[#f3c8cf] bg-[#fff5f6] text-[#b33d52]'
              : 'border-[#bcebdc] bg-[#effbf6] text-[#16785f]',
          )}
          role={error ? 'alert' : 'status'}
        >
          <span className="flex items-center gap-2 font-medium">
            {error ? (
              <AlertCircle className="size-4 shrink-0" />
            ) : (
              <CheckCircle2 className="size-4 shrink-0" />
            )}
            {error || notice}
          </span>
          <button
            type="button"
            onClick={() => {
              setError('')
              setNotice('')
            }}
            className="text-xs font-bold underline-offset-2 hover:underline"
          >
            Đóng
          </button>
        </div>
      )}

      <section className={cn(ds.section, 'overflow-hidden')}>
        <div className={cn(ds.sectionHeader, 'flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between')}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <ClipboardList className={ds.sectionIcon} />
                </span>
                <h2 className={ds.sectionTitle}>Các ca kiểm tra</h2>
                <span className={ds.sectionBadge}>
                  {loading ? '…' : `${filtered.length} ca`}
                </span>
              </div>
              <p className={ds.sectionHint}>Theo dõi các ca kiểm tra trên lớp</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <FilterDropMenu
                label="Lớp học phần"
                value={classFilter}
                onChange={setClassFilter}
                options={classOptions}
                emptyText="Chưa có lớp"
                className="w-56 shrink-0"
              />
              <div className="relative shrink-0">
                <Search className="pointer-events-none absolute top-1/2 left-3.5 size-3.5 -translate-y-1/2 text-[#5b6b84]" />
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Tìm theo tên ca..."
                  className="h-10 w-56 rounded-full border border-[#4988C4] bg-white py-0 pr-4 pl-9 text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:border-[#4988C4] focus:ring-2 focus:ring-[#1C4D8D]/15"
                />
              </div>
              <button
                type="button"
                onClick={() => setReloadKey((key) => key + 1)}
                disabled={loading}
                className={ds.outlineButton}
              >
                <RefreshCw className={cn('size-3.5', loading && 'animate-spin')} />
                Làm mới
              </button>
              <button
                type="button"
                onClick={() => setCreateOpen(true)}
                className={cn(ds.primaryButton, 'min-h-10')}
              >
                <Plus className="size-3.5" />
                Tạo ca kiểm tra
              </button>
            </div>
        </div>

        <div className="bg-white">
          <div className="px-4 pt-3 sm:px-6">
            <div
              className="flex flex-wrap items-end gap-0 border-b-2 border-[#1C4D8D]"
              role="tablist"
              aria-label="Lọc trạng thái ca"
            >
              {STATUS_FILTERS.map((tab) => {
                const count = statusCounts[tab.value]
                const active = statusFilter === tab.value
                return (
                  <button
                    key={tab.value}
                    type="button"
                    role="tab"
                    aria-selected={active}
                    onClick={() => setStatusFilter(tab.value)}
                    className={cn(
                      'relative inline-flex items-center gap-1.5 border px-3.5 py-2.5 text-[14px] font-bold whitespace-nowrap transition sm:px-4',
                      active
                        ? '-mb-0.5 rounded-t-xl border-[#1C4D8D] border-b-white bg-[linear-gradient(165deg,#BDE8F5_0%,#EAF6FB_42%,#ffffff_100%)] text-[#1C4D8D]'
                        : 'mb-0 border-transparent text-[#6f7f93] hover:bg-[#F4FBFD] hover:text-[#1C4D8D]',
                    )}
                  >
                    <span
                      className={cn(
                        'size-1.5 rounded-full',
                        active ? 'bg-[#1C4D8D]' : tab.dotClass,
                      )}
                      aria-hidden="true"
                    />
                    <span>{tab.label}</span>
                    <span
                      className={cn(
                        'rounded-full px-1.5 py-0.5 text-[12px] font-bold tabular-nums',
                        active ? 'bg-[#1C4D8D]/12 text-[#1C4D8D]' : tab.countIdleClass,
                      )}
                    >
                      {count}
                    </span>
                  </button>
                )
              })}
            </div>
          </div>
        </div>

        <div className={cn(ds.sectionBody, 'border-t-0 pt-4')}>
          {loading ? (
            <div className="flex min-h-40 flex-col items-center justify-center gap-3 text-sm text-[#5b6b84]">
              <LoaderCircle className="size-7 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
              Đang tải danh sách ca...
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={UsersRound}
              title="Chưa có ca phù hợp"
              description="Tạo Progress Test / Quiz cho lớp đang dạy để sinh viên vào bằng mã OTP chiếu trên lớp."
              action={
                <button type="button" onClick={() => setCreateOpen(true)} className={ds.primaryButton}>
                  <Plus className="size-4" />
                  Tạo ca kiểm tra
                </button>
              }
            />
          ) : (
            <ul className="divide-y divide-[#4988C4]/25 overflow-hidden rounded-2xl border border-[#82aedb]/55">
              {filtered.map((shift) => {
                const start = formatShiftPoint(shift.startTime)
                const end = formatShiftPoint(shift.endTime)
                const endLabel = start && end && toDateKey(end) !== toDateKey(start)
                  ? `${formatShiftDate(end)} ${formatShiftClock(end)}`
                  : end
                    ? formatShiftClock(end)
                    : ''
                const timeRange = start && endLabel
                  ? `${formatShiftClock(start)} – ${endLabel}`
                  : start
                    ? formatShiftClock(start)
                    : ''
                const factClass =
                  'inline-flex items-center gap-1.5 rounded-lg bg-[#D1EFF8] px-2.5 py-1.5 text-xs font-semibold text-[#1C4D8D]'
                return (
                <li
                  key={shift.id}
                  className="flex flex-col gap-3 bg-white px-4 py-4 transition hover:bg-[#F8FBFD] sm:flex-row sm:items-center sm:justify-between sm:px-5"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      {statusChip(shift.status)}
                      <span className="rounded-full border border-[#82aedb]/55 bg-[#F4FBFD] px-2.5 py-0.5 text-[11px] font-bold text-[#1C4D8D]">
                        {shift.className}
                      </span>
                    </div>
                    <h3 className="mt-2 truncate text-xl font-bold tracking-[-0.02em] text-[#0F2854]">
                      {shift.title}
                    </h3>
                    <div className="mt-2 flex flex-wrap items-center gap-1.5">
                      <span className={factClass}>
                        <Clock3 className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        {start ? (
                          <>
                            <span className="font-bold text-[#0F2854]">{formatShiftDate(start)}</span>
                            {timeRange ? <span>{timeRange}</span> : null}
                          </>
                        ) : (
                          'Chưa có giờ'
                        )}
                      </span>
                      {shift.examDurationMinutes != null && (
                        <span className={factClass}>
                          <Timer className="size-3.5 shrink-0 text-[#1C4D8D]" />
                          <span>
                            Làm bài{' '}
                            <span className="font-bold text-[#0F2854]">
                              {formatDuration(shift.examDurationMinutes)}
                            </span>
                          </span>
                        </span>
                      )}
                      <span className={factClass}>
                        <ClipboardList className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        <span>
                          <span className="font-bold text-[#0F2854]">{shift.questionCount}</span> câu
                          {shift.hasFollowUp ? (
                            <>
                              {' · phụ '}
                              <span className="font-bold text-[#0F2854]">
                                {shift.maxFollowUpQuestions || 1}
                              </span>
                            </>
                          ) : null}
                        </span>
                      </span>
                      <span className={factClass}>
                        <UsersRound className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        <span>
                          <span className="font-bold text-[#0F2854]">{shift.joinedCount}</span>
                          {' vào ca · '}
                          <span className="font-bold text-[#0F2854]">{shift.submittedCount}</span>
                          {' đã nộp'}
                        </span>
                      </span>
                    </div>
                  </div>
                  <div className="flex flex-wrap items-center gap-2 sm:shrink-0">
                    {shift.status === 'SCHEDULED' && (
                      <button
                        type="button"
                        disabled={activatingId === shift.id}
                        onClick={() => void activate(shift)}
                        className={cn(ds.primaryButton, 'min-h-10')}
                      >
                        {activatingId === shift.id ? (
                          <LoaderCircle className="size-3.5 animate-spin" />
                        ) : (
                          <CheckCircle2 className="size-3.5" />
                        )}
                        Kích hoạt
                      </button>
                    )}
                    {shift.status === 'ACTIVE' && (
                      <button
                        type="button"
                        onClick={() => setProjectorShiftId(shift.id)}
                        className={cn(ds.primaryButton, 'min-h-10')}
                      >
                        <Presentation className="size-3.5" />
                        Chiếu mã
                      </button>
                    )}
                    {(shift.status === 'SCHEDULED' || shift.status === 'ACTIVE') && (
                      <button
                        type="button"
                        onClick={() => setClosingShift(shift)}
                        className={cn(ds.outlineButton, 'min-h-10')}
                      >
                        <CircleStop className="size-3.5" />
                        Đóng ca
                      </button>
                    )}
                    <Link
                      to={`/in-class-shifts/${shift.id}/results`}
                      className={cn(ds.outlineButton, 'min-h-10')}
                    >
                      {shift.status === 'FINALIZED' ? 'Xem điểm' : 'Bảng điểm'}
                      <ChevronRight className="size-3.5" />
                    </Link>
                  </div>
                </li>
                )
              })}
            </ul>
          )}
        </div>
      </section>

      <CreateInClassShiftDialog
        open={createOpen}
        classes={classes}
        onClose={() => setCreateOpen(false)}
        onCreated={(shift) => {
          setCreateOpen(false)
          setNotice(`Đã tạo ca “${shift.title}”. Bấm Kích hoạt khi bắt đầu giờ thi, rồi chiếu mã OTP.`)
          setShifts((current) => [shift, ...current.filter((item) => item.id !== shift.id)])
        }}
      />

      <InClassShiftProjectorDialog
        open={Boolean(projectorShiftId)}
        shiftId={projectorShiftId}
        onClose={() => setProjectorShiftId(null)}
      />

      {closingShift && (
        <ConfirmDialog
          tone="warning"
          title="Đóng ca kiểm tra"
          message={`Đóng “${closingShift.title}” ngay. Sinh viên không vào ca hoặc nộp bài được nữa. Bảng điểm vẫn mở để chốt điểm.`}
          confirmLabel="Đóng ca"
          cancelLabel="Hủy"
          pending={closing}
          onConfirm={() => void confirmClose()}
          onCancel={() => {
            if (!closing) setClosingShift(null)
          }}
        />
      )}
    </div>
  )
}
