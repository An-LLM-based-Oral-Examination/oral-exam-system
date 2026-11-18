import { apiClient } from '@/services/api.client'
import type {
  ActiveTotpPassword,
  CreateInClassShiftRequest,
  FinalizeInClassGradesResponse,
  GradeInClassAnswerRequest,
  GradeInClassSubmissionRequest,
  InClassCloPoolStat,
  InClassDifficulty,
  InClassDifficultyCounts,
  InClassNextFollowUp,
  InClassQuestionAnswer,
  InClassQuestionPoolMode,
  InClassShift,
  InClassShiftPoolStats,
  InClassSubmission,
  LecturerClassOption,
  EnterInClassExamResult,
  StudentExamQuestion,
  StudentExamResult,
  SubmitStudentInClassAnswerResponse,
} from '@/types/inClassShift.types'

const CANONICAL_DIFFICULTIES: readonly InClassDifficulty[] = ['easy', 'medium', 'hard']

function normalizeDifficulty(value: unknown): InClassDifficulty | null {
  if (typeof value !== 'string') return null
  const lower = value.trim().toLowerCase()
  if (lower === 'easy') return 'easy'
  if (lower === 'medium') return 'medium'
  if (lower === 'hard') return 'hard'
  return null
}

function parseRawArray(value: unknown): unknown[] {
  if (Array.isArray(value)) return value
  if (typeof value === 'string' && value.trim().startsWith('[')) {
    try {
      const parsed: unknown = JSON.parse(value)
      return Array.isArray(parsed) ? parsed : []
    } catch {
      return []
    }
  }
  return []
}

function readDifficulties(raw: Record<string, unknown>): InClassDifficulty[] {
  for (const key of ['selectedDifficulties', 'SelectedDifficulties', 'difficulties']) {
    const arr = parseRawArray(raw[key])
    if (arr.length > 0) {
      const mapped = new Set<InClassDifficulty>()
      for (const item of arr) {
        const diff = normalizeDifficulty(item)
        if (diff) mapped.add(diff)
      }
      const ordered = CANONICAL_DIFFICULTIES.filter((diff) => mapped.has(diff))
      if (ordered.length > 0) return ordered
    }
  }
  return [...CANONICAL_DIFFICULTIES]
}

function readStringArray(raw: Record<string, unknown>, ...keys: string[]): string[] {
  for (const key of keys) {
    const arr = parseRawArray(raw[key])
    if (arr.length > 0) {
      const cleaned = arr
        .filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
        .map((item) => item.trim())
      if (cleaned.length > 0) return [...new Set(cleaned)]
    }
  }
  return []
}

function normalizePoolMode(rawMode: string): InClassQuestionPoolMode {
  const upper = rawMode.trim().toUpperCase()
  if (upper === 'SELECTED_CLOS') return 'SELECTED_CLOS'
  if (upper === 'SELECTED_POOL') return 'SELECTED_POOL'
  return 'ALL_COURSE_BANK'
}

function normalizeDifficultyCounts(raw: unknown): InClassDifficultyCounts {
  if (!isRecord(raw)) {
    return { easy: 0, medium: 0, hard: 0, total: 0 }
  }
  const easy = Math.max(0, readNumber(raw, 'easy', 'Easy'))
  const medium = Math.max(0, readNumber(raw, 'medium', 'Medium'))
  const hard = Math.max(0, readNumber(raw, 'hard', 'Hard'))
  const explicitTotal = readNullableNumber(raw, 'total', 'Total')
  const computedTotal = easy + medium + hard
  return {
    easy,
    medium,
    hard,
    total: explicitTotal != null && explicitTotal >= computedTotal ? explicitTotal : computedTotal,
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readString(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'string' && value.trim()) return value
  }
  return ''
}

function readNumber(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'number' && Number.isFinite(value)) return value
    if (typeof value === 'string' && value.trim() && !Number.isNaN(Number(value))) {
      return Number(value)
    }
  }
  return 0
}

function readNullableNumber(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'number' && Number.isFinite(value)) return value
    if (typeof value === 'string' && value.trim() && !Number.isNaN(Number(value))) {
      return Number(value)
    }
  }
  return null
}

function readBool(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'boolean') return value
  }
  return false
}

function unwrapList(payload: unknown): unknown[] {
  if (Array.isArray(payload)) return payload
  if (!isRecord(payload)) return []
  for (const key of ['items', 'data', 'results', 'shifts', 'submissions', 'classes']) {
    const value = payload[key]
    if (Array.isArray(value)) return value
  }
  return []
}

function mapShiftStatus(raw: string, isLocked: boolean): InClassShift['status'] {
  if (isLocked || raw === 'FINALIZED') return 'FINALIZED'
  if (raw === 'CLOSED' || raw === 'ENDED') return 'CLOSED'
  if (raw === 'SCHEDULED' || raw === '') return 'SCHEDULED'
  return 'ACTIVE'
}

function durationFromRange(start: string, end: string) {
  const ms = new Date(end).getTime() - new Date(start).getTime()
  if (!Number.isFinite(ms) || ms <= 0) return 20
  return Math.max(1, Math.round(ms / 60_000))
}

function countSubmittedStudents(raw: Record<string, unknown>) {
  const doneSet = new Set([
    'SUBMITTED',
    'GRADING',
    'AI_GRADED',
    'GRADED',
    'AUDITED',
    'FINALIZED',
    'COMPLETED',
  ])
  const participants = Array.isArray(raw.participants) ? raw.participants.filter(isRecord) : []
  if (participants.length > 0) {
    return participants.filter((item) => doneSet.has(readString(item, 'status').toUpperCase())).length
  }
  const submissions = Array.isArray(raw.submissions) ? raw.submissions.filter(isRecord) : []
  if (submissions.length > 0) {
    return new Set(submissions.map((item) => readString(item, 'studentId')).filter(Boolean)).size
  }
  return readNumber(raw, 'submissionsCount', 'submittedCount')
}

function normalizeShift(raw: unknown, fallbackClass?: LecturerClassOption): InClassShift | null {
  if (!isRecord(raw)) return null
  const id = readString(raw, 'id', 'shiftId')
  if (!id) return null
  const statusRaw = readString(raw, 'status').toUpperCase()
  const isLocked = readBool(raw, 'isLocked', 'IsLocked')
  const startTime = readString(raw, 'startTime')
  const endTime = readString(raw, 'endTime')
  const durationMinutes = readNumber(raw, 'durationMinutes') || durationFromRange(startTime, endTime)
  const rawPoolMode = readString(raw, 'questionPoolMode', 'QuestionPoolMode')
  const selectedCloCodes = readStringArray(raw, 'selectedCloCodes', 'SelectedCloCodes', 'cloCodes')
  return {
    id,
    classId: readString(raw, 'classId') || fallbackClass?.id || '',
    className:
      readString(raw, 'className', 'classCode') || fallbackClass?.name || 'Lớp học phần',
    courseCode: readString(raw, 'courseCode', 'classCode') || fallbackClass?.courseCode || '',
    title: readString(raw, 'shiftName', 'title') || 'Ca thi trên lớp',
    description: readString(raw, 'description'),
    status: mapShiftStatus(statusRaw, isLocked),
    isLocked: isLocked || statusRaw === 'FINALIZED',
    startTime,
    durationMinutes,
    examDurationMinutes: (() => {
      const minutes = readNullableNumber(raw, 'examDurationMinutes')
      return minutes != null && minutes > 0 ? minutes : null
    })(),
    questionCount: readNumber(raw, 'totalQuestionsPerStudent', 'questionCount') || 1,
    hasFollowUp: readBool(raw, 'hasFollowUp'),
    maxFollowUpQuestions: readNumber(raw, 'maxFollowUpQuestions'),
    isAutoPublishGrades: readBool(raw, 'isAutoPublishGrades'),
    questionPoolMode: normalizePoolMode(rawPoolMode),
    selectedDifficulties: readDifficulties(raw),
    selectedCloIds: readStringArray(raw, 'selectedCloIds', 'SelectedCloIds', 'cloIds'),
    ...(selectedCloCodes.length > 0 ? { selectedCloCodes } : {}),
    passwordRotationSeconds: readNumber(raw, 'passwordRotationSeconds') || 300,
    gracePeriodSeconds: readNumber(raw, 'gracePeriodSeconds') || 60,
    joinedCount:
      readNumber(raw, 'participantsCount', 'joinedCount', 'participantCount') ||
      (Array.isArray(raw.participants) ? raw.participants.length : 0),
    submittedCount: countSubmittedStudents(raw),
    createdAt: readString(raw, 'createdAt') || new Date().toISOString(),
    finalizedAt: readString(raw, 'finalizedAt') || null,
    endTime: endTime || null,
    myParticipationStatus:
      readString(raw, 'myParticipationStatus', 'MyParticipationStatus') || null,
    myJoinedAt: readString(raw, 'myJoinedAt', 'MyJoinedAt') || null,
    myExamEndsAt: readString(raw, 'myExamEndsAt', 'MyExamEndsAt') || null,
    myRemainingSeconds: readNullableNumber(raw, 'myRemainingSeconds', 'MyRemainingSeconds'),
  }
}

function parseAssignedQuestions(data: Record<string, unknown>): StudentExamQuestion[] {
  const assigned = unwrapList(data.assignedQuestions)
  const questions = assigned
    .filter(isRecord)
    .map((item) => ({
      questionId: readString(item, 'questionId'),
      title: readString(item, 'title') || 'Câu hỏi',
      content: readString(item, 'content'),
      imageUrl: readString(item, 'imageUrl', 'ImageUrl') || null,
      imageDescription: readString(item, 'imageDescription', 'ImageDescription') || null,
    }))
    .filter((item) => item.questionId)
  if (questions.length > 0) return questions
  const single = data.assignedQuestion
  if (!isRecord(single)) return []
  const questionId = readString(single, 'questionId')
  if (!questionId) return []
  return [
    {
      questionId,
      title: readString(single, 'title') || 'Câu hỏi',
      content: readString(single, 'content'),
      imageUrl: readString(single, 'imageUrl', 'ImageUrl') || null,
      imageDescription: readString(single, 'imageDescription', 'ImageDescription') || null,
    },
  ]
}

function parseEnterResult(data: unknown, fallbackDurationMinutes: number): EnterInClassExamResult {
  const record = isRecord(data) ? data : {}
  const joinedAt = readString(record, 'joinedAt') || new Date().toISOString()
  const remainingSeconds = Math.max(0, readNumber(record, 'remainingSeconds'))
  const examEndsAtRaw = readString(record, 'examEndsAt')
  const examEndsAt =
    examEndsAtRaw ||
    new Date(
      Date.parse(joinedAt) + Math.max(1, fallbackDurationMinutes) * 60 * 1000,
    ).toISOString()
  return {
    questions: parseAssignedQuestions(record),
    joinedAt,
    examEndsAt,
    remainingSeconds,
    timedOut: Boolean(record.timedOut),
    status: readString(record, 'status') || 'JOINED',
  }
}

interface ParsedSubmissionAnswerRow extends InClassQuestionAnswer {
  rawGeneratedFollowUpPrompt: string | null
}

function answerFromBeRow(raw: Record<string, unknown>, index: number): ParsedSubmissionAnswerRow {
  const statusRaw = readString(raw, 'status').toUpperCase()
  const transcript = readString(raw, 'transcript', 'transcriptText') || null
  const notes = readString(raw, 'lecturerNotes', 'lecturerComment') || null
  const audited = statusRaw === 'AUDITED' || Boolean(notes)
  const aiScore = readNullableNumber(raw, 'aiScore')
  const finalScore = readNullableNumber(raw, 'finalScore', 'lecturerScore')
  const lecturerScore = audited ? finalScore : null
  let status: InClassQuestionAnswer['status'] = 'PENDING'
  if (audited && (lecturerScore != null || aiScore != null)) {
    status = 'AUDITED'
  } else if (lecturerScore != null || aiScore != null || statusRaw === 'AI_GRADED' || statusRaw === 'GRADED') {
    status = 'GRADED'
  } else if (transcript || statusRaw === 'SUBMITTED' || statusRaw === 'GRADING') {
    status = 'GRADING'
  }
  const isFollowUp = readBool(raw, 'isFollowUp')
  const followUpIndex = readNumber(raw, 'followUpIndex') || (isFollowUp ? 1 : 0)
  const parentSubmissionId = readString(raw, 'parentSubmissionId') || null
  const rawGeneratedFollowUpPrompt = readString(raw, 'followUpPrompt') || null
  return {
    answerId: readString(raw, 'id', 'answerId', 'submissionId') || `answer-${index + 1}`,
    questionId: readString(raw, 'questionId') || `q-${index + 1}`,
    orderIndex: readNumber(raw, 'orderIndex') || index + 1,
    title: readString(raw, 'questionTitle', 'title') || `Câu ${index + 1}`,
    content: readString(raw, 'questionContent', 'content') || null,
    isFollowUp,
    followUpIndex,
    parentSubmissionId,
    followUpPrompt: null,
    rawGeneratedFollowUpPrompt,
    status,
    transcript,
    aiFeedback: readString(raw, 'aiFeedback') || null,
    aiScore,
    lecturerScore,
    lecturerComment: notes,
  }
}

function resolveAskedFollowUpPrompt<
  T extends {
    questionId: string
    isFollowUp?: boolean
    followUpIndex?: number
    parentSubmissionId?: string | null
    rawGeneratedFollowUpPrompt: string | null
  },
>(item: T, byId: Map<string, T>, allRows: T[]): string | null {
  if (!item.isFollowUp) {
    return null
  }
  const parentById = item.parentSubmissionId ? byId.get(item.parentSubmissionId) : undefined
  if (parentById) {
    return parentById.rawGeneratedFollowUpPrompt
  }
  const targetParentTurn = Math.max(0, (item.followUpIndex ?? 1) - 1)
  const parentByTurn = allRows.find(
    (candidate) =>
      candidate !== item &&
      candidate.questionId === item.questionId &&
      (candidate.followUpIndex ?? (candidate.isFollowUp ? 1 : 0)) === targetParentTurn,
  )
  if (parentByTurn) {
    return parentByTurn.rawGeneratedFollowUpPrompt
  }
  return item.rawGeneratedFollowUpPrompt
}

function average(values: Array<number | null | undefined>) {
  const nums = values.filter((value): value is number => typeof value === 'number')
  if (nums.length === 0) return null
  return nums.reduce((sum, value) => sum + value, 0) / nums.length
}

function groupStudents(
  submissionRows: unknown[],
  participantRows: unknown[],
  shiftId: string,
  shiftLocked = false,
): InClassSubmission[] {
  const byStudent = new Map<
    string,
    {
      name: string
      code: string
      email: string
      joinedAt: string
      submittedAt: string | null
      participantStatus: string
      answers: ParsedSubmissionAnswerRow[]
      assigned: InClassQuestionAnswer[]
    }
  >()

  const ensure = (studentId: string) => {
    let row = byStudent.get(studentId)
    if (!row) {
      row = {
        name: 'Sinh viên',
        code: '',
        email: '',
        joinedAt: new Date().toISOString(),
        submittedAt: null,
        participantStatus: '',
        answers: [],
        assigned: [],
      }
      byStudent.set(studentId, row)
    }
    return row
  }

  participantRows.forEach((raw) => {
    if (!isRecord(raw)) return
    const studentId = readString(raw, 'studentId')
    if (!studentId) return
    const row = ensure(studentId)
    row.name = readString(raw, 'studentName', 'fullName') || row.name
    row.code = readString(raw, 'studentCode') || row.code
    row.email = readString(raw, 'studentEmail', 'email') || row.email
    row.joinedAt = readString(raw, 'joinedAt') || row.joinedAt
    row.participantStatus = readString(raw, 'status').toUpperCase()
    unwrapList(raw.assignedQuestions).forEach((item, index) => {
      if (!isRecord(item)) return
      const questionId = readString(item, 'questionId', 'id')
      if (!questionId) return
      row.assigned.push({
        answerId: `pending-${studentId}-${questionId}`,
        questionId,
        orderIndex: index + 1,
        mainOrderIndex: index + 1,
        title: readString(item, 'title') || `Câu ${index + 1}`,
        content: readString(item, 'content') || null,
        isFollowUp: false,
        followUpIndex: 0,
        status: 'PENDING',
      })
    })
  })

  submissionRows.forEach((raw, index) => {
    if (!isRecord(raw)) return
    const studentId = readString(raw, 'studentId')
    if (!studentId) return
    const row = ensure(studentId)
    row.name = readString(raw, 'studentName', 'fullName') || row.name
    row.code = readString(raw, 'studentCode') || row.code
    row.email = readString(raw, 'studentEmail', 'email') || row.email
    const submittedAt = readString(raw, 'submittedAt')
    if (submittedAt && (!row.submittedAt || submittedAt > row.submittedAt)) {
      row.submittedAt = submittedAt
    }
    row.answers.push(answerFromBeRow(raw, index))
  })

  return [...byStudent.entries()].map(([studentId, row]) => {
    const questionOrderById = new Map<string, number>()
    row.assigned.forEach((item, idx) => {
      if (!questionOrderById.has(item.questionId)) {
        questionOrderById.set(item.questionId, idx + 1)
      }
    })
    row.answers.forEach((item) => {
      if (!questionOrderById.has(item.questionId)) {
        questionOrderById.set(item.questionId, questionOrderById.size + 1)
      }
    })

    const submissionById = new Map(row.answers.map((item) => [item.answerId, item]))
    const normalizedSubmitted: InClassQuestionAnswer[] = row.answers.map((item) => {
      const resolvedPrompt = resolveAskedFollowUpPrompt(item, submissionById, row.answers)
      const mainOrderIndex = questionOrderById.get(item.questionId) ?? 1
      const { rawGeneratedFollowUpPrompt: _unusedRawPrompt, ...rest } = item
      void _unusedRawPrompt
      return {
        ...rest,
        mainOrderIndex,
        followUpPrompt: resolvedPrompt,
      }
    })

    const answeredIds = new Set(normalizedSubmitted.map((item) => item.questionId))
    const pending = row.assigned
      .filter((item) => !answeredIds.has(item.questionId))
      .map((item) => ({
        ...item,
        mainOrderIndex: questionOrderById.get(item.questionId) ?? item.orderIndex,
      }))

    const answers = [...normalizedSubmitted, ...pending]
      .sort((left, right) => {
        const leftMain = left.mainOrderIndex ?? left.orderIndex
        const rightMain = right.mainOrderIndex ?? right.orderIndex
        if (leftMain !== rightMain) return leftMain - rightMain
        const leftIsFu = left.isFollowUp ? 1 : 0
        const rightIsFu = right.isFollowUp ? 1 : 0
        if (leftIsFu !== rightIsFu) return leftIsFu - rightIsFu
        return (left.followUpIndex ?? 0) - (right.followUpIndex ?? 0)
      })
      .map((item, idx) => ({
        ...item,
        orderIndex: idx + 1,
      }))

    // Gom nhóm theo từng câu hỏi chính: câu phụ dùng để kéo điểm cao hơn (bù điểm), không kéo tụt điểm câu chính
    const questionGroups = new Map<string, InClassQuestionAnswer[]>()
    answers.forEach((ans) => {
      const list = questionGroups.get(ans.questionId) || []
      list.push(ans)
      questionGroups.set(ans.questionId, list)
    })

    const effectiveAiScores: number[] = []
    const effectiveFinalScores: number[] = []

    questionGroups.forEach((groupAnswers) => {
      const mainAns = groupAnswers.find((item) => !item.isFollowUp) || groupAnswers[0]
      const followUps = groupAnswers.filter((item) => item.isFollowUp && item.answerId !== mainAns.answerId)

      if (mainAns.aiScore != null) {
        let bestAi = mainAns.aiScore
        followUps.forEach((fu) => {
          if (fu.aiScore != null && fu.aiScore > bestAi) {
            bestAi = fu.aiScore
          }
        })
        effectiveAiScores.push(bestAi)
      } else {
        const anyFu = followUps.map((f) => f.aiScore).filter((s): s is number => s != null)
        if (anyFu.length > 0) {
          effectiveAiScores.push(Math.max(...anyFu))
        }
      }

      const mainFinal = mainAns.lecturerScore ?? mainAns.aiScore
      if (mainFinal != null) {
        let bestFinal = mainFinal
        followUps.forEach((fu) => {
          const fuFinal = fu.lecturerScore ?? fu.aiScore
          if (fuFinal != null && fuFinal > bestFinal) {
            bestFinal = fuFinal
          }
        })
        effectiveFinalScores.push(bestFinal)
      } else {
        const anyFuFinal = followUps
          .map((f) => f.lecturerScore ?? f.aiScore)
          .filter((s): s is number => s != null)
        if (anyFuFinal.length > 0) {
          effectiveFinalScores.push(Math.max(...anyFuFinal))
        }
      }
    })

    const aiScore = average(effectiveAiScores)
    const finalScore = average(effectiveFinalScores)
    const overridden = answers.some((item) => item.lecturerScore != null || item.status === 'AUDITED')
    const allDone = answers.length > 0 && answers.every((item) => item.status !== 'PENDING')
    const anyGraded = answers.some((item) => item.status === 'GRADED' || item.status === 'AUDITED')
    const participationDone =
      row.participantStatus === 'SUBMITTED' ||
      row.participantStatus === 'GRADING' ||
      row.participantStatus === 'AI_GRADED' ||
      row.participantStatus === 'GRADED' ||
      row.participantStatus === 'AUDITED' ||
      row.participantStatus === 'FINALIZED' ||
      row.participantStatus === 'COMPLETED'
    let status: InClassSubmission['status'] = 'JOINED'
    if (overridden && (allDone || participationDone)) status = 'AUDITED'
    else if (anyGraded && (allDone || participationDone)) status = 'GRADED'
    else if (allDone || participationDone) status = aiScore != null || finalScore != null ? 'GRADED' : 'GRADING'
    else if (answers.some((item) => item.status !== 'PENDING') || row.participantStatus === 'IN_PROGRESS') {
      status = 'IN_PROGRESS'
    }
    if (shiftLocked && status !== 'JOINED' && status !== 'IN_PROGRESS') status = 'FINALIZED'
    return {
      id: studentId,
      shiftId,
      studentId,
      studentCode: row.code || '—',
      studentEmail: row.email || null,
      studentName: row.name,
      status,
      aiScore,
      finalScore,
      overridden,
      overrideReason: answers.find((item) => item.lecturerComment)?.lecturerComment ?? null,
      transcript: answers.find((item) => item.transcript)?.transcript ?? null,
      answers,
      joinedAt: row.joinedAt,
      submittedAt: row.submittedAt,
    }
  })
}

function normalizePoolStatsPayload(
  raw: unknown,
  fallbackClassId: string,
  fallbackCourseId = '',
): InClassShiftPoolStats | null {
  if (!isRecord(raw)) return null
  const rawClos = Array.isArray(raw.clos)
    ? raw.clos
    : Array.isArray(raw.Clos)
      ? raw.Clos
      : []
  const clos: InClassCloPoolStat[] = []
  for (const item of rawClos) {
    if (!isRecord(item)) continue
    const cloId = readString(item, 'cloId', 'CloId', 'id', 'Id')
    if (!cloId) continue
    clos.push({
      cloId,
      cloCode: readString(item, 'cloCode', 'CloCode') || 'CLO',
      description: readString(item, 'description', 'Description'),
      counts: normalizeDifficultyCounts(item.counts ?? item.Counts ?? item),
    })
  }
  return {
    classId: readString(raw, 'classId', 'ClassId') || fallbackClassId,
    courseId: readString(raw, 'courseId', 'CourseId') || fallbackCourseId,
    courseCode: readString(raw, 'courseCode', 'CourseCode'),
    courseBankCounts: normalizeDifficultyCounts(
      raw.courseBankCounts ?? raw.CourseBankCounts ?? raw.bankCounts ?? raw,
    ),
    clos,
  }
}

export const inClassShiftService = {
  async getLecturerClasses(): Promise<LecturerClassOption[]> {
    const response = await apiClient.get<unknown>('/lecturer/classes')
    const list: LecturerClassOption[] = []
    for (const item of unwrapList(response.data)) {
      if (!isRecord(item)) continue
      const id = readString(item, 'classId', 'id')
      if (!id) continue
      const studentCount = readNumber(item, 'enrolledCount', 'studentCount')
      const courseId = readString(item, 'courseId')
      list.push({
        id,
        name: readString(item, 'name', 'className', 'code') || 'Lớp',
        ...(courseId ? { courseId } : {}),
        courseCode: readString(item, 'courseCode', 'code') || '',
        courseName: readString(item, 'courseName') || '',
        ...(studentCount > 0 ? { studentCount } : {}),
      })
    }
    return list
  },

  async getPoolStats(classId: string, courseId?: string): Promise<InClassShiftPoolStats> {
    try {
      const response = await apiClient.get<unknown>('/lecturer/in-class-shifts/pool-stats', {
        params: { classId },
      })
      const parsed = normalizePoolStatsPayload(response.data, classId, courseId)
      if (!parsed) {
        throw Object.assign(new Error('Phản hồi thống kê ngân hàng câu hỏi không hợp lệ.'), {
          status: 502,
          title: 'Phản hồi không hợp lệ',
          detail: 'Máy chủ không trả về thống kê ngân hàng câu hỏi hợp lệ cho lớp này.',
        })
      }
      return parsed
    } catch (primaryError) {
      const errStatus = isRecord(primaryError) ? readNumber(primaryError, 'status') : 0
      if (errStatus !== 404 || !courseId) {
        throw primaryError
      }

      // Dự phòng đọc dữ liệu thật từ API CLO và câu hỏi APPROVED khi endpoint pool-stats chưa được BE bật (tuyệt đối không mock dữ liệu giả)
      const [closRes, firstPageRes] = await Promise.all([
        apiClient.get<unknown>(`/courses/${encodeURIComponent(courseId)}/clos`, {
          params: { activeOnly: true },
        }),
        apiClient.get<unknown>('/questions', {
          params: {
            courseId,
            approvalStatus: 'APPROVED',
            usageScope: 'practice',
            pageNumber: 1,
            pageSize: 100,
          },
        }),
      ])

      const firstRecord = isRecord(firstPageRes.data) ? firstPageRes.data : {}
      const totalPages = Math.max(1, readNumber(firstRecord, 'totalPages', 'TotalPages') || 1)
      const extraPages =
        totalPages > 1
          ? await Promise.all(
              Array.from({ length: totalPages - 1 }, (_, index) =>
                apiClient.get<unknown>('/questions', {
                  params: {
                    courseId,
                    approvalStatus: 'APPROVED',
                    usageScope: 'practice',
                    pageNumber: index + 2,
                    pageSize: 100,
                  },
                }),
              ),
            )
          : []

      const allQuestions = [
        ...unwrapList(firstPageRes.data),
        ...extraPages.flatMap((page) => unwrapList(page.data)),
      ].filter(isRecord)

      const courseBankCounts: InClassDifficultyCounts = { easy: 0, medium: 0, hard: 0, total: 0 }
      const countsByCloId = new Map<string, InClassDifficultyCounts>()
      let courseCode = ''

      for (const q of allQuestions) {
        if (q.isActive === false || q.IsActive === false) continue
        const approval = readString(q, 'approvalStatus', 'ApprovalStatus').toUpperCase()
        if (approval && approval !== 'APPROVED') continue
        const diff = normalizeDifficulty(readString(q, 'difficulty', 'Difficulty'))
        if (!diff) continue
        if (!courseCode) courseCode = readString(q, 'courseCode', 'CourseCode')
        courseBankCounts[diff] += 1
        courseBankCounts.total += 1
        const cloId = readString(q, 'cloId', 'CloId')
        if (cloId) {
          const entry = countsByCloId.get(cloId) ?? { easy: 0, medium: 0, hard: 0, total: 0 }
          entry[diff] += 1
          entry.total += 1
          countsByCloId.set(cloId, entry)
        }
      }

      const clos: InClassCloPoolStat[] = unwrapList(closRes.data)
        .filter(isRecord)
        .filter((item) => item.isActive !== false && item.IsActive !== false)
        .map((item) => {
          const id = readString(item, 'id', 'Id', 'cloId', 'CloId')
          return {
            cloId: id,
            cloCode: readString(item, 'cloCode', 'CloCode') || 'CLO',
            description: readString(item, 'description', 'Description'),
            counts: countsByCloId.get(id) ?? { easy: 0, medium: 0, hard: 0, total: 0 },
          }
        })
        .filter((item) => Boolean(item.cloId))

      return {
        classId,
        courseId,
        courseCode,
        courseBankCounts,
        clos,
      }
    }
  },

  async listShifts(params?: { classId?: string }): Promise<InClassShift[]> {
    if (params?.classId) {
      const response = await apiClient.get<unknown>(
        `/lecturer/in-class-shifts/by-class/${params.classId}`,
      )
      return unwrapList(response.data)
        .map((item) => normalizeShift(item))
        .filter((item): item is InClassShift => Boolean(item))
    }
    const classes = await this.getLecturerClasses()
    const batches = await Promise.all(
      classes.map((item) => this.listShifts({ classId: item.id })),
    )
    const seen = new Set<string>()
    return batches.flat().filter((shift) => {
      if (seen.has(shift.id)) return false
      seen.add(shift.id)
      return true
    })
  },

  async getShift(shiftId: string): Promise<InClassShift> {
    const response = await apiClient.get<unknown>(`/lecturer/in-class-shifts/${shiftId}`)
    const shift = normalizeShift(response.data)
    if (!shift) {
      throw Object.assign(new Error('Không tìm thấy ca thi.'), { detail: 'Không tìm thấy ca thi.' })
    }
    return shift
  },

  async createShift(payload: CreateInClassShiftRequest): Promise<InClassShift> {
    const requestBody: Record<string, unknown> = {
      classId: payload.classId,
      shiftName: payload.shiftName.trim(),
      startTime: new Date(payload.startTime).toISOString(),
      endTime: new Date(payload.endTime).toISOString(),
      description: payload.description?.trim() || null,
      questionPoolMode: payload.questionPoolMode,
      totalQuestionsPerStudent: payload.totalQuestionsPerStudent,
      selectedDifficulties: payload.selectedDifficulties,
      ...(payload.questionPoolMode === 'SELECTED_CLOS'
        ? { selectedCloIds: payload.selectedCloIds ?? [] }
        : {}),
      hasFollowUp: payload.hasFollowUp,
      maxFollowUpQuestions: payload.hasFollowUp ? payload.maxFollowUpQuestions : 0,
      isAutoPublishGrades: payload.isAutoPublishGrades,
      examDurationMinutes: payload.examDurationMinutes,
    }
    const response = await apiClient.post<unknown>('/lecturer/in-class-shifts', requestBody)
    const shift = normalizeShift(response.data)
    if (!shift) {
      throw Object.assign(new Error('Không tạo được ca thi.'), { detail: 'Không tạo được ca thi.' })
    }
    return shift
  },

  async getActivePassword(shiftId: string): Promise<ActiveTotpPassword> {
    const response = await apiClient.get<unknown>(
      `/lecturer/in-class-shifts/${shiftId}/active-password`,
    )
    if (!isRecord(response.data)) {
      throw Object.assign(new Error('Không có mã OTP.'), { detail: 'Không có mã OTP.' })
    }
    const code = readString(response.data, 'currentPassword', 'currentOtpCode', 'otpCode')
    if (!code) {
      throw Object.assign(new Error('Không có mã OTP.'), { detail: 'Không có mã OTP.' })
    }
    const expiresAt = readString(response.data, 'validTo', 'expiresAt')
    const generatedAt = readString(response.data, 'validFrom', 'generatedAt')
    return {
      shiftId: readString(response.data, 'shiftId') || shiftId,
      currentOtpCode: code.toUpperCase(),
      generatedAt: generatedAt || new Date().toISOString(),
      expiresAt: expiresAt || new Date(Date.now() + 300_000).toISOString(),
      gracePeriodSeconds: readNumber(response.data, 'gracePeriodSeconds'),
      remainingSeconds: readNumber(response.data, 'remainingSeconds') || 300,
      previousOtpCode: null,
    }
  },

  async listSubmissions(shiftId: string): Promise<InClassSubmission[]> {
    const [submissions, detail] = await Promise.all([
      apiClient.get<unknown>(`/lecturer/in-class-shifts/${shiftId}/submissions`),
      apiClient.get<unknown>(`/lecturer/in-class-shifts/${shiftId}`),
    ])
    const detailRecord = isRecord(detail.data) ? detail.data : null
    const participants = detailRecord ? unwrapList(detailRecord.participants) : []
    const shiftLocked = Boolean(
      detailRecord &&
        (readBool(detailRecord, 'isLocked') || readString(detailRecord, 'status').toUpperCase() === 'FINALIZED'),
    )
    return groupStudents(unwrapList(submissions.data), participants, shiftId, shiftLocked)
  },

  async gradeSubmission(
    shiftId: string,
    submissionId: string,
    payload: GradeInClassSubmissionRequest,
  ): Promise<InClassSubmission> {
    await apiClient.put(
      `/lecturer/in-class-shifts/${shiftId}/submissions/${submissionId}/grade`,
      { finalScore: payload.adjustedScore, lecturerNotes: payload.overrideReason },
    )
    const list = await this.listSubmissions(shiftId)
    const updated = list.find(
      (item) =>
        item.id === submissionId || item.answers.some((answer) => answer.answerId === submissionId),
    )
    if (!updated) {
      throw Object.assign(new Error('Không tìm thấy bài nộp.'), { detail: 'Không tìm thấy bài nộp.' })
    }
    return updated
  },

  async gradeAnswer(
    shiftId: string,
    submissionId: string,
    answerId: string,
    payload: GradeInClassAnswerRequest,
  ): Promise<InClassSubmission> {
    await apiClient.put(
      `/lecturer/in-class-shifts/${shiftId}/submissions/${answerId}/grade`,
      {
        finalScore: payload.lecturerScore,
        lecturerNotes: payload.lecturerComment?.trim() || null,
      },
    )
    const list = await this.listSubmissions(shiftId)
    const updated = list.find(
      (item) => item.id === submissionId || item.answers.some((answer) => answer.answerId === answerId),
    )
    if (!updated) {
      throw Object.assign(new Error('Không tìm thấy bài nộp.'), { detail: 'Không tìm thấy bài nộp.' })
    }
    return updated
  },

  async gradeExam(
    shiftId: string,
    studentId: string,
    answerIds: string[],
    payload: GradeInClassAnswerRequest,
  ): Promise<InClassSubmission> {
    if (answerIds.length === 0) {
      throw Object.assign(new Error('Chưa có bài nộp để chấm.'), {
        detail: 'Chưa có bài nộp để chấm.',
      })
    }
    const notes = payload.lecturerComment?.trim() || null
    for (const answerId of answerIds) {
      await apiClient.put(
        `/lecturer/in-class-shifts/${shiftId}/submissions/${answerId}/grade`,
        { finalScore: payload.lecturerScore, lecturerNotes: notes },
      )
    }
    const list = await this.listSubmissions(shiftId)
    const updated = list.find((item) => item.id === studentId)
    if (!updated) {
      throw Object.assign(new Error('Không tìm thấy bài nộp.'), { detail: 'Không tìm thấy bài nộp.' })
    }
    return updated
  },

  async activateShift(shiftId: string): Promise<void> {
    await apiClient.post(`/lecturer/in-class-shifts/${shiftId}/activate`)
  },

  async closeShift(shiftId: string): Promise<void> {
    await apiClient.post(`/lecturer/in-class-shifts/${shiftId}/close`)
  },

  async getStudentShift(shiftId: string): Promise<InClassShift> {
    const response = await apiClient.get<unknown>(`/student/in-class-shifts/${shiftId}`)
    const shift = normalizeShift(response.data)
    if (!shift) {
      throw Object.assign(new Error('Không tìm thấy ca thi.'), { detail: 'Không tìm thấy ca thi.' })
    }
    return shift
  },

  async listStudentShifts(): Promise<InClassShift[]> {
    try {
      const direct = await apiClient.get<unknown>('/student/in-class-shifts/my')
      return unwrapList(direct.data)
        .map((item) => normalizeShift(item))
        .filter((item): item is InClassShift => Boolean(item))
    } catch (directError) {
      const errStatus = isRecord(directError) ? readNumber(directError, 'status') : 0
      if (errStatus !== 404) {
        throw directError
      }
    }

    const classes = await apiClient.get<unknown>('/classes/my')
    const classIds = unwrapList(classes.data)
      .filter(isRecord)
      .map((item) => readString(item, 'classId', 'id'))
      .filter(Boolean)
    const batches = await Promise.all(
      classIds.map(async (classId) => {
        const response = await apiClient.get<unknown>(`/student/in-class-shifts/by-class/${classId}`)
        return unwrapList(response.data)
          .map((item) => normalizeShift(item))
          .filter((item): item is InClassShift => Boolean(item))
      }),
    )
    const seen = new Set<string>()
    return batches.flat().filter((shift) => {
      if (seen.has(shift.id)) return false
      seen.add(shift.id)
      return true
    })
  },

  async enterStudentShift(
    shiftId: string,
    otpCode: string,
    fallbackDurationMinutes = 20,
  ): Promise<EnterInClassExamResult> {
    const response = await apiClient.post<unknown>(`/student/in-class-shifts/${shiftId}/enter`, {
      otpCode: otpCode.trim(),
    })
    return parseEnterResult(response.data, fallbackDurationMinutes)
  },

  async resumeStudentShift(
    shiftId: string,
    fallbackDurationMinutes = 20,
  ): Promise<EnterInClassExamResult> {
    const response = await apiClient.post<unknown>(`/student/in-class-shifts/${shiftId}/resume`)
    return parseEnterResult(response.data, fallbackDurationMinutes)
  },

  async submitStudentAnswer(
    shiftId: string,
    questionId: string,
    transcript: string,
    options?: {
      isFollowUp?: boolean
      parentSubmissionId?: string | null
      followUpIndex?: number
    },
  ): Promise<SubmitStudentInClassAnswerResponse> {
    const response = await apiClient.post<unknown>(
      `/student/in-class-shifts/${shiftId}/submit-answer`,
      {
        questionId,
        transcriptText: transcript.trim(),
        isFollowUp: options?.isFollowUp ?? false,
        parentSubmissionId: options?.parentSubmissionId ?? null,
        followUpIndex: options?.followUpIndex ?? 0,
      },
    )
    const record = isRecord(response.data) ? response.data : {}
    return {
      submissionId: readString(record, 'submissionId', 'id'),
      shiftId: readString(record, 'shiftId') || shiftId,
      questionId: readString(record, 'questionId') || questionId,
      status: readString(record, 'status') || 'SUBMITTED',
      isFollowUp: readBool(record, 'isFollowUp'),
      parentSubmissionId: readString(record, 'parentSubmissionId') || null,
      followUpIndex: readNumber(record, 'followUpIndex'),
    }
  },

  async requestNextFollowUp(
    shiftId: string,
    submissionId: string,
  ): Promise<InClassNextFollowUp> {
    const response = await apiClient.post<unknown>(
      `/student/in-class-shifts/${shiftId}/submissions/${submissionId}/next-followup`,
    )
    const record = isRecord(response.data) ? response.data : {}
    return {
      hasMoreFollowUp: readBool(record, 'hasMoreFollowUp'),
      followUpIndex: readNumber(record, 'followUpIndex'),
      maxFollowUpQuestions: readNumber(record, 'maxFollowUpQuestions'),
      parentSubmissionId: readString(record, 'parentSubmissionId') || submissionId,
      questionId: readString(record, 'questionId'),
      followUpPrompt: readString(record, 'followUpPrompt') || null,
    }
  },

  async finishStudentExam(shiftId: string): Promise<void> {
    await apiClient.post(`/student/in-class-shifts/${shiftId}/finish`)
  },

  async listStudentResults(shiftId: string): Promise<StudentExamResult[]> {
    const response = await apiClient.get<unknown>(`/student/in-class-shifts/${shiftId}/my-submissions`)
    const parsed = unwrapList(response.data)
      .filter(isRecord)
      .map((item, rawIndex) => {
        const isFollowUp = readBool(item, 'isFollowUp')
        const submittedAtRaw = readString(item, 'submittedAt')
        const parsedTime = submittedAtRaw ? Date.parse(submittedAtRaw) : Number.NaN
        return {
          submissionId: readString(item, 'id', 'submissionId'),
          questionId: readString(item, 'questionId'),
          title: readString(item, 'questionTitle', 'title') || 'Câu hỏi',
          content: readString(item, 'questionContent', 'content'),
          transcript: readString(item, 'transcript', 'transcriptText'),
          aiScore: readNullableNumber(item, 'aiScore'),
          aiFeedback: readString(item, 'aiFeedback') || null,
          finalScore: readNullableNumber(item, 'finalScore'),
          lecturerNotes: readString(item, 'lecturerNotes') || null,
          status: readString(item, 'status'),
          isFollowUp,
          followUpIndex: readNumber(item, 'followUpIndex') || (isFollowUp ? 1 : 0),
          parentSubmissionId: readString(item, 'parentSubmissionId') || null,
          rawGeneratedFollowUpPrompt: readString(item, 'followUpPrompt') || null,
          submittedAtMs: Number.isFinite(parsedTime) ? parsedTime : null,
          rawIndex,
        }
      })
      .filter((item) => item.submissionId)

    const chronologicalCandidates = [...parsed].sort((left, right) => {
      const leftIsFu = left.isFollowUp ? 1 : 0
      const rightIsFu = right.isFollowUp ? 1 : 0
      if (leftIsFu !== rightIsFu) return leftIsFu - rightIsFu
      if (left.submittedAtMs != null && right.submittedAtMs != null && left.submittedAtMs !== right.submittedAtMs) {
        return left.submittedAtMs - right.submittedAtMs
      }
      return left.rawIndex - right.rawIndex
    })

    const questionOrderById = new Map<string, number>()
    chronologicalCandidates.forEach((item) => {
      if (!questionOrderById.has(item.questionId)) {
        questionOrderById.set(item.questionId, questionOrderById.size + 1)
      }
    })
    const byId = new Map(parsed.map((item) => [item.submissionId, item]))

    return parsed
      .map((item) => {
        const resolvedPrompt = resolveAskedFollowUpPrompt(item, byId, parsed)
        const {
          rawGeneratedFollowUpPrompt: _unusedRawPrompt,
          submittedAtMs: _unusedSubmittedAtMs,
          rawIndex: _unusedRawIndex,
          ...rest
        } = item
        void _unusedRawPrompt
        void _unusedSubmittedAtMs
        void _unusedRawIndex
        return {
          ...rest,
          mainOrderIndex: questionOrderById.get(item.questionId) ?? 1,
          followUpPrompt: resolvedPrompt,
        }
      })
      .sort((left, right) => {
        const leftMain = left.mainOrderIndex ?? 1
        const rightMain = right.mainOrderIndex ?? 1
        if (leftMain !== rightMain) return leftMain - rightMain
        const leftIsFu = left.isFollowUp ? 1 : 0
        const rightIsFu = right.isFollowUp ? 1 : 0
        if (leftIsFu !== rightIsFu) return leftIsFu - rightIsFu
        return (left.followUpIndex ?? 0) - (right.followUpIndex ?? 0)
      })
  },

  async finalizeGrades(shiftId: string): Promise<FinalizeInClassGradesResponse> {
    const response = await apiClient.post<unknown>(
      `/lecturer/in-class-shifts/${shiftId}/finalize-grades`,
    )
    const data = isRecord(response.data) ? response.data : {}
    return {
      shiftId: readString(data, 'shiftId') || shiftId,
      status: 'FINALIZED',
      isLocked: readBool(data, 'isLocked') || true,
      finalizedAt: readString(data, 'finalizedAt') || new Date().toISOString(),
      message: 'Đã chốt bảng điểm ca thi trong lớp và kích hoạt One-Way Lock.',
    }
  },
}
