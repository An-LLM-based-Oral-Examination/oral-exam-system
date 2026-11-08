import { apiClient } from '@/services/api.client'
import type {
  ActiveTotpPassword,
  CreateInClassShiftRequest,
  FinalizeInClassGradesResponse,
  GradeInClassAnswerRequest,
  GradeInClassSubmissionRequest,
  InClassNextFollowUp,
  InClassQuestionAnswer,
  InClassShift,
  InClassSubmission,
  LecturerClassOption,
  StudentExamQuestion,
  StudentExamResult,
  SubmitStudentInClassAnswerResponse,
} from '@/types/inClassShift.types'

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
  const participants = Array.isArray(raw.participants) ? raw.participants.filter(isRecord) : []
  if (participants.length > 0) {
    return participants.filter((item) => readString(item, 'status').toUpperCase() === 'SUBMITTED').length
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
  const isLocked = readBool(raw, 'isLocked')
  const startTime = readString(raw, 'startTime')
  const endTime = readString(raw, 'endTime')
  const durationMinutes = readNumber(raw, 'durationMinutes') || durationFromRange(startTime, endTime)
  return {
    id,
    classId: readString(raw, 'classId') || fallbackClass?.id || '',
    className:
      readString(raw, 'className', 'classCode') || fallbackClass?.name || 'Lớp học phần',
    courseCode: readString(raw, 'courseCode', 'classCode') || fallbackClass?.courseCode || '',
    title: readString(raw, 'shiftName', 'title') || 'Ca thi trên lớp',
    description: readString(raw, 'description'),
    status: mapShiftStatus(statusRaw, isLocked),
    startTime,
    durationMinutes,
    examDurationMinutes: (() => {
      const minutes = readNullableNumber(raw, 'examDurationMinutes')
      return minutes != null && minutes > 0 ? minutes : null
    })(),
    questionCount: readNumber(raw, 'totalQuestionsPerStudent', 'questionCount') || 1,
    hasFollowUp: readBool(raw, 'hasFollowUp'),
    maxFollowUpQuestions: readNumber(raw, 'maxFollowUpQuestions'),
    questionPoolMode: readString(raw, 'questionPoolMode') === 'SELECTED_POOL' ? 'SELECTED_POOL' : 'ALL_COURSE_BANK',
    passwordRotationSeconds: 300,
    gracePeriodSeconds: 0,
    joinedCount:
      readNumber(raw, 'participantsCount', 'joinedCount', 'participantCount') ||
      (Array.isArray(raw.participants) ? raw.participants.length : 0),
    submittedCount: countSubmittedStudents(raw),
    createdAt: readString(raw, 'createdAt') || new Date().toISOString(),
    finalizedAt: readString(raw, 'finalizedAt') || null,
    endTime: endTime || null,
    myParticipationStatus:
      readString(raw, 'myParticipationStatus', 'MyParticipationStatus') || null,
  }
}

function answerFromBeRow(raw: Record<string, unknown>, index: number): InClassQuestionAnswer {
  const statusRaw = readString(raw, 'status').toUpperCase()
  const transcript = readString(raw, 'transcript', 'transcriptText') || null
  const notes = readString(raw, 'lecturerNotes', 'lecturerComment') || null
  const audited = statusRaw === 'AUDITED' || Boolean(notes)
  const aiScore = readNullableNumber(raw, 'aiScore')
  const finalScore = readNullableNumber(raw, 'finalScore', 'lecturerScore')
  const lecturerScore = audited ? finalScore : null
  const status: InClassQuestionAnswer['status'] = lecturerScore != null
    ? 'GRADED'
    : transcript
      ? 'ANSWERED'
      : 'PENDING'
  const followUpIndex = readNumber(raw, 'followUpIndex')
  return {
    answerId: readString(raw, 'id', 'answerId', 'submissionId') || `answer-${index + 1}`,
    questionId: readString(raw, 'questionId') || `q-${index + 1}`,
    orderIndex:
      readNumber(raw, 'orderIndex') ||
      (readBool(raw, 'isFollowUp') ? 100 + followUpIndex : index + 1),
    title: readString(raw, 'questionTitle', 'title') || `Câu ${index + 1}`,
    content: readString(raw, 'questionContent', 'content') || null,
    isFollowUp: readBool(raw, 'isFollowUp'),
    status,
    transcript,
    aiFeedback: readString(raw, 'aiFeedback') || null,
    aiScore,
    lecturerScore,
    lecturerComment: notes,
  }
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
      answers: InClassQuestionAnswer[]
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
        title: readString(item, 'title') || `Câu ${index + 1}`,
        content: readString(item, 'content') || null,
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
    const answeredIds = new Set(row.answers.map((item) => item.questionId))
    const pending = row.assigned.filter((item) => !answeredIds.has(item.questionId))
    const answers = [...row.answers, ...pending].sort((a, b) => a.orderIndex - b.orderIndex)
    const aiScore = average(answers.map((item) => item.aiScore))
    const finalScore = average(answers.map((item) => item.lecturerScore ?? item.aiScore))
    const overridden = answers.some((item) => item.lecturerScore != null)
    const allDone = answers.length > 0 && answers.every((item) => item.status !== 'PENDING')
    const anyGraded = answers.some((item) => item.status === 'GRADED')
    let status: InClassSubmission['status'] = 'JOINED'
    if (anyGraded && allDone) status = 'GRADED'
    else if (allDone) status = 'SUBMITTED'
    else if (answers.some((item) => item.status !== 'PENDING')) status = 'IN_PROGRESS'
    else if (row.participantStatus === 'IN_PROGRESS') status = 'IN_PROGRESS'
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
    const response = await apiClient.post<unknown>('/lecturer/in-class-shifts', {
      classId: payload.classId,
      shiftName: payload.shiftName.trim(),
      startTime: new Date(payload.startTime).toISOString(),
      endTime: new Date(payload.endTime).toISOString(),
      description: payload.description?.trim() || null,
      questionPoolMode: payload.questionPoolMode,
      selectedQuestionIds:
        payload.questionPoolMode === 'SELECTED_POOL' ? payload.selectedQuestionIds ?? [] : [],
      totalQuestionsPerStudent: payload.totalQuestionsPerStudent,
      hasFollowUp: payload.hasFollowUp,
      maxFollowUpQuestions: payload.hasFollowUp ? payload.maxFollowUpQuestions : 0,
      isAutoPublishGrades: payload.isAutoPublishGrades,
      examDurationMinutes: payload.examDurationMinutes,
    })
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
      const normalized = unwrapList(direct.data)
        .map((item) => normalizeShift(item))
        .filter((item): item is InClassShift => Boolean(item))
      if (normalized.length > 0) {
        return normalized
      }
    } catch {
      // Fallback sang truy vấn từng lớp học phần
    }

    const classes = await apiClient.get<unknown>('/student/classes/my-classes')
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

  async enterStudentShift(shiftId: string, otpCode: string): Promise<StudentExamQuestion[]> {
    const response = await apiClient.post<unknown>(`/student/in-class-shifts/${shiftId}/enter`, {
      otpCode: otpCode.trim(),
    })
    if (!isRecord(response.data)) return []
    const assigned = unwrapList(response.data.assignedQuestions)
    const questions = assigned.filter(isRecord).map((item) => ({
      questionId: readString(item, 'questionId'),
      title: readString(item, 'title') || 'Câu hỏi',
      content: readString(item, 'content'),
    })).filter((item) => item.questionId)
    if (questions.length > 0) return questions
    const single = response.data.assignedQuestion
    if (!isRecord(single)) return []
    const questionId = readString(single, 'questionId')
    if (!questionId) return []
    return [{
      questionId,
      title: readString(single, 'title') || 'Câu hỏi',
      content: readString(single, 'content'),
    }]
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

  /**
   * Nộp nhanh khi đóng tab / rời trang — dùng fetch keepalive (axios thường bị hủy).
   */
  flushStudentExamOnLeave(
    shiftId: string,
    answers: Array<{ questionId: string; transcript: string }>,
  ): void {
    const base = (import.meta.env.VITE_API_BASE_URL || '/api/v1').replace(/\/$/, '')
    const token = sessionStorage.getItem('access_token')
    const headers: Record<string, string> = { 'Content-Type': 'application/json' }
    if (token) headers.Authorization = `Bearer ${token}`

    for (const answer of answers) {
      const transcript = answer.transcript.trim()
      if (!transcript || !answer.questionId) continue
      void fetch(`${base}/student/in-class-shifts/${encodeURIComponent(shiftId)}/submit-answer`, {
        method: 'POST',
        headers,
        body: JSON.stringify({
          questionId: answer.questionId,
          transcriptText: transcript,
          isFollowUp: false,
          followUpIndex: 0,
        }),
        keepalive: true,
      }).catch(() => undefined)
    }

    void fetch(`${base}/student/in-class-shifts/${encodeURIComponent(shiftId)}/finish`, {
      method: 'POST',
      headers,
      body: '{}',
      keepalive: true,
    }).catch(() => undefined)
  },

  async listStudentResults(shiftId: string): Promise<StudentExamResult[]> {
    const response = await apiClient.get<unknown>(`/student/in-class-shifts/${shiftId}/my-submissions`)
    return unwrapList(response.data)
      .filter(isRecord)
      .map((item) => ({
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
        isFollowUp: readBool(item, 'isFollowUp'),
        followUpIndex: readNumber(item, 'followUpIndex'),
        followUpPrompt: readString(item, 'followUpPrompt') || null,
      }))
      .filter((item) => item.submissionId)
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
