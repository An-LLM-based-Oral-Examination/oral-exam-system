import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Bell, MailCheck, RefreshCw, Search, Send, Trash2 } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu, type FilterDropMenuOption } from '@/components/common/FilterDropMenu'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { ListPagination } from '@/components/common/ListPagination'
import { LoadingState } from '@/components/common/LoadingState'
import { SearchField } from '@/components/common/SearchField'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { toast } from '@/components/common/toastStore'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import { BannerFact, PageBanner, SectionHeader } from '@/design/ui'
import { useSearchInput } from '@/hooks/useSearchInput'
import { useUrlQueryState } from '@/hooks/useUrlQueryState'
import {
  extractProblemDetails,
  mapProblemFieldErrors,
  type NormalizedProblem,
} from '@/lib/problemDetails'
import { cn } from '@/lib/utils'
import {
  adminService,
  DEFAULT_ADMIN_PAGE_SIZE,
  type AdminNotificationItem,
  type AdminUserItem,
  type CreateAdminNotificationPayload,
} from '@/services/admin.service'
import {
  ADMIN_ROLE_OPTIONS,
  getAdminRoleShortLabel,
  getNotificationTypeLabel,
  normalizeAdminRoleValue,
  NOTIFICATION_READ_FILTER_OPTIONS,
  NOTIFICATION_SCOPE_OPTIONS,
  NOTIFICATION_TYPE_OPTIONS,
} from '@/pages/admin/adminConstants'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'

const TYPE_FILTER_OPTIONS: FilterDropMenuOption[] = [
  { value: '', label: 'Tất cả loại thông báo' },
  ...NOTIFICATION_TYPE_OPTIONS.map((item) => ({ value: item.value, label: item.label })),
]

const ROLE_FILTER_OPTIONS: FilterDropMenuOption[] = [
  { value: '', label: 'Tất cả vai trò' },
  ...ADMIN_ROLE_OPTIONS.map((item) => ({ value: item.value, label: item.shortLabel })),
]

const READ_VALUES = ['all', 'unread', 'read'] as const
type ReadFilter = (typeof READ_VALUES)[number]

type DispatchField = 'targetScope' | 'targetRole' | 'targetUserId' | 'type' | 'title' | 'message'
const DISPATCH_FIELDS: readonly DispatchField[] = [
  'targetScope',
  'targetRole',
  'targetUserId',
  'type',
  'title',
  'message',
]

function formatDateTime(raw: string): string {
  const parsed = new Date(raw)
  return Number.isNaN(parsed.getTime()) ? raw : parsed.toLocaleString('vi-VN')
}

export function AdminNotificationsPage() {
  const { getString, getEnum, getPage, setParams, resetParams } = useUrlQueryState({
    q: '',
    type: '',
    role: '',
    read: 'all',
    page: 1,
  })

  const urlQuery = getString('q')
  const rawTypeFilter = getString('type').trim().toUpperCase()
  const typeFilter = NOTIFICATION_TYPE_OPTIONS.some((item) => item.value === rawTypeFilter)
    ? rawTypeFilter
    : ''
  const rawRoleFilter = normalizeAdminRoleValue(getString('role'))
  const roleFilter = ADMIN_ROLE_OPTIONS.some((item) => item.value === rawRoleFilter)
    ? rawRoleFilter
    : ''
  const readFilter: ReadFilter = getEnum('read', READ_VALUES, 'all')
  const page = getPage('page', 1)

  const searchInput = useSearchInput((nextQuery) => {
    setParams({ q: nextQuery })
  }, urlQuery)

  const [notifications, setNotifications] = useState<AdminNotificationItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [unreadTotalCount, setUnreadTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [listProblem, setListProblem] = useState<NormalizedProblem | null>(null)
  const [statusMessage, setStatusMessage] = useState('')
  const requestIdRef = useRef(0)

  // Dispatch FormDialog state
  const [showDispatchDialog, setShowDispatchDialog] = useState(false)
  const [dispatchForm, setDispatchForm] = useState<CreateAdminNotificationPayload>({
    targetScope: 'ALL_ACTIVE',
    targetRole: 'student',
    targetUserId: '',
    type: 'SYSTEM_ANNOUNCEMENT',
    title: '',
    message: '',
  })
  const [dispatchFieldErrors, setDispatchFieldErrors] = useState<
    Partial<Record<DispatchField, string>>
  >({})
  const [dispatchProblem, setDispatchProblem] = useState<NormalizedProblem | null>(null)
  const [isSending, setIsSending] = useState(false)

  // Recipient search inside USER scope
  const [recipientUsers, setRecipientUsers] = useState<AdminUserItem[]>([])
  const [recipientSearch, setRecipientSearch] = useState('')
  const [isLoadingRecipients, setIsLoadingRecipients] = useState(false)
  const [recipientProblem, setRecipientProblem] = useState<NormalizedProblem | null>(null)
  const recipientRequestRef = useRef(0)

  // Delete ConfirmDialog state
  const [deletingNotif, setDeletingNotif] = useState<AdminNotificationItem | null>(null)
  const [deleteProblem, setDeleteProblem] = useState<NormalizedProblem | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  const loadNotifications = useCallback(async () => {
    const requestId = ++requestIdRef.current
    setLoading(true)
    setListProblem(null)
    try {
      const result = await adminService.getNotifications({
        search: urlQuery || undefined,
        type: typeFilter || undefined,
        role: roleFilter || undefined,
        isRead: readFilter === 'all' ? undefined : readFilter === 'read',
        page,
        pageSize: DEFAULT_ADMIN_PAGE_SIZE,
      })
      if (requestId !== requestIdRef.current) return
      setNotifications(result.items ?? [])
      setTotalCount(result.totalCount ?? 0)
      setUnreadTotalCount(result.unreadTotalCount ?? 0)
      setTotalPages(Math.max(1, result.totalPages ?? 1))
    } catch (err: unknown) {
      if (requestId !== requestIdRef.current) return
      setListProblem(extractProblemDetails(err, 'Không thể tải danh sách thông báo 3NF.'))
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [page, readFilter, roleFilter, typeFilter, urlQuery])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadNotifications()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [loadNotifications])

  const loadRecipientUsers = useCallback(async (searchTerm: string) => {
    const requestId = ++recipientRequestRef.current
    setIsLoadingRecipients(true)
    setRecipientProblem(null)
    try {
      const res = await adminService.getUsers({
        search: searchTerm.trim() || undefined,
        isActive: true,
        page: 1,
        pageSize: 30,
      })
      if (requestId !== recipientRequestRef.current) return
      const activeOnly = (res.items ?? []).filter((item) => item.isActive)
      setRecipientUsers(activeOnly)
      setDispatchForm((prev) =>
        prev.targetUserId && !activeOnly.some((candidate) => candidate.id === prev.targetUserId)
          ? { ...prev, targetUserId: '' }
          : prev,
      )
    } catch (err: unknown) {
      if (requestId !== recipientRequestRef.current) return
      setRecipientUsers([])
      setDispatchForm((prev) => (prev.targetUserId ? { ...prev, targetUserId: '' } : prev))
      setRecipientProblem(
        extractProblemDetails(err, 'Không thể tải danh sách tài khoản người nhận.'),
      )
    } finally {
      if (requestId === recipientRequestRef.current) {
        setIsLoadingRecipients(false)
      }
    }
  }, [])

  useEffect(() => {
    if (!showDispatchDialog || dispatchForm.targetScope !== 'USER') return
    const timer = window.setTimeout(() => {
      void loadRecipientUsers(recipientSearch)
    }, 250)
    return () => {
      window.clearTimeout(timer)
      recipientRequestRef.current += 1
    }
  }, [dispatchForm.targetScope, loadRecipientUsers, recipientSearch, showDispatchDialog])

  const hasActiveFilters = Boolean(urlQuery || typeFilter || roleFilter || readFilter !== 'all')

  const handleOpenDispatch = () => {
    setDispatchForm({
      targetScope: 'ALL_ACTIVE',
      targetRole: 'student',
      targetUserId: '',
      type: 'SYSTEM_ANNOUNCEMENT',
      title: '',
      message: '',
    })
    setRecipientSearch('')
    setRecipientUsers([])
    setRecipientProblem(null)
    setDispatchFieldErrors({})
    setDispatchProblem(null)
    setShowDispatchDialog(true)
  }

  const handleDispatch = async (event: FormEvent) => {
    event.preventDefault()
    if (isSending) return

    const errors: Partial<Record<DispatchField, string>> = {}
    if (!dispatchForm.title.trim()) errors.title = 'Vui lòng nhập tiêu đề thông báo.'
    else if (dispatchForm.title.trim().length > 255) {
      errors.title = 'Tiêu đề thông báo tối đa 255 ký tự.'
    }
    if (!dispatchForm.message.trim()) errors.message = 'Vui lòng nhập nội dung thông báo.'
    else if (dispatchForm.message.trim().length > 2000) {
      errors.message = 'Nội dung thông báo tối đa 2.000 ký tự.'
    }
    if (
      dispatchForm.targetScope === 'USER' &&
      (!dispatchForm.targetUserId?.trim() ||
        !recipientUsers.some((candidate) => candidate.id === dispatchForm.targetUserId))
    ) {
      errors.targetUserId = 'Vui lòng chọn một tài khoản đang hoạt động.'
    }

    if (Object.keys(errors).length > 0) {
      setDispatchFieldErrors(errors)
      return
    }

    setIsSending(true)
    setDispatchFieldErrors({})
    setDispatchProblem(null)
    try {
      const result = await adminService.createNotification({
        targetScope: dispatchForm.targetScope,
        targetRole: dispatchForm.targetScope === 'ROLE' ? dispatchForm.targetRole : undefined,
        targetUserId: dispatchForm.targetScope === 'USER' ? dispatchForm.targetUserId : undefined,
        type: dispatchForm.type,
        title: dispatchForm.title.trim(),
        message: dispatchForm.message.trim(),
      })
      const msg = `Đã phát thông báo tới ${result.createdCount.toLocaleString('vi-VN')} tài khoản trong bảng notifications.`
      toast.success(msg)
      setStatusMessage(msg)
      setShowDispatchDialog(false)
      if (page !== 1) {
        setParams({ page: 1 })
      } else {
        void loadNotifications()
      }
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể phát thông báo hệ thống.')
      setDispatchFieldErrors(mapProblemFieldErrors(err, DISPATCH_FIELDS))
      setDispatchProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsSending(false)
    }
  }

  const handleConfirmDelete = async () => {
    if (isDeleting || !deletingNotif) return
    setIsDeleting(true)
    setDeleteProblem(null)
    try {
      await adminService.deleteNotification(deletingNotif.id)
      const msg = 'Đã thu hồi bản ghi thông báo 3NF.'
      toast.success(msg)
      setStatusMessage(msg)
      setDeletingNotif(null)
      if (notifications.length === 1 && page > 1) {
        setParams({ page: page - 1 })
      } else {
        void loadNotifications()
      }
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể thu hồi thông báo.')
      if (parsed.status === 404) {
        toast.info('Thông báo này đã được thu hồi trước đó.')
        setDeletingNotif(null)
        void loadNotifications()
        return
      }
      setDeleteProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div className={ds.page}>
      <PageBanner
        variant="calm"
        icon={Bell}
        eyebrow="Quản trị thông báo · Bảng 3NF notifications"
        title="Thông báo hệ thống"
        description="Theo dõi trạng thái đọc theo từng tài khoản, lọc tức thời theo loại/vai trò và phát thông báo toàn hệ thống, theo vai trò hoặc theo người nhận cụ thể."
        extras={
          <>
            <BannerFact icon={Bell} surface="calm">
              <span className="tabular-nums">Tổng bản ghi: {totalCount.toLocaleString('vi-VN')}</span>
            </BannerFact>
            <BannerFact icon={MailCheck} tone="amber" surface="calm">
              <span className="tabular-nums">Chưa đọc: {unreadTotalCount.toLocaleString('vi-VN')}</span>
            </BannerFact>
          </>
        }
      />

      <AdminStateBanner
        problem={listProblem}
        statusMessage={statusMessage}
        onRetry={listProblem ? () => void loadNotifications() : undefined}
        onDismiss={() => {
          setListProblem(null)
          setStatusMessage('')
        }}
      />

      <section className="overflow-hidden rounded-2xl border border-[#0F2854]/10 bg-white">
        <SectionHeader
          variant="calm"
          icon={Bell}
          title="Danh sách thông báo đã phát"
          badge={<span className="tabular-nums">{totalCount}</span>}
          hint={`Chưa đọc: ${unreadTotalCount.toLocaleString('vi-VN')} · Tự động đồng bộ khi thay đổi bộ lọc`}
          action={
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => void loadNotifications()}
                disabled={loading}
                className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
              >
                <RefreshCw
                  className={cn('size-4 motion-reduce:animate-none', loading && 'animate-spin')}
                  strokeWidth={1.5}
                  aria-hidden="true"
                />
                Làm mới
              </button>
              <button
                type="button"
                onClick={handleOpenDispatch}
                className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-medium text-white transition-colors duration-150 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
              >
                <Send className="size-4" strokeWidth={1.5} aria-hidden="true" />
                Phát thông báo mới
              </button>
            </div>
          }
        />

        {/* Auto-fetching filter bar */}
        <div className="flex flex-col gap-3 border-b border-[#0F2854]/8 bg-white p-4 sm:flex-row sm:flex-wrap sm:items-center sm:p-5">
          <div className="relative min-w-0 flex-1 sm:min-w-[220px]">
            <SearchField
              aria-label="Tìm thông báo theo tiêu đề, nội dung hoặc người nhận"
              placeholder="Tìm tiêu đề, nội dung, email người nhận…"
              value={searchInput.draft}
              onChange={(event) => searchInput.change(event.target.value)}
              onKeyDown={searchInput.onKeyDown}
              className="h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal"
            />
          </div>

          <FilterDropMenu
            label="Loại thông báo"
            value={typeFilter}
            options={TYPE_FILTER_OPTIONS}
            onChange={(value) => setParams({ type: value })}
            className="w-full sm:w-52"
            buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
          />

          <FilterDropMenu
            label="Vai trò nhận"
            value={roleFilter}
            options={ROLE_FILTER_OPTIONS}
            onChange={(value) => setParams({ role: value })}
            className="w-full sm:w-44"
            buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
          />

          <FilterDropMenu
            label="Trạng thái đọc"
            value={readFilter}
            options={[...NOTIFICATION_READ_FILTER_OPTIONS]}
            onChange={(value) => setParams({ read: value })}
            className="w-full sm:w-48"
            buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
          />

          {hasActiveFilters ? (
            <button
              type="button"
              onClick={() => {
                searchInput.reset('')
                resetParams(['q', 'type', 'role', 'read', 'page'])
              }}
              className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#5b6b84] transition-colors duration-150 hover:bg-[#F4FBFD] hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              Xóa bộ lọc
            </button>
          ) : null}
        </div>

        {loading ? (
          <LoadingState label="Đang tải danh sách thông báo…" className="min-h-56" />
        ) : listProblem && notifications.length === 0 ? null : notifications.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={Bell}
              title={hasActiveFilters ? 'Không có thông báo khớp bộ lọc' : 'Chưa có thông báo nào'}
              description={
                hasActiveFilters
                  ? 'Thử điều chỉnh từ khóa hoặc bộ lọc loại thông báo, vai trò và trạng thái đọc.'
                  : 'Bấm Phát thông báo mới để gửi thông báo tới toàn bộ tài khoản hoặc nhóm vai trò.'
              }
              action={
                hasActiveFilters ? (
                  <button
                    type="button"
                    onClick={() => {
                      searchInput.reset('')
                      resetParams(['q', 'type', 'role', 'read', 'page'])
                    }}
                    className="inline-flex min-h-11 items-center rounded-xl border border-[#0F2854]/15 bg-white px-4 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                  >
                    Xóa bộ lọc
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={handleOpenDispatch}
                    className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-medium text-white transition-colors duration-150 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                  >
                    <Send className="size-4" strokeWidth={1.5} aria-hidden="true" />
                    Phát thông báo mới
                  </button>
                )
              }
            />
          </div>
        ) : (
          <>
            {/* Desktop Table */}
            <div className="hidden overflow-x-auto md:block">
              <table className="w-full min-w-[840px] text-left text-sm">
                <thead className="border-b border-[#0F2854]/10 bg-[#F8FBFD] text-xs font-medium text-[#5b6b84]">
                  <tr>
                    <th className="px-5 py-3">Tiêu đề &amp; Nội dung</th>
                    <th className="px-4 py-3">Loại</th>
                    <th className="px-4 py-3">Người nhận</th>
                    <th className="px-4 py-3">Trạng thái</th>
                    <th className="px-4 py-3">Thời điểm</th>
                    <th className="px-5 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#0F2854]/8">
                  {notifications.map((item) => (
                    <tr key={item.id} className="hover:bg-[#F8FBFD]">
                      <td className="max-w-sm px-5 py-3.5">
                        <p className="font-medium text-[#0F2854]">{item.title}</p>
                        <p className="mt-0.5 line-clamp-2 text-xs text-[#5b6b84]">{item.message}</p>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className="inline-flex rounded-md border border-[#0F2854]/10 bg-[#F4FBFD] px-2.5 py-1 text-xs font-medium text-[#1C4D8D]">
                          {getNotificationTypeLabel(item.type)}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <p className="text-xs font-medium text-[#0F2854]">{item.userFullName}</p>
                        <p className="mt-0.5 text-xs text-[#5b6b84]">
                          {item.userEmail} · {getAdminRoleShortLabel(item.userRole)}
                        </p>
                      </td>
                      <td className="px-4 py-3.5">
                        <span
                          className={cn(
                            'inline-flex rounded-md border px-2 py-0.5 text-xs font-medium',
                            item.isRead
                              ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                              : 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]',
                          )}
                        >
                          {item.isRead ? 'Đã đọc' : 'Chưa đọc'}
                        </span>
                      </td>
                      <td className="px-4 py-3.5 text-xs text-[#5b6b84] tabular-nums">
                        {formatDateTime(item.createdAt)}
                      </td>
                      <td className="px-5 py-3.5 text-right">
                        <button
                          type="button"
                          onClick={() => {
                            setDeletingNotif(item)
                            setDeleteProblem(null)
                          }}
                          className="inline-flex min-h-9 items-center gap-1 rounded-lg border border-[#f3c2c8] bg-[#fdecec] px-2.5 text-xs font-medium text-[#c2415b] transition-colors duration-150 hover:bg-[#f9dce0] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                        >
                          <Trash2 className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                          Thu hồi
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile Card List */}
            <div className="space-y-3 p-4 md:hidden">
              {notifications.map((item) => (
                <article
                  key={item.id}
                  className="rounded-xl border border-[#0F2854]/10 bg-white p-4"
                >
                  <div className="flex items-start justify-between gap-2">
                    <p className="min-w-0 break-words [overflow-wrap:anywhere] text-sm font-semibold text-[#0F2854]">
                      {item.title}
                    </p>
                    <span
                      className={cn(
                        'inline-flex shrink-0 rounded-md border px-2 py-0.5 text-xs font-medium',
                        item.isRead
                          ? 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
                          : 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]',
                      )}
                    >
                      {item.isRead ? 'Đã đọc' : 'Chưa đọc'}
                    </span>
                  </div>
                  <p className="mt-1.5 break-words [overflow-wrap:anywhere] text-xs leading-relaxed text-[#5b6b84]">
                    {item.message}
                  </p>
                  <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-[#5b6b84]">
                    <span className="rounded-md border border-[#0F2854]/10 bg-[#F4FBFD] px-2 py-0.5 font-medium text-[#1C4D8D]">
                      {getNotificationTypeLabel(item.type)}
                    </span>
                    <span className="tabular-nums">{formatDateTime(item.createdAt)}</span>
                  </div>
                  <p className="mt-2 break-words [overflow-wrap:anywhere] text-xs text-[#0F2854]">
                    {item.userFullName} (<span className="break-all">{item.userEmail}</span>)
                  </p>
                  <button
                    type="button"
                    onClick={() => {
                      setDeletingNotif(item)
                      setDeleteProblem(null)
                    }}
                    className="mt-3 inline-flex min-h-11 w-full items-center justify-center gap-1.5 rounded-xl border border-[#f3c2c8] bg-[#fdecec] px-3 text-xs font-medium text-[#c2415b] transition-colors duration-150 hover:bg-[#f9dce0] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                  >
                    <Trash2 className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                    Thu hồi thông báo
                  </button>
                </article>
              ))}
            </div>

            {totalPages > 1 ? (
              <ListPagination
                label="Phân trang thông báo"
                page={page}
                pageCount={totalPages}
                onPageChange={(nextPage) => setParams({ page: nextPage })}
              />
            ) : null}
          </>
        )}
      </section>

      {/* Dispatch FormDialog */}
      <FormDialog
        open={showDispatchDialog}
        onClose={() => {
          if (!isSending) setShowDispatchDialog(false)
        }}
        busy={isSending}
        variant="calm"
        icon={Send}
        eyebrow="Phát thông báo 3NF"
        title="Phát thông báo hệ thống mới"
        size="lg"
        footer={
          <SubmitDialogFooter
            form="admin-dispatch-notification-form"
            onCancel={() => setShowDispatchDialog(false)}
            label="Gửi thông báo"
            busyLabel="Đang phát…"
            busy={isSending}
            icon={Send}
            variant="calm"
          />
        }
      >
        <form
          id="admin-dispatch-notification-form"
          noValidate
          onSubmit={handleDispatch}
          className="space-y-4"
        >
          {dispatchProblem ? <AdminStateBanner problem={dispatchProblem} /> : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label
                htmlFor="notif-target-scope"
                className="block text-xs font-medium text-[#0F2854]"
              >
                Phạm vi nhận
              </label>
              <select
                id="notif-target-scope"
                value={dispatchForm.targetScope}
                onChange={(event) => {
                  const nextScope = event.target.value as CreateAdminNotificationPayload['targetScope']
                  setDispatchForm((prev) => ({
                    ...prev,
                    targetScope: nextScope,
                    targetUserId: nextScope === 'USER' ? prev.targetUserId : '',
                  }))
                  setDispatchFieldErrors((prev) => ({ ...prev, targetScope: undefined, targetUserId: undefined }))
                }}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                )}
              >
                {NOTIFICATION_SCOPE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label htmlFor="notif-type" className="block text-xs font-medium text-[#0F2854]">
                Loại thông báo
              </label>
              <select
                id="notif-type"
                value={dispatchForm.type}
                onChange={(event) => {
                  setDispatchForm((prev) => ({ ...prev, type: event.target.value }))
                  setDispatchFieldErrors((prev) => ({ ...prev, type: undefined }))
                }}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                )}
              >
                {NOTIFICATION_TYPE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {dispatchForm.targetScope === 'ROLE' ? (
            <div>
              <label htmlFor="notif-target-role" className="block text-xs font-medium text-[#0F2854]">
                Vai trò nhận thông báo
              </label>
              <select
                id="notif-target-role"
                value={dispatchForm.targetRole ?? 'student'}
                onChange={(event) =>
                  setDispatchForm((prev) => ({ ...prev, targetRole: event.target.value }))
                }
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                )}
              >
                {ADMIN_ROLE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <FieldError message={dispatchFieldErrors.targetRole} />
            </div>
          ) : null}

          {dispatchForm.targetScope === 'USER' ? (
            <div className="space-y-3 rounded-xl border border-[#0F2854]/10 bg-white p-4">
              <div>
                <label
                  htmlFor="notif-recipient-search"
                  className="block text-xs font-medium text-[#0F2854]"
                >
                  Tìm tài khoản người nhận (chỉ tài khoản đang hoạt động)
                </label>
                <div className="mt-1.5 flex gap-2">
                  <input
                    id="notif-recipient-search"
                    type="text"
                    value={recipientSearch}
                    onChange={(event) => setRecipientSearch(event.target.value)}
                    placeholder="Nhập tên, email hoặc MSSV…"
                    className={cn(
                      ds.field,
                      'h-11 min-h-11 flex-1 rounded-xl border-[#0F2854]/15 font-normal',
                    )}
                  />
                  <button
                    type="button"
                    onClick={() => void loadRecipientUsers(recipientSearch)}
                    disabled={isLoadingRecipients}
                    className="inline-flex min-h-11 shrink-0 items-center gap-1.5 rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
                  >
                    <Search className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                    Tìm
                  </button>
                </div>
              </div>

              {recipientProblem ? (
                <AdminStateBanner
                  problem={recipientProblem}
                  onRetry={() => void loadRecipientUsers(recipientSearch)}
                />
              ) : null}

              {isLoadingRecipients ? (
                <LoadingState label="Đang tìm người nhận…" className="min-h-16" />
              ) : (
                <div>
                  <label
                    htmlFor="notif-target-user"
                    className="block text-xs font-medium text-[#0F2854]"
                  >
                    Chọn người nhận
                  </label>
                  <select
                    id="notif-target-user"
                    value={dispatchForm.targetUserId ?? ''}
                    onChange={(event) => {
                      setDispatchForm((prev) => ({ ...prev, targetUserId: event.target.value }))
                      setDispatchFieldErrors((prev) => ({ ...prev, targetUserId: undefined }))
                    }}
                    aria-invalid={Boolean(dispatchFieldErrors.targetUserId)}
                    className={cn(
                      ds.field,
                      'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                      dispatchFieldErrors.targetUserId && fieldInvalidClass,
                    )}
                  >
                    <option value="">
                      {recipientUsers.length === 0
                        ? 'Không có tài khoản hoạt động phù hợp'
                        : 'Chọn một tài khoản…'}
                    </option>
                    {recipientUsers.map((candidate) => (
                      <option key={candidate.id} value={candidate.id}>
                        {candidate.fullName} ({candidate.email} · {getAdminRoleShortLabel(candidate.role)})
                      </option>
                    ))}
                  </select>
                  <FieldError message={dispatchFieldErrors.targetUserId} />
                </div>
              )}
            </div>
          ) : null}

          <div>
            <div className="flex items-center justify-between gap-2">
              <label htmlFor="notif-title" className="block text-xs font-medium text-[#0F2854]">
                Tiêu đề thông báo
              </label>
              <span className="text-xs text-[#5b6b84] tabular-nums">
                {dispatchForm.title.length}/255
              </span>
            </div>
            <input
              id="notif-title"
              type="text"
              maxLength={255}
              value={dispatchForm.title}
              onChange={(event) => {
                setDispatchForm((prev) => ({ ...prev, title: event.target.value }))
                setDispatchFieldErrors((prev) => ({ ...prev, title: undefined }))
              }}
              placeholder="VD: Thông báo bảo trì hệ thống"
              aria-invalid={Boolean(dispatchFieldErrors.title)}
              className={cn(
                ds.field,
                'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal',
                dispatchFieldErrors.title && fieldInvalidClass,
              )}
            />
            <FieldError message={dispatchFieldErrors.title} />
          </div>

          <div>
            <div className="flex items-center justify-between gap-2">
              <label htmlFor="notif-message" className="block text-xs font-medium text-[#0F2854]">
                Nội dung thông báo
              </label>
              <span className="text-xs text-[#5b6b84] tabular-nums">
                {dispatchForm.message.length}/2000
              </span>
            </div>
            <textarea
              id="notif-message"
              maxLength={2000}
              value={dispatchForm.message}
              onChange={(event) => {
                setDispatchForm((prev) => ({ ...prev, message: event.target.value }))
                setDispatchFieldErrors((prev) => ({ ...prev, message: undefined }))
              }}
              placeholder="Nhập nội dung chi tiết thông báo…"
              aria-invalid={Boolean(dispatchFieldErrors.message)}
              className={cn(
                'mt-1.5 min-h-28 w-full rounded-xl border border-[#0F2854]/15 bg-white p-3 text-sm text-[#0F2854] outline-none focus:border-[#1C4D8D] focus:ring-2 focus:ring-[#1C4D8D]/15',
                dispatchFieldErrors.message && fieldInvalidClass,
              )}
            />
            <FieldError message={dispatchFieldErrors.message} />
          </div>
        </form>
      </FormDialog>

      {/* Delete ConfirmDialog */}
      {deletingNotif ? (
        <ConfirmDialog
          tone="danger"
          variant="calm"
          icon={Trash2}
          eyebrow="Thu hồi bản ghi 3NF"
          title="Thu hồi thông báo đã phát?"
          message={`Thông báo "${deletingNotif.title}" gửi tới ${deletingNotif.userEmail} sẽ bị xóa và ghi nhật ký NOTIFICATION_DELETED.`}
          confirmLabel="Xác nhận thu hồi"
          cancelLabel="Hủy"
          pending={isDeleting}
          onCancel={() => {
            if (!isDeleting) {
              setDeletingNotif(null)
              setDeleteProblem(null)
            }
          }}
          onConfirm={() => void handleConfirmDelete()}
        >
          {deleteProblem ? <AdminStateBanner problem={deleteProblem} /> : null}
        </ConfirmDialog>
      ) : null}
    </div>
  )
}
