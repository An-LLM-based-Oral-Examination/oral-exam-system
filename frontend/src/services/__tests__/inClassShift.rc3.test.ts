import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '@/services/api.client'
import { inClassShiftService } from '@/services/inClassShift.service'
import { isRouteAllowedForRole } from '@/routes/routeAccess'
import {
  evaluateInClassPoolSelection,
  formatDifficultyListLabel,
  formatPoolModeLabel,
} from '@/utils/inClassShiftPool'
import type { InClassShiftPoolStats } from '@/types/inClassShift.types'

vi.mock('@/services/api.client', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}))

const samplePoolStats: InClassShiftPoolStats = {
  classId: 'cls-1',
  courseId: 'course-1',
  courseCode: 'PRN231',
  courseBankCounts: { easy: 4, medium: 3, hard: 0, total: 7 },
  clos: [
    {
      cloId: 'clo-1',
      cloCode: 'CLO1',
      description: 'Hiểu kiến trúc Clean Architecture',
      counts: { easy: 3, medium: 1, hard: 0, total: 4 },
    },
    {
      cloId: 'clo-2',
      cloCode: 'CLO2',
      description: 'Triển khai CQRS & MediatR',
      counts: { easy: 1, medium: 2, hard: 0, total: 3 },
    },
    {
      cloId: 'clo-3',
      cloCode: 'CLO3',
      description: 'Bảo mật JWT & RBAC',
      counts: { easy: 0, medium: 0, hard: 0, total: 0 },
    },
  ],
}

describe('MF-05 RC-3 pool selection & service contract', () => {
  afterEach(() => {
    vi.resetAllMocks()
  })

  it('enforces role guards on MF-05 lecturer and student routes', () => {
    expect(isRouteAllowedForRole('Instructor', '/lecturer/in-class-shifts')).toBe(true)
    expect(isRouteAllowedForRole('Instructor', '/lecturer/classes/cls-1/shifts/sh-1/results')).toBe(true)
    expect(isRouteAllowedForRole('Student', '/lecturer/in-class-shifts')).toBe(false)
    expect(isRouteAllowedForRole('DepartmentHead', '/lecturer/in-class-shifts')).toBe(false)

    expect(isRouteAllowedForRole('Student', '/in-class-exam')).toBe(true)
    expect(isRouteAllowedForRole('Student', '/in-class-exam/session')).toBe(true)
    expect(isRouteAllowedForRole('Instructor', '/in-class-exam/session')).toBe(false)
    expect(isRouteAllowedForRole('Admin', '/in-class-exam/session')).toBe(false)
  })

  it('validates totalQuestionsPerStudent in range 1..10 and updates preview when changing difficulties or CLOs', () => {
    const invalidZero = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'ALL_COURSE_BANK',
      totalQuestionsPerStudent: 0,
      selectedDifficulties: ['easy'],
      selectedCloIds: [],
    })
    expect(invalidZero.isValid).toBe(false)
    expect(invalidZero.errors[0]).toContain('1 đến 10')

    const invalidEleven = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'ALL_COURSE_BANK',
      totalQuestionsPerStudent: 11,
      selectedDifficulties: ['easy'],
      selectedCloIds: [],
    })
    expect(invalidEleven.isValid).toBe(false)

    const validBank = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'ALL_COURSE_BANK',
      totalQuestionsPerStudent: 4,
      selectedDifficulties: ['easy', 'medium'],
      selectedCloIds: [],
    })
    expect(validBank.isValid).toBe(true)
    expect(validBank.totalAvailable).toBe(7)
    expect(validBank.difficultyPreview).toEqual([
      { difficulty: 'easy', label: 'Dễ', availableCount: 4, allocatedCount: 2 },
      { difficulty: 'medium', label: 'Trung bình', availableCount: 3, allocatedCount: 2 },
    ])

    const validSelectedClos = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'SELECTED_CLOS',
      totalQuestionsPerStudent: 3,
      selectedDifficulties: ['easy', 'medium'],
      selectedCloIds: ['clo-1', 'clo-2'],
    })
    expect(validSelectedClos.isValid).toBe(true)
    expect(validSelectedClos.totalAvailable).toBe(7)
    expect(validSelectedClos.cloPreview.map((c) => c.allocatedCount)).toEqual([2, 1])
  })

  it('rejects selecting more CLOs than totalQuestionsPerStudent and rejects pools lacking eligible questions', () => {
    const moreClosThanQuestions = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'SELECTED_CLOS',
      totalQuestionsPerStudent: 1,
      selectedDifficulties: ['easy', 'medium'],
      selectedCloIds: ['clo-1', 'clo-2'],
    })
    expect(moreClosThanQuestions.isValid).toBe(false)
    expect(
      moreClosThanQuestions.errors.some((e) =>
        e.includes(
          'Số CLO đã chọn (2) không được lớn hơn số câu hỏi mỗi sinh viên (1). Hãy tăng số câu/SV hoặc giảm bớt CLO.',
        ),
      ),
    ).toBe(true)
    const tooManyClos = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'SELECTED_CLOS',
      totalQuestionsPerStudent: 2,
      selectedDifficulties: ['easy', 'medium'],
      selectedCloIds: ['clo-1', 'clo-2', 'clo-3'],
    })
    expect(tooManyClos.isValid).toBe(false)
    expect(tooManyClos.errors.some((e) => e.includes('Số CLO đã chọn'))).toBe(true)
    expect(tooManyClos.errors.some((e) => e.includes('CLO3'))).toBe(true)

    const missingHard = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'ALL_COURSE_BANK',
      totalQuestionsPerStudent: 2,
      selectedDifficulties: ['easy', 'hard'],
      selectedCloIds: [],
    })
    expect(missingHard.isValid).toBe(false)
    expect(missingHard.errors.some((e) => e.includes('Khó'))).toBe(true)

    const duplicateCloIds = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'SELECTED_CLOS',
      totalQuestionsPerStudent: 1,
      selectedDifficulties: ['easy', 'medium'],
      selectedCloIds: ['clo-1', 'clo-1'],
    })
    expect(duplicateCloIds.isValid).toBe(true)
    expect(duplicateCloIds.totalAvailable).toBe(4)
    expect(duplicateCloIds.cloPreview).toHaveLength(1)

    const invalidCloId = evaluateInClassPoolSelection({
      poolStats: samplePoolStats,
      poolMode: 'SELECTED_CLOS',
      totalQuestionsPerStudent: 2,
      selectedDifficulties: ['easy', 'medium'],
      selectedCloIds: ['clo-1', 'clo-missing'],
    })
    expect(invalidCloId.isValid).toBe(false)
    expect(
      invalidCloId.errors.some((e) =>
        e.includes('không còn hoạt động hoặc không thuộc môn học này'),
      ),
    ).toBe(true)
  })

  it('sends SELECTED_CLOS payload without selectedQuestionIds and reads legacy SELECTED_POOL shifts cleanly', async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({
      data: {
        id: 'shift-new-1',
        classId: 'cls-1',
        className: 'PRN231 - SE1801',
        courseCode: 'PRN231',
        shiftName: 'Progress Quiz 1',
        startTime: '2026-11-17T08:00:00Z',
        endTime: '2026-11-17T09:00:00Z',
        status: 'SCHEDULED',
        isLocked: false,
        questionPoolMode: 'SELECTED_CLOS',
        totalQuestionsPerStudent: 3,
        selectedDifficulties: ['easy', 'medium'],
        selectedCloIds: ['clo-1', 'clo-2'],
        selectedCloCodes: ['CLO1', 'CLO2'],
        examDurationMinutes: 20,
      },
    })

    const created = await inClassShiftService.createShift({
      classId: 'cls-1',
      shiftName: 'Progress Quiz 1',
      startTime: '2026-11-17T08:00:00+07:00',
      endTime: '2026-11-17T09:00:00+07:00',
      questionPoolMode: 'SELECTED_CLOS',
      totalQuestionsPerStudent: 3,
      selectedDifficulties: ['easy', 'medium'],
      selectedCloIds: ['clo-1', 'clo-2'],
      hasFollowUp: false,
      maxFollowUpQuestions: 0,
      isAutoPublishGrades: false,
      examDurationMinutes: 20,
    })

    expect(apiClient.post).toHaveBeenCalledTimes(1)
    const [url, sentBody] = vi.mocked(apiClient.post).mock.calls[0] as [string, Record<string, unknown>]
    expect(url).toBe('/lecturer/in-class-shifts')
    expect('selectedQuestionIds' in sentBody).toBe(false)
    expect(sentBody.questionPoolMode).toBe('SELECTED_CLOS')
    expect(sentBody.totalQuestionsPerStudent).toBe(3)
    expect(sentBody.selectedDifficulties).toEqual(['easy', 'medium'])
    expect(sentBody.selectedCloIds).toEqual(['clo-1', 'clo-2'])

    expect(created.questionPoolMode).toBe('SELECTED_CLOS')
    expect(created.selectedCloCodes).toEqual(['CLO1', 'CLO2'])
    expect(formatPoolModeLabel(created.questionPoolMode, created.selectedCloCodes)).toBe(
      'Theo CLO (CLO1, CLO2)',
    )
    expect(formatDifficultyListLabel(created.selectedDifficulties)).toBe('Dễ · Trung bình')

    // Đọc tương thích ngược ca cũ dùng SELECTED_POOL
    vi.mocked(apiClient.get).mockResolvedValueOnce({
      data: {
        id: 'shift-legacy-1',
        classId: 'cls-1',
        className: 'PRN231 - SE1801',
        shiftName: 'Ca cũ SELECTED_POOL',
        startTime: '2026-11-10T08:00:00Z',
        endTime: '2026-11-10T09:00:00Z',
        status: 'FINALIZED',
        isLocked: true,
        questionPoolMode: 'SELECTED_POOL',
        totalQuestionsPerStudent: 2,
      },
    })

    const legacy = await inClassShiftService.getShift('shift-legacy-1')
    expect(legacy.questionPoolMode).toBe('SELECTED_POOL')
    expect(legacy.selectedDifficulties).toEqual(['easy', 'medium', 'hard'])
    expect(formatPoolModeLabel(legacy.questionPoolMode)).toBe('Bộ câu chọn sẵn (ca cũ)')
  })

  it('maps imageUrl and imageDescription (camelCase and PascalCase) in parseAssignedQuestions on enter and resume', async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({
      data: {
        joinedAt: '2026-11-18T05:00:00Z',
        examEndsAt: '2026-11-18T05:20:00Z',
        remainingSeconds: 1200,
        timedOut: false,
        status: 'IN_PROGRESS',
        assignedQuestions: [
          {
            questionId: 'q-img-1',
            title: 'Clean Architecture',
            content: 'Phân tích sơ đồ phụ thuộc',
            imageUrl: '/uploads/questions/clean-arch.png',
            imageDescription: 'Sơ đồ 4 tầng Clean Architecture',
          },
          {
            questionId: 'q-img-2',
            title: 'CQRS',
            content: 'So sánh Command và Query',
            ImageUrl: 'https://cdn.example.test/cqrs.webp',
            ImageDescription: 'Sơ đồ luồng MediatR',
          },
          {
            questionId: 'q-no-img',
            title: '3NF',
            content: 'Chuẩn hóa 3NF là gì?',
            imageUrl: null,
            imageDescription: null,
          },
        ],
      },
    })

    const entered = await inClassShiftService.enterStudentShift('shift-1', '123456')
    expect(entered.questions).toEqual([
      {
        questionId: 'q-img-1',
        title: 'Clean Architecture',
        content: 'Phân tích sơ đồ phụ thuộc',
        imageUrl: '/uploads/questions/clean-arch.png',
        imageDescription: 'Sơ đồ 4 tầng Clean Architecture',
      },
      {
        questionId: 'q-img-2',
        title: 'CQRS',
        content: 'So sánh Command và Query',
        imageUrl: 'https://cdn.example.test/cqrs.webp',
        imageDescription: 'Sơ đồ luồng MediatR',
      },
      {
        questionId: 'q-no-img',
        title: '3NF',
        content: 'Chuẩn hóa 3NF là gì?',
        imageUrl: null,
        imageDescription: null,
      },
    ])

    vi.mocked(apiClient.post).mockResolvedValueOnce({
      data: {
        joinedAt: '2026-11-18T05:00:00Z',
        examEndsAt: '2026-11-18T05:20:00Z',
        remainingSeconds: 1100,
        timedOut: false,
        status: 'IN_PROGRESS',
        assignedQuestion: {
          questionId: 'q-single',
          title: 'Single Question',
          content: 'Nội dung đơn',
          ImageUrl: '/uploads/questions/single.jpg',
          ImageDescription: 'Mô tả ảnh đơn',
        },
      },
    })

    const resumed = await inClassShiftService.resumeStudentShift('shift-1')
    expect(resumed.questions).toEqual([
      {
        questionId: 'q-single',
        title: 'Single Question',
        content: 'Nội dung đơn',
        imageUrl: '/uploads/questions/single.jpg',
        imageDescription: 'Mô tả ảnh đơn',
      },
    ])
  })

  it('falls back to /student/classes/my-classes and only exposes myFinalScore when FINALIZED or locked', async () => {
    vi.mocked(apiClient.get)
      .mockRejectedValueOnce({ status: 404 })
      .mockResolvedValueOnce({
        data: [
          {
            classId: 'cls-42',
            className: 'SE1842',
            courseCode: 'SWD392',
            courseName: 'Software Architecture and Design',
          },
        ],
      })
      .mockResolvedValueOnce({
        data: [
          {
            id: 'sh-open',
            classId: 'cls-42',
            shiftName: 'Quiz chưa chốt',
            status: 'CLOSED',
            isLocked: false,
            myParticipationStatus: 'GRADED',
            myFinalScore: 9.0,
            startTime: '2026-11-18T08:00:00Z',
            endTime: '2026-11-18T09:00:00Z',
          },
          {
            id: 'sh-final',
            classId: 'cls-42',
            shiftName: 'Quiz đã chốt',
            status: 'FINALIZED',
            isLocked: true,
            myParticipationStatus: 'FINALIZED',
            myFinalScore: 8.75,
            startTime: '2026-11-18T08:00:00Z',
            endTime: '2026-11-18T09:00:00Z',
          },
        ],
      })

    const shifts = await inClassShiftService.listStudentShifts()

    expect(apiClient.get).toHaveBeenNthCalledWith(1, '/student/in-class-shifts/my')
    expect(apiClient.get).toHaveBeenNthCalledWith(2, '/student/classes/my-classes')
    expect(apiClient.get).toHaveBeenNthCalledWith(3, '/student/in-class-shifts/by-class/cls-42')

    expect(shifts).toHaveLength(2)
    expect(shifts[0].courseName).toBe('Software Architecture and Design')
    expect(shifts[0].myFinalScore).toBeNull()
    expect(shifts[1].myFinalScore).toBe(8.75)
  })

  it('requestNextFollowUp overrides Axios timeout to 65s for synchronous AI follow-up generation', async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({
      data: {
        hasMoreFollowUp: true,
        followUpIndex: 1,
        maxFollowUpQuestions: 2,
        parentSubmissionId: 'sub-1',
        questionId: 'q-1',
        followUpPrompt: 'Giải thích thêm về cơ chế chống deadlock trong PostgreSQL.',
      },
    })

    const nextFollowUp = await inClassShiftService.requestNextFollowUp('shift-1', 'sub-1')

    expect(apiClient.post).toHaveBeenCalledWith(
      '/student/in-class-shifts/shift-1/submissions/sub-1/next-followup',
      undefined,
      { timeout: 65_000 },
    )
    expect(nextFollowUp.hasMoreFollowUp).toBe(true)
    expect(nextFollowUp.followUpIndex).toBe(1)
  })
})
