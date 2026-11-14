import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { OfficialExamManagementPage } from './OfficialExamManagementPage'
import { AppealManagementPage } from './AppealManagementPage'
import { DeadLetterQueuePage } from './DeadLetterQueuePage'
import { ProctorAttendancePage } from '../proctor/ProctorAttendancePage'
import { MemoryRouter } from 'react-router-dom'
import { Sidebar } from '@/components/layout/Sidebar'
import { AuditEvidencePage } from './AuditEvidencePage'

const mocks = vi.hoisted(() => ({
  role: 'ExaminationOfficer',
  userId: 'actor',
  sessions: vi.fn(), shifts: vi.fn(), structures: vi.fn(), createSession: vi.fn(),
  appeals: vi.fn(), regrade: vi.fn(), listDlq: vi.fn(), replay: vi.fn(),
  attendance: vi.fn(), updateAttendance: vi.fn(), violation: vi.fn(), suspend: vi.fn(),
  audit: vi.fn(), publish: vi.fn(),
}))
vi.mock('@/hooks/useAuth', () => ({ useAuth: () => ({
  user: { id: mocks.userId, role: mocks.role }, hasRole: (roles: string[]) => roles.includes(mocks.role),
}) }))
vi.mock('@/services/officialExam.service', () => ({ officialExamService: {
  getSessions: mocks.sessions, getShifts: mocks.shifts, getExamStructures: mocks.structures,
  createSession: mocks.createSession,
  getAuditEvidence: mocks.audit, publishGrades: mocks.publish,
} }))
vi.mock('@/services/appeals.service', () => ({ appealsService: { list: mocks.appeals, regrade: mocks.regrade } }))
vi.mock('@/services/deadLetterQueue.service', () => ({ deadLetterQueueService: { list: mocks.listDlq, replay: mocks.replay } }))
vi.mock('@/services/proctor.service', () => ({ proctorService: {
  getShiftAttendance: mocks.attendance, updateTicketAttendance: mocks.updateAttendance,
  recordViolation: mocks.violation, suspendTicket: mocks.suspend,
} }))

const appeal = {
  id: 'appeal-1', studentCode: 'SE1', studentName: 'Student', sessionTitle: 'Exam',
  reason: 'Review this answer', status: 'ASSIGNED', assignedTo: 'actor', originalScore: 5,
}
const monitor = {
  shiftId: 'shift-1', shiftName: 'Morning', roomLab: 'Lab', shiftStatus: 'SCHEDULED',
  assignedTicketsCount: 1, checkedInCount: 0, inProgressCount: 0, submittedCount: 0, suspendedCount: 0,
  seats: [{ seatNumber: 1, ticketId: 'ticket', fullName: 'Student', isOccupied: true, isCheckedIn: false }],
}

beforeEach(() => {
  mocks.role = 'ExaminationOfficer'
  mocks.userId = 'actor'
  mocks.sessions.mockResolvedValue([])
  mocks.shifts.mockResolvedValue([])
  mocks.structures.mockResolvedValue([])
  mocks.createSession.mockResolvedValue({})
  mocks.appeals.mockResolvedValue([appeal])
  mocks.listDlq.mockResolvedValue([])
  mocks.attendance.mockResolvedValue(monitor)
  mocks.audit.mockResolvedValue([{
    ticketId: 'ticket', studentCode: 'SE1', fullName: 'Student', seatNumber: 1,
    status: 'AUDITED', aiScore: 8, finalScore: 8, confidenceScore: 0.9,
    isSuspicious: false, isLocked: false,
  }])
  window.history.replaceState({}, '', '/')
})

describe('Task 09 official exam action boundaries', () => {
  it.each(['Admin', 'Instructor', 'Proctor', 'Student'])('%s cannot open operational forms or load their data', async (role) => {
    mocks.role = role
    render(<OfficialExamManagementPage />)
    expect(screen.queryByRole('button', { name: 'Tạo kỳ thi' })).toBeNull()
    await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy())
    expect(mocks.sessions).not.toHaveBeenCalled()
  })

  it('EO creates sessions using academic defaults, without follow-up or structure mutation controls', async () => {
    mocks.structures.mockResolvedValue([{ id: 'structure-1', name: 'Structure', isActive: true }])
    const { container } = render(<OfficialExamManagementPage />)
    await screen.findByRole('option', { name: /Structure/ })
    expect(mocks.structures).toHaveBeenCalledWith({ isActive: true })
    expect(mocks.structures).not.toHaveBeenCalledWith({ isActive: false })
    expect(screen.queryByRole('checkbox')).toBeNull()
    expect(screen.queryByPlaceholderText('Follow-up')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Tạo cấu trúc' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Tắt cấu trúc' })).toBeNull()
    fireEvent.change(screen.getByPlaceholderText('Course ID'), { target: { value: 'course-1' } })
    fireEvent.change(screen.getByRole('option', { name: /Structure/ }).closest('select')!, { target: { value: 'structure-1' } })
    fireEvent.change(screen.getByPlaceholderText('Tên kỳ thi'), { target: { value: 'Exam' } })
    fireEvent.change(container.querySelector('input[type=date]')!, { target: { value: '2026-11-20' } })
    fireEvent.submit(screen.getByRole('button', { name: 'Tạo kỳ thi' }).closest('form')!)
    await waitFor(() => expect(mocks.createSession).toHaveBeenCalledWith(expect.objectContaining({ hasFollowUp: false, maxFollowUpQuestions: 2 })))
  })

  it('DH sees academic structure controls but no operational forms', async () => {
    mocks.role = 'DepartmentHead'
    render(<OfficialExamManagementPage />)
    await screen.findByRole('button', { name: 'Tạo cấu trúc' })
    expect(screen.queryByRole('button', { name: 'Tạo kỳ thi' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Mở ca thi' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Cấp vé thi' })).toBeNull()
  })
  it('DH loads both active and inactive structures without duplicate rows', async () => {
    mocks.role = 'DepartmentHead'
    const active = { id: 'active', name: 'Active structure', isActive: true }
    const inactive = { id: 'inactive', name: 'Inactive structure', isActive: false }
    mocks.structures.mockImplementation(({ isActive }) => Promise.resolve(isActive ? [active] : [active, inactive]))
    render(<OfficialExamManagementPage />)
    expect(await screen.findByRole('button', { name: 'Tắt cấu trúc' })).toBeTruthy()
    expect(screen.getAllByRole('button', { name: 'Tắt cấu trúc' })).toHaveLength(1)
    expect(screen.getByRole('button', { name: 'Bật cấu trúc' })).toBeTruthy()
    expect(mocks.structures).toHaveBeenCalledWith({ isActive: true })
    expect(mocks.structures).toHaveBeenCalledWith({ isActive: false })
  })
})

describe('Task 09 appeal action boundaries', () => {
  it('EO can assign and decide', async () => {
    render(<AppealManagementPage />)
    expect(await screen.findByRole('button', { name: 'Phê duyệt quyết định cuối' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Phân công GV chấm lại' })).toBeTruthy()
  })
  it('Admin does not inherit assignment or final-decision authority', async () => {
    mocks.role = 'Admin'
    render(<AppealManagementPage />)
    expect(screen.getByRole('alert')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Phê duyệt quyết định cuối' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Phân công GV chấm lại' })).toBeNull()
    expect(mocks.appeals).not.toHaveBeenCalled()
  })
  it.each([['actor', true], ['other-lecturer', false]])('lecturer regrade is gated by assignment %s', async (assignedTo, allowed) => {
    mocks.role = 'Instructor'
    mocks.appeals.mockResolvedValue([{ ...appeal, assignedTo }])
    render(<AppealManagementPage />)
    await screen.findByText('Lý do khiếu nại:')
    expect(Boolean(screen.queryByRole('button', { name: 'Gửi biên bản & điểm chấm lại' }))).toBe(allowed)
  })
})

describe('Task 09 proctor actions', () => {
  it.each(['Admin', 'Instructor', 'DepartmentHead', 'Student'])('%s cannot load attendance', async (role) => {
    mocks.role = role
    window.history.replaceState({}, '', '/?shiftId=shift-1')
    render(<ProctorAttendancePage />)
    expect(screen.getByRole('alert')).toBeTruthy()
    expect(mocks.attendance).not.toHaveBeenCalled()
  })
  it.each([['Proctor', true, false], ['ExaminationOfficer', false, true]])('%s has only its authorized controls', async (role, attendance, suspension) => {
    mocks.role = role
    window.history.replaceState({}, '', '/?shiftId=shift-1')
    render(<ProctorAttendancePage />)
    fireEvent.click(await screen.findByRole('button', { name: /#01/ }))
    expect(Boolean(screen.queryByRole('button', { name: 'Điểm danh có mặt' }))).toBe(attendance)
    expect(Boolean(screen.queryByRole('button', { name: 'Báo cáo vi phạm' }))).toBe(attendance)
    expect(Boolean(screen.queryByRole('button', { name: 'Đình chỉ vé' }))).toBe(suspension)
  })
})

describe('Task 09 DLQ flows', () => {
  it.each(['Admin', 'ExaminationOfficer', 'Instructor'])('%s can replay an allowed task', async (role) => {
    mocks.role = role
    const flowType = role === 'ExaminationOfficer' ? 'OFFICIAL_EXAM' : 'IN_CLASS_EXAM'
    mocks.listDlq.mockResolvedValue([{ id: 'task', flowType, status: 'pending', retryCount: 1, taskType: 'GRADING', errorMessage: 'Retry needed', createdAt: '2026-11-14' }])
    mocks.replay.mockResolvedValue({ message: 'Replayed' })
    render(<DeadLetterQueuePage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Replay task' }))
    await waitFor(() => expect(mocks.replay).toHaveBeenCalledWith('task'))
  })
  it.each([
    ['Admin', ['ALL', 'PRACTICE', 'MOCK_EXAM', 'IN_CLASS_EXAM'], 'ALL'],
    ['ExaminationOfficer', ['OFFICIAL_EXAM'], 'OFFICIAL_EXAM'],
    ['Instructor', ['IN_CLASS_EXAM'], 'IN_CLASS_EXAM'],
  ])('%s requests and offers only allowed flows', async (role, expected, initialFlow) => {
    mocks.role = role
    render(<DeadLetterQueuePage />)
    await waitFor(() => expect(mocks.listDlq).toHaveBeenCalledWith('pending', initialFlow))
    const options = Array.from(screen.getByLabelText('Luồng nghiệp vụ').querySelectorAll('option')).map((option) => option.value)
    expect(options).toEqual(expected)
  })
  it.each(['DepartmentHead', 'Proctor', 'Student'])('%s does not load DLQ', async (role) => {
    mocks.role = role
    render(<DeadLetterQueuePage />)
    expect(screen.getByRole('alert')).toBeTruthy()
    await waitFor(() => expect(mocks.listDlq).not.toHaveBeenCalled())
  })
})

describe('Task 09 publish controls', () => {
  it.each(['ExaminationOfficer', 'Instructor', 'DepartmentHead', 'Admin', 'Proctor', 'Student'])('%s only publishes when EO', async (role) => {
    mocks.role = role
    render(<MemoryRouter initialEntries={['/lecturer/audit?shiftId=11111111-1111-1111-1111-111111111111']}><AuditEvidencePage /></MemoryRouter>)
    await screen.findByText('Phân nhóm hậu kiểm')
    expect(Boolean(screen.queryByRole('button', { name: 'Công bố điểm toàn ca' }))).toBe(role === 'ExaminationOfficer')
    fireEvent.click(screen.getByRole('button', { name: /^Tất cả/ }))
    const canAudit = ['Instructor', 'DepartmentHead', 'Admin'].includes(role)
    expect(Boolean(screen.queryByRole('button', { name: 'Lưu điểm hậu kiểm' }))).toBe(canAudit)
    expect(Boolean(screen.queryByRole('button', { name: 'Khóa điểm vé thi' }))).toBe(canAudit)
    expect(mocks.publish).not.toHaveBeenCalled()
  })
})

describe('Task 09 sidebar portal boundaries', () => {
  it.each(['ExaminationOfficer', 'Instructor', 'DepartmentHead', 'Admin', 'Proctor', 'Student'])('%s sees only permitted task 09 portals', (role) => {
    mocks.role = role
    render(<MemoryRouter><Sidebar collapsed={false} mobileOpen={false} onCollapse={vi.fn()} onMobileClose={vi.fn()} /></MemoryRouter>)
    expect(Boolean(screen.queryByRole('link', { name: 'Kỳ thi chính thức' }))).toBe(['ExaminationOfficer', 'DepartmentHead'].includes(role))
    expect(Boolean(screen.queryByRole('link', { name: 'Phúc khảo' }))).toBe(['ExaminationOfficer', 'DepartmentHead', 'Instructor'].includes(role))
    expect(Boolean(screen.queryByRole('link', { name: 'Điểm danh' }))).toBe(['Proctor', 'ExaminationOfficer'].includes(role))
    expect(Boolean(screen.queryByRole('link', { name: 'Lỗi chấm AI' }))).toBe(['Admin', 'Instructor', 'ExaminationOfficer'].includes(role))
  })
})
