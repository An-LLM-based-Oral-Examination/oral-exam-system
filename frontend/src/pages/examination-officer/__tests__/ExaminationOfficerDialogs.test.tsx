import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ExaminationOfficerExamDetailPage } from '../ExaminationOfficerExamDetailPage'
import { ExaminationOfficerDlqPage } from '../ExaminationOfficerDlqPage'
import { BatchShiftCreatorModal } from '@/components/examination-officer/BatchShiftCreatorModal'
import { officialExamService } from '@/services/officialExam.service'
import { examPreparationService } from '@/services/examPreparation.service'
import { deadLetterQueueService } from '@/services/deadLetterQueue.service'

vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getSessions: vi.fn(),
    getShifts: vi.fn(),
    getExamSets: vi.fn(),
    getEnrollmentEstimate: vi.fn(),
    publishShift: vi.fn(),
    updateShift: vi.fn(),
  },
}))

vi.mock('@/services/examPreparation.service', () => ({
  examPreparationService: {
    list: vi.fn(),
    request: vi.fn(),
  },
}))

vi.mock('@/services/course.service', () => ({
  courseService: {
    getCourseLecturers: vi.fn().mockResolvedValue([]),
    getShiftStaffCandidates: vi.fn().mockResolvedValue({
      proctors: [],
      examiners: [],
      departmentHeadIds: [],
    }),
  },
}))

vi.mock('@/services/deadLetterQueue.service', () => ({
  deadLetterQueueService: {
    listForExaminationOfficer: vi.fn(),
    replayForExaminationOfficer: vi.fn(),
    resolveForExaminationOfficer: vi.fn(),
  },
}))

describe('EO Design-System Dialogs (zero window.confirm / window.prompt)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(examPreparationService.list).mockResolvedValue([])
    vi.mocked(officialExamService.getEnrollmentEstimate).mockResolvedValue(null as never)
  })

  it('uses ConfirmDialog instead of window.confirm to publish a shift in ExaminationOfficerExamDetailPage', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm')
    vi.mocked(officialExamService.getSessions).mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-1',
        title: 'PRN231 · FE',
        examDate: '2026-11-20',
        hasFollowUp: false,
        maxFollowUpQuestions: 2,
        examInputMode: 'VoiceOnly',
        transcriptBufferSeconds: 60,
        status: 'scheduled',
      },
    ])
    vi.mocked(officialExamService.getShifts).mockResolvedValue([
      {
        id: 'shift-1',
        sessionId: 'session-1',
        shiftName: 'Ca 1',
        roomLab: 'LAB-301',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        proctorUserId: 'proctor-1',
        examinerUserId: 'examiner-1',
        examinerName: 'Giám khảo 1',
        proctorName: 'Giám thị 1',
        status: 'scheduled',
        isExamPublished: false,
      },
    ])
    vi.mocked(officialExamService.getExamSets).mockResolvedValue([])
    vi.mocked(officialExamService.publishShift).mockResolvedValue({
      success: true,
      shiftId: 'shift-1',
      message: 'Đã xuất bản đề thi.',
    })

    render(
      <MemoryRouter initialEntries={['/examination-officer/exams/session-1']}>
        <Routes>
          <Route
            path="/examination-officer/exams/:sessionId"
            element={<ExaminationOfficerExamDetailPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByRole('button', { name: 'Xuất bản đề' }, { timeout: 3000 }))
    expect(confirmSpy).not.toHaveBeenCalled()
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận xuất bản' }))
    await waitFor(() => expect(officialExamService.publishShift).toHaveBeenCalledWith('shift-1'))
    expect(await screen.findByRole('button', { name: 'Đã xuất bản' })).toBeDisabled()
    expect(screen.getByText('Đã lên lịch')).toBeInTheDocument()
    expect(screen.queryByText('PUBLISHED')).not.toBeInTheDocument()
    confirmSpy.mockRestore()
  })

  it('uses FormDialog with inline >=5 char validation instead of window.prompt in ExaminationOfficerDlqPage', async () => {
    const promptSpy = vi.spyOn(window, 'prompt')
    vi.mocked(deadLetterQueueService.listForExaminationOfficer).mockResolvedValue([
      {
        id: 'dlq-1',
        flowType: 'OFFICIAL_EXAM',
        taskType: 'OFFICIAL_GRADING',
        payloadJson: '{"ticketId":"ticket-1"}',
        status: 'pending',
        retryCount: 3,
        errorMessage: 'Gemini timeout after 3 retries',
        createdAt: '2026-11-20T08:30:00.000Z',
        resolvedAt: null,
      },
    ])
    vi.mocked(deadLetterQueueService.resolveForExaminationOfficer).mockResolvedValue({
      id: 'dlq-1',
      dlqId: 'dlq-1',
      status: 'resolved',
      message: 'Đã đánh dấu xử lý.',
    })

    render(<ExaminationOfficerDlqPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Xử lý' }, { timeout: 3000 }))
    expect(promptSpy).not.toHaveBeenCalled()
    expect(await screen.findByRole('dialog')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Ghi chú xử lý/), { target: { value: 'abc' } })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận đã xử lý' }))
    expect(await screen.findByText('Ghi chú xử lý cần ít nhất 5 ký tự.')).toBeInTheDocument()
    expect(deadLetterQueueService.resolveForExaminationOfficer).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText(/Ghi chú xử lý/), {
      target: { value: 'Giám khảo đã chấm thủ công bằng chứng âm thanh.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận đã xử lý' }))

    await waitFor(() =>
      expect(deadLetterQueueService.resolveForExaminationOfficer).toHaveBeenCalledWith(
        'dlq-1',
        'Giám khảo đã chấm thủ công bằng chứng âm thanh.',
      ),
    )
    promptSpy.mockRestore()
  })

  it('uses exact backend DLQ status filters (pending, resolved, abandoned, all) in ExaminationOfficerDlqPage', async () => {
    vi.mocked(deadLetterQueueService.listForExaminationOfficer).mockResolvedValue([])

    render(<ExaminationOfficerDlqPage />)

    const select = await screen.findByLabelText('Lọc trạng thái DLQ', {}, { timeout: 3000 })
    const optionValues = Array.from(select.querySelectorAll('option')).map((opt) => opt.value)
    expect(optionValues).toEqual(['pending', 'resolved', 'abandoned', 'all'])
    expect(optionValues).not.toContain('failed')
    expect(optionValues).not.toContain('processing')

    await waitFor(() =>
      expect(deadLetterQueueService.listForExaminationOfficer).toHaveBeenCalledWith('pending'),
    )

    fireEvent.change(select, { target: { value: 'abandoned' } })
    await waitFor(() =>
      expect(deadLetterQueueService.listForExaminationOfficer).toHaveBeenCalledWith('abandoned'),
    )

    fireEvent.change(select, { target: { value: 'all' } })
    await waitFor(() =>
      expect(deadLetterQueueService.listForExaminationOfficer).toHaveBeenCalledWith(undefined),
    )
  })

  it('wires CandidateRosterImportPanel and lets EO submit RequestExamPreparation from ExaminationOfficerExamDetailPage', async () => {
    vi.mocked(officialExamService.getSessions).mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-1',
        title: 'PRN231 · FE',
        examDate: '2099-11-20',
        hasFollowUp: false,
        maxFollowUpQuestions: 2,
        examInputMode: 'VoiceOnly',
        transcriptBufferSeconds: 60,
        status: 'scheduled',
      },
    ])
    vi.mocked(officialExamService.getShifts).mockResolvedValue([])
    vi.mocked(officialExamService.getExamSets).mockResolvedValue([])
    vi.mocked(examPreparationService.request).mockResolvedValue({
      id: 'prep-1',
      sessionId: 'session-1',
      requiredSetCount: 4,
      deadline: '2099-11-18T10:00:00.000Z',
    })

    render(
      <MemoryRouter initialEntries={['/examination-officer/exams/session-1']}>
        <Routes>
          <Route
            path="/examination-officer/exams/:sessionId"
            element={<ExaminationOfficerExamDetailPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    expect(
      await screen.findByText(/Bước 2 · Import Danh sách Thí sinh dự thi/i, {}, { timeout: 3000 }),
    ).toBeInTheDocument()

    fireEvent.click(
      await screen.findByRole('button', { name: /Yêu cầu chuẩn bị đề/i }, { timeout: 3000 }),
    )
    expect(await screen.findByRole('dialog')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Số bộ đề cần chuẩn bị/), { target: { value: '0' } })
    fireEvent.click(screen.getByRole('button', { name: /Gửi yêu cầu cho Trưởng BM/i }))
    expect(
      await screen.findByText('Số bộ đề cần chuẩn bị phải là số nguyên lớn hơn 0.'),
    ).toBeInTheDocument()
    expect(examPreparationService.request).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText(/Số bộ đề cần chuẩn bị/), { target: { value: '4' } })
    fireEvent.change(screen.getByLabelText(/Hạn bàn giao bộ đề/), {
      target: { value: '2099-11-18T17:00' },
    })
    fireEvent.change(screen.getByLabelText(/Ghi chú cho Trưởng Bộ môn/), {
      target: { value: 'Chuẩn bị 4 bộ đề cho 2 phòng thi.' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Gửi yêu cầu cho Trưởng BM/i }))

    await waitFor(() =>
      expect(examPreparationService.request).toHaveBeenCalledWith(
        'session-1',
        expect.objectContaining({
          requiredSetCount: 4,
          notes: 'Chuẩn bị 4 bộ đề cho 2 phòng thi.',
        }),
      ),
    )
  })

  it('validates null proctor/examiner in shift edit modal instead of submitting empty strings', async () => {
    const { courseService } = await import('@/services/course.service')
    vi.mocked(courseService.getShiftStaffCandidates).mockResolvedValue({
      proctors: [
        { userId: 'proctor-uuid-1', fullName: 'Giám thị A', detail: 'gt.a@fe.edu.vn' },
      ],
      examiners: [
        { userId: 'examiner-uuid-1', fullName: 'Giám khảo B', detail: 'gk.b@fe.edu.vn' },
      ],
      departmentHeadIds: [],
    })
    vi.mocked(officialExamService.getSessions).mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-1',
        title: 'PRN231 · FE',
        examDate: '2099-11-20',
        hasFollowUp: false,
        maxFollowUpQuestions: 2,
        examInputMode: 'VoiceOnly',
        transcriptBufferSeconds: 60,
        status: 'scheduled',
      },
    ])
    vi.mocked(officialExamService.getShifts).mockResolvedValue([
      {
        id: 'shift-null-staff',
        sessionId: 'session-1',
        shiftName: 'Ca 1',
        roomLab: 'LAB-301',
        startTime: '2099-11-20T08:00:00.000Z',
        endTime: '2099-11-20T09:30:00.000Z',
        proctorUserId: null,
        examinerUserId: null,
        proctorName: null,
        examinerName: null,
        status: 'scheduled',
        isExamPublished: false,
        maxSeats: 24,
      },
    ])
    vi.mocked(officialExamService.getExamSets).mockResolvedValue([])
    vi.mocked(officialExamService.updateShift).mockResolvedValue(undefined)

    render(
      <MemoryRouter initialEntries={['/examination-officer/exams/session-1']}>
        <Routes>
          <Route
            path="/examination-officer/exams/:sessionId"
            element={<ExaminationOfficerExamDetailPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByRole('button', { name: 'Cập nhật ca' }, { timeout: 3000 }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()

    // Attempt to save while proctor/examiner are null -> blocked client-side, never sends ""
    fireEvent.click(screen.getByRole('button', { name: 'Lưu cập nhật ca thi' }))
    expect(
      await screen.findByText('Vui lòng chọn Giám thị cho ca thi trước khi lưu.'),
    ).toBeInTheDocument()
    expect(officialExamService.updateShift).not.toHaveBeenCalled()

    // Select proctor only -> still requires examiner
    fireEvent.change(screen.getByLabelText('Giám thị *'), {
      target: { value: 'proctor-uuid-1' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu cập nhật ca thi' }))
    expect(
      await screen.findByText('Vui lòng chọn Giám khảo cho ca thi trước khi lưu.'),
    ).toBeInTheDocument()
    expect(officialExamService.updateShift).not.toHaveBeenCalled()

    // Select examiner -> submits cleanly with valid staff IDs and updated shift times
    fireEvent.change(screen.getByLabelText('Bắt đầu *'), {
      target: { value: '2099-11-20T10:00' },
    })
    fireEvent.change(screen.getByLabelText('Kết thúc *'), {
      target: { value: '2099-11-20T11:30' },
    })
    fireEvent.change(screen.getByLabelText('Giám khảo *'), {
      target: { value: 'examiner-uuid-1' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu cập nhật ca thi' }))

    await waitFor(() =>
      expect(officialExamService.updateShift).toHaveBeenCalledWith(
        'shift-null-staff',
        expect.objectContaining({
          shiftName: 'Ca 1',
          roomLab: 'LAB-301',
          startTime: new Date('2099-11-20T10:00').toISOString(),
          endTime: new Date('2099-11-20T11:30').toISOString(),
          proctorUserId: 'proctor-uuid-1',
          examinerUserId: 'examiner-uuid-1',
          maxSeats: 24,
        }),
      ),
    )
  })

  it('defaults requiredSetCount in RequestExamPreparationDialog from recommendedTotalRooms (clamped 2..50), not eligibleStudentCount', async () => {
    vi.mocked(officialExamService.getSessions).mockResolvedValue([
      {
        id: 'session-1',
        courseId: 'course-1',
        title: 'PRN231 · FE',
        examDate: '2099-11-20',
        hasFollowUp: false,
        maxFollowUpQuestions: 2,
        examInputMode: 'VoiceOnly',
        transcriptBufferSeconds: 60,
        status: 'scheduled',
      },
    ])
    vi.mocked(officialExamService.getShifts).mockResolvedValue([])
    vi.mocked(officialExamService.getExamSets).mockResolvedValue([])
    vi.mocked(officialExamService.getEnrollmentEstimate).mockResolvedValueOnce({
      sessionId: 'session-1',
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: 'PRN231',
      semesterId: 'sem-1',
      semesterCode: 'FA26',
      examAttemptType: 'FE',
      candidateSource: 'imported',
      hasImportedCandidates: true,
      importedCandidateCount: 24,
      semesterEnrolledCount: 24,
      eligibleStudentCount: 24,
      allocatedStudentCount: 0,
      unallocatedStudentCount: 24,
      maxStudentsPerRoom: 24,
      recommendedTotalRooms: 1,
      recommendedAdditionalRooms: 1,
      currentShiftRoomCount: 0,
      currentTotalCapacity: 0,
      currentAvailableSeats: 0,
      handedOverExamSetCount: 0,
    })

    const { unmount } = render(
      <MemoryRouter initialEntries={['/examination-officer/exams/session-1']}>
        <Routes>
          <Route
            path="/examination-officer/exams/:sessionId"
            element={<ExaminationOfficerExamDetailPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(
      await screen.findByRole('button', { name: /Yêu cầu chuẩn bị đề/i }, { timeout: 3000 }),
    )
    expect(await screen.findByLabelText(/Số bộ đề cần chuẩn bị/)).toHaveValue(2)
    unmount()

    vi.mocked(officialExamService.getEnrollmentEstimate).mockResolvedValueOnce({
      sessionId: 'session-1',
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: 'PRN231',
      semesterId: 'sem-1',
      semesterCode: 'FA26',
      examAttemptType: 'FE',
      candidateSource: 'imported',
      hasImportedCandidates: true,
      importedCandidateCount: 120,
      semesterEnrolledCount: 120,
      eligibleStudentCount: 120,
      allocatedStudentCount: 0,
      unallocatedStudentCount: 120,
      maxStudentsPerRoom: 24,
      recommendedTotalRooms: 5,
      recommendedAdditionalRooms: 5,
      currentShiftRoomCount: 0,
      currentTotalCapacity: 0,
      currentAvailableSeats: 0,
      handedOverExamSetCount: 0,
    })

    const secondRender = render(
      <MemoryRouter initialEntries={['/examination-officer/exams/session-1']}>
        <Routes>
          <Route
            path="/examination-officer/exams/:sessionId"
            element={<ExaminationOfficerExamDetailPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(
      await screen.findByRole('button', { name: /Yêu cầu chuẩn bị đề/i }, { timeout: 3000 }),
    )
    expect(await screen.findByLabelText(/Số bộ đề cần chuẩn bị/)).toHaveValue(5)
    secondRender.unmount()

    vi.mocked(officialExamService.getEnrollmentEstimate).mockResolvedValueOnce({
      sessionId: 'session-1',
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: 'PRN231',
      semesterId: 'sem-1',
      semesterCode: 'FA26',
      examAttemptType: 'FE',
      candidateSource: 'imported',
      hasImportedCandidates: true,
      importedCandidateCount: 600,
      semesterEnrolledCount: 600,
      eligibleStudentCount: 600,
      allocatedStudentCount: 0,
      unallocatedStudentCount: 600,
      maxStudentsPerRoom: 24,
      recommendedTotalRooms: 25,
      recommendedAdditionalRooms: 25,
      currentShiftRoomCount: 0,
      currentTotalCapacity: 0,
      currentAvailableSeats: 0,
      handedOverExamSetCount: 0,
    })

    render(
      <MemoryRouter initialEntries={['/examination-officer/exams/session-1']}>
        <Routes>
          <Route
            path="/examination-officer/exams/:sessionId"
            element={<ExaminationOfficerExamDetailPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(
      await screen.findByRole('button', { name: /Yêu cầu chuẩn bị đề/i }, { timeout: 3000 }),
    )
    expect(await screen.findByLabelText(/Số bộ đề cần chuẩn bị/)).toHaveValue(25)
  })

  it('allows clearing and typing multi-digit roomCountInput in BatchShiftCreatorModal and normalizes [1..50] on blur', async () => {
    const user = userEvent.setup()

    render(
      <BatchShiftCreatorModal
        open
        sessionId="session-1"
        defaultExamDate="2026-11-20"
        recommendedRooms={1}
        proctors={[{ userId: 'proctor-1', fullName: 'Giám thị 1', detail: 'gt1@fe.edu.vn' }]}
        examiners={[{ userId: 'examiner-1', fullName: 'Giám khảo 1', detail: 'gk1@fe.edu.vn' }]}
        onClose={vi.fn()}
        onCreated={vi.fn()}
      />,
    )

    const input = screen.getByLabelText('Số lượng phòng K') as HTMLInputElement
    expect(input.value).toBe('1')

    await user.clear(input)
    expect(input.value).toBe('')

    await user.type(input, '15')
    expect(input.value).toBe('15')
    expect(screen.getAllByLabelText(/^Tên phòng Lab \d+$/)).toHaveLength(15)
    expect(screen.getByRole('button', { name: 'Mở 15 phòng thi' })).toBeInTheDocument()

    // Empty blur restores current valid room count
    await user.clear(input)
    expect(input.value).toBe('')
    fireEvent.blur(input)
    expect(input.value).toBe('15')
    expect(screen.getAllByLabelText(/^Tên phòng Lab \d+$/)).toHaveLength(15)

    // Out-of-range > 50 clamps to 50 on blur
    await user.clear(input)
    await user.type(input, '75')
    fireEvent.blur(input)
    expect(input.value).toBe('50')
    expect(screen.getAllByLabelText(/^Tên phòng Lab \d+$/)).toHaveLength(50)

    // Out-of-range < 1 clamps to 1 on blur
    await user.clear(input)
    await user.type(input, '0')
    fireEvent.blur(input)
    expect(input.value).toBe('1')
    expect(screen.getAllByLabelText(/^Tên phòng Lab \d+$/)).toHaveLength(1)

    // Preset button synchronizes draft input state
    await user.click(screen.getByRole('button', { name: '3' }))
    expect(input.value).toBe('3')
    expect(screen.getAllByLabelText(/^Tên phòng Lab \d+$/)).toHaveLength(3)
  })
})
