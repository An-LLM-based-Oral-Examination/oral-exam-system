import { beforeEach, describe, expect, it } from 'vitest'
import { browserAuthSession } from './browserAuthSession'

beforeEach(() => {
  localStorage.clear()
})

describe('cross-tab refresh lock', () => {
  it('serializes refresh work when Web Locks are unavailable', async () => {
    let release!: () => void
    let entered!: () => void
    const started = new Promise<void>((resolve) => {
      entered = resolve
    })
    const order: string[] = []

    const first = browserAuthSession.withRefreshLock(async () => {
      order.push('first-start')
      entered()
      await new Promise<void>((resolve) => {
        release = resolve
      })
      order.push('first-end')
    })
    await started
    const second = browserAuthSession.withRefreshLock(async () => {
      order.push('second')
    })

    await Promise.resolve()
    expect(order).toEqual(['first-start'])
    release()
    await Promise.all([first, second])
    expect(order).toEqual(['first-start', 'first-end', 'second'])
  })

  it('does not retry a failed refresh action outside the lock', async () => {
    let calls = 0
    await expect(
      browserAuthSession.withRefreshLock(async () => {
        calls += 1
        throw new Error('refresh failed')
      }),
    ).rejects.toThrow('refresh failed')
    expect(calls).toBe(1)
  })
})
