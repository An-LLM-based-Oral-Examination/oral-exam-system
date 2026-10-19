import React, { type HTMLAttributes } from 'react'
import { cn } from '@/utils/cn'

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: 'default' | 'success' | 'warning' | 'danger' | 'info' | 'outline'
}

export const Badge: React.FC<BadgeProps> = ({
  className,
  variant = 'default',
  children,
  ...props
}) => {
  const baseStyles =
    'inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs font-bold'

  const variants = {
    default: 'border-[#4988C4]/30 bg-[#F4FBFD] text-[#1C4D8D]',
    success: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]',
    warning: 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]',
    danger: 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]',
    info: 'border-[#4988C4]/30 bg-[#F4FBFD] text-[#1C4D8D]',
    outline: 'border-[#4988C4] bg-white text-[#1C4D8D]',
  }

  return (
    <span className={cn(baseStyles, variants[variant], className)} {...props}>
      {children}
    </span>
  )
}
