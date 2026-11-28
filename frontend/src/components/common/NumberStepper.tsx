import { Minus, Plus } from 'lucide-react'
import { cn } from '@/lib/utils'

/** Compact counter using the same continuous border as the practice question count. */
export function NumberStepper({
  id,
  label,
  value,
  unit,
  min,
  max = 2147483647,
  onChange,
  disabled = false,
  invalid = false,
}: {
  id?: string
  label: string
  value: string | number
  unit: string
  min: number
  max?: number
  onChange: (value: string) => void
  disabled?: boolean
  invalid?: boolean
}) {
  const buttonClass =
    'flex size-8 items-center justify-center rounded-lg text-[#1C4D8D] transition hover:bg-[#E7F3FB] focus-visible:outline-2 focus-visible:outline-[#1C4D8D] disabled:cursor-not-allowed disabled:opacity-35'
  const change = (step: number) =>
    onChange(String(Math.min(max, Math.max(min, (Number(value) || min) + step))))
  return (
    <div
      className={cn(
        'inline-flex h-10 max-w-full shrink-0 items-center gap-1 rounded-xl border bg-white px-1 shadow-sm shadow-[#0F2854]/10',
        invalid ? 'border-[#c2415b]' : 'border-[#82aedb]/70',
      )}
    >
      <button
        type="button"
        className={buttonClass}
        aria-label={`Giảm ${label.toLocaleLowerCase('vi-VN')}`}
        disabled={disabled || Number(value) <= min}
        onClick={() => change(-1)}
      >
        <Minus className="size-3.5" />
      </button>
      <label className="flex min-w-0 items-center gap-1 pr-1">
        <span className="sr-only">{label}</span>
        <input
          id={id}
          aria-label={label}
          type="number"
          min={min}
          max={max}
          step={1}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
          aria-invalid={invalid || undefined}
          className="h-8 w-10 [appearance:textfield] appearance-none border-0 bg-transparent text-center text-sm font-bold text-[#0F2854] tabular-nums outline-none focus:ring-0 focus:outline-none [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
        />
        <span className="text-xs font-semibold whitespace-nowrap text-[#5b6b84]">{unit}</span>
      </label>
      <button
        type="button"
        className={buttonClass}
        aria-label={`Tăng ${label.toLocaleLowerCase('vi-VN')}`}
        disabled={disabled || Number(value) >= max}
        onClick={() => change(1)}
      >
        <Plus className="size-3.5" />
      </button>
    </div>
  )
}
