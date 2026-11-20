import { describe, expect, it } from 'vitest'
import {
  patchSearchParams,
  readEnumParam,
  readPositiveIntParam,
  readStringParam,
} from './urlSearchParams'

describe('urlSearchParams helpers', () => {
  it('reads string, enum, and positive integer params safely with fallbacks', () => {
    const params = new URLSearchParams('q=%20admin%20&status=locked&page=3')

    expect(readStringParam(params, 'q')).toBe('admin')
    expect(readStringParam(params, 'missing', 'default')).toBe('default')
    expect(readEnumParam(params, 'status', ['all', 'active', 'locked'] as const, 'all')).toBe(
      'locked',
    )
    expect(
      readEnumParam(
        new URLSearchParams('status=invalid'),
        'status',
        ['all', 'active', 'locked'] as const,
        'all',
      ),
    ).toBe('all')
    expect(readPositiveIntParam(params, 'page', 1)).toBe(3)
    expect(readPositiveIntParam(new URLSearchParams('page=0'), 'page', 1)).toBe(1)
    expect(readPositiveIntParam(new URLSearchParams('page=2abc'), 'page', 1)).toBe(1)
  })

  it('resets page when non-page filter changes and omits default values', () => {
    const defaults = { q: '', status: 'all', page: 1 }
    const initial = new URLSearchParams('q=nguyenvan&status=locked&page=4')

    const afterFilterChange = patchSearchParams(initial, { status: 'active' }, defaults)
    expect(afterFilterChange.get('status')).toBe('active')
    expect(afterFilterChange.get('page')).toBeNull()

    const afterResetToDefault = patchSearchParams(
      afterFilterChange,
      { status: 'all', q: '' },
      defaults,
    )
    expect(afterResetToDefault.toString()).toBe('')

    const afterPageChange = patchSearchParams(afterFilterChange, { page: 2 }, defaults)
    expect(afterPageChange.get('page')).toBe('2')
    expect(afterPageChange.get('status')).toBe('active')
  })
})
