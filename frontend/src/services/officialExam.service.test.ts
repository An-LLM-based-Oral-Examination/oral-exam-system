import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from './api.client'
import { officialExamService } from './officialExam.service'

const sessionId = '00000000-0000-4000-8000-000000000020'
const shiftId = '00000000-0000-4000-8000-000000000021'
const ticketId = '00000000-0000-4000-8000-000000000022'

const sessionPayload = {
  id: sessionId,
  courseId: '00000000-0000-4000-8000-000000000023',
  examStructureId: '00000000-0000-4000-8000-000000000024',
  title: 'PRN231 Final Oral Exam',
  examDate: '2026-11-20',
  hasFollowUp: true,
  maxFollowUpQuestions: 2,
  examInputMode: 'VoiceWithTranscriptEdit',
  transcriptBufferSeconds: 60,
  status: 'SCHEDULED',
}

const shiftPayload = {
  id: shiftId,
  sessionId,
  shiftName: 'Ca 1',
  roomLab: 'Lab 301',
  startTime: '2026-11-20T08:00:00.000Z',
  endTime: '2026-11-20T09:30:00.000Z',
  proctorUserId: 'proctor-1',
  examinerUserId: 'examiner-1',
  examinerName: 'Examiner',
  proctorName: 'Proctor',
  status: 'SCHEDULED',
}

describe('officialExamService MF-04 contract', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('loads and creates official sessions/shifts without changing backend state machine', async () => {
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({ data: [sessionPayload] } as never)
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({ data: shiftPayload } as never)

    await expect(officialExamService.getSessions({ status: 'SCHEDULED' })).resolves.toMatchObject([
      { id: sessionId, status: 'SCHEDULED' },
    ])
    await expect(
      officialExamService.createShift({
        sessionId,
        shiftName: 'Ca 1',
        roomLab: 'Lab 301',
        startTime: shiftPayload.startTime,
        endTime: shiftPayload.endTime,
        proctorUserId: 'proctor-1',
      }),
    ).resolves.toMatchObject({ id: shiftId, roomLab: 'Lab 301' })

    expect(get).toHaveBeenCalledWith('/official-exams/sessions', { params: { status: 'SCHEDULED' } })
    expect(post).toHaveBeenCalledWith('/official-exams/shifts', {
      sessionId,
      shiftName: 'Ca 1',
      roomLab: 'Lab 301',
      startTime: shiftPayload.startTime,
      endTime: shiftPayload.endTime,
      proctorUserId: 'proctor-1',
    })
  })

  it('publishes a shift and grades through the one-way backend endpoints', async () => {
    const post = vi
      .spyOn(apiClient, 'post')
      .mockResolvedValueOnce({ data: { success: true, shiftId, message: 'Exam published.' } } as never)
      .mockResolvedValueOnce({
        data: {
          shiftId,
          totalPublished: 35,
          publishedAt: '2026-11-20T12:00:00.000Z',
          message: 'Grades published.',
        },
      } as never)

    await expect(officialExamService.publishShift(shiftId)).resolves.toEqual({
      success: true,
      shiftId,
      message: 'Exam published.',
    })
    await expect(officialExamService.publishGrades(shiftId)).resolves.toMatchObject({
      shiftId,
      totalPublished: 35,
    })

    expect(post).toHaveBeenNthCalledWith(1, `/official-exams/shifts/${shiftId}/publish`)
    expect(post).toHaveBeenNthCalledWith(2, `/official-exams/shifts/${shiftId}/publish-grades`, {})
  })

  it('keeps the kiosk persist-first order: authorization, seal, then submission', async () => {
    const post = vi
      .spyOn(apiClient, 'post')
      .mockResolvedValueOnce({
        data: {
          objectKey: 'STT_SE170001.webm',
          fileName: 'STT_SE170001.webm',
          uploadUrl: 'https://storage.example/upload',
          expiresAt: '2026-11-20T10:00:00.000Z',
          requiredHeaders: { 'Content-Type': 'audio/webm' },
        },
      } as never)
    const put = vi
      .spyOn(apiClient, 'put')
      .mockResolvedValueOnce({
        data: {
          ticketId,
          objectKey: 'STT_SE170001.webm',
          sha256: 'a'.repeat(64),
          contentLength: 1024,
          verifiedAt: '2026-11-20T10:01:00.000Z',
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          ticketId,
          status: 'SUBMITTED',
          submittedAt: '2026-11-20T10:02:00.000Z',
          submissionCount: 3,
        },
      } as never)

    await officialExamService.createAudioUploadAuthorization(ticketId)
    await officialExamService.sealAudio(ticketId, { objectKey: 'STT_SE170001.webm', sha256: 'a'.repeat(64) })
    await officialExamService.submitExam(ticketId, [
      {
        examQuestionId: 'question-1',
        transcriptWhisper: 'Answer transcript',
        timestampsWhisper: null,
        timeSpentSeconds: 42,
      },
    ])

    expect(post).toHaveBeenCalledWith(
      `/official-exams/tickets/${ticketId}/audio-upload-authorizations`,
    )
    expect(put).toHaveBeenNthCalledWith(1, `/official-exams/tickets/${ticketId}/audio`, {
      objectKey: 'STT_SE170001.webm',
      sha256: 'a'.repeat(64),
    })
    expect(put).toHaveBeenNthCalledWith(2, `/official-exams/tickets/${ticketId}/submission`, {
      answers: [
        {
          examQuestionId: 'question-1',
          transcriptWhisper: 'Answer transcript',
          timestampsWhisper: null,
          timeSpentSeconds: 42,
        },
      ],
    })
  })

  it('normalizes student grade acknowledgement and keeps appeal eligibility in the response', async () => {
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: {
        ticketId,
        studentCode: 'SE170001',
        fullName: 'Le Vu Hoang',
        courseCode: 'PRN231',
        status: 'PUBLISHED',
        isLocked: true,
        totalScore: 7.5,
        publishedAt: '2026-11-20T12:00:00.000Z',
        publishedBy: 'examination-officer',
        studentAcknowledgementStatus: 'PENDING',
        detailedScorecard: [],
        internalAppeal: {
          canAppeal: true,
          instruction: 'Submit within the configured deadline.',
          appealDeadlineDays: 3,
          appealEndpoint: '/api/v1/student/appeals',
        },
      },
    } as never)
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        ticketId,
        studentId: 'student-1',
        studentAcknowledgementStatus: 'ACKNOWLEDGED',
        acknowledgedAtUtc: '2026-11-20T12:05:00.000Z',
        message: 'Grade acknowledged.',
      },
    } as never)

    await expect(officialExamService.getStudentGrade(ticketId, 'student-1')).resolves.toMatchObject({
      ticketId,
      internalAppeal: { canAppeal: true, appealDeadlineDays: 3 },
    })
    await expect(officialExamService.acknowledgeGrade(ticketId, 'student-1')).resolves.toMatchObject({
      studentAcknowledgementStatus: 'ACKNOWLEDGED',
    })

    expect(get).toHaveBeenCalledWith(`/student/official-exams/${ticketId}/grade`, {
      params: { studentId: 'student-1' },
    })
    expect(post).toHaveBeenCalledWith(
      `/student/official-exams/${ticketId}/acknowledge-grade`,
      undefined,
      { params: { studentId: 'student-1' } },
    )
  })
})
