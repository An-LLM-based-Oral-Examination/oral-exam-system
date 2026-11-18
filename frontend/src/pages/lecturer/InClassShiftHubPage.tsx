import { StatusTabs } from '@/components/common/StatusTabs'
import { SearchField } from '@/components/common/SearchField'
import { useSearchInput } from '@/hooks/useSearchInput'
import { TwoStepProgress } from '@/design/TwoStepProgress'
import { useEffect, useLayoutEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { Link, useSearchParams } from 'react-router-dom'
import {
  AlertCircle,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CircleStop,
  ClipboardList,
  Clock3,
  Layers,
  LoaderCircle,
  Lock,
  Minus,
  Plus,
  Presentation,
  RefreshCw,
  Timer,
  UsersRound,
} from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { toast } from '@/components/common/toastStore'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { DialogFooter, FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import { userMessage } from '@/lib/userMessage'
import { inClassShiftService } from '@/services/inClassShift.service'
import { InClassShiftProjectorDialog } from '@/pages/lecturer/InClassShiftProjectorDialog'
import {
  IN_CLASS_DIFFICULTIES,
  evaluateInClassPoolSelection,
  formatDifficultyListLabel,
  formatPoolModeLabel,
  sumCountsForDifficulties,
} from '@/utils/inClassShiftPool'
import type {
  CreateInClassShiftRequest,
  InClassCreatePoolMode,
  InClassDifficulty,
  InClassShift,
  InClassShiftPoolStats,
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
  return userMessage(error, 'Không tải được dữ liệu ca thi trên lớp. Vui lòng thử lại.')
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
      return scheduleUi(() => setBox(null))
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
    const cancelPlace = scheduleUi(place)
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      cancelPlace()
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
    return scheduleUi(() => setCursor(new Date(date.getFullYear(), date.getMonth(), 1)))
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
      return scheduleUi(() => setBox(null))
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
    const cancelPlace = scheduleUi(place)
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      cancelPlace()
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

type FieldErrors = Partial<
  Record<
    | 'classId'
    | 'title'
    | 'startDateKey'
    | 'endDateKey'
    | 'time'
    | 'examMinutes'
    | 'questionCount'
    | 'difficulties'
    | 'pool',
    string
  >
>

interface ProblemAlertState {
  title: string
  detail: string
  fieldMessages: string[]
}

function parseProblemAlert(error: unknown): ProblemAlertState {
  if (typeof error === 'object' && error !== null) {
    const rec = error as Record<string, unknown>
    const title =
      typeof rec.title === 'string' && rec.title.trim()
        ? rec.title.trim()
        : 'Không thể tạo ca kiểm tra'
    const detail = userMessage(error, 'Dữ liệu cấu hình ca chưa hợp lệ. Vui lòng kiểm tra lại.')
    const fieldMessages: string[] = []
    if (typeof rec.errors === 'object' && rec.errors !== null && !Array.isArray(rec.errors)) {
      for (const value of Object.values(rec.errors as Record<string, unknown>)) {
        if (Array.isArray(value)) {
          for (const msg of value) {
            if (typeof msg === 'string' && msg.trim()) fieldMessages.push(msg.trim())
          }
        }
      }
    }
    return { title, detail, fieldMessages }
  }
  return {
    title: 'Không thể tạo ca kiểm tra',
    detail: userMessage(error, 'Vui lòng kiểm tra kết nối và thử lại.'),
    fieldMessages: [],
  }
}

function CreateInClassShiftDialog({
  open,
  classes,
  fixedClassId,
  onClose,
  onCreated,
}: {
  open: boolean
  classes: LecturerClassOption[]
  fixedClassId?: string
  onClose: () => void
  onCreated: (shift: InClassShift) => void
}) {
  const [step, setStep] = useState<1 | 2>(1)
  const [classId, setClassId] = useState('')
  const [title, setTitle] = useState('')
  const [startDateKey, setStartDateKey] = useState('')
  const [endDateKey, setEndDateKey] = useState('')
  const [startClock, setStartClock] = useState('')
  const [endClock, setEndClock] = useState('')
  const [examMinutes, setExamMinutes] = useState(20)
  const [description, setDescription] = useState('')
  const [questionCountInput, setQuestionCountInput] = useState('2')
  const [hasFollowUp, setHasFollowUp] = useState(false)
  const [maxFollowUpQuestions, setMaxFollowUpQuestions] = useState(2)
  const [poolMode, setPoolMode] = useState<InClassCreatePoolMode>('ALL_COURSE_BANK')
  const [selectedDifficulties, setSelectedDifficulties] = useState<InClassDifficulty[]>([
    'easy',
    'medium',
    'hard',
  ])
  const [selectedCloIds, setSelectedCloIds] = useState<string[]>([])
  const [poolStats, setPoolStats] = useState<InClassShiftPoolStats | null>(null)
  const [poolLoading, setPoolLoading] = useState(false)
  const [poolLoadError, setPoolLoadError] = useState<ProblemAlertState | null>(null)
  const [poolReloadTick, setPoolReloadTick] = useState(0)
  const [submitProblem, setSubmitProblem] = useState<ProblemAlertState | null>(null)
  const [busy, setBusy] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  useEffect(() => {
    if (!open) return
    const start = new Date()
    const end = new Date(start.getTime() + 2 * 60 * 60_000)
    return scheduleUi(() => {
      setStep(1)
      setClassId(fixedClassId || classes[0]?.id || '')
      setTitle('')
      setStartDateKey(toDateKey(start))
      setEndDateKey(toDateKey(end))
      setStartClock(toClock(start))
      setEndClock(toClock(end))
      setExamMinutes(20)
      setDescription('')
      setQuestionCountInput('2')
      setHasFollowUp(false)
      setMaxFollowUpQuestions(2)
      setPoolMode('ALL_COURSE_BANK')
      setSelectedDifficulties(['easy', 'medium', 'hard'])
      setSelectedCloIds([])
      setPoolStats(null)
      setPoolLoadError(null)
      setSubmitProblem(null)
      setFieldErrors({})
      setBusy(false)
    })
  }, [open, classes, fixedClassId])

  const selectedClass = useMemo(
    () => classes.find((item) => item.id === classId) ?? null,
    [classes, classId],
  )
  const selectedCourseId = selectedClass?.courseId

  useEffect(() => {
    if (!open || !classId) {
      return scheduleUi(() => {
        setPoolStats(null)
        setPoolLoadError(null)
        setPoolLoading(false)
      })
    }
    let cancelled = false
    const cancelStart = scheduleUi(() => {
      setPoolLoading(true)
      setPoolLoadError(null)
      void inClassShiftService
        .getPoolStats(classId, selectedCourseId)
        .then((stats) => {
          if (cancelled) return
          setPoolStats(stats)
          const validCloSet = new Set(stats.clos.map((item) => item.cloId))
          setSelectedCloIds((current) => current.filter((id) => validCloSet.has(id)))
          const nonZeroDiffs = IN_CLASS_DIFFICULTIES.map((item) => item.value).filter(
            (diff) => (stats.courseBankCounts[diff] || 0) > 0,
          )
          if (nonZeroDiffs.length > 0) {
            setSelectedDifficulties((current) => {
              const filtered = current.filter((diff) => (stats.courseBankCounts[diff] || 0) > 0)
              return filtered.length > 0 ? filtered : nonZeroDiffs
            })
          }
        })
        .catch((loadErr) => {
          if (cancelled) return
          setPoolStats(null)
          setPoolLoadError(parseProblemAlert(loadErr))
        })
        .finally(() => {
          if (!cancelled) setPoolLoading(false)
        })
    })
    return () => {
      cancelled = true
      cancelStart()
    }
  }, [open, classId, selectedCourseId, poolReloadTick])

  const parsedQuestionCount = useMemo(() => {
    const trimmed = questionCountInput.trim()
    if (!trimmed) return Number.NaN
    const num = Number(trimmed)
    return Number.isFinite(num) ? num : Number.NaN
  }, [questionCountInput])

  const poolEvaluation = useMemo(
    () =>
      evaluateInClassPoolSelection({
        poolStats,
        poolMode,
        totalQuestionsPerStudent: parsedQuestionCount,
        selectedDifficulties,
        selectedCloIds,
      }),
    [poolStats, poolMode, parsedQuestionCount, selectedDifficulties, selectedCloIds],
  )

  const difficultySourceCounts = useMemo(() => {
    if (!poolStats) return { easy: 0, medium: 0, hard: 0 }
    if (poolMode === 'ALL_COURSE_BANK') {
      return {
        easy: poolStats.courseBankCounts.easy,
        medium: poolStats.courseBankCounts.medium,
        hard: poolStats.courseBankCounts.hard,
      }
    }
    const chosen = poolStats.clos.filter((clo) => selectedCloIds.includes(clo.cloId))
    return chosen.reduce(
      (acc, clo) => ({
        easy: acc.easy + (clo.counts.easy || 0),
        medium: acc.medium + (clo.counts.medium || 0),
        hard: acc.hard + (clo.counts.hard || 0),
      }),
      { easy: 0, medium: 0, hard: 0 },
    )
  }, [poolStats, poolMode, selectedCloIds])

  if (!open) return null

  const classOptions = classes.map((item) => ({
    value: item.id,
    label: item.courseCode ? `${item.name} (${item.courseCode})` : item.name,
  }))
  const startTime = startDateKey && startClock ? `${startDateKey}T${startClock}` : ''
  const endTime = endDateKey && endClock ? `${endDateKey}T${endClock}` : ''
  const startDate = startTime ? new Date(startTime) : null
  const endDate = endTime ? new Date(endTime) : null
  const timeValid = Boolean(
    startDate &&
      endDate &&
      !Number.isNaN(startDate.getTime()) &&
      !Number.isNaN(endDate.getTime()) &&
      endDate.getTime() > startDate.getTime(),
  )
  const windowMinutes =
    timeValid && startDate && endDate
      ? Math.round((endDate.getTime() - startDate.getTime()) / 60_000)
      : 0
  const examMinutesValid =
    examMinutes >= 1 && examMinutes <= 60 && (windowMinutes === 0 || examMinutes <= windowMinutes)

  const clearFieldError = (...keys: Array<keyof FieldErrors>) => {
    setSubmitProblem(null)
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
    if (!classId) next.classId = 'Vui lòng chọn lớp học phần.'
    if (!trimmedTitle) next.title = 'Vui lòng nhập tên ca kiểm tra.'
    else if (trimmedTitle.length > 150) next.title = 'Tên ca tối đa 150 ký tự.'
    if (!startDateKey) next.startDateKey = 'Vui lòng chọn ngày mở ca.'
    if (!endDateKey) next.endDateKey = 'Vui lòng chọn ngày đóng ca.'
    if (!startClock || !endClock) next.time = 'Vui lòng chọn giờ mở và giờ đóng ca.'
    else if (startDateKey && endDateKey && !timeValid) {
      next.time = 'Thời điểm đóng ca phải sau thời điểm mở ca.'
    }
    return next
  }

  const validateStep2 = (): FieldErrors => {
    const next: FieldErrors = {}
    if (examMinutes < 1 || examMinutes > 60) {
      next.examMinutes = 'Thời gian làm bài phải từ 1 đến 60 phút.'
    } else if (windowMinutes > 0 && examMinutes > windowMinutes) {
      next.examMinutes = `Thời gian làm bài không được dài hơn khoảng ca đang mở (${formatDuration(windowMinutes)}).`
    }
    if (
      !Number.isInteger(parsedQuestionCount) ||
      parsedQuestionCount < 1 ||
      parsedQuestionCount > 10
    ) {
      next.questionCount = 'Số câu mỗi sinh viên phải là số nguyên từ 1 đến 10.'
    }
    if (selectedDifficulties.length === 0) {
      next.difficulties = 'Vui lòng chọn ít nhất một mức độ câu hỏi (Dễ, Trung bình hoặc Khó).'
    }
    if (poolLoadError) {
      next.pool = 'Chưa tải được thống kê ngân hàng câu hỏi của môn. Hãy tải lại trước khi tạo ca.'
    } else if (!poolStats) {
      next.pool = 'Đang kiểm tra ngân hàng câu hỏi hợp lệ. Vui lòng đợi trong giây lát.'
    } else if (!poolEvaluation.isValid && poolEvaluation.errors.length > 0) {
      const poolSpecific = poolEvaluation.errors.find(
        (msg) =>
          !msg.startsWith('Số câu mỗi sinh viên') && !msg.startsWith('Vui lòng chọn ít nhất một mức độ'),
      )
      if (poolSpecific) next.pool = poolSpecific
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
    setSubmitProblem(null)
    setStep(1)
  }

  const toggleDifficulty = (diff: InClassDifficulty) => {
    setSelectedDifficulties((current) => {
      const exists = current.includes(diff)
      const updated = exists ? current.filter((item) => item !== diff) : [...current, diff]
      return IN_CLASS_DIFFICULTIES.map((item) => item.value).filter((val) => updated.includes(val))
    })
    clearFieldError('difficulties', 'pool')
  }

  const toggleClo = (cloId: string) => {
    setSelectedCloIds((current) =>
      current.includes(cloId) ? current.filter((id) => id !== cloId) : [...current, cloId],
    )
    clearFieldError('pool')
  }

  const stepQuestionCount = (delta: number) => {
    const base = Number.isInteger(parsedQuestionCount) ? parsedQuestionCount : 2
    const next = Math.min(10, Math.max(1, base + delta))
    setQuestionCountInput(String(next))
    clearFieldError('questionCount', 'pool')
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
    if (!startDateKey || !endDateKey || !startClock || !endClock) return
    const startIso = toOffsetIso(startDateKey, startClock)
    const endIso = toOffsetIso(endDateKey, endClock)
    const startMs = startIso ? new Date(startIso).getTime() : NaN
    const endMs = endIso ? new Date(endIso).getTime() : NaN
    if (!startIso || !endIso || Number.isNaN(startMs) || Number.isNaN(endMs)) {
      setFieldErrors({ time: 'Khung giờ ca không hợp lệ. Hãy chọn lại ngày và giờ.' })
      setStep(1)
      return
    }
    if (endMs <= Date.now()) {
      setFieldErrors({
        time: 'Giờ đóng ca đã qua. Hãy chọn lại khung giờ (cửa sổ ca phải còn ở tương lai).',
      })
      setStep(1)
      return
    }
    if (endMs - startMs < examMinutes * 60_000) {
      setFieldErrors({
        time: `Khung giờ ca phải dài hơn hoặc bằng thời gian làm bài (${examMinutes} phút).`,
      })
      setStep(1)
      return
    }

    setBusy(true)
    setFieldErrors({})
    setSubmitProblem(null)

    const orderedDifficulties = IN_CLASS_DIFFICULTIES.map((item) => item.value).filter((diff) =>
      selectedDifficulties.includes(diff),
    )

    const payload: CreateInClassShiftRequest = {
      classId,
      shiftName: title.trim(),
      startTime: startIso,
      endTime: endIso,
      description: description.trim(),
      questionPoolMode: poolMode,
      totalQuestionsPerStudent: parsedQuestionCount,
      selectedDifficulties: orderedDifficulties,
      ...(poolMode === 'SELECTED_CLOS' ? { selectedCloIds } : {}),
      hasFollowUp,
      maxFollowUpQuestions: hasFollowUp ? maxFollowUpQuestions : 0,
      isAutoPublishGrades: false,
      examDurationMinutes: examMinutes,
    }

    try {
      const shift = await inClassShiftService.createShift(payload)
      onCreated(shift)
    } catch (createError) {
      const problem = parseProblemAlert(createError)
      setSubmitProblem(problem)
      toast.error(problem.detail)
      setBusy(false)
    }
  }

  const onFormSubmit = (event: FormEvent) => {
    event.preventDefault()
    if (step === 1) {
      goNext()
      return
    }
    void createShift()
  }

  const steps = [
    { id: 1 as const, title: fixedClassId ? 'Thông tin và thời gian' : 'Lớp và thời gian' },
    { id: 2 as const, title: 'Nguồn đề & Phân bổ CLO' },
  ]

  const canSubmitStep2 =
    !busy &&
    !poolLoading &&
    !poolLoadError &&
    Boolean(poolStats) &&
    poolEvaluation.isValid &&
    examMinutesValid

  const questionCountInvalid =
    Boolean(fieldErrors.questionCount) ||
    !Number.isInteger(parsedQuestionCount) ||
    parsedQuestionCount < 1 ||
    parsedQuestionCount > 10

  return (
    <FormDialog
      titleId="create-inclass-title"
      eyebrow="Ca kiểm tra trên lớp (MF-05)"
      title="Tạo ca kiểm tra"
      icon={ClipboardList}
      size="lg"
      onClose={onClose}
      busy={busy}
      beforeBody={
        <TwoStepProgress
          label="Tiến độ tạo ca"
          step={step}
          steps={steps}
          disabled={busy}
          onStepChange={(target) => (target === 1 ? goBack() : goNext())}
        />
      }
      footer={
        <DialogFooter className="bg-white sm:items-center sm:justify-between">
          <button
            type="button"
            disabled={busy}
            onClick={onClose}
            className={cn(ds.outlineButton, 'min-h-10')}
          >
            Hủy
          </button>
          <div className="flex flex-col-reverse gap-2 sm:flex-row">
            {step === 2 && (
              <button
                type="button"
                disabled={busy}
                onClick={goBack}
                className={cn(ds.outlineButton, 'min-h-10')}
              >
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
                disabled={!canSubmitStep2}
                onClick={() => void createShift()}
                className={cn(ds.primaryButton, 'min-h-10 disabled:cursor-not-allowed disabled:opacity-50')}
              >
                {busy ? (
                  <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" />
                ) : (
                  <Plus className="size-3.5" />
                )}
                {busy ? 'Đang tạo...' : 'Tạo ca'}
              </button>
            )}
          </div>
        </DialogFooter>
      }
    >
      <form id="create-inclass-form" onSubmit={onFormSubmit} className="space-y-4">
        {step === 1 ? (
          <section className="space-y-3.5">
            {fixedClassId ? null : (
              <div>
                <span className={fieldLabelClass(Boolean(fieldErrors.classId))}>Lớp học phần</span>
                <FilterDropMenu
                  label="Chọn lớp"
                  value={classId}
                  onChange={(value) => {
                    setClassId(value)
                    setSelectedCloIds([])
                    clearFieldError('classId', 'pool')
                  }}
                  options={classOptions}
                  emptyText="Chưa có lớp để chọn"
                  className="mt-1.5 w-full"
                  invalid={Boolean(fieldErrors.classId)}
                />
                <FieldError message={fieldErrors.classId} />
              </div>
            )}
            <label className="block">
              <span className={fieldLabelClass(Boolean(fieldErrors.title))}>Tên ca kiểm tra</span>
              <input
                value={title}
                onChange={(event) => {
                  setTitle(event.target.value)
                  clearFieldError('title')
                }}
                maxLength={150}
                placeholder="Ví dụ: Kiểm tra tiến độ 1"
                aria-invalid={Boolean(fieldErrors.title)}
                className={cn(ds.field, 'mt-1.5', fieldErrors.title && fieldInvalidClass)}
                autoFocus
              />
              <FieldError message={fieldErrors.title} />
            </label>
            <label className="block">
              <span className="text-xs font-bold tracking-[0.04em] text-[#5b6b84] uppercase">
                Mô tả ca (tùy chọn)
              </span>
              <textarea
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                rows={2}
                placeholder="Ví dụ: Đánh giá chuẩn đầu ra giai đoạn giữa kỳ"
                className="mt-1.5 min-h-16 w-full resize-y rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm font-medium text-[#0F2854] outline-none placeholder:text-slate-400 focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/20"
              />
            </label>
            <div className="space-y-3">
              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <DateField
                    label="Ngày mở ca"
                    value={startDateKey}
                    invalid={Boolean(fieldErrors.startDateKey || fieldErrors.time)}
                    onChange={(value) => {
                      setStartDateKey(value)
                      clearFieldError('startDateKey', 'time', 'examMinutes')
                    }}
                  />
                  <FieldError message={fieldErrors.startDateKey} />
                </div>
                <ClockField
                  label="Giờ mở ca"
                  value={startClock}
                  invalid={Boolean(fieldErrors.time)}
                  onChange={(value) => {
                    setStartClock(value)
                    clearFieldError('time', 'examMinutes')
                  }}
                />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <DateField
                    label="Ngày đóng ca"
                    value={endDateKey}
                    invalid={Boolean(fieldErrors.endDateKey || fieldErrors.time)}
                    onChange={(value) => {
                      setEndDateKey(value)
                      clearFieldError('endDateKey', 'time', 'examMinutes')
                    }}
                  />
                  <FieldError message={fieldErrors.endDateKey} />
                </div>
                <ClockField
                  label="Giờ đóng ca"
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
          </section>
        ) : (
          <div className="space-y-4">
            <div className="flex items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50/90 px-3.5 py-2.5">
              <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D]/10 text-[#1C4D8D]">
                <UsersRound className="size-4" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="truncate text-sm font-bold text-[#0F2854]">
                  {title.trim() || 'Chưa đặt tên'}
                </p>
                <p className="truncate text-xs font-medium text-slate-600">
                  {selectedClass?.name || 'Chưa chọn lớp'}
                  {selectedClass?.courseCode ? ` · ${selectedClass.courseCode}` : ''}
                  {timeValid
                    ? ` · ${startClock} ${startDateKey.split('-').reverse().join('/')} – ${endClock} ${endDateKey.split('-').reverse().join('/')}`
                    : ''}
                </p>
              </div>
            </div>

            {submitProblem && (
              <div
                role="alert"
                className="rounded-2xl border border-rose-200 bg-rose-50 px-3.5 py-3 text-sm text-rose-900"
              >
                <p className="flex items-center gap-1.5 font-bold text-rose-800">
                  <AlertCircle className="size-4 shrink-0" />
                  {submitProblem.title}
                </p>
                <p className="mt-1 text-xs leading-relaxed text-rose-800">{submitProblem.detail}</p>
                {submitProblem.fieldMessages.length > 0 && (
                  <ul className="mt-1.5 list-disc space-y-0.5 pl-4 text-xs text-rose-800">
                    {submitProblem.fieldMessages.map((msg, idx) => (
                      <li key={idx}>{msg}</li>
                    ))}
                  </ul>
                )}
              </div>
            )}

            <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-xs">
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <SliderField
                    label="Thời lượng làm bài"
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
                  <FieldError message={fieldErrors.examMinutes} />
                </div>

                <div>
                  <div className="flex items-center justify-between gap-2">
                    <label
                      htmlFor="total-questions-per-student"
                      className={fieldLabelClass(questionCountInvalid)}
                    >
                      Số câu mỗi sinh viên <span className="text-rose-600">*</span>
                    </label>
                    <div className="inline-flex items-center gap-1">
                      <button
                        type="button"
                        aria-label="Giảm số câu mỗi sinh viên"
                        disabled={busy || (Number.isInteger(parsedQuestionCount) && parsedQuestionCount <= 1)}
                        onClick={() => stepQuestionCount(-1)}
                        className="flex size-8 items-center justify-center rounded-lg border border-slate-300 bg-white text-[#0F2854] transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                      >
                        <Minus className="size-3.5" />
                      </button>
                      <input
                        id="total-questions-per-student"
                        type="number"
                        min={1}
                        max={10}
                        step={1}
                        value={questionCountInput}
                        aria-label="Số câu mỗi sinh viên"
                        aria-invalid={questionCountInvalid || undefined}
                        onChange={(event) => {
                          setQuestionCountInput(event.target.value)
                          clearFieldError('questionCount', 'pool')
                        }}
                        className={cn(
                          'h-8 w-16 rounded-lg border border-slate-300 bg-white px-2 text-center font-mono text-sm font-bold tabular-nums text-[#0F2854] outline-none focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/20',
                          questionCountInvalid && fieldInvalidClass,
                        )}
                      />
                      <button
                        type="button"
                        aria-label="Tăng số câu mỗi sinh viên"
                        disabled={busy || (Number.isInteger(parsedQuestionCount) && parsedQuestionCount >= 10)}
                        onClick={() => stepQuestionCount(1)}
                        className="flex size-8 items-center justify-center rounded-lg border border-slate-300 bg-white text-[#0F2854] transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                      >
                        <Plus className="size-3.5" />
                      </button>
                    </div>
                  </div>
                  <p className="mt-1.5 text-xs leading-relaxed text-slate-500">
                    Từ 1 đến 10 câu cho từng sinh viên (không phải tổng số câu của cả lớp).
                  </p>
                  <FieldError
                    message={
                      fieldErrors.questionCount ||
                      (questionCountInvalid
                        ? 'Số câu mỗi sinh viên phải là số nguyên từ 1 đến 10.'
                        : undefined)
                    }
                  />
                </div>
              </div>

              <div className="grid items-center gap-4 border-t border-slate-100 pt-3.5 sm:grid-cols-2">
                <div className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-3 py-2.5">
                  <span>
                    <span className="block text-sm font-bold text-[#0F2854]">Câu hỏi phụ</span>
                    <span className="text-xs text-slate-500">AI hỏi đào sâu sau câu chính</span>
                  </span>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={hasFollowUp}
                    aria-label="Câu hỏi phụ"
                    onClick={() => {
                      setHasFollowUp((current) => {
                        if (!current) {
                          setMaxFollowUpQuestions((count) => Math.min(5, Math.max(1, count || 2)))
                        }
                        return !current
                      })
                    }}
                    className={cn(
                      'relative h-6 w-11 shrink-0 rounded-full transition motion-reduce:transition-none',
                      hasFollowUp ? 'bg-[#1C4D8D]' : 'bg-slate-300',
                    )}
                  >
                    <span
                      className={cn(
                        'absolute top-0.5 size-5 rounded-full bg-white shadow transition motion-reduce:transition-none',
                        hasFollowUp ? 'left-[22px]' : 'left-0.5',
                      )}
                    />
                  </button>
                </div>
                <SliderField
                  label="Số câu phụ tối đa"
                  value={Math.min(5, Math.max(1, maxFollowUpQuestions || 2))}
                  min={1}
                  max={5}
                  suffix="câu"
                  disabled={!hasFollowUp}
                  onChange={setMaxFollowUpQuestions}
                />
              </div>
            </section>

            <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-xs">
              <div>
                <div className="flex items-center justify-between gap-2">
                  <span className={fieldLabelClass(Boolean(fieldErrors.difficulties))}>
                    Mức độ câu hỏi <span className="text-rose-600">*</span>
                  </span>
                  <span className="text-xs font-medium text-slate-500">Chọn một hoặc nhiều mức</span>
                </div>
                <div
                  role="group"
                  aria-label="Mức độ câu hỏi"
                  className="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-3"
                >
                  {IN_CLASS_DIFFICULTIES.map((item) => {
                    const checked = selectedDifficulties.includes(item.value)
                    const count = difficultySourceCounts[item.value]
                    return (
                      <label
                        key={item.value}
                        className={cn(
                          'flex cursor-pointer items-center justify-between gap-2 rounded-xl border px-3 py-2.5 text-sm transition motion-reduce:transition-none',
                          checked
                            ? 'border-[#1C4D8D] bg-[#1C4D8D]/5 text-[#0F2854]'
                            : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300',
                        )}
                      >
                        <span className="flex items-center gap-2 font-semibold">
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() => toggleDifficulty(item.value)}
                            className="size-4 rounded accent-[#1C4D8D]"
                          />
                          <span>{item.label}</span>
                        </span>
                        <span className="rounded-md bg-slate-100 px-2 py-0.5 font-mono text-xs font-bold tabular-nums text-slate-700">
                          {poolStats ? `${count} câu` : '…'}
                        </span>
                      </label>
                    )
                  })}
                </div>
                <FieldError
                  message={
                    fieldErrors.difficulties ||
                    (selectedDifficulties.length === 0
                      ? 'Vui lòng chọn ít nhất một mức độ câu hỏi (Dễ, Trung bình hoặc Khó).'
                      : undefined)
                  }
                />
              </div>

              <div className="border-t border-slate-100 pt-3.5">
                <span className={fieldLabelClass(false)}>
                  Nguồn câu hỏi <span className="text-rose-600">*</span>
                </span>
                <div
                  role="radiogroup"
                  aria-label="Nguồn câu hỏi"
                  className="mt-2 grid grid-cols-1 gap-2.5 sm:grid-cols-2"
                >
                  <label
                    className={cn(
                      'flex cursor-pointer items-start gap-2.5 rounded-xl border p-3 transition motion-reduce:transition-none',
                      poolMode === 'ALL_COURSE_BANK'
                        ? 'border-[#1C4D8D] bg-[#1C4D8D]/5'
                        : 'border-slate-200 bg-white hover:border-slate-300',
                    )}
                  >
                    <input
                      type="radio"
                      name="questionPoolMode"
                      value="ALL_COURSE_BANK"
                      checked={poolMode === 'ALL_COURSE_BANK'}
                      onChange={() => {
                        setPoolMode('ALL_COURSE_BANK')
                        clearFieldError('pool')
                      }}
                      className="mt-1 size-4 accent-[#1C4D8D]"
                    />
                    <span className="min-w-0">
                      <span className="block text-sm font-bold text-[#0F2854]">
                        Toàn bộ ngân hàng môn
                      </span>
                      <span className="mt-0.5 block text-xs leading-relaxed text-slate-600">
                        Lấy từ toàn bộ câu luyện tập đúng môn, đã duyệt (APPROVED), đang hoạt động và
                        đúng mức đã chọn.
                      </span>
                    </span>
                  </label>

                  <label
                    className={cn(
                      'flex cursor-pointer items-start gap-2.5 rounded-xl border p-3 transition motion-reduce:transition-none',
                      poolMode === 'SELECTED_CLOS'
                        ? 'border-[#1C4D8D] bg-[#1C4D8D]/5'
                        : 'border-slate-200 bg-white hover:border-slate-300',
                    )}
                  >
                    <input
                      type="radio"
                      name="questionPoolMode"
                      value="SELECTED_CLOS"
                      checked={poolMode === 'SELECTED_CLOS'}
                      onChange={() => {
                        setPoolMode('SELECTED_CLOS')
                        clearFieldError('pool')
                      }}
                      className="mt-1 size-4 accent-[#1C4D8D]"
                    />
                    <span className="min-w-0">
                      <span className="block text-sm font-bold text-[#0F2854]">
                        Theo chuẩn đầu ra (CLO)
                      </span>
                      <span className="mt-0.5 block text-xs leading-relaxed text-slate-600">
                        Chọn các CLO đang hoạt động của môn; mỗi sinh viên nhận ít nhất 1 câu từ mỗi
                        CLO đã chọn.
                      </span>
                    </span>
                  </label>
                </div>
              </div>

              {poolLoading ? (
                <div
                  role="status"
                  className="flex items-center gap-2 rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-3 text-xs font-semibold text-slate-600"
                >
                  <LoaderCircle className="size-4 animate-spin text-[#1C4D8D] motion-reduce:animate-none" />
                  Đang tải thống kê câu hỏi hợp lệ và CLO của môn học...
                </div>
              ) : poolLoadError ? (
                <div
                  role="alert"
                  className="rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-3 text-xs text-rose-900"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="font-bold text-rose-800">{poolLoadError.title}</p>
                    <button
                      type="button"
                      onClick={() => setPoolReloadTick((tick) => tick + 1)}
                      className="inline-flex items-center gap-1 rounded-lg border border-rose-300 bg-white px-2.5 py-1 text-xs font-bold text-rose-800 hover:bg-rose-100"
                    >
                      <RefreshCw className="size-3" />
                      Thử lại
                    </button>
                  </div>
                  <p className="mt-1 leading-relaxed text-rose-800">{poolLoadError.detail}</p>
                </div>
              ) : poolStats ? (
                <>
                  {poolMode === 'SELECTED_CLOS' && (
                    <div className="space-y-2">
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-xs font-bold tracking-[0.04em] text-slate-600 uppercase">
                          Danh sách CLO đang hoạt động ({selectedCloIds.length}/
                          {poolStats.clos.length} đã chọn)
                        </span>
                        {Number.isInteger(parsedQuestionCount) && parsedQuestionCount >= 1 && (
                          <span className="text-xs font-medium text-slate-500">
                            Tối đa {parsedQuestionCount} CLO cho {parsedQuestionCount} câu/SV
                          </span>
                        )}
                      </div>

                      {poolStats.clos.length === 0 ? (
                        <div className="rounded-xl border border-amber-200 bg-amber-50 px-3.5 py-3 text-xs font-medium text-amber-900">
                          Môn học của lớp này chưa có chuẩn đầu ra (CLO) đang hoạt động. Vui lòng liên
                          hệ Trưởng bộ môn thiết lập CLO hoặc chuyển sang nguồn Toàn bộ ngân hàng môn.
                        </div>
                      ) : (
                        <ul className="max-h-56 space-y-2 overflow-y-auto pr-0.5">
                          {poolStats.clos.map((clo) => {
                            const checked = selectedCloIds.includes(clo.cloId)
                            const validForSelectedLevels = sumCountsForDifficulties(
                              clo.counts,
                              selectedDifficulties,
                            )
                            return (
                              <li key={clo.cloId}>
                                <label
                                  className={cn(
                                    'flex cursor-pointer items-start gap-2.5 rounded-xl border p-3 transition motion-reduce:transition-none',
                                    checked
                                      ? 'border-[#1C4D8D] bg-[#1C4D8D]/5'
                                      : 'border-slate-200 bg-white hover:border-slate-300',
                                  )}
                                >
                                  <input
                                    type="checkbox"
                                    checked={checked}
                                    onChange={() => toggleClo(clo.cloId)}
                                    className="mt-1 size-4 shrink-0 rounded accent-[#1C4D8D]"
                                  />
                                  <div className="min-w-0 flex-1">
                                    <div className="flex flex-wrap items-center justify-between gap-2">
                                      <span className="font-mono text-sm font-bold text-[#0F2854]">
                                        {clo.cloCode}
                                      </span>
                                      <span
                                        className={cn(
                                          'rounded-md px-2 py-0.5 font-mono text-xs font-bold tabular-nums',
                                          validForSelectedLevels > 0
                                            ? 'bg-teal-50 text-teal-800'
                                            : 'bg-rose-50 text-rose-700',
                                        )}
                                      >
                                        Hợp lệ: {validForSelectedLevels} câu
                                      </span>
                                    </div>
                                    <p className="mt-1 text-xs leading-relaxed text-slate-600">
                                      {clo.description || 'Chưa có mô tả chi tiết cho CLO này.'}
                                    </p>
                                    <div className="mt-2 flex flex-wrap items-center gap-1.5 font-mono text-[11px] font-semibold tabular-nums text-slate-600">
                                      <span className="rounded bg-slate-100 px-2 py-0.5">
                                        Dễ: {clo.counts.easy}
                                      </span>
                                      <span className="rounded bg-slate-100 px-2 py-0.5">
                                        Trung bình: {clo.counts.medium}
                                      </span>
                                      <span className="rounded bg-slate-100 px-2 py-0.5">
                                        Khó: {clo.counts.hard}
                                      </span>
                                    </div>
                                  </div>
                                </label>
                              </li>
                            )
                          })}
                        </ul>
                      )}
                    </div>
                  )}

                  <div
                    data-testid="pool-allocation-preview"
                    aria-live="polite"
                    className={cn(
                      'rounded-xl border p-3.5 text-xs',
                      poolEvaluation.isValid
                        ? 'border-slate-200 bg-slate-50/85 text-slate-700'
                        : 'border-rose-200 bg-rose-50/75 text-rose-900',
                    )}
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="flex items-center gap-1.5 font-bold text-[#0F2854]">
                        <Layers className="size-3.5 text-[#1C4D8D]" aria-hidden="true" />
                        Tổng câu hợp lệ sẵn có & Dự kiến phân bổ mỗi sinh viên
                      </span>
                      <span className="rounded-md bg-white px-2.5 py-1 font-mono text-xs font-bold tabular-nums text-[#0F2854] ring-1 ring-slate-200">
                        Sẵn có: {poolEvaluation.totalAvailable} câu · Cần:{' '}
                        {Number.isInteger(parsedQuestionCount) && parsedQuestionCount > 0
                          ? parsedQuestionCount
                          : 0}{' '}
                        câu/SV
                      </span>
                    </div>

                    {poolEvaluation.difficultyPreview.length > 0 && (
                      <div className="mt-2.5 flex flex-wrap items-center gap-1.5">
                        <span className="font-semibold text-slate-600">Theo mức độ:</span>
                        {poolEvaluation.difficultyPreview.map((item) => (
                          <span
                            key={item.difficulty}
                            className="rounded-md border border-slate-200 bg-white px-2 py-0.5 font-mono font-semibold tabular-nums text-[#0F2854]"
                          >
                            {item.label}: ~{item.allocatedCount} câu (sẵn {item.availableCount})
                          </span>
                        ))}
                      </div>
                    )}

                    {poolMode === 'SELECTED_CLOS' && poolEvaluation.cloPreview.length > 0 && (
                      <div className="mt-2 flex flex-wrap items-center gap-1.5">
                        <span className="font-semibold text-slate-600">Theo CLO:</span>
                        {poolEvaluation.cloPreview.map((item) => (
                          <span
                            key={item.cloId}
                            className="rounded-md border border-slate-200 bg-white px-2 py-0.5 font-mono font-semibold tabular-nums text-[#0F2854]"
                          >
                            {item.cloCode}: ~{item.allocatedCount} câu (sẵn{' '}
                            {item.availableInSelectedDifficulties})
                          </span>
                        ))}
                      </div>
                    )}

                    {!poolEvaluation.isValid && poolEvaluation.errors.length > 0 ? (
                      <ul role="alert" className="mt-2.5 space-y-1 border-t border-rose-200/80 pt-2 font-semibold text-rose-800">
                        {poolEvaluation.errors.map((err, idx) => (
                          <li key={idx} className="flex items-start gap-1.5">
                            <AlertCircle className="mt-0.5 size-3.5 shrink-0 text-rose-600" />
                            <span>{err}</span>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="mt-2 text-[11px] text-slate-500">
                        Hệ thống Backend sẽ bốc cố định đúng số câu cho từng sinh viên khi vào ca,
                        ưu tiên câu ít được giao nhất (Anti-Collision) và không tự bù ngoài CLO/mức đã chọn.
                      </p>
                    )}
                  </div>
                </>
              ) : null}

              <FieldError message={fieldErrors.pool} />
            </section>
          </div>
        )}
      </form>
    </FormDialog>
  )
}

export function InClassShiftHubPage({
  classId: lockedClassId,
  embedded = false,
}: {
  classId?: string
  embedded?: boolean
} = {}) {
  const [searchParams] = useSearchParams()
  const [classes, setClasses] = useState<LecturerClassOption[]>([])
  const [shifts, setShifts] = useState<InClassShift[]>([])
  const [classFilter, setClassFilter] = useState(() => lockedClassId || searchParams.get('classId') || 'ALL')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('ALL')
  const searchInput = useSearchInput()
  const search = searchInput.query
  const [loading, setLoading] = useState(true)
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
      try {
        const [classResult, shiftResult] = await Promise.all([
          inClassShiftService.getLecturerClasses(),
          inClassShiftService.listShifts(lockedClassId ? { classId: lockedClassId } : undefined),
        ])
        if (cancelled) return
        setClasses(lockedClassId ? classResult.filter((item) => item.id === lockedClassId) : classResult)
        setShifts(shiftResult)
      } catch (loadError) {
        if (!cancelled) toast.error(getErrorMessage(loadError))
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [reloadKey, lockedClassId])

  useEffect(() => {
    const timer = window.setInterval(() => {
      void inClassShiftService.listShifts(lockedClassId ? { classId: lockedClassId } : undefined).then(setShifts).catch(() => undefined)
    }, 15000)
    return () => window.clearInterval(timer)
  }, [lockedClassId])

  const classOptions = useMemo(() => {
    if (classes.length === 0) return []
    return [
      { value: 'ALL', label: 'Tất cả lớp' },
      ...classes.map((item) => ({ value: item.id, label: item.name })),
    ]
  }, [classes])

  const activeClassId = lockedClassId || classFilter

  const classScopedShifts = useMemo(() => {
    if (activeClassId === 'ALL') return shifts
    return shifts.filter((shift) => shift.classId === activeClassId)
  }, [shifts, activeClassId])

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
    try {
      await inClassShiftService.activateShift(shift.id)
      setShifts((current) =>
        current.map((item) => (item.id === shift.id ? { ...item, status: 'ACTIVE' } : item)),
      )
      toast.success(`Đã kích hoạt “${shift.title}”. Có thể chiếu mã OTP cho sinh viên.`)
    } catch (activateError) {
      toast.error(userMessage(activateError, 'Không kích hoạt được ca thi. Vui lòng thử lại.'))
    } finally {
      setActivatingId(null)
    }
  }

  const confirmClose = async () => {
    if (!closingShift) return
    setClosing(true)
    try {
      await inClassShiftService.closeShift(closingShift.id)
      setShifts((current) =>
        current.map((item) => (item.id === closingShift.id ? { ...item, status: 'CLOSED' } : item)),
      )
      if (projectorShiftId === closingShift.id) setProjectorShiftId(null)
      toast.success(`Đã đóng ca “${closingShift.title}”. Sinh viên không vào ca hoặc nộp bài được nữa.`)
      setClosingShift(null)
    } catch (closeError) {
      toast.error(userMessage(closeError, 'Không đóng được ca thi. Vui lòng thử lại.'))
    } finally {
      setClosing(false)
    }
  }

  return (
    <div lang="vi" className={embedded ? 'space-y-3' : cn(ds.page, 'pb-8')}>

      <section className={cn(ds.section, 'overflow-hidden')}>
        <div className={cn(ds.sectionHeader, 'flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between')}>
            <div className={ds.sectionHeading}>
              <div className={ds.sectionTitleRow}>
                <span className={ds.sectionIconWrap}>
                  <ClipboardList className={ds.sectionIcon} />
                </span>
                <h2 className={ds.sectionTitle}>Danh sách ca</h2>
                <span className={ds.sectionBadge}>
                  {loading ? '…' : `${filtered.length} ca`}
                </span>
              </div>
              <p className={ds.sectionHint}>{embedded ? 'Ca kiểm tra của lớp này' : 'Theo dõi các ca kiểm tra trên lớp'}</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {lockedClassId ? null : (
              <FilterDropMenu
                label="Lớp học phần"
                menuMinWidth={ds.filterMenuMinWidth}
                wrapOptions
                value={classFilter}
                onChange={setClassFilter}
                options={classOptions}
                emptyText="Chưa có lớp"
                className={ds.filterClass}
              />
              )}
              <div className="relative shrink-0">
                <SearchField
                  value={searchInput.draft}
                  onChange={(event) => searchInput.change(event.target.value)}
                  onKeyDown={searchInput.onKeyDown}
                  placeholder="Tìm theo tên ca..."
                  className={cn(ds.searchControl, 'h-10 rounded-full border border-[#4988C4] bg-white py-0 pr-4 pl-9 text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:border-[#4988C4] focus:ring-2 focus:ring-[#1C4D8D]/15')}
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
            <StatusTabs label="Lọc trạng thái ca" value={statusFilter} options={STATUS_FILTERS.map(tab => ({ ...tab, count: statusCounts[tab.value] }))} onChange={setStatusFilter} />
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
                          <span className="font-bold text-[#0F2854] tabular-nums">{shift.questionCount}</span> câu/SV
                          {shift.hasFollowUp ? (
                            <>
                              {' · phụ '}
                              <span className="font-bold text-[#0F2854] tabular-nums">
                                {shift.maxFollowUpQuestions || 1}
                              </span>
                            </>
                          ) : null}
                        </span>
                      </span>
                      <span className={factClass}>
                        <Layers className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        <span>
                          {formatPoolModeLabel(
                            shift.questionPoolMode,
                            shift.selectedCloCodes,
                            shift.selectedCloIds.length,
                          )}
                          {' · '}
                          {formatDifficultyListLabel(shift.selectedDifficulties)}
                        </span>
                      </span>
                      <span className={factClass}>
                        <UsersRound className="size-3.5 shrink-0 text-[#1C4D8D]" />
                        <span className="tabular-nums">
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
                      to={shift.classId ? `/lecturer/classes/${shift.classId}/shifts/${shift.id}/results` : '/lecturer/classes'}
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
        fixedClassId={lockedClassId}
        onClose={() => setCreateOpen(false)}
        onCreated={(shift) => {
          setCreateOpen(false)
          toast.success(`Đã tạo ca “${shift.title}”. Bấm Kích hoạt khi bắt đầu giờ thi, rồi chiếu mã OTP.`)
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
