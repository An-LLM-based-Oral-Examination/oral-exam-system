import { useEffect, useRef, useState } from 'react'
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
}

/**
 * Custom dropdown đồng bộ với bộ lọc môn học trong Lịch sử thi thử (MockExam).
 */
export function FilterDropMenu({
  label,
  value,
  options,
  onChange,
  className,
}: FilterDropMenuProps) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const selected = options.find((option) => option.value === value)?.label ?? label

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }
    window.addEventListener('pointerdown', onPointerDown)
    window.addEventListener('keydown', onKeyDown)
    return () => {
      window.removeEventListener('pointerdown', onPointerDown)
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return (
    <div
      ref={rootRef}
      data-filter-open={open ? 'true' : undefined}
      className={cn('relative', className)}
    >
      <button
        type="button"
        aria-label={label}
        aria-expanded={open}
        aria-haspopup="listbox"
        onClick={() => setOpen((current) => !current)}
        className={cn(
          'relative inline-flex h-10 w-full items-center rounded-full border bg-white py-0 pr-11 pl-4 text-left text-sm font-semibold text-[#0F2854] shadow-[0_2px_8px_rgba(23,48,96,0.04)]',
          open ? 'border-[#4988C4]/45 ring-2 ring-[#1C4D8D]/15' : 'border-[#4988C4]/45',
        )}
      >
        <span className="truncate">{selected}</span>
        <ChevronDown
          className={cn(
            'pointer-events-none absolute right-5 size-4 text-[#1C4D8D] transition',
            open && 'rotate-180',
          )}
        />
      </button>
      {open && (
        <ul
          role="listbox"
          aria-label={label}
          className="absolute top-[calc(100%+8px)] left-0 z-30 max-h-64 min-w-full overflow-y-auto rounded-2xl border border-[#4988C4]/45 bg-white p-1.5 shadow-[0_16px_36px_rgba(23,48,96,0.14)]"
        >
          {options.map((option) => {
            const isSelected = option.value === value
            return (
              <li key={option.value || 'all'}>
                <button
                  type="button"
                  role="option"
                  aria-selected={isSelected}
                  onClick={() => {
                    onChange(option.value)
                    setOpen(false)
                  }}
                  className={cn(
                    'flex w-full items-center gap-3 rounded-xl px-3 py-2 text-left text-sm font-semibold',
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
        </ul>
      )}
    </div>
  )
}
