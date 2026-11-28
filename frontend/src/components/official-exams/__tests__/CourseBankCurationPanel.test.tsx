import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CourseBankCurationPanel } from '../CourseBankCurationPanel'
import { courseCloService } from '@/services/courseClo.service'
import { examPreparationService } from '@/services/examPreparation.service'

vi.mock('@/services/courseClo.service', () => ({
  courseCloService: {
    getManagedCourses: vi.fn(),
  },
}))

vi.mock('@/services/examPreparation.service', () => ({
  examPreparationService: {
    getBankCurationCandidates: vi.fn(),
    decideBankCuration: vi.fn(),
  },
}))

describe('CourseBankCurationPanel — direct ?view=curation error handling', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  afterEach(cleanup)

  it('surfaces getManagedCourses() failures via ExamLoadError instead of empty state and recovers on retry', async () => {
    vi.mocked(courseCloService.getManagedCourses)
      .mockRejectedValueOnce(new Error('Không tải được danh sách môn phụ trách'))
      .mockResolvedValueOnce([{ id: 'course-1', code: 'PRN231', name: 'Backend Development' }])

    vi.mocked(examPreparationService.getBankCurationCandidates).mockResolvedValue({
      courseId: 'course-1',
      courseCode: 'PRN231',
      totalPendingCount: 0,
      redCount: 0,
      yellowCount: 0,
      clearCount: 0,
      items: [],
    })

    render(<CourseBankCurationPanel />)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Không tải được danh sách môn phụ trách')
    expect(screen.queryByText('Chưa có môn học được phân công')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))

    await waitFor(() => expect(courseCloService.getManagedCourses).toHaveBeenCalledTimes(2))
    await waitFor(() =>
      expect(examPreparationService.getBankCurationCandidates).toHaveBeenCalledWith(
        'course-1',
        'ALL',
        expect.any(AbortSignal),
      ),
    )
    expect(await screen.findByText('Không có câu hỏi chờ lọc')).toBeInTheDocument()
  })
})
