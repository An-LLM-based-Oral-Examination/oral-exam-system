import React, { type ButtonHTMLAttributes } from 'react'
import { cn } from '@/utils/cn'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'outline' | 'ghost'
  size?: 'sm' | 'md' | 'lg'
  isLoading?: boolean
}

export const Button: React.FC<ButtonProps> = ({
  className,
  variant = 'primary',
  size = 'md',
  isLoading = false,
  disabled,
  children,
  ...props
}) => {
  const baseStyles =
    'inline-flex items-center justify-center font-bold rounded-xl transition duration-200 hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2 disabled:pointer-events-none disabled:opacity-50 disabled:hover:translate-y-0 cursor-pointer motion-reduce:transform-none'

  const variants = {
    primary:
      'bg-[#1C4D8D] text-white shadow-[0_8px_16px_rgba(28,77,141,0.22)] hover:bg-[#0F2854] hover:shadow-[0_12px_22px_rgba(15,40,84,0.28)] focus-visible:outline-[#1C4D8D]',
    secondary: 'bg-[#4988C4] text-white hover:bg-[#1C4D8D] hover:shadow-[0_8px_16px_rgba(15,40,84,0.18)] focus-visible:outline-[#4988C4]',
    danger:
      'border border-[#f3c2c8] bg-[#fdecec] text-[#c2415b] hover:bg-[#f8d5d8] hover:shadow-[0_8px_16px_rgba(194,65,91,0.16)] focus-visible:outline-[#c2415b]',
    outline:
      'border border-[#4988C4] bg-white text-[#1C4D8D] hover:border-[#1C4D8D] hover:bg-[#E7F3FB] hover:shadow-[0_8px_16px_rgba(15,40,84,0.14)] focus-visible:outline-[#1C4D8D]',
    ghost: 'bg-transparent text-[#1C4D8D] hover:bg-[#F4FBFD] focus-visible:outline-[#1C4D8D]',
  }

  const sizes = {
    sm: 'min-h-9 px-3 text-xs gap-1.5',
    md: 'h-10 px-4 text-sm gap-2',
    lg: 'min-h-12 px-5 text-base gap-2.5',
  }

  return (
    <button
      className={cn(baseStyles, variants[variant], sizes[size], className)}
      disabled={disabled || isLoading}
      {...props}
    >
      {isLoading && (
        <svg
          className="mr-2 -ml-1 h-4 w-4 animate-spin text-current"
          fill="none"
          viewBox="0 0 24 24"
        >
          <circle
            className="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            strokeWidth="4"
          />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
        </svg>
      )}
      {children}
    </button>
  )
}
