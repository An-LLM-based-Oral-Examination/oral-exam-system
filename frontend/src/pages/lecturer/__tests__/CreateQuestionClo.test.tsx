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
  questionService: {
    createPracticeQuestion: vi.fn(),
    batchSubmitForReview: vi.fn(),
    generateFromClo: vi.fn(),
    uploadQuestionImage: vi.fn(),
  },
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
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      configurable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: true,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    })
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

  it('clears prior manual image and disables image upload when editing an AI-generated draft (source=flm_api)', async () => {
    window.scrollTo = vi.fn()
    vi.mocked(questionService.uploadQuestionImage).mockResolvedValue({
      imageUrl: '/uploads/questions/202611/manual.png',
      objectKey: 'questions/202611/manual.png',
      fileName: 'manual.png',
      contentType: 'image/png',
      sizeBytes: 512,
    })
    vi.mocked(questionService.generateFromClo).mockResolvedValue({
      courseId,
      courseCode: 'PRF192',
      generatedCount: 1,
      source: 'flm_api',
      questions: [
        {
          tempId: 'ai-temp-1',
          cloCode: 'CLO1',
          title: 'Độ phức tạp tìm kiếm nhị phân',
          content: 'Trình bày độ phức tạp thời gian của tìm kiếm nhị phân.',
          difficulty: 'medium',
          bloomLevel: 'Understand',
          source: 'flm_api',
          usageScope: 'practice',
          modelAnswer:
            'Tìm kiếm nhị phân giảm một nửa không gian tìm kiếm ở mỗi bước nên có độ phức tạp O(log n) trong trường hợp xấu nhất.',
          keyPoints: ['O(log n)'],
          hasFollowUp: false,
          followUpPrompt: null,
          rubric: {
            name: 'Rubric AI',
            totalMaxScore: 10,
            criteria: [
              {
                criterionName: 'Phân tích đúng',
                maxScore: 5,
                weight: 0.5,
                bloomLevel: 'Understand',
                description: '',
              },
              {
                criterionName: 'Ví dụ minh họa',
                maxScore: 5,
                weight: 0.5,
                bloomLevel: 'Understand',
                description: '',
              },
            ],
          },
        },
      ],
    })

    const { container } = render(
      <MemoryRouter>
        <CreatePracticeQuestionForm />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: /Soạn câu hỏi thủ công/i }))
    await waitFor(() => expect(screen.getByRole('option', { name: /CLO1/i })).toBeInTheDocument())

    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement
    const file = new File(['img'], 'manual.png', { type: 'image/png' })
    fireEvent.change(fileInput, { target: { files: [file] } })

    await waitFor(() => expect(screen.getByLabelText('Mô tả ảnh')).toBeInTheDocument())
    fireEvent.change(screen.getByLabelText('Mô tả ảnh'), { target: { value: 'Ảnh cũ' } })

    fireEvent.click(screen.getByRole('button', { name: 'Đổi cách tạo' }))
    fireEvent.click(screen.getByRole('button', { name: /Tạo bộ câu hỏi bằng AI/i }))
    fireEvent.click(screen.getByRole('button', { name: 'Tạo bộ câu hỏi bằng AI' }))

    expect(await screen.findByText('Độ phức tạp tìm kiếm nhị phân')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Chỉnh sửa trước khi lưu' }))

    expect(
      await screen.findByText('Câu hỏi nguồn AI không hỗ trợ đính kèm hình ảnh.'),
    ).toBeInTheDocument()
    expect(container.querySelector('input[type="file"]')).toBeNull()
    expect(screen.queryByRole('button', { name: /Thêm ảnh|Thay ảnh/ })).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục/i }))
    fireEvent.click(screen.getByRole('button', { name: 'Lưu thành bản nháp' }))

    await waitFor(() =>
      expect(questionService.createPracticeQuestion).toHaveBeenCalledWith(
        expect.objectContaining({
          source: 'flm_api',
          imageUrl: null,
          imageDescription: null,
        }),
      ),
    )
  })

  it('validates title <= 300, rubricName <= 200, criterionName <= 200, and maxScore <= 2 decimals', async () => {
    render(
      <MemoryRouter>
        <CreatePracticeQuestionForm />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: /Soạn câu hỏi thủ công/i }))
    await waitFor(() => expect(screen.getByRole('option', { name: /CLO1/i })).toBeInTheDocument())

    fireEvent.change(screen.getByRole('combobox', { name: 'Chọn CLO cho câu hỏi' }), {
      target: { value: cloId },
    })
    fireEvent.change(screen.getByPlaceholderText('Tên ngắn gọn giúp nhận diện câu hỏi'), {
      target: { value: 'T'.repeat(301) },
    })
    fireEvent.change(screen.getByPlaceholderText('Viết đầy đủ yêu cầu mà sinh viên cần trình bày...'), {
      target: { value: 'Nội dung câu hỏi hợp lệ' },
    })
    fireEvent.change(screen.getByPlaceholderText('Câu trả lời chuẩn mực dùng làm mốc chấm...'), {
      target: {
        value:
          'Câu trả lời mẫu đủ 50 ký tự để kiểm tra điều kiện độ dài tiêu đề và rubric.',
      },
    })

    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục/i }))
    expect(
      await screen.findByText('Tiêu đề không được vượt quá 300 ký tự.'),
    ).toBeInTheDocument()

    fireEvent.change(screen.getByPlaceholderText('Tên ngắn gọn giúp nhận diện câu hỏi'), {
      target: { value: 'Tiêu đề hợp lệ' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục/i }))

    fireEvent.change(screen.getByPlaceholderText('Ví dụ: Rubric đánh giá kiến trúc'), {
      target: { value: 'R'.repeat(201) },
    })
    const criteria = screen.getAllByPlaceholderText('Tên tiêu chí đánh giá')
    fireEvent.change(criteria[0], { target: { value: 'C'.repeat(201) } })
    fireEvent.change(criteria[1], { target: { value: 'Tiêu chí 2' } })
    const scoreInputs = screen.getAllByLabelText('Điểm')
    fireEvent.change(scoreInputs[0], { target: { value: '3.333' } })
    fireEvent.change(scoreInputs[1], { target: { value: '6.667' } })

    fireEvent.click(screen.getByRole('button', { name: 'Lưu thành bản nháp' }))

    expect(
      await screen.findByText('Tên rubric không được vượt quá 200 ký tự.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Tên tiêu chí tối đa 200 ký tự.')).toBeInTheDocument()
    expect(
      screen.getAllByText('Điểm chỉ hỗ trợ tối đa 2 chữ số thập phân.'),
    ).toHaveLength(2)
    expect(questionService.createPracticeQuestion).not.toHaveBeenCalled()
  })

  it('rejects 1e-9 (< 0.01) and 5.000000001 criterion scores on manual step 2', async () => {
    render(
      <MemoryRouter>
        <CreatePracticeQuestionForm />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: /Soạn câu hỏi thủ công/i }))
    await waitFor(() => expect(screen.getByRole('option', { name: /CLO1/i })).toBeInTheDocument())

    fireEvent.change(screen.getByRole('combobox', { name: 'Chọn CLO cho câu hỏi' }), {
      target: { value: cloId },
    })
    fireEvent.change(screen.getByPlaceholderText('Tên ngắn gọn giúp nhận diện câu hỏi'), {
      target: { value: 'Tiêu đề hợp lệ' },
    })
    fireEvent.change(screen.getByPlaceholderText('Viết đầy đủ yêu cầu mà sinh viên cần trình bày...'), {
      target: { value: 'Nội dung câu hỏi hợp lệ' },
    })
    fireEvent.change(screen.getByPlaceholderText('Câu trả lời chuẩn mực dùng làm mốc chấm...'), {
      target: {
        value: 'Câu trả lời mẫu đủ 50 ký tự để kiểm tra biên nhỏ hơn 0.01 và sai số 1e-9.',
      },
    })
    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục/i }))

    fireEvent.change(screen.getByPlaceholderText('Ví dụ: Rubric đánh giá kiến trúc'), {
      target: { value: 'Rubric chuẩn' },
    })
    const criteria = screen.getAllByPlaceholderText('Tên tiêu chí đánh giá')
    fireEvent.change(criteria[0], { target: { value: 'Tiêu chí 1' } })
    fireEvent.change(criteria[1], { target: { value: 'Tiêu chí 2' } })
    const scoreInputs = screen.getAllByLabelText('Điểm')

    fireEvent.change(scoreInputs[0], { target: { value: '1e-9' } })
    fireEvent.change(scoreInputs[1], { target: { value: '10' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu thành bản nháp' }))

    expect(await screen.findByText('Điểm phải từ 0.01 đến 10.')).toBeInTheDocument()
    expect(questionService.createPracticeQuestion).not.toHaveBeenCalled()

    fireEvent.change(scoreInputs[0], { target: { value: '5' } })
    fireEvent.change(scoreInputs[1], { target: { value: '5.000000001' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu thành bản nháp' }))

    expect(
      await screen.findByText('Điểm chỉ hỗ trợ tối đa 2 chữ số thập phân.'),
    ).toBeInTheDocument()
    expect(questionService.createPracticeQuestion).not.toHaveBeenCalled()
  })
})
