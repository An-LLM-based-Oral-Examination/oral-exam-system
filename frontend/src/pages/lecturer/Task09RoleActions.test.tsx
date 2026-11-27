import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { OfficialExamManagementPage } from './OfficialExamManagementPage'
import { AppealManagementPage } from './AppealManagementPage'
import { DeadLetterQueuePage } from './DeadLetterQueuePage'
import { ProctorAttendancePage } from '../proctor/ProctorAttendancePage'
import { MemoryRouter } from 'react-router-dom'
import { Sidebar } from '@/components/layout/Sidebar'
import { ExaminationOfficerLayout } from '@/layouts/ExaminationOfficerLayout'
import { AuditEvidencePage } from './AuditEvidencePage'

const mocks = vi.hoisted(() => ({
  role: 'ExaminationOfficer',
  userId: 'actor',
  sessions: vi.fn(), shifts: vi.fn(), structures: vi.fn(), createSession: vi.fn(),
  configureFollowUp: vi.fn(), getAntiCheatLogs: vi.fn(),
  createStructure: vi.fn(), updateStructure: vi.fn(), lecturerCourses: vi.fn(),
  appeals: vi.fn(), regrade: vi.fn(), listDlq: vi.fn(), replay: vi.fn(), resolve: vi.fn(),
  attendance: vi.fn(), updateAttendance: vi.fn(), violation: vi.fn(), suspend: vi.fn(),
  audit: vi.fn(), publish: vi.fn(),
}))
vi.mock('@/hooks/useAuth', () => ({ useAuth: () => ({
  user: { id: mocks.userId, role: mocks.role }, hasRole: (roles: string[]) => roles.includes(mocks.role),
}) }))
vi.mock('@/hooks/useAntiCheatHub', () => ({
  useAntiCheatHub: () => ({
    status: 'idle',
    isConnected: false,
    alerts: [],
    latestAlert: null,
    reconnectVersion: 0,
    lastError: null,
    clearAlerts: vi.fn(),
  }),
}))
vi.mock('@/services/officialExam.service', () => ({ officialExamService: {
  getSessions: mocks.sessions, getShifts: mocks.shifts, getExamStructures: mocks.structures,
  createSession: mocks.createSession, configureFollowUp: mocks.configureFollowUp,
  getAntiCheatLogs: mocks.getAntiCheatLogs,
  createExamStructure: mocks.createStructure, updateExamStructure: mocks.updateStructure,
  getAuditEvidence: mocks.audit, publishGrades: mocks.publish,
} }))
vi.mock('@/services/course.service', () => ({ courseService: {
  getLecturerCourses: mocks.lecturerCourses,
} }))
vi.mock('@/services/appeals.service', () => ({ appealsService: { list: mocks.appeals, regrade: mocks.regrade } }))
vi.mock('@/services/deadLetterQueue.service', () => ({
  deadLetterQueueService: {
    list: mocks.listDlq,
    replay: mocks.replay,
    resolve: mocks.resolve,
    listForAdmin: mocks.listDlq,
    replayForAdmin: mocks.replay,
    resolveForAdmin: mocks.resolve,
    listForExaminationOfficer: mocks.listDlq,
    replayForExaminationOfficer: mocks.replay,
    resolveForExaminationOfficer: mocks.resolve,
  },
}))
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
  mocks.configureFollowUp.mockResolvedValue({
    id: 'session-1',
    courseId: 'course-1',
    title: 'PRN231 FE',
    examDate: '2026-11-20',
    hasFollowUp: true,
    maxFollowUpQuestions: 3,
    examInputMode: 'VoiceWithTranscriptEdit',
    transcriptBufferSeconds: 60,
    status: 'scheduled',
  })
  mocks.getAntiCheatLogs.mockResolvedValue([])
  mocks.createStructure.mockResolvedValue({
    id: 'structure-new',
    courseId: 'course-1',
    courseCode: 'PRN231',
    createdBy: 'actor',
    name: 'New Structure',
    totalQuestions: 5,
    durationMinutes: 30,
    bloomDistribution: '{"Remember":2}',
    isActive: true,
    createdAt: '2026-11-18T00:00:00Z',
  })
  mocks.updateStructure.mockResolvedValue({})
  mocks.lecturerCourses.mockResolvedValue([{ id: 'course-1', code: 'PRN231', name: 'Backend' }])
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
  it.each(['Admin', 'Instructor', 'Proctor', 'Student', 'ExaminationOfficer'])('%s cannot open DepartmentHead academic/follow-up page or load its data', async (role) => {
    mocks.role = role
    render(<OfficialExamManagementPage />)
    expect(screen.queryByRole('button', { name: 'Tạo kỳ thi' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Tạo cấu trúc' })).toBeNull()
    await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy())
    expect(mocks.sessions).not.toHaveBeenCalled()
    expect(mocks.shifts).not.toHaveBeenCalled()
  })

  it('DH manages academic structures, Bloom distribution, and exclusive official exam Follow-up config without EO shift forms', async () => {
    mocks.role = 'DepartmentHead'
    mocks.sessions.mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-1',
        title: 'PRN231 FE',
        examDate: '2026-11-20',
        examAttemptType: 'FE',
        hasFollowUp: false,
        maxFollowUpQuestions: 2,
        examInputMode: 'VoiceWithTranscriptEdit',
        transcriptBufferSeconds: 60,
        status: 'scheduled',
      },
    ])
    render(<OfficialExamManagementPage />)
    await screen.findByRole('option', { name: /PRN231 · Backend/ })
    expect(mocks.shifts).not.toHaveBeenCalled()
    expect(mocks.lecturerCourses).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('button', { name: 'Tạo kỳ thi' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Mở ca thi' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Cấp vé thi' })).toBeNull()

    fireEvent.click(screen.getByLabelText('Bật Follow-up cho PRN231 FE'))
    fireEvent.change(screen.getByLabelText('Số câu Follow-up cho PRN231 FE'), {
      target: { value: '3' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu Follow-up' }))
    await waitFor(() =>
      expect(mocks.configureFollowUp).toHaveBeenCalledWith('session-1', {
        hasFollowUp: true,
        maxFollowUpQuestions: 3,
      }),
    )

    const courseSelect = screen.getByLabelText('Môn học') as HTMLSelectElement
    expect(screen.getByRole('option', { name: /PRN231 · Backend/ })).toBeTruthy()
    expect(courseSelect.value).toBe('course-1')

    fireEvent.change(screen.getByPlaceholderText('Tên cấu trúc'), { target: { value: 'Midterm Oral' } })
    fireEvent.change(screen.getByPlaceholderText('Số câu'), { target: { value: '5' } })
    fireEvent.change(screen.getByPlaceholderText('Phút'), { target: { value: '30' } })
    fireEvent.change(screen.getByLabelText('Bloom Remember'), { target: { value: '3' } })
    fireEvent.change(screen.getByLabelText('Bloom Understand'), { target: { value: '3' } })
    fireEvent.submit(screen.getByRole('button', { name: 'Tạo cấu trúc' }).closest('form')!)

    expect(await screen.findByRole('alert')).toHaveTextContent('Tổng số câu phân bổ theo bậc Bloom không được vượt quá tổng số câu hỏi.')
    expect(mocks.createStructure).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText('Bloom Understand'), { target: { value: '2' } })
    fireEvent.submit(screen.getByRole('button', { name: 'Tạo cấu trúc' }).closest('form')!)

    await waitFor(() =>
      expect(mocks.createStructure).toHaveBeenCalledWith({
        courseId: 'course-1',
        name: 'Midterm Oral',
        totalQuestions: 5,
        durationMinutes: 30,
        bloomDistribution: JSON.stringify({ Remember: 3, Understand: 2 }),
        isActive: true,
      }),
    )
  })
  it('DH loads both active and inactive structures without duplicate rows and renders Bloom chips', async () => {
    mocks.role = 'DepartmentHead'
    const active = { id: 'active', name: 'Active structure', courseCode: 'PRN231', totalQuestions: 5, durationMinutes: 30, bloomDistribution: '{"Remember":2,"Apply":1}', isActive: true }
    const inactive = { id: 'inactive', name: 'Inactive structure', courseCode: 'PRN231', totalQuestions: 4, durationMinutes: 20, bloomDistribution: '{}', isActive: false }
    mocks.structures.mockImplementation(({ isActive }) => Promise.resolve(isActive ? [active] : [active, inactive]))
    render(<OfficialExamManagementPage />)
    expect(await screen.findByRole('button', { name: 'Tắt cấu trúc' })).toBeTruthy()
    expect(screen.getAllByRole('button', { name: 'Tắt cấu trúc' })).toHaveLength(1)
    expect(screen.getByRole('button', { name: 'Bật cấu trúc' })).toBeTruthy()
    expect(screen.getByText('Remember: 2')).toBeTruthy()
    expect(screen.getByText('Apply: 1')).toBeTruthy()
    expect(screen.getByText('Phân bổ linh hoạt')).toBeTruthy()
    expect(mocks.structures).toHaveBeenCalledWith({ isActive: true })
    expect(mocks.structures).toHaveBeenCalledWith({ isActive: false })
  })

  it('DH strips leading VALIDATION prefix when structure creation fails', async () => {
    mocks.role = 'DepartmentHead'
    mocks.createStructure.mockRejectedValueOnce({
      status: 422,
      detail: 'VALIDATION: Tên cấu trúc đề thi đã tồn tại trong môn học.',
    })
    render(<OfficialExamManagementPage />)
    await screen.findByRole('option', { name: /PRN231 · Backend/ })

    fireEvent.change(screen.getByPlaceholderText('Tên cấu trúc'), { target: { value: 'Midterm Oral' } })
    fireEvent.submit(screen.getByRole('button', { name: 'Tạo cấu trúc' }).closest('form')!)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Tên cấu trúc đề thi đã tồn tại trong môn học.')
    expect(alert).not.toHaveTextContent('VALIDATION:')
  })
})

describe('Task 09 appeal action boundaries', () => {
  it('EO can assign a submitted appeal to a lecturer', async () => {
    mocks.appeals.mockResolvedValue([{ ...appeal, status: 'SUBMITTED_TO_EXAM_OFFICE' }])
    render(<AppealManagementPage />)
    expect(await screen.findByRole('button', { name: 'Phân công GV chấm lại' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Phân công GV chấm lại' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Phê duyệt quyết định cuối' })).toBeDisabled()
  })
  it('EO can make the final decision after lecturer regrade', async () => {
    mocks.appeals.mockResolvedValue([{ ...appeal, status: 'REGRADED_BY_LECTURER' }])
    render(<AppealManagementPage />)
    expect(await screen.findByRole('button', { name: 'Phê duyệt quyết định cuối' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Phê duyệt quyết định cuối' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Phân công GV chấm lại' })).toBeDisabled()
  })
  it('Admin does not inherit assignment or final-decision authority', async () => {
    mocks.role = 'Admin'
    render(<AppealManagementPage />)
    expect(screen.getByRole('alert')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Phê duyệt quyết định cuối' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Phân công GV chấm lại' })).toBeNull()
    expect(mocks.appeals).not.toHaveBeenCalled()
  })
  it('Instructor can access assigned appeals to regrade without seeing EO allocation or decision controls', async () => {
    mocks.role = 'Instructor'
    mocks.appeals.mockResolvedValue([{ ...appeal, status: 'ASSIGNED_TO_LECTURER', assignedTo: 'actor' }])
    render(<AppealManagementPage />)
    expect(await screen.findByRole('button', { name: 'Gửi biên bản & điểm chấm lại' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Phân công GV chấm lại' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Phê duyệt quyết định cuối' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Đề xuất phân bổ tự động' })).toBeNull()
  })
  it('DepartmentHead cannot access appeal management controls', async () => {
    mocks.role = 'DepartmentHead'
    mocks.appeals.mockResolvedValue([{ ...appeal, status: 'ASSIGNED_TO_LECTURER', assignedTo: 'actor' }])
    render(<AppealManagementPage />)
    expect(screen.getByRole('alert')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Gửi biên bản & điểm chấm lại' })).toBeNull()
    expect(mocks.appeals).not.toHaveBeenCalled()
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
  it.each([
    ['Admin', 'PRACTICE'],
    ['Admin', 'IN_CLASS_EXAM'],
    ['ExaminationOfficer', 'OFFICIAL_EXAM'],
  ])('%s can replay an allowed %s task', async (role, flowType) => {
    mocks.role = role
    mocks.listDlq.mockResolvedValue([{ id: 'task', flowType, status: 'pending', retryCount: 1, taskType: 'GRADING', errorMessage: 'Retry needed', createdAt: '2026-11-14' }])
    mocks.replay.mockResolvedValue({ message: 'Replayed' })
    render(<MemoryRouter><DeadLetterQueuePage /></MemoryRouter>)
    fireEvent.click(await screen.findByRole('button', { name: 'Replay task' }))
    await waitFor(() => expect(mocks.replay).toHaveBeenCalledWith('task'))
  })
  it.each([
    ['Admin', ['Tất cả luồng được phép', 'Luyện tập (MF-01)', 'Thi thử (MF-02)', 'Ca thi trong lớp (MF-05)'], 'ALL'],
    ['ExaminationOfficer', ['Thi thật Kiosk (MF-04)'], 'OFFICIAL_EXAM'],
  ])('%s requests and offers only allowed flows', async (role, expectedLabels, initialFlow) => {
    mocks.role = role
    render(<MemoryRouter><DeadLetterQueuePage /></MemoryRouter>)
    if (role === 'Admin') {
      await waitFor(() => expect(mocks.listDlq).toHaveBeenCalledWith('pending', initialFlow))
    } else {
      await waitFor(() => expect(mocks.listDlq).toHaveBeenCalledWith('pending'))
    }
    fireEvent.click(screen.getByRole('button', { name: 'Luồng nghiệp vụ' }))
    const optionElements = await screen.findAllByRole('option')
    const optionTexts = optionElements.map((option) => option.textContent?.trim())
    expect(optionTexts).toEqual(expectedLabels)
    if (role === 'Admin') {
      expect(screen.queryByText('Thi thật Kiosk (MF-04)')).toBeNull()
      fireEvent.click(screen.getByRole('option', { name: 'Ca thi trong lớp (MF-05)' }))
      await waitFor(() => expect(mocks.listDlq).toHaveBeenLastCalledWith('pending', 'IN_CLASS_EXAM'))
    } else {
      expect(screen.queryByText('Ca thi trong lớp (MF-05)')).toBeNull()
    }
  })
  it.each(['Instructor', 'DepartmentHead', 'Proctor', 'Student'])('%s does not load DLQ', async (role) => {
    mocks.role = role
    render(<MemoryRouter><DeadLetterQueuePage /></MemoryRouter>)
    expect(screen.getByRole('alert')).toBeTruthy()
    await waitFor(() => expect(mocks.listDlq).not.toHaveBeenCalled())
  })

  it('Admin selecting "Tất cả trạng thái" calls listForAdmin(undefined, "ALL") and mounts /admin/dlq?status=all cleanly', async () => {
    mocks.role = 'Admin'
    const { unmount } = render(
      <MemoryRouter initialEntries={['/admin/dlq']}>
        <DeadLetterQueuePage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(mocks.listDlq).toHaveBeenCalledWith('pending', 'ALL'))
    fireEvent.click(screen.getByRole('button', { name: 'Trạng thái' }))
    fireEvent.click(await screen.findByRole('option', { name: 'Tất cả trạng thái' }))

    await waitFor(() => expect(mocks.listDlq).toHaveBeenLastCalledWith(undefined, 'ALL'))
    expect(screen.getByRole('button', { name: 'Trạng thái' })).toHaveTextContent('Tất cả trạng thái')
    unmount()

    mocks.listDlq.mockClear()
    render(
      <MemoryRouter initialEntries={['/admin/dlq?status=all']}>
        <DeadLetterQueuePage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(mocks.listDlq).toHaveBeenCalledWith(undefined, 'ALL'))
    expect(screen.getByRole('button', { name: 'Trạng thái' })).toHaveTextContent('Tất cả trạng thái')
  })

  it('Admin displays PRACTICE and IN_CLASS_EXAM rows while filtering out OFFICIAL_EXAM and OfficialExamGrading rows, and renders resolve errors inside FormDialog', async () => {
    mocks.role = 'Admin'
    mocks.listDlq.mockResolvedValue([
      {
        id: 'task-practice',
        flowType: 'PRACTICE',
        status: 'pending',
        retryCount: 1,
        taskType: 'GRADING',
        errorMessage: 'Practice task error',
        createdAt: '2026-11-18T08:00:00Z',
      },
      {
        id: 'task-inclass',
        flowType: 'IN_CLASS_EXAM',
        status: 'pending',
        retryCount: 2,
        taskType: 'GRADING',
        errorMessage: 'In-class task should be visible',
        createdAt: '2026-11-18T08:01:00Z',
      },
      {
        id: 'task-official',
        flowType: 'OFFICIAL_EXAM',
        status: 'pending',
        retryCount: 3,
        taskType: 'GRADING',
        errorMessage: 'Official task should stay hidden',
        createdAt: '2026-11-18T08:02:00Z',
      },
      {
        id: 'task-official-null-flow',
        flowType: null,
        status: 'pending',
        retryCount: 4,
        taskType: 'OfficialExamGrading',
        errorMessage: 'Legacy official task should stay hidden',
        createdAt: '2026-11-18T08:03:00Z',
      },
    ])
    mocks.resolve.mockRejectedValueOnce({
      status: 409,
      detail: 'Tác vụ DLQ đã được xử lý trước đó.',
    })

    render(
      <MemoryRouter initialEntries={['/admin/dlq']}>
        <DeadLetterQueuePage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Practice task error')).toBeTruthy()
    expect(screen.getByText('In-class task should be visible')).toBeTruthy()
    expect(screen.queryByText('Official task should stay hidden')).toBeNull()
    expect(screen.queryByText('Legacy official task should stay hidden')).toBeNull()

    fireEvent.click(screen.getAllByRole('button', { name: 'Đánh dấu đã xử lý' })[0])
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận đã xử lý' }))

    await waitFor(() => {
      expect(dialog.querySelector('[role="alert"]')).toHaveTextContent(
        'Tác vụ DLQ đã được xử lý trước đó.',
      )
    })
  })
})

describe('Task 09 publish controls', () => {
  it.each(['ExaminationOfficer', 'Instructor', 'DepartmentHead', 'Admin', 'Proctor', 'Student'])('%s only publishes when EO', async (role) => {
    mocks.role = role
    render(<MemoryRouter initialEntries={['/lecturer/audit?shiftId=11111111-1111-1111-1111-111111111111']}><AuditEvidencePage /></MemoryRouter>)
    await screen.findByText('Phân nhóm hậu kiểm')
    expect(Boolean(screen.queryByRole('button', { name: 'Công bố điểm toàn ca' }))).toBe(role === 'ExaminationOfficer')
    fireEvent.click(screen.getByRole('button', { name: /^Tất cả/ }))
    const canAudit = ['Instructor', 'DepartmentHead'].includes(role)
    expect(Boolean(screen.queryByRole('button', { name: 'Lưu điểm hậu kiểm' }))).toBe(canAudit)
    expect(Boolean(screen.queryByRole('button', { name: 'Khóa điểm vé thi' }))).toBe(canAudit)
    expect(mocks.publish).not.toHaveBeenCalled()
  })
})

describe('Task 09 sidebar portal boundaries', () => {
  it.each([
    ['Instructor', 'Quản lý lớp học', 'Câu hỏi luyện tập', ['Lỗi chấm ca lớp', 'Phúc khảo được giao', 'Hậu kiểm điểm']],
    ['Instructor', 'Hậu kiểm thi thật', 'Chấm phúc khảo', ['Lỗi chấm ca lớp', 'Quản lý phúc khảo']],
    ['DepartmentHead', 'Quản lý câu hỏi', 'Chuẩn bị đề thi', ['Quản lý phúc khảo']],
    ['Admin', 'Lỗi chấm MF-01/02/05', null, ['Quản lý phúc khảo']],
    ['Proctor', 'Điểm danh', null, ['Lỗi chấm ca lớp']],
  ] as Array<[string, string, string | null, string[]]>)('%s sees only its permitted portals in the shared sidebar', (role, primaryLink, secondaryLink, hiddenLinks) => {
    mocks.role = role
    render(<MemoryRouter><Sidebar collapsed={false} mobileOpen={false} onCollapse={vi.fn()} onMobileClose={vi.fn()} /></MemoryRouter>)
    expect(screen.getByRole('link', { name: primaryLink })).toBeTruthy()
    if (secondaryLink) expect(screen.getByRole('link', { name: secondaryLink })).toBeTruthy()
    for (const hidden of hiddenLinks) {
      expect(screen.queryByRole('link', { name: hidden })).toBeNull()
    }
    expect(screen.queryByRole('link', { name: 'Kỳ thi chính thức' })).toBeNull()
  })

  it('EO uses its dedicated layout and sees only official-exam task 09 portals', () => {
    mocks.role = 'ExaminationOfficer'
    render(<MemoryRouter><ExaminationOfficerLayout /></MemoryRouter>)
    expect(screen.getByRole('link', { name: 'Kỳ thi chính thức' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Phúc khảo' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Lỗi chấm AI' })).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Phúc khảo được giao' })).toBeNull()
  })
})
