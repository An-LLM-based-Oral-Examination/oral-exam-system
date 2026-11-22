import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Check, ChevronDown } from 'lucide-react'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'

export interface FilterDropMenuOption {
  value: string
  label: string
}

interface FilterDropMenuProps {
  label: string
  value: string | readonly string[]
  options: FilterDropMenuOption[]
  onChange: (value: string) => void
  className?: string
  buttonClassName?: string
  menuMinWidth?: number
  wrapOptions?: boolean
  /** Thông báo khi danh sách trống. */
  emptyText?: string
  /** Viền đỏ khi trường không hợp lệ. */
  invalid?: boolean
  disabled?: boolean
}

interface MenuBox {
  left: number
  width: number
  maxHeight: number
  /** Neo mép trên (mở xuống) hoặc mép dưới (mở lên) — tránh lệch khi nội dung ngắn hơn maxHeight. */
  top?: number
  bottom?: number
}

/**
 * Dropdown bộ lọc dùng chung: menu portal cố định theo nút mở,
 * không bị lệch/cắt bởi overflow của section.
 */
export function FilterDropMenu({
  label,
  value,
  options,
  onChange,
  className,
  buttonClassName,
  menuMinWidth = 176,
  wrapOptions = false,
  emptyText = 'Không có dữ liệu',
  invalid = false,
  disabled = false,
}: FilterDropMenuProps) {
  const [open, setOpen] = useState(false)
  const [box, setBox] = useState<MenuBox | null>(null)
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLUListElement>(null)
  const isEmpty = options.length === 0
  const unavailable = disabled || isEmpty
  const multiple = typeof value !== 'string'
  const selectedValues = typeof value === 'string' ? [value] : value
  const selected = options.filter((option) => selectedValues.includes(option.value)).map((option) => option.label).join(', ') || undefined
  const triggerLabel = selected ?? (isEmpty ? emptyText : label)

  useLayoutEffect(() => {
    if (!open) {
      return scheduleUi(() => setBox(null))
    }

    const place = () => {
      const rect = buttonRef.current?.getBoundingClientRect()
      if (!rect) return

      const gap = 8
      const viewportPad = 8
      const preferredHeight = 256
      const spaceBelow = window.innerHeight - rect.bottom - gap - viewportPad
      const spaceAbove = rect.top - gap - viewportPad
      const openUp = spaceBelow < 160 && spaceAbove > spaceBelow
      const maxHeight = Math.max(120, Math.min(preferredHeight, openUp ? spaceAbove : spaceBelow))
      const width = Math.min(Math.max(rect.width, menuMinWidth), window.innerWidth - 2 * viewportPad)
      const left = Math.min(Math.max(viewportPad, rect.left), window.innerWidth - width - viewportPad)

      if (openUp) {
        setBox({
          bottom: window.innerHeight - rect.top + gap,
          left,
          width,
          maxHeight,
        })
      } else {
        setBox({
          top: rect.bottom + gap,
          left,
          width,
          maxHeight,
        })
      }
    }

    const cancelPlace = scheduleUi(place)
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      cancelPlace()
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open, options.length, menuMinWidth])

  useEffect(() => {
    if (!unavailable || !open) return
    return scheduleUi(() => setOpen(false))
  }, [unavailable, open])

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node
      if (rootRef.current?.contains(target) || panelRef.current?.contains(target)) return
      setOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        event.stopPropagation()
        setOpen(false)
        buttonRef.current?.focus()
        return
      }
      if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return
      const optionButtons = Array.from(
        panelRef.current?.querySelectorAll<HTMLButtonElement>('[role="option"]:not(:disabled)') ?? [],
      )
      if (optionButtons.length === 0) return
      event.preventDefault()
      const currentIndex = optionButtons.findIndex((el) => el === document.activeElement)
      let nextIndex = 0
      if (event.key === 'ArrowDown') {
        nextIndex = currentIndex >= 0 ? (currentIndex + 1) % optionButtons.length : 0
      } else if (event.key === 'ArrowUp') {
        nextIndex =
          currentIndex >= 0
            ? (currentIndex - 1 + optionButtons.length) % optionButtons.length
            : optionButtons.length - 1
      } else if (event.key === 'End') {
        nextIndex = optionButtons.length - 1
      }
      optionButtons[nextIndex]?.focus()
    }
    window.addEventListener('pointerdown', onPointerDown)
    window.addEventListener('keydown', onKeyDown, true)
    return () => {
      window.removeEventListener('pointerdown', onPointerDown)
      window.removeEventListener('keydown', onKeyDown, true)
    }
  }, [open])

  return (
    <div
      ref={rootRef}
      data-filter-open={open ? 'true' : undefined}
      className={cn('relative min-w-0 max-w-full', className)}
    >
      <button
        ref={buttonRef}
        type="button"
        aria-label={label}
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-disabled={unavailable || undefined}
        disabled={unavailable}
        onClick={() => {
          if (unavailable) return
          setOpen((current) => !current)
        }}
        onKeyDown={(event) => {
          if (unavailable) return
          if ((event.key === 'ArrowDown' || event.key === 'ArrowUp') && !open) {
            event.preventDefault()
            setOpen(true)
          }
        }}
        aria-invalid={invalid || undefined}
        className={cn(
          'relative inline-flex h-10 w-full items-center rounded-full border bg-white py-0 pr-11 pl-4 text-left text-sm font-semibold shadow-[0_2px_8px_rgba(23,48,96,0.04)] transition motion-reduce:transition-none',
          invalid ? 'border-[#c2415b] bg-[#fff7f8]' : 'border-[#4988C4]',
          unavailable ? 'cursor-default text-[#8aa0b8]' : 'text-[#0F2854]',
          open && !invalid && 'ring-2 ring-[#1C4D8D]/15',
          open && invalid && 'ring-2 ring-[#c2415b]/20',
          buttonClassName,
        )}
      >
        <span className="min-w-0 flex-1 truncate">{triggerLabel}</span>
        <ChevronDown
          className={cn(
            'pointer-events-none absolute right-5 size-4 text-[#1C4D8D] transition motion-reduce:transition-none',
            open && 'rotate-180',
            unavailable && 'opacity-50',
          )}
        />
      </button>
      {open &&
        !unavailable &&
        box &&
        createPortal(
          <ul
            ref={panelRef}
            role="listbox"
            aria-label={label}
            aria-multiselectable={multiple || undefined}
            style={{
              top: box.top,
              bottom: box.bottom,
              left: box.left,
              width: box.width,
              maxHeight: box.maxHeight,
            }}
            className="fixed z-[120] overflow-y-auto rounded-2xl border border-[#4988C4] bg-white p-1.5 shadow-[0_16px_36px_rgba(23,48,96,0.14)]"
          >
            {options.map((option) => {
              const isSelected = selectedValues.includes(option.value)
              return (
                <li key={`${option.value}::${option.label}`}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={isSelected}
                    onClick={() => {
                      onChange(option.value)
                      if (!multiple) setOpen(false)
                    }}
                    className={cn(
                      'flex min-h-11 w-full items-center gap-3 rounded-xl px-3 py-2 text-left text-sm font-semibold transition motion-reduce:transition-none',
                      isSelected
                        ? 'bg-[#4988C4]/16 text-[#1C4D8D]'
                        : 'text-[#0F2854] hover:bg-[#4988C4]/18',
                    )}
                  >
                    <span className={cn('min-w-0 flex-1', wrapOptions ? 'whitespace-normal [overflow-wrap:anywhere]' : 'truncate')}>
                      {option.label}
                    </span>
                    {isSelected && <Check className="size-4 shrink-0" />}
                  </button>
                </li>
              )
            })}
          </ul>,
          document.body,
        )}
    </div>
  )
}
