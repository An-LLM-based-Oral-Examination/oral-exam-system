import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { googleAuthManager } from './googleAuth.service'

const initialize = vi.fn()
beforeEach(() => {
  googleAuthManager.reset()
  initialize.mockReset()
  window.google = { accounts: { id: { initialize, renderButton: vi.fn(), cancel: vi.fn() } } }
})
afterEach(() => { googleAuthManager.reset(); delete window.google })

describe('GIS ID-token nonce binding', () => {
  it('sends the server nonce and deduplicates only the same client/nonce', () => {
    const callback = vi.fn()
    googleAuthManager.initialize('client', callback, 'n'.repeat(43))
    googleAuthManager.initialize('client', callback, 'n'.repeat(43))
    expect(initialize).toHaveBeenCalledTimes(1)
    expect(initialize).toHaveBeenCalledWith(expect.objectContaining({ nonce: 'n'.repeat(43), auto_select: false }))
  })
  it('renews SDK configuration for a new challenge and ignores stale callbacks', () => {
    const old = vi.fn(); const next = vi.fn()
    googleAuthManager.initialize('client', old, 'a'.repeat(43))
    const staleCallback = initialize.mock.calls[0][0].callback
    googleAuthManager.initialize('client', next, 'b'.repeat(43))
    expect(initialize).toHaveBeenCalledTimes(2)
    staleCallback({ credential: 'old-token' })
    expect(next).not.toHaveBeenCalled()
    initialize.mock.calls[1][0].callback({ credential: 'new-token' })
    expect(next).toHaveBeenCalledWith({ credential: 'new-token' })
  })
  it('refuses missing browser challenge instead of silently using a legacy flow', () => {
    expect(() => googleAuthManager.initialize('client', vi.fn(), '')).toThrow()
    expect(initialize).not.toHaveBeenCalled()
  })
})
