import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

export function DividedList({
  as: Element = 'ul',
  className,
  ...props
}: HTMLAttributes<HTMLElement> & { as?: 'ul' | 'div' }) {
  return <Element {...props} className={cn('divide-y divide-[#4988C4]/30', className)} />
}
