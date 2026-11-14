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
    ['DepartmentHead', '/department-head/appeals'],
    ['ExaminationOfficer', '/examination-officer/exams/123'],
    ['Admin', '/admin/dashboard'],
    ['ExaminationOfficer', '/official-exams/manage'],
    ['DepartmentHead', '/question-bank'],
    ['Student', '/practice/session'],
  ]

  it.each(allowed)('allows %s to access %s', (role, pathname) => {
    expect(isRouteAllowedForRole(role, pathname)).toBe(true)
  })

  const forbidden: Array<[UserRole, string]> = [
    ['Instructor', '/admin/users'],
    ['Instructor', '/examination-officer/exams'],
    ['DepartmentHead', '/lecturer/classes'],
    ['DepartmentHead', '/examination-officer/dlq'],
    ['ExaminationOfficer', '/lecturer/audit'],
    ['ExaminationOfficer', '/admin/dashboard'],
    ['Admin', '/lecturer/classes'],
    ['Admin', '/examination-officer/exams'],
    ['DepartmentHead', '/question-bank/create'],
    ['Admin', '/appeals'],
    ['Instructor', '/dlq'],
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
