import { ArrowLeft } from 'lucide-react'
import { Link, type LinkProps } from 'react-router-dom'
import { cn } from '@/lib/utils'

export function BackLink({ className, children, ...props }: LinkProps) {
  return (
    <Link
      {...props}
      className={cn(
        'inline-flex min-h-8 w-fit items-center gap-1.5 rounded-lg text-base font-bold text-[#1C4D8D] transition hover:text-[#0F2854] hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D]',
        className,
      )}
    >
      <ArrowLeft className="size-5 shrink-0" aria-hidden="true" />
      {children}
    </Link>
  )
}
