import type { UserRole } from '@/types/auth.types'
import { canonicalRole } from '@/routes/routeAccess'

// UI capabilities mirror Task 09. The API still authorizes the authenticated
// actor and checks course/shift/appeal ownership on every request.
export const officialManagementRoles: UserRole[] = ['ExaminationOfficer', 'DepartmentHead']
export const appealManagementRoles: UserRole[] = ['ExaminationOfficer', 'Instructor']
export const attendanceRoles: UserRole[] = ['Proctor', 'ExaminationOfficer']
export const dlqRoles: UserRole[] = ['Admin', 'ExaminationOfficer']

export function dlqFlowsFor(role: UserRole | undefined): string[] {
  const normalized = canonicalRole(role)
  if (normalized === 'Admin') return ['ALL', 'PRACTICE', 'MOCK_EXAM', 'IN_CLASS_EXAM']
  if (normalized === 'ExaminationOfficer') return ['OFFICIAL_EXAM']
  return []
}
