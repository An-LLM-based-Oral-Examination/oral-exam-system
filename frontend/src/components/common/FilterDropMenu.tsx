import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Check, ChevronDown } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface FilterDropMenuOption {
  value: string
  label: string
}

interface FilterDropMenuProps {
  label: string
  value: string
  options: FilterDropMenuOption[]
  onChange: (value: string) => void
  className?: string
  /** Thông báo khi danh sách trống. */
  emptyText?: string
  /** Viền đỏ khi trường không hợp lệ. */
  invalid?: boolean
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
  emptyText = 'Không có dữ liệu',
  invalid = false,
}: FilterDropMenuProps) {
  const [open, setOpen] = useState(false)
  const [box, setBox] = useState<MenuBox | null>(null)
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLUListElement>(null)
  const isEmpty = options.length === 0
  const isOpen = open && !isEmpty
  const selected = options.find((option) => option.value === value)?.label
  const triggerLabel = selected ?? (isEmpty ? emptyText : label)

  useLayoutEffect(() => {
    if (!isOpen) return

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
      const width = Math.max(rect.width, 176)
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

    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [isOpen, options.length])

  useEffect(() => {
    if (!isOpen) return
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node
      if (rootRef.current?.contains(target) || panelRef.current?.contains(target)) return
      setOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      event.stopPropagation()
      setOpen(false)
    }
    window.addEventListener('pointerdown', onPointerDown)
    window.addEventListener('keydown', onKeyDown, true)
    return () => {
      window.removeEventListener('pointerdown', onPointerDown)
      window.removeEventListener('keydown', onKeyDown, true)
    }
  }, [isOpen])

  return (
    <div
      ref={rootRef}
      data-filter-open={isOpen ? 'true' : undefined}
      className={cn('relative', className)}
    >
      <button
        ref={buttonRef}
        type="button"
        aria-label={label}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        aria-disabled={isEmpty || undefined}
        disabled={isEmpty}
        onClick={() => {
          if (isEmpty) return
          setOpen((current) => !(current && !isEmpty))
        }}
        aria-invalid={invalid || undefined}
        className={cn(
          'relative inline-flex h-10 w-full items-center rounded-full border bg-white py-0 pr-11 pl-4 text-left text-sm font-semibold shadow-[0_2px_8px_rgba(23,48,96,0.04)] transition',
          invalid ? 'border-[#c2415b] bg-[#fff7f8]' : 'border-[#4988C4]',
          isEmpty ? 'cursor-default text-[#8aa0b8]' : 'text-[#0F2854]',
          isOpen && !invalid && 'ring-2 ring-[#1C4D8D]/15',
          isOpen && invalid && 'ring-2 ring-[#c2415b]/20',
        )}
      >
        <span className="truncate">{triggerLabel}</span>
        <ChevronDown
          className={cn(
            'pointer-events-none absolute right-5 size-4 text-[#1C4D8D] transition',
            isOpen && 'rotate-180',
            isEmpty && 'opacity-50',
          )}
        />
      </button>
      {isOpen &&
        box &&
        createPortal(
          <ul
            ref={panelRef}
            role="listbox"
            aria-label={label}
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
              const isSelected = option.value === value
              return (
                <li key={`${option.value}::${option.label}`}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={isSelected}
                    onClick={() => {
                      onChange(option.value)
                      setOpen(false)
                    }}
                    className={cn(
                      'flex w-full items-center gap-3 rounded-xl px-3 py-2 text-left text-sm font-semibold transition',
                      isSelected
                        ? 'bg-[#4988C4]/16 text-[#1C4D8D]'
                        : 'text-[#0F2854] hover:bg-[#4988C4]/18',
                    )}
                  >
                    <span className="min-w-0 flex-1 truncate">{option.label}</span>
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
