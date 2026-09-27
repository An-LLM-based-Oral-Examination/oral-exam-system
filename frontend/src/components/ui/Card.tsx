import React, { type HTMLAttributes } from 'react'
import { cn } from '@/utils/cn'

export const Card: React.FC<HTMLAttributes<HTMLDivElement>> = ({ className, children, ...props }) => (
  <div
    className={cn(
      'rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-950 dark:text-slate-50 shadow-sm',
      className
    )}
    {...props}
  >
    {children}
  </div>
)

export const CardHeader: React.FC<HTMLAttributes<HTMLDivElement>> = ({ className, children, ...props }) => (
  <div className={cn('flex flex-col space-y-1.5 p-6 border-b border-slate-100 dark:border-slate-800', className)} {...props}>
    {children}
  </div>
)

export const CardTitle: React.FC<HTMLAttributes<HTMLHeadingElement>> = ({ className, children, ...props }) => (
  <h3 className={cn('text-lg font-semibold leading-none tracking-tight text-slate-900 dark:text-slate-100', className)} {...props}>
    {children}
  </h3>
)

export const CardDescription: React.FC<HTMLAttributes<HTMLParagraphElement>> = ({ className, children, ...props }) => (
  <p className={cn('text-sm text-slate-500 dark:text-slate-400', className)} {...props}>
    {children}
  </p>
)

export const CardContent: React.FC<HTMLAttributes<HTMLDivElement>> = ({ className, children, ...props }) => (
  <div className={cn('p-6 pt-4', className)} {...props}>
    {children}
  </div>
)

export const CardFooter: React.FC<HTMLAttributes<HTMLDivElement>> = ({ className, children, ...props }) => (
  <div className={cn('flex items-center p-6 pt-0 border-t border-slate-100 dark:border-slate-800 mt-4', className)} {...props}>
    {children}
  </div>
)
