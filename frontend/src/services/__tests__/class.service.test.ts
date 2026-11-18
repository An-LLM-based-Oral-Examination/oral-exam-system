import { beforeEach, describe, expect, it, vi } from 'vitest'
import apiClient from '../api.client'
import { classService } from '../class.service'

describe('classService credentials & reset password', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('createClass posts to course-offerings endpoint and returns normalized CreateClassResponse', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        id: 'class-1',
        code: 'SE1802-CSD',
        name: 'Lớp SE1802',
        joinCode: 'JOIN8888',
        courseId: 'course-1',
        courseCode: 'CSD201',
        courseName: 'Data Structures',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        lecturerId: 'lec-1',
        lecturerName: 'Giảng viên A',
        maxStudents: 35,
        hasPassword: true,
        isJoinEnabled: true,
        allowSelfEnrollment: true,
        createdAt: '2026-11-18T00:00:00Z',
        courseOfferingId: 'offering-1',
        joinPassword: 'SecretPass#1',
      },
    } as never)

    const result = await classService.createClass({
      courseOfferingId: 'offering-1',
      code: ' SE1802-CSD ',
      name: ' Lớp SE1802 ',
      password: 'SecretPass#1',
      maxStudents: 35,
    })

    expect(post).toHaveBeenCalledWith('/lecturer/course-offerings/offering-1/classes', {
      code: 'SE1802-CSD',
      name: 'Lớp SE1802',
      password: 'SecretPass#1',
      maxStudents: 35,
      allowSelfEnrollment: true,
      isJoinEnabled: true,
    })
    expect(result).toEqual({
      id: 'class-1',
      code: 'SE1802-CSD',
      name: 'Lớp SE1802',
      joinCode: 'JOIN8888',
      courseId: 'course-1',
      courseCode: 'CSD201',
      courseName: 'Data Structures',
      semesterId: 'sem-1',
      semesterCode: 'FA26',
      lecturerId: 'lec-1',
      lecturerName: 'Giảng viên A',
      maxStudents: 35,
      hasPassword: true,
      isJoinEnabled: true,
      allowSelfEnrollment: true,
      createdAt: '2026-11-18T00:00:00Z',
      courseOfferingId: 'offering-1',
      joinPassword: 'SecretPass#1',
    })
  })

  it('createClass handles null joinPassword safely', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        id: 'class-2',
        code: 'SE1803',
        name: 'Lớp SE1803',
        joinCode: 'ABCD1234',
        courseId: 'course-1',
        courseCode: 'CSD201',
        courseName: 'Data Structures',
        semesterId: 'sem-1',
        semesterCode: 'FA26',
        lecturerId: null,
        lecturerName: null,
        maxStudents: 30,
        hasPassword: false,
        isJoinEnabled: true,
        allowSelfEnrollment: false,
        createdAt: '2026-11-18T00:00:00Z',
        courseOfferingId: null,
        joinPassword: null,
      },
    } as never)

    const result = await classService.createClass({
      courseOfferingId: 'offering-1',
      code: 'SE1803',
      name: 'Lớp SE1803',
      password: 'Password123',
      maxStudents: 30,
    })

    expect(result.joinPassword).toBeNull()
    expect(result.lecturerId).toBeNull()
    expect(result.courseOfferingId).toBeNull()
  })

  it('resetPassword omits blank newJoinCode and returns normalized ResetClassPasswordResponse', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        classId: 'class-1',
        code: 'SE1802-CSD',
        name: 'Lớp SE1802',
        joinCode: 'JOIN9999',
        hasPassword: true,
        isJoinEnabled: false,
        updatedAt: '2026-11-18T05:00:00Z',
        joinPassword: 'NewPassword#9',
      },
    } as never)

    const result = await classService.resetPassword('class-1', {
      newPassword: ' NewPassword#9 ',
      newJoinCode: '   ',
      isJoinEnabled: false,
    })

    expect(post).toHaveBeenCalledWith('/lecturer/classes/class-1/reset-password', {
      newPassword: 'NewPassword#9',
      isJoinEnabled: false,
    })
    expect(result).toEqual({
      classId: 'class-1',
      code: 'SE1802-CSD',
      name: 'Lớp SE1802',
      joinCode: 'JOIN9999',
      hasPassword: true,
      isJoinEnabled: false,
      updatedAt: '2026-11-18T05:00:00Z',
      joinPassword: 'NewPassword#9',
    })
  })

  it('resetPassword sends trimmed newJoinCode when provided', async () => {
    const post = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        classId: 'class-1',
        code: 'SE1802-CSD',
        name: 'Lớp SE1802',
        joinCode: 'CUSTOM26',
        hasPassword: true,
        isJoinEnabled: true,
        updatedAt: '2026-11-18T05:00:00Z',
        joinPassword: 'NewPassword#9',
      },
    } as never)

    await classService.resetPassword('class-1', {
      newPassword: 'NewPassword#9',
      newJoinCode: ' custom26 ',
      isJoinEnabled: true,
    })

    expect(post).toHaveBeenCalledWith('/lecturer/classes/class-1/reset-password', {
      newPassword: 'NewPassword#9',
      newJoinCode: 'custom26',
      isJoinEnabled: true,
    })
  })
})
