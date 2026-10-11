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
            className="block text-xs font-semibold tracking-wider text-slate-700 uppercase dark:text-slate-300"
          >
            {label}
          </label>
        )}
        <input
          id={inputId}
          ref={ref}
          className={cn(
            'flex w-full rounded-lg border bg-white px-3 py-2 text-sm placeholder:text-slate-400 focus:ring-2 focus:ring-offset-1 focus:outline-none disabled:opacity-50 dark:bg-slate-900',
            error
              ? 'border-red-500 text-red-900 focus:ring-red-500'
              : 'border-slate-300 focus:ring-orange-500 dark:border-slate-700',
            className,
          )}
          {...props}
        />
        {error ? (
          <p className="text-xs font-medium text-red-600">{error}</p>
        ) : helperText ? (
          <p className="text-xs text-slate-500">{helperText}</p>
        ) : null}
      </div>
    )
  },
)

Input.displayName = 'Input'
