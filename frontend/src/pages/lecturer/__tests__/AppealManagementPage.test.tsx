import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppealManagementPage } from '../AppealManagementPage'
import type { Appeal, AppealAllocationProposal } from '@/types/appeal.types'
import type { AuditEvidenceItem } from '@/types/officialExam.types'

const mocks = vi.hoisted(() => ({
  role: 'ExaminationOfficer',
  userId: 'eo-1',
  list: vi.fn(),
  getById: vi.fn(),
  getEvidence: vi.fn(),
  getAllocationProposal: vi.fn(),
  assignLecturer: vi.fn(),
  batchAssign: vi.fn(),
  regrade: vi.fn(),
  review: vi.fn(),
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { id: mocks.userId, role: mocks.role, fullName: 'Test Actor', email: 'actor@test.local' },
    hasRole: (roles: string[]) => roles.includes(mocks.role),
  }),
}))

vi.mock('@/services/appeals.service', () => ({
  appealsService: {
    list: mocks.list,
    getById: mocks.getById,
    getEvidence: mocks.getEvidence,
    getAllocationProposal: mocks.getAllocationProposal,
    assignLecturer: mocks.assignLecturer,
    batchAssign: mocks.batchAssign,
    regrade: mocks.regrade,
    review: mocks.review,
  },
}))

const submittedAppeal: Appeal = {
  id: 'appeal-submitted-1',
  ticketId: 'ticket-1',
  studentId: 'student-1',
  studentCode: 'SE170001',
  studentName: 'Nguyen Van A',
  sessionId: 'session-1',
  sessionTitle: 'PRN231 · FE',
  submissionId: null,
  reason: 'Em trình bày đủ ý về Dependency Injection ở phút thứ 2.',
  status: 'SUBMITTED_TO_EXAM_OFFICE',
  assignedTo: undefined,
  assignedToName: '',
  reviewedBy: null,
  reviewedByName: null,
  decision: null,
  originalScore: 6.5,
  proposedScore: null,
  reviewNotes: null,
  createdAt: '2026-11-15T08:00:00.000Z',
  updatedAt: '2026-11-15T08:00:00.000Z',
  resolvedAt: null,
}

const assignedAppeal: Appeal = {
  ...submittedAppeal,
  id: 'appeal-assigned-2',
  studentCode: 'SE170002',
  studentName: 'Tran Thi B',
  status: 'ASSIGNED_TO_LECTURER',
  assignedTo: 'lecturer-2',
  assignedToName: 'Giảng viên B',
}

const regradedAppeal: Appeal = {
  ...submittedAppeal,
  id: 'appeal-regraded-3',
  studentCode: 'SE170003',
  studentName: 'Le Van C',
  status: 'REGRADED_BY_LECTURER',
  assignedTo: 'lecturer-2',
  assignedToName: 'Giảng viên B',
  proposedScore: 8.0,
  reviewNotes: 'Nghe lại audio thấy thí sinh trả lời đúng kiến trúc Clean Architecture.',
}

const sampleEvidence: AuditEvidenceItem = {
  ticketId: 'ticket-1',
  studentCode: 'SE170002',
  fullName: 'Tran Thi B',
  seatNumber: 3,
  status: 'PUBLISHED',
  audioR2Url: null,
  audioHashSha256: 'c'.repeat(64),
  transcriptWhisper: 'Thí sinh giải thích rõ ràng vòng đời Scoped và Transient.',
  aiScore: 6.5,
  confidenceScore: 0.88,
  isSuspicious: false,
  suspiciousReason: null,
  cotTrace: 'Điểm thành phần DI đạt 4/5.',
  finalScore: 6.5,
  isLocked: true,
}

const sampleProposal: AppealAllocationProposal = {
  totalAppeals: 1,
  totalLecturers: 3,
  exceedsStandardQuota: false,
  lecturerSummaries: [
    { lecturerId: 'lecturer-initial', lecturerName: 'Giám Khảo Đầu', email: 'gk@fpt.edu.vn', assignedCount: 2 },
    { lecturerId: 'lecturer-2', lecturerName: 'Giảng Viên Độc Lập', email: 'gv2@fpt.edu.vn', assignedCount: 5 },
    { lecturerId: 'lecturer-full', lecturerName: 'Giảng Viên Đủ Tải', email: 'full@fpt.edu.vn', assignedCount: 24 },
  ],
  items: [
    {
      appealId: submittedAppeal.id,
      ticketId: submittedAppeal.ticketId,
      studentCode: submittedAppeal.studentCode,
      studentName: submittedAppeal.studentName,
      sessionId: submittedAppeal.sessionId,
      sessionTitle: submittedAppeal.sessionTitle,
      initialExaminerUserId: 'lecturer-initial',
      initialExaminerName: 'Giám Khảo Đầu',
      proposedLecturerId: 'lecturer-2',
      proposedLecturerName: 'Giảng Viên Độc Lập',
      originalScore: 6.5,
      reason: submittedAppeal.reason,
    },
  ],
}

describe('AppealManagementPage (Unified 5-step MF-04 Appeals)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.role = 'ExaminationOfficer'
    mocks.userId = 'eo-1'
    mocks.list.mockResolvedValue([submittedAppeal])
    mocks.getById.mockResolvedValue(regradedAppeal)
    mocks.getEvidence.mockResolvedValue(sampleEvidence)
    mocks.getAllocationProposal.mockResolvedValue(sampleProposal)
    mocks.assignLecturer.mockResolvedValue(assignedAppeal)
    mocks.batchAssign.mockResolvedValue({
      totalAssigned: 1,
      assignedCount: 1,
      failedCount: 0,
      assignedAppeals: [assignedAppeal],
    })
    mocks.regrade.mockResolvedValue(regradedAppeal)
    mocks.review.mockResolvedValue({ ...regradedAppeal, status: 'APPROVED', decision: 'APPROVED' })
  })

  it('unblocks Instructor at /lecturer/appeals, loads single-ticket evidence, and submits regrade', async () => {
    mocks.role = 'Instructor'
    mocks.userId = 'lecturer-2'
    mocks.list.mockResolvedValue([assignedAppeal])

    render(
      <MemoryRouter initialEntries={['/lecturer/appeals']}>
        <Routes>
          <Route path="/lecturer/appeals" element={<AppealManagementPage />} />
          <Route path="/lecturer/appeals/:appealId" element={<AppealManagementPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('SE170002')).toBeInTheDocument()
    expect(await screen.findByText(/Thí sinh giải thích rõ ràng/)).toBeInTheDocument()
    expect(mocks.getEvidence).toHaveBeenCalledWith('appeal-assigned-2')
    expect(screen.queryByRole('button', { name: 'Đề xuất phân bổ tự động' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Phê duyệt quyết định cuối' })).not.toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Điểm đề xuất/i), { target: { value: '8.5' } })
    fireEvent.change(screen.getByLabelText('Ghi chú xử lý'), {
      target: { value: 'Nghe lại bản ghi xác nhận câu trả lời đạt chuẩn CLO2.' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Gửi biên bản/ }))

    await waitFor(() =>
      expect(mocks.regrade).toHaveBeenCalledWith('appeal-assigned-2', {
        newScore: 8.5,
        reviewNotes: 'Nghe lại bản ghi xác nhận câu trả lời đạt chuẩn CLO2.',
      }),
    )
  })

  it('supports ExaminationOfficer 5-step filter, zero raw UUID input, propose-allocation <= 24/GV, and batch-assign confirmation', async () => {
    render(
      <MemoryRouter initialEntries={['/examination-officer/appeals']}>
        <Routes>
          <Route path="/examination-officer/appeals" element={<AppealManagementPage />} />
          <Route path="/examination-officer/appeals/:appealId" element={<AppealManagementPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('SE170001')).toBeInTheDocument()
    expect(screen.queryByPlaceholderText(/UUID|user ID/i)).toBeNull()

    const statusSelect = screen.getByLabelText('Lọc trạng thái đơn phúc khảo')
    const statusValues = Array.from(statusSelect.querySelectorAll('option')).map(
      (option) => option.value,
    )
    expect(statusValues).toEqual([
      '',
      'SUBMITTED_TO_EXAM_OFFICE',
      'ASSIGNED_TO_LECTURER',
      'REGRADED_BY_LECTURER',
      'APPROVED',
      'REJECTED',
    ])

    fireEvent.click(screen.getByRole('button', { name: /Đề xuất phân bổ tự động/ }))
    expect(await screen.findByText('5/24')).toBeInTheDocument()
    expect(screen.getByText('24/24')).toBeInTheDocument()

    const proposalDropdown = screen.getByLabelText(
      `Chọn giảng viên chấm lại cho ${submittedAppeal.studentCode}`,
    )
    const initialExaminerOption = Array.from(proposalDropdown.querySelectorAll('option')).find(
      (option) => option.value === 'lecturer-initial',
    )
    const fullQuotaOption = Array.from(proposalDropdown.querySelectorAll('option')).find(
      (option) => option.value === 'lecturer-full',
    )
    expect(initialExaminerOption).toBeDisabled()
    expect(fullQuotaOption).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: /Xác nhận phân công \(1 đơn\)/ }))
    const dialog = await screen.findByRole('alertdialog')
    expect(dialog).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận phân công hàng loạt' }))

    await waitFor(() =>
      expect(mocks.batchAssign).toHaveBeenCalledWith({
        assignments: [{ appealId: submittedAppeal.id, lecturerId: 'lecturer-2' }],
      }),
    )
  })

  it('deep-links /examination-officer/appeals/:appealId and confirms final decision via ConfirmDialog', async () => {
    mocks.list.mockResolvedValue([submittedAppeal])
    mocks.getById.mockResolvedValue(regradedAppeal)

    render(
      <MemoryRouter initialEntries={[`/examination-officer/appeals/${regradedAppeal.id}`]}>
        <Routes>
          <Route path="/examination-officer/appeals" element={<AppealManagementPage />} />
          <Route path="/examination-officer/appeals/:appealId" element={<AppealManagementPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('SE170003')).toBeInTheDocument()
    expect(mocks.getById).toHaveBeenCalledWith(regradedAppeal.id)

    fireEvent.change(screen.getByLabelText('Ghi chú xử lý'), {
      target: { value: 'Thẩm định biên bản chấm lại hợp lệ, chấp thuận cập nhật điểm.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Phê duyệt quyết định cuối' }))

    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận quyết định' }))

    await waitFor(() =>
      expect(mocks.review).toHaveBeenCalledWith(regradedAppeal.id, {
        decision: 'APPROVED',
        proposedScore: 8,
        reviewNotes: 'Thẩm định biên bản chấm lại hợp lệ, chấp thuận cập nhật điểm.',
      }),
    )
  })

  it('resets selected lecturerId when switching between two submitted appeals so initial examiner never leaks', async () => {
    const secondSubmittedAppeal: Appeal = {
      ...submittedAppeal,
      id: 'appeal-submitted-2',
      ticketId: 'ticket-2',
      studentCode: 'SE170009',
      studentName: 'Pham Van D',
    }
    mocks.list.mockResolvedValue([submittedAppeal, secondSubmittedAppeal])
    mocks.getAllocationProposal.mockResolvedValue({
      ...sampleProposal,
      totalAppeals: 2,
      items: [
        sampleProposal.items[0],
        {
          appealId: secondSubmittedAppeal.id,
          ticketId: secondSubmittedAppeal.ticketId,
          studentCode: secondSubmittedAppeal.studentCode,
          studentName: secondSubmittedAppeal.studentName,
          sessionId: secondSubmittedAppeal.sessionId,
          sessionTitle: secondSubmittedAppeal.sessionTitle,
          initialExaminerUserId: 'lecturer-2',
          initialExaminerName: 'Giảng Viên Độc Lập',
          proposedLecturerId: null,
          proposedLecturerName: null,
          originalScore: 6.0,
          reason: secondSubmittedAppeal.reason,
        },
      ],
    })

    render(
      <MemoryRouter initialEntries={['/examination-officer/appeals']}>
        <Routes>
          <Route path="/examination-officer/appeals" element={<AppealManagementPage />} />
          <Route path="/examination-officer/appeals/:appealId" element={<AppealManagementPage />} />
        </Routes>
      </MemoryRouter>,
    )

    const singleAssignSelect = (await screen.findByLabelText(
      'Chọn giảng viên chấm lại độc lập',
    )) as HTMLSelectElement
    await waitFor(() => expect(singleAssignSelect.value).toBe('lecturer-2'))

    fireEvent.click(screen.getByRole('button', { name: /SE170009/ }))
    await waitFor(() => expect(singleAssignSelect.value).toBe(''))

    const leakedOption = Array.from(singleAssignSelect.querySelectorAll('option')).find(
      (option) => option.value === 'lecturer-2',
    )
    expect(leakedOption).toBeDisabled()
  })

  it('locks proposedScore input as readOnly for ExaminationOfficer deciding REGRADED_BY_LECTURER appeal (EO-07)', async () => {
    mocks.list.mockResolvedValue([regradedAppeal])

    render(
      <MemoryRouter initialEntries={['/examination-officer/appeals']}>
        <Routes>
          <Route path="/examination-officer/appeals" element={<AppealManagementPage />} />
          <Route path="/examination-officer/appeals/:appealId" element={<AppealManagementPage />} />
        </Routes>
      </MemoryRouter>,
    )

    const scoreInput = (await screen.findByLabelText(/Điểm đề xuất/i)) as HTMLInputElement
    expect(scoreInput).toHaveAttribute('readonly')
    expect(scoreInput).toBeDisabled()
    expect(scoreInput.value).toBe('8')
  })

  it('syncs disabled proposedScore input and ConfirmDialog summary when same appeal transitions to REGRADED_BY_LECTURER on refresh', async () => {
    const initialAssigned: Appeal = {
      ...assignedAppeal,
      id: 'appeal-sync-1',
      studentCode: 'SE170088',
      studentName: 'Hoang Van E',
      status: 'ASSIGNED_TO_LECTURER',
      proposedScore: null,
      reviewNotes: null,
    }
    const refreshedRegraded: Appeal = {
      ...initialAssigned,
      status: 'REGRADED_BY_LECTURER',
      proposedScore: 8.5,
      reviewNotes: 'Đã nghe lại toàn bộ bản ghi âm và xác nhận đáp án đúng chuẩn CLO3.',
    }

    mocks.list
      .mockResolvedValueOnce([initialAssigned])
      .mockResolvedValueOnce([refreshedRegraded])

    render(
      <MemoryRouter initialEntries={['/examination-officer/appeals']}>
        <Routes>
          <Route path="/examination-officer/appeals" element={<AppealManagementPage />} />
          <Route path="/examination-officer/appeals/:appealId" element={<AppealManagementPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('SE170088')).toBeInTheDocument()
    expect(screen.queryByLabelText(/Điểm đề xuất/i)).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Làm mới/i }))

    const scoreInput = (await screen.findByLabelText(/Điểm đề xuất/i)) as HTMLInputElement
    expect(scoreInput).toBeDisabled()
    expect(scoreInput.value).toBe('8.5')

    fireEvent.click(screen.getByRole('button', { name: 'Phê duyệt quyết định cuối' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(dialog).toHaveTextContent(/Điểm đề xuất: 8\.5/)
  })
})
