import {
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { X, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { lockBodyScroll } from './bodyScrollLock'
import {
  getFocusableElements,
  isTopmostOverlayModal,
  trapOverlayTabKey,
} from './overlayFocus'
import { overlay, overlaySize } from './system'

type OverlaySize = keyof typeof overlaySize
type LockScrollMode = boolean | 'mobile'

function shouldLockScroll(mode: LockScrollMode) {
  if (mode === false) return false
  if (mode === true) return true
  return !(window.matchMedia?.('(min-width: 1024px)').matches ?? window.innerWidth >= 1024)
}

function useOverlayChrome({
  onClose,
  containerRef,
  busy = false,
  lockScroll = true,
}: {
  onClose: () => void
  containerRef: { readonly current: HTMLElement | null }
  busy?: boolean
  lockScroll?: LockScrollMode
}) {
  const onCloseRef = useRef(onClose)
  const busyRef = useRef(busy)
  const [previousFocus] = useState<HTMLElement | null>(() =>
    typeof document !== 'undefined' && document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null,
  )

  useLayoutEffect(() => {
    onCloseRef.current = onClose
    busyRef.current = busy
  }, [onClose, busy])

  useEffect(() => {
    const container = containerRef.current
    if (container && !container.contains(document.activeElement)) {
      const focusables = getFocusableElements(container)
      if (focusables.length > 0) {
        focusables[0].focus()
      } else {
        container.focus()
      }
    }

    const unlock = shouldLockScroll(lockScroll) ? lockBodyScroll() : undefined

    const onKeyDown = (event: KeyboardEvent) => {
      const dialog = containerRef.current
      if (!dialog) return

      const topmost = isTopmostOverlayModal(dialog)

      if (event.key === 'Escape') {
        if (!topmost || busyRef.current || event.defaultPrevented) return
        event.preventDefault()
        onCloseRef.current()
        return
      }

      if (event.key !== 'Tab' || !topmost) return
      trapOverlayTabKey(event, dialog)
    }

    window.addEventListener('keydown', onKeyDown)
    return () => {
      unlock?.()
      window.removeEventListener('keydown', onKeyDown)
      if (!container?.isConnected && previousFocus?.isConnected) {
        previousFocus.focus()
      }
    }
  }, [lockScroll, containerRef, previousFocus])
}

function OverlayCloseButton({
  onClose,
  busy,
  className,
}: {
  onClose: () => void
  busy?: boolean
  className?: string
}) {
  return (
    <button
      type="button"
      aria-label="Đóng"
      data-space-ignore="true"
      disabled={busy}
      onClick={onClose}
      className={cn(overlay.close, className)}
    >
      <X className="size-4" aria-hidden="true" />
    </button>
  )
}

export function DialogFooter({
  children,
  className,
  variant = 'default',
}: {
  children: ReactNode
  className?: string
  variant?: 'default' | 'calm'
}) {
  return (
    <div
      className={cn(
        variant === 'calm'
          ? 'shrink-0 flex flex-col-reverse gap-2 border-t border-[#0F2854]/10 bg-white px-5 py-3.5 sm:flex-row sm:justify-end sm:px-5'
          : overlay.footer,
        className,
      )}
    >
      {children}
    </div>
  )
}

interface FormDialogProps {
  open?: boolean
  titleId?: string
  eyebrow?: ReactNode
  title?: ReactNode
  icon?: LucideIcon
  iconNode?: ReactNode
  /** Chip/tag hiển thị trên header, cạnh nút đóng. */
  headerExtra?: ReactNode
  headerClassName?: string
  onClose: () => void
  busy?: boolean
  size?: OverlaySize
  maxHeight?: string
  beforeBody?: ReactNode
  bodyClassName?: string
  footer?: ReactNode
  children: ReactNode
  lockScroll?: LockScrollMode
  nested?: boolean
  /** `sheet` = bottom sheet trên mobile; `center` = luôn giữa màn hình */
  align?: 'sheet' | 'center'
  variant?: 'default' | 'calm'
  labelledBy?: string
  label?: string
}

/** Shell form/popup chuẩn: sheet mobile → centered desktop. */
export function FormDialog({ open = true, ...props }: FormDialogProps) {
  if (!open) return null
  return <FormDialogContent {...props} />
}

function FormDialogContent({
  titleId: titleIdProp,
  eyebrow,
  title,
  icon: Icon,
  iconNode,
  headerExtra,
  headerClassName,
  onClose,
  busy = false,
  size = 'md',
  maxHeight = 'max-h-[min(92dvh,680px)]',
  beforeBody,
  bodyClassName,
  footer,
  children,
  lockScroll = true,
  nested = false,
  align = 'sheet',
  variant = 'default',
  labelledBy,
  label,
}: Omit<FormDialogProps, 'open'>) {
  const autoId = useId()
  const titleId = titleIdProp ?? labelledBy ?? autoId
  const dialogRef = useRef<HTMLDivElement>(null)
  useOverlayChrome({ onClose, containerRef: dialogRef, busy, lockScroll })

  const hasChrome = title != null || eyebrow != null || Icon != null || iconNode != null
  const isCalm = variant === 'calm'

  return (
    <div
      className={cn(
        align === 'center' ? overlay.scrimCenter : overlay.scrim,
        nested ? overlay.z.nested : overlay.z.modal,
      )}
      role="presentation"
      data-space-ignore="true"
      onClick={(event) => {
        if (event.target === event.currentTarget && !busy) onClose()
      }}
    >
      <div
        ref={dialogRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby={hasChrome ? titleId : undefined}
        aria-label={!hasChrome ? label : undefined}
        className={cn(
          isCalm
            ? 'flex w-full flex-col overflow-hidden rounded-t-2xl border border-[#0F2854]/12 bg-white shadow-[0_20px_50px_rgba(15,40,84,0.16)] sm:rounded-2xl'
            : overlay.panel,
          'focus:outline-none',
          overlaySize[size],
          maxHeight,
        )}
      >
        {hasChrome ? (
          <header
            className={cn(
              isCalm
                ? 'shrink-0 border-b border-[#0F2854]/10 bg-white px-5 py-4'
                : overlay.header,
              headerClassName,
            )}
          >
            <div className="flex items-start justify-between gap-3">
              <div className="flex min-w-0 flex-1 items-start gap-3">
                {Icon || iconNode ? (
                  <span
                    className={
                      isCalm
                        ? 'flex size-10 shrink-0 items-center justify-center rounded-xl border border-[#0F2854]/10 bg-[#F4FBFD] text-[#1C4D8D]'
                        : overlay.iconWrap
                    }
                  >
                    {Icon ? (
                      <Icon
                        className="size-5"
                        strokeWidth={isCalm ? 1.5 : 2}
                        aria-hidden="true"
                      />
                    ) : (
                      iconNode
                    )}
                  </span>
                ) : null}
                <div className="min-w-0 flex-1">
                  {eyebrow != null ? (
                    <p
                      className={cn(
                        isCalm ? 'text-xs font-medium text-[#5b6b84]' : overlay.eyebrow,
                        'break-words [overflow-wrap:anywhere]',
                      )}
                    >
                      {eyebrow}
                    </p>
                  ) : null}
                  {title != null ? (
                    <h2
                      id={titleId}
                      className={cn(
                        isCalm
                          ? 'mt-0.5 text-lg font-semibold tracking-[-0.01em] text-[#0F2854]'
                          : overlay.title,
                        'break-words [overflow-wrap:anywhere]',
                        eyebrow == null && 'mt-0',
                      )}
                    >
                      {title}
                    </h2>
                  ) : null}
                </div>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                {headerExtra}
                <OverlayCloseButton
                  onClose={onClose}
                  busy={busy}
                  className={
                    isCalm ? 'border-[#0F2854]/15 hover:bg-[#F4FBFD]' : undefined
                  }
                />
              </div>
            </div>
          </header>
        ) : null}
        {beforeBody}
        <div
          className={cn(
            isCalm
              ? 'min-h-0 min-w-0 max-w-full flex-1 overflow-y-auto overscroll-contain bg-white px-5 pt-4 pb-6'
              : overlay.body,
            !hasChrome && 'rounded-none',
            bodyClassName,
          )}
        >
          {children}
        </div>
        {footer}
      </div>
    </div>
  )
}

interface BareDialogProps {
  open?: boolean
  onClose: () => void
  busy?: boolean
  size?: OverlaySize
  maxHeight?: string
  children: ReactNode
  lockScroll?: LockScrollMode
  label: string
}

/** Panel overlay không header — dùng khi nội dung tự có chrome (vd. form tạo câu hỏi). */
export function BareDialog({ open = true, ...props }: BareDialogProps) {
  if (!open) return null
  return <BareDialogContent {...props} />
}

function BareDialogContent({
  onClose,
  busy = false,
  size = '6xl',
  maxHeight = 'max-h-[min(94dvh,920px)]',
  children,
  lockScroll = true,
  label,
}: Omit<BareDialogProps, 'open'>) {
  const dialogRef = useRef<HTMLDivElement>(null)
  useOverlayChrome({ onClose, containerRef: dialogRef, busy, lockScroll })

  return (
    <div
      className={cn(overlay.scrim, overlay.z.modal)}
      role="presentation"
      data-space-ignore="true"
      onClick={(event) => {
        if (event.target === event.currentTarget && !busy) onClose()
      }}
    >
      <div
        ref={dialogRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-label={label}
        className={cn(overlay.panel, 'focus:outline-none', overlaySize[size], maxHeight)}
      >
        {children}
      </div>
    </div>
  )
}

interface SideDrawerProps {
  open?: boolean
  onClose: () => void
  titleId?: string
  eyebrow?: ReactNode
  title?: ReactNode
  meta?: ReactNode
  actions?: ReactNode
  /** Thay toàn bộ nội dung header (vẫn giữ khung header chuẩn). */
  header?: ReactNode
  size?: 'lg' | 'xl' | '2xl'
  children: ReactNode
  footer?: ReactNode
  lockScroll?: LockScrollMode
  bodyClassName?: string
  label?: string
}

/** Drawer phải — danh sách chi tiết / review. */
export function SideDrawer({ open = true, ...props }: SideDrawerProps) {
  if (!open) return null
  return <SideDrawerContent {...props} />
}

function SideDrawerContent({
  onClose,
  titleId: titleIdProp,
  eyebrow,
  title,
  meta,
  actions,
  header,
  size = 'xl',
  children,
  footer,
  /** Desktop giữ list nền cuộn được; mobile mới khóa body. */
  lockScroll = 'mobile',
  bodyClassName,
  label,
}: Omit<SideDrawerProps, 'open'>) {
  const autoId = useId()
  const titleId = titleIdProp ?? autoId
  const drawerRef = useRef<HTMLElement>(null)
  useOverlayChrome({ onClose, containerRef: drawerRef, lockScroll })

  const width =
    size === '2xl' ? 'max-w-2xl' : size === 'lg' ? 'max-w-lg' : 'max-w-xl'

  return (
    <>
      <button
        type="button"
        className={cn(overlay.scrimDrawer, overlay.z.drawer)}
        onClick={onClose}
        aria-label="Đóng bảng chi tiết"
      />
      <aside
        ref={drawerRef}
        tabIndex={-1}
        className={cn(overlay.drawer, overlay.z.modal, width, 'focus:outline-none')}
        role="dialog"
        aria-modal="true"
        aria-labelledby={header ? undefined : titleId}
        aria-label={header ? label : undefined}
      >
        <header className={overlay.header}>
          {header ?? (
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                {eyebrow != null ? (
                  <p className={cn(overlay.eyebrow, 'break-words [overflow-wrap:anywhere]')}>
                    {eyebrow}
                  </p>
                ) : null}
                {title != null ? (
                  <h2
                    id={titleId}
                    className={cn(
                      overlay.title,
                      'break-words [overflow-wrap:anywhere]',
                      eyebrow == null && 'mt-0',
                    )}
                  >
                    {title}
                  </h2>
                ) : null}
                {meta}
              </div>
              <div className="flex shrink-0 items-center gap-1.5">
                {actions}
                <OverlayCloseButton onClose={onClose} />
              </div>
            </div>
          )}
        </header>
        <div className={cn(overlay.body, bodyClassName)}>{children}</div>
        {footer}
      </aside>
    </>
  )
}

export { OverlayCloseButton }
