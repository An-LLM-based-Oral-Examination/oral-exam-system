import type { KeyboardEvent } from 'react'
import { cn } from '@/lib/utils'

interface StatusOption<T extends string> {
  value: T
  label: string
  count?: number
  dotClass?: string
  countIdleClass?: string
}

export function StatusTabs<T extends string>({
  label,
  value,
  options,
  onChange,
  className,
}: {
  label: string
  value: T
  options: readonly StatusOption<T>[]
  onChange: (value: T) => void
  className?: string
}) {
  function move(event: KeyboardEvent<HTMLButtonElement>, index: number) {
    const length = options.length
    let next: number
    switch (event.key) {
      case 'ArrowRight':
        next = (index + 1) % length
        break
      case 'ArrowLeft':
        next = (index - 1 + length) % length
        break
      case 'Home':
        next = 0
        break
      case 'End':
        next = length - 1
        break
      default:
        return
    }
    event.preventDefault()
    const buttons = event.currentTarget.parentElement?.querySelectorAll<HTMLButtonElement>('button')
    buttons?.[next]?.focus()
    onChange(options[next].value)
  }

  return (
    <div
      role="tablist"
      aria-label={label}
      className={cn('flex min-w-0 flex-wrap items-end border-b-2 border-[#1C4D8D]', className)}
    >
      {options.map((option, index) => {
        const active = option.value === value
        return (
          <button
            key={option.value}
            type="button"
            role="tab"
            aria-selected={active}
            tabIndex={
              active || (index === 0 && !options.some((item) => item.value === value)) ? 0 : -1
            }
            onClick={() => onChange(option.value)}
            onKeyDown={(event) => move(event, index)}
            className={cn(
              'relative inline-flex items-center gap-1.5 border px-3.5 py-2.5 text-sm font-bold whitespace-nowrap transition sm:px-4',
              active
                ? '-mb-0.5 rounded-t-xl border-[#1C4D8D] border-b-white bg-[linear-gradient(165deg,#BDE8F5_0%,#EAF6FB_42%,#ffffff_100%)] text-[#1C4D8D]'
                : 'border-transparent text-[#6f7f93] hover:bg-[#F4FBFD] hover:text-[#1C4D8D]',
            )}
          >
            {option.dotClass && (
              <span
                className={cn('size-1.5 rounded-full', active ? 'bg-[#1C4D8D]' : option.dotClass)}
                aria-hidden="true"
              />
            )}
            {option.label}
            {option.count !== undefined && (
              <span
                className={cn(
                  'rounded-full px-1.5 py-0.5 text-xs font-bold tabular-nums',
                  active
                    ? 'bg-[#1C4D8D]/12 text-[#1C4D8D]'
                    : (option.countIdleClass ?? 'bg-[#E7F3FB] text-[#1C4D8D]'),
                )}
              >
                {option.count}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
