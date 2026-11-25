import { test, expect } from '../fixtures/test'
import { isLiveApiMode } from '../fixtures/env'
import { E2E_IDS, makeUuid } from '../helpers/mock-data'
import type {
  ExamBlueprint,
  ExamPaper,
  ExamPreparation,
} from '../../src/types/examPreparation.types'
const root = '/department-head/exam-structures'
const sessionId = makeUuid('fe01', 1)
const assignmentId = makeUuid('fe02', 1)
const paperId = makeUuid('fe03', 1)
const paperId2 = makeUuid('fe03', 2)
const blue: ExamBlueprint = {
  id: E2E_IDS.examStructure,
  name: 'Cấu trúc vấn đáp cuối kỳ',
  totalQuestions: 3,
  durationMinutes: 30,
  easyCount: 1,
  mediumCount: 1,
  hardCount: 1,
  cloIds: [E2E_IDS.clo1],
}
function paper(id: string, code: string): ExamPaper {
  return {
    id,
    assignmentId,
    structureId: blue.id,
    courseId: E2E_IDS.course,
    sessionId,
    createdBy: E2E_IDS.users.Instructor,
    setCode: code,
    status: 'SUBMITTED_FOR_REVIEW',
    reviewNotes: null,
    submittedAt: '2026-11-24T08:00:00Z',
    approvedAt: null,
    handedOverAt: null,
    generatedAt: '2026-11-23T08:00:00Z',
    isActive: true,
    questions: [1, 2, 3].map((i) => ({
      questionId: makeUuid('fe04', i),
      sourceQuestionId: makeUuid('fe05', i),
      orderIndex: i,
      title: `Phân tích kiến trúc dịch vụ — câu ${i}`,
      content: `Nội dung câu ${i}: trình bày nguyên lý và phân tích ví dụ trong hệ thống phân tán.`,
      bloomLevel: 'Apply',
      difficulty: i === 1 ? 'easy' : i === 2 ? 'medium' : 'hard',
      cloId: E2E_IDS.clo1,
      sampleAnswer: `Đáp án mẫu câu ${i}: giải thích nguyên lý, phân tích ưu nhược điểm và ví dụ thực tế.`,
      imageUrl: null,
      imageDescription: null,
      rubricName: `Rubric câu ${i}`,
      totalMaxScore: 10,
      criteria: [
        {
          name: 'Hiểu đúng nguyên lý',
          description: 'Trình bày đầy đủ và chính xác các khái niệm.',
          maxScore: 6,
          weight: 0.6,
          bloomLevel: 'Understand',
          orderIndex: 1,
        },
        {
          name: 'Phân tích ví dụ',
          description: 'Liên hệ ví dụ, giải thích lựa chọn thiết kế.',
          maxScore: 4,
          weight: 0.4,
          bloomLevel: 'Apply',
          orderIndex: 2,
        },
      ],
    })),
  }
}
for (const viewport of [
  { width: 1440, height: 1000 },
  { width: 390, height: 844 },
]) {
  test(`head reviews papers in a question-tab drawer without navigation at ${viewport.width}px`, async ({
    page,
    authSession,
  }, testInfo) => {
    test.skip(isLiveApiMode(), 'Browser interaction regression uses isolated API fixtures.')
    await page.setViewportSize(viewport)
    let papers = [paper(paperId, 'PRN-FE-01'), paper(paperId2, 'PRN-FE-02')]
    const decisions: string[] = []
    const prep: ExamPreparation = {
      id: makeUuid('fe06', 1),
      sessionId,
      courseId: E2E_IDS.course,
      courseCode: 'PRN231',
      semesterId: E2E_IDS.semester,
      semesterCode: 'FA26',
      title: 'Thi cuối kỳ PRN231 — Vấn đáp',
      examStructureId: blue.id,
      status: 'IN_PROGRESS',
      requiredSetCount: 2,
      approvedSetCount: 0,
      handedOverSetCount: 0,
      deadline: '2099-12-31T08:00:00Z',
      notes: 'Soạn đề theo cấu trúc và phạm vi CLO đã thống nhất.',
      requestedAt: '2026-11-20T08:00:00Z',
      handedOverAt: null,
      assignments: [
        {
          id: assignmentId,
          lecturerId: E2E_IDS.users.Instructor,
          lecturerName: 'Nguyễn Trọng Tốt',
          structureId: blue.id,
          setCount: 2,
          deadline: '2099-12-30T08:00:00Z',
          notes: 'Chú ý ví dụ ứng dụng thực tế',
          assignedAt: '2026-11-21T08:00:00Z',
          blueprint: blue,
          draft: 0,
          pending: 2,
          needsRevision: 0,
          rejected: 0,
          approved: 0,
          handedOver: 0,
        },
      ],
    }
    await authSession.seedSession('DepartmentHead')
    await page.route('**/api/v1/**', async (route) => {
      const path = new URL(route.request().url()).pathname
      const method = route.request().method()
      const json = (body: unknown) =>
        route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) })
      if (path === '/api/v1/official-exams/preparations')
        return json([
          { ...prep, approvedSetCount: papers.filter((p) => p.status === 'OFFICIAL').length },
        ])
      if (path === '/api/v1/official-exams/sessions')
        return json([
          {
            id: sessionId,
            courseId: E2E_IDS.course,
            semesterId: E2E_IDS.semester,
            examStructureId: blue.id,
            title: prep.title,
            examDate: '2099-12-31',
            status: 'scheduled',
            examAttemptType: 'FE',
            hasFollowUp: false,
            maxFollowUpQuestions: 2,
            examInputMode: 'VoiceWithTranscriptEdit',
            transcriptBufferSeconds: 60,
          },
        ])
      if (path === '/api/v1/official-exams/exam-sets') return json(papers)
      if (path.startsWith('/api/v1/exam-structures/')) return json(blue)
      if (path.endsWith('/duplicates'))
        return json([
          {
            questionId: makeUuid('fe05', 2),
            questionTitle: 'Câu 2',
            setCode: 'PRN-RE-03',
            sessionId: makeUuid('fe01', 2),
            sessionTitle: 'Thi lại học kỳ trước',
            semesterCode: 'SU26',
          },
        ])
      if (path.endsWith('/history')) return json([])
      if (method === 'POST' && path.endsWith('/approve')) {
        const id = path.split('/').at(-2)
        decisions.push(id!)
        papers = papers.map((p) => (p.id === id ? { ...p, status: 'OFFICIAL' } : p))
        return json({ status: 'OFFICIAL', hasDuplicates: true, duplicateWarnings: [] })
      }
      return route.fallback()
    })
    await page.goto(root)
    await expect(
      page.getByRole('heading', { name: 'Chuẩn bị đề thi', exact: true, level: 2 }),
    ).toBeVisible()
    await page.getByRole('link', { name: /Thi cuối kỳ PRN231/ }).click()
    await expect(page.getByRole('heading', { name: 'Danh sách bộ đề' })).toBeVisible()
    const url = page.url()
    await page.screenshot({
      path: testInfo.outputPath(`request-${viewport.width}.png`),
      fullPage: true,
    })
    await page.getByRole('button', { name: 'Xem bộ đề PRN-FE-01', exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Xem và duyệt bộ đề', exact: true })
    await expect(drawer.getByRole('heading', { name: 'PRN-FE-01', exact: true })).toBeVisible()
    await drawer.getByRole('tab', { name: /^Câu 2/ }).click()
    await expect(drawer.getByText('Rubric câu 2', { exact: true })).toBeVisible()
    await expect(drawer.getByText('Câu hỏi đã xuất hiện trong 1 bộ đề')).toBeVisible()
    expect(page.url()).toBe(url)
    await expect.poll(() => drawer.evaluate((e) => e.scrollWidth <= e.clientWidth + 2)).toBe(true)
    await page.screenshot({
      path: testInfo.outputPath(`review-${viewport.width}.png`),
      fullPage: true,
    })
    await drawer.getByRole('button', { name: 'Duyệt bộ đề', exact: true }).click()
    await expect(drawer.getByRole('heading', { name: 'PRN-FE-02', exact: true })).toBeVisible()
    expect(decisions).toEqual([paperId])
    expect(page.url()).toBe(url)
    await drawer.getByRole('button', { name: 'Đóng bộ đề', exact: true }).click()
    await page.getByRole('button', { name: 'Xem cấu trúc', exact: true }).click()
    const dialog = page.getByRole('dialog', { name: 'Cấu trúc đề thi', exact: true })
    await dialog.getByRole('button', { name: 'Tạo phiên bản mới', exact: true }).click()
    const editing = page.getByRole('dialog', { name: 'Cấu hình đề thi', exact: true })
    await editing.getByLabel('Tên cấu trúc', { exact: true }).fill('a'.repeat(200))
    await expect.poll(() => editing.evaluate((e) => e.scrollWidth <= e.clientWidth + 2)).toBe(true)
    await editing.getByRole('button', { name: 'Chọn CLO', exact: true }).click()
    await expect(page.getByRole('dialog', { name: 'Chọn CLO', exact: true })).toBeVisible()
    await page
      .getByRole('dialog', { name: 'Chọn CLO', exact: true })
      .getByRole('button', { name: 'Hủy', exact: true })
      .click()
    await expect(page.getByRole('dialog', { name: 'Cấu hình đề thi', exact: true })).toBeVisible()
    await page
      .getByRole('dialog', { name: 'Cấu hình đề thi', exact: true })
      .getByRole('button', { name: 'Hủy', exact: true })
      .click()
    await expect
      .poll(() =>
        page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 2),
      )
      .toBe(true)
  })
}
