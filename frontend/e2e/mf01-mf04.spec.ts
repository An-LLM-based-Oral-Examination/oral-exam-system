import { expect, test, type Page } from '@playwright/test'

const courseId = '00000000-0000-4000-8000-000000000001'
const sessionId = '00000000-0000-4000-8000-000000000003'
const appealId = '00000000-0000-4000-8000-000000000010'

type E2eRole = 'Student' | 'ExaminationOfficer' | 'Instructor'

interface BatchSubmitRequest {
  studentId: string
  answers: Array<{
    questionId: string
    answerText: string
    isFollowUp: boolean
    parentAnswerId: string | null
  }>
}

function createAuthenticatedUser(role: E2eRole) {
  return {
    id:
      role === 'Student'
        ? '00000000-0000-4000-8000-000000000002'
        : role === 'Instructor'
          ? 'lecturer-1'
          : 'eo-1',
    email: `${role.toLowerCase()}@e2e.test`,
    fullName: 'E2E Workflow User',
    role,
    isActive: true,
    mustChangePassword: false,
    ...(role === 'Student' ? { studentCode: 'SE170001' } : {}),
  }
}

async function mockAuthenticatedUser(page: Page, role: E2eRole) {
  const user = createAuthenticatedUser(role)
  await page.route('**/api/v1/auth/me**', async (route) => {
    if (route.request().method() !== 'GET') return route.continue()
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(user),
    })
  })
}

async function seedRole(page: Page, role: E2eRole) {
  await mockAuthenticatedUser(page, role)
  const user = createAuthenticatedUser(role)
  const profile = {
    id: user.id,
    email: user.email,
    fullName: user.fullName,
    role: user.role,
    mustChangePassword: user.mustChangePassword,
    ...(user.studentCode ? { studentCode: user.studentCode } : {}),
  }
  await page.addInitScript((seededProfile) => {
    sessionStorage.setItem('access_token', 'e2e-access-token')
    sessionStorage.setItem('refresh_token', 'e2e-refresh-token')
    sessionStorage.setItem('user_profile', JSON.stringify(seededProfile))
  }, profile)
}

test.describe('MF-01 practice workflow', () => {
  test('loads enrolled courses and starts a per-question session through the real route', async ({
    page,
  }) => {
    let startRequestBody: Record<string, unknown> | null = null
    await page.route('**/api/v1/student/classes/my-classes', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { classId: 'class-1', courseId, courseCode: 'PRN231', className: 'SE1801-PRN231' },
        ]),
      })
    })
    await page.route('**/api/v1/courses', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: courseId,
            code: 'PRN231',
            name: 'Building Cross-Platform Back-End Applications',
            credits: 3,
            transcriptBufferSeconds: 60,
            maxFollowUpQuestions: 2,
            hasFollowUp: true,
            examInputMode: 'VoiceWithTranscriptEdit',
            isActive: true,
          },
        ]),
      })
    })
    await page.route('**/api/v1/practice/student/history**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    })
    await page.route('**/api/v1/practice/sessions', async (route) => {
      if (route.request().method() !== 'POST') return route.continue()
      startRequestBody = route.request().postDataJSON() as Record<string, unknown>
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          sessionId,
          transcriptBufferSeconds: 60,
          questions: [
            {
              questionId: '00000000-0000-4000-8000-000000000004',
              content: 'Explain dependency inversion.',
              rubricCriteria: ['Definition'],
            },
          ],
        }),
      })
    })

    await seedRole(page, 'Student')
    await page.goto('/practice?course=PRN231')
    await expect(page.getByRole('combobox').first()).toHaveValue(courseId)

    const startButton = page.getByRole('button', { name: /B.*t.*luy.*t.*c/ })
    await expect(startButton).toBeEnabled()
    await startButton.click()
    await expect(page).toHaveURL(new RegExp(`/practice/session[?]session=${sessionId}`))
    await expect(page.getByRole('button', { name: /Bỏ qua/ })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Quay lại câu trước' })).toHaveCount(0)
    expect(startRequestBody).toMatchObject({ courseId, isFullSession: false })
  })

  test('completes a full-session batch with unanswered questions preserved as empty answers', async ({
    page,
  }) => {
    const fullSessionId = '00000000-0000-4000-8000-000000000020'
    const questions = [1, 2, 3].map((orderIndex) => ({
      questionId: `00000000-0000-4000-8000-0000000000${20 + orderIndex}`,
      content: `Explain architecture principle ${orderIndex}.`,
      rubricCriteria: ['Definition'],
    }))
    const batchRequest = { body: null as BatchSubmitRequest | null }

    await page.route(`**/api/v1/practice/sessions/${fullSessionId}/batch-submit`, async (route) => {
      batchRequest.body = route.request().postDataJSON() as BatchSubmitRequest
      await route.fulfill({ status: 202, body: '' })
    })
    await mockAuthenticatedUser(page, 'Student')
    await page.addInitScript(
      ({ seededRole, sessionKey, runtime }) => {
        sessionStorage.setItem('access_token', 'e2e-access-token')
        sessionStorage.setItem('refresh_token', 'e2e-refresh-token')
        sessionStorage.setItem(
          'user_profile',
          JSON.stringify({
            id: '00000000-0000-4000-8000-000000000002',
            email: 'student@e2e.test',
            fullName: 'E2E Workflow User',
            role: seededRole,
            mustChangePassword: false,
          }),
        )
        sessionStorage.setItem(sessionKey, JSON.stringify(runtime))
      },
      {
        seededRole: 'Student',
        sessionKey: `mf01_practice_session:${fullSessionId}`,
        runtime: {
          version: 1,
          sessionId: fullSessionId,
          studentId: '00000000-0000-4000-8000-000000000002',
          transcriptBufferSeconds: 60,
          config: {
            courseId,
            courseName: 'Building Cross-Platform Back-End Applications',
            courseCode: 'PRN231',
            mode: 'FULL_SESSION',
            selectedDifficulties: [],
            questionCount: 3,
            microphoneName: 'E2E text mode',
          },
          questions,
          drafts: [],
          currentQuestionIndex: 0,
          mainQuestionsCompleted: 0,
          questionDifficulties: {},
          activeFollowUp: null,
          pendingSubmission: null,
          pendingAnswerId: null,
          resolvedAnswer: null,
          submittedAnswerIdsByQuestion: {},
          fullSessionSubmitted: false,
          draftAnswerText: '',
          voiceBaselineTranscript: '',
          draftAnswerMode: 'text',
          showBuffer: false,
          bufferDeadlineTimestamp: null,
        },
      },
    )

    await page.goto(`/practice/session?session=${fullSessionId}`)
    const answer = page.getByRole('textbox', { name: 'Nhập câu trả lời bằng văn bản' })
    await answer.fill('A complete answer that is long enough for the first question.')
    await page.getByRole('button', { name: /Bỏ qua/ }).click()
    await answer.fill('A complete answer that is long enough for the second question.')
    await page.getByRole('button', { name: /Bỏ qua/ }).click()
    await answer.fill('A complete answer that is long enough for the final question.')
    await page.getByRole('button', { name: 'Nộp bài' }).click()
    await page.getByRole('button', { name: 'Xác nhận nộp bài' }).click()

    await expect.poll(() => batchRequest.body).not.toBeNull()
    if (batchRequest.body === null)
      throw new Error('Expected full-session batch submission request.')

    expect(batchRequest.body).toMatchObject({
      studentId: '00000000-0000-4000-8000-000000000002',
    })
    expect(batchRequest.body.answers).toEqual(
      questions.map((question, index) => ({
        questionId: question.questionId,
        answerText:
          index === 2 ? 'A complete answer that is long enough for the final question.' : '',
        isFollowUp: false,
        parentAnswerId: null,
      })),
    )
  })
})

test.describe('Cổng Khảo thí and MF-04 appeals', () => {
  const officialSession = {
    id: '00000000-0000-4000-8000-000000000030',
    courseId,
    examStructureId: '00000000-0000-4000-8000-000000000031',
    title: 'PRN231 Final Oral Exam',
    examDate: '2026-11-20',
    hasFollowUp: true,
    maxFollowUpQuestions: 2,
    examInputMode: 'VoiceWithTranscriptEdit',
    transcriptBufferSeconds: 60,
    status: 'SCHEDULED',
  }
  const officialShift = {
    id: '00000000-0000-4000-8000-000000000032',
    sessionId: officialSession.id,
    shiftName: 'Ca 1',
    roomLab: 'Lab 301',
    startTime: '2026-11-20T08:00:00.000Z',
    endTime: '2026-11-20T09:30:00.000Z',
    proctorUserId: 'proctor-1',
    examinerUserId: 'examiner-1',
    examinerName: 'Examiner E2E',
    proctorName: 'Proctor E2E',
    status: 'SCHEDULED',
  }

  test('creates an official exam session and shift through the examination-officer forms', async ({
    page,
  }) => {
    let sessions = [] as (typeof officialSession)[]
    let shifts = [] as (typeof officialShift)[]
    let sessionBody: Record<string, unknown> | null = null
    let shiftBody: Record<string, unknown> | null = null

    await page.route('**/api/v1/semesters', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'semester-fa26',
            code: 'FA26',
            name: 'Fall 2026',
            startDate: '2026-09-01',
            endDate: '2026-12-31',
            isActive: true,
          },
        ]),
      })
    })
    await page.route('**/api/v1/semesters/semester-fa26/course-offerings**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            courseId,
            courseOfferingId: 'course-offering-1',
            code: 'PRN231',
            name: 'Building Cross-Platform Back-End Applications',
            credits: 3,
            isCourseActive: true,
            isSelected: true,
          },
        ]),
      })
    })
    await page.route(`**/api/v1/courses/${courseId}/configurations`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          courseId,
          courseCode: 'PRN231',
          courseName: 'Building Cross-Platform Back-End Applications',
          hasFollowUp: true,
          maxFollowUpQuestions: 2,
          examInputMode: 'VoiceWithTranscriptEdit',
          transcriptBufferSeconds: 60,
          isActive: true,
        }),
      })
    })
    await page.route(`**/api/v1/courses/${courseId}/lecturers`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            lecturerId: officialShift.proctorUserId,
            fullName: officialShift.proctorName,
            email: 'proctor@example.com',
            studentCode: 'GV001',
            isActive: true,
            semesterId: 'semester-fa26',
            semesterCode: 'FA26',
            courseOfferingId: 'course-offering-1',
          },
          {
            lecturerId: officialShift.examinerUserId,
            fullName: officialShift.examinerName,
            email: 'examiner@example.com',
            studentCode: 'GV002',
            isActive: true,
            semesterId: 'semester-fa26',
            semesterCode: 'FA26',
            courseOfferingId: 'course-offering-1',
          },
        ]),
      })
    })
    await page.route('**/api/v1/exam-structures**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    })

    await page.route('**/api/v1/official-exams/sessions', async (route) => {
      if (route.request().method() === 'POST') {
        sessionBody = route.request().postDataJSON() as Record<string, unknown>
        sessions = [officialSession]
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(officialSession),
        })
        return
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(sessions),
      })
    })
    await page.route('**/api/v1/official-exams/shifts**', async (route) => {
      if (route.request().method() === 'POST') {
        shiftBody = route.request().postDataJSON() as Record<string, unknown>
        shifts = [officialShift]
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(officialShift),
        })
        return
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(shifts),
      })
    })

    await seedRole(page, 'ExaminationOfficer')
    await page.goto('/examination-officer/exams')

    await page.getByRole('button', { name: 'Tạo ca thi' }).click()
    await expect(page.locator('#exam-semester')).toHaveValue('semester-fa26')
    await page.locator('#exam-course').selectOption(courseId)
    await page.locator('#shift-name').fill(officialShift.shiftName)
    await page.locator('#shift-room').fill(officialShift.roomLab)
    await page.locator('#shift-start').fill('2026-11-20T08:00')
    await page.locator('#shift-end').fill('2026-11-20T09:30')
    await page.locator('#shift-proctor').selectOption(officialShift.proctorUserId)
    await page.locator('#shift-examiner').selectOption(officialShift.examinerUserId)
    await page.getByRole('button', { name: 'Tạo ca thi cho PRN231' }).click()
    await page.getByRole('button', { name: 'Đã hiểu' }).click()
    await expect(
      page.locator('article strong').filter({ hasText: officialShift.shiftName }),
    ).toBeVisible()

    expect(sessionBody).toMatchObject({
      courseId,
      examStructureId: null,
      examDate: officialSession.examDate,
      transcriptBufferSeconds: 60,
    })
    expect(sessionBody?.title).toContain('PRN231')
    expect(shiftBody).toMatchObject({
      sessionId: officialSession.id,
      shiftName: officialShift.shiftName,
      roomLab: officialShift.roomLab,
      proctorUserId: officialShift.proctorUserId,
      examinerUserId: officialShift.examinerUserId,
    })
  })

  test('assigns a ticket and publishes the exam set only through the officer actions', async ({
    page,
  }) => {
    let ticketBody: Record<string, unknown> | null = null
    let publishCalled = false
    const ticket = {
      id: '00000000-0000-4000-8000-000000000033',
      shiftId: officialShift.id,
      studentId: '00000000-0000-4000-8000-000000000002',
      studentCode: 'SE170002',
      seatNumber: 7,
      ipAddress: '10.0.0.7',
      status: 'SCHEDULED',
      checkedInAt: '2026-11-20T07:50:00.000Z',
      examSetId: null,
    }
    await page.route('**/api/v1/official-exams/sessions**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([officialSession]),
      })
    })
    await page.route('**/api/v1/official-exams/shifts**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([officialShift]),
      })
    })
    await page.route('**/api/v1/official-exams/exam-sets**', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    })
    await page.route(
      `**/api/v1/official-exams/shifts/${officialShift.id}/publish`,
      async (route) => {
        publishCalled = true
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            success: true,
            shiftId: officialShift.id,
            message: 'Đã xuất bản đề.',
          }),
        })
      },
    )
    await page.route('**/api/v1/official-exams/tickets', async (route) => {
      ticketBody = route.request().postDataJSON() as Record<string, unknown>
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(ticket),
      })
    })

    await seedRole(page, 'ExaminationOfficer')
    await page.goto(`/examination-officer/exams/${officialSession.id}`)
    const shiftCard = page.locator('article').filter({ hasText: officialShift.shiftName })
    await page.once('dialog', (dialog) => void dialog.accept())
    await shiftCard.locator('button').last().click()
    await expect.poll(() => publishCalled).toBe(true)

    await page.goto('/examination-officer/tickets/assign')
    await page.getByRole('button', { name: 'Cấp vé thi' }).click()
    await page.locator('#ticket-shift').selectOption(officialShift.id)
    await page.locator('#ticket-student-code').fill(ticket.studentCode)
    await page.locator('#ticket-seat-number').fill(String(ticket.seatNumber))
    await page.locator('#ticket-ip-address').fill(ticket.ipAddress)
    await page.getByRole('button', { name: 'Xác nhận cấp vé' }).click()
    await expect(page.getByText(`MSSV: ${ticket.studentCode}`)).toBeVisible()

    expect(ticketBody).toEqual({
      shiftId: officialShift.id,
      studentCode: ticket.studentCode,
      seatNumber: ticket.seatNumber,
      ipAddress: ticket.ipAddress,
    })
  })

  const appeal = {
    id: appealId,
    ticketId: 'ticket-1',
    studentId: 'student-1',
    studentCode: 'SE170001',
    studentName: 'Le Vu Hoang',
    sessionId: 'session-1',
    sessionTitle: 'PRN231 Final Oral Exam',
    submissionId: null,
    reason: 'Please review the recorded answer.',
    status: 'PENDING',
    assignedTo: null,
    assignedToName: '',
    reviewedBy: null,
    reviewedByName: null,
    decision: null,
    originalScore: 7.5,
    proposedScore: null,
    reviewNotes: null,
    createdAt: '2026-11-14T01:00:00.000Z',
    updatedAt: '2026-11-14T01:00:00.000Z',
    resolvedAt: null,
  }

  test('renders examination-officer appeals and calls the assignment endpoint', async ({
    page,
  }) => {
    const reviewBodies: Record<string, unknown>[] = []
    await page.route(`**/api/v1/appeals/${appealId}`, async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(appeal),
        })
        return
      }
      await route.continue()
    })
    await page.route(`**/api/v1/appeals/${appealId}/review`, async (route) => {
      const requestBody = route.request().postDataJSON() as Record<string, unknown>
      reviewBodies.push(requestBody)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ...appeal,
          status: reviewBodies.length === 1 ? 'IN_REVIEW' : 'APPROVED',
          reviewNotes: requestBody.reviewNotes,
        }),
      })
    })
    let assignmentBody: Record<string, unknown> | null = null
    await page.route(
      `**/api/v1/examination-officer/appeals/${appealId}/assign-lecturer`,
      async (route) => {
        assignmentBody = route.request().postDataJSON() as Record<string, unknown>
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ...appeal,
            status: 'IN_REVIEW',
            assignedTo: 'lecturer-1',
            assignedToName: 'Qualified Lecturer',
          }),
        })
      },
    )

    await seedRole(page, 'ExaminationOfficer')
    await page.goto(`/examination-officer/appeals/${appealId}`)
    await expect(page.getByText('SE170001')).toBeVisible()
    await page.getByLabel('Ghi chú').fill('Hồ sơ hợp lệ và đủ điều kiện thẩm tra.')
    await page.locator('section').filter({ hasText: 'Thẩm tra' }).locator('button').last().click()
    await expect.poll(() => reviewBodies.length).toBe(1)
    await page.getByLabel('Lecturer user ID').fill('lecturer-1')
    await page.getByRole('button', { name: /Ph.*c.*ng ch.*m l.*i/ }).click()
    await expect(page.getByText('Qualified Lecturer')).toBeVisible()
    await page.getByLabel('Trạng thái').selectOption('APPROVED')
    await page.getByLabel('Ghi chú').fill('Đã phê duyệt quyết định cuối theo biên bản chấm lại.')
    await page.locator('section').filter({ hasText: 'Thẩm tra' }).locator('button').last().click()
    await expect.poll(() => reviewBodies.length).toBe(2)
    expect(reviewBodies).toEqual([
      { decision: 'IN_REVIEW', reviewNotes: 'Hồ sơ hợp lệ và đủ điều kiện thẩm tra.' },
      { decision: 'APPROVED', reviewNotes: 'Đã phê duyệt quyết định cuối theo biên bản chấm lại.' },
    ])
    expect(assignmentBody).toEqual({ lecturerId: 'lecturer-1' })
  })

  test('lets the assigned lecturer submit a regrade through the lecturer workflow', async ({
    page,
  }) => {
    const assignedAppeal = {
      ...appeal,
      status: 'ASSIGNED_TO_LECTURER',
      assignedTo: 'lecturer-1',
      assignedToName: 'Qualified Lecturer',
    }
    let regradeBody: Record<string, unknown> | null = null
    await page.route('**/api/v1/appeals', async (route) => {
      if (route.request().method() !== 'GET') return route.continue()
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([assignedAppeal]),
      })
    })
    await page.route(`**/api/v1/appeals/${appealId}/regrade`, async (route) => {
      regradeBody = route.request().postDataJSON() as Record<string, unknown>
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ...assignedAppeal,
          status: 'REGRADED_BY_LECTURER',
          proposedScore: 8.5,
        }),
      })
    })

    await seedRole(page, 'Instructor')
    await page.goto('/lecturer/appeals')
    await expect(page.getByRole('button', { name: /SE170001 ASSIGNED_TO_LECTURER/ })).toBeVisible()
    await page.locator('input[type="number"]').fill('8.5')
    await page.locator('textarea').fill('Đã đối chiếu bằng chứng và chấm lại độc lập.')
    await page.locator('button').last().click()
    await expect(page.getByRole('status')).toBeVisible()
    expect(regradeBody).toEqual({
      newScore: 8.5,
      reviewNotes: 'Đã đối chiếu bằng chứng và chấm lại độc lập.',
    })
  })
})
