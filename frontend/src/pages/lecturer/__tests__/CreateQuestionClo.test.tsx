import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { CreatePracticeQuestionForm } from '../CreateQuestionPage'
import { courseService } from '@/services/course.service'
import { courseCloService } from '@/services/courseClo.service'
import { questionService } from '@/services/question.service'

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: '11111111-1111-4111-8111-111111111111' } }),
}))
vi.mock('@/services/course.service', () => ({ courseService: { getLecturerCourses: vi.fn() } }))
vi.mock('@/services/courseClo.service', () => ({ courseCloService: { list: vi.fn() } }))
vi.mock('@/services/question.service', () => ({
  questionService: { createPracticeQuestion: vi.fn(), batchSubmitForReview: vi.fn() },
}))
vi.mock('@/components/common/FilterDropMenu', () => ({
  FilterDropMenu: ({ label, value, options, onChange }: {
    label: string
    value: string
    options: Array<{ value: string; label: string }>
    onChange: (value: string) => void
  }) => (
    <select aria-label={label} value={value} onChange={(event) => onChange(event.target.value)}>
      <option value="">Chọn</option>
      {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  ),
}))

const courseId = '22222222-2222-4222-8222-222222222222'
const cloId = '33333333-3333-4333-8333-333333333333'

describe('CreatePracticeQuestionForm CLO binding', () => {
  beforeEach(() => {
    vi.mocked(courseService.getLecturerCourses).mockResolvedValue([
      { id: courseId, code: 'PRF192', name: 'Programming Fundamentals' },
    ])
    vi.mocked(courseCloService.list).mockResolvedValue([{
      id: cloId, courseId, cloCode: 'CLO1', description: 'Giải thích thuật toán',
      bloomLevel: 'Understand', orderIndex: 1, isActive: true, createdAt: '2026-11-17T00:00:00Z',
    }])
    vi.mocked(questionService.createPracticeQuestion).mockResolvedValue({ questionId: 'question-1' })
  })

  afterEach(() => { cleanup(); vi.resetAllMocks() })

  it('requires an active course CLO and sends its id when saving a manual practice question', async () => {
    render(<MemoryRouter><CreatePracticeQuestionForm /></MemoryRouter>)
    fireEvent.click(screen.getByRole('button', { name: /Soạn câu hỏi thủ công/i }))
    await waitFor(() => expect(screen.getByRole('option', { name: /CLO1/i })).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText('Tên ngắn gọn giúp nhận diện câu hỏi'),
      { target: { value: 'Thuật toán tìm kiếm' } })
    fireEvent.change(screen.getByPlaceholderText('Viết đầy đủ yêu cầu mà sinh viên cần trình bày...'),
      { target: { value: 'Giải thích thuật toán tìm kiếm nhị phân' } })
    fireEvent.change(screen.getByPlaceholderText('Câu trả lời chuẩn mực dùng làm mốc chấm...'),
      { target: { value: 'Thuật toán tìm kiếm nhị phân chia đôi phạm vi ở từng bước và chỉ áp dụng cho dãy đã sắp xếp.' } })

    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục/i }))
    expect(await screen.findByText('Chọn CLO đang hoạt động của môn học trước khi lưu câu hỏi.')).toBeInTheDocument()
    expect(questionService.createPracticeQuestion).not.toHaveBeenCalled()

    fireEvent.change(screen.getByRole('combobox', { name: 'Chọn CLO cho câu hỏi' }), { target: { value: cloId } })
    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục/i }))
    fireEvent.change(screen.getByPlaceholderText('Ví dụ: Rubric đánh giá kiến trúc'),
      { target: { value: 'Rubric thuật toán' } })
    const criteria = screen.getAllByPlaceholderText('Tên tiêu chí đánh giá')
    fireEvent.change(criteria[0], { target: { value: 'Đúng nguyên lý' } })
    fireEvent.change(criteria[1], { target: { value: 'Giải thích độ phức tạp' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu thành bản nháp' }))

    await waitFor(() => expect(questionService.createPracticeQuestion).toHaveBeenCalledWith(
      expect.objectContaining({ courseId, cloId, usageScope: 'practice' }),
    ))
  })
})
