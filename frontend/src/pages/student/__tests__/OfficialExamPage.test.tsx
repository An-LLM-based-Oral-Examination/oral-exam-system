import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { OfficialExamPage } from '../OfficialExamPage'
import { kioskExamService } from '@/services/kioskExam.service'
import { transcribeSpeech } from '@/services/speech.service'
import { webKioskBridge } from '@/lib/kioskNativeBridge'
import { useAuth } from '@/hooks/useAuth'
import { useAudioRecorder } from '@/hooks/useAudioRecorder'

vi.mock('@/services/kioskExam.service', () => ({
  kioskExamService: {
    getCurrentTicket: vi.fn(),
    getTicketState: vi.fn(),
    checkInTicket: vi.fn(),
    logAntiCheat: vi.fn(),
    createAudioUploadAuthorization: vi.fn(),
    uploadAudio: vi.fn(),
    sealAudio: vi.fn(),
    authorizeQuestionAudioUpload: vi.fn(),
    submitQuestionAnswer: vi.fn(),
    requestNextFollowUp: vi.fn(),
    submitExam: vi.fn(),
    getSubmissionStatus: vi.fn(),
  },
}))

vi.mock('@/services/speech.service', () => ({
  transcribeSpeech: vi.fn(),
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: vi.fn(),
}))

vi.mock('@/hooks/useAudioRecorder', () => ({
  useAudioRecorder: vi.fn(),
}))

const baseTicket = {
  id: 'ticket-1',
  shiftId: 'shift-1',
  sessionTitle: 'PRN231 Final Oral Exam',
  shiftName: 'Ca 1',
  roomLab: 'Lab 301',
  startTime: '2026-11-20T08:00:00.000Z',
  endTime: '2026-11-20T09:30:00.000Z',
  studentId: 'student-1',
  seatNumber: 5,
  status: 'IN_PROGRESS',
  shiftStatus: 'in_progress',
  examSetId: 'set-1',
  examSetCode: 'SET-01',
  examInputMode: 'VoiceWithTranscriptEdit',
  transcriptBufferSeconds: 60,
  hasFollowUp: false,
  maxFollowUpQuestions: 2,
  examAttemptType: 'FE' as const,
  isExamPublished: true,
  isKioskLocked: false,
  deviceId: 'BYOD-01',
  deviceLockedAt: '2026-11-20T08:00:00.000Z',
  deviceUnlockCount: 0,
  violationCount: 0,
  maxViolations: 3,
  lockThreshold: 4,
  remainingStrikes: 4,
  serverNowUtc: '2026-11-20T08:05:00.000Z',
  followUps: [],
  questions: [
    {
      questionId: 'q-1',
      orderIndex: 1,
      title: 'Clean Architecture',
      content: 'Trình bày Dependency Inversion Principle trong .NET 8.',
      bloomLevel: 'Understand',
      difficulty: 'medium',
      imageUrl: null,
      imageDescription: null,
    },
  ],
}

describe('OfficialExamPage — FE-3 & FE-4 Kiosk BYOD, VoiceOnly/35% & Per-Turn Follow-up', () => {
  const cancelRecording = vi.fn()
  const startRecording = vi.fn()
  const stopRecording = vi.fn()
  const originalAudioContext = window.AudioContext

  beforeEach(() => {
    vi.clearAllMocks()
    sessionStorage.clear()
    localStorage.clear()
    webKioskBridge.clearSessionToken('ticket-1')

    vi.mocked(useAuth).mockReturnValue({
      user: {
        id: 'student-1',
        email: 'sv@fpt.edu.vn',
        fullName: 'Sinh viên A',
        role: 'Student',
      },
      isAuthenticated: true,
    } as never)

    vi.mocked(useAudioRecorder).mockReturnValue({
      isRecording: false,
      error: null,
      startRecording,
      stopRecording,
      cancelRecording,
    } as never)

    vi.mocked(kioskExamService.getCurrentTicket).mockResolvedValue(baseTicket)
    vi.mocked(kioskExamService.getTicketState).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      status: 'IN_PROGRESS',
      isExamPublished: true,
      isKioskLocked: false,
      violationCount: 0,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 4,
      serverNowUtc: '2026-11-20T08:05:00.000Z',
      deviceId: 'BYOD-01',
      deviceLockedAt: '2026-11-20T08:00:00.000Z',
      deviceUnlockCount: 0,
      followUps: [],
    })
  })

  afterEach(() => {
    Object.defineProperty(window, 'AudioContext', {
      configurable: true,
      writable: true,
      value: originalAudioContext,
    })
  })

  it('logs window blur via kioskExamService.logAntiCheat with clientEventId + BYOD headers and locks Kiosk when server confirms isLocked: true', async () => {
    vi.mocked(useAudioRecorder).mockReturnValue({
      isRecording: true,
      error: null,
      startRecording,
      stopRecording,
      cancelRecording,
    } as never)
    vi.mocked(kioskExamService.logAntiCheat).mockResolvedValue({
      id: 'log-4',
      clientEventId: 'evt-4',
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      studentId: 'student-1',
      studentCode: 'SE170001',
      studentName: 'Sinh viên A',
      eventType: 'WINDOW_BLUR',
      severity: 'high',
      details: 'Cửa sổ thi mất tiêu điểm (window blur).',
      violationCount: 4,
      occurredAt: '2026-11-20T08:10:00.000Z',
      isLocked: true,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 0,
    })

    render(<OfficialExamPage />)
    await screen.findByText('PRN231 Final Oral Exam')

    fireEvent(window, new Event('blur'))

    await waitFor(() =>
      expect(kioskExamService.logAntiCheat).toHaveBeenCalledWith(
        expect.objectContaining({
          ticketId: 'ticket-1',
          shiftId: 'shift-1',
          eventType: 'WINDOW_BLUR',
          severity: 'high',
          clientEventId: expect.any(String),
        }),
        expect.objectContaining({
          deviceId: expect.stringMatching(/^WEB-KIOSK-/),
        }),
      ),
    )
    expect(cancelRecording).toHaveBeenCalled()
    expect(await screen.findByText(/Vé thi đã bị khóa do vi phạm quy chế Kiosk/)).toBeInTheDocument()
  })

  it('maps blocked Kiosk shortcuts to BE-valid DISALLOWED_PROCESS with lowercase medium severity', async () => {
    vi.mocked(useAudioRecorder).mockReturnValue({
      isRecording: true,
      error: null,
      startRecording,
      stopRecording,
      cancelRecording,
    } as never)
    vi.mocked(kioskExamService.logAntiCheat).mockResolvedValue({
      id: 'log-1',
      clientEventId: 'evt-1',
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      studentId: 'student-1',
      studentCode: 'SE170001',
      studentName: 'Sinh viên A',
      eventType: 'DISALLOWED_PROCESS',
      severity: 'medium',
      details: 'Tổ hợp phím bị chặn trong chế độ Kiosk (F12).',
      violationCount: 1,
      occurredAt: '2026-11-20T08:08:00.000Z',
      isLocked: false,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 3,
    })

    render(<OfficialExamPage />)
    await screen.findByText('PRN231 Final Oral Exam')

    fireEvent.keyDown(document, { key: 'F12' })

    await waitFor(() =>
      expect(kioskExamService.logAntiCheat).toHaveBeenCalledWith(
        expect.objectContaining({
          ticketId: 'ticket-1',
          shiftId: 'shift-1',
          eventType: 'DISALLOWED_PROCESS',
          severity: 'medium',
          clientEventId: expect.any(String),
        }),
        expect.objectContaining({
          deviceId: expect.stringMatching(/^WEB-KIOSK-/),
        }),
      ),
    )
  })

  it('hydrates F5 server violationCount via getTicketState and stores drafts strictly in sessionStorage (zero localStorage)', async () => {
    vi.mocked(kioskExamService.getTicketState).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      status: 'IN_PROGRESS',
      isExamPublished: true,
      isKioskLocked: false,
      violationCount: 2,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 2,
      serverNowUtc: '2026-11-20T08:06:00.000Z',
      deviceId: 'BYOD-01',
      deviceLockedAt: '2026-11-20T08:00:00.000Z',
      deviceUnlockCount: 0,
      followUps: [],
    })

    render(<OfficialExamPage />)
    expect(await screen.findByText(/Vi phạm Kiosk: 2\/3 \(khóa ở lần 4\)/)).toBeInTheDocument()
    expect(kioskExamService.getTicketState).toHaveBeenCalledWith(
      'ticket-1',
      expect.objectContaining({ deviceId: expect.stringMatching(/^WEB-KIOSK-/) }),
    )
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.getItem('mf04_kiosk_session_draft_student-1_ticket-1')).toBeTruthy()
  })

  it('fails closed on Mic-Check without AudioContext, stores 1-time check-in sessionToken in sessionStorage, and maps 409 BYOD conflicts', async () => {
    Object.defineProperty(window, 'AudioContext', {
      configurable: true,
      writable: true,
      value: undefined,
    })

    const { unmount } = render(<OfficialExamPage />)
    await screen.findByText('PRN231 Final Oral Exam')

    fireEvent.click(screen.getByRole('button', { name: /Kiểm tra Microphone/ }))
    expect(await screen.findByText(/AudioContext không khả dụng/)).toBeInTheDocument()
    expect(screen.queryByText(/65dB/)).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Bắt đầu ghi âm/ }))
    expect(startRecording).not.toHaveBeenCalled()
    unmount()

    vi.mocked(kioskExamService.getCurrentTicket).mockResolvedValue({
      ...baseTicket,
      status: 'SCHEDULED',
    })
    vi.mocked(kioskExamService.getTicketState).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      status: 'SCHEDULED',
      isExamPublished: true,
      isKioskLocked: false,
      violationCount: 0,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 4,
      serverNowUtc: '2026-11-20T08:05:00.000Z',
      deviceId: null,
      deviceLockedAt: null,
      deviceUnlockCount: 0,
      followUps: [],
    })
    vi.mocked(kioskExamService.checkInTicket)
      .mockRejectedValueOnce({
        status: 409,
        code: 'DEVICE_IN_USE_OTHER_TICKET',
        detail: 'Device bound to another ticket',
      })
      .mockResolvedValueOnce({
        id: 'ticket-1',
        shiftId: 'shift-1',
        studentId: 'student-1',
        studentCode: 'SE170001',
        seatNumber: 5,
        ipAddress: null,
        status: 'IN_PROGRESS',
        checkedInAt: '2026-11-20T08:05:00.000Z',
        examSetId: 'set-1',
        deviceId: webKioskBridge.getDeviceId(),
        deviceLockedAt: '2026-11-20T08:05:00.000Z',
        deviceUnlockCount: 0,
        activeSessionToken: 'one-time-session-token-xyz',
      })

    render(<OfficialExamPage />)
    fireEvent.click(await screen.findByRole('button', { name: /Xác nhận nhận máy/ }))
    expect(
      await screen.findByText(/Thiết bị BYOD này đang được gắn với một vé thi khác/),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Xác nhận nhận máy/ }))
    await waitFor(() =>
      expect(webKioskBridge.getSessionToken('ticket-1')).toBe('one-time-session-token-xyz'),
    )
    expect(localStorage.length).toBe(0)
  })

  it('locks transcript input in VoiceOnly mode and runs per-turn R2 + follow-up flow without revealing scores when hasFollowUp is true', async () => {
    vi.mocked(useAudioRecorder).mockReturnValue({
      isRecording: true,
      error: null,
      startRecording,
      stopRecording: vi.fn().mockResolvedValue(new Blob(['audio-turn-1'], { type: 'audio/webm' })),
      cancelRecording,
    } as never)

    vi.mocked(kioskExamService.getCurrentTicket).mockResolvedValue({
      ...baseTicket,
      examInputMode: 'VoiceOnly',
      hasFollowUp: true,
      maxFollowUpQuestions: 2,
    })
    vi.mocked(kioskExamService.getTicketState).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      status: 'IN_PROGRESS',
      isExamPublished: true,
      isKioskLocked: false,
      violationCount: 0,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 4,
      serverNowUtc: '2026-11-20T08:05:00.000Z',
      deviceId: 'BYOD-01',
      deviceLockedAt: '2026-11-20T08:00:00.000Z',
      deviceUnlockCount: 0,
      followUps: [],
    })
    vi.mocked(transcribeSpeech).mockResolvedValue({
      transcript: 'Em trình bày nguyên lý đảo ngược phụ thuộc.',
      timestampsJson: null,
    } as never)
    vi.mocked(kioskExamService.authorizeQuestionAudioUpload).mockResolvedValue({
      objectKey: 'official-exams/shift-1/ticket-1/05_SE170001_Q01.webm',
      fileName: '05_SE170001_Q01.webm',
      uploadUrl: 'https://r2.example.com/upload',
      expiresAt: '2026-11-20T09:00:00.000Z',
      requiredHeaders: { 'Content-Type': 'audio/webm' },
    })
    vi.mocked(kioskExamService.uploadAudio).mockResolvedValue(undefined)
    vi.mocked(kioskExamService.submitQuestionAnswer).mockResolvedValue({
      submissionId: 'sub-main-1',
      ticketId: 'ticket-1',
      questionId: 'q-1',
      followUpIndex: 0,
      audioHashSha256: 'a'.repeat(64),
      status: 'RECORDED',
      canRequestFollowUp: true,
    })
    vi.mocked(kioskExamService.requestNextFollowUp).mockResolvedValue({
      hasMoreFollowUp: true,
      followUpIndex: 1,
      maxFollowUpQuestions: 2,
      parentSubmissionId: 'sub-main-1',
      questionId: 'q-1',
      followUpPrompt: 'Hãy cho biết lợi ích của việc tiêm phụ thuộc qua Constructor Injection.',
    })

    render(<OfficialExamPage />)
    expect(
      await screen.findByText(/Chế độ VoiceOnly: khóa hoàn toàn chỉnh sửa transcript thủ công/),
    ).toBeInTheDocument()
    const textarea = screen.getByLabelText('Transcript câu 1')
    expect(textarea).toHaveAttribute('readonly')
    expect(textarea).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: /Thu âm trả lời câu này/ }))
    fireEvent.click(await screen.findByRole('button', { name: /Dừng & Bóc băng câu này/ }))

    const saveTurnButton = await screen.findByRole('button', {
      name: /Lưu câu trả lời & Kiểm tra câu phụ/,
    })
    fireEvent.click(saveTurnButton)

    await waitFor(() =>
      expect(kioskExamService.authorizeQuestionAudioUpload).toHaveBeenCalledWith(
        'ticket-1',
        'q-1',
        expect.objectContaining({ followUpIndex: 0 }),
        expect.objectContaining({ deviceId: expect.stringMatching(/^WEB-KIOSK-/) }),
      ),
    )
    expect(kioskExamService.submitQuestionAnswer).toHaveBeenCalledWith(
      'ticket-1',
      'q-1',
      expect.objectContaining({
        followUpIndex: 0,
        isManuallyEdited: false,
        initialVoiceTranscript: 'Em trình bày nguyên lý đảo ngược phụ thuộc.',
      }),
      expect.any(Object),
    )
    expect(kioskExamService.requestNextFollowUp).toHaveBeenCalledWith(
      'ticket-1',
      'sub-main-1',
      expect.any(Object),
    )
    expect(
      await screen.findByText(
        'Hãy cho biết lợi ích của việc tiêm phụ thuộc qua Constructor Injection.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText(/\/10/)).not.toBeInTheDocument()
  })

  it('recovers F5 per-turn follow-up state accurately: marks main #0 & FU1 as saved without rendering Câu hỏi phụ #0 and exposes pending FU2 prompt', async () => {
    vi.mocked(useAudioRecorder).mockReturnValue({
      isRecording: true,
      error: null,
      startRecording,
      stopRecording,
      cancelRecording,
    } as never)

    vi.mocked(kioskExamService.getCurrentTicket).mockResolvedValue({
      ...baseTicket,
      hasFollowUp: true,
      maxFollowUpQuestions: 2,
    })
    vi.mocked(kioskExamService.getTicketState).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      status: 'IN_PROGRESS',
      isExamPublished: true,
      isKioskLocked: false,
      violationCount: 0,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 4,
      serverNowUtc: '2026-11-20T08:10:00.000Z',
      deviceId: 'BYOD-01',
      deviceLockedAt: '2026-11-20T08:00:00.000Z',
      deviceUnlockCount: 0,
      followUps: [
        {
          submissionId: 'sub-main-1',
          parentSubmissionId: null,
          examQuestionId: 'q-1',
          followUpIndex: 0,
          followUpPrompt: null,
          generatedFollowUpPrompt: 'Câu phụ 1 cần giải thích thêm',
          transcriptWhisper: 'Câu trả lời chính đã lưu',
          audioObjectKey: 'official-exams/shift-1/ticket-1/05_SE170001_Q01.webm',
          audioR2Url: null,
          audioHashSha256: 'a'.repeat(64),
          aiScore: null,
          finalScore: null,
        },
        {
          submissionId: 'sub-fu-1',
          parentSubmissionId: 'sub-main-1',
          examQuestionId: 'q-1',
          followUpIndex: 1,
          followUpPrompt: 'Câu phụ 1 cần giải thích thêm',
          generatedFollowUpPrompt: 'Câu phụ 2 đào sâu thực tế',
          transcriptWhisper: 'Câu trả lời phụ 1 đã lưu',
          audioObjectKey: 'official-exams/shift-1/ticket-1/05_SE170001_Q01_FU1.webm',
          audioR2Url: null,
          audioHashSha256: 'b'.repeat(64),
          aiScore: null,
          finalScore: null,
        },
      ],
    })

    render(<OfficialExamPage />)

    expect(await screen.findByText('Câu phụ 2 đào sâu thực tế')).toBeInTheDocument()
    expect(screen.queryByText(/Câu hỏi phụ #0/)).not.toBeInTheDocument()
    expect(screen.getByText(/Clean Architecture · medium · Đã lưu R2/)).toBeInTheDocument()
    expect(screen.getByText(/Câu hỏi phụ #1 · Đã lưu R2/)).toBeInTheDocument()
    expect(screen.getByText('Câu phụ 1 cần giải thích thêm')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Thu âm câu phụ #2/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Thu âm câu phụ #1/ })).not.toBeInTheDocument()
  })

  it('unblocks BYOD rebind/resume when ticket is IN_PROGRESS with deviceId null (after EO unlock) or empty questions (after crash/lost sessionStorage)', async () => {
    vi.mocked(kioskExamService.getCurrentTicket)
      .mockResolvedValueOnce({
        ...baseTicket,
        status: 'IN_PROGRESS',
        deviceId: null,
        isExamPublished: true,
        questions: [],
      })
      .mockResolvedValueOnce({
        ...baseTicket,
        status: 'IN_PROGRESS',
        deviceId: webKioskBridge.getDeviceId(),
        isExamPublished: true,
      })
    vi.mocked(kioskExamService.getTicketState)
      .mockResolvedValueOnce({
        ticketId: 'ticket-1',
        shiftId: 'shift-1',
        status: 'IN_PROGRESS',
        isExamPublished: true,
        isKioskLocked: false,
        violationCount: 0,
        maxViolations: 3,
        lockThreshold: 4,
        remainingStrikes: 4,
        serverNowUtc: '2026-11-20T08:12:00.000Z',
        deviceId: null,
        deviceLockedAt: null,
        deviceUnlockCount: 1,
        followUps: [],
      })
      .mockResolvedValueOnce({
        ticketId: 'ticket-1',
        shiftId: 'shift-1',
        status: 'IN_PROGRESS',
        isExamPublished: true,
        isKioskLocked: false,
        violationCount: 0,
        maxViolations: 3,
        lockThreshold: 4,
        remainingStrikes: 4,
        serverNowUtc: '2026-11-20T08:12:30.000Z',
        deviceId: webKioskBridge.getDeviceId(),
        deviceLockedAt: '2026-11-20T08:12:30.000Z',
        deviceUnlockCount: 1,
        followUps: [],
      })
    vi.mocked(kioskExamService.checkInTicket).mockResolvedValueOnce({
      id: 'ticket-1',
      shiftId: 'shift-1',
      studentId: 'student-1',
      studentCode: 'SE170001',
      seatNumber: 5,
      ipAddress: null,
      status: 'IN_PROGRESS',
      checkedInAt: '2026-11-20T08:12:30.000Z',
      examSetId: 'set-1',
      deviceId: webKioskBridge.getDeviceId(),
      deviceLockedAt: '2026-11-20T08:12:30.000Z',
      deviceUnlockCount: 1,
      activeSessionToken: 'rebound-session-token-999',
    })

    render(<OfficialExamPage />)

    const rebindButton = await screen.findByRole('button', {
      name: /Khôi phục phiên \/ Gắn lại thiết bị BYOD/,
    })
    fireEvent.click(rebindButton)

    await waitFor(() =>
      expect(kioskExamService.checkInTicket).toHaveBeenCalledWith(
        'ticket-1',
        expect.objectContaining({ deviceId: webKioskBridge.getDeviceId() }),
        expect.any(Object),
      ),
    )
    expect(webKioskBridge.getSessionToken('ticket-1')).toBe('rebound-session-token-999')
    expect(await screen.findByRole('button', { name: /Kiểm tra Microphone/ })).toBeInTheDocument()
  })

  it('sends finalizePerTurn=true on per-turn final submission and locks UI on TICKET_LOCKED', async () => {
    const stopRec = vi.fn().mockResolvedValue(new Blob(['audio-turn-1'], { type: 'audio/webm' }))
    vi.mocked(useAudioRecorder).mockReturnValue({
      isRecording: true,
      error: null,
      startRecording,
      stopRecording: stopRec,
      cancelRecording,
    } as never)

    vi.mocked(kioskExamService.getCurrentTicket).mockResolvedValue({
      ...baseTicket,
      hasFollowUp: true,
      maxFollowUpQuestions: 2,
    })
    vi.mocked(kioskExamService.getTicketState).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      status: 'IN_PROGRESS',
      isExamPublished: true,
      isKioskLocked: false,
      violationCount: 0,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 4,
      serverNowUtc: '2026-11-20T08:05:00.000Z',
      deviceId: 'BYOD-01',
      deviceLockedAt: '2026-11-20T08:00:00.000Z',
      deviceUnlockCount: 0,
      followUps: [
        {
          submissionId: 'sub-main-1',
          parentSubmissionId: null,
          examQuestionId: 'q-1',
          followUpIndex: 0,
          followUpPrompt: null,
          generatedFollowUpPrompt: null,
          transcriptWhisper: 'Câu trả lời đã lưu',
          audioObjectKey: 'official-exams/shift-1/ticket-1/05_SE170001_Q01.webm',
          audioR2Url: null,
          audioHashSha256: 'a'.repeat(64),
          aiScore: null,
          finalScore: null,
        },
      ],
    })
    vi.mocked(kioskExamService.submitExam)
      .mockRejectedValueOnce({
        status: 409,
        detail: 'CONFLICT: TICKET_LOCKED — Vé thi đã bị khóa do vi phạm quy chế.',
      })
      .mockResolvedValueOnce({
        ticketId: 'ticket-1',
        status: 'SUBMITTED',
        submittedAt: '2026-11-20T08:30:00.000Z',
        submissionCount: 1,
      })

    const { unmount } = render(<OfficialExamPage />)
    await screen.findByText(/Clean Architecture · medium · Đã lưu R2/)

    fireEvent.click(screen.getByRole('button', { name: /Nộp bài và niêm phong/ }))
    await waitFor(() =>
      expect(kioskExamService.submitExam).toHaveBeenCalledWith(
        'ticket-1',
        [],
        expect.objectContaining({ deviceId: expect.stringMatching(/^WEB-KIOSK-/) }),
        { finalizePerTurn: true },
      ),
    )
    expect(cancelRecording).toHaveBeenCalled()
    expect(await screen.findByText(/Vé thi đã bị khóa \(LOCKED\)/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Nộp bài và niêm phong/ })).not.toBeInTheDocument()
    unmount()

    render(<OfficialExamPage />)
    await screen.findByText(/Clean Architecture · medium · Đã lưu R2/)
    fireEvent.click(screen.getByRole('button', { name: /Nộp bài và niêm phong/ }))
    await waitFor(() =>
      expect(kioskExamService.submitExam).toHaveBeenLastCalledWith(
        'ticket-1',
        [],
        expect.objectContaining({ deviceId: expect.stringMatching(/^WEB-KIOSK-/) }),
        { finalizePerTurn: true },
      ),
    )
    expect(
      await screen.findByText(/Toàn bộ các lượt trả lời & câu phụ đã được chốt sổ và niêm phong an toàn/),
    ).toBeInTheDocument()
  })

  it('extracts TICKET_LOCKED from detail even when generic transport code ERR_BAD_REQUEST is present and omits sealed banners', async () => {
    const stopRec = vi.fn().mockResolvedValue(new Blob(['audio-turn-1'], { type: 'audio/webm' }))
    vi.mocked(useAudioRecorder).mockReturnValue({
      isRecording: true,
      error: null,
      startRecording,
      stopRecording: stopRec,
      cancelRecording,
    } as never)

    vi.mocked(kioskExamService.getCurrentTicket).mockResolvedValue({
      ...baseTicket,
      hasFollowUp: true,
      maxFollowUpQuestions: 2,
    })
    vi.mocked(kioskExamService.getTicketState).mockResolvedValue({
      ticketId: 'ticket-1',
      shiftId: 'shift-1',
      status: 'IN_PROGRESS',
      isExamPublished: true,
      isKioskLocked: false,
      violationCount: 0,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 4,
      serverNowUtc: '2026-11-20T08:05:00.000Z',
      deviceId: 'BYOD-01',
      deviceLockedAt: '2026-11-20T08:00:00.000Z',
      deviceUnlockCount: 0,
      followUps: [
        {
          submissionId: 'sub-main-1',
          parentSubmissionId: null,
          examQuestionId: 'q-1',
          followUpIndex: 0,
          followUpPrompt: null,
          generatedFollowUpPrompt: null,
          transcriptWhisper: 'Câu trả lời đã lưu',
          audioObjectKey: 'official-exams/shift-1/ticket-1/05_SE170001_Q01.webm',
          audioR2Url: null,
          audioHashSha256: 'a'.repeat(64),
          aiScore: null,
          finalScore: null,
        },
      ],
    })
    vi.mocked(kioskExamService.submitExam).mockRejectedValueOnce({
      status: 409,
      code: 'ERR_BAD_REQUEST',
      detail: 'CONFLICT: TICKET_LOCKED — Vé thi đã bị khóa do vi phạm quy chế.',
    })

    render(<OfficialExamPage />)
    await screen.findByText(/Clean Architecture · medium · Đã lưu R2/)

    fireEvent.click(screen.getByRole('button', { name: /Nộp bài và niêm phong/ }))

    expect(await screen.findByText(/Vé thi đã bị khóa \(LOCKED\)/)).toBeInTheDocument()
    expect(screen.queryByText(/Bài thi đã được tiếp nhận và niêm phong an toàn/)).not.toBeInTheDocument()
    expect(screen.queryByText(/Bản ghi âm đã được tải lên và niêm phong mã băm SHA-256/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Nộp bài và niêm phong/ })).not.toBeInTheDocument()
  })
})
