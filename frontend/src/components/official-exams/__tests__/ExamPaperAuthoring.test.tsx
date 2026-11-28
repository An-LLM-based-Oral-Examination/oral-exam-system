import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ExamPaperAuthoringDialog } from '../ExamPaperAuthoringDialog'
import { PaperBankDialog } from '../PaperBankDialog'
import { ExamAuthoringDetailPage } from '@/pages/lecturer/ExamAuthoringDetailPage'
import { initialSlots, paperReady, questionErrors } from '@/lib/examPaperAuthoring'
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
    archiveDraftPaper: vi.fn(),
    createQuestion: vi.fn(),
    submitAssignment: vi.fn(),
    duplicates: vi.fn(),
    availableQuestions: vi.fn(),
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
  vi.mocked(service.availableQuestions).mockResolvedValue(
    ['bank1', 'bank2', 'medium', 'hard', 'outside'].map((id) => ({
      questionId: id,
      sourceQuestionId: id,
      usedCount: 0,
      maxUses: 1,
      remainingUses: 1,
      canSelect: true,
      disabledReason: null,
    })),
  )
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
  it('validates each rubric criterion and reports errors beside fields after blur', () => {
    mount()
    fireEvent.change(screen.getByLabelText('Tiêu đề'), { target: { value: 'Câu hỏi hợp lệ' } })
    fireEvent.change(screen.getByLabelText('Nội dung câu hỏi'), {
      target: { value: 'Giải thích kiến trúc phần mềm' },
    })
    fireEvent.change(screen.getByLabelText('Model Answer'), { target: { value: answer } })
    fireEvent.click(screen.getByRole('button', { name: 'Barem' }))
    const rubric = screen.getByLabelText('Tên rubric')
    fireEvent.blur(rubric)
    expect(screen.getByText('Vui lòng nhập tên rubric.')).toBeTruthy()
    const name = screen.getByLabelText('Tiêu chí 1')
    fireEvent.blur(name)
    expect(screen.getByText('Nhập tên tiêu chí.')).toBeTruthy()
    const score = screen.getAllByLabelText('Điểm')[0]
    fireEvent.change(score, { target: { value: '11' } })
    fireEvent.blur(score)
    expect(screen.getByText('Điểm phải từ 0.1 đến 10.')).toBeTruthy()
    expect(screen.getByText('Tổng điểm các tiêu chí phải bằng chính xác 10.0.')).toBeTruthy()
    expect(score).toHaveAttribute('aria-invalid', 'true')
    const draft = initialSlots(blueprint, clos)[0].question
    const errors = questionErrors(draft, clos)
    expect(errors['criterion-0-name']).toBe('Nhập tên tiêu chí.')
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
    fireEvent.click(screen.getByRole('button', { name: 'Barem' }))
    expect(screen.getByText('Vui lòng nhập nội dung câu hỏi.')).toBeTruthy()
    expect(screen.queryByLabelText('Tên rubric')).toBeNull()
    fireEvent.change(screen.getByLabelText('Nội dung câu hỏi'), {
      target: { value: 'Giải thích kiến trúc phần mềm' },
    })
    fireEvent.change(screen.getByLabelText('Model Answer'), { target: { value: answer } })
    fireEvent.click(screen.getByRole('button', { name: 'Barem' }))
    expect(screen.getByLabelText('Tên rubric')).toBeTruthy()
    fireEvent.click(screen.getAllByRole('button', { name: /^Nội dung$/ }).at(-1)!)
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Câu thủ công đang soạn')
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
    fireEvent.click(screen.getByRole('button', { name: 'Ngân hàng câu hỏi' }))
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Chọn Ngân hàng bank1' }))
    fireEvent.click(screen.getByRole('button', { name: 'Đưa vào đề' }))
    const confirmation = await screen.findByRole('dialog', { name: 'Xác nhận thay câu hỏi' })
    expect(service.availableQuestions).toHaveBeenCalledWith(blueprint.id, 'session', undefined, paper().id)
    expect(within(confirmation).getByText('Sẽ thay: Câu one')).toBeTruthy()
    fireEvent.click(within(confirmation).getByRole('button', { name: 'Giữ câu hiện tại' }))
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('Câu one')
  })
  it('keeps exhausted bank questions visible but disabled with their usage count', async () => {
    vi.mocked(service.availableQuestions).mockResolvedValue([
      {
        questionId: 'bank1',
        sourceQuestionId: 'bank1',
        usedCount: 3,
        maxUses: 3,
        remainingUses: 0,
        canSelect: false,
        disabledReason: 'Đã dùng 3/3 đề trong kỳ thi.',
      },
      {
        questionId: 'bank2',
        sourceQuestionId: 'bank2',
        usedCount: 1,
        maxUses: 3,
        remainingUses: 2,
        canSelect: true,
        disabledReason: null,
      },
    ])
    render(
      <PaperBankDialog
        courseId="course"
        sessionId="session"
        blueprint={blueprint}
        clos={clos}
        slots={initialSlots(blueprint, clos)}
        onClose={onClose}
        onApply={vi.fn()}
      />,
    )
    expect(await screen.findByRole('checkbox', { name: 'Chọn Ngân hàng bank1' })).toBeDisabled()
    expect(screen.getByText('Đã dùng 3/3 đề trong kỳ thi.')).toBeTruthy()
    expect(screen.getByRole('checkbox', { name: 'Chọn Ngân hàng bank2' })).not.toBeDisabled()
  })
  it('applies multiple bank questions to matching slots with the same CLO and saves their source IDs', async () => {
    mount()
    fireEvent.change(screen.getByLabelText('Mã đề'), { target: { value: 'NEW' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ngân hàng câu hỏi' }))
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
    fireEvent.click(screen.getByRole('button', { name: 'Tạo bằng AI (1)' }))
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
  it('selects only complete drafts in the draft tab and submits the chosen batch', async () => {
    const refresh = vi.fn()
    vi.mocked(useExamPreparationDetail).mockReturnValue({
      data: {
        preparation,
        session: { id: 'session', status: 'scheduled' },
        course: { id: 'course', name: 'Môn học' },
        papers: [paper('one'), paper('two'), paper('partial', [item('partial')])],
        clos,
      } as NonNullable<ReturnType<typeof useExamPreparationDetail>['data']>,
      loading: false,
      error: undefined,
      refresh,
      updatePaper: vi.fn(),
    })
    vi.mocked(service.submitAssignment).mockResolvedValue({ submittedCount: 2 })
    render(
      <MemoryRouter initialEntries={['/session/assignment']}>
        <Routes>
          <Route path="/:sessionId/:assignmentId" element={<ExamAuthoringDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )
    expect(screen.queryByRole('checkbox')).toBeNull()
    fireEvent.click(screen.getByRole('tab', { name: /Bản nháp/ }))
    expect(screen.getByRole('checkbox', { name: 'Chọn bộ đề DE-partial' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Gửi duyệt (0)' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Chọn tất cả' }))
    expect(screen.getByRole('checkbox', { name: 'Chọn bộ đề DE-one' })).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Gửi duyệt (2)' }))
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận gửi' }))
    await waitFor(() =>
      expect(service.submitAssignment).toHaveBeenCalledWith('assignment', ['one', 'two']),
    )
    await waitFor(() => expect(refresh).toHaveBeenCalled())
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

  it('shows anonymous yellow pending warning in PaperBankDialog without leaking author or set code', async () => {
    vi.mocked(service.availableQuestions).mockResolvedValue([
      {
        questionId: 'bank1',
        sourceQuestionId: 'bank1',
        usedCount: 1,
        maxUses: 2,
        remainingUses: 1,
        canSelect: true,
        disabledReason: null,
        isYellowPendingByOtherLecturer: true,
        yellowWarningLabel: 'Đã có người chọn, đang chờ duyệt',
        yellowThresholdPercent: 70,
      },
    ])
    render(
      <PaperBankDialog
        courseId="course"
        sessionId="session"
        blueprint={blueprint}
        clos={clos}
        slots={[]}
        onClose={vi.fn()}
        onApply={vi.fn()}
      />,
    )
    expect(await screen.findByText('Đã có người chọn, đang chờ duyệt')).toBeTruthy()
    expect(screen.queryByText(/DE-/)).toBeNull()
  })

  it('marks newly created manual paper question as isPaperPrivate=true inside ExamPaperAuthoringDialog', async () => {
    const onSavedLocal = vi.fn()
    vi.mocked(service.createQuestion).mockResolvedValue({ questionId: 'created-priv' })
    render(
      <ExamPaperAuthoringDialog
        preparation={preparation}
        assignment={assignment}
        blueprint={blueprint}
        clos={clos}
        paper={paper('p-edit', [item('one')])}
        onClose={vi.fn()}
        onSaved={onSavedLocal}
      />,
    )
    fireEvent.click(screen.getByRole('tab', { name: /Câu 2/ }))
    fireEvent.change(screen.getByLabelText('Tiêu đề'), { target: { value: 'Câu tạo tay riêng' } })
    fireEvent.change(screen.getByLabelText('Nội dung câu hỏi'), {
      target: { value: 'Trình bày cơ chế khóa lạc quan và kiểm soát đồng thời trong PostgreSQL.' },
    })
    fireEvent.change(screen.getByLabelText('Model Answer'), { target: { value: answer } })
    fireEvent.click(screen.getByRole('button', { name: 'Barem' }))
    fireEvent.change(screen.getByLabelText('Tên rubric'), { target: { value: 'Rubric riêng' } })
    fireEvent.change(screen.getByLabelText('Tiêu chí 1'), { target: { value: 'Khái niệm' } })
    fireEvent.change(screen.getByLabelText('Tiêu chí 2'), { target: { value: 'Ví dụ' } })
    const scoreInputs = screen.getAllByLabelText('Điểm')
    fireEvent.change(scoreInputs[0], { target: { value: '5' } })
    fireEvent.change(scoreInputs[1], { target: { value: '5' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))
    await waitFor(() =>
      expect(service.createQuestion).toHaveBeenCalledWith(
        expect.objectContaining({ isPaperPrivate: true }),
      ),
    )
  })

  it('shows read-only sample paper viewer and archives draft paper with confirmation in ExamAuthoringDetailPage', async () => {
    const refresh = vi.fn()
    const samplePaper: ExamPaper = {
      ...paper('sample-1', [item('s-one')]),
      assignmentId: null,
      setCode: 'MAU-PRN231',
      isSampleSet: true,
    }
    vi.mocked(service.archiveDraftPaper).mockResolvedValue({ archived: true })
    vi.mocked(useExamPreparationDetail).mockReturnValue({
      data: {
        preparation,
        session: { id: 'session', status: 'scheduled' },
        course: { id: 'course', name: 'Môn học' },
        papers: [samplePaper, paper('one')],
        clos,
      } as NonNullable<ReturnType<typeof useExamPreparationDetail>['data']>,
      loading: false,
      error: undefined,
      refresh,
      updatePaper: vi.fn(),
    })
    render(
      <MemoryRouter initialEntries={['/session/assignment']}>
        <Routes>
          <Route path="/:sessionId/:assignmentId" element={<ExamAuthoringDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )
    expect(screen.queryByRole('button', { name: 'Xem bộ đề MAU-PRN231' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Xem đề mẫu tham khảo' }))
    const sampleDialog = await screen.findByRole('dialog', { name: /Đề mẫu: MAU-PRN231/ })
    expect(within(sampleDialog).queryByRole('button', { name: 'Xóa đề mẫu' })).toBeNull()
    fireEvent.click(within(sampleDialog).getAllByRole('button', { name: 'Đóng' }).at(-1)!)

    fireEvent.click(screen.getByRole('button', { name: 'Xóa nháp' }))
    const dialog = await screen.findByRole('dialog', { name: 'Xóa bản nháp bộ đề' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận xóa nháp' }))
    await waitFor(() => expect(service.archiveDraftPaper).toHaveBeenCalledWith('one'))
    await waitFor(() => expect(refresh).toHaveBeenCalled())
  })

  it('passes bankReadyOnly=true and filters PaperBankDialog items to availableQuestions before pagination when sessionId is set', async () => {
    vi.mocked(service.availableQuestions).mockResolvedValue([
      {
        questionId: 'bank1',
        sourceQuestionId: 'bank1',
        usedCount: 0,
        maxUses: 2,
        remainingUses: 2,
        canSelect: true,
        disabledReason: null,
      },
    ])

    render(
      <PaperBankDialog
        courseId="course"
        sessionId="session"
        blueprint={blueprint}
        clos={clos}
        slots={initialSlots(blueprint, clos)}
        onClose={vi.fn()}
        onApply={vi.fn()}
      />,
    )

    expect(await screen.findByRole('checkbox', { name: 'Chọn Ngân hàng bank1' })).toBeInTheDocument()
    expect(questionService.getQuestions).toHaveBeenCalledWith(
      expect.objectContaining({
        courseId: 'course',
        usageScope: 'exam',
        approvalStatus: 'APPROVED',
        bankReadyOnly: true,
      }),
    )
    expect(screen.queryByRole('checkbox', { name: 'Chọn Ngân hàng bank2' })).toBeNull()
    expect(screen.queryByRole('checkbox', { name: 'Chọn Ngân hàng outside' })).toBeNull()
  })

  it('shows live >70% yellow overlap amber warning banner in ExamPaperAuthoringDialog and ExamAuthoringDetailPage before submit', async () => {
    vi.mocked(service.availableQuestions).mockResolvedValue([
      {
        questionId: 'one',
        sourceQuestionId: 'source-one',
        usedCount: 1,
        maxUses: 2,
        remainingUses: 1,
        canSelect: true,
        disabledReason: null,
        isYellowPendingByOtherLecturer: true,
        yellowWarningLabel: 'Đã có người chọn, đang chờ duyệt',
      },
      {
        questionId: 'two',
        sourceQuestionId: 'source-two',
        usedCount: 1,
        maxUses: 2,
        remainingUses: 1,
        canSelect: true,
        disabledReason: null,
        isYellowPendingByOtherLecturer: true,
        yellowWarningLabel: 'Đã có người chọn, đang chờ duyệt',
      },
    ])

    const { unmount } = render(
      <ExamPaperAuthoringDialog
        preparation={preparation}
        assignment={assignment}
        blueprint={blueprint}
        clos={clos}
        paper={paper('p-yellow', [item('one', 'easy', true), item('two', 'medium', true)])}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    )

    expect(await screen.findByText(/Vượt ngưỡng trùng chờ duyệt \(> 70%\)/)).toBeInTheDocument()
    expect(screen.getByText(/2\/2 câu \(100%\)/)).toBeInTheDocument()
    unmount()

    vi.mocked(useExamPreparationDetail).mockReturnValue({
      data: {
        preparation,
        session: { id: 'session', status: 'scheduled' },
        course: { id: 'course', name: 'Môn học' },
        papers: [
          paper('one', [item('one', 'easy', true), item('two', 'medium', true)]),
          paper('two', [item('three', 'easy', false), item('four', 'medium', false)]),
        ],
        clos,
      } as NonNullable<ReturnType<typeof useExamPreparationDetail>['data']>,
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

    expect(
      await screen.findByText(/Cảnh báo vượt ngưỡng 70% câu hỏi trùng chờ duyệt:/),
    ).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Gửi duyệt (2)' }))
    const submitDialog = await screen.findByRole('dialog', { name: 'Gửi bộ đề duyệt' })
    const yellowAlert = within(submitDialog).getByRole('alert')
    expect(
      within(yellowAlert).getByText(/Cảnh báo vượt ngưỡng trùng chờ duyệt \(> 70%\)/),
    ).toBeInTheDocument()
    expect(within(yellowAlert).getByText(/DE-one/)).toBeInTheDocument()
    expect(service.submitAssignment).not.toHaveBeenCalled()
  })

  it('guards ExamPaperAuthoringDialog when blueprint.totalQuestions is 0 and clears stale availableUsage in ExamAuthoringDetailPage when no drafts remain', async () => {
    const emptyBlueprint = {
      ...blueprint,
      totalQuestions: 0,
      easyCount: 0,
      mediumCount: 0,
      hardCount: 0,
    }
    const { unmount } = render(
      <ExamPaperAuthoringDialog
        preparation={preparation}
        assignment={assignment}
        blueprint={emptyBlueprint}
        clos={clos}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    )

    expect(
      await screen.findByText(/Cấu trúc bộ đề hiện có 0 câu hỏi/i),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Ngân hàng câu hỏi' })).toBeDisabled()
    unmount()

    vi.mocked(service.availableQuestions).mockResolvedValue([
      {
        questionId: 'one',
        sourceQuestionId: 'source-one',
        usedCount: 1,
        maxUses: 2,
        remainingUses: 1,
        canSelect: true,
        disabledReason: null,
        isYellowPendingByOtherLecturer: true,
        yellowWarningLabel: 'Đã có người chọn, đang chờ duyệt',
      },
      {
        questionId: 'two',
        sourceQuestionId: 'source-two',
        usedCount: 1,
        maxUses: 2,
        remainingUses: 1,
        canSelect: true,
        disabledReason: null,
        isYellowPendingByOtherLecturer: true,
        yellowWarningLabel: 'Đã có người chọn, đang chờ duyệt',
      },
    ])

    vi.mocked(useExamPreparationDetail).mockReturnValue({
      data: {
        preparation,
        session: { id: 'session', status: 'scheduled' },
        course: { id: 'course', name: 'Môn học' },
        papers: [
          paper('one', [item('one', 'easy', true), item('two', 'medium', true)]),
        ],
        clos,
      } as NonNullable<ReturnType<typeof useExamPreparationDetail>['data']>,
      loading: false,
      error: undefined,
      refresh: vi.fn(),
      updatePaper: vi.fn(),
    })

    const view = render(
      <MemoryRouter initialEntries={['/session/assignment']}>
        <Routes>
          <Route path="/:sessionId/:assignmentId" element={<ExamAuthoringDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(
      await screen.findByText(/Cảnh báo vượt ngưỡng 70% câu hỏi trùng chờ duyệt:/),
    ).toBeInTheDocument()

    vi.mocked(useExamPreparationDetail).mockReturnValue({
      data: {
        preparation,
        session: { id: 'session', status: 'scheduled' },
        course: { id: 'course', name: 'Môn học' },
        papers: [
          {
            ...paper('one', [item('one', 'easy', true), item('two', 'medium', true)]),
            status: 'SUBMITTED_FOR_REVIEW',
          },
        ],
        clos,
      } as NonNullable<ReturnType<typeof useExamPreparationDetail>['data']>,
      loading: false,
      error: undefined,
      refresh: vi.fn(),
      updatePaper: vi.fn(),
    })

    view.rerender(
      <MemoryRouter initialEntries={['/session/assignment']}>
        <Routes>
          <Route path="/:sessionId/:assignmentId" element={<ExamAuthoringDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(
      screen.queryByText(/Cảnh báo vượt ngưỡng 70% câu hỏi trùng chờ duyệt:/),
    ).not.toBeInTheDocument()
  })

  it('defensively filters inactive, PENDING_CURATION, and EXCLUDED_FROM_BANK questions client-side in PaperBankDialog', async () => {
    vi.mocked(questionService.getQuestions).mockResolvedValue({
      items: [
        { ...summary('bank1'), isActive: true, bankCurationStatus: 'BANK_READY' },
        { ...summary('bank2'), isActive: true, bankCurationStatus: 'CURATED_INTO_BANK' },
        { ...summary('medium', 'medium'), isActive: false, bankCurationStatus: 'BANK_READY' },
        { ...summary('hard', 'hard'), isActive: true, bankCurationStatus: 'PENDING_CURATION' },
        { ...summary('outside'), cloId: 'clo', isActive: true, bankCurationStatus: 'EXCLUDED_FROM_BANK' },
      ],
      totalCount: 5,
      pageNumber: 1,
      pageSize: 100,
      totalPages: 1,
    })

    render(
      <PaperBankDialog
        courseId="course"
        blueprint={blueprint}
        clos={clos}
        slots={initialSlots(blueprint, clos)}
        onClose={vi.fn()}
        onApply={vi.fn()}
      />,
    )

    expect(await screen.findByRole('checkbox', { name: 'Chọn Ngân hàng bank1' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Chọn Ngân hàng bank2' })).toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: 'Chọn Ngân hàng medium' })).toBeNull()
    expect(screen.queryByRole('checkbox', { name: 'Chọn Ngân hàng hard' })).toBeNull()
    expect(screen.queryByRole('checkbox', { name: 'Chọn Ngân hàng outside' })).toBeNull()
  })

  it('hides image controls for AI slots, blocks stale AI images until cleared, and sends null imageUrl with source=flm_api', async () => {
    const aiItem: PaperQuestion = {
      ...item('ai-stale', 'easy', false),
      source: 'flm_api',
      imageUrl: '/uploads/questions/202611/stale.png',
      imageDescription: 'Stale AI image',
    }
    vi.mocked(service.createQuestion).mockResolvedValue({ questionId: 'saved-ai-clean' })

    render(
      <ExamPaperAuthoringDialog
        preparation={preparation}
        assignment={assignment}
        blueprint={blueprint}
        clos={clos}
        paper={paper('p-ai-img', [aiItem, item('two', 'medium', false)])}
        onClose={onClose}
        onSaved={onSaved}
      />,
    )

    expect(
      screen.getByText('Câu hỏi nguồn AI không hỗ trợ đính kèm hình ảnh.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Thêm ảnh|Đổi ảnh/ })).toBeNull()
    expect(screen.queryByLabelText('Mô tả ảnh')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Barem' }))
    expect(
      await screen.findByText(
        'Câu hỏi nguồn AI không hỗ trợ đính kèm hình ảnh. Vui lòng bấm "Bỏ ảnh" trước khi tiếp tục.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Tên rubric')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Bỏ ảnh' }))
    expect(screen.queryByRole('button', { name: 'Bỏ ảnh' })).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))
    await waitFor(() =>
      expect(service.createQuestion).toHaveBeenCalledWith(
        expect.objectContaining({
          source: 'flm_api',
          imageUrl: null,
          imageDescription: null,
          isPaperPrivate: true,
        }),
      ),
    )
  })

  it('allows manual slots to upload and remove images and forwards manual imageUrl and isPaperPrivate=true on save', async () => {
    vi.mocked(questionService.uploadQuestionImage).mockResolvedValue({
      imageUrl: '/uploads/questions/202611/manual-slot.png',
      objectKey: 'questions/202611/manual-slot.png',
      fileName: 'manual-slot.png',
      contentType: 'image/png',
      sizeBytes: 2048,
    })
    vi.mocked(service.createQuestion).mockResolvedValue({ questionId: 'created-img' })

    const { container } = render(
      <ExamPaperAuthoringDialog
        preparation={preparation}
        assignment={assignment}
        blueprint={blueprint}
        clos={clos}
        paper={paper('p-manual-img', [item('one')])}
        onClose={onClose}
        onSaved={onSaved}
      />,
    )

    fireEvent.click(screen.getByRole('tab', { name: /Câu 2/ }))
    fireEvent.change(screen.getByLabelText('Tiêu đề'), { target: { value: 'Câu có sơ đồ' } })
    fireEvent.change(screen.getByLabelText('Nội dung câu hỏi'), {
      target: { value: 'Phân tích sơ đồ tuần tự hệ thống thi vấn đáp.' },
    })
    fireEvent.change(screen.getByLabelText('Model Answer'), { target: { value: answer } })

    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement
    const file = new File(['img'], 'manual-slot.png', { type: 'image/png' })
    fireEvent.change(fileInput, { target: { files: [file] } })

    await waitFor(() => expect(questionService.uploadQuestionImage).toHaveBeenCalledWith(file))
    const descField = await screen.findByLabelText('Mô tả ảnh')
    fireEvent.change(descField, { target: { value: 'Sơ đồ tuần tự' } })

    fireEvent.click(screen.getByRole('button', { name: 'Bỏ ảnh' }))
    expect(screen.queryByLabelText('Mô tả ảnh')).toBeNull()

    const fileInputReupload = container.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(fileInputReupload, { target: { files: [file] } })
    const descFieldReupload = await screen.findByLabelText('Mô tả ảnh')
    fireEvent.change(descFieldReupload, { target: { value: 'Sơ đồ tuần tự mới' } })

    fireEvent.click(screen.getByRole('button', { name: 'Barem' }))
    fireEvent.change(screen.getByLabelText('Tên rubric'), { target: { value: 'Rubric sơ đồ' } })
    fireEvent.change(screen.getByLabelText('Tiêu chí 1'), { target: { value: 'Luồng chính' } })
    fireEvent.change(screen.getByLabelText('Tiêu chí 2'), { target: { value: 'Xử lý lỗi' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu bản nháp' }))

    await waitFor(() =>
      expect(service.createQuestion).toHaveBeenCalledWith(
        expect.objectContaining({
          source: 'manual',
          imageUrl: '/uploads/questions/202611/manual-slot.png',
          imageDescription: 'Sơ đồ tuần tự mới',
          isPaperPrivate: true,
        }),
      ),
    )
  })

  it('rejects rubric name > 200, criterion name > 200, and maxScore > 2 decimal places in questionErrors', () => {
    const draft = initialSlots(blueprint, clos)[0].question
    draft.title = 'Câu hợp lệ'
    draft.content = 'Nội dung hợp lệ'
    draft.sampleAnswer = answer
    draft.rubric.name = 'R'.repeat(201)
    draft.rubric.criteria[0].criterionName = 'C'.repeat(201)
    draft.rubric.criteria[0].maxScore = 3.333
    draft.rubric.criteria[1].criterionName = 'Tiêu chí 2'
    draft.rubric.criteria[1].maxScore = 6.667

    const errors = questionErrors(draft, clos)
    expect(errors.rubricName).toBe('Tên rubric tối đa 200 ký tự.')
    expect(errors['criterion-0-name']).toBe('Tên tiêu chí tối đa 200 ký tự.')
    expect(errors['criterion-0-score']).toBe('Điểm chỉ hỗ trợ tối đa 2 chữ số thập phân.')
    expect(errors['criterion-1-score']).toBe('Điểm chỉ hỗ trợ tối đa 2 chữ số thập phân.')
  })

  it('resets touchedFields blur errors when clearing a slot at the same index in ExamPaperAuthoringDialog', async () => {
    mount()
    const titleInput = screen.getByLabelText('Tiêu đề')
    fireEvent.blur(titleInput)
    expect(screen.getByText('Nhập tiêu đề, tối đa 300 ký tự.')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Nội dung câu hỏi'), {
      target: { value: 'Nội dung tạm thời để hiện nút xóa' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Xóa' }))
    const confirmDialog = await screen.findByRole('dialog', { name: 'Bỏ câu khỏi đề?' })
    fireEvent.click(within(confirmDialog).getByRole('button', { name: 'Bỏ câu' }))

    await waitFor(() => expect(screen.getByLabelText('Tiêu đề')).toHaveValue(''))
    expect(screen.queryByText('Nhập tiêu đề, tối đa 300 ký tự.')).toBeNull()
  })
})
