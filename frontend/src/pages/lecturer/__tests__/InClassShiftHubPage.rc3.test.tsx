import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { InClassShiftHubPage } from '../InClassShiftHubPage'
import { inClassShiftService } from '@/services/inClassShift.service'
import type {
  InClassShift,
  InClassShiftPoolStats,
  LecturerClassOption,
} from '@/types/inClassShift.types'

vi.mock('@/services/inClassShift.service', () => ({
  inClassShiftService: {
    getLecturerClasses: vi.fn(),
    listShifts: vi.fn(),
    getPoolStats: vi.fn(),
    createShift: vi.fn(),
    activateShift: vi.fn(),
    closeShift: vi.fn(),
    getActivePassword: vi.fn(),
  },
}))

const mockClasses: LecturerClassOption[] = [
  {
    id: 'cls-prn',
    name: 'PRN231 - SE1801',
    courseId: 'course-prn',
    courseCode: 'PRN231',
    courseName: 'Building Cross-Platform Web APIs',
    studentCount: 30,
  },
]

const mockPoolStats: InClassShiftPoolStats = {
  classId: 'cls-prn',
  courseId: 'course-prn',
  courseCode: 'PRN231',
  courseBankCounts: { easy: 3, medium: 2, hard: 0, total: 5 },
  clos: [
    {
      cloId: 'clo-1',
      cloCode: 'CLO1',
      description: 'Hiểu kiến trúc Clean Architecture',
      counts: { easy: 2, medium: 1, hard: 0, total: 3 },
    },
    {
      cloId: 'clo-2',
      cloCode: 'CLO2',
      description: 'Áp dụng CQRS & FluentValidation',
      counts: { easy: 1, medium: 1, hard: 0, total: 2 },
    },
    {
      cloId: 'clo-3',
      cloCode: 'CLO3',
      description: 'Triển khai SignalR thời gian thực',
      counts: { easy: 0, medium: 0, hard: 0, total: 0 },
    },
  ],
}

const existingLegacyShift: InClassShift = {
  id: 'shift-legacy-01',
  classId: 'cls-prn',
  className: 'PRN231 - SE1801',
  courseCode: 'PRN231',
  title: 'Ca kiểm tra cũ tuần trước',
  description: '',
  status: 'FINALIZED',
  isLocked: true,
  startTime: '2026-11-10T01:00:00Z',
  endTime: '2026-11-10T02:00:00Z',
  durationMinutes: 60,
  examDurationMinutes: 15,
  questionCount: 2,
  hasFollowUp: false,
  maxFollowUpQuestions: 0,
  isAutoPublishGrades: false,
  questionPoolMode: 'SELECTED_POOL',
  selectedDifficulties: ['easy', 'medium', 'hard'],
  selectedCloIds: [],
  passwordRotationSeconds: 300,
  gracePeriodSeconds: 60,
  joinedCount: 28,
  submittedCount: 28,
  createdAt: '2026-11-10T00:30:00Z',
}

describe('InClassShiftHubPage (RC-3 New Shift Design)', () => {
  beforeEach(() => {
    vi.mocked(inClassShiftService.getLecturerClasses).mockResolvedValue(mockClasses)
    vi.mocked(inClassShiftService.listShifts).mockResolvedValue([existingLegacyShift])
    vi.mocked(inClassShiftService.getPoolStats).mockResolvedValue(mockPoolStats)
  })

  afterEach(() => {
    cleanup()
    vi.resetAllMocks()
  })

  it('displays legacy SELECTED_POOL shift config cleanly and validates RC-3 CLO/difficulty preview & submission without selectedQuestionIds', async () => {
    vi.mocked(inClassShiftService.createShift).mockImplementation(async (req) => ({
      id: 'shift-rc3-new',
      classId: req.classId,
      className: 'PRN231 - SE1801',
      courseCode: 'PRN231',
      title: req.shiftName,
      description: req.description ?? '',
      status: 'SCHEDULED',
      isLocked: false,
      startTime: req.startTime,
      endTime: req.endTime,
      durationMinutes: 120,
      examDurationMinutes: req.examDurationMinutes,
      questionCount: req.totalQuestionsPerStudent,
      hasFollowUp: req.hasFollowUp,
      maxFollowUpQuestions: req.maxFollowUpQuestions,
      isAutoPublishGrades: req.isAutoPublishGrades,
      questionPoolMode: req.questionPoolMode,
      selectedDifficulties: req.selectedDifficulties,
      selectedCloIds: req.selectedCloIds ?? [],
      selectedCloCodes: ['CLO1', 'CLO2'],
      passwordRotationSeconds: 300,
      gracePeriodSeconds: 60,
      joinedCount: 0,
      submittedCount: 0,
      createdAt: new Date().toISOString(),
    }))

    render(
      <MemoryRouter>
        <InClassShiftHubPage />
      </MemoryRouter>,
    )

    // Đọc được ca cũ SELECTED_POOL không lỗi
    expect(await screen.findByText('Ca kiểm tra cũ tuần trước')).toBeInTheDocument()
    expect(screen.getByText(/Bộ câu chọn sẵn \(ca cũ\)/)).toBeInTheDocument()

    // Mở dialog tạo ca mới
    fireEvent.click(screen.getByRole('button', { name: /Tạo ca kiểm tra/i }))
    const titleInput = await screen.findByPlaceholderText(/Kiểm tra tiến độ 1/i)
    fireEvent.change(titleInput, { target: { value: 'Kiểm tra vấn đáp Sprint 2' } })
    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục/i }))

    // Bước 2: Form tự động bỏ chọn mức 0 câu khi mở (còn Dễ + Trung bình = 5 câu); không có checkbox chọn từng câu hỏi
    await screen.findByText('3 câu')
    expect(screen.queryByText(/Sẵn có:/)).toBeNull()
    expect(screen.queryByTestId('pool-allocation-preview')).toBeNull()
    expect(screen.getAllByRole('slider')).toHaveLength(3)
    fireEvent.change(screen.getByRole('slider', { name: 'Thời gian thanh trượt' }), { target: { value: '15' } })
    expect(screen.getByRole('spinbutton', { name: /Thời gian/i })).toHaveValue(15)
    fireEvent.change(screen.getByRole('slider', { name: 'Số câu mỗi đề thanh trượt' }), { target: { value: '3' } })
    expect(screen.getByRole('spinbutton', { name: /Số câu mỗi đề/i })).toHaveValue(3)
    fireEvent.change(screen.getByRole('slider', { name: 'Số câu mỗi đề thanh trượt' }), { target: { value: '2' } })
    expect(screen.getByRole('spinbutton', { name: /Thời gian/i })).toHaveAttribute('max', '60')
    const followUpLimit = screen.getByRole('spinbutton', { name: /Số câu phụ tối đa/i })
    expect(followUpLimit).toBeDisabled()
    fireEvent.click(screen.getByRole('switch', { name: 'Câu hỏi phụ' }))
    expect(followUpLimit).toBeEnabled()
    fireEvent.click(screen.getByRole('switch', { name: 'Câu hỏi phụ' }))
    expect(screen.queryByText(/Chọn bộ câu/i)).not.toBeInTheDocument()

    // Bật thêm mức Khó (0 câu) -> báo lỗi cụ thể mức Khó thiếu câu, sau đó bỏ chọn Khó -> preview cập nhật hợp lệ trở lại
    const diffGroup = screen.getByRole('group', { name: /Độ khó được sử dụng/i })
    const hardCheckbox = within(diffGroup).getByRole('checkbox', { name: /Khó/i })
    fireEvent.click(hardCheckbox)
    expect(within(screen.getByTestId('pool-allocation-preview')).getByText(/Thiếu câu hỏi hợp lệ ở mức Khó/i)).toBeInTheDocument()

    fireEvent.click(hardCheckbox)
    expect(screen.queryByTestId('pool-allocation-preview')).toBeNull()

    // Kiểm tra validation Số câu mỗi đề (1..10): nhập 11 -> báo lỗi
    const countInput = screen.getByRole('spinbutton', { name: /Số câu mỗi đề/i })
    fireEvent.change(countInput, { target: { value: '11' } })
    expect(
      (await screen.findAllByText(/Số câu mỗi đề phải là số nguyên từ 1 đến 10/i)).length,
    ).toBeGreaterThanOrEqual(1)

    // Đặt Số câu mỗi đề = 2 và chuyển sang chế độ SELECTED_CLOS
    fireEvent.change(countInput, { target: { value: '2' } })
    fireEvent.click(screen.getByRole('radio', { name: /Theo chuẩn đầu ra \(CLO\)/i }))

    // Danh sách chỉ xuất hiện trong popup; Hủy không áp dụng lựa chọn tạm.
    expect(screen.queryByRole('checkbox', { name: /CLO1/i })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Chọn CLO' }))
    let picker = await screen.findByRole('dialog', { name: 'Chọn CLO' })
    fireEvent.click(within(picker).getByRole('checkbox', { name: /CLO1/i }))
    fireEvent.click(within(picker).getByRole('button', { name: 'Hủy' }))
    expect(screen.getByText('Đã chọn 0 CLO')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Bỏ CLO1' })).toBeNull()

    // Có thể chọn nhiều CLO hơn số câu; chỉ chặn CLO không có câu hợp lệ.
    fireEvent.change(countInput, { target: { value: '1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Chọn CLO' }))
    picker = await screen.findByRole('dialog', { name: 'Chọn CLO' })
    expect(within(picker).getByRole('checkbox', { name: /CLO1/i })).not.toBeChecked()
    fireEvent.click(within(picker).getByRole('checkbox', { name: /CLO1/i }))
    fireEvent.click(within(picker).getByRole('checkbox', { name: /CLO2/i }))
    fireEvent.click(within(picker).getByRole('checkbox', { name: /CLO3/i }))
    expect(within(picker).getByRole('button', { name: 'Xác nhận lựa chọn' })).toBeDisabled()
    expect(within(picker).getByRole('alert')).toHaveTextContent(/Thiếu câu hỏi hợp lệ ở CLO3/)
    fireEvent.click(within(picker).getByRole('checkbox', { name: /CLO3/i }))
    fireEvent.click(within(picker).getByRole('button', { name: 'Xác nhận lựa chọn' }))
    expect(screen.getByText('Đã chọn 2 CLO')).toBeInTheDocument()
    expect(screen.queryByText(/Tối đa .*CLO/)).toBeNull()
    fireEvent.change(countInput, { target: { value: '2' } })
    expect(screen.queryByTestId('pool-allocation-preview')).toBeNull()

    // Bỏ nhanh bằng tag, rồi mở lại để bổ sung CLO và giữ đúng thứ tự payload.
    fireEvent.click(screen.getByRole('button', { name: 'Bỏ CLO1' }))
    expect(screen.getByText('Đã chọn 1 CLO')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Chọn CLO' }))
    picker = await screen.findByRole('dialog', { name: 'Chọn CLO' })
    expect(within(picker).getByRole('checkbox', { name: /CLO1/i })).not.toBeChecked()
    expect(within(picker).getByRole('checkbox', { name: /CLO2/i })).toBeChecked()
    fireEvent.click(within(picker).getByRole('checkbox', { name: /CLO1/i }))
    fireEvent.click(within(picker).getByRole('button', { name: 'Xác nhận lựa chọn' }))
    expect(screen.queryByText(/Sẵn có:/)).toBeNull()

    // Bấm Tạo ca -> gửi đúng contract, tuyệt đối không có selectedQuestionIds
    const submitBtn = screen.getByRole('button', { name: /^Tạo ca$/i })
    expect(submitBtn).not.toBeDisabled()
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(inClassShiftService.createShift).toHaveBeenCalledTimes(1)
    })
    const sentPayload = vi.mocked(inClassShiftService.createShift).mock.calls[0][0]
    expect('selectedQuestionIds' in sentPayload).toBe(false)
    expect(sentPayload.questionPoolMode).toBe('SELECTED_CLOS')
    expect(sentPayload.totalQuestionsPerStudent).toBe(2)
    expect(sentPayload.selectedDifficulties).toEqual(['easy', 'medium'])
    expect(sentPayload.selectedCloIds).toEqual(['clo-1', 'clo-2'])

    // Ca mới hiển thị cấu hình đã lưu trên danh sách ca
    expect(await screen.findByText('Kiểm tra vấn đáp Sprint 2')).toBeInTheDocument()
    expect(screen.getByText(/Theo CLO \(CLO1, CLO2\)/)).toBeInTheDocument()
  })
})
