import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import DepartmentLecturerAssignmentPage from './DepartmentLecturerAssignmentPage'

const mocks = vi.hoisted(() => ({
  role: 'DepartmentHead',
  offerings: vi.fn(),
  assignments: vi.fn(),
  search: vi.fn(),
  sync: vi.fn(),
}))
vi.mock('@/hooks/useAuth', () => ({ useAuth: () => ({ user: { role: mocks.role } }) }))
vi.mock('@/services/lecturerAssignment.service', () => ({ lecturerAssignmentService: mocks }))

const offering = {
  courseOfferingId: 'offering-fall',
  courseId: 'course',
  courseCode: 'PRN212',
  courseName: 'Programming',
  semesterId: 'fall',
  semesterCode: 'FA26',
  isOfferingEnabled: true,
}
const lecturer = {
  lecturerId: 'teacher-a',
  fullName: 'Nguyễn An',
  email: 'an@example.test',
  isActive: true,
  semesterId: 'fall',
  semesterCode: 'FA26',
  courseOfferingId: 'offering-fall',
}
const candidate = { id: 'teacher-b', fullName: 'Trần Bình', email: 'binh@example.test' }

beforeEach(() => {
  mocks.role = 'DepartmentHead'
  mocks.offerings.mockResolvedValue([offering])
  mocks.assignments.mockResolvedValue([lecturer])
  mocks.search.mockResolvedValue([candidate])
  mocks.sync.mockResolvedValue({
    courseOfferingId: offering.courseOfferingId,
    lecturers: [
      lecturer,
      {
        ...lecturer,
        lecturerId: candidate.id,
        fullName: candidate.fullName,
        email: candidate.email,
      },
    ],
  })
})

describe('Department lecturer assignments', () => {
  it('does not load assignment data for other portal roles', () => {
    mocks.role = 'Admin'
    render(<DepartmentLecturerAssignmentPage />)
    expect(screen.getByRole('alert').textContent).toContain('Chỉ Trưởng bộ môn')
    expect(mocks.offerings).not.toHaveBeenCalled()
  })

  it('filters offerings by semester and loads actual assignments of the selected offering', async () => {
    mocks.offerings.mockResolvedValue([
      offering,
      {
        ...offering,
        courseOfferingId: 'offering-spring',
        semesterId: 'spring',
        semesterCode: 'SP27',
      },
    ])
    render(<DepartmentLecturerAssignmentPage />)
    await screen.findByRole('button', { name: 'Gỡ phân công Nguyễn An' })
    expect(mocks.assignments).toHaveBeenCalledWith('offering-fall', expect.any(AbortSignal))
    fireEvent.change(screen.getByLabelText('Học kỳ'), { target: { value: 'spring' } })
    await waitFor(() =>
      expect(mocks.assignments).toHaveBeenCalledWith('offering-spring', expect.any(AbortSignal)),
    )
  })

  it('searches only after two characters, selects a lecturer and requires confirmation before adding', async () => {
    render(<DepartmentLecturerAssignmentPage />)
    await screen.findByRole('button', { name: 'Gỡ phân công Nguyễn An' })
    fireEvent.change(screen.getByLabelText('Tìm giảng viên theo tên hoặc email'), {
      target: { value: 'b' },
    })
    expect(mocks.search).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText('Tìm giảng viên theo tên hoặc email'), {
      target: { value: 'binh' },
    })
    await screen.findByRole('option', { name: 'Trần Bình — binh@example.test' })
    fireEvent.change(screen.getByLabelText('Giảng viên đang hoạt động'), {
      target: { value: candidate.id },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Thêm giảng viên' }))
    expect(screen.getByRole('alertdialog')).toBeTruthy()
    expect(screen.getByRole('alertdialog').textContent).toContain('Giảng viên sẽ được cấp quyền')
    expect(screen.getByRole('alertdialog').textContent).not.toContain('Gỡ phân công chỉ thu hồi quyền')
    expect(mocks.sync).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận' }))
    await waitFor(() =>
      expect(mocks.sync).toHaveBeenCalledWith(offering.courseOfferingId, [
        lecturer.lecturerId,
        candidate.id,
      ]),
    )
    await screen.findByRole('button', { name: 'Gỡ phân công Trần Bình' })
  })

  it('can cancel removal and only updates the list after the server confirms', async () => {
    mocks.sync.mockResolvedValue({
      courseOfferingId: offering.courseOfferingId,
      lecturers: [{ ...lecturer, isActive: false }],
    })
    render(<DepartmentLecturerAssignmentPage />)
    const remove = await screen.findByRole('button', { name: 'Gỡ phân công Nguyễn An' })
    fireEvent.click(remove)
    expect(screen.getByRole('alertdialog').textContent).toContain('Gỡ phân công chỉ thu hồi quyền')
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }))
    expect(mocks.sync).not.toHaveBeenCalled()
    fireEvent.click(remove)
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận' }))
    await waitFor(() => expect(mocks.sync).toHaveBeenCalledWith(offering.courseOfferingId, []))
    await screen.findByText('Chưa phân công giảng viên cho môn trong học kỳ này.')
  })

  it('keeps the existing assignment and displays a forbidden response when saving fails', async () => {
    mocks.sync.mockRejectedValue({ detail: 'Không có quyền trên môn này.' })
    render(<DepartmentLecturerAssignmentPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Gỡ phân công Nguyễn An' }))
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận' }))
    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toContain('Không có quyền trên môn này.'),
    )
    expect(screen.getByText('Nguyễn An')).toBeTruthy()
  })

  it('does not offer an already assigned lecturer again', async () => {
    mocks.search.mockResolvedValue([
      { id: lecturer.lecturerId, fullName: lecturer.fullName, email: lecturer.email },
    ])
    render(<DepartmentLecturerAssignmentPage />)
    await screen.findByRole('button', { name: 'Gỡ phân công Nguyễn An' })
    fireEvent.change(screen.getByLabelText('Tìm giảng viên theo tên hoặc email'), {
      target: { value: 'an' },
    })
    await screen.findByText(
      'Không có giảng viên phù hợp chưa được phân công. Thử tên hoặc email khác.',
    )
    expect(screen.queryByRole('option', { name: 'Nguyễn An — an@example.test' })).toBeNull()
    expect(
      (screen.getByRole('button', { name: 'Thêm giảng viên' }) as HTMLButtonElement).disabled,
    ).toBe(true)
  })
})
