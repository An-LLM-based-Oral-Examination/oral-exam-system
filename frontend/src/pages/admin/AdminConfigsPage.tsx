import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { LoaderCircle, Pencil, Plus, RefreshCw, SlidersHorizontal } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { LoadingState } from '@/components/common/LoadingState'
import { SearchField } from '@/components/common/SearchField'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { toast } from '@/components/common/toastStore'
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
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { MF01_CONFIG_RULES } from '@/pages/admin/mf01ConfigRules'
import { adminService, type SystemConfigItem } from '@/services/admin.service'

const CONFIG_QUERY_DEFAULTS = {
  q: '',
} as const

type ConfigField = 'value' | 'description'
const CONFIG_FIELDS: readonly ConfigField[] = ['value', 'description']

function formatDateTimeVi(raw: string): string {
  const parsed = new Date(raw)
  return Number.isNaN(parsed.getTime()) ? raw : parsed.toLocaleString('vi-VN')
}

export function AdminConfigsPage() {
  const { getString, setParams, resetParams } = useUrlQueryState(CONFIG_QUERY_DEFAULTS)
  const urlQuery = getString('q')

  const searchInput = useSearchInput((nextQuery) => {
    setParams({ q: nextQuery })
  }, urlQuery)

  const [configs, setConfigs] = useState<SystemConfigItem[]>([])
  const [loading, setLoading] = useState(true)
  const [listProblem, setListProblem] = useState<NormalizedProblem | null>(null)
  const [statusMessage, setStatusMessage] = useState('')
  const [isInitializing, setIsInitializing] = useState(false)
  const requestIdRef = useRef(0)

  const [editConfig, setEditConfig] = useState<SystemConfigItem | null>(null)
  const [configValue, setConfigValue] = useState('')
  const [configDesc, setConfigDesc] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<ConfigField, string>>>({})
  const [dialogProblem, setDialogProblem] = useState<NormalizedProblem | null>(null)
  const [isUpdating, setIsUpdating] = useState(false)

  const loadConfigs = useCallback(async () => {
    const requestId = ++requestIdRef.current
    setLoading(true)
    setListProblem(null)
    try {
      const data = await adminService.getSystemConfigs()
      if (requestId !== requestIdRef.current) return
      setConfigs(Array.isArray(data) ? data : [])
    } catch (err: unknown) {
      if (requestId !== requestIdRef.current) return
      const parsed = extractProblemDetails(err, 'Không thể tải danh sách cấu hình hệ thống.')
      setListProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadConfigs()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [loadConfigs])

  const missingCanonicalKeys = useMemo(() => {
    return Object.keys(MF01_CONFIG_RULES).filter(
      (key) => !configs.some((item) => item.key === key),
    )
  }, [configs])

  const filteredConfigs = useMemo(() => {
    const keyword = urlQuery.trim().toLowerCase()
    if (!keyword) return configs
    return configs.filter((c) => {
      const matchesKey = c.key.toLowerCase().includes(keyword)
      const desc = (c.description || MF01_CONFIG_RULES[c.key]?.description || '').toLowerCase()
      return matchesKey || desc.includes(keyword)
    })
  }, [configs, urlQuery])

  const handleInitializeDefaults = async () => {
    if (isInitializing || missingCanonicalKeys.length === 0) return
    setIsInitializing(true)
    setListProblem(null)
    setStatusMessage('')
    let initProblem: NormalizedProblem | null = null
    try {
      await Promise.all(
        missingCanonicalKeys.map((key) => {
          const rule = MF01_CONFIG_RULES[key]
          return adminService.updateSystemConfig(key, {
            value: rule.defaultValue,
            description: rule.description,
          })
        }),
      )
      const msg = `Đã khởi tạo ${missingCanonicalKeys.length} tham số mặc định cho luồng luyện tập MF-01.`
      toast.success(msg)
      setStatusMessage(msg)
    } catch (err: unknown) {
      initProblem = extractProblemDetails(err, 'Không thể khởi tạo các tham số mặc định MF-01.')
      toast.error(initProblem.detail)
    } finally {
      await loadConfigs()
      if (initProblem) {
        setListProblem(initProblem)
      }
      setIsInitializing(false)
    }
  }

  const handleOpenEdit = (config: SystemConfigItem) => {
    setEditConfig(config)
    setConfigValue(config.value)
    setConfigDesc(config.description || '')
    setFieldErrors({})
    setDialogProblem(null)
  }

  const handleUpdateConfig = async (event: FormEvent) => {
    event.preventDefault()
    if (isUpdating || !editConfig) return

    const trimmedVal = configValue.trim()
    const rule = MF01_CONFIG_RULES[editConfig.key]

    if (rule) {
      const num = Number.parseInt(trimmedVal, 10)
      if (Number.isNaN(num) || !/^-?\d+$/.test(trimmedVal)) {
        const msg = `Giá trị cấu hình [${editConfig.key}] phải là một số nguyên hợp lệ.`
        setFieldErrors({ value: msg })
        toast.error(msg)
        return
      }
      if (num < rule.min || num > rule.max) {
        const msg = `Giá trị [${editConfig.key}] phải nằm trong khoảng từ ${rule.min} đến ${rule.max}.`
        setFieldErrors({ value: msg })
        toast.error(msg)
        return
      }
      if (rule.validate) {
        const customErr = rule.validate(num, configs)
        if (customErr) {
          setFieldErrors({ value: customErr })
          toast.error(customErr)
          return
        }
      }
    } else if (!trimmedVal) {
      const msg = `Vui lòng nhập giá trị cho tham số [${editConfig.key}].`
      setFieldErrors({ value: msg })
      toast.error(msg)
      return
    }

    setIsUpdating(true)
    setFieldErrors({})
    setDialogProblem(null)
    try {
      const targetKey = editConfig.key
      const nextDesc = configDesc.trim() || undefined
      const updated = await adminService.updateSystemConfig(targetKey, {
        value: trimmedVal,
        description: nextDesc,
      })
      setConfigs((prev) =>
        prev.map((item) =>
          item.key === targetKey
            ? {
                ...item,
                ...updated,
                value: updated?.value ?? trimmedVal,
                description: updated?.description ?? nextDesc ?? item.description,
              }
            : item,
        ),
      )
      const msg = `Đã cập nhật cấu hình [${targetKey}].`
      toast.success(msg)
      setStatusMessage(msg)
      setEditConfig(null)
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không thể cập nhật cấu hình.')
      setFieldErrors(
        mapProblemFieldErrors(err, CONFIG_FIELDS, {
          value: 'value',
          description: 'description',
        }),
      )
      setDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsUpdating(false)
    }
  }

  const activeRule = editConfig ? MF01_CONFIG_RULES[editConfig.key] : undefined

  return (
    <div className={ds.page}>
      <PageBanner
        variant="calm"
        icon={SlidersHorizontal}
        eyebrow="Tham số toàn cục · Luyện tập MF-01"
        title="Cấu hình hệ thống MF-01"
        description="Quản lý tham số bảng system_configs cho luồng Luyện tập tương tác (MF-01): số câu hỏi phụ 1–5 (mặc định 2), số câu Progressive 3–10, thời gian đệm transcript 10–300s (mặc định 60s) và timeout phiên 5 phút."
        extras={
          <BannerFact icon={SlidersHorizontal} surface="calm">
            <span className="tabular-nums">Tổng tham số: {configs.length}</span>
          </BannerFact>
        }
      />

      <AdminStateBanner
        problem={listProblem}
        statusMessage={statusMessage}
        onRetry={listProblem ? () => void loadConfigs() : undefined}
        onDismiss={() => {
          setListProblem(null)
          setStatusMessage('')
        }}
      />

      <section className="overflow-hidden rounded-2xl border border-[#0F2854]/10 bg-white">
        <SectionHeader
          variant="calm"
          icon={SlidersHorizontal}
          title="Bảng tham số system_configs"
          badge={<span className="tabular-nums">{filteredConfigs.length}</span>}
          hint="Kiểm tra miền giá trị nguyên và ràng buộc Min <= Max trước khi lưu"
          action={
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => void loadConfigs()}
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
              {!loading && !listProblem && missingCanonicalKeys.length > 0 ? (
                <button
                  type="button"
                  disabled={isInitializing}
                  onClick={() => void handleInitializeDefaults()}
                  className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[#1C4D8D] px-4 text-xs font-medium text-white transition-colors duration-150 hover:bg-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] disabled:opacity-50 motion-reduce:transition-none"
                >
                  {isInitializing ? (
                    <LoaderCircle
                      className="size-4 animate-spin motion-reduce:animate-none"
                      aria-hidden="true"
                    />
                  ) : (
                    <Plus className="size-4" strokeWidth={1.5} aria-hidden="true" />
                  )}
                  Khởi tạo {missingCanonicalKeys.length} tham số mặc định còn thiếu
                </button>
              ) : null}
            </div>
          }
        />

        {/* Toolbar */}
        <div className="flex flex-col gap-3 border-b border-[#0F2854]/8 bg-white p-4 sm:flex-row sm:items-center sm:p-5">
          <div className="relative min-w-0 flex-1 sm:max-w-md">
            <SearchField
              aria-label="Tìm tham số cấu hình theo khóa hoặc diễn giải"
              placeholder="Tìm khóa tham số, diễn giải…"
              value={searchInput.draft}
              onChange={(event) => searchInput.change(event.target.value)}
              onKeyDown={searchInput.onKeyDown}
              className="h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-normal"
            />
          </div>
          {urlQuery ? (
            <button
              type="button"
              onClick={() => {
                searchInput.reset('')
                resetParams(['q'])
              }}
              className="inline-flex min-h-11 items-center justify-center rounded-xl border border-[#0F2854]/15 bg-white px-3.5 text-xs font-medium text-[#5b6b84] transition-colors duration-150 hover:bg-[#F4FBFD] hover:text-[#0F2854] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
            >
              Xóa bộ lọc
            </button>
          ) : null}
        </div>

        {loading ? (
          <LoadingState label="Đang tải danh sách tham số..." className="min-h-56" />
        ) : filteredConfigs.length === 0 ? (
          listProblem ? null : (
            <div className="p-5">
              <EmptyState
                icon={SlidersHorizontal}
                title={
                  urlQuery
                    ? 'Không tìm thấy tham số phù hợp'
                    : 'Chưa có tham số hệ thống nào trong bảng system_configs.'
                }
                description={
                  urlQuery
                    ? 'Thử thay đổi từ khóa tìm kiếm.'
                    : 'Các tham số cấu hình sẽ xuất hiện tại đây hoặc có thể khởi tạo mặc định.'
                }
                action={
                  urlQuery ? (
                    <button
                      type="button"
                      onClick={() => {
                        searchInput.reset('')
                        resetParams(['q'])
                      }}
                      className="inline-flex min-h-11 items-center rounded-xl border border-[#0F2854]/15 bg-white px-4 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                    >
                      Xóa bộ lọc
                    </button>
                  ) : null
                }
              />
            </div>
          )
        ) : (
          <>
            {/* Desktop Table */}
            <div className="hidden overflow-x-auto md:block">
              <table className="w-full min-w-[780px] text-left text-sm">
                <thead className="border-b border-[#0F2854]/10 bg-[#F8FBFD] text-xs font-medium text-[#5b6b84]">
                  <tr>
                    <th className="px-5 py-3">Khóa tham số (Key)</th>
                    <th className="px-4 py-3">Giá trị cấu hình</th>
                    <th className="px-4 py-3">Diễn giải nghiệp vụ</th>
                    <th className="px-4 py-3">Cập nhật lúc</th>
                    <th className="px-5 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#0F2854]/8">
                  {filteredConfigs.map((c) => {
                    const rule = MF01_CONFIG_RULES[c.key]
                    return (
                      <tr key={c.id} className="hover:bg-[#F8FBFD]">
                        <td className="px-5 py-3.5">
                          <span className="font-mono text-xs font-semibold text-[#0F2854]">
                            {c.key}
                          </span>
                          {rule ? (
                            <span className="mt-1 block text-xs text-[#5b6b84] tabular-nums">
                              Miền: {rule.min} – {rule.max}
                            </span>
                          ) : null}
                        </td>
                        <td className="px-4 py-3.5 font-mono text-sm font-semibold text-[#1C4D8D] tabular-nums">
                          {c.value}
                        </td>
                        <td className="max-w-md px-4 py-3.5 text-xs leading-relaxed text-[#5b6b84]">
                          {c.description || rule?.description || '—'}
                        </td>
                        <td className="px-4 py-3.5 text-xs text-[#5b6b84] tabular-nums">
                          {formatDateTimeVi(c.updatedAt)}
                        </td>
                        <td className="px-5 py-3.5 text-right">
                          <button
                            type="button"
                            aria-label={`Chỉnh sửa ${c.key}`}
                            onClick={() => handleOpenEdit(c)}
                            className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                          >
                            <Pencil className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                            Chỉnh sửa
                          </button>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* Mobile Card List */}
            <div className="space-y-3 p-4 md:hidden">
              {filteredConfigs.map((c) => {
                const rule = MF01_CONFIG_RULES[c.key]
                return (
                  <article
                    key={c.id}
                    className="rounded-xl border border-[#0F2854]/10 bg-white p-4"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="font-mono text-xs font-semibold break-all text-[#0F2854]">
                          {c.key}
                        </p>
                        {rule ? (
                          <p className="mt-0.5 text-xs text-[#5b6b84] tabular-nums">
                            Miền hợp lệ: {rule.min} – {rule.max}
                          </p>
                        ) : null}
                      </div>
                      <span className="shrink-0 rounded-md border border-[#0F2854]/10 bg-[#F4FBFD] px-2.5 py-1 font-mono text-xs font-semibold text-[#1C4D8D] tabular-nums">
                        {c.value}
                      </span>
                    </div>

                    <p className="mt-2.5 text-xs leading-relaxed text-[#5b6b84]">
                      {c.description || rule?.description || '—'}
                    </p>
                    <p className="mt-2 text-xs text-[#5b6b84] tabular-nums">
                      Cập nhật: {formatDateTimeVi(c.updatedAt)}
                    </p>

                    <div className="mt-3">
                      <button
                        type="button"
                        aria-label={`Chỉnh sửa ${c.key}`}
                        onClick={() => handleOpenEdit(c)}
                        className="inline-flex min-h-11 w-full items-center justify-center gap-1.5 rounded-xl border border-[#0F2854]/15 bg-white px-3 text-xs font-medium text-[#1C4D8D] transition-colors duration-150 hover:bg-[#F4FBFD] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#1C4D8D] motion-reduce:transition-none"
                      >
                        <Pencil className="size-3.5" strokeWidth={1.5} aria-hidden="true" />
                        Chỉnh sửa
                      </button>
                    </div>
                  </article>
                )
              })}
            </div>
          </>
        )}
      </section>

      {/* Edit Config FormDialog */}
      <FormDialog
        open={Boolean(editConfig)}
        variant="calm"
        size="sm"
        busy={isUpdating}
        onClose={() => {
          if (!isUpdating) setEditConfig(null)
        }}
        icon={SlidersHorizontal}
        eyebrow={editConfig ? editConfig.key : 'Tham số hệ thống'}
        title="Chỉnh sửa tham số hệ thống"
        footer={
          <SubmitDialogFooter
            variant="calm"
            form="admin-edit-config-form"
            onCancel={() => {
              if (!isUpdating) setEditConfig(null)
            }}
            label="Lưu cấu hình"
            busyLabel="Đang lưu…"
            busy={isUpdating}
          />
        }
      >
        {editConfig ? (
          <form
            id="admin-edit-config-form"
            onSubmit={handleUpdateConfig}
            noValidate
            className="space-y-4"
          >
            <AdminStateBanner problem={dialogProblem} />

            <div>
              <label htmlFor="config-value-input" className="block text-xs font-medium text-[#0F2854]">
                Giá trị cấu hình
                {activeRule ? (
                  <span className="ml-1.5 text-xs font-normal text-[#1C4D8D] tabular-nums">
                    (Miền hợp lệ: {activeRule.min} – {activeRule.max})
                  </span>
                ) : null}
              </label>
              <input
                id="config-value-input"
                type={activeRule ? 'number' : 'text'}
                min={activeRule?.min}
                max={activeRule?.max}
                step={activeRule ? '1' : undefined}
                value={configValue}
                onChange={(event) => {
                  setConfigValue(event.target.value)
                  setFieldErrors((prev) => ({ ...prev, value: undefined }))
                }}
                placeholder={activeRule ? `${activeRule.min} - ${activeRule.max}` : undefined}
                aria-invalid={Boolean(fieldErrors.value)}
                className={cn(
                  ds.field,
                  'mt-1.5 h-11 min-h-11 rounded-xl border-[#0F2854]/15 font-mono text-xs tabular-nums',
                  fieldErrors.value && fieldInvalidClass,
                )}
              />
              <FieldError message={fieldErrors.value} />
              {activeRule ? (
                <p className="mt-1.5 text-xs leading-relaxed text-[#5b6b84]">
                  {activeRule.description}
                </p>
              ) : null}
            </div>

            <div>
              <label htmlFor="config-desc-input" className="block text-xs font-medium text-[#0F2854]">
                Diễn giải nghiệp vụ
              </label>
              <textarea
                id="config-desc-input"
                value={configDesc}
                onChange={(event) => {
                  setConfigDesc(event.target.value)
                  setFieldErrors((prev) => ({ ...prev, description: undefined }))
                }}
                aria-invalid={Boolean(fieldErrors.description)}
                className={cn(
                  'mt-1.5 min-h-20 w-full rounded-xl border border-[#0F2854]/15 bg-white p-3 text-sm text-[#0F2854] outline-none focus:ring-2 focus:ring-[#4988C4]/30',
                  fieldErrors.description && fieldInvalidClass,
                )}
              />
              <FieldError message={fieldErrors.description} />
            </div>
          </form>
        ) : null}
      </FormDialog>
    </div>
  )
}
