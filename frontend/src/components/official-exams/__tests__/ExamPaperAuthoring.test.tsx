import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ExamPaperAuthoringDialog } from '../ExamPaperAuthoringDialog'
import { PaperBankDialog } from '../PaperBankDialog'
import { ExamAuthoringDetailPage } from '@/pages/lecturer/ExamAuthoringDetailPage'
import { initialSlots, paperReady } from '@/lib/examPaperAuthoring'
import { questionService } from '@/services/question.service'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { useExamPreparationDetail } from '@/hooks/useExamPreparationDetail'
import type { CourseClo } from '@/types/courseClo.types'
import type {
  ExamAuthoringAssignment,
  ExamBlueprint,
  ExamPaper,
  ExamPreparation,
  PaperQuestion,
} from '@/types/examPreparation.types'
import type { QuestionDetail, QuestionDifficulty, QuestionSummary } from '@/types/rubric.types'
vi.mock('@/services/examPreparation.service', () => ({
  examPreparationService: {
    createPaper: vi.fn(),
    savePaper: vi.fn(),
    createQuestion: vi.fn(),
    submitAssignment: vi.fn(),
    duplicates: vi.fn(),
    history: vi.fn(),
  },
}))
vi.mock('@/services/question.service', () => ({
  questionService: {
    getQuestions: vi.fn(),
    getQuestionById: vi.fn(),
    generateFromClo: vi.fn(),
    uploadQuestionImage: vi.fn(),
  },
}))
vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: 'teacher', role: 'Instructor', fullName: 'Giảng viên' } }),
}))
vi.mock('@/hooks/useExamPreparationDetail', () => ({ useExamPreparationDetail: vi.fn() }))
const answer = 'Đáp án mẫu đầy đủ, giải thích khái niệm và đưa ra ví dụ thực tế rõ ràng.'
const clos: CourseClo[] = [
  {
    id: 'clo',
    courseId: 'course',
    cloCode: 'CLO1',
    description: 'Vận dụng kiến thức',
    bloomLevel: 'Apply',
    orderIndex: 1,
    isActive: true,
    createdAt: '2026-01-01',
  },
]
const blueprint: ExamBlueprint = {
  id: 'structure',
  name: 'Cấu trúc',
  totalQuestions: 2,
  durationMinutes: 30,
  easyCount: 1,
  mediumCount: 1,
  hardCount: 0,
  cloIds: ['clo'],
}
const assignment: ExamAuthoringAssignment = {
  id: 'assignment',
  lecturerId: 'teacher',
  lecturerName: 'Giảng viên',
  structureId: 'structure',
  setCount: 2,
  deadline: '2099-12-31T10:00:00Z',
  notes: '',
  assignedAt: '2026-11-01',
  blueprint,
  draft: 2,
  pending: 0,
  needsRevision: 0,
  rejected: 0,
  approved: 0,
  handedOver: 0,
}
const preparation: ExamPreparation = {
  id: 'prep',
  sessionId: 'session',
  title: 'Kỳ thi',
  courseId: 'course',
  courseCode: 'PRN231',
  semesterId: 'semester',
  semesterCode: 'FA26',
  examStructureId: 'structure',
  status: 'IN_PROGRESS',
  requiredSetCount: 2,
  approvedSetCount: 0,
  handedOverSetCount: 0,
  deadline: '2099-12-31T10:00:00Z',
  notes: '',
  requestedAt: '2026-11-01',
  handedOverAt: null,
  assignments: [assignment],
}
function item(id: string, difficulty: QuestionDifficulty = 'easy', bank = false): PaperQuestion {
  return {
    questionId: id,
    sourceQuestionId: 'source-' + id,
    orderIndex: 1,
    title: 'Câu ' + id,
    content: 'Nội dung câu ' + id,
    sampleAnswer: answer,
    cloId: 'clo',
    bloomLevel: 'Apply',
    difficulty,
    source: 'manual',
    isBankQuestion: bank,
    imageUrl: null,
    imageDescription: null,
    rubricName: 'Rubric ' + id,
    totalMaxScore: 10,
    criteria: [
      {
        name: 'Giải thích',
        description: 'Đầy đủ',
        maxScore: 6,
        weight: 0.6,
        bloomLevel: 'Apply',
        orderIndex: 1,
      },
      {
        name: 'Ví dụ',
        description: 'Chính xác',
        maxScore: 4,
        weight: 0.4,
        bloomLevel: 'Apply',
        orderIndex: 2,
      },
    ],
  }
}
function paper(id = 'paper', questions = [item('one'), item('two', 'medium')]): ExamPaper {
  return {
    id,
    assignmentId: 'assignment',
    structureId: 'structure',
    courseId: 'course',
    sessionId: 'session',
    createdBy: 'teacher',
    setCode: 'DE-' + id,
    status: 'DRAFT',
    reviewNotes: null,
    submittedAt: null,
    approvedAt: null,
    handedOverAt: null,
    generatedAt: '2026-11-01',
    isActive: true,
    questions,
  }
}
function detail(id: string, difficulty: QuestionDifficulty = 'easy'): QuestionDetail {
  return {
    id,
    courseId: 'course',
    courseCode: 'PRN231',
    rubricId: 'rubric',
    title: 'Ngân hàng ' + id,
    content: 'Nội dung ngân hàng ' + id,
    sampleAnswer: answer,
    keyPoints: [],
    difficulty,
    bloomLevel: 'Apply',
    source: 'manual',
    approvalStatus: 'APPROVED',
    submittedBy: 'other',
    submittedByName: 'Khác',
    approvedBy: 'head',
    approvedByName: 'Trưởng bộ môn',
    reviewNotes: null,
    isActive: true,
    createdAt: '2026-11-01',
    cloId: 'clo',
    rubric: {
      id: 'rubric',
      name: 'Rubric ' + id,
      totalMaxScore: 10,
      description: null,
      criteria: [
        {
          id: 'r1',
          criterionName: 'Giải thích',
          description: '',
          maxScore: 6,
          weight: 0.6,
          bloomLevel: 'Apply',
          orderIndex: 1,
        },
        {
          id: 'r2',
          criterionName: 'Ví dụ',
          description: '',
          maxScore: 4,
          weight: 0.4,
          bloomLevel: 'Apply',
          orderIndex: 2,
        },
      ],
    },
  }
}
function summary(id: string, difficulty: QuestionDifficulty = 'easy'): QuestionSummary {
  return { ...detail(id, difficulty), totalRubricScore: 10 }
}
const onClose = vi.fn(),
  onSaved = vi.fn()
function mount(p?: ExamPaper) {
  render(
    <ExamPaperAuthoringDialog
      preparation={preparation}
      assignment={assignment}
      blueprint={blueprint}
      clos={clos}
      paper={p}
      onClose={onClose}
      onSaved={onSaved}
    />,
  )
}
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(service.createPaper).mockResolvedValue(paper())
  vi.mocked(service.createQuestion).mockResolvedValue({ questionId: 'created' })
  vi.mocked(service.savePaper).mockResolvedValue({})
  vi.mocked(service.submitAssignment).mockResolvedValue({ submittedCount: 2 })
  vi.mocked(service.history).mockResolvedValue([])
  vi.mocked(questionService.getQuestions).mockResolvedValue({
    items: [
      summary('bank1'),
      summary('bank2'),
      summary('medium', 'medium'),
      summary('hard', 'hard'),
      { ...summary('outside'), cloId: 'outside' },
    ],
    totalCount: 5,
    pageNumber: 1,
    pageSize: 100,
    totalPages: 1,
  })
  vi.mocked(questionService.getQuestionById).mockImplementation(async (id) =>
    detail(id, id === 'medium' ? 'medium' : 'easy'),
  )
})
afterEach(cleanup)
describe('Soạn bộ đề với các tab cố định', () => {
  it('orders tabs by difficulty and permits multiple questions on the same CLO', () => {
    const p = paper()
    expect(paperReady(p, blueprint, clos)).toBe(true)
    expect(initialSlots(blueprint, clos, p).map((s) => s.difficulty)).toEqual(['easy', 'medium'])
    mount()
    expect(screen.getAllByRole('tab').map((t) => t.textContent)).toEqual(
      expect.arrayContaining([
        expect.stringContaining('Dễ'),
        expect.stringContaining('Trung bình'),
      ]),
    )
    expect(screen.queryByRole('button', { name: 'Độ khó' })).toBeNull()
  })
  it('preserves typed content when switching tabs and keeps two-step rubric state', () => {
    mount()
    fireEvent.change(screen.getByLabelText('Tiêu đề'), {
      target: { value: 'Câu thủ công đang soạn' },
    })
    fireEvent.click(screen.getByRole('tab', { name: /Câu 2/ }))
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('')
    fireEvent.click(screen.getByRole('tab', { name: /Câu 1/ }))
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Câu thủ công đang soạn')
    fireEvent.click(screen.getByRole('button', { name: 'Tiếp tục' }))
    expect(screen.getByText('Vui lòng nhập nội dung câu hỏi.')).toBeTruthy()
  })
  it('opens saved bank questions as read-only including the rubric', () => {
    mount(paper('bank', [item('one', 'easy', true)]))
    expect(screen.getByLabelText('Tiêu đề')).toHaveAttribute('readonly')
    expect(screen.getByLabelText('Nội dung câu hỏi')).toHaveAttribute('readonly')
    fireEvent.click(screen.getByRole('button', { name: 'Barem rubric' }))
    expect(screen.getByLabelText('Tên rubric')).toHaveAttribute('readonly')
    expect(screen.queryByRole('button', { name: 'Thêm tiêu chí' })).toBeNull()
  })
  it('keeps ineligible bank questions visible and lets a full difficulty be selected for replacement', async () => {
    const apply = vi.fn()
    render(
      <PaperBankDialog
        courseId="course"
        blueprint={blueprint}
        clos={clos}
        slots={initialSlots(blueprint, clos, paper())}
        onClose={onClose}
        onApply={apply}
      />,
    )
    const first = await screen.findByRole('checkbox', { name: 'Chọn Ngân hàng bank1' })
    expect(first).not.toBeDisabled()
    expect(screen.getByRole('checkbox', { name: 'Chọn Ngân hàng hard' })).toBeDisabled()
    expect(screen.getByRole('checkbox', { name: 'Chọn Ngân hàng outside' })).toBeDisabled()
    fireEvent.click(first)
    expect(screen.getByRole('checkbox', { name: 'Chọn Ngân hàng bank2' })).toBeDisabled()
    fireEvent.click(first)
    expect(screen.getByRole('checkbox', { name: 'Chọn Ngân hàng bank2' })).not.toBeDisabled()
  })
  it('asks before replacing manual content and does not mutate it when canceled', async () => {
    mount(paper())
    fireEvent.click(screen.getByRole('button', { name: 'Chọn từ ngân hàng' }))
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Chọn Ngân hàng bank1' }))
    fireEvent.click(screen.getByRole('button', { name: 'Đưa vào đề' }))
    const confirmation = await screen.findByRole('dialog', { name: 'Xác nhận thay câu hỏi' })
    expect(within(confirmation).getByText('Sẽ thay: Câu one')).toBeTruthy()
    fireEvent.click(within(confirmation).getByRole('button', { name: 'Giữ câu hiện tại' }))
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Câu one')
  })
  it('applies multiple bank questions to matching slots with the same CLO and saves their source IDs', async () => {
    mount()
    fireEvent.change(screen.getByLabelText('Mã đề'), { target: { value: 'NEW' } })
    fireEvent.click(screen.getByRole('button', { name: 'Chọn từ ngân hàng' }))
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Chọn Ngân hàng bank1' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Chọn Ngân hàng medium' }))
    fireEvent.click(screen.getByRole('button', { name: 'Đưa vào đề' }))
    await waitFor(() => expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Ngân hàng bank1'))
    expect(screen.getByLabelText('Tiêu đề')).toHaveAttribute('readonly')
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))
    await waitFor(() =>
      expect(service.savePaper).toHaveBeenCalledWith('paper', ['bank1', 'medium']),
    )
    expect(service.createQuestion).not.toHaveBeenCalled()
    expect(onClose).toHaveBeenCalledOnce()
  })
  it('generates only empty slots and preserves the manual question', async () => {
    const ai = detail('generated', 'medium')
    vi.mocked(questionService.generateFromClo).mockResolvedValue({
      courseId: 'course',
      courseCode: 'PRN231',
      source: 'flm_api',
      generatedCount: 1,
      questions: [
        {
          tempId: 'ai',
          cloCode: 'CLO1',
          title: ai.title,
          content: ai.content,
          modelAnswer: answer,
          keyPoints: [],
          difficulty: 'medium',
          bloomLevel: 'Apply',
          source: 'flm_api',
          usageScope: 'exam',
          hasFollowUp: false,
          followUpPrompt: null,
          rubric: {
            name: 'Rubric AI',
            totalMaxScore: 10,
            criteria: ai.rubric.criteria.map((c) => ({
              ...c,
              description: '',
              bloomLevel: 'Apply',
            })),
          },
        },
      ],
    })
    mount(paper('partial', [item('manual')]))
    fireEvent.click(screen.getByRole('button', { name: 'AI tạo câu còn thiếu (1)' }))
    fireEvent.click(screen.getByRole('button', { name: 'Tạo 1 câu hỏi' }))
    await waitFor(() =>
      expect(questionService.generateFromClo).toHaveBeenCalledWith(
        expect.objectContaining({
          numberOfQuestions: 1,
          usageScope: 'exam',
          difficultyDistribution: { easy: 0, medium: 1, hard: 0 },
        }),
      ),
    )
    await waitFor(() => expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Ngân hàng generated'))
    expect(screen.getByLabelText('Tiêu đề')).not.toHaveAttribute('readonly')
    fireEvent.click(screen.getByRole('tab', { name: /Câu 1/ }))
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Câu manual')
  })
  it('does not close or discard the form if saving fails', async () => {
    vi.mocked(service.savePaper).mockRejectedValueOnce(new Error('Không thể lưu'))
    mount(paper())
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Không thể lưu')
    expect(onClose).not.toHaveBeenCalled()
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Câu one')
  })
  it('warns before discarding unsaved edits', () => {
    mount()
    fireEvent.change(screen.getByLabelText('Tiêu đề'), { target: { value: 'Chưa lưu' } })
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }))
    expect(screen.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })).toBeTruthy()
    expect(onClose).not.toHaveBeenCalled()
  })
  it('only enables batch submission when complete drafts plus submitted papers meet the assigned count', () => {
    const data = {
      preparation,
      session: { id: 'session', status: 'scheduled' },
      course: { id: 'course', name: 'Môn học' },
      papers: [paper('one'), paper('two', [item('partial')])],
      clos,
    }
    vi.mocked(useExamPreparationDetail).mockReturnValue({
      data: data as NonNullable<ReturnType<typeof useExamPreparationDetail>['data']>,
      loading: false,
      error: undefined,
      refresh: vi.fn(),
      updatePaper: vi.fn(),
    })
    render(
      <MemoryRouter initialEntries={['/session/assignment']}>
        <Routes>
          <Route path="/:sessionId/:assignmentId" element={<ExamAuthoringDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )
    expect(screen.getByRole('button', { name: 'Gửi duyệt (1)' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Tạo bộ đề' })).not.toBeDisabled()
  })
})
