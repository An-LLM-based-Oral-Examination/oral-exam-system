import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { CalendarDays, Layers, Pencil, Plus, RefreshCw, ShieldAlert } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FilterDropMenu, type FilterDropMenuOption } from '@/components/common/FilterDropMenu'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { LoadingState } from '@/components/common/LoadingState'
import { SearchField } from '@/components/common/SearchField'
import { SubmitDialogFooter } from '@/components/common/SubmitDialogFooter'
import { toast } from '@/components/common/toastStore'
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
import { AdminSemesterOfferingsModal } from '@/pages/admin/AdminSemesterOfferingsModal'
import { AdminPageHeader } from '@/pages/admin/components/AdminPageHeader'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { adminUi } from '@/pages/admin/components/adminUi'
import {
  adminService,
  type SaveSemesterPayload,
  type SemesterItem,
} from '@/services/admin.service'

const SEMESTER_QUERY_DEFAULTS = {
  q: '',
  status: 'all',
} as const

const STATUS_VALUES = ['all', 'active', 'inactive'] as const
type SemesterStatusFilter = (typeof STATUS_VALUES)[number]

const STATUS_FILTER_OPTIONS: FilterDropMenuOption[] = [
  { value: 'all', label: 'Tất cả trạng thái' },
  { value: 'active', label: 'Đang hoạt động' },
  { value: 'inactive', label: 'Đã đóng' },
]

type SemesterField = 'code' | 'name' | 'startDate' | 'endDate'
const SEMESTER_FIELDS: readonly SemesterField[] = ['code', 'name', 'startDate', 'endDate']

function formatDateVi(raw?: string | null): string {
  if (!raw) return '—'
  const parsed = new Date(raw)
  return Number.isNaN(parsed.getTime()) ? raw : parsed.toLocaleDateString('vi-VN')
}

function makeDefaultSemesterForm(): SaveSemesterPayload {
  return {
    code: '',
    name: '',
    startDate: new Date().toISOString().slice(0, 10),
    endDate: new Date(Date.now() + 90 * 24 * 3600 * 1000).toISOString().slice(0, 10),
    isActive: true,
  }
}

export function AdminSemestersPage() {
  const { getString, getEnum, setParams, resetParams } =
    useUrlQueryState(SEMESTER_QUERY_DEFAULTS)

  const urlQuery = getString('q')
  const statusFilter: SemesterStatusFilter = getEnum('status', STATUS_VALUES, 'all')

  const searchInput = useSearchInput((nextQuery) => {
    setParams({ q: nextQuery })
  }, urlQuery)

  const [semesters, setSemesters] = useState<SemesterItem[]>([])
  const [loading, setLoading] = useState(true)
  const [listProblem, setListProblem] = useState<NormalizedProblem | null>(null)
  const [statusMessage, setStatusMessage] = useState('')
  const requestIdRef = useRef(0)

  const [showModal, setShowModal] = useState(false)
  const [editingSemester, setEditingSemester] = useState<SemesterItem | null>(null)
  const [form, setForm] = useState<SaveSemesterPayload>(makeDefaultSemesterForm)
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<SemesterField, string>>>({})
  const [dialogProblem, setDialogProblem] = useState<NormalizedProblem | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  const [offeringsSemester, setOfferingsSemester] = useState<SemesterItem | null>(null)

  const loadSemesters = useCallback(async () => {
    const requestId = ++requestIdRef.current
    setLoading(true)
    setListProblem(null)
    try {
      const data = await adminService.getSemesters()
      if (requestId !== requestIdRef.current) return
      setSemesters(data)
    } catch (err: unknown) {
      if (requestId !== requestIdRef.current) return
      setListProblem(extractProblemDetails(err, 'Không thể tải danh sách học kỳ.'))
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadSemesters()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [loadSemesters])

  const filteredSemesters = useMemo(() => {
    const keyword = urlQuery.trim().toLowerCase()
    return semesters.filter((s) => {
      if (statusFilter === 'active' && !s.isActive) return false
      if (statusFilter === 'inactive' && s.isActive) return false
      if (keyword) {
        const matchesCode = s.code.toLowerCase().includes(keyword)
        const matchesName = s.name.toLowerCase().includes(keyword)
        if (!matchesCode && !matchesName) return false
      }
      return true
    })
  }, [semesters, statusFilter, urlQuery])

  const hasActiveFilters = Boolean(urlQuery || statusFilter !== 'all')

  const handleOpenCreate = () => {
    setEditingSemester(null)
    setForm(makeDefaultSemesterForm())
    setFieldErrors({})
    setDialogProblem(null)
    setShowModal(true)
  }

  const handleOpenEdit = (semester: SemesterItem) => {
    setEditingSemester(semester)
    setForm({
      code: semester.code,
      name: semester.name,
      startDate: semester.startDate ? semester.startDate.slice(0, 10) : '',
      endDate: semester.endDate ? semester.endDate.slice(0, 10) : '',
      isActive: semester.isActive,
    })
    setFieldErrors({})
    setDialogProblem(null)
    setShowModal(true)
  }

  const handleSaveSemester = async (event: FormEvent) => {
    event.preventDefault()
    if (isSaving) return

    const errors: Partial<Record<SemesterField, string>> = {}
    const normalizedCode = form.code.trim().toUpperCase()
    const normalizedName = form.name.trim()

    if (!normalizedCode) errors.code = 'Vui lòng nhập mã học kỳ.'
    if (!normalizedName) errors.name = 'Vui lòng nhập tên học kỳ.'
    if (!form.startDate) errors.startDate = 'Vui lòng chọn ngày bắt đầu.'
    if (!form.endDate) errors.endDate = 'Vui lòng chọn ngày kết thúc.'
    if (
      form.startDate &&
      form.endDate &&
      new Date(form.endDate) <= new Date(form.startDate)
    ) {
      errors.endDate = 'Ngày kết thúc học kỳ bắt buộc phải sau ngày bắt đầu.'
    }

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors)
      return
    }

    setIsSaving(true)
    setFieldErrors({})
    setDialogProblem(null)
    try {
      const payload: SaveSemesterPayload = {
        ...form,
        code: normalizedCode,
        name: normalizedName,
      }
      if (editingSemester) {
        await adminService.updateSemester(editingSemester.id, payload)
        const msg = `Đã cập nhật học kỳ [${normalizedCode}] thành công.`
        toast.success(msg)
        setStatusMessage(msg)
      } else {
        await adminService.createSemester(payload)
        const msg = `Đã tạo học kỳ [${normalizedCode}] thành công.`
        toast.success(msg)
        setStatusMessage(msg)
      }
      setShowModal(false)
      void loadSemesters()
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Lưu học kỳ thất bại.')
      setFieldErrors(
        mapProblemFieldErrors(err, SEMESTER_FIELDS, {
          code: 'code',
          name: 'name',
          startDate: 'startDate',
          endDate: 'endDate',
        }),
      )
      setDialogProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className={ds.page}>
      <AdminPageHeader
        icon={CalendarDays}
        title="Học kỳ & Môn mở"
        count={semesters.length}
        description="Tạo, cập nhật trạng thái học kỳ và chọn danh mục môn mở giảng dạy theo từng kỳ."
        meta={
          <span className={adminUi.badgeWarning}>
            <ShieldAlert className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
            Cấm xóa (ON DELETE RESTRICT)
          </span>
        }
        actions={
          <>
            <button
              type="button"
              onClick={() => void loadSemesters()}
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
            <button type="button" onClick={handleOpenCreate} className={adminUi.btnPrimary}>
              <Plus className="size-4" strokeWidth={1.75} aria-hidden="true" />
              Thêm học kỳ mới
            </button>
          </>
        }
      />

      <AdminStateBanner
        problem={listProblem}
        statusMessage={statusMessage}
        onRetry={listProblem ? () => void loadSemesters() : undefined}
        onDismiss={() => {
          setListProblem(null)
          setStatusMessage('')
        }}
      />

      <section aria-labelledby="admin-semesters-list-heading" className={adminUi.card}>
        <h2 id="admin-semesters-list-heading" className="sr-only">
          Danh sách học kỳ
        </h2>
        {/* Toolbar */}
        <div className={adminUi.toolbar}>
          <div className="relative min-w-0 flex-1 sm:min-w-[220px]">
            <SearchField
              aria-label="Tìm học kỳ theo mã hoặc tên"
              placeholder="Tìm mã học kỳ, tên học kỳ…"
              value={searchInput.draft}
              onChange={(event) => searchInput.change(event.target.value)}
              onKeyDown={searchInput.onKeyDown}
              className={adminUi.input}
            />
          </div>

          <FilterDropMenu
            label="Trạng thái học kỳ"
            value={statusFilter}
            options={STATUS_FILTER_OPTIONS}
            onChange={(value) => setParams({ status: value })}
            className="w-full sm:w-48"
            buttonClassName={adminUi.filterButton}
          />

          {hasActiveFilters ? (
            <button
              type="button"
              onClick={() => {
                searchInput.reset('')
                resetParams(['q', 'status'])
              }}
              className={adminUi.btnSubtle}
            >
              Xóa bộ lọc
            </button>
          ) : null}
        </div>

        {loading ? (
          <LoadingState label="Đang tải danh sách học kỳ…" className="min-h-56" />
        ) : filteredSemesters.length === 0 ? (
          listProblem ? null : (
            <div className="p-5">
              <EmptyState
                icon={CalendarDays}
                title={
                  hasActiveFilters
                    ? 'Không tìm thấy học kỳ phù hợp'
                    : 'Chưa có học kỳ nào trong hệ thống'
                }
                description={
                  hasActiveFilters
                    ? 'Thử thay đổi từ khóa tìm kiếm hoặc trạng thái học kỳ.'
                    : 'Tạo học kỳ đầu tiên để quản lý danh mục môn mở và lớp học.'
                }
                action={
                  hasActiveFilters ? (
                    <button
                      type="button"
                      onClick={() => {
                        searchInput.reset('')
                        resetParams(['q', 'status'])
                      }}
                      className={adminUi.btnSecondary}
                    >
                      Xóa bộ lọc
                    </button>
                  ) : (
                    <button type="button" onClick={handleOpenCreate} className={adminUi.btnPrimary}>
                      <Plus className="size-4" strokeWidth={1.75} aria-hidden="true" />
                      Thêm học kỳ mới
                    </button>
                  )
                }
              />
            </div>
          )
        ) : (
          <>
            {/* Desktop Table */}
            <div className="hidden overflow-x-auto md:block">
              <table className={cn(adminUi.table, 'min-w-[780px]')}>
                <thead className={adminUi.thead}>
                  <tr>
                    <th className="px-5 py-3.5">Mã học kỳ</th>
                    <th className="px-4 py-3.5">Tên học kỳ</th>
                    <th className="px-4 py-3.5">Thời gian</th>
                    <th className="px-4 py-3.5">Quy mô mở</th>
                    <th className="px-4 py-3.5">Trạng thái</th>
                    <th className="px-5 py-3.5 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className={adminUi.tbody}>
                  {filteredSemesters.map((s) => (
                    <tr key={s.id} className={adminUi.tr}>
                      <td className="px-5 py-3.5 font-mono text-sm font-bold text-[#0F2854]">
                        {s.code}
                      </td>
                      <td className="px-4 py-3.5 text-sm font-semibold text-[#0F2854]">{s.name}</td>
                      <td className="px-4 py-3.5 text-[13px] font-medium text-slate-700 tabular-nums">
                        {formatDateVi(s.startDate)} – {formatDateVi(s.endDate)}
                      </td>
                      <td className="px-4 py-3.5 text-[13px] font-medium text-slate-700 tabular-nums">
                        {s.coursesCount ?? 0} môn · {s.classesCount ?? 0} lớp
                      </td>
                      <td className="px-4 py-3.5">
                        <span className={s.isActive ? adminUi.badgeSuccess : adminUi.badgeNeutral}>
                          {s.isActive ? 'Hoạt động' : 'Đã đóng'}
                        </span>
                      </td>
                      <td className="px-5 py-3.5">
                        <div className="flex items-center justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => setOfferingsSemester(s)}
                            className={adminUi.btnRowPrimary}
                          >
                            <Layers className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
                            Môn mở trong kỳ
                          </button>
                          <button
                            type="button"
                            onClick={() => handleOpenEdit(s)}
                            aria-label={`Sửa học kỳ ${s.code}`}
                            className={adminUi.btnRowNeutral}
                          >
                            <Pencil className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
                            Sửa
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile Card List */}
            <div className="space-y-3 p-4 md:hidden">
              {filteredSemesters.map((s) => (
                <article key={s.id} className={adminUi.mobileCard}>
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="font-mono text-sm font-bold text-[#0F2854]">{s.code}</p>
                      <p className="mt-0.5 text-sm font-semibold text-[#1C4D8D]">{s.name}</p>
                    </div>
                    <span
                      className={cn(
                        'shrink-0',
                        s.isActive ? adminUi.badgeSuccess : adminUi.badgeNeutral,
                      )}
                    >
                      {s.isActive ? 'Hoạt động' : 'Đã đóng'}
                    </span>
                  </div>

                  <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-[13px] font-medium text-slate-600 tabular-nums">
                    <span>
                      {formatDateVi(s.startDate)} – {formatDateVi(s.endDate)}
                    </span>
                    <span>
                      {s.coursesCount ?? 0} môn · {s.classesCount ?? 0} lớp
                    </span>
                  </div>

                  <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
                    <button
                      type="button"
                      onClick={() => setOfferingsSemester(s)}
                      className={adminUi.btnMobilePrimary}
                    >
                      <Layers className="size-4" strokeWidth={1.75} aria-hidden="true" />
                      Môn mở trong kỳ
                    </button>
                    <button
                      type="button"
                      aria-label={`Sửa học kỳ ${s.code}`}
                      onClick={() => handleOpenEdit(s)}
                      className={adminUi.btnMobileNeutral}
                    >
                      <Pencil className="size-4" strokeWidth={1.75} aria-hidden="true" />
                      Sửa
                    </button>
                  </div>
                </article>
              ))}
            </div>
          </>
        )}
      </section>

      {/* Create / Edit Semester FormDialog */}
      <FormDialog
        open={showModal}
        variant="calm"
        size="md"
        busy={isSaving}
        onClose={() => {
          if (!isSaving) setShowModal(false)
        }}
        icon={CalendarDays}
        title={editingSemester ? 'Chỉnh sửa học kỳ' : 'Tạo học kỳ mới'}
        footer={
          <SubmitDialogFooter
            variant="calm"
            form="admin-semester-form"
            onCancel={() => {
              if (!isSaving) setShowModal(false)
            }}
            label={editingSemester ? 'Cập nhật học kỳ' : 'Tạo học kỳ'}
            busyLabel="Đang lưu…"
            busy={isSaving}
          />
        }
      >
        <form
          id="admin-semester-form"
          onSubmit={handleSaveSemester}
          noValidate
          className="space-y-3"
        >
          <AdminStateBanner problem={dialogProblem} />

          <div>
            <label htmlFor="semester-code" className={adminUi.label}>
              Mã học kỳ
            </label>
            <input
              id="semester-code"
              type="text"
              value={form.code}
              onChange={(event) => {
                setForm((prev) => ({ ...prev, code: event.target.value }))
                setFieldErrors((prev) => ({ ...prev, code: undefined }))
              }}
              placeholder="ví dụ: FA26, SP26, SU26"
              aria-invalid={Boolean(fieldErrors.code)}
              className={cn(
                ds.field,
                adminUi.input,
                'mt-1.5 font-mono font-semibold uppercase',
                fieldErrors.code && fieldInvalidClass,
              )}
            />
            <FieldError message={fieldErrors.code} />
          </div>

          <div>
            <label htmlFor="semester-name" className={adminUi.label}>
              Tên học kỳ
            </label>
            <input
              id="semester-name"
              type="text"
              value={form.name}
              onChange={(event) => {
                setForm((prev) => ({ ...prev, name: event.target.value }))
                setFieldErrors((prev) => ({ ...prev, name: undefined }))
              }}
              placeholder="ví dụ: Fall 2026, Spring 2026"
              aria-invalid={Boolean(fieldErrors.name)}
              className={cn(
                ds.field,
                adminUi.input,
                'mt-1.5',
                fieldErrors.name && fieldInvalidClass,
              )}
            />
            <FieldError message={fieldErrors.name} />
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label htmlFor="semester-start-date" className={adminUi.label}>
                Ngày bắt đầu
              </label>
              <input
                id="semester-start-date"
                type="date"
                value={form.startDate}
                onChange={(event) => {
                  setForm((prev) => ({ ...prev, startDate: event.target.value }))
                  setFieldErrors((prev) => ({ ...prev, startDate: undefined, endDate: undefined }))
                }}
                aria-invalid={Boolean(fieldErrors.startDate)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 tabular-nums',
                  fieldErrors.startDate && fieldInvalidClass,
                )}
              />
              <FieldError message={fieldErrors.startDate} />
            </div>

            <div>
              <label htmlFor="semester-end-date" className={adminUi.label}>
                Ngày kết thúc
              </label>
              <input
                id="semester-end-date"
                type="date"
                value={form.endDate}
                onChange={(event) => {
                  setForm((prev) => ({ ...prev, endDate: event.target.value }))
                  setFieldErrors((prev) => ({ ...prev, endDate: undefined }))
                }}
                aria-invalid={Boolean(fieldErrors.endDate)}
                className={cn(
                  ds.field,
                  adminUi.input,
                  'mt-1.5 tabular-nums',
                  fieldErrors.endDate && fieldInvalidClass,
                )}
              />
              <FieldError message={fieldErrors.endDate} />
            </div>
          </div>

          <label className="inline-flex min-h-11 items-center gap-2 text-sm font-medium text-[#0F2854]">
            <input
              type="checkbox"
              id="semester-is-active"
              checked={form.isActive ?? true}
              onChange={(event) =>
                setForm((prev) => ({ ...prev, isActive: event.target.checked }))
              }
              className="size-4 rounded border-slate-400 text-[#1C4D8D] focus:ring-[#4988C4]"
            />
            Đang mở hoạt động
          </label>

          <div className={adminUi.infoBox}>
            Học kỳ sau khi tạo không thể xóa. Ngày kết thúc phải sau ngày bắt đầu.
          </div>
        </form>
      </FormDialog>

      {offeringsSemester ? (
        <AdminSemesterOfferingsModal
          semester={offeringsSemester}
          onClose={() => setOfferingsSemester(null)}
          onSaved={() => void loadSemesters()}
        />
      ) : null}
    </div>
  )
}
