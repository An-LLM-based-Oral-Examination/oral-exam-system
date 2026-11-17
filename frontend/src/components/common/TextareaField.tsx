import { useId, type ComponentPropsWithRef, type ReactNode } from 'react'
import { ds } from '@/design/system'
import { cn } from '@/lib/utils'
import { FieldError } from './formField'
import { fieldInvalidClass, fieldLabelClass } from './formFieldClasses'

type TextareaFieldProps = ComponentPropsWithRef<'textarea'> & {
  label: ReactNode
  error?: string
  wrapperClassName?: string
}

export function TextareaField({
  label,
  error,
  wrapperClassName,
  className,
  'aria-describedby': describedBy,
  ...props
}: TextareaFieldProps) {
  const errorId = useId()
  const descriptionIds = [describedBy, error ? errorId : undefined].filter(Boolean).join(' ')
  return (
    <div className={cn('min-w-0', wrapperClassName)}>
      <label className="block">
        <span className={fieldLabelClass(Boolean(error))}>{label}</span>
        <textarea
          {...props}
          aria-invalid={error ? true : props['aria-invalid']}
          aria-describedby={descriptionIds || undefined}
          className={cn(
            ds.field,
            'mt-1.5 block min-h-24 max-w-full min-w-0 resize-y py-2.5 leading-relaxed font-normal',
            className,
            error && fieldInvalidClass,
          )}
        />
      </label>
      {error && (
        <div id={errorId} role="alert">
          <FieldError message={error} />
        </div>
      )}
    </div>
  )
}
