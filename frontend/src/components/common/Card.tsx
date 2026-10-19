import React, { type HTMLAttributes } from 'react'
import { cn } from '@/utils/cn'

export const Card: React.FC<HTMLAttributes<HTMLDivElement>> = ({
  className,
  children,
  ...props
}) => (
  <div
    className={cn(
      'overflow-hidden rounded-2xl border border-[#4988C4]/45 bg-white text-[#0F2854] shadow-[0_16px_40px_rgba(15,40,84,0.08)]',
      className,
    )}
    {...props}
  >
    {children}
  </div>
)

export const CardHeader: React.FC<HTMLAttributes<HTMLDivElement>> = ({
  className,
  children,
  ...props
}) => (
  <div
    className={cn(
      'flex flex-col space-y-1.5 border-b border-[#4988C4]/45 bg-white p-6',
      className,
    )}
    {...props}
  >
    {children}
  </div>
)

export const CardTitle: React.FC<HTMLAttributes<HTMLHeadingElement>> = ({
  className,
  children,
  ...props
}) => (
  <h3
    className={cn(
      'text-base leading-none font-bold tracking-tight text-[#0F2854] sm:text-lg',
      className,
    )}
    {...props}
  >
    {children}
  </h3>
)

export const CardDescription: React.FC<HTMLAttributes<HTMLParagraphElement>> = ({
  className,
  children,
  ...props
}) => (
  <p className={cn('text-sm text-[#5b6b84]', className)} {...props}>
    {children}
  </p>
)

export const CardContent: React.FC<HTMLAttributes<HTMLDivElement>> = ({
  className,
  children,
  ...props
}) => (
  <div className={cn('p-6 pt-4', className)} {...props}>
    {children}
  </div>
)

export const CardFooter: React.FC<HTMLAttributes<HTMLDivElement>> = ({
  className,
  children,
  ...props
}) => (
  <div
    className={cn(
      'mt-4 flex items-center border-t border-[#4988C4]/45 p-6 pt-0',
      className,
    )}
    {...props}
  >
    {children}
  </div>
)
