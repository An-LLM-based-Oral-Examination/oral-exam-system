import {
  useEffect,
  useId,
  useRef,
  type ReactNode,
} from 'react'
import { X, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { lockBodyScroll } from './bodyScrollLock'
import { overlay, overlaySize } from './system'

type OverlaySize = keyof typeof overlaySize
type LockScrollMode = boolean | 'mobile'

function shouldLockScroll(mode: LockScrollMode) {
  if (mode === false) return false
  if (mode === true) return true
  return !window.matchMedia('(min-width: 1024px)').matches
}

function useOverlayChrome({
  open,
  onClose,
  busy = false,
  lockScroll = true,
}: {
  open: boolean
  onClose: () => void
  busy?: boolean
  lockScroll?: LockScrollMode
}) {
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  useEffect(() => {
    if (!open) return
    const unlock = shouldLockScroll(lockScroll) ? lockBodyScroll() : undefined
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || busy) return
      event.preventDefault()
      onCloseRef.current()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => {
      unlock?.()
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [open, busy, lockScroll])
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
}: {
  children: ReactNode
  className?: string
}) {
  return <div className={cn(overlay.footer, className)}>{children}</div>
}

/** Shell form/popup chuẩn: sheet mobile → centered desktop. */
export function FormDialog({
  open = true,
  titleId: titleIdProp,
  eyebrow,
  title,
  icon: Icon,
  iconNode,
  headerExtra,
  onClose,
  busy = false,
  size = 'md',
  maxHeight = 'max-h-[min(92dvh,680px)]',
  beforeBody,
  footer,
  children,
  lockScroll = true,
  nested = false,
  align = 'sheet',
  labelledBy,
  label,
}: {
  open?: boolean
  titleId?: string
  eyebrow?: ReactNode
  title?: ReactNode
  icon?: LucideIcon
  iconNode?: ReactNode
  /** Chip/tag hiển thị trên header, cạnh nút đóng. */
  headerExtra?: ReactNode
  onClose: () => void
  busy?: boolean
  size?: OverlaySize
  maxHeight?: string
  beforeBody?: ReactNode
  footer?: ReactNode
  children: ReactNode
  lockScroll?: LockScrollMode
  nested?: boolean
  /** `sheet` = bottom sheet trên mobile; `center` = luôn giữa màn hình */
  align?: 'sheet' | 'center'
  labelledBy?: string
  label?: string
}) {
  const autoId = useId()
  const titleId = titleIdProp ?? labelledBy ?? autoId
  useOverlayChrome({ open, onClose, busy, lockScroll })

  if (!open) return null

  const hasChrome = title != null || eyebrow != null || Icon != null || iconNode != null

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
        role="dialog"
        aria-modal="true"
        aria-labelledby={hasChrome ? titleId : undefined}
        aria-label={!hasChrome ? label : undefined}
        className={cn(overlay.panel, overlaySize[size], maxHeight)}
      >
        {hasChrome ? (
          <header className={overlay.header}>
            <div className="flex items-start justify-between gap-3">
              <div className="flex min-w-0 items-start gap-3">
                {Icon || iconNode ? (
                  <span className={overlay.iconWrap}>
                    {Icon ? <Icon className="size-5" aria-hidden="true" /> : iconNode}
                  </span>
                ) : null}
                <div className="min-w-0">
                  {eyebrow != null ? <p className={overlay.eyebrow}>{eyebrow}</p> : null}
                  {title != null ? (
                    <h2 id={titleId} className={cn(overlay.title, eyebrow == null && 'mt-0')}>
                      {title}
                    </h2>
                  ) : null}
                </div>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                {headerExtra}
                <OverlayCloseButton onClose={onClose} busy={busy} />
              </div>
            </div>
          </header>
        ) : null}
        {beforeBody}
        <div className={cn(overlay.body, !hasChrome && 'rounded-none')}>{children}</div>
        {footer}
      </div>
    </div>
  )
}

/** Panel overlay không header — dùng khi nội dung tự có chrome (vd. form tạo câu hỏi). */
export function BareDialog({
  open = true,
  onClose,
  busy = false,
  size = '6xl',
  maxHeight = 'max-h-[min(94dvh,920px)]',
  children,
  lockScroll = true,
  label,
}: {
  open?: boolean
  onClose: () => void
  busy?: boolean
  size?: OverlaySize
  maxHeight?: string
  children: ReactNode
  lockScroll?: LockScrollMode
  label: string
}) {
  useOverlayChrome({ open, onClose, busy, lockScroll })
  if (!open) return null

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
        role="dialog"
        aria-modal="true"
        aria-label={label}
        className={cn(overlay.panel, overlaySize[size], maxHeight)}
      >
        {children}
      </div>
    </div>
  )
}

/** Drawer phải — danh sách chi tiết / review. */
export function SideDrawer({
  open = true,
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
}: {
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
}) {
  const autoId = useId()
  const titleId = titleIdProp ?? autoId
  useOverlayChrome({ open, onClose, lockScroll })
  if (!open) return null

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
        className={cn(overlay.drawer, overlay.z.modal, width)}
        role="dialog"
        aria-modal="true"
        aria-labelledby={header ? undefined : titleId}
        aria-label={header ? label : undefined}
      >
        <header className={overlay.header}>
          {header ?? (
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                {eyebrow != null ? <p className={overlay.eyebrow}>{eyebrow}</p> : null}
                {title != null ? (
                  <h2 id={titleId} className={cn(overlay.title, 'truncate', eyebrow == null && 'mt-0')}>
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
