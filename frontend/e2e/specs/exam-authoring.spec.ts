import { test, expect } from '../fixtures/test'
import { isLiveApiMode } from '../fixtures/env'
import { E2E_IDS, makeQuestion, makeUuid } from '../helpers/mock-data'
import type {
  ExamBlueprint,
  ExamPaper,
  ExamPreparation,
  PaperQuestion,
} from '../../src/types/examPreparation.types'
const sessionId = makeUuid('ab01', 1),
  assignmentId = makeUuid('ab02', 1),
  paperId = makeUuid('ab03', 1),
  secondId = makeUuid('ab03', 2)
const easyId = makeUuid('ab04', 1),
  mediumId = makeUuid('ab04', 2),
  hardId = makeUuid('ab04', 3)
const root = '/lecturer/exam-authoring'
const blueprint: ExamBlueprint = {
  id: E2E_IDS.examStructure,
  name: 'Cấu trúc vấn đáp cuối kỳ',
  totalQuestions: 2,
  durationMinutes: 30,
  easyCount: 1,
  mediumCount: 1,
  hardCount: 0,
  cloIds: [E2E_IDS.clo1],
}
const bank = [
  makeQuestion({
    id: easyId,
    title: 'Câu dễ trong ngân hàng',
    difficulty: 'easy',
    cloId: E2E_IDS.clo1,
  }),
  makeQuestion({
    id: mediumId,
    title: 'Câu trung bình trong ngân hàng',
    difficulty: 'medium',
    cloId: E2E_IDS.clo1,
  }),
  makeQuestion({
    id: hardId,
    title: 'Câu khó ngoài cấu trúc',
    difficulty: 'hard',
    cloId: E2E_IDS.clo1,
  }),
]
function snapshot(id: string, bankSource: boolean): PaperQuestion {
  const q = bank.find((q) => q.id === id)!
  return {
    questionId: makeUuid('ab05', id === easyId ? 1 : 2),
    sourceQuestionId: id,
    orderIndex: id === easyId ? 1 : 2,
    title: q.title,
    content: q.content,
    sampleAnswer: q.sampleAnswer,
    difficulty: q.difficulty,
    bloomLevel: q.bloomLevel,
    cloId: q.cloId ?? null,
    source: 'manual',
    isBankQuestion: bankSource,
    imageUrl: null,
    imageDescription: null,
    rubricName: q.rubric.name,
    totalMaxScore: q.rubric.totalMaxScore,
    criteria: q.rubric.criteria.map((c) => ({
      name: c.criterionName,
      description: c.description,
      maxScore: c.maxScore,
      weight: c.weight,
      bloomLevel: c.bloomLevel,
      orderIndex: c.orderIndex,
    })),
  }
}
function paper(id: string, complete: boolean): ExamPaper {
  return {
    id,
    assignmentId,
    structureId: blueprint.id,
    courseId: E2E_IDS.course,
    sessionId,
    createdBy: E2E_IDS.users.Instructor,
    setCode: id === paperId ? 'PRN-FE-01' : 'PRN-FE-02',
    status: 'DRAFT',
    reviewNotes: null,
    submittedAt: null,
    approvedAt: null,
    handedOverAt: null,
    generatedAt: '2026-11-24T08:00:00Z',
    isActive: true,
    questions: complete
      ? [snapshot(easyId, true), snapshot(mediumId, true)]
      : [
          {
            ...snapshot(easyId, false),
            sourceQuestionId: makeUuid('ab06', 1),
            title: 'Câu dễ soạn thủ công',
          },
        ],
  }
}
for (const viewport of [
  { width: 1440, height: 1000 },
  { width: 390, height: 844 },
]) {
  test(`lecturer composes matching question slots and submits a complete batch at ${viewport.width}px`, async ({
    page,
    authSession,
  }, testInfo) => {
    test.skip(isLiveApiMode(), 'Isolated API fixtures for editor interaction.')
    await page.setViewportSize(viewport)
    let papers = [paper(paperId, false), paper(secondId, true)]
    const prep: ExamPreparation = {
      id: makeUuid('ab07', 1),
      sessionId,
      courseId: E2E_IDS.course,
      courseCode: 'PRN231',
      semesterId: E2E_IDS.semester,
      semesterCode: 'FA26',
      title: 'Thi cuối kỳ PRN231',
      examStructureId: blueprint.id,
      status: 'IN_PROGRESS',
      requiredSetCount: 2,
      approvedSetCount: 0,
      handedOverSetCount: 0,
      deadline: '2099-12-31T08:00:00Z',
      notes: '',
      requestedAt: '2026-11-20T08:00:00Z',
      handedOverAt: null,
      assignments: [
        {
          id: assignmentId,
          lecturerId: E2E_IDS.users.Instructor,
          lecturerName: 'Giảng viên',
          structureId: blueprint.id,
          setCount: 2,
          deadline: '2099-12-30T08:00:00Z',
          notes: 'Soạn câu hỏi gắn tình huống thực tế.',
          assignedAt: '2026-11-21T08:00:00Z',
          blueprint,
          draft: 2,
          pending: 0,
          needsRevision: 0,
          rejected: 0,
          approved: 0,
          handedOver: 0,
        },
      ],
    }
    let submitted: string[] = []
    await authSession.seedSession('Instructor')
    await page.route('**/api/v1/**', async (route) => {
      const path = new URL(route.request().url()).pathname,
        method = route.request().method()
      const json = (body: unknown) =>
        route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) })
      if (path === '/api/v1/official-exams/preparations') return json([prep])
      if (path === '/api/v1/official-exams/sessions')
        return json([
          {
            id: sessionId,
            courseId: E2E_IDS.course,
            semesterId: E2E_IDS.semester,
            examStructureId: blueprint.id,
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
      if (path === '/api/v1/official-exams/exam-sets' && method === 'GET') return json(papers)
      if (path.startsWith('/api/v1/exam-structures/')) return json(blueprint)
      if (path === '/api/v1/questions' && method === 'GET')
        return json({
          items: bank,
          totalCount: bank.length,
          pageNumber: 1,
          pageSize: 100,
          totalPages: 1,
        })
      if (path.startsWith('/api/v1/questions/') && method === 'GET') {
        const q = bank.find((q) => path.endsWith(q.id))
        if (q) return json(q)
      }
      if (path.endsWith('/draft') && method === 'PUT') {
        const body = route.request().postDataJSON() as { questionIds: string[] }
        expect(body.questionIds).toEqual([easyId, mediumId])
        papers = papers.map((p) =>
          p.id === paperId
            ? { ...p, questions: body.questionIds.map((id) => snapshot(id, true)) }
            : p,
        )
        return json({ id: paperId, status: 'DRAFT' })
      }
      if (path.endsWith(`/assignments/${assignmentId}/submit-review`)) {
        const body = route.request().postDataJSON() as { examSetIds: string[] }
        submitted = body.examSetIds
        papers = papers.map((p) =>
          submitted.includes(p.id) ? { ...p, status: 'SUBMITTED_FOR_REVIEW' } : p,
        )
        return json({ submittedCount: submitted.length })
      }
      if (path.endsWith('/history')) return json([])
      return route.fallback()
    })
    await page.goto(root)
    await page.getByRole('link', { name: /Thi cuối kỳ PRN231/ }).click()
    await expect(page.getByRole('button', { name: 'Gửi duyệt (1)' })).toBeDisabled()
    const row = page.getByRole('button', { name: 'Xem bộ đề PRN-FE-01', exact: true })
    await row.getByRole('button', { name: 'Chỉnh sửa', exact: true }).click()
    const editor = page.getByRole('dialog', { name: 'Chỉnh sửa bộ đề', exact: true })
    await expect(editor.getByLabel('Tiêu đề', { exact: true })).toHaveValue('Câu dễ soạn thủ công')
    await editor.getByRole('tab', { name: /Câu 2/ }).click()
    await editor.getByLabel('Tiêu đề', { exact: true }).fill('a'.repeat(300))
    await editor.getByRole('tab', { name: /Câu 1/ }).click()
    await expect(editor.getByLabel('Tiêu đề', { exact: true })).toHaveValue('Câu dễ soạn thủ công')
    await expect.poll(() => editor.evaluate((e) => e.scrollWidth <= e.clientWidth + 2)).toBe(true)
    await expect
      .poll(() =>
        editor.evaluate((e) =>
          [...e.querySelectorAll<HTMLElement>('div,nav,fieldset')].every(
            (n) => !n.clientWidth || n.scrollWidth <= n.clientWidth + 2,
          ),
        ),
      )
      .toBe(true)
    await page.screenshot({
      path: testInfo.outputPath(`authoring-${viewport.width}.png`),
      fullPage: true,
    })
    await editor.getByRole('button', { name: 'Chọn từ ngân hàng', exact: true }).click()
    const picker = page.getByRole('dialog', { name: 'Chọn câu từ ngân hàng', exact: true })
    await expect(
      picker.getByRole('checkbox', { name: 'Chọn Câu khó ngoài cấu trúc' }),
    ).toBeDisabled()
    await picker.getByRole('checkbox', { name: 'Chọn Câu dễ trong ngân hàng' }).check()
    await picker.getByRole('checkbox', { name: 'Chọn Câu trung bình trong ngân hàng' }).check()
    await picker.getByRole('button', { name: 'Đưa vào đề' }).click()
    const replace = page.getByRole('dialog', { name: 'Xác nhận thay câu hỏi', exact: true })
    await expect(replace.getByText('Sẽ thay: Câu dễ soạn thủ công')).toBeVisible()
    await replace.getByRole('button', { name: 'Thay thế và đưa vào đề' }).click()
    await expect(editor.getByLabel('Tiêu đề', { exact: true })).toHaveAttribute('readonly')
    await editor.getByRole('button', { name: 'Barem rubric', exact: true }).click()
    await expect(editor.getByLabel('Tên rubric', { exact: true })).toHaveAttribute('readonly')
    await editor.getByRole('button', { name: 'Lưu bản nháp', exact: true }).click()
    await expect(editor).toHaveCount(0)
    await page.getByRole('button', { name: 'Gửi duyệt (2)', exact: true }).click()
    const submit = page.getByRole('dialog', { name: 'Gửi bộ đề duyệt', exact: true })
    await submit.getByRole('button', { name: 'Xác nhận gửi', exact: true }).click()
    await expect(submit).toHaveCount(0)
    expect(submitted).toEqual(expect.arrayContaining([paperId, secondId]))
    await page.getByRole('button', { name: 'Xem bộ đề PRN-FE-01', exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Xem bộ đề', exact: true })
    await expect(drawer.getByRole('tab', { name: /Câu 2/ })).toBeVisible()
    await expect(drawer.getByRole('button', { name: 'Duyệt bộ đề' })).toHaveCount(0)
    await page.screenshot({
      path: testInfo.outputPath(`author-view-${viewport.width}.png`),
      fullPage: true,
    })
  })
}
