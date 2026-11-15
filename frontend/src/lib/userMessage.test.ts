import { describe, expect, it } from 'vitest'
import { clarifyToastText, userMessage } from './userMessage'

describe('user-facing API errors', () => {
  it.each([
    'NOT_FOUND: Môn học không tồn tại.',
    'NOT FOUND: Môn học không tồn tại.',
    'not_found — Môn học không tồn tại.',
    'BAD_REQUEST: Môn học không tồn tại.',
    'INTERNAL_SERVER_ERROR: Môn học không tồn tại.',
  ])('removes only the leading status prefix: %s', (detail) => {
    expect(userMessage({ detail }, 'Không tải được dữ liệu.')).toBe('Môn học không tồn tại.')
    expect(clarifyToastText(detail)).toBe('Môn học không tồn tại.')
  })

  it('preserves the meaning when a status code is part of the message', () => {
    expect(userMessage({ detail: 'Mã NOT_FOUND đang được tra cứu.' }, 'Lỗi.'))
      .toBe('Mã NOT_FOUND đang được tra cứu.')
  })

  it.each([
    'NOT_FOUND: Npgsql.PostgresException: 42P01',
    'INTERNAL_SERVER_ERROR: at Handler.cs:line 12',
  ])('uses a safe fallback for internal details: %s', (detail) => {
    expect(userMessage({ detail }, 'Không tải được dữ liệu.')).toBe('Không tải được dữ liệu.')
    expect(clarifyToastText(detail)).toBe('Không thực hiện được thao tác. Vui lòng thử lại.')
  })
})
