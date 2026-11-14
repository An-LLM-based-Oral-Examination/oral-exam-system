import type { UserRole } from '@/types/auth.types'

// UI capabilities mirror Task 09. The API still authorizes the authenticated
// actor and checks course/shift/appeal ownership on every request.
export const officialManagementRoles: UserRole[] = ['ExaminationOfficer', 'DepartmentHead']
export const appealManagementRoles: UserRole[] = ['ExaminationOfficer', 'Instructor', 'DepartmentHead']
export const attendanceRoles: UserRole[] = ['Proctor', 'ExaminationOfficer']
export const dlqRoles: UserRole[] = ['Admin', 'ExaminationOfficer', 'Instructor']

export function dlqFlowsFor(role: UserRole | undefined): string[] {
  if (role === 'Admin') return ['ALL', 'PRACTICE', 'MOCK_EXAM', 'IN_CLASS_EXAM']
  if (role === 'ExaminationOfficer') return ['OFFICIAL_EXAM']
  if (role === 'Instructor') return ['IN_CLASS_EXAM']
  return []
}
