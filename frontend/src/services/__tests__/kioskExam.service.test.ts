import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from '../api.client'
import { kioskExamService } from '../kioskExam.service'

const shiftId = '00000000-0000-4000-8000-000000000030'
const ticketId = '00000000-0000-4000-8000-000000000031'

describe('kioskExamService synchronized MF-04 & BYOD contracts', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('fetches current ticket and ticket recovery state with BYOD headers and synchronized fields', async () => {
    const get = vi
      .spyOn(apiClient, 'get')
      .mockResolvedValueOnce({
        data: {
          id: ticketId,
          shiftId,
          shiftName: 'Ca 1',
          roomLab: 'LAB-301',
          startTime: '2026-11-20T08:00:00.000Z',
          endTime: '2026-11-20T09:00:00.000Z',
          shiftStatus: 'in_progress',
          sessionTitle: 'PRN231 · FE',
          examAttemptType: 'FE',
          studentId: 'student-1',
          seatNumber: 4,
          status: 'IN_PROGRESS',
          examSetId: 'set-1',
          examSetCode: 'SET-01',
          examInputMode: 'VoiceWithTranscriptEdit',
          transcriptBufferSeconds: 60,
          hasFollowUp: true,
          maxFollowUpQuestions: 2,
          isExamPublished: true,
          isKioskLocked: false,
          deviceId: 'OES-DEVICE-1',
          deviceLockedAt: '2026-11-20T07:55:00.000Z',
          deviceUnlockCount: 0,
          violationCount: 1,
          maxViolations: 3,
          lockThreshold: 4,
          remainingStrikes: 3,
          serverNowUtc: '2026-11-20T08:05:00.000Z',
          questions: [],
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          ticketId,
          shiftId,
          status: 'IN_PROGRESS',
          isExamPublished: true,
          isKioskLocked: false,
          violationCount: 2,
          maxViolations: 3,
          lockThreshold: 4,
          remainingStrikes: 2,
          serverNowUtc: '2026-11-20T08:10:00.000Z',
          deviceId: 'OES-DEVICE-1',
          deviceLockedAt: '2026-11-20T07:55:00.000Z',
          deviceUnlockCount: 1,
          followUps: [
            {
              submissionId: 'sub-main-1',
              parentSubmissionId: null,
              examQuestionId: 'q-1',
              followUpIndex: 0,
              followUpPrompt: null,
              generatedFollowUpPrompt: 'Câu phụ 1 sinh từ main',
              audioObjectKey: 'official-exams/shift/ticket/04_SE170001_Q01.webm',
              transcriptWhisper: 'Trả lời câu chính',
            },
            {
              submissionId: 'sub-fu-1',
              parentSubmissionId: 'sub-main-1',
              examQuestionId: 'q-1',
              followUpIndex: 1,
              followUpPrompt: 'Câu phụ 1 sinh từ main',
              generatedFollowUpPrompt: 'Câu phụ 2 sinh từ FU1',
              audioObjectKey: 'official-exams/shift/ticket/04_SE170001_Q01_FU1.webm',
              transcriptWhisper: 'Trả lời câu phụ 1',
            },
          ],
        },
      } as never)

    const current = await kioskExamService.getCurrentTicket({
      deviceId: 'OES-DEVICE-1',
      kioskSessionToken: 'sess-token',
    })
    const state = await kioskExamService.getTicketState(ticketId, { deviceId: 'OES-DEVICE-1' })

    expect(current).toMatchObject({
      id: ticketId,
      deviceId: 'OES-DEVICE-1',
      deviceLockedAt: '2026-11-20T07:55:00.000Z',
      deviceUnlockCount: 0,
      violationCount: 1,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 3,
    })
    expect(state).toMatchObject({
      ticketId,
      deviceId: 'OES-DEVICE-1',
      deviceUnlockCount: 1,
      violationCount: 2,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 2,
      followUps: [
        expect.objectContaining({
          submissionId: 'sub-main-1',
          followUpIndex: 0,
          followUpPrompt: null,
          generatedFollowUpPrompt: 'Câu phụ 1 sinh từ main',
          audioObjectKey: 'official-exams/shift/ticket/04_SE170001_Q01.webm',
        }),
        expect.objectContaining({
          submissionId: 'sub-fu-1',
          followUpIndex: 1,
          followUpPrompt: 'Câu phụ 1 sinh từ main',
          generatedFollowUpPrompt: 'Câu phụ 2 sinh từ FU1',
          audioObjectKey: 'official-exams/shift/ticket/04_SE170001_Q01_FU1.webm',
        }),
      ],
    })
    expect(get).toHaveBeenNthCalledWith(1, '/official-exams/tickets/current', {
      headers: {
        'X-Device-Id': 'OES-DEVICE-1',
        'X-Kiosk-Session-Token': 'sess-token',
      },
    })
    expect(get).toHaveBeenNthCalledWith(2, `/official-exams/tickets/${ticketId}`, {
      headers: { 'X-Device-Id': 'OES-DEVICE-1' },
    })
  })

  it('checks in ticket, logs idempotent anti-cheat events, and exposes audit/override/lock operations', async () => {
    const post = vi
      .spyOn(apiClient, 'post')
      .mockResolvedValueOnce({
        data: {
          id: ticketId,
          shiftId,
          studentId: 'student-1',
          studentCode: 'SE170001',
          seatNumber: 4,
          ipAddress: '10.0.0.10',
          deviceId: 'OES-DEVICE-1',
          deviceLockedAt: '2026-11-20T08:00:00.000Z',
          deviceUnlockCount: 0,
          activeSessionToken: 'active-token-1',
          status: 'IN_PROGRESS',
          checkedInAt: '2026-11-20T08:00:00.000Z',
          examSetId: 'set-1',
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          id: 'log-4',
          ticketId,
          shiftId,
          studentId: 'student-1',
          clientEventId: 'evt-4',
          studentCode: 'SE170001',
          studentName: 'Nguyen An',
          eventType: 'WINDOW_BLUR',
          severity: 'CRITICAL',
          details: 'Blur lần 4',
          violationCount: 4,
          maxViolations: 3,
          lockThreshold: 4,
          remainingStrikes: 0,
          isLocked: true,
          occurredAt: '2026-11-20T08:15:00.000Z',
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          ticketId,
          isLocked: true,
          lockedAt: '2026-11-20T09:05:00.000Z',
          status: 'LOCKED',
          message: 'Ticket locked.',
        },
      } as never)

    const get = vi.spyOn(apiClient, 'get').mockResolvedValueOnce({
      data: [
        {
          ticketId,
          studentCode: 'SE170001',
          fullName: 'Nguyen An',
          seatNumber: 4,
          status: 'AI_GRADED',
          audioR2Url: 'https://r2.example/04_SE170001.webm',
          audioHashSha256: 'a'.repeat(64),
          transcriptWhisper: 'Transcript',
          aiScore: 7.0,
          confidenceScore: 0.85,
          isSuspicious: false,
          suspiciousReason: null,
          cotTrace: 'Reasoning',
          finalScore: 7.0,
          isLocked: false,
        },
      ],
    } as never)

    const put = vi.spyOn(apiClient, 'put').mockResolvedValueOnce({
      data: {
        auditId: 'audit-1',
        ticketId,
        lecturerId: 'lecturer-1',
        originalAiScore: 7.0,
        auditedScore: 8.0,
        overrideReason: 'Thí sinh giải thích đầy đủ kiến thức cốt lõi.',
        ticketStatus: 'AUDITED',
        isLocked: false,
        auditedAt: '2026-11-20T09:02:00.000Z',
      },
    } as never)

    const checkedIn = await kioskExamService.checkInTicket(ticketId, { deviceId: 'OES-DEVICE-1' })
    expect(checkedIn).toMatchObject({
      deviceId: 'OES-DEVICE-1',
      activeSessionToken: 'active-token-1',
      status: 'IN_PROGRESS',
    })

    const antiCheat = await kioskExamService.logAntiCheat({
      ticketId,
      shiftId,
      eventType: 'WINDOW_BLUR',
      severity: 'CRITICAL',
      details: 'Blur lần 4',
      clientEventId: 'evt-4',
    })
    expect(antiCheat).toMatchObject({
      clientEventId: 'evt-4',
      violationCount: 4,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 0,
      isLocked: true,
    })

    const evidence = await kioskExamService.getAuditEvidence(shiftId)
    expect(evidence).toHaveLength(1)
    expect(get).toHaveBeenCalledWith(`/official-exams/shifts/${shiftId}/audit-evidence`)

    const overridden = await kioskExamService.overrideScore(ticketId, {
      auditedScore: 8.0,
      overrideReason: 'Thí sinh giải thích đầy đủ kiến thức cốt lõi.',
    })
    expect(overridden.auditedScore).toBe(8.0)
    expect(put).toHaveBeenCalledWith(`/official-exams/tickets/${ticketId}/override-score`, {
      ticketId,
      auditedScore: 8.0,
      overrideReason: 'Thí sinh giải thích đầy đủ kiến thức cốt lõi.',
    })

    const locked = await kioskExamService.lockTicket(ticketId)
    expect(locked.isLocked).toBe(true)
    expect(post).toHaveBeenCalledTimes(3)
  })

  it('supports Sprint 2 per-turn R2 upload authorization, question answer submission, and next-followup with BYOD headers', async () => {
    const questionId = '00000000-0000-4000-8000-000000000041'
    const submissionId = '00000000-0000-4000-8000-000000000042'
    const headers = {
      deviceId: 'OES-DEVICE-123456789',
      kioskSessionToken: 'sess-token-xyz',
    }

    const post = vi
      .spyOn(apiClient, 'post')
      .mockResolvedValueOnce({
        data: {
          objectKey: 'official-exams/shift/ticket/04_SE170001_Q01.webm',
          fileName: '04_SE170001_Q01.webm',
          uploadUrl: 'https://r2.example/q01',
          expiresAt: '2026-11-20T09:00:00.000Z',
          requiredHeaders: { 'Content-Type': 'audio/webm', 'If-None-Match': '*' },
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          hasMoreFollowUp: true,
          followUpIndex: 1,
          maxFollowUpQuestions: 2,
          parentSubmissionId: submissionId,
          questionId,
          followUpPrompt: 'Giải thích thêm về cơ chế chống N+1 query.',
        },
      } as never)

    const put = vi.spyOn(apiClient, 'put').mockResolvedValueOnce({
      data: {
        submissionId,
        ticketId,
        questionId,
        followUpIndex: 0,
        audioHashSha256: 'a'.repeat(64),
        status: 'RECORDED',
        canRequestFollowUp: true,
      },
    } as never)

    await expect(
      kioskExamService.authorizeQuestionAudioUpload(
        ticketId,
        questionId,
        { followUpIndex: 0, expectedSha256Base64: 'abc123==' },
        headers,
      ),
    ).resolves.toMatchObject({ fileName: '04_SE170001_Q01.webm' })

    await expect(
      kioskExamService.submitQuestionAnswer(
        ticketId,
        questionId,
        {
          followUpIndex: 0,
          parentSubmissionId: null,
          audioObjectKey: 'official-exams/shift/ticket/04_SE170001_Q01.webm',
          audioHashSha256: 'a'.repeat(64),
          initialVoiceTranscript: 'Bản gốc từ Whisper',
          transcriptWhisper: 'Bản gốc từ Whisper',
          timestampsWhisper: null,
          timeSpentSeconds: 35,
          isManuallyEdited: false,
        },
        headers,
      ),
    ).resolves.toMatchObject({ submissionId, canRequestFollowUp: true })

    await expect(
      kioskExamService.requestNextFollowUp(ticketId, submissionId, headers),
    ).resolves.toMatchObject({
      hasMoreFollowUp: true,
      followUpIndex: 1,
      followUpPrompt: 'Giải thích thêm về cơ chế chống N+1 query.',
    })

    expect(post).toHaveBeenNthCalledWith(
      1,
      `/official-exams/tickets/${ticketId}/questions/${questionId}/audio-upload-authorizations`,
      { followUpIndex: 0, expectedSha256Base64: 'abc123==' },
      {
        headers: {
          'X-Device-Id': 'OES-DEVICE-123456789',
          'X-Kiosk-Session-Token': 'sess-token-xyz',
        },
      },
    )
    expect(put).toHaveBeenCalledWith(
      `/official-exams/tickets/${ticketId}/questions/${questionId}/answers`,
      expect.objectContaining({
        initialVoiceTranscript: 'Bản gốc từ Whisper',
        isManuallyEdited: false,
      }),
      {
        headers: {
          'X-Device-Id': 'OES-DEVICE-123456789',
          'X-Kiosk-Session-Token': 'sess-token-xyz',
        },
      },
    )
    expect(post).toHaveBeenNthCalledWith(
      2,
      `/official-exams/tickets/${ticketId}/submissions/${submissionId}/next-followup`,
      undefined,
      {
        headers: {
          'X-Device-Id': 'OES-DEVICE-123456789',
          'X-Kiosk-Session-Token': 'sess-token-xyz',
        },
        timeout: 65_000,
      },
    )
  })
})
