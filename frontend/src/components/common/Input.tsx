import { type InputHTMLAttributes, forwardRef } from 'react'
import { cn } from '@/utils/cn'

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string
  error?: string
  helperText?: string
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, label, error, helperText, id, ...props }, ref) => {
    const inputId = id || (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined)

    return (
      <div className="w-full space-y-1">
        {label && (
          <label
            htmlFor={inputId}
            className="block text-xs font-bold tracking-[0.06em] text-[#5b6b84] uppercase"
          >
            {label}
          </label>
        )}
        <input
          id={inputId}
          ref={ref}
          className={cn(
            'flex min-h-11 w-full rounded-xl border bg-white px-3 text-sm font-semibold text-[#0F2854] outline-none placeholder:font-medium placeholder:text-[#8aa0b8] focus:ring-2 disabled:opacity-50',
            error
              ? 'border-[#f3c2c8] text-[#c2415b] focus:ring-[#c2415b]/30'
              : 'border-[#4988C4] focus:ring-[#4988C4]/30',
            className,
          )}
          {...props}
        />
        {error ? (
          <p className="text-xs font-medium text-[#c2415b]">{error}</p>
        ) : helperText ? (
          <p className="text-xs text-[#5b6b84]">{helperText}</p>
        ) : null}
      </div>
    )
  },
)

Input.displayName = 'Input'
