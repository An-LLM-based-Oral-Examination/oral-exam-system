export type CanonicalRole =
  | 'Student'
  | 'Instructor'
  | 'DepartmentHead'
  | 'ExaminationOfficer'
  | 'Proctor'
  | 'Admin'

export interface LiveCredentials {
  username: string
  password: string
}

const ENV_KEY_BY_ROLE: Record<CanonicalRole, readonly string[]> = {
  Student: ['STUDENT'],
  Instructor: ['INSTRUCTOR', 'LECTURER'],
  DepartmentHead: ['DEPARTMENT_HEAD', 'DEPARTMENTHEAD'],
  ExaminationOfficer: ['EXAMINATION_OFFICER', 'EXAMINATIONOFFICER'],
  Proctor: ['PROCTOR'],
  Admin: ['ADMIN'],
}

const DEFAULT_MOCK_CREDENTIALS: Record<CanonicalRole, LiveCredentials> = {
  Student: { username: 'student.e2e@fe.edu.vn', password: 'E2ePassword!123' },
  Instructor: { username: 'lecturer.e2e@fe.edu.vn', password: 'E2ePassword!123' },
  DepartmentHead: { username: 'depthead.e2e@fe.edu.vn', password: 'E2ePassword!123' },
  ExaminationOfficer: { username: 'officer.e2e@fe.edu.vn', password: 'E2ePassword!123' },
  Proctor: { username: 'proctor.e2e@fe.edu.vn', password: 'E2ePassword!123' },
  Admin: { username: 'admin.e2e@fe.edu.vn', password: 'E2ePassword!123' },
}

export function isLiveApiMode(): boolean {
  return process.env.E2E_LIVE_API === '1'
}

export function getLiveBaseUrl(): string {
  return process.env.E2E_BASE_URL || `http://127.0.0.1:${process.env.E2E_PORT || '4173'}`
}

export function getLiveCredentials(role: CanonicalRole): LiveCredentials {
  const prefixes = ENV_KEY_BY_ROLE[role]
  for (const prefix of prefixes) {
    const username = process.env[`E2E_${prefix}_USER`]?.trim()
    const password = process.env[`E2E_${prefix}_PASS`]?.trim()
    if (username && password) {
      return { username, password }
    }
  }

  if (isLiveApiMode()) {
    const primary = prefixes[0]
    throw new Error(
      `Missing live API credentials for role ${role}. Set E2E_${primary}_USER and E2E_${primary}_PASS.`,
    )
  }

  return DEFAULT_MOCK_CREDENTIALS[role]
}
