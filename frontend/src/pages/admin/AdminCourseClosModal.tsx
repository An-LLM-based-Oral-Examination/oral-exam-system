import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { BookOpenCheck, LoaderCircle, Pencil, Plus, ShieldAlert, Trash2 } from 'lucide-react'
import { EmptyState } from '@/components/common/EmptyState'
import { FieldError } from '@/components/common/formField'
import { fieldInvalidClass } from '@/components/common/formFieldClasses'
import { LoadingState } from '@/components/common/LoadingState'
import { toast } from '@/components/common/toastStore'
import { ConfirmDialog } from '@/design/ConfirmDialog'
import { FormDialog } from '@/design/overlays'
import { ds } from '@/design/system'
import {
  extractProblemDetails,
  mapProblemFieldErrors,
  type NormalizedProblem,
} from '@/lib/problemDetails'
import { cn } from '@/lib/utils'
import { AdminStateBanner } from '@/pages/admin/components/AdminStateBanner'
import { adminUi } from '@/pages/admin/components/adminUi'
import type { AdminCourseItem } from '@/services/admin.service'
import { courseCloService } from '@/services/courseClo.service'
import {
  CLO_BLOOM_LEVELS,
  type CloBloomLevel,
  type CourseClo,
  type UpdateCourseCloInput,
} from '@/types/courseClo.types'

interface AdminCourseClosModalProps {
  course: AdminCourseItem
  onClose: () => void
}

type CloField = 'cloCode' | 'bloomLevel' | 'orderIndex' | 'description'
const CLO_FIELDS: readonly CloField[] = ['cloCode', 'bloomLevel', 'orderIndex', 'description']

function makeDefaultForm(nextOrderIndex = 1): UpdateCourseCloInput {
  return {
    cloCode: '',
    description: '',
    bloomLevel: 'Understand',
    orderIndex: nextOrderIndex,
    isActive: true,
  }
}

export function AdminCourseClosModal({ course, onClose }: AdminCourseClosModalProps) {
  const [clos, setClos] = useState<CourseClo[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [problem, setProblem] = useState<NormalizedProblem | null>(null)
  const [statusMessage, setStatusMessage] = useState('')
  const [editing, setEditing] = useState<CourseClo | 'new' | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<CourseClo | null>(null)
  const [conflictedClo, setConflictedClo] = useState<CourseClo | null>(null)
  const [form, setForm] = useState<UpdateCourseCloInput>(() => makeDefaultForm(1))
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<CloField, string>>>({})
  const requestIdRef = useRef(0)

  const loadClos = useCallback(async () => {
    const requestId = ++requestIdRef.current
    setLoading(true)
    setProblem(null)
    try {
      const items = await courseCloService.list(course.id)
      if (requestId !== requestIdRef.current) return
      setClos(items)
    } catch (err: unknown) {
      if (requestId !== requestIdRef.current) return
      setProblem(
        extractProblemDetails(err, 'Không tải được danh sách chuẩn đầu ra (CLO) của môn học.'),
      )
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false)
      }
    }
  }, [course.id])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadClos()
    }, 0)
    return () => {
      window.clearTimeout(timer)
      requestIdRef.current += 1
    }
  }, [loadClos])

  // Close inline editor on Escape before outer FormDialog closes
  useEffect(() => {
    if (!editing || confirmDelete || busy) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      event.stopPropagation()
      setEditing(null)
      setFieldErrors({})
    }
    window.addEventListener('keydown', onKeyDown, true)
    return () => window.removeEventListener('keydown', onKeyDown, true)
  }, [editing, confirmDelete, busy])

  const openEditor = (clo?: CourseClo) => {
    setProblem(null)
    setStatusMessage('')
    setConflictedClo(null)
    setConfirmDelete(null)
    setFieldErrors({})
    if (clo) {
      setEditing(clo)
      setForm({
        cloCode: clo.cloCode,
        description: clo.description,
        bloomLevel: clo.bloomLevel,
        orderIndex: clo.orderIndex,
        isActive: clo.isActive,
      })
    } else {
      const maxOrder = clos.reduce((max, item) => Math.max(max, item.orderIndex), 0)
      setEditing('new')
      setForm(makeDefaultForm(maxOrder + 1))
    }
  }

  const handleSave = async (event: FormEvent) => {
    event.preventDefault()
    if (!editing || busy) return

    const normalizedCode = form.cloCode.trim().toUpperCase()
    const normalizedDesc = form.description.trim()
    const nextErrors: Partial<Record<CloField, string>> = {}

    if (!normalizedCode || normalizedCode.length > 30) {
      nextErrors.cloCode = 'Mã CLO bắt buộc từ 1 đến 30 ký tự.'
    }
    if (!normalizedDesc || normalizedDesc.length > 1000) {
      nextErrors.description = 'Mô tả chuẩn đầu ra bắt buộc từ 1 đến 1000 ký tự.'
    }
    if (!Number.isInteger(form.orderIndex) || form.orderIndex < 1) {
      nextErrors.orderIndex = 'Thứ tự hiển thị phải là số nguyên dương (>= 1).'
    }

    const editingId = editing === 'new' ? '' : editing.id
    if (
      normalizedCode &&
      clos.some((c) => c.id !== editingId && c.cloCode.trim().toUpperCase() === normalizedCode)
    ) {
      nextErrors.cloCode = `Mã CLO [${normalizedCode}] đã tồn tại trong môn ${course.code}.`
    }

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors(nextErrors)
      return
    }

    setBusy(true)
    setProblem(null)
    setFieldErrors({})
    setStatusMessage('')
    setConflictedClo(null)
    try {
      const saved =
        editing === 'new'
          ? await courseCloService.create(course.id, {
              cloCode: normalizedCode,
              description: normalizedDesc,
              bloomLevel: form.bloomLevel,
              orderIndex: form.orderIndex,
            })
          : await courseCloService.update(course.id, editing.id, {
              cloCode: normalizedCode,
              description: normalizedDesc,
              bloomLevel: form.bloomLevel,
              orderIndex: form.orderIndex,
              isActive: form.isActive,
            })

      setClos((prev) =>
        [...prev.filter((item) => item.id !== saved.id), saved].sort(
          (a, b) => a.orderIndex - b.orderIndex || a.cloCode.localeCompare(b.cloCode),
        ),
      )
      const msg =
        editing === 'new'
          ? `Đã tạo chuẩn đầu ra [${saved.cloCode}] cho môn ${course.code}.`
          : `Đã cập nhật chuẩn đầu ra [${saved.cloCode}].`
      setStatusMessage(msg)
      toast.success(msg)
      setEditing(null)
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không lưu được chuẩn đầu ra (CLO).')
      setFieldErrors(
        mapProblemFieldErrors(err, CLO_FIELDS, {
          cloCode: 'cloCode',
          description: 'description',
          bloomLevel: 'bloomLevel',
          orderIndex: 'orderIndex',
        }),
      )
      setProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async () => {
    if (!confirmDelete || busy) return
    const target = confirmDelete
    setBusy(true)
    setProblem(null)
    setStatusMessage('')
    setConflictedClo(null)
    try {
      await courseCloService.remove(course.id, target.id)
      setClos((prev) => prev.filter((item) => item.id !== target.id))
      const msg = `Đã xóa chuẩn đầu ra [${target.cloCode}].`
      setStatusMessage(msg)
      toast.success(msg)
      setConfirmDelete(null)
    } catch (err: unknown) {
      const parsed = extractProblemDetails(err, 'Không xóa được chuẩn đầu ra.')
      setConfirmDelete(null)
      if (parsed.isConflict || parsed.status === 409) {
        setConflictedClo(target)
        const conflictDetail = `${parsed.detail} (CLO [${target.cloCode}] đang được câu hỏi hoặc cấu trúc đề tham chiếu — chỉ được chuyển sang Ngừng áp dụng is_active = false.)`
        setProblem({
          ...parsed,
          status: 409,
          isConflict: true,
          detail: conflictDetail,
        })
        toast.error(conflictDetail)
      } else {
        setProblem(parsed)
        toast.error(parsed.detail)
      }
    } finally {
      setBusy(false)
    }
  }

  const handleDeactivateConflictedClo = async () => {
    if (!conflictedClo || busy) return
    const target = conflictedClo
    setBusy(true)
    setProblem(null)
    setStatusMessage('')
    try {
      const updated = await courseCloService.update(course.id, target.id, {
        cloCode: target.cloCode,
        description: target.description,
        bloomLevel: target.bloomLevel,
        orderIndex: target.orderIndex,
        isActive: false,
      })
      setClos((prev) =>
        prev.map((item) => (item.id === updated.id ? updated : item)),
      )
      setConflictedClo(null)
      const msg = `Đã chuyển chuẩn đầu ra [${updated.cloCode}] sang ngừng áp dụng (is_active = false).`
      setStatusMessage(msg)
      toast.success(msg)
    } catch (err: unknown) {
      const parsed = extractProblemDetails(
        err,
        `Không thể chuyển [${target.cloCode}] sang ngừng áp dụng.`,
      )
      setProblem(parsed)
      toast.error(parsed.detail)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <FormDialog
        open
        variant="calm"
        size="xl"
        busy={busy}
        onClose={() => {
          if (!busy && !confirmDelete) onClose()
        }}
        icon={BookOpenCheck}
        eyebrow={course.name}
        title={`Quản lý CLO — ${course.code}`}
        headerExtra={
          <button
            type="button"
            disabled={loading || busy}
            onClick={() => openEditor()}
            className={adminUi.btnPrimary}
          >
            <Plus className="size-4" strokeWidth={1.75} aria-hidden="true" />
            Thêm CLO
          </button>
        }
      >
        <div className="space-y-3">
          <div className={adminUi.infoBox}>
            CLO đã được câu hỏi hoặc cấu trúc đề tham chiếu sẽ không thể xóa vật lý (HTTP 409); hãy
            chuyển sang <code className="font-mono font-semibold text-[#0F2854]">is_active = false</code>{' '}
            để ngừng áp dụng.
          </div>

          <AdminStateBanner
            problem={problem}
            statusMessage={statusMessage}
            onRetry={problem && !problem.isConflict ? () => void loadClos() : undefined}
            onDismiss={() => {
              setProblem(null)
              setStatusMessage('')
              setConflictedClo(null)
            }}
          />

          {conflictedClo && conflictedClo.isActive ? (
            <div className="flex flex-col gap-3 rounded-xl border border-amber-400 bg-amber-50 p-3.5 text-sm text-amber-900 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-start gap-2.5">
                <ShieldAlert className="mt-0.5 size-4 shrink-0 text-amber-700" strokeWidth={1.75} aria-hidden="true" />
                <div>
                  <p className="font-semibold text-[#0F2854]">
                    Không thể xóa vật lý [{conflictedClo.cloCode}] vì đang có dữ liệu tham chiếu.
                  </p>
                  <p className="mt-0.5 text-xs text-slate-700">
                    Ngừng áp dụng chuẩn đầu ra này để ẩn khỏi các luồng tạo câu hỏi mới.
                  </p>
                </div>
              </div>
              <button
                type="button"
                disabled={busy}
                onClick={() => void handleDeactivateConflictedClo()}
                className={cn(adminUi.btnSecondary, 'shrink-0')}
              >
                {busy && (
                  <LoaderCircle
                    className="size-4 animate-spin motion-reduce:animate-none"
                    aria-hidden="true"
                  />
                )}
                Ngừng áp dụng CLO này (is_active = false)
              </button>
            </div>
          ) : null}

          {editing ? (
            <form
              onSubmit={handleSave}
              noValidate
              className="space-y-3 rounded-xl border border-slate-300 bg-[#f7fafd] p-4"
            >
              <h3 className="text-base font-bold text-[#0F2854]">
                {editing === 'new' ? 'Thêm chuẩn đầu ra mới' : `Chỉnh sửa ${editing.cloCode}`}
              </h3>
              <div className="grid gap-3 sm:grid-cols-3">
                <div>
                  <label htmlFor="clo-code-input" className={adminUi.label}>
                    Mã CLO
                  </label>
                  <input
                    id="clo-code-input"
                    type="text"
                    maxLength={30}
                    value={form.cloCode}
                    onChange={(e) => {
                      setForm({ ...form, cloCode: e.target.value })
                      setFieldErrors((prev) => ({ ...prev, cloCode: undefined }))
                    }}
                    placeholder="CLO1"
                    aria-invalid={Boolean(fieldErrors.cloCode)}
                    className={cn(
                      ds.field,
                      adminUi.input,
                      'mt-1.5 font-mono font-semibold uppercase',
                      fieldErrors.cloCode && fieldInvalidClass,
                    )}
                  />
                  <FieldError message={fieldErrors.cloCode} />
                </div>

                <div>
                  <label htmlFor="clo-bloom-select" className={adminUi.label}>
                    Bậc Bloom
                  </label>
                  <select
                    id="clo-bloom-select"
                    value={form.bloomLevel}
                    onChange={(e) =>
                      setForm({ ...form, bloomLevel: e.target.value as CloBloomLevel })
                    }
                    className={cn(ds.field, adminUi.input, 'mt-1.5 font-medium')}
                  >
                    {CLO_BLOOM_LEVELS.map((level) => (
                      <option key={level} value={level}>
                        {level}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label htmlFor="clo-order-input" className={adminUi.label}>
                    Thứ tự hiển thị
                  </label>
                  <input
                    id="clo-order-input"
                    type="number"
                    min={1}
                    step={1}
                    value={form.orderIndex}
                    onChange={(e) => {
                      setForm({ ...form, orderIndex: Number(e.target.value) })
                      setFieldErrors((prev) => ({ ...prev, orderIndex: undefined }))
                    }}
                    aria-invalid={Boolean(fieldErrors.orderIndex)}
                    className={cn(
                      ds.field,
                      adminUi.input,
                      'mt-1.5 font-mono tabular-nums',
                      fieldErrors.orderIndex && fieldInvalidClass,
                    )}
                  />
                  <FieldError message={fieldErrors.orderIndex} />
                </div>
              </div>

              <div>
                <label htmlFor="clo-description-input" className={adminUi.label}>
                  Mô tả chuẩn đầu ra
                </label>
                <textarea
                  id="clo-description-input"
                  maxLength={1000}
                  value={form.description}
                  onChange={(e) => {
                    setForm({ ...form, description: e.target.value })
                    setFieldErrors((prev) => ({ ...prev, description: undefined }))
                  }}
                  placeholder="Mô tả năng lực sinh viên cần đạt…"
                  aria-invalid={Boolean(fieldErrors.description)}
                  className={cn(adminUi.textarea, fieldErrors.description && fieldInvalidClass)}
                />
                <FieldError message={fieldErrors.description} />
              </div>

              {editing !== 'new' ? (
                <label className="inline-flex min-h-11 items-center gap-2 text-sm font-medium text-[#0F2854]">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                    className="size-4 rounded border-slate-400 text-[#1C4D8D] focus:ring-[#4988C4]"
                  />
                  Đang hoạt động (is_active)
                </label>
              ) : null}

              <div className="flex flex-wrap justify-end gap-2">
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => {
                    setEditing(null)
                    setFieldErrors({})
                  }}
                  className={adminUi.btnSubtle}
                >
                  Hủy
                </button>
                <button type="submit" disabled={busy} className={adminUi.btnPrimary}>
                  {busy && (
                    <LoaderCircle
                      className="size-4 animate-spin motion-reduce:animate-none"
                      aria-hidden="true"
                    />
                  )}
                  Lưu CLO
                </button>
              </div>
            </form>
          ) : null}

          <div className="overflow-hidden rounded-xl border border-slate-300 bg-white">
            {loading ? (
              <LoadingState label="Đang tải danh sách CLO…" className="min-h-48" />
            ) : problem && clos.length === 0 ? null : clos.length === 0 ? (
              <div className="p-4">
                <EmptyState
                  compact
                  icon={BookOpenCheck}
                  title="Môn học chưa có CLO nào"
                  description="Bấm Thêm CLO để khởi tạo chuẩn đầu ra đầu tiên cho môn học này."
                />
              </div>
            ) : (
              <>
                <div className="hidden overflow-x-auto md:block">
                  <table className={adminUi.table}>
                    <thead className={adminUi.thead}>
                      <tr>
                        <th className="px-4 py-3">Thứ tự</th>
                        <th className="px-4 py-3">Mã CLO</th>
                        <th className="px-4 py-3">Mô tả</th>
                        <th className="px-4 py-3">Bloom</th>
                        <th className="px-4 py-3">Trạng thái</th>
                        <th className="px-4 py-3 text-right">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className={adminUi.tbody}>
                      {clos.map((clo) => (
                        <tr key={clo.id} className={adminUi.tr}>
                          <td className="px-4 py-3.5 font-mono text-sm font-semibold text-[#0F2854] tabular-nums">
                            {clo.orderIndex}
                          </td>
                          <td className="px-4 py-3.5 font-mono text-sm font-bold text-[#1C4D8D]">
                            {clo.cloCode}
                          </td>
                          <td className="max-w-sm whitespace-pre-wrap px-4 py-3.5 text-sm leading-relaxed text-[#0F2854]">
                            {clo.description}
                          </td>
                          <td className="px-4 py-3.5 text-[13px] font-semibold text-slate-700">
                            {clo.bloomLevel}
                          </td>
                          <td className="px-4 py-3.5">
                            <span className={clo.isActive ? adminUi.badgeSuccess : adminUi.badgeNeutral}>
                              {clo.isActive ? 'Hoạt động' : 'Ngừng dùng'}
                            </span>
                          </td>
                          <td className="px-4 py-3.5 text-right">
                            <div className="flex items-center justify-end gap-1.5">
                              <button
                                type="button"
                                disabled={busy}
                                aria-label={`Sửa ${clo.cloCode}`}
                                onClick={() => openEditor(clo)}
                                className={adminUi.btnRowPrimary}
                              >
                                <Pencil className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
                                Sửa
                              </button>
                              <button
                                type="button"
                                disabled={busy}
                                aria-label={`Xóa ${clo.cloCode}`}
                                onClick={() => setConfirmDelete(clo)}
                                className={adminUi.btnRowDanger}
                              >
                                <Trash2 className="size-3.5" strokeWidth={1.75} aria-hidden="true" />
                                Xóa
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="space-y-3 p-3.5 md:hidden">
                  {clos.map((clo) => (
                    <article key={clo.id} className={adminUi.mobileCard}>
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-mono text-sm font-bold text-[#1C4D8D]">
                              {clo.cloCode}
                            </span>
                            <span className={cn(adminUi.badgeNeutral, 'tabular-nums')}>
                              #{clo.orderIndex} · {clo.bloomLevel}
                            </span>
                          </div>
                          <p className="mt-2 text-sm leading-relaxed text-[#0F2854]">
                            {clo.description}
                          </p>
                        </div>
                        <span
                          className={cn(
                            'shrink-0',
                            clo.isActive ? adminUi.badgeSuccess : adminUi.badgeNeutral,
                          )}
                        >
                          {clo.isActive ? 'Hoạt động' : 'Ngừng dùng'}
                        </span>
                      </div>

                      <div className="mt-3 grid grid-cols-2 gap-2">
                        <button
                          type="button"
                          disabled={busy}
                          aria-label={`Sửa ${clo.cloCode} (mobile)`}
                          onClick={() => openEditor(clo)}
                          className={adminUi.btnMobilePrimary}
                        >
                          <Pencil className="size-4" strokeWidth={1.75} aria-hidden="true" />
                          Sửa
                        </button>
                        <button
                          type="button"
                          disabled={busy}
                          aria-label={`Xóa ${clo.cloCode} (mobile)`}
                          onClick={() => setConfirmDelete(clo)}
                          className={adminUi.btnMobileDanger}
                        >
                          <Trash2 className="size-4" strokeWidth={1.75} aria-hidden="true" />
                          Xóa
                        </button>
                      </div>
                    </article>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>
      </FormDialog>

      {confirmDelete ? (
        <ConfirmDialog
          variant="calm"
          tone="danger"
          eyebrow="Xác nhận xóa chuẩn đầu ra"
          title={`Xóa chuẩn đầu ra [${confirmDelete.cloCode}]?`}
          message="Nếu CLO đã được câu hỏi hoặc cấu trúc đề tham chiếu, hệ thống sẽ từ chối xóa (HTTP 409 Conflict) để bảo vệ toàn vẹn 3NF và gợi ý chuyển sang is_active = false."
          confirmLabel="Xác nhận xóa CLO"
          cancelLabel="Hủy"
          pending={busy}
          onConfirm={() => void handleDelete()}
          onCancel={() => {
            if (!busy) setConfirmDelete(null)
          }}
        />
      ) : null}
    </>
  )
}
