import { useCallback, useEffect, useRef, useState } from 'react'
import { notificationService } from '@/services/notification.service'
import type { StudentNotification } from '@/types/notification.types'

const PAGE_SIZE = 20

export function useStudentNotifications(isOpen: boolean) {
  const [items, setItems] = useState<StudentNotification[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(0)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestId = useRef(0)

  const loadPage = useCallback(async (targetPage: number, append: boolean) => {
    const currentRequest = ++requestId.current
    if (append) setIsLoadingMore(true)
    else setIsLoading(true)
    setError(null)

    try {
      const result = await notificationService.list(targetPage, PAGE_SIZE)
      if (currentRequest !== requestId.current) return

      setItems((current) => {
        if (!append) return result.items
        const existingIds = new Set(current.map((item) => item.id))
        return [...current, ...result.items.filter((item) => !existingIds.has(item.id))]
      })
      setUnreadCount(result.unreadCount)
      setTotalCount(result.totalCount)
      setPage(result.page)
    } catch (cause) {
      if (currentRequest === requestId.current) {
        const message = cause instanceof Error ? cause.message : 'Không thể tải thông báo.'
        setError(message)
      }
    } finally {
      if (currentRequest === requestId.current) {
        setIsLoading(false)
        setIsLoadingMore(false)
      }
    }
  }, [])

  const refresh = useCallback(() => loadPage(1, false), [loadPage])

  useEffect(() => {
    const initialLoad = window.setTimeout(() => void refresh(), 0)
    const handleFocus = () => void refresh()
    window.addEventListener('focus', handleFocus)
    return () => {
      window.clearTimeout(initialLoad)
      window.removeEventListener('focus', handleFocus)
      requestId.current += 1
    }
  }, [refresh])

  useEffect(() => {
    if (!isOpen) return
    const refreshOnOpen = window.setTimeout(() => void refresh(), 0)
    return () => window.clearTimeout(refreshOnOpen)
  }, [isOpen, refresh])

  const markRead = useCallback(
    async (notification: StudentNotification) => {
      if (notification.isRead) return
      setItems((current) =>
        current.map((item) => (item.id === notification.id ? { ...item, isRead: true } : item)),
      )
      setUnreadCount((current) => Math.max(0, current - 1))
      try {
        await notificationService.markRead(notification.id)
      } catch (cause) {
        await refresh()
        throw cause
      }
    },
    [refresh],
  )

  const markAllRead = useCallback(async () => {
    const previousItems = items
    const previousUnreadCount = unreadCount
    setItems((current) => current.map((item) => ({ ...item, isRead: true })))
    setUnreadCount(0)
    try {
      await notificationService.markAllRead()
    } catch (cause) {
      setItems(previousItems)
      setUnreadCount(previousUnreadCount)
      throw cause
    }
  }, [items, unreadCount])

  const hasMore = page > 0 && items.length < totalCount

  return {
    items,
    unreadCount,
    isLoading,
    isLoadingMore,
    error,
    hasMore,
    refresh,
    loadMore: () => loadPage(page + 1, true),
    markRead,
    markAllRead,
  }
}
