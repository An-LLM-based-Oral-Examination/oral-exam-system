export interface VerifiedStudentScoreSummary {
  seatNumber: number
  studentCode: string
  fullName: string
  finalScore: number | null
  isLocked: boolean
}

export interface GradeCertificateVerification {
  status: string
  university: string
  digitalSignatureIssuer: string
  certificateHash: string
  certificateThumbprint: string | null
  certificateSignedAt: string | null
  isTampered: boolean
  shiftId: string
  shiftName: string
  roomLab: string
  sessionTitle: string
  courseCode: string
  courseName: string
  semesterCode: string
  examDate: string
  shiftStartTime: string
  shiftEndTime: string
  proctorName: string
  totalStudents: number
  lockedTicketsCount: number
  isFullyLocked: boolean
  verifiedAtUtc: string
  studentScores: VerifiedStudentScoreSummary[]
}

export class GradeVerificationError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'GradeVerificationError'
    this.status = status
  }
}

const SHA256_HEX_REGEX = /^[0-9a-fA-F]{64}$/

export function isValidCertificateHash(value: string): boolean {
  return SHA256_HEX_REGEX.test(value.trim())
}

function resolveBaseApiUrl(): string {
  const metaEnv = import.meta.env as Record<string, string | undefined>
  const configured = (metaEnv.VITE_API_BASE_URL ?? metaEnv.VITE_API_URL ?? '')
    .trim()
    .replace(/\/+$/, '')
  return configured || '/api/v1'
}

export async function verifyGradeCertificate(
  certificateHash: string,
  signal?: AbortSignal,
): Promise<GradeCertificateVerification> {
  const normalized = certificateHash.trim().toLowerCase()
  if (!isValidCertificateHash(normalized)) {
    throw new GradeVerificationError(
      'Mã chứng thư số SHA-256 phải gồm đúng 64 ký tự thập lục phân (0–9, a–f).',
      400,
    )
  }

  const response = await fetch(
    `${resolveBaseApiUrl()}/verification/grades/${encodeURIComponent(normalized)}`,
    {
      method: 'GET',
      headers: { Accept: 'application/json' },
      signal,
    },
  )

  if (response.status === 429) {
    throw new GradeVerificationError(
      'Bạn đang tra cứu quá nhanh. Vui lòng đợi ít phút rồi thử lại.',
      429,
    )
  }

  const contentType = response.headers.get('content-type') ?? ''
  const payload =
    contentType.includes('application/json') || contentType.includes('+json')
      ? ((await response.json()) as Record<string, unknown>)
      : null

  if (!response.ok) {
    const detail =
      (typeof payload?.detail === 'string' && payload.detail.trim()) ||
      (typeof payload?.title === 'string' && payload.title.trim()) ||
      'Chứng thư bảng điểm không hợp lệ hoặc không tồn tại trên hệ thống.'
    throw new GradeVerificationError(detail, response.status)
  }

  if (!payload) {
    throw new GradeVerificationError('Phản hồi xác thực từ máy chủ không hợp lệ.', 500)
  }

  const rawScores = Array.isArray(payload.studentScores) ? payload.studentScores : []
  const studentScores: VerifiedStudentScoreSummary[] = rawScores.map((item) => {
    const row = (item ?? {}) as Record<string, unknown>
    return {
      seatNumber: typeof row.seatNumber === 'number' ? row.seatNumber : 0,
      studentCode: typeof row.studentCode === 'string' ? row.studentCode : '',
      fullName: typeof row.fullName === 'string' ? row.fullName : '',
      finalScore: typeof row.finalScore === 'number' ? row.finalScore : null,
      isLocked: row.isLocked === true,
    }
  })

  return {
    status: typeof payload.status === 'string' ? payload.status : 'VALID',
    university: typeof payload.university === 'string' ? payload.university : '',
    digitalSignatureIssuer:
      typeof payload.digitalSignatureIssuer === 'string' ? payload.digitalSignatureIssuer : '',
    certificateHash: typeof payload.certificateHash === 'string' ? payload.certificateHash : normalized,
    certificateThumbprint:
      typeof payload.certificateThumbprint === 'string' ? payload.certificateThumbprint : null,
    certificateSignedAt:
      typeof payload.certificateSignedAt === 'string' ? payload.certificateSignedAt : null,
    isTampered: payload.isTampered === true,
    shiftId: typeof payload.shiftId === 'string' ? payload.shiftId : '',
    shiftName: typeof payload.shiftName === 'string' ? payload.shiftName : '',
    roomLab: typeof payload.roomLab === 'string' ? payload.roomLab : '',
    sessionTitle: typeof payload.sessionTitle === 'string' ? payload.sessionTitle : '',
    courseCode: typeof payload.courseCode === 'string' ? payload.courseCode : '',
    courseName: typeof payload.courseName === 'string' ? payload.courseName : '',
    semesterCode: typeof payload.semesterCode === 'string' ? payload.semesterCode : '',
    examDate: typeof payload.examDate === 'string' ? payload.examDate : '',
    shiftStartTime: typeof payload.shiftStartTime === 'string' ? payload.shiftStartTime : '',
    shiftEndTime: typeof payload.shiftEndTime === 'string' ? payload.shiftEndTime : '',
    proctorName: typeof payload.proctorName === 'string' ? payload.proctorName : '',
    totalStudents: typeof payload.totalStudents === 'number' ? payload.totalStudents : studentScores.length,
    lockedTicketsCount:
      typeof payload.lockedTicketsCount === 'number' ? payload.lockedTicketsCount : 0,
    isFullyLocked: payload.isFullyLocked === true,
    verifiedAtUtc: typeof payload.verifiedAtUtc === 'string' ? payload.verifiedAtUtc : '',
    studentScores,
  }
}
