import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Eye, FileText, RefreshCw, UserRound } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu, type FilterDropMenuOption } from '@/components/common/FilterDropMenu'
import { ListPagination } from '@/components/common/ListPagination'
import { LoadingState } from '@/components/common/LoadingState'
import { FormDialog, DialogFooter } from '@/design/overlays'
import { ds } from '@/design/system'
import { BannerFact, PageBanner, SectionHeader } from '@/design/ui'
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
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'

function formatDateTime(raw: string): string {
  const parsed = new Date(raw)
  return Number.isNaN(parsed.getTime()) ? raw : parsed.toLocaleString('vi-VN')
}

function getActionBadgeClass(action: string): string {
  const upper = action.toUpperCase()
  if (upper.includes('CREATED') || upper.includes('CREATE') || upper.includes('DISPATCHED')) {
    return 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]'
  }
  if (upper.includes('DELETED') || upper.includes('DELETE') || upper.includes('RESET')) {
    return 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]'
  }
  return 'border-[#0F2854]/10 bg-[#F4FBFD] text-[#1C4D8D]'
}

const CHANGE_BADGE_META: Record<AuditDiffChangeKind, { label: string; className: string }> = {
  added: {
    label: 'Thêm mới',
    className: 'border-[#b7e4cf] bg-[#e8f8f1] text-[#16836a]',
  },
  changed: {
    label: 'Thay đổi',
    className: 'border-[#f3ddb0] bg-[#fff8eb] text-[#9a6b12]',
  },
  removed: {
    label: 'Đã xóa',
    className: 'border-[#f3c2c8] bg-[#fdecec] text-[#c2415b]',
  },
  unchanged: {
    label: 'Không đổi',
    className: 'border-[#0F2854]/10 bg-[#F8FBFD] text-[#5b6b84]',
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

  // Diff modal
  const [selectedLog, setSelectedLog] = useState<AuditLogItem | null>(null)
  const [showRawPayload, setShowRawPayload] = useState(false)

  const loadActors = useCallback(async () => {
    setActorsLoading(true)
    try {
      const res = await adminService.getUsers({ page: 1, pageSize: 50 })
      setActors(res.items ?? [])
    } catch {
      setActors([])
    } finally {
      setActorsLoading(false)
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadActors()
    }, 0)
    return () => window.clearTimeout(timer)
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
    return () => window.clearTimeout(timer)
  }, [loadLogs])

  const actorOptions: FilterDropMenuOption[] = useMemo(
    () => [
      { value: '', label: actorsLoading ? 'Đang tải danh sách…' : 'Tất cả người thực hiện' },
      ...actors.map((actor) => ({
        value: actor.id,
        label: `${actor.fullName} (${actor.email})`,
      })),
    ],
    [actors, actorsLoading],
  )

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
      <PageBanner
        variant="calm"
        icon={FileText}
        eyebrow="Truy vết kiểm toán bất biến · audit_logs"
        title="Nhật ký kiểm toán hệ thống"
        description="Tra cứu thao tác thay đổi dữ liệu theo thực thể, hành động hoặc người thực hiện và đối chiếu chi tiết giá trị Trước / Sau."
        extras={
          <BannerFact icon={FileText} surface="calm">
            <span className="tabular-nums">Tổng bản ghi: {totalCount.toLocaleString('vi-VN')}</span>
          </BannerFact>
        }
      />

      <AdminStateBanner
        problem={problem}
        onRetry={problem ? () => void loadLogs() : undefined}
        onDismiss={() => setProblem(null)}
      />

      <section className="overflow-hidden rounded-2xl border border-[#0F2854]/10 bg-white">
        <SectionHeader
          variant="calm"
          icon={FileText}
          title="Nhật ký hoạt động"
          badge={<span className="tabular-nums">{totalCount}</span>}
          hint="API so khớp chính xác tên thực thể (entity_name) và mã hành động (action)"
          action={
            <button
              type="button"
              onClick={() => void loadLogs()}
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
          }
        />

        {/* Filter Bar */}
        <div className="space-y-3 border-b border-[#0F2854]/8 bg-white p-4 sm:p-5">
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
              buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
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
              buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
            />

            <FilterDropMenu
              label="Người thực hiện"
              value={userIdFilter}
              options={actorOptions}
              onChange={(nextUserId) => setParams({ userId: nextUserId })}
              wrapOptions
              buttonClassName="min-h-11 rounded-xl border-[#0F2854]/15 font-medium"
            />
          </div>

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
              placeholder="Hoặc nhập chính xác entity_name (vd: users)"
              className={cn(
                ds.field,
                'h-11 min-h-11 flex-1 rounded-xl border-[#0F2854]/15 font-mono text-xs font-normal placeholder:font-sans',
              )}
            />
            <input
              aria-label="Nhập chính xác action"
              type="text"
              value={customAction}
              onChange={(event) => setCustomAction(event.target.value)}
              placeholder="Hoặc nhập chính xác action (vd: USER_CREATED)"
              className={cn(
                ds.field,
                'h-11 min-h-11 flex-1 rounded-xl border-[#0F2854]/15 font-mono text-xs font-normal uppercase placeholder:font-sans placeholder:normal-case',
              )}
            />
            <button
              type="submit"
              className="inline-flex min-h-11 shrink-0 items-center justify-center rounded-xl bg-[#1C4D8D] px-4 text-xs font-medium text-white transition-colors duration-150 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
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
                className="inline-flex min-h-11 shrink-0 items-center justify-center rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#5b6b84] transition-colors duration-150 hover:bg-[#F4FBFD] hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
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
                  ? 'Lưu ý: Bộ lọc entity_name và action so khớp chính xác theo tên bảng và mã hành động.'
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
                    className="inline-flex min-h-11 items-center rounded-xl border border-[#0F2854]/15 bg-white px-4 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
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
              <table className="w-full min-w-[860px] text-left text-sm">
                <thead className="border-b border-[#0F2854]/10 bg-[#F8FBFD] text-xs font-medium text-[#5b6b84]">
                  <tr>
                    <th className="px-5 py-3">Thời điểm</th>
                    <th className="px-4 py-3">Người thực hiện</th>
                    <th className="px-4 py-3">Hành động</th>
                    <th className="px-4 py-3">Thực thể</th>
                    <th className="px-4 py-3">Tóm tắt thay đổi</th>
                    <th className="px-5 py-3 text-right">Chi tiết</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#0F2854]/8">
                  {logs.map((log) => (
                    <tr key={log.id} className="hover:bg-[#F8FBFD]">
                      <td className="px-5 py-3.5 text-xs whitespace-nowrap text-[#5b6b84] tabular-nums">
                        {formatDateTime(log.createdAt)}
                      </td>
                      <td className="px-4 py-3.5">
                        <p className="text-xs font-medium text-[#0F2854]">
                          {log.userFullName || 'Hệ thống'}
                        </p>
                        <p className="mt-0.5 text-xs text-[#5b6b84]">
                          {log.userEmail || log.ipAddress || '—'}
                        </p>
                      </td>
                      <td className="px-4 py-3.5">
                        <span
                          className={cn(
                            'inline-flex rounded-md border px-2 py-0.5 font-mono text-xs font-medium',
                            getActionBadgeClass(log.action),
                          )}
                        >
                          {log.action}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <p className="font-mono text-xs font-medium text-[#0F2854]">
                          {log.entityName}
                        </p>
                        {log.entityId ? (
                          <p className="mt-0.5 max-w-[140px] truncate font-mono text-xs text-[#5b6b84]">
                            {log.entityId}
                          </p>
                        ) : null}
                      </td>
                      <td className="max-w-xs px-4 py-3.5 text-xs text-[#5b6b84]">
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
                          className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                        >
                          <Eye className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
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
                <article
                  key={log.id}
                  className="rounded-xl border border-[#0F2854]/10 bg-white p-4"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span
                      className={cn(
                        'inline-flex rounded-md border px-2 py-0.5 font-mono text-xs font-medium',
                        getActionBadgeClass(log.action),
                      )}
                    >
                      {log.action}
                    </span>
                    <span className="text-xs text-[#5b6b84] tabular-nums">
                      {formatDateTime(log.createdAt)}
                    </span>
                  </div>
                  <p className="mt-2 break-all [overflow-wrap:anywhere] font-mono text-xs font-medium text-[#0F2854]">
                    {log.entityName}
                    {log.entityId ? ` · ${log.entityId}` : ''}
                  </p>
                  <p className="mt-1 flex items-start gap-1 text-xs text-[#5b6b84]">
                    <UserRound className="mt-0.5 size-3.5 shrink-0" strokeWidth={1.5} aria-hidden="true" />
                    <span className="min-w-0 break-all [overflow-wrap:anywhere]">
                      {log.userFullName || 'Hệ thống'} ({log.userEmail || '—'})
                    </span>
                  </p>
                  <p className="mt-2 text-xs text-[#5b6b84]">
                    {summarizeAuditDiff(log.oldValues, log.newValues)}
                  </p>
                  <button
                    type="button"
                    aria-label={`Xem diff ${log.action} ${log.entityName}`}
                    onClick={() => {
                      setSelectedLog(log)
                      setShowRawPayload(false)
                    }}
                    className="mt-3 inline-flex min-h-11 w-full items-center justify-center gap-1.5 rounded-xl border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                  >
                    <Eye className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
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
        eyebrow="Chi tiết thay đổi kiểm toán"
        title={selectedLog ? `${selectedLog.action} · ${selectedLog.entityName}` : 'Chi tiết diff'}
        size="lg"
        footer={
          <DialogFooter variant="calm">
            <button
              type="button"
              onClick={() => setSelectedLog(null)}
              className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#0F2854]/15 bg-white px-4 text-xs font-medium text-[#0F2854] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              Đóng
            </button>
          </DialogFooter>
        }
      >
        {selectedLog && activeDiff ? (
          <div className="space-y-4">
            <dl className="grid grid-cols-1 gap-3 rounded-xl border border-[#0F2854]/10 bg-white p-4 text-xs sm:grid-cols-2">
              <div>
                <dt className="text-[#5b6b84]">Thời điểm</dt>
                <dd className="mt-0.5 font-medium text-[#0F2854] tabular-nums">
                  {formatDateTime(selectedLog.createdAt)}
                </dd>
              </div>
              <div>
                <dt className="text-[#5b6b84]">Người thực hiện</dt>
                <dd className="mt-0.5 break-all [overflow-wrap:anywhere] font-medium text-[#0F2854]">
                  {selectedLog.userFullName || 'Hệ thống'}{' '}
                  {selectedLog.userEmail ? `(${selectedLog.userEmail})` : ''}
                </dd>
              </div>
              <div>
                <dt className="text-[#5b6b84]">Thực thể / Entity ID</dt>
                <dd className="mt-0.5 break-all [overflow-wrap:anywhere] font-mono text-[#0F2854]">
                  {selectedLog.entityName}
                  {selectedLog.entityId ? ` · ${selectedLog.entityId}` : ''}
                </dd>
              </div>
              <div>
                <dt className="text-[#5b6b84]">Địa chỉ IP</dt>
                <dd className="mt-0.5 font-mono text-[#0F2854] tabular-nums">
                  {selectedLog.ipAddress || '—'}
                </dd>
              </div>
            </dl>

            {activeDiff.oldNote || activeDiff.newNote ? (
              <div className="space-y-1 rounded-xl border border-[#0F2854]/10 bg-[#F4FBFD] p-3 text-xs text-[#0F2854]">
                {activeDiff.oldNote &&
                activeDiff.newNote &&
                activeDiff.oldNote !== activeDiff.newNote ? (
                  <>
                    <p>
                      <span className="font-medium text-[#1C4D8D]">Ghi chú trước: </span>
                      {activeDiff.oldNote}
                    </p>
                    <p>
                      <span className="font-medium text-[#1C4D8D]">Ghi chú sau: </span>
                      {activeDiff.newNote}
                    </p>
                  </>
                ) : (
                  <p>
                    <span className="font-medium text-[#1C4D8D]">Ghi chú kèm theo: </span>
                    {activeDiff.newNote || activeDiff.oldNote}
                  </p>
                )}
              </div>
            ) : null}

            {activeDiff.rows.length > 0 ? (
              <div className="overflow-x-auto rounded-xl border border-[#0F2854]/10 bg-white">
                <table className="w-full text-left text-xs">
                  <thead className="border-b border-[#0F2854]/10 bg-[#F8FBFD] font-medium text-[#5b6b84]">
                    <tr>
                      <th className="px-3.5 py-2.5">Trường dữ liệu</th>
                      <th className="px-3.5 py-2.5">Trạng thái</th>
                      <th className="px-3.5 py-2.5">Trước</th>
                      <th className="px-3.5 py-2.5">Sau</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#0F2854]/8">
                    {activeDiff.rows.map((row) => {
                      const meta = CHANGE_BADGE_META[row.change]
                      return (
                        <tr key={row.field}>
                          <td className="px-3.5 py-2.5 font-mono font-medium text-[#0F2854]">
                            {row.field}
                          </td>
                          <td className="px-3.5 py-2.5">
                            <span
                              className={cn(
                                'inline-flex rounded-md border px-2 py-0.5 text-xs font-medium',
                                meta.className,
                              )}
                            >
                              {meta.label}
                            </span>
                          </td>
                          <td className="max-w-[200px] px-3.5 py-2.5 font-mono text-[#5b6b84] break-all">
                            {row.before ?? '—'}
                          </td>
                          <td className="max-w-[200px] px-3.5 py-2.5 font-mono text-[#0F2854] break-all">
                            {row.after ?? '—'}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="rounded-xl border border-[#0F2854]/10 bg-white p-4 text-xs text-[#5b6b84]">
                Bản ghi không chứa dữ liệu Trước / Sau chi tiết.
              </p>
            )}

            {activeDiff.hasRaw ? (
              <div>
                <button
                  type="button"
                  onClick={() => setShowRawPayload((prev) => !prev)}
                  className="inline-flex min-h-11 items-center rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                >
                  {showRawPayload ? 'Ẩn dữ liệu gốc' : 'Xem dữ liệu gốc (Raw)'}
                </button>
                {showRawPayload ? (
                  <div className="mt-2 grid gap-3 sm:grid-cols-2">
                    <div className="min-w-0 rounded-xl border border-[#0F2854]/10 bg-white p-3">
                      <p className="text-xs font-medium text-[#5b6b84]">oldValues</p>
                      <pre className="mt-1.5 overflow-x-auto break-words [overflow-wrap:anywhere] font-mono text-xs whitespace-pre-wrap text-[#0F2854]">
                        {selectedLog.oldValues || 'null'}
                      </pre>
                    </div>
                    <div className="min-w-0 rounded-xl border border-[#0F2854]/10 bg-white p-3">
                      <p className="text-xs font-medium text-[#5b6b84]">newValues</p>
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
