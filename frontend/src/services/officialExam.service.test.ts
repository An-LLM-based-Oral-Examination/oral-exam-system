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
        examinerUserId: 'examiner-1',
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
      examinerUserId: 'examiner-1',
    })
  })

  it('normalizes official sessions when examStructureId is null', async () => {
    vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: [{ ...sessionPayload, examStructureId: null }],
    } as never)

    await expect(officialExamService.getSessions()).resolves.toMatchObject([
      { id: sessionId, examStructureId: '' },
    ])
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

  it('sends the same roster file for preview and commit and preserves backend fullName/setCode', async () => {
    const file = new File(['studentCode,seatNumber,ipAddress\nSE170001,1,10.0.0.1'], 'roster.csv', {
      type: 'text/csv',
    })
    const post = vi
      .spyOn(apiClient, 'post')
      .mockResolvedValueOnce({
        data: {
          shiftId,
          totalRowCount: 1,
          validRowCount: 1,
          errorRowCount: 0,
          availableHandedOverSetCount: 1,
          rows: [
            {
              sourceRow: 2,
              studentCode: 'SE170001',
              fullName: 'Nguyen An',
              seatNumber: 1,
              ipAddress: '10.0.0.1',
              setCode: 'SET-01',
              isValid: true,
              errors: [],
            },
          ],
          globalErrors: [],
          canCommit: true,
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          shiftId,
          createdTicketCount: 1,
          tickets: [
            {
              id: ticketId,
              shiftId,
              studentId: 'student-1',
              studentCode: 'SE170001',
              seatNumber: 1,
              ipAddress: '10.0.0.1',
              status: 'SCHEDULED',
              checkedInAt: '2026-11-20T07:55:00.000Z',
              examSetId: 'set-1',
            },
          ],
        },
      } as never)

    await expect(officialExamService.previewTicketRosterImport(shiftId, file)).resolves.toMatchObject({
      rows: [{ fullName: 'Nguyen An', setCode: 'SET-01' }],
      canCommit: true,
    })
    await expect(officialExamService.commitTicketRosterImport(shiftId, file)).resolves.toMatchObject({
      createdTicketCount: 1,
      tickets: [{ studentCode: 'SE170001' }],
    })

    expect(post).toHaveBeenCalledTimes(2)
    expect((post.mock.calls[0][1] as FormData).get('file')).toBe(file)
    expect((post.mock.calls[1][1] as FormData).get('file')).toBe(file)
  })

  it('downloads the candidate import template through the Examination Officer template endpoint', async () => {
    const blob = new Blob(['studentCode\nSE170001\n'], { type: 'text/csv' })
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: blob,
      headers: {
        'content-type': 'text/csv',
        'content-disposition': 'attachment; filename="exam-candidates-template.csv"',
      },
    } as never)

    await expect(officialExamService.downloadTicketRosterTemplate('csv')).resolves.toMatchObject({
      blob,
      fileName: 'exam-candidates-template.csv',
      contentType: 'text/csv',
    })
    expect(get).toHaveBeenCalledWith('/official-exams/candidates/import/template', {
      params: { format: 'csv' },
      responseType: 'blob',
    })
  })

  it('normalizes anti-cheat logs without crashing when clientEventId is absent and preserves explicit values', async () => {
    const logIdWithoutClient = '00000000-0000-4000-8000-000000000091'
    const logIdWithClient = '00000000-0000-4000-8000-000000000092'
    vi.spyOn(apiClient, 'get').mockResolvedValue({
      data: [
        {
          id: logIdWithoutClient,
          ticketId,
          shiftId,
          studentId: 'student-1',
          studentCode: 'SE170001',
          studentName: 'Nguyen Van A',
          eventType: 'WINDOW_BLUR',
          severity: 'HIGH',
          details: 'Mất tiêu điểm cửa sổ',
          violationCount: 1,
          occurredAt: '2026-11-20T08:10:00.000Z',
        },
        {
          id: logIdWithClient,
          ticketId,
          shiftId,
          studentId: 'student-1',
          clientEventId: 'client-evt-uuid-1',
          studentCode: 'SE170001',
          studentName: 'Nguyen Van A',
          eventType: 'WINDOW_BLUR',
          severity: 'CRITICAL',
          details: 'Vi phạm lần 3',
          violationCount: 3,
          occurredAt: '2026-11-20T08:12:00.000Z',
        },
      ],
    } as never)

    const logs = await officialExamService.getAntiCheatLogs(shiftId)
    expect(logs).toHaveLength(2)
    expect(logs[0]).toMatchObject({
      id: logIdWithoutClient,
      clientEventId: logIdWithoutClient,
      violationCount: 1,
    })
    expect(logs[1]).toMatchObject({
      id: logIdWithClient,
      clientEventId: 'client-evt-uuid-1',
      violationCount: 3,
    })
  })

  it('calls all Sprint 1 6-step EO & DH Follow-up endpoints with synchronized contracts', async () => {
    const semesterId = '00000000-0000-4000-8000-000000000030'
    const studentId = '00000000-0000-4000-8000-000000000031'
    const file = new File(['MSSV\nSE170001\n'], 'candidates.csv', { type: 'text/csv' })
    const templateBlob = new Blob(['MSSV\nSE170001\n'], { type: 'text/csv' })

    const get = vi
      .spyOn(apiClient, 'get')
      .mockResolvedValueOnce({
        data: [
          {
            courseId: 'course-1',
            courseCode: 'PRN231',
            courseName: 'Backend',
            semesterId,
            semesterCode: 'FA26',
            activeClassCount: 2,
            eligibleStudentCount: 50,
            maxStudentsPerRoom: 24,
            recommendedRoomCount: 3,
            activeExamStructureId: 'struct-1',
            activeExamStructureName: 'Final Structure',
            handedOverExamSetCount: 24,
            hasFollowUp: true,
            maxFollowUpQuestions: 2,
          },
        ],
      } as never)
      .mockResolvedValueOnce({
        data: {
          sessionId,
          examAttemptType: 'FE',
          totalCount: 1,
          allocatedCount: 0,
          unallocatedCount: 1,
          pageNumber: 1,
          pageSize: 50,
          items: [
            {
              id: 'cand-1',
              sessionId,
              studentId,
              studentCode: 'SE170001',
              fullName: 'Nguyen Van A',
              isAllocated: false,
            },
          ],
        },
      } as never)
      .mockResolvedValueOnce({
        data: templateBlob,
        headers: { 'content-type': 'text/csv' },
      } as never)
      .mockResolvedValueOnce({
        data: {
          sessionId,
          courseId: 'course-1',
          examAttemptType: 'FE',
          hasImportedCandidates: true,
          eligibleStudentCount: 50,
          allocatedStudentCount: 0,
          unallocatedStudentCount: 50,
          maxStudentsPerRoom: 24,
          currentShiftRoomCount: 0,
          currentTotalCapacity: 0,
          currentAvailableSeats: 0,
          recommendedTotalRooms: 3,
          recommendedAdditionalRooms: 3,
          handedOverExamSetCount: 24,
        },
      } as never)

    const post = vi
      .spyOn(apiClient, 'post')
      .mockResolvedValueOnce({
        data: { semesterId, createdCount: 1, skippedCount: 0, sessions: [sessionPayload] },
      } as never)
      .mockResolvedValueOnce({
        data: {
          sessionId,
          examAttemptType: 'FE',
          mode: 'REPLACE',
          totalRows: 1,
          validCount: 1,
          invalidCount: 0,
          duplicateCount: 0,
          canCommit: true,
          existingCandidateCount: 0,
          projectedTotalCandidates: 1,
          maxStudentsPerRoom: 24,
          recommendedTotalRooms: 1,
          rows: [],
          issues: [],
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          sessionId,
          examAttemptType: 'FE',
          mode: 'REPLACE',
          importedCount: 1,
          totalCandidateCount: 1,
          recommendedTotalRooms: 1,
          importedAt: '2026-11-20T08:00:00.000Z',
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          sessionId,
          shiftGroupName: 'Ca 1',
          createdRoomCount: 1,
          totalCapacityCreated: 24,
          shifts: [shiftPayload],
        },
      } as never)
      .mockResolvedValueOnce({
        data: {
          sessionId,
          dryRun: true,
          totalEligibleStudents: 50,
          previouslyAllocatedCount: 0,
          newlyAllocatedCount: 50,
          shiftGroups: [],
        },
      } as never)
      .mockResolvedValueOnce({
        data: { shiftId, totalDistributed: 20, message: 'OK', distributedTickets: [] },
      } as never)

    const put = vi
      .spyOn(apiClient, 'put')
      .mockResolvedValueOnce({ data: { ...shiftPayload, status: 'in_progress' } } as never)
      .mockResolvedValueOnce({
        data: { ...sessionPayload, hasFollowUp: true, maxFollowUpQuestions: 3 },
      } as never)

    const del = vi.spyOn(apiClient, 'delete').mockResolvedValueOnce({ data: undefined } as never)

    await expect(officialExamService.getEligibleCourses(semesterId)).resolves.toHaveLength(1)
    await expect(
      officialExamService.batchCreateSessions(semesterId, {
        examDate: '2026-11-20',
        examAttemptType: 'FE',
        transcriptBufferSeconds: 60,
        courses: [{ courseId: 'course-1', examAttemptType: 'FE' }],
      }),
    ).resolves.toMatchObject({ createdCount: 1 })
    await expect(
      officialExamService.previewCandidateImport(sessionId, file, 'REPLACE'),
    ).resolves.toMatchObject({ canCommit: true, recommendedTotalRooms: 1 })
    await expect(
      officialExamService.commitCandidateImport(sessionId, file, 'REPLACE'),
    ).resolves.toMatchObject({ importedCount: 1 })
    await expect(officialExamService.getSessionCandidates(sessionId)).resolves.toMatchObject({
      totalCount: 1,
    })
    await expect(
      officialExamService.removeSessionCandidate(sessionId, studentId),
    ).resolves.toBeUndefined()
    await expect(officialExamService.downloadCandidateTemplate('csv')).resolves.toMatchObject({
      blob: templateBlob,
    })
    await expect(officialExamService.getEnrollmentEstimate(sessionId)).resolves.toMatchObject({
      recommendedTotalRooms: 3,
    })
    await expect(
      officialExamService.batchOpenShifts(sessionId, {
        shiftGroupName: 'Ca 1',
        startTime: shiftPayload.startTime,
        endTime: shiftPayload.endTime,
        rooms: [
          {
            roomLab: 'Lab 301',
            maxSeats: 24,
            proctorUserId: 'proctor-1',
            examinerUserId: 'examiner-1',
          },
        ],
      }),
    ).resolves.toMatchObject({ createdRoomCount: 1 })
    await expect(
      officialExamService.autoAllocateStudents(sessionId, {
        assignHandedOverExamSets: true,
        dryRun: true,
      }),
    ).resolves.toMatchObject({ newlyAllocatedCount: 50 })
    await expect(officialExamService.distributeShiftTickets(shiftId)).resolves.toMatchObject({
      totalDistributed: 20,
    })
    await expect(
      officialExamService.updateShiftStatus(shiftId, 'in_progress'),
    ).resolves.toMatchObject({ status: 'in_progress' })
    await expect(
      officialExamService.configureFollowUp(sessionId, {
        hasFollowUp: true,
        maxFollowUpQuestions: 3,
      }),
    ).resolves.toMatchObject({ hasFollowUp: true, maxFollowUpQuestions: 3 })

    expect(get).toHaveBeenCalledWith(`/official-exams/semesters/${semesterId}/eligible-courses`)
    expect(post).toHaveBeenCalledWith(
      `/official-exams/semesters/${semesterId}/sessions/batch`,
      expect.any(Object),
    )
    expect(del).toHaveBeenCalledWith(
      `/official-exams/sessions/${sessionId}/candidates/${studentId}`,
    )
    expect(put).toHaveBeenCalledWith(`/official-exams/shifts/${shiftId}/status`, {
      status: 'in_progress',
    })
    expect(put).toHaveBeenCalledWith(`/official-exams/sessions/${sessionId}/follow-up-config`, {
      hasFollowUp: true,
      maxFollowUpQuestions: 3,
    })
  })

  it('calls EO-only BYOD unlock-device endpoint with trimmed reason', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValueOnce({
      data: {
        ticketId,
        shiftId,
        studentCode: 'SE170001',
        seatNumber: 4,
        previousDeviceId: 'BYOD-OLD-123456789',
        deviceUnlockCount: 1,
        unlockedBy: 'eo-1',
        unlockedAt: '2026-11-20T08:15:00.000Z',
        reason: 'Laptop cá nhân bị hỏng nguồn giữa giờ.',
      },
    } as never)

    await expect(
      officialExamService.unlockTicketDevice(ticketId, '  Laptop cá nhân bị hỏng nguồn giữa giờ.  '),
    ).resolves.toMatchObject({
      ticketId,
      previousDeviceId: 'BYOD-OLD-123456789',
      deviceUnlockCount: 1,
    })
    expect(post).toHaveBeenCalledWith(`/official-exams/tickets/${ticketId}/unlock-device`, {
      reason: 'Laptop cá nhân bị hỏng nguồn giữa giờ.',
    })
  })
})
