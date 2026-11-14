import type { UserRole } from '@/types/auth.types'

export type CanonicalRole =
  'Student' | 'Instructor' | 'DepartmentHead' | 'ExaminationOfficer' | 'Proctor' | 'Admin'

export const PORTAL_HOME_BY_ROLE: Record<CanonicalRole, string> = {
  Student: '/student/overview',
  Instructor: '/lecturer/dashboard',
  DepartmentHead: '/department-head/dashboard',
  ExaminationOfficer: '/examination-officer/dashboard',
  Proctor: '/proctor/attendance',
  Admin: '/admin/dashboard',
}

const PORTAL_PREFIXES: Record<string, UserRole[]> = {
  '/lecturer': ['Instructor'],
  '/department-head': ['DepartmentHead'],
  '/examination-officer': ['ExaminationOfficer'],
  '/admin': ['Admin'],
  '/proctor': ['Proctor'],
  '/student': ['Student'],
  '/practice': ['Student'],
  '/mock-exam': ['Student'],
  '/exam': ['Student'],
  '/results': ['Student'],
  '/in-class-exam': ['Student'],
  '/classes': ['Student'],
}

const LEGACY_PREFIXES: Record<string, UserRole[]> = {
  '/question-bank/create': ['Instructor'],
  '/lecturer/classes': ['Instructor'],
  '/lecturer/audit': ['Instructor'],
  '/question-bank': ['Instructor', 'DepartmentHead'],
  '/in-class-shifts': ['Instructor'],
  '/official-exams/manage': ['ExaminationOfficer'],
  '/appeals': ['Instructor', 'DepartmentHead', 'ExaminationOfficer'],
  '/dlq': ['ExaminationOfficer', 'Admin'],
}

function matchesPrefix(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(`${prefix}/`)
}

export function canonicalRole(role: UserRole | unknown): CanonicalRole | null {
  if (role === 'Student' || role === 'student') return 'Student'
  if (role === 'Instructor' || role === 'Lecturer' || role === 'lecturer') return 'Instructor'
  if (role === 'DepartmentHead' || role === 'department_head') return 'DepartmentHead'
  if (role === 'ExaminationOfficer' || role === 'examination_officer') return 'ExaminationOfficer'
  if (role === 'Proctor' || role === 'proctor') return 'Proctor'
  if (role === 'Admin' || role === 'admin') return 'Admin'
  return null
}

export function getRoleHomePath(role: UserRole): string {
  const normalizedRole = canonicalRole(role)
  return normalizedRole ? PORTAL_HOME_BY_ROLE[normalizedRole] : '/unauthorized'
}

export function isRouteAllowedForRole(role: UserRole, pathname: string): boolean {
  const normalizedRole = canonicalRole(role)
  if (!normalizedRole) return false
  const portalRule = Object.entries(PORTAL_PREFIXES).find(([prefix]) =>
    matchesPrefix(pathname, prefix),
  )
  if (portalRule) return portalRule[1].includes(normalizedRole)

  const legacyRule = Object.entries(LEGACY_PREFIXES).find(([prefix]) =>
    matchesPrefix(pathname, prefix),
  )
  if (legacyRule) return legacyRule[1].includes(normalizedRole)

  return pathname === '/' || pathname === '/change-password' || pathname === '/unauthorized'
}
