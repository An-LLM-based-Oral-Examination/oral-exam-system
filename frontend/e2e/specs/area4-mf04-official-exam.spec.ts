import { expect, test } from '../fixtures/test'
import { E2E_IDS, makeUser, makeUuid } from '../helpers/mock-data'

const sessionId = makeUuid('0f04', 1)
const shiftId = makeUuid('0f04', 2)
const ticketId = makeUuid('0f04', 3)
const examSetId = makeUuid('0f04', 4)
const questionId = makeUuid('0f04', 5)
const mainSubmissionId = makeUuid('0f04', 6)

test.describe('Area 4 — MF-04 Official Exam End-to-End Flows', () => {
  test('EO @wave02 views 24-seat room estimate and defaults RequestExamPreparationDialog from recommendedTotalRooms', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    const lecturer = makeUser('Instructor')
    const proctor = makeUser('Proctor')

    await authSession.seedSession('ExaminationOfficer')
    await apiMocks.mockJson('GET', '/api/v1/official-exams/sessions', 200, [
      {
        id: sessionId,
        courseId: E2E_IDS.course,
        courseCode: 'PRN231',
        courseName: 'Building Cross-Platform Back-End Applications',
        semesterId: E2E_IDS.semester,
        examAttemptType: 'FE',
        examStructureId: E2E_IDS.examStructure,
        title: 'PRN231 · Fall 2026 · FE',
        examDate: '2099-11-20',
        hasFollowUp: false,
        maxFollowUpQuestions: 2,
        examInputMode: 'VoiceWithTranscriptEdit',
        transcriptBufferSeconds: 60,
        status: 'scheduled',
      },
    ])
    await apiMocks.mockJson('GET', '/api/v1/official-exams/shifts*', 200, [])
    await apiMocks.mockJson(
      'GET',
      `/api/v1/official-exams/sessions/${sessionId}/enrollment-estimate`,
      200,
      {
        sessionId,
        courseId: E2E_IDS.course,
        courseCode: 'PRN231',
        courseName: 'Building Cross-Platform Back-End Applications',
        semesterId: E2E_IDS.semester,
        semesterCode: 'FA26',
        examAttemptType: 'FE',
        candidateSource: 'imported',
        hasImportedCandidates: true,
        importedCandidateCount: 24,
        semesterEnrolledCount: 24,
        eligibleStudentCount: 24,
        allocatedStudentCount: 0,
        unallocatedStudentCount: 24,
        maxStudentsPerRoom: 24,
        recommendedTotalRooms: 1,
        recommendedAdditionalRooms: 1,
        currentShiftRoomCount: 0,
        currentTotalCapacity: 0,
        currentAvailableSeats: 0,
        handedOverExamSetCount: 0,
      },
    )
    await apiMocks.mockJson(
      'GET',
      `/api/v1/official-exams/sessions/${sessionId}/candidates*`,
      200,
      {
        sessionId,
        examAttemptType: 'FE',
        totalCount: 24,
        allocatedCount: 0,
        unallocatedCount: 24,
        pageNumber: 1,
        pageSize: 100,
        items: [],
      },
    )
    await apiMocks.mockJson('GET', `/api/v1/courses/${E2E_IDS.course}/lecturers`, 200, [
      {
        lecturerId: lecturer.id,
        fullName: lecturer.fullName,
        email: lecturer.email,
        semesterId: E2E_IDS.semester,
      },
    ])
    await apiMocks.mockJson('GET', '/api/v1/semesters/classes*', 200, [])
    await apiMocks.mockJson('GET', '/api/v1/examination-officer/appeals/allocation-proposal', 200, [])
    await apiMocks.mockJson(
      'GET',
      `/api/v1/courses/${E2E_IDS.course}/shift-staff-candidates*`,
      200,
      {
        proctors: [{ userId: proctor.id, fullName: proctor.fullName, detail: proctor.email }],
        examiners: [{ userId: lecturer.id, fullName: lecturer.fullName, detail: lecturer.email }],
        departmentHeadIds: [E2E_IDS.users.DepartmentHead],
      },
    )
    await apiMocks.mockJson(
      'POST',
      `/api/v1/official-exams/sessions/${sessionId}/preparation`,
      201,
      {
        id: makeUuid('0f04', 99),
        sessionId,
        requiredSetCount: 4,
        deadline: '2099-11-18T10:00:00.000Z',
      },
    )

    await page.goto(`/examination-officer/exams/${sessionId}`, { waitUntil: 'domcontentloaded' })
    await expect(page.getByText(/PRN231 · Fall 2026 · FE/)).toBeVisible()
    await expect(page.getByText(/Bước 2 · Import Danh sách Thí sinh dự thi/)).toBeVisible()

    await page.getByRole('button', { name: /Yêu cầu chuẩn bị đề/ }).click()
    const prepDialog = page.getByRole('dialog')
    await expect(prepDialog).toBeVisible()

    const setCountInput = prepDialog.getByLabel(/Số bộ đề cần chuẩn bị/)
    await expect(setCountInput).toHaveValue('2')

    await setCountInput.fill('4')
    await prepDialog.getByLabel(/Hạn bàn giao bộ đề/).fill('2099-11-18T17:00')
    await prepDialog.getByLabel(/Ghi chú cho Trưởng Bộ môn/).fill('Chuẩn bị 4 bộ đề thi FE.')
    await prepDialog.getByRole('button', { name: /Gửi yêu cầu cho Trưởng BM/ }).click()

    await expect(prepDialog).toBeHidden()
    const prepRequests = apiMocks.getRequests(`/api/v1/official-exams/sessions/${sessionId}/preparation`)
    expect(prepRequests).toHaveLength(1)
    expect(prepRequests[0]?.body).toMatchObject({
      requiredSetCount: 4,
      notes: 'Chuẩn bị 4 bộ đề thi FE.',
    })
  })

  test('Proctor @wave02 marks seat attendance and logs anti-cheat violation without grade/unlock actions', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    const student = makeUser('Student')
    let isCheckedIn = false

    await authSession.seedSession('Proctor')
    await apiMocks.mockJson('GET', '/api/v1/official-exams/shifts*', 200, [
      {
        id: shiftId,
        sessionId,
        shiftName: 'Ca 1 · PRN231 FE',
        roomLab: 'LAB-301',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        proctorUserId: E2E_IDS.users.Proctor,
        examinerUserId: E2E_IDS.users.Instructor,
        status: 'in_progress',
        isExamPublished: true,
        maxSeats: 24,
      },
    ])

    await page.route(`**/api/v1/proctor/shifts/${shiftId}/attendance`, async (route) => {
      if (route.request().method() !== 'GET') return route.fallback()
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          shiftId,
          shiftName: 'Ca 1 · PRN231 FE',
          roomLab: 'LAB-301',
          shiftStatus: 'in_progress',
          startTime: '2026-11-20T08:00:00.000Z',
          endTime: '2026-11-20T09:30:00.000Z',
          totalSeats: 24,
          assignedTicketsCount: 1,
          checkedInCount: isCheckedIn ? 1 : 0,
          inProgressCount: isCheckedIn ? 1 : 0,
          submittedCount: 0,
          suspendedCount: 0,
          seats: [
            {
              seatNumber: 1,
              isOccupied: true,
              ticketId,
              studentId: student.id,
              studentCode: 'SE180001',
              fullName: student.fullName,
              deviceId: 'BYOD-SE180001-DEVICE',
              violationCount: 0,
              examSetId,
              examSetCode: 'PRN-FE-01',
              status: isCheckedIn ? 'IN_PROGRESS' : 'SCHEDULED',
              isCheckedIn,
              isSuspended: false,
            },
          ],
        }),
      })
    })

    await page.route(`**/api/v1/proctor/tickets/${ticketId}/attendance`, async (route) => {
      if (route.request().method() !== 'PUT') return route.fallback()
      isCheckedIn = true
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ticketId,
          shiftId,
          studentId: student.id,
          studentCode: 'SE180001',
          fullName: student.fullName,
          seatNumber: 1,
          examSetId,
          examSetCode: 'PRN-FE-01',
          status: 'IN_PROGRESS',
          isSuspended: false,
          updatedAt: '2026-11-20T08:05:00.000Z',
        }),
      })
    })

    await apiMocks.mockJson(
      'GET',
      `/api/v1/official-exams/shifts/${shiftId}/anti-cheat/logs`,
      200,
      [],
    )
    await apiMocks.mockJson('POST', `/api/v1/proctor/shifts/${shiftId}/violations`, 201, {
      id: makeUuid('0f04', 20),
      shiftId,
      ticketId,
      studentId: student.id,
      eventType: 'PROCTOR_REPORTED_VIOLATION',
      severity: 'HIGH',
      details: 'Thí sinh mở tài liệu giấy trên bàn thi.',
      occurredAt: '2026-11-20T08:12:00.000Z',
    })

    await page.goto(`/proctor/attendance?shiftId=${shiftId}`, { waitUntil: 'domcontentloaded' })
    await expect(page.getByText('SE180001')).toBeVisible()

    await page.getByRole('button', { name: /#01.*SE180001/ }).click()
    await expect(page.getByRole('button', { name: 'Điểm danh có mặt' })).toBeVisible()
    await expect(page.getByRole('button', { name: /Công bố điểm/ })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /Đình chỉ vé/ })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /Mở khóa máy/ })).toHaveCount(0)

    await page.getByRole('button', { name: 'Điểm danh có mặt' }).click()
    await expect(page.getByText('Đã điểm danh ghế 1.')).toBeVisible()

    await page.getByRole('button', { name: /#01.*SE180001/ }).click()
    await page
      .getByPlaceholder('Mô tả hành vi vi phạm tại máy trạm...')
      .fill('Thí sinh mở tài liệu giấy trên bàn thi.')
    await page.getByRole('button', { name: 'Báo cáo vi phạm' }).click()
    await expect(page.getByText(/Đã ghi nhận biên bản vi phạm tại ghế #1/)).toBeVisible()

    const violationRequests = apiMocks.getRequests(`/api/v1/proctor/shifts/${shiftId}/violations`)
    expect(violationRequests).toHaveLength(1)
    expect(violationRequests[0]?.body).toMatchObject({
      ticketId,
      eventType: 'PROCTOR_REPORTED_VIOLATION',
      severity: 'HIGH',
      details: 'Thí sinh mở tài liệu giấy trên bàn thi.',
    })
  })

  test('Lecturer @wave02 audits MF-04 evidence and overrides score with reason without grade publish button', async ({
    page,
    authSession,
    apiMocks,
  }) => {
    const student = makeUser('Student')
    await authSession.seedSession('Instructor')

    await apiMocks.mockJson('GET', '/api/v1/official-exams/shifts*', 200, [
      {
        id: shiftId,
        sessionId,
        shiftName: 'Ca 1 · PRN231 FE',
        roomLab: 'LAB-301',
        startTime: '2026-11-20T08:00:00.000Z',
        endTime: '2026-11-20T09:30:00.000Z',
        proctorUserId: E2E_IDS.users.Proctor,
        examinerUserId: E2E_IDS.users.Instructor,
        status: 'completed',
        isExamPublished: true,
      },
    ])
    await apiMocks.mockJson(
      'GET',
      `/api/v1/official-exams/shifts/${shiftId}/audit-evidence`,
      200,
      [
        {
          ticketId,
          studentCode: 'SE180001',
          fullName: student.fullName,
          seatNumber: 1,
          status: 'AI_GRADED',
          audioR2Url: null,
          audioHashSha256: 'a'.repeat(64),
          transcriptWhisper: 'Dependency Inversion giúp tách tầng Application khỏi Infrastructure.',
          aiScore: 6.5,
          confidenceScore: 0.62,
          isSuspicious: true,
          suspiciousReason: 'Độ tin cậy AI dưới 70%, cần giảng viên hậu kiểm.',
          cotTrace: 'Thí sinh nêu được ý chính nhưng thiếu ví dụ Interface.',
          finalScore: 6.5,
          isLocked: false,
        },
      ],
    )
    await apiMocks.mockJson('PUT', `/api/v1/official-exams/tickets/${ticketId}/override-score`, 200, {
      auditId: makeUuid('0f04', 30),
      ticketId,
      lecturerId: E2E_IDS.users.Instructor,
      originalAiScore: 6.5,
      auditedScore: 8.0,
      overrideReason: 'Đã đối chiếu audio: thí sinh giải thích đầy đủ DIP và ví dụ DI.',
      ticketStatus: 'AUDITED',
      isLocked: false,
      auditedAt: '2026-11-20T10:00:00.000Z',
    })

    await page.goto(`/lecturer/audit?shiftId=${shiftId}`, { waitUntil: 'domcontentloaded' })
    await expect(page.getByText('SE180001').first()).toBeVisible()
    await expect(page.getByText(/Độ tin cậy AI dưới 70%/)).toBeVisible()
    await expect(page.getByRole('button', { name: /Công bố điểm/ })).toHaveCount(0)

    await page.getByLabel(/Điểm chính thức/).fill('8')
    await page
      .getByPlaceholder(/Đối chiếu audio, transcript và từng tiêu chí rubric/)
      .fill('Đã đối chiếu audio: thí sinh giải thích đầy đủ DIP và ví dụ DI.')
    await page.getByRole('button', { name: 'Lưu điểm hậu kiểm' }).click()

    const overrideRequests = apiMocks.getRequests(
      `/api/v1/official-exams/tickets/${ticketId}/override-score`,
    )
    expect(overrideRequests).toHaveLength(1)
    expect(overrideRequests[0]?.body).toMatchObject({
      ticketId,
      auditedScore: 8,
      overrideReason: 'Đã đối chiếu audio: thí sinh giải thích đầy đủ DIP và ví dụ DI.',
    })
  })

  test('Student @wave02 completes Web Kiosk check-in, mic test, per-turn submit with finalizePerTurn=true', async ({
    page,
    authSession,
    apiMocks,
    mediaMock,
  }) => {
    await authSession.seedSession('Student')
    await mediaMock.enableMicStream(0.05)

    let checkedIn = false

    await page.route('**/api/v1/official-exams/tickets/current', async (route) => {
      if (route.request().method() !== 'GET') return route.fallback()
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: ticketId,
          shiftId,
          shiftName: 'Ca 1 · PRN231 FE',
          roomLab: 'LAB-301',
          startTime: '2026-11-20T08:00:00.000Z',
          endTime: '2026-11-20T09:30:00.000Z',
          shiftStatus: 'in_progress',
          sessionTitle: 'PRN231 Final Oral Exam',
          examAttemptType: 'FE',
          studentId: E2E_IDS.users.Student,
          seatNumber: 5,
          status: checkedIn ? 'IN_PROGRESS' : 'SCHEDULED',
          examSetId,
          examSetCode: 'PRN-FE-01',
          examInputMode: 'VoiceOnly',
          transcriptBufferSeconds: 60,
          hasFollowUp: true,
          maxFollowUpQuestions: 2,
          isExamPublished: true,
          isKioskLocked: false,
          deviceId: checkedIn ? 'WEB-KIOSK-E2E-DEVICE-0001' : null,
          deviceLockedAt: checkedIn ? '2026-11-20T08:01:00.000Z' : null,
          deviceUnlockCount: 0,
          violationCount: 0,
          maxViolations: 3,
          lockThreshold: 4,
          remainingStrikes: 4,
          serverNowUtc: '2026-11-20T08:05:00.000Z',
          followUps: [],
          questions: checkedIn
            ? [
                {
                  questionId,
                  orderIndex: 1,
                  title: 'Dependency Inversion Principle',
                  content: 'Trình bày nguyên lý DIP trong Clean Architecture.',
                  bloomLevel: 'Understand',
                  difficulty: 'medium',
                  imageUrl: null,
                  imageDescription: null,
                },
              ]
            : [],
        }),
      })
    })

    await page.route(`**/api/v1/official-exams/tickets/${ticketId}`, async (route) => {
      if (route.request().method() !== 'GET') return route.fallback()
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ticketId,
          shiftId,
          status: checkedIn ? 'IN_PROGRESS' : 'SCHEDULED',
          isExamPublished: true,
          isKioskLocked: false,
          violationCount: 0,
          maxViolations: 3,
          lockThreshold: 4,
          remainingStrikes: 4,
          serverNowUtc: '2026-11-20T08:05:00.000Z',
          deviceId: checkedIn ? 'WEB-KIOSK-E2E-DEVICE-0001' : null,
          deviceLockedAt: checkedIn ? '2026-11-20T08:01:00.000Z' : null,
          deviceUnlockCount: 0,
          followUps: [],
        }),
      })
    })

    await page.route(`**/api/v1/official-exams/tickets/${ticketId}/check-in`, async (route) => {
      if (route.request().method() !== 'POST') return route.fallback()
      checkedIn = true
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: ticketId,
          shiftId,
          studentId: E2E_IDS.users.Student,
          studentCode: 'SE180001',
          seatNumber: 5,
          ipAddress: null,
          status: 'IN_PROGRESS',
          checkedInAt: '2026-11-20T08:01:00.000Z',
          examSetId,
          deviceId: 'WEB-KIOSK-E2E-DEVICE-0001',
          deviceLockedAt: '2026-11-20T08:01:00.000Z',
          deviceUnlockCount: 0,
          activeSessionToken: 'kiosk-session-token-e2e-1',
        }),
      })
    })

    await apiMocks.mockJson('POST', '/api/v1/speech/stt', 200, {
      transcript: 'Dependency Inversion Principle giúp giảm phụ thuộc trực tiếp.',
      timestampsJson: null,
      confidenceScore: 0.95,
    })
    await apiMocks.mockJson(
      'POST',
      `/api/v1/official-exams/tickets/${ticketId}/questions/${questionId}/audio-upload-authorizations`,
      200,
      {
        objectKey: `official-exams/${shiftId}/${ticketId}/05_SE180001_Q01.webm`,
        fileName: '05_SE180001_Q01.webm',
        uploadUrl: 'https://r2-mock.local/upload-q1',
        expiresAt: '2026-11-20T09:00:00.000Z',
        requiredHeaders: { 'Content-Type': 'audio/webm' },
      },
    )
    await page.route('https://r2-mock.local/**', (route) =>
      route.fulfill({ status: 200, body: '' }),
    )
    await apiMocks.mockJson(
      'PUT',
      `/api/v1/official-exams/tickets/${ticketId}/questions/${questionId}/answers`,
      200,
      {
        submissionId: mainSubmissionId,
        ticketId,
        questionId,
        followUpIndex: 0,
        audioHashSha256: 'a'.repeat(64),
        status: 'RECORDED',
        canRequestFollowUp: true,
      },
    )
    await apiMocks.mockJson(
      'POST',
      `/api/v1/official-exams/tickets/${ticketId}/submissions/${mainSubmissionId}/next-followup`,
      200,
      {
        hasMoreFollowUp: false,
        followUpIndex: 0,
        maxFollowUpQuestions: 2,
        parentSubmissionId: mainSubmissionId,
        questionId,
        followUpPrompt: null,
      },
    )
    await apiMocks.mockJson('PUT', `/api/v1/official-exams/tickets/${ticketId}/submission`, 202, {
      ticketId,
      status: 'SUBMITTED',
      submittedAt: '2026-11-20T08:15:00.000Z',
      submissionCount: 1,
      isKioskLocked: true,
    })

    await page.goto('/exam', { waitUntil: 'domcontentloaded' })
    await expect(page.getByText('PRN231 Final Oral Exam')).toBeVisible()

    await page.getByRole('button', { name: /Xác nhận nhận máy/ }).click()
    await expect(page.getByRole('button', { name: /Kiểm tra Microphone/ })).toBeVisible()

    await page.getByRole('button', { name: /Kiểm tra Microphone/ }).click()
    await expect(page.getByRole('button', { name: /Mic đạt chuẩn/ })).toBeVisible()

    await page.getByRole('button', { name: /Bắt đầu ghi âm/ }).click()
    await expect(page.getByText('Trình bày nguyên lý DIP trong Clean Architecture.')).toBeVisible()

    await page.getByRole('button', { name: /Thu âm trả lời câu này/ }).click()
    await page.getByRole('button', { name: /Dừng & Bóc băng câu này/ }).click()
    await page.getByRole('button', { name: /Lưu câu trả lời & Kiểm tra câu phụ/ }).click()
    await expect(page.getByText(/Dependency Inversion Principle · medium · Đã lưu R2/)).toBeVisible()

    await page.getByRole('button', { name: /Nộp bài và niêm phong/ }).click()
    await expect(
      page.getByText(/Toàn bộ các lượt trả lời & câu phụ đã được chốt sổ và niêm phong an toàn/),
    ).toBeVisible()

    const submitRequests = apiMocks.getRequests(
      `/api/v1/official-exams/tickets/${ticketId}/submission`,
    )
    expect(submitRequests.some((req) => req.method === 'PUT')).toBe(true)
    const putSubmit = submitRequests.find((req) => req.method === 'PUT')
    expect(putSubmit?.body).toEqual({ answers: [], finalizePerTurn: true })
  })

  test('Student @wave02 locks Web Kiosk UI immediately when submission returns 409 TICKET_LOCKED', async ({
    page,
    authSession,
    apiMocks,
    mediaMock,
  }) => {
    await authSession.seedSession('Student')
    await mediaMock.enableMicStream(0.05)

    await apiMocks.mockJson('GET', '/api/v1/official-exams/tickets/current', 200, {
      id: ticketId,
      shiftId,
      shiftName: 'Ca 1 · PRN231 FE',
      roomLab: 'LAB-301',
      startTime: '2026-11-20T08:00:00.000Z',
      endTime: '2026-11-20T09:30:00.000Z',
      shiftStatus: 'in_progress',
      sessionTitle: 'PRN231 Final Oral Exam',
      examAttemptType: 'FE',
      studentId: E2E_IDS.users.Student,
      seatNumber: 5,
      status: 'IN_PROGRESS',
      examSetId,
      examSetCode: 'PRN-FE-01',
      examInputMode: 'VoiceOnly',
      transcriptBufferSeconds: 60,
      hasFollowUp: true,
      maxFollowUpQuestions: 2,
      isExamPublished: true,
      isKioskLocked: false,
      deviceId: 'WEB-KIOSK-E2E-DEVICE-0001',
      deviceLockedAt: '2026-11-20T08:01:00.000Z',
      deviceUnlockCount: 0,
      violationCount: 0,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 4,
      serverNowUtc: '2026-11-20T08:05:00.000Z',
      followUps: [],
      questions: [
        {
          questionId,
          orderIndex: 1,
          title: 'Dependency Inversion Principle',
          content: 'Trình bày nguyên lý DIP trong Clean Architecture.',
          bloomLevel: 'Understand',
          difficulty: 'medium',
          imageUrl: null,
          imageDescription: null,
        },
      ],
    })
    await apiMocks.mockJson('GET', `/api/v1/official-exams/tickets/${ticketId}`, 200, {
      ticketId,
      shiftId,
      status: 'IN_PROGRESS',
      isExamPublished: true,
      isKioskLocked: false,
      violationCount: 0,
      maxViolations: 3,
      lockThreshold: 4,
      remainingStrikes: 4,
      serverNowUtc: '2026-11-20T08:05:00.000Z',
      deviceId: 'WEB-KIOSK-E2E-DEVICE-0001',
      deviceLockedAt: '2026-11-20T08:01:00.000Z',
      deviceUnlockCount: 0,
      followUps: [
        {
          submissionId: mainSubmissionId,
          parentSubmissionId: null,
          examQuestionId: questionId,
          followUpIndex: 0,
          followUpPrompt: null,
          generatedFollowUpPrompt: null,
          transcriptWhisper: 'Câu trả lời đã lưu',
          audioObjectKey: `official-exams/${shiftId}/${ticketId}/05_SE180001_Q01.webm`,
          audioR2Url: null,
          audioHashSha256: 'a'.repeat(64),
          aiScore: null,
          finalScore: null,
        },
      ],
    })
    await apiMocks.mockProblem(
      'PUT',
      `/api/v1/official-exams/tickets/${ticketId}/submission`,
      409,
      'CONFLICT: TICKET_LOCKED — Vé thi đã bị khóa do vi phạm quy chế hoặc đã niêm phong, không thể nộp bài.',
      { title: 'Xung đột trạng thái tài nguyên' },
    )

    await page.goto('/exam', { waitUntil: 'domcontentloaded' })
    await expect(page.getByRole('button', { name: /Kiểm tra Microphone/ })).toBeVisible()
    await page.getByRole('button', { name: /Kiểm tra Microphone/ }).click()
    await expect(page.getByRole('button', { name: /Mic đạt chuẩn/ })).toBeVisible()
    await page.getByRole('button', { name: /Bắt đầu ghi âm/ }).click()

    await page.getByRole('button', { name: /Nộp bài và niêm phong/ }).click()
    await expect(page.getByText(/Vé thi đã bị khóa \(LOCKED\)/)).toBeVisible()
    await expect(page.getByRole('button', { name: /Nộp bài và niêm phong/ })).toHaveCount(0)
  })
})
