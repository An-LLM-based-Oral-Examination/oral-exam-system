import { describe, expect, it } from 'vitest'
import {
  classifyMicrophoneError,
  detectBrowserFamily,
  getBrowserPermissionGuide,
  getMicrophoneIssueMessage,
} from '@/utils/microphoneDiagnostics'

describe('microphoneDiagnostics', () => {
  it('detects Chrome, Cốc Cốc, Edge, Firefox, and Safari accurately from User-Agent', () => {
    expect(
      detectBrowserFamily(
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) coc_coc_browser/114.0.120 Chrome/108.0.5359.128 Safari/537.36',
      ),
    ).toBe('coccoc')
    expect(
      detectBrowserFamily(
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0',
      ),
    ).toBe('edge')
    expect(
      detectBrowserFamily(
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36',
      ),
    ).toBe('chrome')
    expect(
      detectBrowserFamily(
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0',
      ),
    ).toBe('firefox')
    expect(
      detectBrowserFamily(
        'Mozilla/5.0 (Macintosh; Intel Mac OS X 14_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15',
      ),
    ).toBe('safari')
  })

  it('classifies DOMException and Web Speech microphone errors accurately', () => {
    expect(classifyMicrophoneError(new DOMException('Permission denied', 'NotAllowedError'))).toBe(
      'permission_denied',
    )
    expect(classifyMicrophoneError('not-allowed')).toBe('permission_denied')
    expect(
      classifyMicrophoneError(new DOMException('Requested device not found', 'NotFoundError')),
    ).toBe('device_missing')
    expect(classifyMicrophoneError('audio-capture')).toBe('device_missing')
    expect(
      classifyMicrophoneError(new DOMException('Could not start audio source', 'NotReadableError')),
    ).toBe('device_busy')
  })

  it('provides 3 actionable Vietnamese steps for every browser family', () => {
    for (const family of ['chrome', 'coccoc', 'edge', 'firefox', 'safari', 'other'] as const) {
      const guide = getBrowserPermissionGuide(family)
      expect(guide.steps.length).toBe(3)
      expect(guide.osHint.length).toBeGreaterThan(10)
    }
    expect(getMicrophoneIssueMessage('permission_denied')).toContain('Microphone')
  })
})
