import { describe, expect, it } from 'vitest'
import { getRoleHomePath, isRouteAllowedForRole } from '@/routes/routeAccess'
import type { UserRole } from '@/types/auth.types'

describe('role route matrix', () => {
  const homes: Array<[UserRole, string]> = [
    ['Student', '/student/overview'],
    ['Instructor', '/lecturer/dashboard'],
    ['DepartmentHead', '/department-head/dashboard'],
    ['ExaminationOfficer', '/examination-officer/dashboard'],
    ['Admin', '/admin/dashboard'],
    ['Proctor', '/proctor/attendance'],
  ]

  it.each(homes)('redirects %s to its own portal', (role, expectedPath) => {
    expect(getRoleHomePath(role)).toBe(expectedPath)
  })

  const allowed: Array<[UserRole, string]> = [
    ['Instructor', '/lecturer/classes'],
    ['DepartmentHead', '/department-head/question-review'],
    ['ExaminationOfficer', '/examination-officer/exams/123'],
    ['Admin', '/admin/dashboard'],
    ['Admin', '/admin/users'],
    ['Admin', '/admin/semesters'],
    ['Admin', '/admin/courses'],
    ['Admin', '/admin/configs'],
    ['Admin', '/admin/notifications'],
    ['Admin', '/admin/audit-logs'],
    ['Admin', '/admin/dlq'],
    ['admin', '/admin/users'],
    ['ExaminationOfficer', '/official-exams/manage'],
    ['DepartmentHead', '/question-bank'],
    ['Student', '/practice/session'],
    ['Admin', '/dlq'],
  ]

  it.each(allowed)('allows %s to access %s', (role, pathname) => {
    expect(isRouteAllowedForRole(role, pathname)).toBe(true)
  })

  const forbidden: Array<[UserRole, string]> = [
    ['Instructor', '/admin/users'],
    ['Instructor', '/examination-officer/exams'],
    ['Instructor', '/lecturer/dlq'],
    ['Instructor', '/dlq'],
    ['DepartmentHead', '/lecturer/classes'],
    ['DepartmentHead', '/examination-officer/dlq'],
    ['DepartmentHead', '/department-head/appeals'],
    ['DepartmentHead', '/appeals'],
    ['ExaminationOfficer', '/lecturer/audit'],
    ['ExaminationOfficer', '/admin/dashboard'],
    ['ExaminationOfficer', '/admin/dlq'],
    ['DepartmentHead', '/admin/users'],
    ['Student', '/admin/audit-logs'],
    ['Admin', '/lecturer/classes'],
    ['Admin', '/examination-officer/exams'],
    ['DepartmentHead', '/question-bank/create'],
    ['Admin', '/appeals'],
    ['Student', '/lecturer/dashboard'],
    ['Proctor', '/student/overview'],
    ['Proctor', '/lecturer/dashboard'],
    ['Proctor', '/department-head/dashboard'],
    ['Proctor', '/examination-officer/dashboard'],
    ['Proctor', '/admin/dashboard'],
    ['Student', '/department-head/dashboard'],
    ['Student', '/examination-officer/dashboard'],
    ['Student', '/proctor/attendance'],
    ['Student', '/admin/dashboard'],
  ]

  it.each(forbidden)('denies %s from %s', (role, pathname) => {
    expect(isRouteAllowedForRole(role, pathname)).toBe(false)
  })

  it('never grants an unknown role the Admin portal by default', () => {
    expect(isRouteAllowedForRole('UnknownRole' as UserRole, '/admin/dashboard')).toBe(false)
  })

  it.each([
    ['/admin-malicious', 'Admin'],
    ['/lecturer-fake', 'Instructor'],
    ['/studentx', 'Student'],
    ['/examination-officer-malicious', 'ExaminationOfficer'],
  ] as Array<[string, UserRole]>)('does not treat %s as a protected namespace prefix', (path, role) => {
    expect(isRouteAllowedForRole(role, path)).toBe(false)
  })
})
