import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import DepartmentCourseClosPage from './DepartmentCourseClosPage'
import { courseCloService } from '@/services/courseClo.service'
import type { CourseClo } from '@/types/courseClo.types'

vi.mock('@/services/courseClo.service', () => ({
  courseCloService: { getManagedCourses: vi.fn(), list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn() },
}))

const course = { id: 'course-1', code: 'PRN231', name: 'Backend' }
const clo: CourseClo = { id: 'clo-1', courseId: course.id, cloCode: 'CLO1', description: 'Understand APIs', bloomLevel: 'Understand', orderIndex: 1, isActive: true, createdAt: '2026-11-14T00:00:00Z' }

describe('DepartmentCourseClosPage', () => {
  beforeEach(() => {
    vi.mocked(courseCloService.getManagedCourses).mockResolvedValue([course])
    vi.mocked(courseCloService.list).mockResolvedValue([clo])
  })
  afterEach(() => { cleanup(); vi.resetAllMocks() })

  it('loads owned courses and creates a normalized CLO using the current description field', async () => {
    vi.mocked(courseCloService.create).mockResolvedValue({ ...clo, id: 'clo-2', cloCode: 'CLO2' })
    render(<DepartmentCourseClosPage />)
    await screen.findByText('Understand APIs')
    expect(courseCloService.list).toHaveBeenCalledWith(course.id)
    fireEvent.click(screen.getByRole('button', { name: 'Thêm CLO' }))
    fireEvent.change(screen.getByLabelText('Mã CLO'), { target: { value: ' clo2 ' } })
    fireEvent.change(screen.getByLabelText('Tên / mô tả chuẩn đầu ra'), { target: { value: ' Create APIs ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu CLO' }))
    await waitFor(() => expect(courseCloService.create).toHaveBeenCalledWith(course.id, {
      cloCode: 'CLO2', description: 'Create APIs', bloomLevel: 'Understand', orderIndex: 2,
    }))
  })

  it('rejects a case-insensitive duplicate before sending a request', async () => {
    render(<DepartmentCourseClosPage />)
    await screen.findByText('Understand APIs')
    fireEvent.click(screen.getByRole('button', { name: 'Thêm CLO' }))
    fireEvent.change(screen.getByLabelText('Mã CLO'), { target: { value: 'clo1' } })
    fireEvent.change(screen.getByLabelText('Tên / mô tả chuẩn đầu ra'), { target: { value: 'Duplicate outcome' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu CLO' }))
    expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'Mã CLO đã tồn tại trong môn học.')
    expect(courseCloService.create).not.toHaveBeenCalled()
  })

  it('confirms deletion and displays a dependency error without removing the row', async () => {
    vi.mocked(courseCloService.remove).mockRejectedValue({ status: 409, detail: 'CLO_IN_USE: CLO đang được câu hỏi sử dụng.' })
    render(<DepartmentCourseClosPage />)
    await screen.findByText('Understand APIs')
    fireEvent.click(screen.getByRole('button', { name: 'Xóa CLO1' }))
    const dialog = screen.getByRole('alertdialog', { name: 'Xác nhận xóa CLO1' })
    expect(courseCloService.remove).not.toHaveBeenCalled()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận xóa' }))
    expect((await screen.findByRole('alert')).textContent).toContain('CLO đang được câu hỏi sử dụng')
    expect(screen.getByText('Understand APIs')).not.toBeNull()
  })

  it('updates the selected CLO without changing its course', async () => {
    vi.mocked(courseCloService.update).mockResolvedValue({ ...clo, description: 'Updated outcome' })
    render(<DepartmentCourseClosPage />)
    await screen.findByText('Understand APIs')
    fireEvent.click(screen.getByRole('button', { name: 'Sửa CLO1' }))
    fireEvent.change(screen.getByLabelText('Tên / mô tả chuẩn đầu ra'), { target: { value: 'Updated outcome' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu CLO' }))
    await waitFor(() => expect(courseCloService.update).toHaveBeenCalledWith(course.id, clo.id, {
      cloCode: 'CLO1', description: 'Updated outcome', bloomLevel: 'Understand', orderIndex: 1, isActive: true,
    }))
  })

  it('shows no edit actions when no courses are assigned', async () => {
    vi.mocked(courseCloService.getManagedCourses).mockResolvedValue([])
    render(<DepartmentCourseClosPage />)
    await screen.findByText('Bạn chưa được phân công phụ trách môn học nào.')
    expect(screen.queryByRole('button', { name: 'Thêm CLO' })).toBeNull()
    expect(courseCloService.list).not.toHaveBeenCalled()
  })
})
