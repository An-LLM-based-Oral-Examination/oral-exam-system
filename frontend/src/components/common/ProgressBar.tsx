import type { ComponentPropsWithoutRef } from 'react'
import { cn } from '@/lib/utils'

export function ProgressBar({ className, ...props }: ComponentPropsWithoutRef<'progress'>) {
  return (
    <progress
      {...props}
      className={cn(
        'block h-2.5 w-full appearance-none overflow-hidden rounded-full border border-[#82aedb]/60 bg-[#e1eef8] [&::-moz-progress-bar]:bg-[#1C4D8D] [&::-webkit-progress-bar]:bg-[#e1eef8] [&::-webkit-progress-value]:bg-[#1C4D8D]',
        className,
      )}
    />
  )
}
