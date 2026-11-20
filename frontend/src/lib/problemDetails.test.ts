import { describe, expect, it } from 'vitest'
import { extractProblemDetails, mapProblemFieldErrors } from './problemDetails'

describe('problemDetails helpers', () => {
  it('extracts flattened apiClient ProblemDetails with status flags and traceId', () => {
    const parsed = extractProblemDetails(
      {
        status: 409,
        title: 'Conflict',
        detail: 'CONFLICT: Email đã tồn tại trong hệ thống.',
        errorCode: 'DUPLICATE_EMAIL',
        traceId: 'trace-123',
      },
      'Thao tác thất bại.',
    )

    expect(parsed.status).toBe(409)
    expect(parsed.isConflict).toBe(true)
    expect(parsed.isAuthError).toBe(false)
    expect(parsed.detail).toBe('Email đã tồn tại trong hệ thống.')
    expect(parsed.errorCode).toBe('DUPLICATE_EMAIL')
    expect(parsed.traceId).toBe('trace-123')
  })

  it('unwraps Axios-wrapped response.data ProblemDetails and maps PascalCase field errors', () => {
    const axiosErr = {
      response: {
        status: 400,
        data: {
          title: 'Validation Failed',
          detail: 'Dữ liệu đầu vào không hợp lệ.',
          errors: {
            Email: ['Email không đúng định dạng.'],
            'request.TemporaryPassword': ['Mật khẩu tạm thời phải từ 8 ký tự.'],
            StudentCode: ['Mã sinh viên bắt buộc khi vai trò là student.'],
          },
        },
      },
    }

    const parsed = extractProblemDetails(axiosErr, 'Lỗi mặc định.')
    expect(parsed.status).toBe(400)
    expect(parsed.detail).toBe('Dữ liệu đầu vào không hợp lệ.')
    expect(parsed.fieldErrors).toEqual({
      email: 'Email không đúng định dạng.',
      temporaryPassword: 'Mật khẩu tạm thời phải từ 8 ký tự.',
      studentCode: 'Mã sinh viên bắt buộc khi vai trò là student.',
    })

    const mapped = mapProblemFieldErrors(axiosErr, ['email', 'temporaryPassword', 'studentCode'])
    expect(mapped).toEqual({
      email: 'Email không đúng định dạng.',
      temporaryPassword: 'Mật khẩu tạm thời phải từ 8 ký tự.',
      studentCode: 'Mã sinh viên bắt buộc khi vai trò là student.',
    })
  })

  it.each([
    [401, 'isAuthError'],
    [403, 'isAuthError'],
    [410, 'isGone'],
    [429, 'isRateLimited'],
  ] as const)('classifies HTTP %i status flag %s and provides Vietnamese fallback', (status, flag) => {
    const parsed = extractProblemDetails({ status }, 'Lỗi mặc định.')
    expect(parsed[flag]).toBe(true)
    expect(parsed.detail.length).toBeGreaterThan(10)
  })

  it('handles array errors payloads without generating numeric fieldErrors keys', () => {
    const parsed = extractProblemDetails(
      {
        status: 400,
        detail: 'Dữ liệu không hợp lệ.',
        errors: ['Top-level array error'],
      },
      'Lỗi mặc định.',
    )

    expect(parsed.fieldErrors).toEqual({})
    expect(parsed.fieldMessages).toEqual([])
    expect(parsed.detail).toBe('Dữ liệu không hợp lệ.')
  })

  it('suppresses SQL/Postgres detail and does not leak raw Axios 500 transport message', () => {
    const axiosErr = Object.assign(new Error('Request failed with status code 500'), {
      response: {
        status: 500,
        data: {
          status: 500,
          detail: 'PostgresException: 42703: column "bad_col" does not exist',
        },
      },
    })

    const parsed = extractProblemDetails(axiosErr, 'Lỗi hệ thống an toàn.')
    expect(parsed.status).toBe(500)
    expect(parsed.detail).toBe('Lỗi hệ thống an toàn.')
    expect(parsed.detail).not.toMatch(/PostgresException|42703|Request failed with status code/i)

    const rawAxiosWithoutDetail = new Error('Request failed with status code 500')
    const parsedTransportOnly = extractProblemDetails(rawAxiosWithoutDetail, 'Không kết nối được máy chủ.')
    expect(parsedTransportOnly.detail).toBe('Không kết nối được máy chủ.')
  })
})
