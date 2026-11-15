import { useEffect, useState, type FormEvent } from 'react'
import { courseCloService } from '@/services/courseClo.service'
import { CLO_BLOOM_LEVELS, type CloBloomLevel, type CourseClo, type ManagedCloCourse, type UpdateCourseCloInput } from '@/types/courseClo.types'
import { userMessage } from '@/lib/userMessage'

const control = 'w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm'
const button = 'rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold disabled:opacity-50'
const freshForm = (orderIndex = 1): UpdateCourseCloInput => ({ cloCode: '', description: '', bloomLevel: 'Understand', orderIndex, isActive: true })

export default function DepartmentCourseClosPage() {
  const [courses, setCourses] = useState<ManagedCloCourse[]>([])
  const [courseId, setCourseId] = useState('')
  const [clos, setClos] = useState<CourseClo[]>([])
  const [loading, setLoading] = useState(true)
  const [coursesLoaded, setCoursesLoaded] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [revision, setRevision] = useState(0)
  const [editing, setEditing] = useState<CourseClo | 'new' | null>(null)
  const [deleting, setDeleting] = useState<CourseClo | null>(null)
  const [form, setForm] = useState(freshForm())

  useEffect(() => {
    let active = true
    courseCloService.getManagedCourses().then(items => {
      if (!active) return
      setCourses(items)
      setCourseId(items[0]?.id ?? '')
      setCoursesLoaded(true)
      if (!items.length) setLoading(false)
    }).catch(failure => {
      if (active) { setError(userMessage(failure, 'Không tải được các môn phụ trách.')); setLoading(false) }
    })
    return () => { active = false }
  }, [])

  useEffect(() => {
    if (!courseId) return
    let active = true
    courseCloService.list(courseId).then(items => { if (active) setClos(items) })
      .catch(failure => { if (active) setError(userMessage(failure, 'Không tải được danh sách CLO.')) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [courseId, revision])

  function prepareLoad() {
    setLoading(true)
    setClos([])
    setEditing(null)
    setDeleting(null)
    setError('')
    setNotice('')
  }

  function startEdit(clo?: CourseClo) {
    setEditing(clo ?? 'new')
    setForm(clo ? {
      cloCode: clo.cloCode, description: clo.description, bloomLevel: clo.bloomLevel,
      orderIndex: clo.orderIndex, isActive: clo.isActive,
    } : freshForm(Math.max(0, ...clos.map(item => item.orderIndex)) + 1))
    setError('')
    setNotice('')
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (!editing || !courseId || busy) return
    const input = { ...form, cloCode: form.cloCode.trim().toUpperCase(), description: form.description.trim() }
    if (!input.cloCode || input.cloCode.length > 30 || !input.description || input.description.length > 1000
      || !Number.isInteger(input.orderIndex) || input.orderIndex < 1) {
      setError('Nhập mã CLO tối đa 30 ký tự, mô tả tối đa 1000 ký tự và thứ tự là số nguyên dương.'); return
    }
    if (clos.some(item => item.id !== (editing === 'new' ? '' : editing.id)
      && item.cloCode.trim().toUpperCase() === input.cloCode)) {
      setError('Mã CLO đã tồn tại trong môn học này.'); return
    }
    setBusy(true)
    setError('')
    try {
      const saved = editing === 'new'
        ? await courseCloService.create(courseId, { cloCode: input.cloCode, description: input.description, bloomLevel: input.bloomLevel, orderIndex: input.orderIndex })
        : await courseCloService.update(courseId, editing.id, input)
      setClos(current => [...current.filter(item => item.id !== saved.id), saved].sort((a, b) => a.orderIndex - b.orderIndex || a.cloCode.localeCompare(b.cloCode)))
      setEditing(null)
      setNotice('Đã lưu chuẩn đầu ra.')
    } catch (failure) { setError(userMessage(failure, 'Không lưu được CLO. Vui lòng thử lại.')) }
    finally { setBusy(false) }
  }

  async function remove() {
    if (!deleting || busy) return
    setBusy(true)
    setError('')
    try {
      await courseCloService.remove(courseId, deleting.id)
      setClos(current => current.filter(item => item.id !== deleting.id))
      setDeleting(null)
      setNotice('Đã xóa chuẩn đầu ra.')
    } catch (failure) { setDeleting(null); setError(userMessage(failure, 'Không xóa được CLO. Vui lòng kiểm tra các tham chiếu.')) }
    finally { setBusy(false) }
  }

  return <div className="mx-auto max-w-6xl space-y-5 p-4 sm:p-6">
    <header><h1 className="text-2xl font-bold text-slate-900">Chuẩn đầu ra môn học (CLO)</h1>
      <p className="mt-2 text-sm text-slate-600">Quản lý các chuẩn đầu ra được câu hỏi và cấu trúc đề tham chiếu. Mỗi CLO có một mã riêng trong môn học.</p></header>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-800">{error}</p>}
    {notice && <p role="status" className="rounded-lg bg-green-50 p-3 text-sm text-green-800">{notice}</p>}
    {courses.length > 0 && <section className="flex flex-wrap items-end gap-3 rounded-xl border border-slate-200 bg-white p-4">
      <label className="min-w-60 flex-1 text-sm font-semibold">Môn học phụ trách<select aria-label="Môn học phụ trách" className={`${control} mt-1`} value={courseId} disabled={busy} onChange={event => { prepareLoad(); setCourseId(event.target.value) }}>
        {courses.map(course => <option key={course.id} value={course.id}>{course.code} — {course.name}</option>)}
      </select></label>
      <button className={button} disabled={loading || busy} onClick={() => { prepareLoad(); setRevision(value => value + 1) }}>Tải lại</button>
      <button className={`${button} bg-blue-800 text-white`} disabled={loading || busy} onClick={() => startEdit()}>Thêm CLO</button>
    </section>}
    {loading ? <p role="status">Đang tải CLO...</p> : coursesLoaded && !courses.length
      ? <p className="rounded-xl border border-dashed p-6 text-slate-600">Bạn chưa được phân công phụ trách môn học nào.</p>
      : courseId && <section className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="w-full text-left text-sm"><caption className="sr-only">Danh sách CLO của môn học được chọn</caption>
          <thead className="bg-slate-50"><tr>{['Thứ tự', 'Mã CLO', 'Tên / mô tả', 'Bloom', 'Trạng thái', 'Thao tác'].map(label => <th key={label} className="p-3">{label}</th>)}</tr></thead>
          <tbody>{clos.map(clo => <tr key={clo.id} className="border-t border-slate-100">
            <td className="p-3">{clo.orderIndex}</td><td className="p-3 font-semibold">{clo.cloCode}</td><td className="max-w-md whitespace-pre-wrap p-3">{clo.description}</td><td className="p-3">{clo.bloomLevel}</td><td className="p-3">{clo.isActive ? 'Hoạt động' : 'Ngừng sử dụng'}</td>
            <td className="space-x-2 whitespace-nowrap p-3"><button className={button} aria-label={`Sửa ${clo.cloCode}`} disabled={busy} onClick={() => startEdit(clo)}>Sửa</button><button className={`${button} text-red-700`} aria-label={`Xóa ${clo.cloCode}`} disabled={busy} onClick={() => { setDeleting(clo); setError(''); setNotice('') }}>Xóa</button></td>
          </tr>)}{!clos.length && <tr><td colSpan={6} className="p-6 text-center text-slate-500">Môn học chưa có CLO. Thêm chuẩn đầu ra đầu tiên.</td></tr>}</tbody>
        </table>
      </section>}
    {editing && <section role="dialog" aria-modal="true" aria-label={editing === 'new' ? 'Tạo CLO' : `Sửa ${editing.cloCode}`} className="rounded-xl border border-slate-200 bg-white p-5 shadow-lg">
      <h2 className="mb-4 text-lg font-bold">{editing === 'new' ? 'Thêm chuẩn đầu ra' : 'Cập nhật chuẩn đầu ra'}</h2>
      <form onSubmit={event => void save(event)} className="grid gap-4 sm:grid-cols-2">
        <label className="text-sm font-semibold">Mã CLO<input className={`${control} mt-1`} value={form.cloCode} maxLength={30} required disabled={busy} onChange={event => setForm({ ...form, cloCode: event.target.value })} /></label>
        <label className="text-sm font-semibold">Thứ tự<input className={`${control} mt-1`} type="number" min={1} step={1} required disabled={busy} value={form.orderIndex} onChange={event => setForm({ ...form, orderIndex: Number(event.target.value) })} /></label>
        <label className="text-sm font-semibold sm:col-span-2">Tên / mô tả chuẩn đầu ra<textarea className={`${control} mt-1`} rows={3} maxLength={1000} required disabled={busy} value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} /></label>
        <label className="text-sm font-semibold">Cấp độ Bloom<select className={`${control} mt-1`} disabled={busy} value={form.bloomLevel} onChange={event => setForm({ ...form, bloomLevel: event.target.value as CloBloomLevel })}>{CLO_BLOOM_LEVELS.map(level => <option key={level}>{level}</option>)}</select></label>
        {editing !== 'new' && <label className="flex items-center gap-2 text-sm"><input type="checkbox" disabled={busy} checked={form.isActive} onChange={event => setForm({ ...form, isActive: event.target.checked })} />Hoạt động</label>}
        <div className="flex gap-3 sm:col-span-2"><button className={`${button} bg-blue-800 text-white`} type="submit" disabled={busy}>Lưu CLO</button><button className={button} type="button" disabled={busy} onClick={() => setEditing(null)}>Hủy</button></div>
      </form>
    </section>}
    {deleting && <section role="dialog" aria-modal="true" aria-label={`Xác nhận xóa ${deleting.cloCode}`} className="rounded-xl border border-red-200 bg-white p-5 shadow-lg">
      <h2 className="text-lg font-bold">Xác nhận xóa {deleting.cloCode}</h2><p className="my-3 text-sm">Bạn có chắc muốn xóa CLO này? CLO đang được câu hỏi hoặc cấu trúc đề sử dụng sẽ được giữ lại.</p>
      <div className="flex gap-3"><button className={`${button} bg-red-700 text-white`} disabled={busy} onClick={() => void remove()}>Xác nhận xóa</button><button className={button} disabled={busy} onClick={() => setDeleting(null)}>Hủy</button></div>
    </section>}
  </div>
}
