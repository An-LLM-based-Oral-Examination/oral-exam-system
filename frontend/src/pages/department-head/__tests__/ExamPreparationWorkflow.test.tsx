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
    history: vi.fn(),
    decide: vi.fn(),
    configure: vi.fn(),
    assign: vi.fn(),
    handOver: vi.fn(),
  },
}))
vi.mock('@/services/officialExam.service', () => ({
  officialExamService: { getSessions: vi.fn(), configureFollowUp: vi.fn() },
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
  vi.mocked(service.duplicates).mockResolvedValue([])
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
  it('approves the whole set once and automatically selects the next pending set', async () => {
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
    vi.mocked(service.duplicates).mockRejectedValueOnce(new Error('Mất kết nối'))
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    const drawer = screen.getByRole('dialog', { name: 'Xem và duyệt bộ đề' })
    expect(await within(drawer).findByRole('alert')).toHaveTextContent(
      'Chưa kiểm tra được câu trùng',
    )
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
    vi.mocked(service.duplicates).mockResolvedValue([
      {
        questionId: 'source-2',
        questionTitle: 'Tiêu đề câu 2',
        setCode: 'DE-CU',
        sessionId: 'old',
        sessionTitle: 'Thi lại kỳ trước',
        semesterCode: 'SU26',
      },
    ])
    mount()
    fireEvent.click(await screen.findByRole('button', { name: 'Xem bộ đề DE-01' }))
    fireEvent.click(await screen.findByRole('tab', { name: /^Câu 2/ }))
    expect(await screen.findByText('Câu hỏi đã xuất hiện trong 1 bộ đề')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Duyệt bộ đề' })).not.toBeDisabled()
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
    fireEvent.change(screen.getByLabelText('Số câu mỗi đề'), { target: { value: '4' } })
    fireEvent.submit(
      screen.getByRole('button', { name: 'Lưu cấu trúc' }).closest('form') ??
        document.getElementById('exam-blueprint')!,
    )
    expect(await screen.findByText('Tổng phân bổ độ khó phải bằng số câu mỗi đề.')).toBeTruthy()
    expect(service.configure).not.toHaveBeenCalled()
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
})
