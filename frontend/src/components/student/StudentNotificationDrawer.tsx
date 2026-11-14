import { Bell, CheckCheck, LoaderCircle, RefreshCw } from 'lucide-react'
import { SideDrawer } from '@/design/overlays'
import type { StudentNotification } from '@/types/notification.types'

function formatCreatedAt(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Vừa xong'
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
    hour12: false,
  }).format(date)
}
export function StudentNotificationDrawer({
  open,
  onClose,
  notifications,
  unreadCount,
  isLoading,
  isLoadingMore,
  error,
  hasMore,
  onRetry,
  onLoadMore,
  onMarkAllRead,
  onSelect,
}: {
  open: boolean
  onClose: () => void
  notifications: StudentNotification[]
  unreadCount: number
  isLoading: boolean
  isLoadingMore: boolean
  error: string | null
  hasMore: boolean
  onRetry: () => void
  onLoadMore: () => void
  onMarkAllRead: () => void
  onSelect: (notification: StudentNotification) => void
}) {
  return (
    <SideDrawer
      open={open}
      onClose={onClose}
      title="Thông báo"
      eyebrow={unreadCount > 0 ? `${unreadCount} chưa đọc` : 'Student Portal'}
      size="lg"
      bodyClassName="p-0"
      actions={
        <button
          type="button"
          onClick={onMarkAllRead}
          disabled={unreadCount === 0 || isLoading}
          className="inline-flex items-center gap-1.5 rounded-lg border border-[#4988C4]/45 px-2.5 py-1.5 text-xs font-bold text-[#1C4D8D] transition hover:bg-[#F4FBFD] disabled:cursor-not-allowed disabled:opacity-45"
        >
          <CheckCheck className="size-3.5" aria-hidden="true" />
          Đọc tất cả
        </button>
      }
    >
      {isLoading ? (
        <div className="flex min-h-48 items-center justify-center gap-2 text-sm text-[#61779b]">
          <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
          Đang tải thông báo...
        </div>
      ) : error ? (
        <div className="m-4 rounded-xl border border-[#f3c2c8] bg-[#fdecec] p-4 text-sm text-[#9f3045]">
          <p>{error}</p>
          <button
            type="button"
            onClick={onRetry}
            className="mt-3 inline-flex items-center gap-1.5 font-bold underline underline-offset-2"
          >
            <RefreshCw className="size-3.5" aria-hidden="true" />
            Thử lại
          </button>
        </div>
      ) : notifications.length === 0 ? (
        <div className="flex min-h-48 flex-col items-center justify-center gap-3 px-6 text-center text-[#61779b]">
          <span className="flex size-12 items-center justify-center rounded-full bg-[#BDE8F5]/55 text-[#1C4D8D]">
            <Bell className="size-5" aria-hidden="true" />
          </span>
          <p className="text-sm font-semibold">Bạn chưa có thông báo mới.</p>
        </div>
      ) : (
        <div className="divide-y divide-[#4988C4]/15">
          {notifications.map((notification) => (
            <button
              key={notification.id}
              type="button"
              onClick={() => onSelect(notification)}
              className={`block w-full px-5 py-4 text-left transition hover:bg-[#F4FBFD] ${
                notification.isRead ? 'bg-white' : 'bg-[#eef8ff]'
              }`}
            >
              <div className="flex items-start gap-3">
                <span
                  className={`mt-1 size-2.5 shrink-0 rounded-full ${
                    notification.isRead ? 'bg-transparent' : 'bg-[#ef5c72]'
                  }`}
                  aria-hidden="true"
                />
                <span className="min-w-0 flex-1">
                  <span className="flex items-start justify-between gap-3">
                    <strong className="text-sm text-[#0F2854]">{notification.title}</strong>
                    <time className="shrink-0 text-[11px] text-[#7890b1]">
                      {formatCreatedAt(notification.createdAt)}
                    </time>
                  </span>
                  <span className="mt-1 block text-sm leading-5 text-[#536b8f]">
                    {notification.message}
                  </span>
                </span>
              </div>
            </button>
          ))}
          {hasMore ? (
            <button
              type="button"
              onClick={onLoadMore}
              disabled={isLoadingMore}
              className="flex w-full items-center justify-center gap-2 px-5 py-4 text-sm font-bold text-[#1C4D8D] hover:bg-[#F4FBFD] disabled:opacity-60"
            >
              {isLoadingMore ? <LoaderCircle className="size-4 animate-spin" /> : null}
              {isLoadingMore ? 'Đang tải...' : 'Tải thêm thông báo'}
            </button>
          ) : null}
        </div>
      )}
    </SideDrawer>
  )
}
