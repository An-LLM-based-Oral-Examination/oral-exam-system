import type { CourseClo } from '@/types/courseClo.types'
import type { ExamBlueprint, ExamPaper, PaperQuestion } from '@/types/examPreparation.types'
import type {
  BloomLevel,
  GeneratedQuestionDraft,
  QuestionDetail,
  QuestionDifficulty,
  RubricDraft,
} from '@/types/rubric.types'
export type DraftOrigin = 'manual' | 'ai' | 'bank'
export interface PaperQuestionDraft {
  id?: string
  originId?: string
  persisted?: boolean
  origin: DraftOrigin
  title: string
  content: string
  sampleAnswer: string
  imageUrl: string
  imageDescription: string
  keyPoints: string[]
  cloId: string
  bloomLevel: BloomLevel
  rubric: RubricDraft
}
export interface QuestionSlot {
  difficulty: QuestionDifficulty
  question: PaperQuestionDraft
  step: 1 | 2
}
export const difficulties: QuestionDifficulty[] = ['easy', 'medium', 'hard']
export const bloomOptions: BloomLevel[] = [
  'Remember',
  'Understand',
  'Apply',
  'Analyze',
  'Evaluate',
  'Create',
]
export function emptyQuestion(cloId = ''): PaperQuestionDraft {
  return {
    origin: 'manual',
    title: '',
    content: '',
    sampleAnswer: '',
    imageUrl: '',
    imageDescription: '',
    keyPoints: [],
    cloId,
    bloomLevel: 'Apply',
    rubric: {
      name: '',
      description: '',
      totalMaxScore: 10,
      criteria: [1, 2].map((i) => ({
        criterionName: '',
        description: '',
        maxScore: 5,
        weight: 0.5,
        bloomLevel: 'Apply',
        orderIndex: i,
      })),
    },
  }
}
export function touched(q: PaperQuestionDraft) {
  return Boolean(
    q.id ||
    q.title ||
    q.content ||
    q.sampleAnswer ||
    q.imageUrl ||
    q.rubric.name ||
    q.rubric.criteria.some((c) => c.criterionName || c.description),
  )
}
export function questionErrors(q: PaperQuestionDraft, clos: CourseClo[]) {
  const errors: Record<string, string> = {}
  if (!q.title.trim() || q.title.length > 300) errors.title = 'Nhập tiêu đề, tối đa 300 ký tự.'
  if (!q.content.trim()) errors.content = 'Vui lòng nhập nội dung câu hỏi.'
  if (q.sampleAnswer.trim().length < 50) errors.sampleAnswer = 'Đáp án mẫu cần ít nhất 50 ký tự.'
  if (!clos.some((c) => c.id === q.cloId && c.isActive))
    errors.cloId = 'Chọn CLO đang hoạt động trong phạm vi được giao.'
  if (!bloomOptions.includes(q.bloomLevel)) errors.bloomLevel = 'Chọn bậc Bloom hợp lệ.'
  if (!q.rubric.name.trim()) errors.rubricName = 'Vui lòng nhập tên rubric.'
  if (q.rubric.criteria.length < 2) errors.criteria = 'Barem Rubric phải có ít nhất hai tiêu chí.'
  q.rubric.criteria.forEach((c, i) => {
    if (!c.criterionName.trim()) errors[`criterion-${i}-name`] = 'Nhập tên tiêu chí.'
    if (!Number.isFinite(c.maxScore) || c.maxScore < 0.1 || c.maxScore > 10)
      errors[`criterion-${i}-score`] = 'Điểm phải từ 0.1 đến 10.'
  })
  const total = q.rubric.criteria.reduce((sum, c) => sum + c.maxScore, 0)
  if (!Number.isFinite(total) || Math.abs(total - 10) > 0.001)
    errors.rubricTotal = 'Tổng điểm các tiêu chí phải bằng chính xác 10.0.'
  return errors
}
function keyPoints(raw?: string) {
  try {
    const result: unknown = JSON.parse(raw ?? '[]')
    return Array.isArray(result) ? result.filter((v): v is string => typeof v === 'string') : []
  } catch {
    return []
  }
}
export function savedQuestion(q: PaperQuestion): PaperQuestionDraft {
  return {
    id: q.questionId,
    originId: q.sourceQuestionId ?? q.questionId,
    persisted: true,
    origin: q.isBankQuestion ? 'bank' : q.source === 'flm_api' ? 'ai' : 'manual',
    title: q.title,
    content: q.content,
    sampleAnswer: q.sampleAnswer ?? '',
    imageUrl: q.imageUrl ?? '',
    imageDescription: q.imageDescription ?? '',
    keyPoints: keyPoints(q.keyPoints),
    cloId: q.cloId ?? '',
    bloomLevel: q.bloomLevel as BloomLevel,
    rubric: {
      name: q.rubricName ?? '',
      description: q.rubricDescription ?? '',
      totalMaxScore: q.totalMaxScore,
      criteria: q.criteria.map((c) => ({
        criterionName: c.name,
        description: c.description ?? '',
        maxScore: c.maxScore,
        weight: c.weight,
        bloomLevel: (c.bloomLevel ?? q.bloomLevel) as BloomLevel,
        orderIndex: c.orderIndex,
      })),
    },
  }
}
export function bankQuestion(q: QuestionDetail): PaperQuestionDraft {
  return {
    id: q.id,
    originId: q.id,
    persisted: true,
    origin: 'bank',
    title: q.title,
    content: q.content,
    sampleAnswer: q.sampleAnswer ?? '',
    imageUrl: q.imageUrl ?? '',
    imageDescription: q.imageDescription ?? '',
    keyPoints: q.keyPoints,
    cloId: q.cloId ?? '',
    bloomLevel: q.bloomLevel,
    rubric: {
      name: q.rubric.name,
      description: q.rubric.description,
      totalMaxScore: q.rubric.totalMaxScore,
      criteria: q.rubric.criteria.map((c) => ({
        ...c,
        description: c.description ?? '',
        bloomLevel: c.bloomLevel ?? q.bloomLevel,
      })),
    },
  }
}
export function generatedQuestion(
  q: GeneratedQuestionDraft,
  clos: CourseClo[],
): PaperQuestionDraft {
  return {
    ...emptyQuestion(),
    origin: 'ai',
    title: q.title,
    content: q.content,
    sampleAnswer: q.modelAnswer,
    imageUrl: q.imageUrl ?? '',
    keyPoints: q.keyPoints,
    cloId: clos.find((c) => c.cloCode.toLowerCase() === q.cloCode?.toLowerCase())?.id ?? '',
    bloomLevel: q.bloomLevel,
    rubric: structuredClone(q.rubric),
  }
}
export function initialSlots(
  blueprint: ExamBlueprint,
  clos: CourseClo[],
  paper?: ExamPaper,
): QuestionSlot[] {
  const pool = [...(paper?.questions ?? [])].sort((a, b) => a.orderIndex - b.orderIndex)
  return difficulties.flatMap((d) =>
    Array.from({ length: blueprint[`${d === 'medium' ? 'medium' : d}Count`] }, () => {
      const index = pool.findIndex((q) => q.difficulty === d)
      return {
        difficulty: d,
        question: index < 0 ? emptyQuestion(clos[0]?.id) : savedQuestion(pool.splice(index, 1)[0]),
        step: 1 as const,
      }
    }),
  )
}
export function paperReady(paper: ExamPaper, blueprint: ExamBlueprint, clos: CourseClo[]) {
  return (
    paper.questions.length === blueprint.totalQuestions &&
    new Set(paper.questions.map((q) => q.sourceQuestionId ?? q.questionId)).size ===
      paper.questions.length &&
    difficulties.every(
      (d) => paper.questions.filter((q) => q.difficulty === d).length === blueprint[`${d}Count`],
    ) &&
    paper.questions.every((q) => Object.keys(questionErrors(savedQuestion(q), clos)).length === 0)
  )
}
export function placementPlan(
  slots: QuestionSlot[],
  incoming: { difficulty: QuestionDifficulty; question: PaperQuestionDraft }[],
) {
  const used = new Set<number>()
  return incoming.map((item) => {
    const choices = slots
      .map((slot, index) => ({ slot, index }))
      .filter(({ slot, index }) => slot.difficulty === item.difficulty && !used.has(index))
    const target = choices.find(({ slot }) => !touched(slot.question)) ?? choices[0]
    if (!target) throw new Error('Số câu đã chọn vượt phân bổ độ khó của cấu trúc.')
    used.add(target.index)
    return { ...item, target: target.index }
  })
}
