import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import QuestionReviewRequestsPage from './QuestionReviewRequestsPage'
import {
  questionReviewRequestService as service,
  type ReviewRequestDetail,
} from '@/services/questionReviewRequest.service'
import { courseCloService } from '@/services/courseClo.service'

vi.mock('@/services/questionReviewRequest.service', () => ({
  questionReviewRequestService: { list: vi.fn(), detail: vi.fn(), decide: vi.fn() },
}))
vi.mock('@/services/courseClo.service', () => ({
  courseCloService: { getCurrentSemesterManagedCourses: vi.fn() },
}))

const detail: ReviewRequestDetail = {
  summary: {
    id: 'request-1',
    courseId: 'course-1',
    courseCode: 'SE',
    courseName: 'Software Engineering',
    lecturerName: 'Nguyễn An',
    lecturerEmail: 'an@example.test',
    submittedAt: '2026-11-17T00:00:00Z',
    notes: 'Nhờ rà soát nội dung',
    status: 'PENDING',
    total: 2,
    pending: 2,
    approved: 0,
    needsRevision: 0,
    rejected: 0,
    version: 3,
  },
  items: ['1', '2'].map((id) => ({
    id,
    questionId: id,
    usageScope: 'practice',
    decision: 'PENDING',
    reviewNotes: null,
    reviewedAt: null,
    reviewerName: null,
    question: {
      title: `Câu hỏi ${id}`,
      content: 'Nội dung chi tiết',
      sampleAnswer: 'Đáp án mẫu',
      difficulty: 'medium',
      bloomLevel: 'Apply',
      cloCode: 'CLO1',
      imageUrl: null,
      imageDescription: null,
      criteria: [
        { name: 'Kiến thức', description: 'Trình bày đầy đủ', maxScore: 10, bloomLevel: 'Apply' },
      ],
    },
  })),
}

function mount(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/department-head/question-review" element={<QuestionReviewRequestsPage />} />
        <Route
          path="/department-head/question-review/:requestId"
          element={<QuestionReviewRequestsPage />}
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Question review requests', () => {
  beforeEach(() => {
    vi.mocked(courseCloService.getCurrentSemesterManagedCourses).mockResolvedValue([])
    vi.mocked(service.list).mockResolvedValue({
      items: [detail.summary],
      totalCount: 1,
      page: 1,
      pageSize: 12,
    })
    vi.mocked(service.detail).mockResolvedValue(detail)
    vi.mocked(service.decide).mockResolvedValue(undefined)
  })
  afterEach(() => {
    cleanup()
    vi.resetAllMocks()
  })

  it('shows requests and links to the request instead of a standalone question', async () => {
    mount('/department-head/question-review')
    await screen.findByText('Nguyễn An')
    expect(screen.getByRole('link', { name: 'Xem chi tiết' }).getAttribute('href')).toBe(
      '/department-head/question-review/request-1',
    )
    expect(service.list).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'PENDING' }),
      expect.any(AbortSignal),
    )
  })

  it('keeps search and filters when returning from request details', async () => {
    const query = '?status=IN_PROGRESS&keyword=An&page=2'
    mount(`/department-head/question-review${query}`)
    await screen.findByText('Nguyễn An')
    expect(screen.getByRole('searchbox', { name: 'Tìm yêu cầu' })).toHaveValue('An')
    expect(service.list).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'IN_PROGRESS', keyword: 'An', page: 2 }),
      expect.any(AbortSignal),
    )
    fireEvent.click(screen.getByRole('link', { name: 'Xem chi tiết' }))
    await screen.findByText('Nhờ rà soát nội dung')
    expect(screen.getByRole('link', { name: 'Về danh sách' })).toHaveAttribute(
      'href', `/department-head/question-review${query}`,
    )
    fireEvent.click(screen.getByRole('link', { name: 'Về danh sách' }))
    await screen.findByRole('searchbox', { name: 'Tìm yêu cầu' })
    expect(screen.getByRole('searchbox', { name: 'Tìm yêu cầu' })).toHaveValue('An')
    expect(screen.getByRole('navigation', { name: 'Quản lý câu hỏi' })).toBeInTheDocument()
  })

  it('requires confirmation for approving the selected pending questions', async () => {
    mount('/department-head/question-review/request-1')
    await screen.findByText('Nhờ rà soát nội dung')
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('tab', { name: /^Chờ duyệt/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Chọn tất cả' }))
    fireEvent.click(screen.getByRole('button', { name: 'Phê duyệt 2 câu' }))
    expect(service.decide).not.toHaveBeenCalled()
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận' }))
    await waitFor(() =>
      expect(service.decide).toHaveBeenCalledWith('request-1', {
        itemIds: ['1', '2'],
        decision: 'APPROVED',
        notes: undefined,
        version: 3,
      }),
    )
  })

  it('opens the complete snapshot and requires a reason for revision', async () => {
    mount('/department-head/question-review/request-1')
    await screen.findByText('Nhờ rà soát nội dung')
    fireEvent.click(screen.getAllByRole('button', { name: 'Xem chi tiết' })[0])
    expect(screen.getByText('Nội dung chi tiết')).toBeTruthy()
    expect(screen.getByText('Đáp án mẫu', { selector: 'p' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Yêu cầu chỉnh sửa' }))
    const decisionDialog = screen
      .getAllByRole('dialog')
      .find((dialog) => within(dialog).queryByRole('button', { name: 'Xác nhận' }))!
    fireEvent.click(within(decisionDialog).getByRole('button', { name: 'Xác nhận' }))
    expect(
      await screen.findByText('Nhập nhận xét từ 10 ký tự để hướng dẫn giảng viên.'),
    ).toBeTruthy()
    expect(service.decide).not.toHaveBeenCalled()
    fireEvent.change(within(decisionDialog).getByRole('textbox'), {
      target: { value: 'Bổ sung phân tích chi tiết.' },
    })
    fireEvent.click(within(decisionDialog).getByRole('button', { name: 'Xác nhận' }))
    await waitFor(() =>
      expect(service.decide).toHaveBeenCalledWith('request-1', {
        itemIds: ['1'],
        decision: 'NEEDS_REVISION',
        notes: 'Bổ sung phân tích chi tiết.',
        version: 3,
      }),
    )
  })
})
