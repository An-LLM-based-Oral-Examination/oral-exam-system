import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { CalendarDays, ChevronLeft, ChevronRight, Clock3 } from 'lucide-react'
import { ds } from '@/design/system'
import { fieldInvalidClass, fieldLabelClass } from '@/components/common/formFieldClasses'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'

function pad2(value: number) {
  return String(value).padStart(2, '0')
}

function toDateKey(date: Date) {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
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

const CLOCK_HOURS = Array.from({ length: 24 }, (_, index) => pad2(index))
const CLOCK_MINUTES = Array.from({ length: 60 }, (_, index) => pad2(index))

export function ClockField({
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
  const [box, setBox] = useState<{
    left: number
    width: number
    top?: number
    bottom?: number
  } | null>(null)
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
    hourListRef.current
      ?.querySelector<HTMLElement>(`[data-value="${hour}"]`)
      ?.scrollIntoView({ block: 'center' })
    minuteListRef.current
      ?.querySelector<HTMLElement>(`[data-value="${minute}"]`)
      ?.scrollIntoView({ block: 'center' })
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
        className={cn(
          ds.field,
          'mt-1.5 flex items-center justify-between gap-2 px-3 text-left',
          invalid && fieldInvalidClass,
        )}
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
                        item === hour
                          ? 'bg-[#1C4D8D] text-white'
                          : 'text-[#0F2854] hover:bg-[#e7f1fb]',
                      )}
                    >
                      {item}
                    </button>
                  ))}
                </div>
                <div
                  ref={minuteListRef}
                  className="max-h-48 overflow-y-auto border-l border-[#82aedb]/55 py-1"
                >
                  {CLOCK_MINUTES.map((item) => (
                    <button
                      key={item}
                      type="button"
                      data-value={item}
                      onClick={() => onChange(`${hour}:${item}`)}
                      className={cn(
                        'block w-full py-1.5 text-center text-sm font-bold tabular-nums',
                        item === minute
                          ? 'bg-[#1C4D8D] text-white'
                          : 'text-[#0F2854] hover:bg-[#e7f1fb]',
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
  const cells: Array<{ key: string; day: number } | null> = Array.from(
    { length: offset },
    () => null,
  )
  for (let day = 1; day <= daysInMonth; day += 1) {
    cells.push({ key: toDateKey(new Date(year, month, day)), day })
  }
  return cells
}

export function DateField({
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
        className={cn(
          ds.field,
          'mt-1.5 flex items-center justify-between gap-2 px-3 text-left',
          invalid && fieldInvalidClass,
        )}
      >
        <span className="truncate text-sm font-bold capitalize">
          {value ? formatExamDay(value) : 'Chọn ngày'}
        </span>
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
                  onClick={() =>
                    setCursor(
                      (current) => new Date(current.getFullYear(), current.getMonth() - 1, 1),
                    )
                  }
                  className="flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] hover:bg-white"
                >
                  <ChevronLeft className="size-4" />
                </button>
                <p className="text-sm font-bold text-[#0F2854] capitalize">{monthLabel}</p>
                <button
                  type="button"
                  aria-label="Tháng sau"
                  onClick={() =>
                    setCursor(
                      (current) => new Date(current.getFullYear(), current.getMonth() + 1, 1),
                    )
                  }
                  className="flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] hover:bg-white"
                >
                  <ChevronRight className="size-4" />
                </button>
              </div>
              <div className="grid grid-cols-7 gap-0.5 px-2 pt-2">
                {WEEKDAYS.map((day) => (
                  <span key={day} className="py-1 text-center text-[11px] font-bold text-[#5b6b84]">
                    {day}
                  </span>
                ))}
              </div>
              <div className="grid grid-cols-7 gap-0.5 px-2 pb-2">
                {cells.map((cell, index) =>
                  cell ? (
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
                  ),
                )}
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
