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
import { AdminPageHeader } from '@/pages/admin/components/AdminPageHeader'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { adminUi } from '@/pages/admin/components/adminUi'

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
      setListProblem(extractProblemDetails(err, 'Không thể tải danh sách thông báo.'))
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
      const msg = `Đã phát thông báo tới ${result.createdCount.toLocaleString('vi-VN')} tài khoản.`
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
      const msg = 'Đã thu hồi thông báo.'
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
      <AdminPageHeader
        icon={Bell}
        title="Thông báo hệ thống"
        count={totalCount}
        description="Gửi thông báo tới toàn bộ tài khoản, theo vai trò hoặc người nhận cụ thể và theo dõi trạng thái đọc."
        meta={
          <span className={cn(adminUi.badgeWarning, 'tabular-nums')}>
            <MailCheck className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
            Chưa đọc: {unreadTotalCount.toLocaleString('vi-VN')}
          </span>
        }
        actions={
          <>
            <button
              type="button"
              onClick={() => void loadNotifications()}
              disabled={loading}
              className={adminUi.btnSecondary}
            >
              <RefreshCw
                className={cn('size-4 motion-reduce:animate-none', loading && 'animate-spin')}
                strokeWidth={1.75}
                aria-hidden="true"
              />
              Làm mới
            </button>
            <button type="button" onClick={handleOpenDispatch} className={adminUi.btnPrimary}>
              <Send className="size-4" strokeWidth={1.75} aria-hidden="true" />
              Phát thông báo mới
            </button>
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

      <section className={adminUi.card}>
        {/* Auto-fetching filter bar */}
        <div className={adminUi.toolbar}>
          <div className="relative min-w-0 flex-1 sm:min-w-[220px]">
            <SearchField
              aria-label="Tìm thông báo theo tiêu đề, nội dung hoặc người nhận"
              placeholder="Tìm tiêu đề, nội dung, email người nhận…"
              value={searchInput.draft}
              onChange={(event) => searchInput.change(event.target.value)}
              onKeyDown={searchInput.onKeyDown}
              className={adminUi.input}
            />
          </div>

          <FilterDropMenu
            label="Loại thông báo"
            value={typeFilter}
            options={TYPE_FILTER_OPTIONS}
            onChange={(value) => setParams({ type: value })}
            className="w-full sm:w-52"
            buttonClassName={adminUi.filterButton}
          />

          <FilterDropMenu
            label="Vai trò nhận"
            value={roleFilter}
            options={ROLE_FILTER_OPTIONS}
            onChange={(value) => setParams({ role: value })}
            className="w-full sm:w-44"
            buttonClassName={adminUi.filterButton}
          />

          <FilterDropMenu
            label="Trạng thái đọc"
            value={readFilter}
            options={[...NOTIFICATION_READ_FILTER_OPTIONS]}
            onChange={(value) => setParams({ read: value })}
            className="w-full sm:w-48"
            buttonClassName={adminUi.filterButton}
          />

          {hasActiveFilters ? (
            <button
              type="button"
              onClick={() => {
                searchInput.reset('')
                resetParams(['q', 'type', 'role', 'read', 'page'])
              }}
              className={adminUi.btnSubtle}
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
                    className={adminUi.btnSecondary}
                  >
                    Xóa bộ lọc
                  </button>
                ) : (
                  <button type="button" onClick={handleOpenDispatch} className={adminUi.btnPrimary}>
                    <Send className="size-4" strokeWidth={1.75} aria-hidden="true" />
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
              <table className={cn(adminUi.table, 'min-w-[840px]')}>
                <thead className={adminUi.thead}>
                  <tr>
                    <th className="px-5 py-3.5">Tiêu đề &amp; Nội dung</th>
                    <th className="px-4 py-3.5">Loại</th>
                    <th className="px-4 py-3.5">Người nhận</th>
                    <th className="px-4 py-3.5">Trạng thái</th>
                    <th className="px-4 py-3.5">Thời điểm</th>
                    <th className="px-5 py-3.5 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className={adminUi.tbody}>
                  {notifications.map((item) => (
                    <tr key={item.id} className={adminUi.tr}>
                      <td className="max-w-sm px-5 py-3.5">
                        <p className="text-sm font-semibold text-[#0F2854]">{item.title}</p>
                        <p className="mt-0.5 line-clamp-2 text-[13px] text-slate-600">
                          {item.message}
                        </p>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className={adminUi.badgeInfo}>
                          {getNotificationTypeLabel(item.type)}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <p className="text-sm font-semibold text-[#0F2854]">{item.userFullName}</p>
                        <p className="mt-0.5 text-[13px] text-slate-600">
                          {item.userEmail} · {getAdminRoleShortLabel(item.userRole)}
                        </p>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className={item.isRead ? adminUi.badgeSuccess : adminUi.badgeWarning}>
                          {item.isRead ? 'Đã đọc' : 'Chưa đọc'}
                        </span>
                      </td>
                      <td className="px-4 py-3.5 text-[13px] font-medium text-slate-600 tabular-nums">
                        {formatDateTime(item.createdAt)}
                      </td>
                      <td className="px-5 py-3.5 text-right">
                        <button
                          type="button"
                          onClick={() => {
                            setDeletingNotif(item)
                            setDeleteProblem(null)
                          }}
                          className={adminUi.btnRowDanger}
                        >
                          <Trash2 className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
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
                <article key={item.id} className={adminUi.mobileCard}>
                  <div className="flex items-start justify-between gap-2">
                    <p className="min-w-0 break-words [overflow-wrap:anywhere] text-sm font-bold text-[#0F2854]">
                      {item.title}
                    </p>
                    <span
                      className={cn(
                        'shrink-0',
                        item.isRead ? adminUi.badgeSuccess : adminUi.badgeWarning,
                      )}
                    >
                      {item.isRead ? 'Đã đọc' : 'Chưa đọc'}
                    </span>
                  </div>
                  <p className="mt-1.5 break-words [overflow-wrap:anywhere] text-sm leading-relaxed text-slate-700">
                    {item.message}
                  </p>
                  <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-slate-600">
                    <span className={adminUi.badgeInfo}>
                      {getNotificationTypeLabel(item.type)}
                    </span>
                    <span className="font-medium tabular-nums">
                      {formatDateTime(item.createdAt)}
                    </span>
                  </div>
                  <p className="mt-2 break-words [overflow-wrap:anywhere] text-[13px] font-medium text-[#0F2854]">
                    {item.userFullName} (<span className="break-all text-slate-600">{item.userEmail}</span>)
                  </p>
                  <button
                    type="button"
                    onClick={() => {
                      setDeletingNotif(item)
                      setDeleteProblem(null)
                    }}
                    className={cn(adminUi.btnMobileDanger, 'mt-3 w-full')}
                  >
                    <Trash2 className="size-4" strokeWidth={1.75} aria-hidden="true" />
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
              <label htmlFor="notif-target-scope" className={adminUi.label}>
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
                className={cn(ds.field, adminUi.input, 'mt-1.5')}
              >
                {NOTIFICATION_SCOPE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label htmlFor="notif-type" className={adminUi.label}>
                Loại thông báo
              </label>
              <select
                id="notif-type"
                value={dispatchForm.type}
                onChange={(event) => {
                  setDispatchForm((prev) => ({ ...prev, type: event.target.value }))
                  setDispatchFieldErrors((prev) => ({ ...prev, type: undefined }))
                }}
                className={cn(ds.field, adminUi.input, 'mt-1.5')}
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
              <label htmlFor="notif-target-role" className={adminUi.label}>
                Vai trò nhận thông báo
              </label>
              <select
                id="notif-target-role"
                value={dispatchForm.targetRole ?? 'student'}
                onChange={(event) =>
                  setDispatchForm((prev) => ({ ...prev, targetRole: event.target.value }))
                }
                className={cn(ds.field, adminUi.input, 'mt-1.5')}
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
            <div className="space-y-3 rounded-xl border border-slate-300 bg-[#f7fafd] p-4">
              <div>
                <label htmlFor="notif-recipient-search" className={adminUi.label}>
                  Tìm tài khoản người nhận (chỉ tài khoản đang hoạt động)
                </label>
                <div className="mt-1.5 flex gap-2">
                  <input
                    id="notif-recipient-search"
                    type="text"
                    value={recipientSearch}
                    onChange={(event) => setRecipientSearch(event.target.value)}
                    placeholder="Nhập tên, email hoặc MSSV…"
                    className={cn(ds.field, adminUi.input, 'flex-1')}
                  />
                  <button
                    type="button"
                    onClick={() => void loadRecipientUsers(recipientSearch)}
                    disabled={isLoadingRecipients}
                    className={cn(adminUi.btnSecondary, 'shrink-0')}
                  >
                    <Search className="size-4" strokeWidth={1.75} aria-hidden="true" />
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
                  <label htmlFor="notif-target-user" className={adminUi.label}>
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
                      adminUi.input,
                      'mt-1.5',
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
              <label htmlFor="notif-title" className={adminUi.label}>
                Tiêu đề thông báo
              </label>
              <span className="text-xs font-medium text-slate-600 tabular-nums">
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
                adminUi.input,
                'mt-1.5',
                dispatchFieldErrors.title && fieldInvalidClass,
              )}
            />
            <FieldError message={dispatchFieldErrors.title} />
          </div>

          <div>
            <div className="flex items-center justify-between gap-2">
              <label htmlFor="notif-message" className={adminUi.label}>
                Nội dung thông báo
              </label>
              <span className="text-xs font-medium text-slate-600 tabular-nums">
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
                adminUi.textarea,
                'min-h-28',
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
          title="Thu hồi thông báo đã phát?"
          message={`Thông báo "${deletingNotif.title}" gửi tới ${deletingNotif.userEmail} sẽ bị xóa và ghi nhận vào nhật ký hệ thống.`}
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
