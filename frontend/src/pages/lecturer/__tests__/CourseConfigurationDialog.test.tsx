import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CourseConfigurationDialog } from '../CourseConfigurationDialog'
import { courseService } from '@/services/course.service'

vi.mock('@/services/course.service', () => ({
  courseService: {
    getConfiguration: vi.fn(),
    updateConfiguration: vi.fn(),
  },
}))

describe('CourseConfigurationDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(courseService.getConfiguration).mockResolvedValue({
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: 'Building Cross-Platform Back-End',
      isActive: true,
      transcriptBufferSeconds: 60,
      maxFollowUpQuestions: 2,
      hasFollowUp: true,
      examInputMode: 'VoiceWithTranscriptEdit',
      mockExamDurationMinutes: 20,
      mockExamTotalQuestions: 5,
      mockExamDifficultyDistribution: '{"easy":2,"medium":2,"hard":1}',
    })
    vi.mocked(courseService.updateConfiguration).mockResolvedValue({
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: 'Building Cross-Platform Back-End',
      isActive: true,
      transcriptBufferSeconds: 120,
      maxFollowUpQuestions: 2,
      hasFollowUp: true,
      examInputMode: 'VoiceOnly',
      mockExamDurationMinutes: 20,
      mockExamTotalQuestions: 5,
      mockExamDifficultyDistribution: '{"easy":2,"medium":2,"hard":1}',
    })
  })

  afterEach(cleanup)

  it('exposes transcriptBufferSeconds and examInputMode, validates 10-300s range, and submits changes', async () => {
    const onClose = vi.fn()
    render(<CourseConfigurationDialog courseId="course-1" onClose={onClose} />)

    const bufferInput = await screen.findByLabelText(/Thời gian sửa transcript/i)
    const modeSelect = screen.getByLabelText(/Chế độ nhập câu trả lời/i)

    expect(bufferInput).toHaveValue(60)
    expect(modeSelect).toHaveValue('VoiceWithTranscriptEdit')

    fireEvent.change(bufferInput, { target: { value: '5' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu cấu hình' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Thời gian kiểm tra transcript phải là số nguyên từ 10 đến 300 giây.',
    )
    expect(courseService.updateConfiguration).not.toHaveBeenCalled()

    fireEvent.change(bufferInput, { target: { value: '120' } })
    fireEvent.change(modeSelect, { target: { value: 'VoiceOnly' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lưu cấu hình' }))

    await waitFor(() =>
      expect(courseService.updateConfiguration).toHaveBeenCalledWith('course-1', {
        transcriptBufferSeconds: 120,
        maxFollowUpQuestions: 2,
        hasFollowUp: true,
        examInputMode: 'VoiceOnly',
        mockExamDurationMinutes: 20,
        mockExamTotalQuestions: 5,
        mockExamDifficultyDistribution: '{"easy":2,"medium":2,"hard":1}',
      }),
    )
    expect(onClose).toHaveBeenCalled()
  })

  it('rejects invalid duration, follow-up bounds, negative/decimal difficulty counts, and total questions out of 1..100', async () => {
    render(<CourseConfigurationDialog courseId="course-1" onClose={vi.fn()} />)

    const durationInput = await screen.findByLabelText(/Thời gian thi thử/i)
    const followUpInput = screen.getByLabelText(/Câu hỏi phụ tối đa/i)
    const easyInput = screen.getByLabelText(/Dễ/i)
    const saveButton = screen.getByRole('button', { name: 'Lưu cấu hình' })

    fireEvent.change(durationInput, { target: { value: '0' } })
    fireEvent.click(saveButton)
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Thời gian thi thử phải là số nguyên lớn hơn hoặc bằng 1 phút.',
    )

    fireEvent.change(durationInput, { target: { value: '20' } })
    fireEvent.change(followUpInput, { target: { value: '6' } })
    fireEvent.click(saveButton)
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Số câu hỏi phụ tối đa phải là số nguyên từ 1 đến 5.',
    )

    fireEvent.change(followUpInput, { target: { value: '2' } })
    fireEvent.change(easyInput, { target: { value: '-1' } })
    fireEvent.click(saveButton)
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Số câu ở mỗi độ khó phải là số nguyên từ 0 đến 100.',
    )

    fireEvent.change(easyInput, { target: { value: '1.5' } })
    fireEvent.click(saveButton)
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Số câu ở mỗi độ khó phải là số nguyên từ 0 đến 100.',
    )

    fireEvent.change(easyInput, { target: { value: '99' } })
    fireEvent.click(saveButton)
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Tổng số câu phải từ 1 đến 100.',
    )
    expect(courseService.updateConfiguration).not.toHaveBeenCalled()
  })

  it('normalizes missing difficulty keys from partial BE JSON to 0 on submit', async () => {
    vi.mocked(courseService.getConfiguration).mockResolvedValue({
      courseId: 'course-1',
      courseCode: 'PRN231',
      courseName: 'Building Cross-Platform Back-End',
      isActive: true,
      transcriptBufferSeconds: 60,
      maxFollowUpQuestions: 2,
      hasFollowUp: true,
      examInputMode: 'VoiceWithTranscriptEdit',
      mockExamDurationMinutes: 20,
      mockExamTotalQuestions: 4,
      mockExamDifficultyDistribution: '{"easy":2,"medium":2}',
    })
    const onClose = vi.fn()
    render(<CourseConfigurationDialog courseId="course-1" onClose={onClose} />)

    await screen.findByLabelText(/Thời gian thi thử/i)
    fireEvent.click(screen.getByRole('button', { name: 'Lưu cấu hình' }))

    await waitFor(() =>
      expect(courseService.updateConfiguration).toHaveBeenCalledWith('course-1', {
        transcriptBufferSeconds: 60,
        maxFollowUpQuestions: 2,
        hasFollowUp: true,
        examInputMode: 'VoiceWithTranscriptEdit',
        mockExamDurationMinutes: 20,
        mockExamTotalQuestions: 4,
        mockExamDifficultyDistribution: '{"easy":2,"medium":2,"hard":0}',
      }),
    )
    expect(onClose).toHaveBeenCalled()
  })
})
