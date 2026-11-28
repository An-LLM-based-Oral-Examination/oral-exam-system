import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { ExamPreparationPage } from '../ExamPreparationPage'
import { ExamPreparationDetailPage } from '../ExamPreparationDetailPage'
import {
  ExamBlueprintDialog,
  ExamAssignmentDialog,
} from '@/components/official-exams/ExamPreparationDialogs'
import type { ExamBlueprint, ExamPaper, ExamPreparation } from '@/types/examPreparation.types'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { officialExamService } from '@/services/officialExam.service'
import { classService } from '@/services/class.service'
import { courseCloService } from '@/services/courseClo.service'
import { lecturerAssignmentService } from '@/services/lecturerAssignment.service'
const auth = vi.hoisted(() => ({ role: 'DepartmentHead' }))
vi.mock('@/hooks/useAuth', () => ({ useAuth: () => ({ user: { id: 'head', role: auth.role } }) }))
vi.mock('@/services/examPreparation.service', () => ({
  examPreparationService: {
    list: vi.fn(),
    papers: vi.fn(),
    blueprint: vi.fn(),
    duplicates: vi.fn(),
    similarity: vi.fn(),
    capacity: vi.fn(),
    updateReuse: vi.fn(),
    promoteQuestions: vi.fn(),
    bankCandidates: vi.fn(),
    history: vi.fn(),
    decide: vi.fn(),
    configure: vi.fn(),
    assign: vi.fn(),
    assignBatch: vi.fn(),
    handOver: vi.fn(),
    createSamplePaper: vi.fn(),
    deleteSamplePaper: vi.fn(),
    getBankCurationCandidates: vi.fn(),
    decideBankCuration: vi.fn(),
  },
}))
vi.mock('@/services/question.service', () => ({
  questionService: {
    getQuestions: vi.fn(),
  },
}))
vi.mock('@/services/officialExam.service', () => ({
  officialExamService: {
    getSessions: vi.fn(),
    configureFollowUp: vi.fn(),
    getExamStructures: vi.fn(),
  },
}))
vi.mock('@/services/course.service', () => ({
  courseService: {
    getLecturerCourses: vi.fn(),
  },
}))
vi.mock('@/services/class.service', () => ({ classService: { listSemesters: vi.fn() } }))
vi.mock('@/services/courseClo.service', () => ({
  courseCloService: { getManagedCourses: vi.fn(), list: vi.fn() },
}))
vi.mock('@/services/lecturerAssignment.service', () => ({
  lecturerAssignmentService: { offerings: vi.fn(), assignments: vi.fn() },
}))
const base = '/department-head/exam-structures'
const blueprint: ExamBlueprint = {
  id: 'structure',
  name: 'Cấu trúc FE',
  totalQuestions: 3,
  durationMinutes: 30,
  easyCount: 1,
  mediumCount: 1,
  hardCount: 1,
  cloIds: ['clo'],
}
const clos = [
  {
    id: 'clo',
    courseId: 'course',
    cloCode: 'CLO1',
    description: 'Phân tích thuật toán',
    bloomLevel: 'Analyze' as const,
    orderIndex: 1,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
  },
]
const preparation: ExamPreparation = {
  id: 'prep',
  sessionId: 'session',
  title: 'Kỳ thi PRN231 FE',
  courseId: 'course',
  courseCode: 'PRN231',
  semesterId: 'term',
  semesterCode: 'FA26',
  examStructureId: 'structure',
  status: 'IN_PROGRESS',
  requiredSetCount: 2,
  approvedSetCount: 0,
  handedOverSetCount: 0,
  deadline: '2099-12-31T12:00:00Z',
  notes: 'Chuẩn bị đủ đề',
  requestedAt: '2026-11-20T00:00:00Z',
  handedOverAt: null,
  assignments: [
    {
      id: 'assignment',
      lecturerId: 'lecturer',
      lecturerName: 'Nguyễn An',
      structureId: 'structure',
      setCount: 2,
      deadline: '2099-12-30T12:00:00Z',
      notes: '',
      assignedAt: '2026-11-20T00:00:00Z',
      blueprint,
      draft: 0,
      pending: 2,
      needsRevision: 0,
      rejected: 0,
      approved: 0,
      handedOver: 0,
    },
  ],
}
function makePaper(id: string): ExamPaper {
  return {
    id,
    assignmentId: 'assignment',
    structureId: 'structure',
    courseId: 'course',
    sessionId: 'session',
    createdBy: 'lecturer',
    setCode: `DE-${id}`,
    status: 'SUBMITTED_FOR_REVIEW',
    reviewNotes: null,
    submittedAt: '2026-11-23T08:00:00Z',
    approvedAt: null,
    handedOverAt: null,
    generatedAt: '2026-11-22T08:00:00Z',
    isActive: true,
    questions: [1, 2, 3].map((index) => ({
      questionId: `${id}-${index}`,
      sourceQuestionId: `source-${index}`,
      orderIndex: index,
      title: `Tiêu đề câu ${index}`,
      content: `Nội dung câu ${index}`,
      bloomLevel: 'Apply',
      difficulty: 'medium',
      cloId: 'clo',
      sampleAnswer: `Đáp án câu ${index}`,
      imageUrl: null,
      imageDescription: null,
      rubricName: `Rubric câu ${index}`,
      totalMaxScore: 10,
      criteria: [
        {
          name: `Tiêu chí ${index}`,
          description: 'Giải thích đầy đủ',
          maxScore: 10,
          weight: 1,
          bloomLevel: 'Apply',
          orderIndex: 1,
        },
      ],
    })),
  }
}
let stored: ExamPaper[]
function Location() {
  const location = useLocation()
  return <output data-testid="location">{location.pathname}</output>
}
function mount(path = `${base}/session`) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Location />
      <Routes>
        <Route path={base} element={<ExamPreparationPage />} />
        <Route path={`${base}/:sessionId`} element={<ExamPreparationDetailPage />} />
      </Routes>
    </MemoryRouter>,
  )
}
beforeEach(() => {
  vi.resetAllMocks()
  auth.role = 'DepartmentHead'
  stored = [makePaper('01'), makePaper('02')]
  vi.mocked(service.list).mockImplementation(async () => [structuredClone(preparation)])
  vi.mocked(service.papers).mockImplementation(async () => structuredClone(stored))
  vi.mocked(service.blueprint).mockResolvedValue(blueprint)
  vi.mocked(service.similarity).mockImplementation(async (id) => ({
    examSetId: id,
    similarityPercent: 0,
    comparedPaper: null,
    questions: (stored.find((p) => p.id === id)?.questions ?? []).map((q) => ({
      questionId: q.questionId,
      orderIndex: q.orderIndex,
      percent: 0,
      band: 'LOW',
      matches: [],
    })),
  }))
  vi.mocked(service.capacity).mockResolvedValue({
    requiredSetCount: 2,
    reusePercent: 30,
    maxUses: 1,
    band: 'BALANCED',
    additionalQuestionsRequired: 0,
    suggestedReusePercent: 10,
    warning: null,
  })
  vi.mocked(service.updateReuse).mockResolvedValue({})
  vi.mocked(service.promoteQuestions).mockResolvedValue({})
  vi.mocked(service.bankCandidates).mockResolvedValue([])
  vi.mocked(service.history).mockResolvedValue([])
  vi.mocked(service.configure).mockResolvedValue({ structureId: 'new-structure' })
  vi.mocked(service.assign).mockResolvedValue({ id: 'new-assignment' })
  vi.mocked(service.handOver).mockResolvedValue({})
  vi.mocked(service.decide).mockImplementation(async (id, decision, notes) => {
    stored = stored.map((p) =>
      p.id === id
        ? {
            ...p,
            status:
              decision === 'approve'
                ? 'OFFICIAL'
                : decision === 'reject'
                  ? 'REJECTED'
                  : 'NEEDS_REVISION',
            reviewNotes: notes || null,
          }
        : p,
    )
    return {}
  })
  vi.mocked(officialExamService.getSessions).mockResolvedValue([
    {
      id: 'session',
      courseId: 'course',
      semesterId: 'term',
      title: preparation.title,
      examDate: '2099-12-31',
      examAttemptType: 'FE',
      hasFollowUp: false,
      maxFollowUpQuestions: 2,
      examInputMode: 'VoiceWithTranscriptEdit',
      transcriptBufferSeconds: 60,
      status: 'scheduled',
    },
  ])
  vi.mocked(classService.listSemesters).mockResolvedValue([
    {
      id: 'term',
      code: 'FA26',
      name: 'Fall 2026',
      startDate: '2026-01-01',
      endDate: '2099-12-31',
      isActive: true,
    },
  ])
  vi.mocked(courseCloService.getManagedCourses).mockResolvedValue([
    { id: 'course', code: 'PRN231', name: 'Backend Development' },
  ])
  vi.mocked(courseCloService.list).mockResolvedValue(clos)
  vi.mocked(lecturerAssignmentService.offerings).mockResolvedValue([
    {
      courseOfferingId: 'offering',
      courseId: 'course',
      courseCode: 'PRN231',
      courseName: 'Backend',
      semesterId: 'term',
      semesterCode: 'FA26',
      isOfferingEnabled: true,
    },
  ])
  vi.mocked(lecturerAssignmentService.assignments).mockResolvedValue([])
})
afterEach(cleanup)

describe('Chuẩn bị đề thi trưởng bộ môn', () => {
  it('excludes drafts from all papers and ignores a stale draft-tab URL', async () => {
    stored.push({ ...makePaper('DRAFT'), status: 'DRAFT' })
    mount(`${base}/session?paperStatus=DRAFT`)
    expect(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' })).toBeTruthy()
    expect(screen.queryByRole('tab', { name: /Bản nháp/ })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Xem bộ đề DE-DRAFT' })).toBeNull()
    const all = screen.getByRole('tab', { name: /^Tất cả/ })
    expect(all).toHaveAttribute('aria-selected', 'true')
    expect(within(all).getByText('2')).toBeTruthy()
  })

  it('defaults to current semester and links the request to its detail', async () => {
    mount(base)
    const row = await screen.findByRole('link', { name: /Kỳ thi PRN231 FE/ })
    expect(row).toHaveAttribute('href', `${base}/session`)
    expect(screen.getByRole('button', { name: 'Học kỳ' })).toHaveTextContent('Fall 2026')
    fireEvent.click(row)
    expect(await screen.findByRole('heading', { name: 'Danh sách bộ đề' })).toBeTruthy()
  })
  it.each([base, `${base}/session`])(
    'does not fetch preparation data for a foreign role: %s',
    async (path) => {
      auth.role = 'Student'
      mount(path)
      expect(screen.getByRole('alert')).toHaveTextContent('không có quyền')
      expect(service.list).not.toHaveBeenCalled()
      expect(officialExamService.getSessions).not.toHaveBeenCalled()
    },
  )
  it('distinguishes API errors from empty state', async () => {
    vi.mocked(service.list).mockRejectedValueOnce(new Error('Không kết nối được máy chủ'))
    mount(base)
    expect(await screen.findByRole('alert')).toHaveTextContent('Không kết nối')
    expect(screen.queryByText('Chưa có yêu cầu chuẩn bị đề')).toBeNull()
  })
  it('shows a shared empty state when no request exists', async () => {
    vi.mocked(service.list).mockResolvedValue([])
    mount(base)
    expect(await screen.findByText('Chưa có yêu cầu chuẩn bị đề')).toBeTruthy()
  })
  it('reviews every question in one drawer without changing the page URL', async () => {
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    const drawer = await screen.findByRole('dialog', { name: 'Xem và duyệt bộ đề' })
    expect(within(drawer).getByText('Nội dung câu 1')).toBeTruthy()
    fireEvent.click(within(drawer).getByRole('tab', { name: /^Câu 2/ }))
    expect(within(drawer).getByText('Nội dung câu 2')).toBeTruthy()
    expect(within(drawer).getByText('Đáp án câu 2')).toBeTruthy()
    expect(within(drawer).getByText('Rubric câu 2')).toBeTruthy()
    expect(screen.getByTestId('location')).toHaveTextContent(`${base}/session`)
  })
  it('advances to the next pending set and closes after approving the last one', async () => {
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    const approve = await screen.findByRole('button', { name: 'Duyệt bộ đề' })
    await waitFor(() => expect(approve).not.toBeDisabled())
    fireEvent.click(approve)
    await waitFor(() => expect(service.decide).toHaveBeenCalledWith('01', 'approve', ''))
    expect(
      await within(screen.getByRole('dialog', { name: 'Xem và duyệt bộ đề' })).findByRole(
        'heading',
        { name: 'DE-02' },
      ),
    ).toBeTruthy()
    expect(service.decide).toHaveBeenCalledTimes(1)
    const lastApprove = screen.getByRole('button', { name: 'Duyệt bộ đề' })
    await waitFor(() => expect(lastApprove).not.toBeDisabled())
    fireEvent.click(lastApprove)
    await waitFor(() => {
      expect(service.decide).toHaveBeenCalledWith('02', 'approve', '')
      expect(screen.queryByRole('dialog', { name: 'Xem và duyệt bộ đề' })).toBeNull()
    })
  })
  it('keeps the current paper pending when approval fails', async () => {
    vi.mocked(service.decide).mockRejectedValueOnce(new Error('Bộ đề đã được thay đổi'))
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    const drawer = screen.getByRole('dialog', { name: 'Xem và duyệt bộ đề' })
    const approve = within(drawer).getByRole('button', { name: 'Duyệt bộ đề' })
    await waitFor(() => expect(approve).not.toBeDisabled())
    fireEvent.click(approve)
    expect(await within(drawer).findByRole('alert')).toHaveTextContent('Bộ đề đã được thay đổi')
    expect(within(drawer).getByRole('heading', { name: 'DE-01' })).toBeTruthy()
    expect(within(drawer).getByText('Chờ duyệt')).toBeTruthy()
  })
  it('requires a successful duplicate check before approving and allows retry', async () => {
    vi.mocked(service.similarity).mockRejectedValueOnce(new Error('Mất kết nối'))
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    const drawer = screen.getByRole('dialog', { name: 'Xem và duyệt bộ đề' })
    expect(await within(drawer).findByRole('alert')).toHaveTextContent('Chưa đối chiếu được bộ đề')
    expect(within(drawer).getByRole('button', { name: 'Duyệt bộ đề' })).toBeDisabled()
    fireEvent.click(within(drawer).getByRole('button', { name: 'Thử lại' }))
    await waitFor(() =>
      expect(within(drawer).getByRole('button', { name: 'Duyệt bộ đề' })).not.toBeDisabled(),
    )
  })
  it('requires revision notes and preserves the open modal on API failure', async () => {
    vi.mocked(service.decide).mockRejectedValueOnce(new Error('Máy chủ chưa xử lý được'))
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Yêu cầu sửa' }))
    const form = screen.getByRole('dialog', { name: 'Yêu cầu chỉnh sửa' })
    fireEvent.submit(form.querySelector('form')!)
    expect(await within(form).findByText('Vui lòng nhập phản hồi cho giảng viên.')).toBeTruthy()
    expect(service.decide).not.toHaveBeenCalled()
    fireEvent.change(within(form).getByLabelText('Phản hồi cho giảng viên'), {
      target: { value: 'Làm rõ tiêu chí chấm' },
    })
    fireEvent.submit(form.querySelector('form')!)
    expect(await within(form).findByRole('alert')).toHaveTextContent('Máy chủ chưa xử lý được')
    expect(within(form).getByLabelText('Phản hồi cho giảng viên')).toHaveValue(
      'Làm rõ tiêu chí chấm',
    )
  })
  it('shows duplicates on the source question but does not block approval', async () => {
    vi.mocked(service.similarity).mockResolvedValue({
      examSetId: '01',
      similarityPercent: 80,
      comparedPaper: {
        examSetId: 'old',
        setCode: 'DE-CU',
        sessionId: 'old-session',
        sessionTitle: 'Thi lại kỳ trước',
        semesterCode: 'SU26',
        questionCount: 3,
      },
      questions: [
        {
          questionId: '01-2',
          orderIndex: 2,
          percent: 100,
          band: 'VERY_HIGH',
          matches: [
            {
              questionId: 'old-q',
              examSetId: 'old',
              setCode: 'DE-CU',
              sessionTitle: 'Thi lại kỳ trước',
              semesterCode: 'SU26',
              title: 'Câu cũ',
              content: 'Nội dung đối chiếu',
              percent: 100,
              sameSource: true,
              orderIndex: 1,
            },
          ],
        },
      ],
    })
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    fireEvent.click(await screen.findByRole('tab', { name: /^Câu 2/ }))
    expect(await screen.findByText('Tương đồng cao · DE-CU')).toBeTruthy()
    fireEvent.click(screen.getByText('Xem câu đối chiếu'))
    expect(screen.getByText('Nội dung đối chiếu')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Duyệt bộ đề' })).not.toBeDisabled()
  })
  it('shows only the overall percentage below 70 and hides comparison details', async () => {
    vi.mocked(service.similarity).mockResolvedValue({
      examSetId: '01',
      similarityPercent: 69,
      comparedPaper: {
        examSetId: 'old',
        setCode: 'HIDDEN-OLD',
        sessionId: 'old',
        sessionTitle: 'Kỳ thi trước',
        semesterCode: 'SU26',
        questionCount: 3,
      },
      questions: [],
    })
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    expect(await screen.findByText('69%')).toBeTruthy()
    expect(screen.queryByText('Xem câu đối chiếu')).toBeNull()
    expect(screen.queryByText('HIDDEN-OLD')).toBeNull()
  })
  it('keeps the approved paper open for bank selection before advancing to the next paper', async () => {
    vi.mocked(service.decide).mockImplementationOnce(async (id) => {
      stored = stored.map((p) => (p.id === id ? { ...p, status: 'OFFICIAL' } : p))
      return {
        bankCandidates: [
          {
            questionId: '01-1',
            sourceQuestionId: 'source-1',
            title: 'Câu mới ít tương đồng',
            similarityPercent: 20,
            band: 'LOW',
            recommendation: 'CAN_ADD',
            comparedQuestionId: null,
            comparedTitle: null,
            comparedContent: null,
          },
        ],
      }
    })
    vi.mocked(service.promoteQuestions).mockRejectedValueOnce(
      new Error('Không thể thêm, vui lòng thử lại'),
    )
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    const approve = screen.getByRole('button', { name: 'Duyệt bộ đề' })
    await waitFor(() => expect(approve).not.toBeDisabled())
    fireEvent.click(approve)
    const popup = await screen.findByRole('dialog', { name: 'Thêm câu vào ngân hàng' })
    expect(
      within(screen.getByRole('dialog', { name: 'Xem và duyệt bộ đề' })).getByRole('heading', {
        name: 'DE-01',
      }),
    ).toBeTruthy()
    expect(within(popup).getByRole('checkbox')).toBeChecked()
    fireEvent.click(within(popup).getByRole('button', { name: 'Thêm vào ngân hàng' }))
    expect(await within(popup).findByRole('alert')).toHaveTextContent('Không thể thêm')
    expect(service.decide).toHaveBeenCalledTimes(1)
    fireEvent.click(within(popup).getByRole('button', { name: 'Thêm vào ngân hàng' }))
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Thêm câu vào ngân hàng' })).toBeNull(),
    )
    expect(service.promoteQuestions).toHaveBeenCalledWith('01', ['01-1'])
    expect(await screen.findByRole('heading', { name: 'DE-02' })).toBeTruthy()
  })
  it('retains the last shortage while a new reuse ratio is being checked', async () => {
    const warning = 'Ngân hàng hiện thiếu ít nhất 2 câu hỏi để đáp ứng cấu hình.'
    vi.mocked(service.capacity)
      .mockResolvedValueOnce({
        requiredSetCount: 2,
        reusePercent: 30,
        maxUses: 1,
        band: 'BALANCED',
        additionalQuestionsRequired: 2,
        suggestedReusePercent: 50,
        warning,
      })
      .mockImplementationOnce(() => new Promise(() => {}))
    render(
      <ExamBlueprintDialog
        sessionId="session"
        current={blueprint}
        clos={clos}
        locked={false}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        reusePercent={30}
        requiredSetCount={2}
      />,
    )
    expect(await screen.findByText(warning)).toBeTruthy()
    fireEvent.change(screen.getByRole('slider', { name: 'Tỷ lệ tái sử dụng thanh trượt' }), {
      target: { value: '50' },
    })
    expect(screen.getByText(warning)).toBeTruthy()
    expect(
      screen.getByRole('button', { name: 'Dùng tỷ lệ 50% để đáp ứng ngân hàng hiện tại' }),
    ).toBeDisabled()
    await waitFor(() => expect(service.capacity).toHaveBeenCalledTimes(2))
    expect(screen.getByText(warning)).toBeTruthy()
    expect(screen.getByLabelText('Đang cập nhật kết quả kiểm tra')).toBeTruthy()
  })
  it('previews a general shortage and changes reuse policy without creating a blueprint', async () => {
    vi.mocked(service.capacity).mockResolvedValue({
      requiredSetCount: 2,
      reusePercent: 40,
      maxUses: 1,
      band: 'HIGH_REUSE',
      additionalQuestionsRequired: 5,
      suggestedReusePercent: null,
      warning: 'Ngân hàng hiện thiếu ít nhất 5 câu hỏi để đáp ứng cấu hình.',
    })
    render(
      <ExamBlueprintDialog
        sessionId="session"
        current={blueprint}
        clos={clos}
        locked={false}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        reusePercent={30}
        requiredSetCount={2}
      />,
    )
    fireEvent.change(screen.getByRole('slider', { name: 'Tỷ lệ tái sử dụng thanh trượt' }), {
      target: { value: '40' },
    })
    expect(
      await screen.findByText('Ngân hàng hiện thiếu ít nhất 5 câu hỏi để đáp ứng cấu hình.'),
    ).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }))
    await waitFor(() => expect(service.updateReuse).toHaveBeenCalledWith('session', 40))
    expect(service.configure).not.toHaveBeenCalled()
  })
  it('does not show old feedback on a pending paper and disables editing after handover', async () => {
    stored[0].reviewNotes = 'Phản hồi cũ'
    vi.mocked(service.list).mockResolvedValue([
      { ...preparation, status: 'HANDED_OVER', handedOverAt: '2026-11-24T00:00:00Z' },
    ])
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    expect(screen.queryByText('Phản hồi cũ')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Duyệt bộ đề' })).toBeNull()
    expect(screen.queryByRole('button', { name: /^Bàn giao/ })).toBeNull()
  })
  it('counts only approved papers under the current blueprint toward delivery', async () => {
    stored[0].status = 'OFFICIAL'
    stored[0].structureId = 'old'
    stored[1].status = 'OFFICIAL'
    mount()
    expect(await screen.findByRole('button', { name: 'Bàn giao (1/2)' })).toBeDisabled()
    expect(screen.getByText('Cấu trúc cũ')).toBeTruthy()
  })
  it('requires enough selected approved sets before confirming delivery', async () => {
    stored.forEach((p) => {
      p.status = 'OFFICIAL'
    })
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Bàn giao (2/2)' }))
    const dialog = screen.getByRole('dialog', { name: 'Bàn giao đề thi' })
    const boxes = within(dialog).getAllByRole('checkbox')
    fireEvent.click(boxes[0])
    expect(within(dialog).getByRole('button', { name: 'Xác nhận bàn giao' })).toBeDisabled()
    fireEvent.click(boxes[0])
    fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận bàn giao' }))
    await waitFor(() =>
      expect(service.handOver).toHaveBeenCalledWith(
        'session',
        expect.arrayContaining(['01', '02']),
      ),
    )
  })
  it('saves follow-up settings from the blueprint form and blocks invalid counts', async () => {
    const saved = vi.fn()
    const session = (await officialExamService.getSessions())[0]
    render(
      <ExamBlueprintDialog
        sessionId="session"
        session={session}
        current={blueprint}
        clos={clos}
        locked={false}
        onClose={vi.fn()}
        onSaved={saved}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Tạo phiên bản mới' }))
    fireEvent.click(screen.getByRole('switch', { name: 'Câu hỏi phụ' }))
    fireEvent.change(screen.getByLabelText('Số câu phụ tối đa'), { target: { value: '6' } })
    fireEvent.submit(document.getElementById('exam-blueprint')!)
    expect(screen.getByText('Số câu hỏi phụ phải từ 1 đến 5.')).toBeTruthy()
    expect(officialExamService.configureFollowUp).not.toHaveBeenCalled()
    expect(service.configure).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText('Số câu phụ tối đa'), { target: { value: '3' } })
    fireEvent.submit(document.getElementById('exam-blueprint')!)
    await waitFor(() => expect(saved).toHaveBeenCalled())
    expect(officialExamService.configureFollowUp).toHaveBeenCalledWith('session', {
      hasFollowUp: true,
      maxFollowUpQuestions: 3,
    })
    expect(service.configure).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({
        totalQuestions: 3,
        easyCount: 1,
        mediumCount: 1,
        hardCount: 1,
      }),
    )
  })
  it('validates difficulty distribution inline instead of submitting an invalid blueprint', async () => {
    render(
      <ExamBlueprintDialog
        sessionId="session"
        current={blueprint}
        clos={clos}
        locked={false}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Tạo phiên bản mới' }))
    for (const key of ['easyCount', 'mediumCount', 'hardCount'])
      fireEvent.change(document.getElementById('blueprint-' + key)!, { target: { value: '0' } })
    fireEvent.submit(
      screen.getByRole('button', { name: 'Lưu cấu trúc' }).closest('form') ??
        document.getElementById('exam-blueprint')!,
    )
    expect(await screen.findByText('Tổng số câu phải là số nguyên dương hợp lệ.')).toBeTruthy()
    expect(service.configure).not.toHaveBeenCalled()
  })
  it('assigns multiple lecturers with shared deadline and individual quotas in one request', async () => {
    vi.mocked(lecturerAssignmentService.assignments).mockResolvedValue([
      {
        lecturerId: 'teacher-a',
        fullName: 'Teacher A',
        email: 'a@test.local',
        isActive: true,
        semesterId: 'term',
        semesterCode: 'FA26',
        courseOfferingId: 'offering',
      },
      {
        lecturerId: 'teacher-b',
        fullName: 'Teacher B',
        email: 'b@test.local',
        isActive: true,
        semesterId: 'term',
        semesterCode: 'FA26',
        courseOfferingId: 'offering',
      },
    ])
    const onSaved = vi.fn()
    render(
      <ExamAssignmentDialog
        preparation={preparation}
        offeringId="offering"
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    )
    fireEvent.click(await screen.findByRole('checkbox', { name: /Teacher A/ }))
    fireEvent.click(screen.getByRole('checkbox', { name: /Teacher B/ }))
    fireEvent.change(screen.getByLabelText('Số bộ đề của Teacher B'), { target: { value: '2' } })
    fireEvent.click(screen.getByRole('button', { name: 'Phân công 2 giảng viên · 3 bộ đề' }))
    await waitFor(() => expect(onSaved).toHaveBeenCalled())
    expect(service.assignBatch).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({
        structureId: 'structure',
        lecturers: [
          { lecturerId: 'teacher-a', setCount: 1 },
          { lecturerId: 'teacher-b', setCount: 2 },
        ],
      }),
    )
  })
  it('shows an empty state instead of an alert when no lecturer is available', async () => {
    render(
      <ExamAssignmentDialog
        preparation={preparation}
        offeringId="offering"
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    )
    expect(await screen.findByText('Không có giảng viên để phân công')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
    expect(screen.getByRole('button', { name: 'Giao soạn đề' })).toBeDisabled()
  })

  it('edits MF-04 follow-up directly via OfficialFollowUpDialog without creating a new blueprint version', async () => {
    vi.mocked(officialExamService.configureFollowUp).mockResolvedValue({
      id: 'session',
      courseId: 'course',
      semesterId: 'term',
      title: preparation.title,
      examDate: '2099-12-31',
      examAttemptType: 'FE',
      hasFollowUp: true,
      maxFollowUpQuestions: 4,
      examInputMode: 'VoiceWithTranscriptEdit',
      transcriptBufferSeconds: 60,
      status: 'scheduled',
    })
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Câu hỏi phụ' }))
    const dialog = await screen.findByRole('dialog', { name: 'Câu hỏi phụ' })
    fireEvent.click(within(dialog).getByRole('checkbox'))
    fireEvent.change(within(dialog).getByLabelText('Số câu hỏi phụ tối đa'), {
      target: { value: '4' },
    })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Lưu cấu hình' }))
    await waitFor(() =>
      expect(officialExamService.configureFollowUp).toHaveBeenCalledWith('session', {
        hasFollowUp: true,
        maxFollowUpQuestions: 4,
      }),
    )
    expect(service.configure).not.toHaveBeenCalled()
  })

  it('restores standalone Exam Structures and MF-04 Follow-up tab on ExamPreparationPage', async () => {
    vi.mocked(officialExamService.getExamStructures).mockResolvedValue([])
    const { courseService } = await import('@/services/course.service')
    vi.mocked(courseService.getLecturerCourses).mockResolvedValue([])
    mount(base)
    const structuresTab = await screen.findByRole('tab', { name: 'Cấu trúc đề & Follow-up' })
    fireEvent.click(structuresTab)
    expect(await screen.findByRole('heading', { name: 'Cấu trúc đề thi' })).toBeTruthy()
  })

  it('creates and deletes Sample Set without counting it toward requiredSetCount', async () => {
    const { questionService } = await import('@/services/question.service')
    vi.mocked(questionService.getQuestions).mockResolvedValue({
      items: [
        {
          id: 'q-sample-1',
          courseId: 'course',
          courseCode: 'PRN231',
          title: 'Câu đề mẫu số 1',
          difficulty: 'medium',
          bloomLevel: 'Apply',
          source: 'manual',
          approvalStatus: 'APPROVED',
          submittedBy: 'head',
          approvedBy: 'head',
          cloId: 'clo',
          createdAt: '2026-11-20T00:00:00Z',
          totalRubricScore: 10,
        },
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 100,
      totalPages: 1,
    })
    vi.mocked(service.createSamplePaper).mockResolvedValue({
      ...makePaper('sample'),
      assignmentId: null,
      setCode: 'MAU-PRN231',
      status: 'DRAFT',
      isSampleSet: true,
    })
    vi.mocked(service.deleteSamplePaper).mockResolvedValue(undefined)

    mount(`${base}/session`)
    fireEvent.click(await screen.findByRole('button', { name: 'Tạo đề mẫu' }))
    const createDialog = await screen.findByRole('dialog', { name: 'Tạo đề mẫu tham khảo' })
    fireEvent.click(await within(createDialog).findByRole('checkbox'))
    fireEvent.click(within(createDialog).getByRole('button', { name: 'Lưu đề mẫu' }))
    await waitFor(() =>
      expect(service.createSamplePaper).toHaveBeenCalledWith({
        structureId: 'structure',
        courseId: 'course',
        sessionId: 'session',
        setCode: 'MAU-PRN231',
        questionIds: ['q-sample-1'],
      }),
    )

    stored.push({
      ...makePaper('sample'),
      assignmentId: null,
      setCode: 'MAU-PRN231',
      status: 'DRAFT',
      isSampleSet: true,
    })
    mount(`${base}/session`)
    expect(screen.queryByRole('button', { name: 'Xem bộ đề MAU-PRN231' })).toBeNull()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem đề mẫu (MAU-PRN231)' }))
    const viewDialog = await screen.findByRole('dialog', { name: /Đề mẫu: MAU-PRN231/ })
    fireEvent.click(within(viewDialog).getByRole('button', { name: 'Xóa đề mẫu' }))
    fireEvent.click(within(viewDialog).getByRole('button', { name: 'Xác nhận xóa' }))
    await waitFor(() => expect(service.deleteSamplePaper).toHaveBeenCalledWith('sample'))
  })

  it('displays red colliding SetCodes banner in ExamPaperReviewDrawer and executes CourseBankCurationPanel workflow', async () => {
    stored[0] = {
      ...stored[0],
      overlapSeverity: 'RED_APPROVED',
      redOverlapQuestionCount: 1,
      questions: stored[0].questions.map((q, idx) =>
        idx === 0 ? { ...q, overlapSeverity: 'RED_APPROVED', duplicateInSets: ['DE-99'] } : q,
      ),
    }
    mount(`${base}/session`)
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    const banner = await screen.findByRole('region', { name: 'Cảnh báo trùng câu giữa các bộ đề' })
    expect(banner).toHaveTextContent('DE-99')
    cleanup()

    vi.mocked(service.getBankCurationCandidates).mockResolvedValue({
      courseId: 'course',
      courseCode: 'PRN231',
      totalPendingCount: 1,
      redCount: 1,
      yellowCount: 0,
      clearCount: 0,
      items: [
        {
          questionId: 'cand-1',
          title: 'Câu tạo tay trùng cao',
          content: 'Nội dung câu tạo tay cần kiểm tra trùng lặp với kho đề.',
          difficulty: 'medium',
          bloomLevel: 'Apply',
          cloId: 'clo',
          cloCode: 'CLO1',
          approvalStatus: 'APPROVED',
          bankCurationStatus: 'PENDING_CURATION',
          submittedBy: 'lecturer',
          submittedByName: 'Nguyễn An',
          sourceSetCodes: ['DE-01'],
          createdAt: '2026-11-23T08:00:00Z',
          maxSimilarityScore: 0.85,
          maxSimilarityPercent: 85,
          highestBand: 'RED_80_PLUS',
          matches: [
            {
              comparedQuestionId: 'bank-99',
              title: 'Câu ngân hàng gốc',
              content: 'Nội dung câu ngân hàng gốc để đối chiếu 2 cột.',
              difficulty: 'medium',
              bloomLevel: 'Apply',
              bankCurationStatus: 'BANK_READY',
              isFromExistingBank: true,
              sourceSetCodes: [],
              similarityScore: 0.85,
              similarityPercent: 85,
              band: 'RED_80_PLUS',
            },
          ],
        },
      ],
    })
    vi.mocked(service.decideBankCuration).mockResolvedValue({
      courseId: 'course',
      decision: 'CURATED_INTO_BANK',
      processedCount: 1,
      questionIds: ['cand-1'],
      curatedAt: '2026-11-24T00:00:00Z',
    })

    mount(base)
    fireEvent.click(await screen.findByRole('tab', { name: 'Lọc kho kỳ sau' }))
    expect(await screen.findByText('Câu tạo tay trùng cao')).toBeTruthy()
    expect(screen.getByText('Câu ngân hàng gốc')).toBeTruthy()
    expect(screen.getByText('Đề DE-01')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Nạp vào kho' }))
    const confirmDialog = await screen.findByRole('dialog', {
      name: 'Xác nhận nạp câu hỏi vào ngân hàng',
    })
    fireEvent.click(within(confirmDialog).getByRole('button', { name: 'Nạp vào kho' }))
    await waitFor(() =>
      expect(service.decideBankCuration).toHaveBeenCalledWith('course', {
        questionIds: ['cand-1'],
        decision: 'CURATED_INTO_BANK',
        notes: undefined,
      }),
    )
  })

  it('filters out inactive, PENDING_CURATION, and EXCLUDED_FROM_BANK questions in SamplePaperCreateDialog and passes bankReadyOnly=true', async () => {
    const { questionService } = await import('@/services/question.service')
    vi.mocked(questionService.getQuestions).mockResolvedValue({
      items: [
        {
          id: 'q-ready',
          courseId: 'course',
          courseCode: 'PRN231',
          title: 'Câu ngân hàng chuẩn',
          difficulty: 'medium',
          bloomLevel: 'Apply',
          source: 'manual',
          approvalStatus: 'APPROVED',
          submittedBy: 'head',
          approvedBy: 'head',
          cloId: 'clo',
          createdAt: '2026-11-20T00:00:00Z',
          totalRubricScore: 10,
          isActive: true,
          bankCurationStatus: 'BANK_READY',
        },
        {
          id: 'q-curated',
          courseId: 'course',
          courseCode: 'PRN231',
          title: 'Câu đã nạp kho',
          difficulty: 'easy',
          bloomLevel: 'Understand',
          source: 'manual',
          approvalStatus: 'APPROVED',
          submittedBy: 'head',
          approvedBy: 'head',
          cloId: 'clo',
          createdAt: '2026-11-20T00:00:00Z',
          totalRubricScore: 10,
          isActive: true,
          bankCurationStatus: 'CURATED_INTO_BANK',
        },
        {
          id: 'q-inactive',
          courseId: 'course',
          courseCode: 'PRN231',
          title: 'Câu đã tạm ẩn',
          difficulty: 'medium',
          bloomLevel: 'Apply',
          source: 'manual',
          approvalStatus: 'APPROVED',
          submittedBy: 'head',
          approvedBy: 'head',
          cloId: 'clo',
          createdAt: '2026-11-20T00:00:00Z',
          totalRubricScore: 10,
          isActive: false,
          bankCurationStatus: 'BANK_READY',
        },
        {
          id: 'q-pending',
          courseId: 'course',
          courseCode: 'PRN231',
          title: 'Câu tạo tay chờ lọc',
          difficulty: 'medium',
          bloomLevel: 'Apply',
          source: 'manual',
          approvalStatus: 'APPROVED',
          submittedBy: 'lecturer',
          approvedBy: 'head',
          cloId: 'clo',
          createdAt: '2026-11-20T00:00:00Z',
          totalRubricScore: 10,
          isActive: false,
          bankCurationStatus: 'PENDING_CURATION',
        },
        {
          id: 'q-excluded',
          courseId: 'course',
          courseCode: 'PRN231',
          title: 'Câu đã loại khỏi kho',
          difficulty: 'hard',
          bloomLevel: 'Analyze',
          source: 'manual',
          approvalStatus: 'APPROVED',
          submittedBy: 'lecturer',
          approvedBy: 'head',
          cloId: 'clo',
          createdAt: '2026-11-20T00:00:00Z',
          totalRubricScore: 10,
          isActive: false,
          bankCurationStatus: 'EXCLUDED_FROM_BANK',
        },
      ],
      totalCount: 5,
      pageNumber: 1,
      pageSize: 100,
      totalPages: 1,
    })

    mount(`${base}/session`)
    fireEvent.click(await screen.findByRole('button', { name: 'Tạo đề mẫu' }))
    const createDialog = await screen.findByRole('dialog', { name: 'Tạo đề mẫu tham khảo' })

    expect(await within(createDialog).findByText('Câu ngân hàng chuẩn')).toBeTruthy()
    expect(within(createDialog).getByText('Câu đã nạp kho')).toBeTruthy()
    expect(within(createDialog).queryByText('Câu đã tạm ẩn')).toBeNull()
    expect(within(createDialog).queryByText('Câu tạo tay chờ lọc')).toBeNull()
    expect(within(createDialog).queryByText('Câu đã loại khỏi kho')).toBeNull()
    expect(questionService.getQuestions).toHaveBeenCalledWith(
      expect.objectContaining({
        courseId: 'course',
        usageScope: 'exam',
        approvalStatus: 'APPROVED',
        bankReadyOnly: true,
      }),
    )
  })
})
