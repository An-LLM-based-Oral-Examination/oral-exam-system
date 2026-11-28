import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { EditQuestionDraftDialog } from '../EditQuestionDraftDialog'
import { courseCloService } from '@/services/courseClo.service'
import { questionService } from '@/services/question.service'
import type { QuestionDetail } from '@/types/rubric.types'

vi.mock('@/services/courseClo.service', () => ({
  courseCloService: {
    list: vi.fn(),
  },
}))

vi.mock('@/services/question.service', () => ({
  questionService: {
    updateDraft: vi.fn(),
    uploadQuestionImage: vi.fn(),
  },
}))

const COURSE_UUID = '11111111-1111-4111-8111-111111111111'
const CLO1_UUID = '22222222-2222-4222-8222-222222222221'
const CLO2_UUID = '22222222-2222-4222-8222-222222222222'

function makeQuestion(overrides: Partial<QuestionDetail> = {}): QuestionDetail {
  return {
    id: 'question-1',
    courseId: COURSE_UUID,
    courseCode: 'PRN231',
    cloId: CLO1_UUID,
    cloCode: 'CLO1',
    rubricId: 'rubric-1',
    title: 'Clean Architecture DIP',
    content: 'Giải thích Dependency Inversion Principle và lợi ích khi kiểm thử.',
    sampleAnswer:
      'Module cấp cao không phụ thuộc trực tiếp module cấp thấp; cả hai phụ thuộc vào abstraction thông qua interface.',
    keyPoints: ['Phụ thuộc abstraction', 'Giảm coupling'],
    difficulty: 'medium',
    bloomLevel: 'Analyze',
    source: 'manual',
    approvalStatus: 'DRAFT',
    submittedBy: 'lecturer-1',
    submittedByName: 'Giảng viên A',
    approvedBy: null,
    approvedByName: null,
    reviewNotes: null,
    isActive: true,
    createdAt: '2026-11-20T00:00:00Z',
    rubric: {
      id: 'rubric-1',
      name: 'Rubric 10.0',
      description: 'Chuẩn chấm 10đ',
      totalMaxScore: 10,
      criteria: [
        {
          id: 'crit-1',
          criterionName: 'Định nghĩa đúng',
          description: 'Trình bày chính xác',
          maxScore: 5,
          weight: 0.5,
          bloomLevel: 'Understand',
          orderIndex: 1,
        },
        {
          id: 'crit-2',
          criterionName: 'Ví dụ áp dụng',
          description: 'Liên hệ .NET 8',
          maxScore: 5,
          weight: 0.5,
          bloomLevel: 'Analyze',
          orderIndex: 2,
        },
      ],
    },
    ...overrides,
  }
}

const activeClos = [
  {
    id: CLO1_UUID,
    courseId: COURSE_UUID,
    cloCode: 'CLO1',
    description: 'Áp dụng Clean Architecture',
    bloomLevel: 'Apply' as const,
    orderIndex: 1,
    isActive: true,
    createdAt: '2026-09-01T00:00:00Z',
  },
  {
    id: CLO2_UUID,
    courseId: COURSE_UUID,
    cloCode: 'CLO2',
    description: 'Thiết kế REST API chuẩn RFC 7807',
    bloomLevel: 'Analyze' as const,
    orderIndex: 2,
    isActive: true,
    createdAt: '2026-09-01T00:00:00Z',
  },
]

describe('EditQuestionDraftDialog — MF-05 SELECTED_CLOS CLO guard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(questionService.updateDraft).mockResolvedValue({
      questionId: 'question-1',
      rubricId: 'rubric-1',
      approvalStatus: 'DRAFT',
    } as never)
  })

  afterEach(cleanup)

  it('does not offer a null "Chưa gắn CLO" option when active CLOs exist and keeps a valid CLO selected', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)
    const onSaved = vi.fn()

    render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({ cloId: CLO1_UUID, cloCode: 'CLO1' })}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )

    const cloTrigger = await screen.findByRole('button', { name: 'Chọn chuẩn đầu ra CLO' })
    await waitFor(() => expect(cloTrigger).toBeEnabled())
    expect(cloTrigger).toHaveTextContent('CLO1 · Áp dụng Clean Architecture')

    fireEvent.click(cloTrigger)
    expect(await screen.findByRole('option', { name: /CLO1/ })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: /CLO2/ })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'Chưa gắn CLO' })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('option', { name: /CLO2/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    await waitFor(() =>
      expect(questionService.updateDraft).toHaveBeenCalledWith(
        'question-1',
        expect.objectContaining({
          cloId: CLO2_UUID,
        }),
      ),
    )
    expect(onSaved).toHaveBeenCalledWith('question-1')
  })

  it('blocks saving a question with cloId=null when the course has active CLOs until a valid CLO is selected', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)
    const onSaved = vi.fn()

    render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({ cloId: null, cloCode: null })}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )

    const cloTrigger = await screen.findByRole('button', { name: 'Chọn chuẩn đầu ra CLO' })
    await waitFor(() => expect(cloTrigger).toBeEnabled())

    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))
    expect(
      await screen.findByText('Vui lòng chọn chuẩn đầu ra (CLO) đang hoạt động của môn học.'),
    ).toBeInTheDocument()
    expect(questionService.updateDraft).not.toHaveBeenCalled()

    fireEvent.click(cloTrigger)
    fireEvent.click(await screen.findByRole('option', { name: /CLO1/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    await waitFor(() =>
      expect(questionService.updateDraft).toHaveBeenCalledWith(
        'question-1',
        expect.objectContaining({
          cloId: CLO1_UUID,
        }),
      ),
    )
    expect(onSaved).toHaveBeenCalledWith('question-1')
  })

  it('still permits saving when the course has no active CLOs configured', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue([])
    const onSaved = vi.fn()

    render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({ cloId: null, cloCode: null })}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    await waitFor(() =>
      expect(questionService.updateDraft).toHaveBeenCalledWith(
        'question-1',
        expect.objectContaining({
          cloId: null,
        }),
      ),
    )
    expect(onSaved).toHaveBeenCalledWith('question-1')
  })

  it('uploads an image and description for manual questions and forwards them to updateDraft', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)
    vi.mocked(questionService.uploadQuestionImage).mockResolvedValue({
      imageUrl: '/uploads/questions/202611/diagram.png',
      objectKey: 'questions/202611/diagram.png',
      fileName: 'diagram.png',
      contentType: 'image/png',
      sizeBytes: 1024,
    })
    const onSaved = vi.fn()

    const { container } = render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({ source: 'manual', imageUrl: null, imageDescription: null })}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeEnabled(),
    )

    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement
    const file = new File(['png-bytes'], 'diagram.png', { type: 'image/png' })
    fireEvent.change(fileInput, { target: { files: [file] } })

    await waitFor(() =>
      expect(questionService.uploadQuestionImage).toHaveBeenCalledWith(file, ''),
    )

    const descInput = await screen.findByLabelText('Mô tả ảnh')
    fireEvent.change(descInput, { target: { value: 'Sơ đồ kiến trúc 4 tầng' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    await waitFor(() =>
      expect(questionService.updateDraft).toHaveBeenCalledWith(
        'question-1',
        expect.objectContaining({
          imageUrl: '/uploads/questions/202611/diagram.png',
          imageDescription: 'Sơ đồ kiến trúc 4 tầng',
          clearImage: false,
        }),
      ),
    )
    expect(onSaved).toHaveBeenCalledWith('question-1')
  })

  it('allows clearing only imageDescription ("") while preserving imageUrl, and clearing the whole image with clearImage=true', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)
    const onSaved = vi.fn()

    const { rerender } = render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({
          source: 'manual',
          imageUrl: '/uploads/questions/202611/existing.png',
          imageDescription: 'Mô tả cũ',
        })}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeEnabled(),
    )
    const descInput = screen.getByLabelText('Mô tả ảnh')
    expect(descInput).toHaveValue('Mô tả cũ')

    fireEvent.change(descInput, { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    await waitFor(() =>
      expect(questionService.updateDraft).toHaveBeenCalledWith(
        'question-1',
        expect.objectContaining({
          imageUrl: '/uploads/questions/202611/existing.png',
          imageDescription: '',
          clearImage: false,
        }),
      ),
    )

    vi.clearAllMocks()
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)
    vi.mocked(questionService.updateDraft).mockResolvedValue({ questionId: 'question-2' } as never)

    rerender(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({
          id: 'question-2',
          source: 'manual',
          imageUrl: '/uploads/questions/202611/existing.png',
          imageDescription: 'Mô tả cũ',
        })}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Xóa ảnh' })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Xóa ảnh' }))
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    await waitFor(() =>
      expect(questionService.updateDraft).toHaveBeenCalledWith(
        'question-2',
        expect.objectContaining({
          imageUrl: null,
          imageDescription: null,
          clearImage: true,
        }),
      ),
    )
  })

  it('disables image upload for non-manual questions and allows clearing legacy stale images via clearImage=true', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)
    const onSaved = vi.fn()

    const { container } = render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({
          source: 'flm_api',
          imageUrl: '/uploads/questions/202611/legacy.png',
          imageDescription: 'Legacy AI image',
        })}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeEnabled(),
    )
    expect(container.querySelector('input[type="file"]')).toBeNull()
    expect(screen.queryByRole('button', { name: /Thêm ảnh|Thay ảnh/ })).toBeNull()
    expect(
      screen.getByText('Chỉ câu hỏi tạo thủ công mới hỗ trợ đính kèm hình ảnh.'),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))
    expect(
      await screen.findByText(
        'Chỉ câu hỏi tạo thủ công mới hỗ trợ đính kèm hình ảnh. Vui lòng bấm "Bỏ ảnh" trước khi lưu.',
      ),
    ).toBeInTheDocument()
    expect(questionService.updateDraft).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Bỏ ảnh' }))
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    await waitFor(() =>
      expect(questionService.updateDraft).toHaveBeenCalledWith(
        'question-1',
        expect.objectContaining({
          imageUrl: null,
          imageDescription: null,
          clearImage: true,
        }),
      ),
    )
  })

  it('validates rubric name <= 200, criterion name <= 200, score <= 10 and 2-decimal precision, and imageDescription <= 2000', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)

    render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({
          source: 'manual',
          imageUrl: '/uploads/questions/202611/existing.png',
          imageDescription: 'a'.repeat(2001),
          rubric: {
            id: 'rubric-1',
            name: 'R'.repeat(201),
            description: null,
            totalMaxScore: 10,
            criteria: [
              {
                id: 'crit-1',
                criterionName: 'C'.repeat(201),
                description: '',
                maxScore: 3.333,
                weight: 0.3333,
                bloomLevel: 'Understand',
                orderIndex: 1,
              },
              {
                id: 'crit-2',
                criterionName: 'Tiêu chí 2',
                description: '',
                maxScore: 6.667,
                weight: 0.6667,
                bloomLevel: 'Analyze',
                orderIndex: 2,
              },
            ],
          },
        })}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeEnabled(),
    )
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    expect(
      await screen.findByText('Mô tả ảnh không được vượt quá 2000 ký tự.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Tên Barem Rubric không được vượt quá 200 ký tự.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Tên tiêu chí không được vượt quá 200 ký tự.'),
    ).toBeInTheDocument()
    expect(
      screen.getAllByText('Điểm tiêu chí chỉ hỗ trợ tối đa 2 chữ số thập phân.'),
    ).toHaveLength(2)
    expect(questionService.updateDraft).not.toHaveBeenCalled()
  })

  it('shows "Bỏ ảnh" recovery when a non-manual question has orphan imageDescription without imageUrl', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)
    const onSaved = vi.fn()

    render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion({
          source: 'flm_api',
          imageUrl: null,
          imageDescription: 'Mô tả ảnh bị sót lại từ nguồn AI',
        })}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeEnabled(),
    )
    const clearButton = screen.getByRole('button', { name: 'Bỏ ảnh' })
    expect(clearButton).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))
    expect(
      await screen.findByText(
        'Chỉ câu hỏi tạo thủ công mới hỗ trợ đính kèm hình ảnh. Vui lòng bấm "Bỏ ảnh" trước khi lưu.',
      ),
    ).toBeInTheDocument()
    expect(questionService.updateDraft).not.toHaveBeenCalled()

    fireEvent.click(clearButton)
    expect(screen.queryByRole('button', { name: 'Bỏ ảnh' })).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))
    await waitFor(() =>
      expect(questionService.updateDraft).toHaveBeenCalledWith(
        'question-1',
        expect.objectContaining({
          imageUrl: null,
          imageDescription: null,
          clearImage: true,
        }),
      ),
    )
    expect(onSaved).toHaveBeenCalledWith('question-1')
  })

  it('rejects 1e-9 (< 0.01) and 5.000000001 criterion scores so maxScore=0 is never submitted', async () => {
    vi.mocked(courseCloService.list).mockResolvedValue(activeClos)

    render(
      <EditQuestionDraftDialog
        open
        question={makeQuestion()}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    )

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeEnabled(),
    )

    const scoreInputs = screen.getAllByLabelText('Điểm tối đa')
    fireEvent.change(scoreInputs[0], { target: { value: '1e-9' } })
    fireEvent.change(scoreInputs[1], { target: { value: '10' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    expect(await screen.findByText('Điểm tiêu chí phải từ 0.01 đến 10.')).toBeInTheDocument()
    expect(questionService.updateDraft).not.toHaveBeenCalled()

    fireEvent.change(scoreInputs[0], { target: { value: '5' } })
    fireEvent.change(scoreInputs[1], { target: { value: '5.000000001' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    expect(
      await screen.findByText('Điểm tiêu chí chỉ hỗ trợ tối đa 2 chữ số thập phân.'),
    ).toBeInTheDocument()
    expect(questionService.updateDraft).not.toHaveBeenCalled()
  })
})
