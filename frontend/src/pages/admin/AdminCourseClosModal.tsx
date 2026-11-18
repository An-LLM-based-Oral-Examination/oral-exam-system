import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { BookOpenCheck, LoaderCircle, Pencil, Plus, RefreshCw, Trash2, X } from 'lucide-react'
import { courseCloService } from '@/services/courseClo.service'
import {
  CLO_BLOOM_LEVELS,
  type CloBloomLevel,
  type CourseClo,
  type UpdateCourseCloInput,
} from '@/types/courseClo.types'
import type { AdminCourseItem } from '@/services/admin.service'
import { toast } from '@/components/common/toastStore'
import { userMessage } from '@/lib/userMessage'

interface AdminCourseClosModalProps {
  course: AdminCourseItem
  onClose: () => void
}

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
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<CourseClo | 'new' | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<CourseClo | null>(null)
  const [form, setForm] = useState<UpdateCourseCloInput>(() => makeDefaultForm(1))

  const loadClos = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const items = await courseCloService.list(course.id)
      setClos(items)
    } catch (err) {
      setError(userMessage(err, 'Không tải được danh sách chuẩn đầu ra (CLO) của môn học.'))
    } finally {
      setLoading(false)
    }
  }, [course.id])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadClos()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [loadClos])

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || busy) return
      if (confirmDelete) {
        setConfirmDelete(null)
      } else if (editing) {
        setEditing(null)
      } else {
        onClose()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [busy, confirmDelete, editing, onClose])

  const openEditor = (clo?: CourseClo) => {
    setError('')
    setConfirmDelete(null)
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
    if (!normalizedCode || normalizedCode.length > 30 || !normalizedDesc || normalizedDesc.length > 1000) {
      setError('Mã CLO bắt buộc 1–30 ký tự và mô tả bắt buộc 1–1000 ký tự.')
      return
    }
    if (!Number.isInteger(form.orderIndex) || form.orderIndex < 1) {
      setError('Thứ tự hiển thị phải là số nguyên dương.')
      return
    }

    const editingId = editing === 'new' ? '' : editing.id
    if (clos.some((c) => c.id !== editingId && c.cloCode.trim().toUpperCase() === normalizedCode)) {
      setError(`Mã CLO [${normalizedCode}] đã tồn tại trong môn ${course.code}.`)
      return
    }

    setBusy(true)
    setError('')
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
      toast.success(
        editing === 'new'
          ? `Đã tạo chuẩn đầu ra [${saved.cloCode}] cho môn ${course.code}.`
          : `Đã cập nhật chuẩn đầu ra [${saved.cloCode}].`,
      )
      setEditing(null)
    } catch (err) {
      const msg = userMessage(err, 'Không lưu được chuẩn đầu ra (CLO).')
      setError(msg)
      toast.error(msg)
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async () => {
    if (!confirmDelete || busy) return
    setBusy(true)
    setError('')
    try {
      await courseCloService.remove(course.id, confirmDelete.id)
      setClos((prev) => prev.filter((item) => item.id !== confirmDelete.id))
      toast.success(`Đã xóa chuẩn đầu ra [${confirmDelete.cloCode}].`)
      setConfirmDelete(null)
    } catch (err) {
      const status =
        typeof err === 'object' && err !== null && 'status' in err
          ? Number((err as { status?: unknown }).status)
          : 0
      const baseMsg = userMessage(err, 'Không xóa được chuẩn đầu ra.')
      const msg =
        status === 409
          ? `${baseMsg} (CLO đang được câu hỏi hoặc cấu trúc đề tham chiếu — chỉ được chuyển sang Ngừng sử dụng is_active = false qua nút Sửa).`
          : baseMsg
      setConfirmDelete(null)
      setError(msg)
      toast.error(msg)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={`Quản lý CLO môn ${course.code}`}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
    >
      <div className="flex max-h-[90vh] w-full max-w-3xl flex-col rounded-3xl border border-[#82aedb]/50 bg-white p-6 shadow-2xl">
        <div className="flex items-center justify-between border-b border-[#82aedb]/30 pb-3">
          <div className="flex items-center gap-2">
            <BookOpenCheck className="size-5 text-[#1C4D8D]" />
            <div>
              <h3 className="text-base font-bold text-[#0F2854]">
                Chuẩn Đầu Ra 3NF (course_clos) — {course.code}
              </h3>
              <p className="text-[11px] text-[#63738a]">
                {course.name} · CLO đã có câu hỏi hoặc cấu trúc đề tham chiếu sẽ bị chặn xóa vật lý (HTTP 409)
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={loading || busy}
              onClick={() => openEditor()}
              className="inline-flex items-center gap-1 rounded-xl bg-[#1C4D8D] px-3 py-1.5 text-xs font-bold text-white transition hover:bg-[#0F2854] disabled:opacity-50"
            >
              <Plus className="size-3.5" /> Thêm CLO
            </button>
            <button
              type="button"
              aria-label="Đóng"
              onClick={onClose}
              className="text-[#94a3b8] transition hover:text-[#0F2854]"
            >
              <X className="size-5" />
            </button>
          </div>
        </div>

        {error && (
          <div
            role="alert"
            className="mt-3 flex items-center justify-between gap-3 rounded-xl border border-[#f0c5c9] bg-[#fff5f6] px-3.5 py-2.5 text-xs font-semibold text-[#a43d4b]"
          >
            <span>{error}</span>
            <button
              type="button"
              onClick={() => void loadClos()}
              className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-[#f0c5c9] bg-white px-2.5 py-1 text-[11px] font-bold text-[#a43d4b]"
            >
              <RefreshCw className="size-3" /> Tải lại
            </button>
          </div>
        )}

        <div className="mt-4 min-h-0 flex-1 overflow-y-auto space-y-4">
          {editing && (
            <form
              onSubmit={handleSave}
              className="rounded-2xl border border-[#82aedb]/50 bg-[#F4FBFD] p-4 space-y-3 text-xs"
            >
              <h4 className="font-bold text-[#0F2854]">
                {editing === 'new' ? 'Thêm chuẩn đầu ra mới' : `Chỉnh sửa ${editing.cloCode}`}
              </h4>
              <div className="grid gap-3 sm:grid-cols-3">
                <div>
                  <label className="block font-bold text-[#0F2854]">Mã CLO (*)</label>
                  <input
                    type="text"
                    required
                    maxLength={30}
                    value={form.cloCode}
                    onChange={(e) => setForm({ ...form, cloCode: e.target.value })}
                    placeholder="CLO1"
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 font-mono text-xs font-bold uppercase outline-none"
                  />
                </div>
                <div>
                  <label className="block font-bold text-[#0F2854]">Bậc Bloom (*)</label>
                  <select
                    value={form.bloomLevel}
                    onChange={(e) => setForm({ ...form, bloomLevel: e.target.value as CloBloomLevel })}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 text-xs font-semibold outline-none"
                  >
                    {CLO_BLOOM_LEVELS.map((level) => (
                      <option key={level} value={level}>
                        {level}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block font-bold text-[#0F2854]">Thứ tự (*)</label>
                  <input
                    type="number"
                    min={1}
                    step={1}
                    required
                    value={form.orderIndex}
                    onChange={(e) => setForm({ ...form, orderIndex: Number(e.target.value) })}
                    className="mt-1 w-full rounded-xl border border-[#82aedb]/50 bg-white px-3 py-2 font-mono text-xs outline-none"
                  />
                </div>
              </div>
              <div>
                <label className="block font-bold text-[#0F2854]">Mô tả chuẩn đầu ra (*)</label>
                <textarea
                  required
                  maxLength={1000}
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                  placeholder="Mô tả năng lực sinh viên cần đạt..."
                  className="mt-1 min-h-16 w-full rounded-xl border border-[#82aedb]/50 bg-white p-2.5 text-xs outline-none"
                />
              </div>
              {editing !== 'new' && (
                <label className="inline-flex items-center gap-2 font-semibold text-[#0F2854]">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                    className="size-4 rounded"
                  />
                  Đang hoạt động (is_active)
                </label>
              )}
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setEditing(null)}
                  className="rounded-xl border border-[#82aedb]/50 bg-white px-3.5 py-1.5 font-bold text-[#64748b]"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={busy}
                  className="inline-flex items-center gap-1.5 rounded-xl bg-[#0F2854] px-4 py-1.5 font-bold text-white disabled:opacity-50"
                >
                  {busy && <LoaderCircle className="size-3.5 animate-spin" />}
                  Lưu CLO
                </button>
              </div>
            </form>
          )}

          {confirmDelete && (
            <div className="rounded-2xl border border-[#f0c5c9] bg-[#fff5f6] p-4 text-xs">
              <p className="font-bold text-[#a43d4b]">
                Xác nhận xóa chuẩn đầu ra [{confirmDelete.cloCode}]?
              </p>
              <p className="mt-1 text-[#63738a]">
                Nếu CLO đã được câu hỏi hoặc cấu trúc đề tham chiếu, hệ thống sẽ từ chối xóa (HTTP 409 Conflict) để bảo vệ toàn vẹn 3NF.
              </p>
              <div className="mt-3 flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setConfirmDelete(null)}
                  className="rounded-xl border border-[#82aedb]/50 bg-white px-3 py-1.5 font-bold text-[#64748b]"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => void handleDelete()}
                  className="inline-flex items-center gap-1 rounded-xl bg-[#dc2626] px-3.5 py-1.5 font-bold text-white disabled:opacity-50"
                >
                  {busy && <LoaderCircle className="size-3.5 animate-spin" />}
                  Xác nhận xóa CLO
                </button>
              </div>
            </div>
          )}

          <div className="overflow-hidden rounded-2xl border border-[#82aedb]/40">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-[#82aedb]/40 bg-[#F4FBFD] font-bold text-[#0F2854] uppercase">
                <tr>
                  <th className="px-3 py-2.5">Thứ tự</th>
                  <th className="px-3 py-2.5">Mã CLO</th>
                  <th className="px-3 py-2.5">Mô tả</th>
                  <th className="px-3 py-2.5">Bloom</th>
                  <th className="px-3 py-2.5">Trạng thái</th>
                  <th className="px-3 py-2.5 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#82aedb]/25">
                {loading ? (
                  <tr>
                    <td colSpan={6} className="py-10 text-center text-[#63738a]">
                      <LoaderCircle className="mx-auto size-5 animate-spin text-[#1C4D8D]" />
                      <span className="mt-2 block">Đang tải danh sách CLO...</span>
                    </td>
                  </tr>
                ) : clos.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-10 text-center text-[#63738a]">
                      Môn học chưa có CLO nào. Bấm "Thêm CLO" để khởi tạo chuẩn đầu ra đầu tiên.
                    </td>
                  </tr>
                ) : (
                  clos.map((clo) => (
                    <tr key={clo.id} className="hover:bg-[#F4FBFD]/50">
                      <td className="px-3 py-2.5 font-mono font-bold text-[#0F2854]">
                        {clo.orderIndex}
                      </td>
                      <td className="px-3 py-2.5 font-mono font-bold text-[#1C4D8D]">
                        {clo.cloCode}
                      </td>
                      <td className="max-w-sm whitespace-pre-wrap px-3 py-2.5 text-[#0F2854]">
                        {clo.description}
                      </td>
                      <td className="px-3 py-2.5 font-semibold text-[#63738a]">{clo.bloomLevel}</td>
                      <td className="px-3 py-2.5">
                        {clo.isActive ? (
                          <span className="rounded-full bg-[#ecfdf5] px-2 py-0.5 font-bold text-[#059669]">
                            Hoạt động
                          </span>
                        ) : (
                          <span className="rounded-full bg-[#f1f5f9] px-2 py-0.5 font-bold text-[#64748b]">
                            Ngừng dùng
                          </span>
                        )}
                      </td>
                      <td className="px-3 py-2.5 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          <button
                            type="button"
                            disabled={busy}
                            onClick={() => openEditor(clo)}
                            className="inline-flex items-center gap-1 rounded-lg border border-[#82aedb]/50 px-2 py-1 text-[11px] font-bold text-[#1C4D8D] hover:bg-[#edf5fc]"
                          >
                            <Pencil className="size-3" /> Sửa
                          </button>
                          <button
                            type="button"
                            disabled={busy}
                            onClick={() => setConfirmDelete(clo)}
                            className="inline-flex items-center gap-1 rounded-lg border border-[#ef4444]/40 px-2 py-1 text-[11px] font-bold text-[#dc2626] hover:bg-[#fef2f2]"
                          >
                            <Trash2 className="size-3" /> Xóa
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  )
}
