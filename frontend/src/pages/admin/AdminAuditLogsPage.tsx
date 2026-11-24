import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Eye, FileText, RefreshCw, UserRound } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu, type FilterDropMenuOption } from '@/components/common/FilterDropMenu'
import { ListPagination } from '@/components/common/ListPagination'
import { LoadingState } from '@/components/common/LoadingState'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { useUrlQueryState } from '@/hooks/useUrlQueryState'
import { extractProblemDetails, type NormalizedProblem } from '@/lib/problemDetails'
import { scheduleUi } from '@/lib/scheduleUi'
import { cn } from '@/lib/utils'
import {
  adminService,
  DEFAULT_ADMIN_PAGE_SIZE,
  type AdminUserItem,
  type AuditLogItem,
} from '@/services/admin.service'
import { AUDIT_ACTION_PRESETS, AUDIT_ENTITY_PRESETS } from '@/pages/admin/adminConstants'
import {
  buildAuditDiffSummary,
  summarizeAuditDiff,
  type AuditDiffChangeKind,
} from '@/pages/admin/auditLogDiff'
import { AdminPageHeader } from '@/pages/admin/components/AdminPageHeader'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { adminUi } from '@/pages/admin/components/adminUi'

function formatDateTime(raw: string): string {
  const parsed = new Date(raw)
  return Number.isNaN(parsed.getTime()) ? raw : parsed.toLocaleString('vi-VN')
}

function getActionBadgeClass(action: string): string {
  const upper = action.toUpperCase()
  if (
    upper.includes('CREATED') ||
    upper.includes('CREATE') ||
    upper.includes('IMPORTED') ||
    upper.includes('DISPATCHED')
  ) {
    return adminUi.badgeSuccess
  }
  if (upper.includes('DELETED') || upper.includes('DELETE') || upper.includes('RESET')) {
    return adminUi.badgeDanger
  }
  return adminUi.badgeInfo
}

const CHANGE_BADGE_META: Record<AuditDiffChangeKind, { label: string; className: string }> = {
  added: {
    label: 'Thêm mới',
    className: adminUi.badgeSuccess,
  },
  changed: {
    label: 'Thay đổi',
    className: adminUi.badgeWarning,
  },
  removed: {
    label: 'Đã xóa',
    className: adminUi.badgeDanger,
  },
  unchanged: {
    label: 'Không đổi',
    className: adminUi.badgeNeutral,
  },
}

export function AdminAuditLogsPage() {
  const { getString, getPage, setParams, resetParams } = useUrlQueryState({
    entity: '',
    action: '',
    userId: '',
    page: 1,
  })

  const entityFilter = getString('entity')
  const actionFilter = getString('action')
  const userIdFilter = getString('userId')
  const page = getPage('page', 1)

  const [logs, setLogs] = useState<AuditLogItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(0)
  const [loading, setLoading] = useState(true)
  const [problem, setProblem] = useState<NormalizedProblem | null>(null)
  const requestIdRef = useRef(0)

  // Custom exact inputs synced with URL filters
  const [customEntity, setCustomEntity] = useState(entityFilter)
  const [customAction, setCustomAction] = useState(actionFilter)

  useEffect(() => {
    return scheduleUi(() => {
      setCustomEntity(entityFilter)
    })
  }, [entityFilter])

  useEffect(() => {
    return scheduleUi(() => {
      setCustomAction(actionFilter)
    })
  }, [actionFilter])

  // Actor options for userId FilterDropMenu
  const [actors, setActors] = useState<AdminUserItem[]>([])
  const [actorsLoading, setActorsLoading] = useState(false)
  const [actorsError, setActorsError] = useState(false)
  const actorsRequestRef = useRef(0)

  // Diff modal
  const [selectedLog, setSelectedLog] = useState<AuditLogItem | null>(null)
  const [showRawPayload, setShowRawPayload] = useState(false)

  const loadActors = useCallback(async () => {
    const requestId = ++actorsRequestRef.current
    setActorsLoading(true)
    setActorsError(false)
    try {
      const res = await adminService.getUsers({ page: 1, pageSize: 100 })
      if (requestId !== actorsRequestRef.current) return
      setActors(res.items ?? [])
    } catch {
      if (requestId !== actorsRequestRef.current) return
      setActors([])
      setActorsError(true)
    } finally {
      if (requestId === actorsRequestRef.current) {
        setActorsLoading(false)
      }
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadActors()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      actorsRequestRef.current += 1
    }
  }, [loadActors])

  const loadLogs = useCallback(async () => {
    const requestId = ++requestIdRef.current
    setLoading(true)
    setProblem(null)
    try {
      const result = await adminService.getAuditLogs({
        entityName: entityFilter || undefined,
        action: actionFilter || undefined,
        userId: userIdFilter || undefined,
        page,
        pageSize: DEFAULT_ADMIN_PAGE_SIZE,
      })
      if (requestId !== requestIdRef.current) return
      setLogs(result.items ?? [])
      setTotalCount(result.totalCount ?? 0)
      setTotalPages(result.totalPages ?? 0)
    } catch (err: unknown) {
      if (requestId !== requestIdRef.current) return
      setProblem(extractProblemDetails(err, 'Không thể tải nhật ký kiểm toán.'))
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [actionFilter, entityFilter, page, userIdFilter])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadLogs()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [loadLogs])

  const actorOptions: FilterDropMenuOption[] = useMemo(() => {
    const hasCustomActor =
      Boolean(userIdFilter) && !actors.some((actor) => actor.id === userIdFilter)
    return [
      {
        value: '',
        label: actorsLoading
          ? 'Đang tải danh sách…'
          : actorsError
            ? 'Tất cả người thực hiện (không tải được danh bạ)'
            : 'Tất cả người thực hiện',
      },
      ...actors.map((actor) => ({
        value: actor.id,
        label: `${actor.fullName} (${actor.email})`,
      })),
      ...(hasCustomActor
        ? [{ value: userIdFilter, label: `ID: ${userIdFilter} (tùy chỉnh)` }]
        : []),
    ]
  }, [actors, actorsError, actorsLoading, userIdFilter])

  const entityMenuOptions: FilterDropMenuOption[] = useMemo(() => {
    const hasCustom =
      Boolean(entityFilter) && !AUDIT_ENTITY_PRESETS.some((item) => item.value === entityFilter)
    return [
      ...AUDIT_ENTITY_PRESETS,
      ...(hasCustom ? [{ value: entityFilter, label: `${entityFilter} (tùy chỉnh)` }] : []),
    ]
  }, [entityFilter])

  const actionMenuOptions: FilterDropMenuOption[] = useMemo(() => {
    const hasCustom =
      Boolean(actionFilter) && !AUDIT_ACTION_PRESETS.some((item) => item.value === actionFilter)
    return [
      ...AUDIT_ACTION_PRESETS,
      ...(hasCustom ? [{ value: actionFilter, label: `${actionFilter} (tùy chỉnh)` }] : []),
    ]
  }, [actionFilter])

  const hasActiveFilters = Boolean(entityFilter || actionFilter || userIdFilter)

  const activeDiff = useMemo(
    () =>
      selectedLog
        ? buildAuditDiffSummary(selectedLog.oldValues, selectedLog.newValues)
        : null,
    [selectedLog],
  )

  return (
    <div className={ds.page}>
      <AdminPageHeader
        icon={FileText}
        title="Nhật ký kiểm toán hệ thống"
        count={totalCount}
        description="Tra cứu lịch sử thay đổi dữ liệu theo đối tượng, hành động hoặc người thực hiện và xem chi tiết Trước / Sau."
        actions={
          <button
            type="button"
            onClick={() => {
              void loadLogs()
              if (actorsError) {
                void loadActors()
              }
            }}
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
        }
      />

      <AdminStateBanner
        problem={problem}
        onRetry={problem ? () => void loadLogs() : undefined}
        onDismiss={() => setProblem(null)}
      />

      <section className={adminUi.card}>
        {/* Filter Bar */}
        <div className="space-y-3 border-b border-slate-200 bg-[#f8fbfe] p-4 sm:p-5">
          <div className="grid gap-3 md:grid-cols-3">
            <FilterDropMenu
              label="Thực thể (entity_name)"
              value={entityFilter}
              options={entityMenuOptions}
              onChange={(nextEntity) => {
                setCustomEntity(nextEntity)
                setParams({ entity: nextEntity })
              }}
              wrapOptions
              buttonClassName={adminUi.filterButton}
            />

            <FilterDropMenu
              label="Hành động (action)"
              value={actionFilter}
              options={actionMenuOptions}
              onChange={(nextAction) => {
                setCustomAction(nextAction)
                setParams({ action: nextAction })
              }}
              wrapOptions
              buttonClassName={adminUi.filterButton}
            />

            <FilterDropMenu
              label="Người thực hiện"
              value={userIdFilter}
              options={actorOptions}
              onChange={(nextUserId) => setParams({ userId: nextUserId })}
              wrapOptions
              buttonClassName={adminUi.filterButton}
            />
          </div>

          {actorsError ? (
            <div
              role="status"
              className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-amber-400 bg-amber-50 px-3.5 py-2.5 text-xs font-medium text-amber-900"
            >
              <span>Không tải được danh sách người thực hiện để lọc nhanh; nhật ký kiểm toán vẫn hiển thị bình thường.</span>
              <button
                type="button"
                onClick={() => void loadActors()}
                disabled={actorsLoading}
                className="inline-flex min-h-11 items-center rounded-lg px-2 font-semibold text-[#1C4D8D] underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50"
              >
                Tải lại danh bạ
              </button>
            </div>
          ) : null}

          <form
            onSubmit={(event) => {
              event.preventDefault()
              setParams({
                entity: customEntity.trim(),
                action: customAction.trim().toUpperCase(),
              })
            }}
            className="flex flex-col gap-2 sm:flex-row sm:items-center"
          >
            <input
              aria-label="Nhập chính xác entity_name"
              type="text"
              value={customEntity}
              onChange={(event) => setCustomEntity(event.target.value)}
              placeholder="Nhập mã bảng tùy chỉnh (vd: users)"
              className={cn(
                ds.field,
                adminUi.input,
                'flex-1 font-mono placeholder:font-sans',
              )}
            />
            <input
              aria-label="Nhập chính xác action"
              type="text"
              value={customAction}
              onChange={(event) => setCustomAction(event.target.value)}
              placeholder="Nhập mã hành động tùy chỉnh (vd: USER_CREATED)"
              className={cn(
                ds.field,
                adminUi.input,
                'flex-1 font-mono uppercase placeholder:font-sans placeholder:normal-case',
              )}
            />
            <button type="submit" className={cn(adminUi.btnPrimary, 'shrink-0')}>
              Áp dụng mã
            </button>
            {hasActiveFilters ? (
              <button
                type="button"
                onClick={() => {
                  setCustomEntity('')
                  setCustomAction('')
                  resetParams(['entity', 'action', 'userId', 'page'])
                }}
                className={cn(adminUi.btnSubtle, 'shrink-0')}
              >
                Xóa bộ lọc
              </button>
            ) : null}
          </form>
        </div>

        {loading ? (
          <LoadingState label="Đang tải nhật ký kiểm toán…" className="min-h-56" />
        ) : problem && logs.length === 0 ? null : logs.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={FileText}
              title={
                hasActiveFilters
                  ? 'Không tìm thấy bản ghi kiểm toán phù hợp'
                  : 'Chưa có nhật ký kiểm toán'
              }
              description={
                hasActiveFilters
                  ? 'Bộ lọc đối tượng và hành động so khớp chính xác theo mã.'
                  : 'Mọi thay đổi tài khoản, học kỳ, cấu hình và thông báo sẽ được ghi nhận tại đây.'
              }
              action={
                hasActiveFilters ? (
                  <button
                    type="button"
                    onClick={() => {
                      setCustomEntity('')
                      setCustomAction('')
                      resetParams(['entity', 'action', 'userId', 'page'])
                    }}
                    className={adminUi.btnSecondary}
                  >
                    Xóa bộ lọc
                  </button>
                ) : undefined
              }
            />
          </div>
        ) : (
          <>
            {/* Desktop Table */}
            <div className="hidden overflow-x-auto md:block">
              <table className={cn(adminUi.table, 'min-w-[860px]')}>
                <thead className={adminUi.thead}>
                  <tr>
                    <th className="px-5 py-3.5">Thời điểm</th>
                    <th className="px-4 py-3.5">Người thực hiện</th>
                    <th className="px-4 py-3.5">Hành động</th>
                    <th className="px-4 py-3.5">Thực thể</th>
                    <th className="px-4 py-3.5">Tóm tắt thay đổi</th>
                    <th className="px-5 py-3.5 text-right">Chi tiết</th>
                  </tr>
                </thead>
                <tbody className={adminUi.tbody}>
                  {logs.map((log) => (
                    <tr key={log.id} className={adminUi.tr}>
                      <td className="px-5 py-3.5 text-[13px] font-medium whitespace-nowrap text-slate-700 tabular-nums">
                        {formatDateTime(log.createdAt)}
                      </td>
                      <td className="px-4 py-3.5">
                        <p className="text-sm font-semibold text-[#0F2854]">
                          {log.userFullName || 'Hệ thống'}
                        </p>
                        <p className="mt-0.5 text-[13px] text-slate-600">
                          {log.userEmail || log.ipAddress || '—'}
                        </p>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className={cn(getActionBadgeClass(log.action), 'font-mono')}>
                          {log.action}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <p className="font-mono text-[13px] font-semibold text-[#0F2854]">
                          {log.entityName}
                        </p>
                        {log.entityId ? (
                          <p className="mt-0.5 max-w-[140px] truncate font-mono text-xs text-slate-600">
                            {log.entityId}
                          </p>
                        ) : null}
                      </td>
                      <td className="max-w-xs px-4 py-3.5 text-[13px] text-slate-700">
                        <p className="truncate">{summarizeAuditDiff(log.oldValues, log.newValues)}</p>
                      </td>
                      <td className="px-5 py-3.5 text-right">
                        <button
                          type="button"
                          aria-label={`Xem diff ${log.action} ${log.entityName}`}
                          onClick={() => {
                            setSelectedLog(log)
                            setShowRawPayload(false)
                          }}
                          className={adminUi.btnRowPrimary}
                        >
                          <Eye className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
                          Xem diff
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile Card List */}
            <div className="space-y-3 p-4 md:hidden">
              {logs.map((log) => (
                <article key={log.id} className={adminUi.mobileCard}>
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className={cn(getActionBadgeClass(log.action), 'font-mono')}>
                      {log.action}
                    </span>
                    <span className="text-xs font-medium text-slate-600 tabular-nums">
                      {formatDateTime(log.createdAt)}
                    </span>
                  </div>
                  <p className="mt-2 break-all [overflow-wrap:anywhere] font-mono text-sm font-semibold text-[#0F2854]">
                    {log.entityName}
                    {log.entityId ? ` · ${log.entityId}` : ''}
                  </p>
                  <p className="mt-1 flex items-start gap-1.5 text-[13px] text-slate-600">
                    <UserRound className="mt-0.5 size-4 shrink-0" strokeWidth={1.75} aria-hidden="true" />
                    <span className="min-w-0 break-all [overflow-wrap:anywhere]">
                      {log.userFullName || 'Hệ thống'} ({log.userEmail || '—'})
                    </span>
                  </p>
                  <p className="mt-2 text-[13px] text-slate-700">
                    {summarizeAuditDiff(log.oldValues, log.newValues)}
                  </p>
                  <button
                    type="button"
                    aria-label={`Xem diff ${log.action} ${log.entityName}`}
                    onClick={() => {
                      setSelectedLog(log)
                      setShowRawPayload(false)
                    }}
                    className={cn(adminUi.btnMobilePrimary, 'mt-3 w-full')}
                  >
                    <Eye className="size-4" strokeWidth={1.75} aria-hidden="true" />
                    Xem diff
                  </button>
                </article>
              ))}
            </div>

            {totalPages > 1 ? (
              <ListPagination
                label="Phân trang nhật ký kiểm toán"
                page={page}
                pageCount={Math.max(1, totalPages)}
                onPageChange={(nextPage) => setParams({ page: nextPage })}
              />
            ) : null}
          </>
        )}
      </section>

      {/* Diff Viewer FormDialog */}
      <FormDialog
        open={Boolean(selectedLog)}
        onClose={() => setSelectedLog(null)}
        variant="calm"
        icon={FileText}
        eyebrow="Chi tiết thay đổi"
        title={selectedLog ? `${selectedLog.action} · ${selectedLog.entityName}` : 'Chi tiết diff'}
        size="lg"
        footer={
          <DialogFooter variant="calm">
            <button
              type="button"
              onClick={() => setSelectedLog(null)}
              className={adminUi.btnSecondary}
            >
              Đóng
            </button>
          </DialogFooter>
        }
      >
        {selectedLog && activeDiff ? (
          <div className="space-y-4">
            <dl className="grid grid-cols-1 gap-3 rounded-xl border border-slate-300 bg-[#f7fafd] p-4 text-[13px] sm:grid-cols-2">
              <div>
                <dt className="text-xs font-semibold text-slate-600">Thời điểm</dt>
                <dd className="mt-0.5 font-semibold text-[#0F2854] tabular-nums">
                  {formatDateTime(selectedLog.createdAt)}
                </dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-slate-600">Người thực hiện</dt>
                <dd className="mt-0.5 break-all [overflow-wrap:anywhere] font-semibold text-[#0F2854]">
                  {selectedLog.userFullName || 'Hệ thống'}{' '}
                  {selectedLog.userEmail ? `(${selectedLog.userEmail})` : ''}
                </dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-slate-600">Thực thể / Entity ID</dt>
                <dd className="mt-0.5 break-all [overflow-wrap:anywhere] font-mono text-[#0F2854]">
                  {selectedLog.entityName}
                  {selectedLog.entityId ? ` · ${selectedLog.entityId}` : ''}
                </dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-slate-600">Địa chỉ IP</dt>
                <dd className="mt-0.5 font-mono font-medium text-[#0F2854] tabular-nums">
                  {selectedLog.ipAddress || '—'}
                </dd>
              </div>
            </dl>

            {activeDiff.oldNote || activeDiff.newNote ? (
              <div className="space-y-1 rounded-xl border border-[#94b8da] bg-[#edf5fd] p-3.5 text-[13px] text-[#0F2854]">
                {activeDiff.oldNote &&
                activeDiff.newNote &&
                activeDiff.oldNote !== activeDiff.newNote ? (
                  <>
                    <p>
                      <span className="font-semibold text-[#1C4D8D]">Ghi chú trước: </span>
                      {activeDiff.oldNote}
                    </p>
                    <p>
                      <span className="font-semibold text-[#1C4D8D]">Ghi chú sau: </span>
                      {activeDiff.newNote}
                    </p>
                  </>
                ) : (
                  <p>
                    <span className="font-semibold text-[#1C4D8D]">Ghi chú kèm theo: </span>
                    {activeDiff.newNote || activeDiff.oldNote}
                  </p>
                )}
              </div>
            ) : null}

            {activeDiff.rows.length > 0 ? (
              <div className="overflow-x-auto rounded-xl border border-slate-300 bg-white">
                <table className="w-full text-left text-[13px]">
                  <thead className={adminUi.thead}>
                    <tr>
                      <th className="px-3.5 py-2.5">Trường dữ liệu</th>
                      <th className="px-3.5 py-2.5">Trạng thái</th>
                      <th className="px-3.5 py-2.5">Trước</th>
                      <th className="px-3.5 py-2.5">Sau</th>
                    </tr>
                  </thead>
                  <tbody className={adminUi.tbody}>
                    {activeDiff.rows.map((row) => {
                      const meta = CHANGE_BADGE_META[row.change]
                      return (
                        <tr key={row.field}>
                          <td className="px-3.5 py-2.5 font-mono font-semibold text-[#0F2854]">
                            {row.field}
                          </td>
                          <td className="px-3.5 py-2.5">
                            <span className={meta.className}>{meta.label}</span>
                          </td>
                          <td className="max-w-[200px] px-3.5 py-2.5 font-mono text-slate-600 break-all">
                            {row.before ?? '—'}
                          </td>
                          <td className="max-w-[200px] px-3.5 py-2.5 font-mono font-semibold text-[#0F2854] break-all">
                            {row.after ?? '—'}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className={adminUi.infoBox}>
                Bản ghi không chứa dữ liệu Trước / Sau chi tiết.
              </p>
            )}

            {activeDiff.hasRaw ? (
              <div>
                <button
                  type="button"
                  onClick={() => setShowRawPayload((prev) => !prev)}
                  className={adminUi.btnSecondary}
                >
                  {showRawPayload ? 'Ẩn dữ liệu gốc' : 'Xem dữ liệu gốc (Raw)'}
                </button>
                {showRawPayload ? (
                  <div className="mt-2 grid gap-3 sm:grid-cols-2">
                    <div className="min-w-0 rounded-xl border border-slate-300 bg-[#f7fafd] p-3">
                      <p className="text-xs font-semibold text-slate-600">oldValues</p>
                      <pre className="mt-1.5 overflow-x-auto break-words [overflow-wrap:anywhere] font-mono text-xs whitespace-pre-wrap text-[#0F2854]">
                        {selectedLog.oldValues || 'null'}
                      </pre>
                    </div>
                    <div className="min-w-0 rounded-xl border border-slate-300 bg-[#f7fafd] p-3">
                      <p className="text-xs font-semibold text-slate-600">newValues</p>
                      <pre className="mt-1.5 overflow-x-auto break-words [overflow-wrap:anywhere] font-mono text-xs whitespace-pre-wrap text-[#0F2854]">
                        {selectedLog.newValues || 'null'}
                      </pre>
                    </div>
                  </div>
                ) : null}
              </div>
            ) : null}
          </div>
        ) : null}
      </FormDialog>
    </div>
  )
}
