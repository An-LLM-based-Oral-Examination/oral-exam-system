import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { QuestionStudioPage } from '../QuestionStudioPage'
import type { QuestionDetail, QuestionSummary } from '@/types/rubric.types'

const mocks = vi.hoisted(() => ({
  role: 'DepartmentHead',
  userId: '11111111-1111-4111-8111-111111111111',
  getQuestions: vi.fn(),
  getQuestionById: vi.fn(),
  reviewDecision: vi.fn(),
  batchApprovePendingQuestions: vi.fn(),
  batchApprove: vi.fn(),
  updateDraft: vi.fn(),
  setQuestionActive: vi.fn(),
  deleteDraft: vi.fn(),
  getLecturerCourses: vi.fn(),
  listClos: vi.fn(),
  toastWarning: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { id: mocks.userId, role: mocks.role, fullName: 'Test User' },
  }),
}))

vi.mock('@/services/question.service', () => ({
  questionService: {
    getQuestions: mocks.getQuestions,
    getQuestionById: mocks.getQuestionById,
    reviewDecision: mocks.reviewDecision,
    batchApprovePendingQuestions: mocks.batchApprovePendingQuestions,
    batchApprove: mocks.batchApprove,
    updateDraft: mocks.updateDraft,
    setQuestionActive: mocks.setQuestionActive,
    deleteDraft: mocks.deleteDraft,
  },
}))

vi.mock('@/services/courseClo.service', () => ({
  courseCloService: {
    list: mocks.listClos,
    getCurrentSemesterManagedCourses: vi
      .fn()
      .mockResolvedValue([{ id: 'course-1', code: 'PRN231', name: 'Backend' }]),
  },
}))

vi.mock('@/services/course.service', () => ({
  courseService: {
    getLecturerCourses: mocks.getLecturerCourses,
  },
}))

vi.mock('@/pages/lecturer/CreateQuestionPage', () => ({
  CreatePracticeQuestionDialog: () => null,
}))

vi.mock('@/components/common/toastStore', () => ({
  toast: {
    warning: mocks.toastWarning,
    success: mocks.toastSuccess,
    error: mocks.toastError,
    info: vi.fn(),
  },
}))

function installMatchMedia() {
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
}

const lecturerQuestion1: QuestionSummary = {
  id: 'q-lec-1',
  courseId: 'course-1',
  courseCode: 'PRN231',
  title: 'Lecturer Question 1',
  difficulty: 'medium',
  bloomLevel: 'Apply',
  source: 'manual',
  approvalStatus: 'SUBMITTED_FOR_REVIEW',
  submittedBy: '22222222-2222-4222-8222-222222222222',
  approvedBy: null,
  createdAt: '2026-11-18T00:00:00Z',
  totalRubricScore: 10,
}

const lecturerQuestion2: QuestionSummary = {
  ...lecturerQuestion1,
  id: 'q-lec-2',
  title: 'Lecturer Question 2',
}

const selfSubmittedQuestion: QuestionSummary = {
  ...lecturerQuestion1,
  id: 'q-self-1',
  title: 'Self Submitted Question',
  submittedBy: '11111111-1111-4111-8111-111111111111',
}

function makeDetail(summary: QuestionSummary): QuestionDetail {
  return {
    id: summary.id,
    courseId: summary.courseId,
    courseCode: summary.courseCode,
    rubricId: 'rubric-1',
    title: summary.title,
    content: 'Detailed prompt',
    sampleAnswer: 'Sample answer text that is sufficiently long.',
    keyPoints: ['Key 1'],
    difficulty: summary.difficulty,
    bloomLevel: summary.bloomLevel,
    source: summary.source,
    approvalStatus: summary.approvalStatus,
    submittedBy: summary.submittedBy,
    submittedByName: 'Giảng viên A',
    approvedBy: null,
    approvedByName: null,
    reviewNotes: null,
    isActive: true,
    createdAt: summary.createdAt,
    rubric: {
      id: 'rubric-1',
      name: 'Rubric 10đ',
      description: null,
      totalMaxScore: 10,
      criteria: [
        {
          id: 'crit-1',
          criterionName: 'Accuracy',
          description: 'Core concept',
          maxScore: 10,
          weight: 100,
          bloomLevel: 'Apply',
          orderIndex: 1,
        },
      ],
    },
  }
}

describe('QuestionStudioPage review queue & batch approval', () => {
  beforeEach(() => {
    installMatchMedia()
    vi.clearAllMocks()
    mocks.role = 'DepartmentHead'
    mocks.userId = '11111111-1111-4111-8111-111111111111'
    mocks.getLecturerCourses.mockResolvedValue([
      { id: 'course-1', code: 'PRN231', name: 'Backend' },
      { id: 'course-2', code: 'SWP391', name: 'Capstone' },
    ])
    mocks.getQuestions.mockResolvedValue({
      items: [lecturerQuestion1, lecturerQuestion2, selfSubmittedQuestion],
      totalCount: 3,
      pageNumber: 1,
      pageSize: 12,
      totalPages: 1,
    })
  })

  it('defaults DepartmentHead to "Chờ phê duyệt" tab, queries SUBMITTED_FOR_REVIEW without submittedBy, and shows count badge', async () => {
    render(
      <MemoryRouter initialEntries={['/department-head/question-review']}>
        <QuestionStudioPage />
      </MemoryRouter>,
    )

    const reviewTab = await screen.findByRole('tab', { name: /Chờ phê duyệt/ })
    expect(reviewTab).toHaveAttribute('aria-selected', 'true')
    expect(reviewTab).toHaveTextContent('3')

    await waitFor(() =>
      expect(mocks.getQuestions).toHaveBeenCalledWith(
        expect.objectContaining({
          usageScope: 'practice',
          approvalStatus: 'SUBMITTED_FOR_REVIEW',
        }),
      ),
    )
    const firstQuery = mocks.getQuestions.mock.calls[0]?.[0] as Record<string, unknown>
    expect(firstQuery.submittedBy).toBeUndefined()
  })

  it('excludes self-submitted questions from batch approve and disables self-approval in row & detail drawer', async () => {
    mocks.getQuestionById.mockImplementation((id: string) => {
      if (id === selfSubmittedQuestion.id) return Promise.resolve(makeDetail(selfSubmittedQuestion))
      return Promise.resolve(makeDetail(lecturerQuestion1))
    })

    render(
      <MemoryRouter initialEntries={['/department-head/question-review']}>
        <QuestionStudioPage />
      </MemoryRouter>,
    )

    await screen.findByText('Self Submitted Question')

    // Only 2 lecturer questions are eligible for batch approve
    const quickApproveBtn = screen.getByRole('button', { name: 'Duyệt nhanh (2)' })
    expect(quickApproveBtn).toBeEnabled()

    const selfCheckbox = await screen.findByLabelText('Chọn câu hỏi Self Submitted Question')
    expect(selfCheckbox).toBeDisabled()
    expect(screen.getByLabelText('Chọn câu hỏi Lecturer Question 1')).toBeEnabled()
    expect(screen.getByText('Của bạn · Không thể tự duyệt')).toBeTruthy()

    // Open detail drawer on self-submitted question -> Phê duyệt disabled with notice
    fireEvent.click(screen.getByText('Self Submitted Question'))
    expect(await screen.findByText('Bạn là người gửi câu hỏi này nên không thể tự phê duyệt.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Phê duyệt' })).toBeDisabled()

    // Open detail drawer on lecturer question -> Phê duyệt enabled
    fireEvent.click(screen.getByText('Lecturer Question 1'))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Phê duyệt' })).toBeEnabled())
  })

  it('calls batchApprovePendingQuestions with eligible IDs and surfaces partial failure warning', async () => {
    mocks.batchApprovePendingQuestions.mockResolvedValue({
      approvedCount: 1,
      approvedIds: ['q-lec-1'],
      failed: [{ id: 'q-lec-2', message: 'Câu hỏi đã được xử lý trước đó.' }],
    })

    render(
      <MemoryRouter initialEntries={['/department-head/question-review']}>
        <QuestionStudioPage />
      </MemoryRouter>,
    )

    const quickApproveBtn = await screen.findByRole('button', { name: 'Duyệt nhanh (2)' })
    fireEvent.click(quickApproveBtn)

    await waitFor(() =>
      expect(mocks.batchApprovePendingQuestions).toHaveBeenCalledWith(
        ['q-lec-1', 'q-lec-2'],
        { reviewerId: mocks.userId },
      ),
    )
    expect(mocks.batchApprove).not.toHaveBeenCalled()
    expect(mocks.toastWarning).toHaveBeenCalledWith(
      expect.stringContaining('Đã duyệt 1 câu hỏi, 1 câu lỗi'),
    )
  })

  it('re-queries review queue with courseId filter without attaching submittedBy', async () => {
    render(
      <MemoryRouter initialEntries={['/department-head/question-review']}>
        <QuestionStudioPage />
      </MemoryRouter>,
    )

    await screen.findByText('Lecturer Question 1')
    fireEvent.click(screen.getByRole('button', { name: 'Lọc theo môn học' }))
    const courseOption = await screen.findByRole('option', { name: 'PRN231 · Backend' })
    fireEvent.click(courseOption)

    await waitFor(() =>
      expect(mocks.getQuestions).toHaveBeenLastCalledWith(
        expect.objectContaining({
          courseId: 'course-1',
          approvalStatus: 'SUBMITTED_FOR_REVIEW',
          usageScope: 'practice',
        }),
      ),
    )
    const lastCall = mocks.getQuestions.mock.calls.at(-1)?.[0] as Record<string, unknown>
    expect(lastCall.submittedBy).toBeUndefined()
  })

  it('keeps Instructor on "Câu hỏi của tôi" without "Chờ phê duyệt" tab', async () => {
    mocks.role = 'Instructor'
    mocks.getQuestions.mockResolvedValue({
      items: [],
      totalCount: 0,
      pageNumber: 1,
      pageSize: 12,
      totalPages: 1,
    })

    render(
      <MemoryRouter initialEntries={['/lecturer/question-bank']}>
        <QuestionStudioPage />
      </MemoryRouter>,
    )

    expect(await screen.findByRole('tab', { name: 'Câu hỏi của tôi' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.queryByRole('tab', { name: /Chờ phê duyệt/ })).toBeNull()
    await waitFor(() =>
      expect(mocks.getQuestions).toHaveBeenCalledWith(
        expect.objectContaining({
          submittedBy: mocks.userId,
          usageScope: 'practice',
        }),
      ),
    )
  })

  it('does not show submit-for-review or review decision buttons when DepartmentHead opens a DRAFT with submittedBy: null', async () => {
    const nullAuthorDraft: QuestionSummary = {
      ...lecturerQuestion1,
      id: 'q-draft-null',
      title: 'Null Author Draft',
      approvalStatus: 'DRAFT',
      submittedBy: null,
    }
    mocks.getQuestions.mockResolvedValue({
      items: [nullAuthorDraft],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 12,
      totalPages: 1,
    })
    mocks.getQuestionById.mockResolvedValue(makeDetail(nullAuthorDraft))

    render(
      <MemoryRouter initialEntries={['/department-head/question-review']}>
        <QuestionStudioPage />
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByRole('tab', { name: 'Câu hỏi của tôi' }))
    expect(await screen.findByText('Null Author Draft')).toBeTruthy()
    expect(screen.queryByText('Của bạn')).toBeNull()
    expect(screen.getByLabelText('Chọn câu hỏi Null Author Draft')).toBeDisabled()

    fireEvent.click(screen.getByText('Null Author Draft'))
    expect(await screen.findByText('Detailed prompt')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Gửi Trưởng BM duyệt' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Phê duyệt' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Yêu cầu sửa' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Từ chối' })).toBeNull()
  })

  it('allows Lecturer to withdraw own DRAFT via ConfirmDialog and DepartmentHead to toggle APPROVED question active state', async () => {
    mocks.role = 'Instructor'
    const ownDraft: QuestionSummary = {
      ...lecturerQuestion1,
      id: 'q-own-draft',
      title: 'Own Draft Question',
      approvalStatus: 'DRAFT',
      submittedBy: mocks.userId,
    }
    mocks.getQuestions.mockResolvedValue({
      items: [ownDraft],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 12,
      totalPages: 1,
    })
    mocks.getQuestionById.mockResolvedValue(makeDetail(ownDraft))
    mocks.deleteDraft.mockResolvedValue(undefined)

    const { unmount } = render(
      <MemoryRouter initialEntries={['/lecturer/question-bank']}>
        <QuestionStudioPage />
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByText('Own Draft Question'))
    expect(await screen.findByRole('button', { name: 'Chỉnh sửa câu hỏi' })).toBeEnabled()

    fireEvent.click(screen.getByRole('button', { name: 'Thu hồi bản nháp' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Xác nhận thu hồi' }))

    await waitFor(() => expect(mocks.deleteDraft).toHaveBeenCalledWith('q-own-draft'))
    expect(mocks.toastSuccess).toHaveBeenCalledWith('Đã thu hồi bản nháp câu hỏi.')
    unmount()

    // DepartmentHead toggles APPROVED question active state
    mocks.role = 'DepartmentHead'
    const approvedQuestion: QuestionSummary = {
      ...lecturerQuestion1,
      id: 'q-approved-toggle',
      title: 'Approved Question Toggle',
      approvalStatus: 'APPROVED',
    }
    mocks.getQuestions.mockResolvedValue({
      items: [approvedQuestion],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 12,
      totalPages: 1,
    })
    mocks.getQuestionById.mockResolvedValue({
      ...makeDetail(approvedQuestion),
      isActive: true,
    })
    mocks.setQuestionActive.mockResolvedValue(undefined)

    render(
      <MemoryRouter initialEntries={['/department-head/question-bank']}>
        <QuestionStudioPage />
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByText('Approved Question Toggle'))
    const pauseBtn = await screen.findByRole('button', { name: 'Tạm ngưng sử dụng' })
    fireEvent.click(pauseBtn)

    await waitFor(() =>
      expect(mocks.setQuestionActive).toHaveBeenCalledWith('q-approved-toggle', false),
    )
    expect(await screen.findByRole('button', { name: 'Kích hoạt lại' })).toBeEnabled()
  })
})
