import { apiClient } from '@/services/api.client'
import type {
  ActiveTotpPassword,
  CreateInClassShiftRequest,
  FinalizeInClassGradesResponse,
  GradeInClassAnswerRequest,
  GradeInClassSubmissionRequest,
  InClassQuestionAnswer,
  InClassShift,
  InClassSubmission,
  LecturerClassOption,
} from '@/types/inClassShift.types'

const MOCK_KEY = 'mf05_in_class_shifts_v1'
const MOCK_SUB_KEY = 'mf05_in_class_submissions_v4'

const MOCK_CLASSES: LecturerClassOption[] = [
  {
    id: 'class-se1830',
    name: 'SE1830',
    courseCode: 'PRN232',
    courseName: 'Advanced Cross-Platform Application Programming With .NET',
    studentCount: 32,
  },
  {
    id: 'class-se1831',
    name: 'SE1831',
    courseCode: 'PRN232',
    courseName: 'Advanced Cross-Platform Application Programming With .NET',
    studentCount: 28,
  },
  {
    id: 'class-se1840',
    name: 'SE1840',
    courseCode: 'SWD392',
    courseName: 'SWP Topic',
    studentCount: 30,
  },
]

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

function normalizeShift(raw: unknown, fallbackClass?: LecturerClassOption): InClassShift | null {
  if (!isRecord(raw)) return null
  const id = readString(raw, 'id', 'shiftId')
  if (!id) return null
  const statusRaw = readString(raw, 'status').toUpperCase()
  const status =
    statusRaw === 'FINALIZED' || statusRaw === 'CLOSED' || statusRaw === 'ACTIVE'
      ? statusRaw
      : 'ACTIVE'
  return {
    id,
    classId: readString(raw, 'classId') || fallbackClass?.id || '',
    className:
      readString(raw, 'className', 'classCode') || fallbackClass?.name || 'Lớp học phần',
    courseCode: readString(raw, 'courseCode') || fallbackClass?.courseCode || '',
    title: readString(raw, 'title') || 'Ca thi trên lớp',
    status,
    durationMinutes: readNumber(raw, 'durationMinutes') || 20,
    questionCount: readNumber(raw, 'questionCount') || 3,
    hasFollowUp: readBool(raw, 'hasFollowUp'),
    passwordRotationSeconds: readNumber(raw, 'passwordRotationSeconds') || 300,
    gracePeriodSeconds: readNumber(raw, 'gracePeriodSeconds') || 60,
    joinedCount: readNumber(raw, 'joinedCount', 'participantCount'),
    submittedCount: readNumber(raw, 'submittedCount'),
    createdAt: readString(raw, 'createdAt') || new Date().toISOString(),
    finalizedAt: readString(raw, 'finalizedAt') || null,
  }
}

function normalizeAnswer(raw: unknown, index: number): InClassQuestionAnswer | null {
  if (!isRecord(raw)) return null
  const answerId = readString(raw, 'answerId', 'id') || `answer-${index + 1}`
  const statusRaw = readString(raw, 'status').toUpperCase()
  const status: InClassQuestionAnswer['status'] =
    statusRaw === 'GRADED' || statusRaw === 'ANSWERED' || statusRaw === 'PENDING'
      ? statusRaw
      : readString(raw, 'transcript')
        ? 'ANSWERED'
        : 'PENDING'
  return {
    answerId,
    questionId: readString(raw, 'questionId') || answerId,
    orderIndex: readNumber(raw, 'orderIndex') || index + 1,
    title: readString(raw, 'title', 'questionTitle') || `Câu ${index + 1}`,
    content: readString(raw, 'content', 'questionContent') || null,
    isFollowUp: readBool(raw, 'isFollowUp'),
    status,
    transcript: readString(raw, 'transcript') || null,
    aiFeedback: readString(raw, 'aiFeedback', 'aiComment', 'rationale') || null,
    aiScore: typeof raw.aiScore === 'number' ? raw.aiScore : null,
    lecturerScore: typeof raw.lecturerScore === 'number' ? raw.lecturerScore : null,
    lecturerComment: readString(raw, 'lecturerComment', 'overrideReason') || null,
  }
}

function normalizeAnswers(raw: Record<string, unknown>): InClassQuestionAnswer[] {
  const fromList = unwrapList(raw.answers ?? raw.questionAnswers ?? raw.items)
    .map((item, index) => normalizeAnswer(item, index))
    .filter((item): item is InClassQuestionAnswer => Boolean(item))
    .sort((a, b) => a.orderIndex - b.orderIndex)
  if (fromList.length > 0) return fromList

  const legacy = readString(raw, 'transcript')
  if (!legacy) return []
  return [
    {
      answerId: 'legacy-1',
      questionId: 'legacy-q1',
      orderIndex: 1,
      title: 'Câu 1',
      status: 'ANSWERED',
      transcript: legacy,
      aiScore: typeof raw.aiScore === 'number' ? raw.aiScore : null,
    },
  ]
}

function normalizeSubmission(raw: unknown, shiftId: string): InClassSubmission | null {
  if (!isRecord(raw)) return null
  const id = readString(raw, 'id', 'submissionId')
  if (!id) return null
  const statusRaw = readString(raw, 'status').toUpperCase()
  const status = (
    ['JOINED', 'IN_PROGRESS', 'SUBMITTED', 'GRADED', 'FINALIZED'] as const
  ).includes(statusRaw as InClassSubmission['status'])
    ? (statusRaw as InClassSubmission['status'])
    : 'SUBMITTED'
  const aiScore =
    typeof raw.aiScore === 'number'
      ? raw.aiScore
      : typeof raw.suggestedScore === 'number'
        ? raw.suggestedScore
        : null
  const finalScore =
    typeof raw.finalScore === 'number'
      ? raw.finalScore
      : typeof raw.adjustedScore === 'number'
        ? raw.adjustedScore
        : aiScore
  const answers = normalizeAnswers(raw)
  return {
    id,
    shiftId: readString(raw, 'shiftId') || shiftId,
    studentId: readString(raw, 'studentId') || id,
    studentCode: readString(raw, 'studentCode', 'mssv') || 'N/A',
    studentName: readString(raw, 'studentName', 'fullName') || 'Sinh viên',
    status,
    aiScore,
    finalScore,
    overridden: readBool(raw, 'overridden', 'isOverridden'),
    overrideReason: readString(raw, 'overrideReason') || null,
    transcript: readString(raw, 'transcript') || answers[0]?.transcript || null,
    answers,
    joinedAt: readString(raw, 'joinedAt') || new Date().toISOString(),
    submittedAt: readString(raw, 'submittedAt') || null,
  }
}

function loadMockShifts(): InClassShift[] {
  try {
    const raw = localStorage.getItem(MOCK_KEY)
    if (!raw) return seedMockShifts()
    const parsed = JSON.parse(raw) as unknown
    if (!Array.isArray(parsed)) return seedMockShifts()
    return parsed
      .map((item) => normalizeShift(item))
      .filter((item): item is InClassShift => Boolean(item))
  } catch {
    return seedMockShifts()
  }
}

function saveMockShifts(shifts: InClassShift[]) {
  localStorage.setItem(MOCK_KEY, JSON.stringify(shifts))
}

function loadMockSubs(): Record<string, InClassSubmission[]> {
  try {
    const raw = localStorage.getItem(MOCK_SUB_KEY)
    if (!raw) return seedMockSubs()
    const parsed = JSON.parse(raw) as unknown
    if (!isRecord(parsed)) return seedMockSubs()

    const next: Record<string, InClassSubmission[]> = {}
    let hasAny = false
    for (const [shiftId, list] of Object.entries(parsed)) {
      if (!Array.isArray(list)) continue
      const normalized = list
        .map((item) => normalizeSubmission(item, shiftId))
        .filter((item): item is InClassSubmission => Boolean(item))
        .map((item) => ({
          ...item,
          answers: item.answers ?? [],
        }))
      next[shiftId] = normalized
      if (normalized.length > 0) hasAny = true
    }
    if (!hasAny) return seedMockSubs()
    saveMockSubs(next)
    return next
  } catch {
    return seedMockSubs()
  }
}

function saveMockSubs(map: Record<string, InClassSubmission[]>) {
  localStorage.setItem(MOCK_SUB_KEY, JSON.stringify(map))
}

function seedMockShifts(): InClassShift[] {
  const now = Date.now()
  const shifts: InClassShift[] = [
    {
      id: 'shift-demo-active',
      classId: 'class-se1830',
      className: 'SE1830',
      courseCode: 'PRN232',
      title: 'Progress Test 1 — Clean Architecture & SOLID',
      status: 'ACTIVE',
      durationMinutes: 20,
      questionCount: 3,
      hasFollowUp: true,
      passwordRotationSeconds: 300,
      gracePeriodSeconds: 60,
      joinedCount: 18,
      submittedCount: 11,
      createdAt: new Date(now - 35 * 60_000).toISOString(),
    },
    {
      id: 'shift-demo-closed',
      classId: 'class-se1831',
      className: 'SE1831',
      courseCode: 'PRN232',
      title: 'Quiz — Dependency Injection',
      status: 'CLOSED',
      durationMinutes: 15,
      questionCount: 2,
      hasFollowUp: false,
      passwordRotationSeconds: 300,
      gracePeriodSeconds: 60,
      joinedCount: 24,
      submittedCount: 24,
      createdAt: new Date(now - 2 * 24 * 3600_000).toISOString(),
    },
    {
      id: 'shift-demo-final',
      classId: 'class-se1840',
      className: 'SE1840',
      courseCode: 'SWD392',
      title: 'Progress Test — Requirement Analysis',
      status: 'FINALIZED',
      durationMinutes: 25,
      questionCount: 3,
      hasFollowUp: true,
      passwordRotationSeconds: 300,
      gracePeriodSeconds: 60,
      joinedCount: 29,
      submittedCount: 29,
      createdAt: new Date(now - 7 * 24 * 3600_000).toISOString(),
      finalizedAt: new Date(now - 6 * 24 * 3600_000).toISOString(),
    },
  ]
  saveMockShifts(shifts)
  return shifts
}

function demoAnswers(
  variants: Array<{
    title: string
    content?: string
    transcript?: string | null
    aiFeedback?: string | null
    aiScore?: number | null
    lecturerScore?: number | null
    lecturerComment?: string | null
    status?: InClassQuestionAnswer['status']
    isFollowUp?: boolean
  }>,
): InClassQuestionAnswer[] {
  return variants.map((item, index) => ({
    answerId: `ans-${index + 1}`,
    questionId: `q-${index + 1}`,
    orderIndex: index + 1,
    title: item.title,
    content: item.content ?? `Trình bày ngắn gọn và có ví dụ về: ${item.title}.`,
    isFollowUp: item.isFollowUp,
    status:
      item.status ??
      (item.transcript ? (item.aiScore != null ? 'GRADED' : 'ANSWERED') : 'PENDING'),
    transcript: item.transcript ?? null,
    aiFeedback:
      item.aiFeedback ??
      (item.aiScore != null
        ? `AI nhận xét câu ${index + 1}: mức độ đạt khoảng ${item.aiScore.toFixed(1)}/10. Cần bổ sung ví dụ cụ thể hơn nếu điểm < 8.`
        : null),
    aiScore: item.aiScore ?? null,
    lecturerScore: item.lecturerScore ?? null,
    lecturerComment: item.lecturerComment ?? null,
  }))
}

function recomputeSubmissionScores(submission: InClassSubmission): InClassSubmission {
  const scored = submission.answers.filter(
    (item) => (item.lecturerScore ?? item.aiScore) != null && item.status !== 'PENDING',
  )
  if (scored.length === 0) {
    return { ...submission, aiScore: null, finalScore: null }
  }
  const aiAvg =
    scored.reduce((sum, item) => sum + (item.aiScore ?? 0), 0) / scored.length
  const finalAvg =
    scored.reduce((sum, item) => sum + (item.lecturerScore ?? item.aiScore ?? 0), 0) /
    scored.length
  const overridden = submission.answers.some((item) => item.lecturerScore != null)
  return {
    ...submission,
    aiScore: Number(aiAvg.toFixed(1)),
    finalScore: Number(finalAvg.toFixed(1)),
    overridden,
    overrideReason: overridden
      ? submission.answers.find((item) => item.lecturerComment)?.lecturerComment ??
        'Đã chỉnh điểm theo từng câu.'
      : submission.overrideReason,
  }
}

function seedMockSubs(): Record<string, InClassSubmission[]> {
  const map: Record<string, InClassSubmission[]> = {
    'shift-demo-active': [
      {
        id: 'sub-a1',
        shiftId: 'shift-demo-active',
        studentId: 'stu-1',
        studentCode: 'SE183001',
        studentName: 'Nguyễn Văn An',
        status: 'GRADED',
        aiScore: 7.5,
        finalScore: 7.5,
        overridden: false,
        answers: demoAnswers([
          {
            title: 'DIP trong Clean Architecture',
            transcript:
              'DIP yêu cầu module cấp cao không phụ thuộc module cấp thấp, cả hai phụ thuộc abstraction. Trong .NET ta dùng interface và DI container.',
            aiScore: 8.0,
          },
          {
            title: 'Vai trò IoC Container',
            transcript:
              'IoC Container đăng ký service lifetime và inject dependency lúc runtime, giảm coupling giữa các lớp.',
            aiScore: 7.5,
          },
          {
            title: 'Follow-up: ví dụ .NET',
            transcript:
              'Em đăng ký IOrderService bằng AddScoped trong Program.cs rồi inject vào controller.',
            aiScore: 7.0,
            isFollowUp: true,
          },
        ]),
        joinedAt: new Date(Date.now() - 20 * 60_000).toISOString(),
        submittedAt: new Date(Date.now() - 8 * 60_000).toISOString(),
      },
      {
        id: 'sub-a2',
        shiftId: 'shift-demo-active',
        studentId: 'stu-2',
        studentCode: 'SE183014',
        studentName: 'Trần Thị Bình',
        status: 'SUBMITTED',
        aiScore: 6.2,
        finalScore: 6.2,
        overridden: false,
        answers: demoAnswers([
          {
            title: 'DIP trong Clean Architecture',
            transcript:
              'SOLID gồm 5 nguyên lý. Em nêu SRP và OCP, còn DIP thì module nên phụ thuộc vào interface.',
            aiScore: 6.5,
          },
          {
            title: 'Vai trò IoC Container',
            transcript: 'Container giúp tạo object và inject sẵn, không new thủ công.',
            aiScore: 6.0,
          },
          {
            title: 'Follow-up: ví dụ .NET',
            transcript: null,
            status: 'PENDING',
            isFollowUp: true,
          },
        ]),
        joinedAt: new Date(Date.now() - 18 * 60_000).toISOString(),
        submittedAt: new Date(Date.now() - 5 * 60_000).toISOString(),
      },
      {
        id: 'sub-a3',
        shiftId: 'shift-demo-active',
        studentId: 'stu-3',
        studentCode: 'SE183022',
        studentName: 'Lê Minh Cường',
        status: 'IN_PROGRESS',
        aiScore: null,
        finalScore: null,
        overridden: false,
        answers: demoAnswers([
          {
            title: 'DIP trong Clean Architecture',
            transcript: 'Em đang trình bày phần abstraction và interface...',
            status: 'ANSWERED',
          },
          { title: 'Vai trò IoC Container', status: 'PENDING' },
          { title: 'Follow-up: ví dụ .NET', status: 'PENDING', isFollowUp: true },
        ]),
        joinedAt: new Date(Date.now() - 4 * 60_000).toISOString(),
      },
      {
        id: 'sub-a4',
        shiftId: 'shift-demo-active',
        studentId: 'stu-4',
        studentCode: 'SE183030',
        studentName: 'Phạm Thu Dung',
        status: 'GRADED',
        aiScore: 8.0,
        finalScore: 8.5,
        overridden: true,
        overrideReason: 'Bổ sung đúng ví dụ IoC trên lớp (+0.5).',
        answers: demoAnswers([
          {
            title: 'DIP trong Clean Architecture',
            transcript:
              'Clean Architecture tách domain khỏi infrastructure. Em lấy ví dụ UseCase gọi repository qua interface.',
            aiScore: 8.5,
            lecturerScore: 9.0,
            lecturerComment: 'Có nêu đúng ví dụ trên lớp về IoC (+0.5).',
          },
          {
            title: 'Vai trò IoC Container',
            transcript: 'Lifetime lifetimes Scoped/Transient/Singleton; Scoped phù hợp request web.',
            aiScore: 8.0,
          },
          {
            title: 'Follow-up: ví dụ .NET',
            transcript: 'builder.Services.AddScoped<IRepo, Repo>(); rồi inject vào handler.',
            aiScore: 7.5,
            isFollowUp: true,
          },
        ]),
        joinedAt: new Date(Date.now() - 22 * 60_000).toISOString(),
        submittedAt: new Date(Date.now() - 10 * 60_000).toISOString(),
      },
    ],
    'shift-demo-closed': [
      {
        id: 'sub-c1',
        shiftId: 'shift-demo-closed',
        studentId: 'stu-5',
        studentCode: 'SE183101',
        studentName: 'Hoàng Đức Em',
        status: 'GRADED',
        aiScore: 7.0,
        finalScore: 7.0,
        overridden: false,
        answers: demoAnswers([
          {
            title: 'Dependency Injection',
            transcript: 'DI giúp giảm coupling, dễ unit test bằng mock.',
            aiScore: 7.2,
          },
          {
            title: 'Constructor injection',
            transcript: 'Truyền dependency qua constructor để bắt buộc cung cấp lúc tạo object.',
            aiScore: 6.8,
          },
        ]),
        joinedAt: new Date(Date.now() - 2 * 24 * 3600_000).toISOString(),
        submittedAt: new Date(Date.now() - 2 * 24 * 3600_000 + 600_000).toISOString(),
      },
    ],
    'shift-demo-final': [
      {
        id: 'sub-f1',
        shiftId: 'shift-demo-final',
        studentId: 'stu-6',
        studentCode: 'SE184001',
        studentName: 'Vũ Hải Phong',
        status: 'FINALIZED',
        aiScore: 8.2,
        finalScore: 8.2,
        overridden: false,
        answers: demoAnswers([
          {
            title: 'User story & INVEST',
            transcript: 'User story cần INVEST; acceptance criteria đo lường được.',
            aiScore: 8.5,
          },
          {
            title: 'MoSCoW prioritization',
            transcript: 'Must/Should/Could/Won’t giúp sắp backlog theo giá trị nghiệp vụ.',
            aiScore: 8.0,
          },
          {
            title: 'Follow-up: ví dụ sprint',
            transcript: 'Must: đăng nhập SSO; Should: lọc lịch sử; Could: theme tối.',
            aiScore: 8.0,
            isFollowUp: true,
          },
        ]),
        joinedAt: new Date(Date.now() - 7 * 24 * 3600_000).toISOString(),
        submittedAt: new Date(Date.now() - 7 * 24 * 3600_000 + 900_000).toISOString(),
      },
    ],
  }
  saveMockSubs(map)
  return map
}

function otpAlphabet() {
  return 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'
}

function generateOtpCode() {
  const alphabet = otpAlphabet()
  let code = ''
  for (let i = 0; i < 6; i += 1) {
    code += alphabet[Math.floor(Math.random() * alphabet.length)]
  }
  return code
}

/** Windowed OTP for mock projector so refresh stays stable within a rotation slot. */
function mockActivePassword(shift: InClassShift): ActiveTotpPassword {
  const rotationMs = (shift.passwordRotationSeconds || 300) * 1000
  const now = Date.now()
  const slot = Math.floor(now / rotationMs)
  const slotStart = slot * rotationMs
  const expiresAt = slotStart + rotationMs
  const remainingSeconds = Math.max(1, Math.ceil((expiresAt - now) / 1000))
  // Deterministic-ish code per shift+slot for demo stability
  const seed = `${shift.id}:${slot}`
  let hash = 0
  for (let i = 0; i < seed.length; i += 1) hash = (hash * 31 + seed.charCodeAt(i)) >>> 0
  const alphabet = otpAlphabet()
  let code = ''
  let cursor = hash
  for (let i = 0; i < 6; i += 1) {
    code += alphabet[cursor % alphabet.length]
    cursor = Math.floor(cursor / alphabet.length) + (i + 1) * 17
  }
  return {
    shiftId: shift.id,
    currentOtpCode: code || generateOtpCode(),
    generatedAt: new Date(slotStart).toISOString(),
    expiresAt: new Date(expiresAt).toISOString(),
    gracePeriodSeconds: shift.gracePeriodSeconds || 60,
    remainingSeconds,
    previousOtpCode: null,
  }
}

function getErrorDetail(error: unknown) {
  if (isRecord(error) && typeof error.detail === 'string') return error.detail
  if (error instanceof Error && error.message) return error.message
  return 'Không thể hoàn tất yêu cầu.'
}

export const inClassShiftService = {
  async getLecturerClasses(): Promise<LecturerClassOption[]> {
    try {
      const response = await apiClient.get<unknown>('/lecturer/classes')
      const list: LecturerClassOption[] = []
      for (const item of unwrapList(response.data)) {
        if (!isRecord(item)) continue
        const id = readString(item, 'id', 'classId')
        if (!id) continue
        const studentCount = readNumber(item, 'studentCount', 'enrolledCount')
        list.push({
          id,
          name: readString(item, 'name', 'className', 'code') || 'Lớp',
          courseCode: readString(item, 'courseCode') || '',
          courseName: readString(item, 'courseName') || '',
          ...(studentCount > 0 ? { studentCount } : {}),
        })
      }
      if (list.length > 0) return list
    } catch {
      /* fallback mock */
    }
    return MOCK_CLASSES
  },

  async listShifts(params?: { classId?: string }): Promise<InClassShift[]> {
    try {
      const response = await apiClient.get<unknown>('/lecturer/in-class-shifts', {
        params: params?.classId ? { classId: params.classId } : undefined,
      })
      const list = unwrapList(response.data)
        .map((item) => normalizeShift(item))
        .filter((item): item is InClassShift => Boolean(item))
      if (list.length > 0 || response.status === 200) {
        return params?.classId ? list.filter((s) => s.classId === params.classId) : list
      }
    } catch {
      /* fallback mock */
    }
    const shifts = loadMockShifts()
    return params?.classId ? shifts.filter((s) => s.classId === params.classId) : shifts
  },

  async getShift(shiftId: string): Promise<InClassShift> {
    try {
      const response = await apiClient.get<unknown>(`/lecturer/in-class-shifts/${shiftId}`)
      const shift = normalizeShift(response.data)
      if (shift) return shift
    } catch {
      /* fallback */
    }
    const found = loadMockShifts().find((item) => item.id === shiftId)
    if (!found) throw Object.assign(new Error('Không tìm thấy ca thi.'), { detail: 'Không tìm thấy ca thi.' })
    return found
  },

  async createShift(payload: CreateInClassShiftRequest): Promise<InClassShift> {
    try {
      const response = await apiClient.post<unknown>('/lecturer/in-class-shifts', payload)
      const classMeta = MOCK_CLASSES.find((item) => item.id === payload.classId)
      const shift = normalizeShift(response.data, classMeta)
      if (shift) return shift
    } catch (error) {
      const status =
        isRecord(error) && isRecord(error.response) ? Number(error.response.status) : 0
      if (status && status !== 404 && status < 500) throw error
    }

    const classMeta = MOCK_CLASSES.find((item) => item.id === payload.classId)
    const shift: InClassShift = {
      id: `shift-${crypto.randomUUID?.() ?? Date.now()}`,
      classId: payload.classId,
      className: classMeta?.name ?? 'Lớp học phần',
      courseCode: classMeta?.courseCode ?? '',
      title: payload.title.trim(),
      status: 'ACTIVE',
      durationMinutes: payload.durationMinutes,
      questionCount: payload.questionCount,
      hasFollowUp: payload.hasFollowUp,
      passwordRotationSeconds: payload.passwordRotationSeconds,
      gracePeriodSeconds: payload.gracePeriodSeconds,
      joinedCount: 0,
      submittedCount: 0,
      createdAt: new Date().toISOString(),
    }
    const next = [shift, ...loadMockShifts()]
    saveMockShifts(next)
    const subs = loadMockSubs()
    subs[shift.id] = []
    saveMockSubs(subs)
    return shift
  },

  async getActivePassword(shiftId: string): Promise<ActiveTotpPassword> {
    try {
      const response = await apiClient.get<unknown>(
        `/lecturer/in-class-shifts/${shiftId}/active-password`,
      )
      if (isRecord(response.data)) {
        const code = readString(response.data, 'currentOtpCode', 'otpCode')
        if (code) {
          return {
            shiftId: readString(response.data, 'shiftId') || shiftId,
            currentOtpCode: code.toUpperCase(),
            generatedAt: readString(response.data, 'generatedAt') || new Date().toISOString(),
            expiresAt: readString(response.data, 'expiresAt') || new Date(Date.now() + 300_000).toISOString(),
            gracePeriodSeconds: readNumber(response.data, 'gracePeriodSeconds') || 60,
            remainingSeconds: readNumber(response.data, 'remainingSeconds') || 300,
            previousOtpCode: readString(response.data, 'previousOtpCode') || null,
          }
        }
      }
    } catch {
      /* fallback */
    }
    const shift = await this.getShift(shiftId)
    return mockActivePassword(shift)
  },

  async listSubmissions(shiftId: string): Promise<InClassSubmission[]> {
    try {
      const response = await apiClient.get<unknown>(
        `/lecturer/in-class-shifts/${shiftId}/submissions`,
      )
      const list = unwrapList(response.data)
        .map((item) => normalizeSubmission(item, shiftId))
        .filter((item): item is InClassSubmission => Boolean(item))
      // BE chưa có dữ liệu / endpoint rỗng → dùng mock có answers[] để UI tiến độ hoạt động
      if (list.length > 0) return list
    } catch {
      /* fallback */
    }
    return loadMockSubs()[shiftId] ?? []
  },

  async gradeSubmission(
    shiftId: string,
    submissionId: string,
    payload: GradeInClassSubmissionRequest,
  ): Promise<InClassSubmission> {
    try {
      const response = await apiClient.put<unknown>(
        `/lecturer/in-class-shifts/${shiftId}/submissions/${submissionId}/grade`,
        payload,
      )
      const updated = normalizeSubmission(response.data, shiftId)
      if (updated) return updated
    } catch (error) {
      const status =
        isRecord(error) && isRecord(error.response) ? Number(error.response.status) : 0
      if (status && status !== 404 && status < 500) throw error
    }

    const map = loadMockSubs()
    const list = map[shiftId] ?? []
    const index = list.findIndex((item) => item.id === submissionId)
    if (index < 0) throw Object.assign(new Error(getErrorDetail({})), { detail: 'Không tìm thấy bài nộp.' })
    const current = list[index]
    const next: InClassSubmission = {
      ...current,
      answers: Array.isArray(current.answers) ? current.answers : [],
      finalScore: payload.adjustedScore,
      overridden: true,
      overrideReason: payload.overrideReason,
      status: 'GRADED',
    }
    list[index] = next
    map[shiftId] = list
    saveMockSubs(map)
    return next
  },

  async gradeAnswer(
    shiftId: string,
    submissionId: string,
    answerId: string,
    payload: GradeInClassAnswerRequest,
  ): Promise<InClassSubmission> {
    try {
      const response = await apiClient.put<unknown>(
        `/lecturer/in-class-shifts/${shiftId}/submissions/${submissionId}/answers/${answerId}/grade`,
        payload,
      )
      const updated = normalizeSubmission(response.data, shiftId)
      if (updated) return updated
    } catch (error) {
      const status =
        isRecord(error) && isRecord(error.response) ? Number(error.response.status) : 0
      if (status && status !== 404 && status < 500) throw error
    }

    const map = loadMockSubs()
    const list = map[shiftId] ?? []
    const index = list.findIndex((item) => item.id === submissionId)
    if (index < 0) {
      throw Object.assign(new Error('Không tìm thấy bài nộp.'), {
        detail: 'Không tìm thấy bài nộp.',
      })
    }
    const current = list[index]
    if (['JOINED', 'IN_PROGRESS'].includes(current.status)) {
      throw Object.assign(new Error('Sinh viên đang làm bài — chưa thể chấm điểm.'), {
        detail: 'Sinh viên đang làm bài — chưa thể xem/chấm bảng điểm.',
      })
    }
    const answers = (current.answers ?? []).map((answer) =>
      answer.answerId === answerId
        ? {
            ...answer,
            lecturerScore: payload.lecturerScore,
            lecturerComment: payload.lecturerComment,
            status: 'GRADED' as const,
          }
        : answer,
    )
    const next = recomputeSubmissionScores({
      ...current,
      answers,
      status: 'GRADED',
    })
    list[index] = next
    map[shiftId] = list
    saveMockSubs(map)
    return next
  },

  async finalizeGrades(shiftId: string): Promise<FinalizeInClassGradesResponse> {
    try {
      const response = await apiClient.post<unknown>(
        `/lecturer/in-class-shifts/${shiftId}/finalize-grades`,
      )
      if (isRecord(response.data)) {
        return {
          shiftId: readString(response.data, 'shiftId') || shiftId,
          status: 'FINALIZED',
          isLocked: true,
          finalizedAt: readString(response.data, 'finalizedAt') || new Date().toISOString(),
          message:
            readString(response.data, 'message') ||
            'Đã chốt bảng điểm ca thi trong lớp và kích hoạt One-Way Lock.',
        }
      }
    } catch (error) {
      const status =
        isRecord(error) && isRecord(error.response) ? Number(error.response.status) : 0
      if (status && status !== 404 && status < 500) throw error
    }

    const shifts = loadMockShifts()
    const index = shifts.findIndex((item) => item.id === shiftId)
    if (index < 0) throw Object.assign(new Error('Không tìm thấy ca.'), { detail: 'Không tìm thấy ca.' })
    const finalizedAt = new Date().toISOString()
    shifts[index] = { ...shifts[index], status: 'FINALIZED', finalizedAt }
    saveMockShifts(shifts)
    const map = loadMockSubs()
    map[shiftId] = (map[shiftId] ?? []).map((item) => ({
      ...item,
      status: 'FINALIZED' as const,
      finalScore: item.finalScore ?? item.aiScore,
    }))
    saveMockSubs(map)
    return {
      shiftId,
      status: 'FINALIZED',
      isLocked: true,
      finalizedAt,
      message: 'Đã chốt bảng điểm ca thi trong lớp và kích hoạt One-Way Lock thành công.',
    }
  },
}
