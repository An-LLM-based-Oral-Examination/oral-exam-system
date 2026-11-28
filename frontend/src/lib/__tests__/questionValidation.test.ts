import { describe, expect, it } from 'vitest'
import { hasValidScorePrecision, isSafeQuestionImageUrl } from '../questionValidation'

describe('questionValidation', () => {
  describe('isSafeQuestionImageUrl', () => {
    it('accepts safe /uploads/questions paths, public HTTPS domains, and public IPv6/IPv4-mapped addresses', () => {
      expect(isSafeQuestionImageUrl('/uploads/questions/202611/diagram.png')).toBe(true)
      expect(isSafeQuestionImageUrl('/uploads/questions/sub_dir/img-01.webp')).toBe(true)
      expect(isSafeQuestionImageUrl('https://cdn.example.com/questions/arch.jpg')).toBe(true)
      expect(isSafeQuestionImageUrl('https://[2001:4860:4860::8888]/a.png')).toBe(true)
      expect(isSafeQuestionImageUrl('https://[::ffff:8.8.8.8]/a.png')).toBe(true)
    })

    it('rejects all restricted IPv6, IPv4-mapped IPv6, and expanded IPv6 SSRF hosts', () => {
      const restrictedUrls = [
        'https://[::]/a.png',
        'https://[0:0:0:0:0:0:0:0]/a.png',
        'https://[::1]/a.png',
        'https://[0:0:0:0:0:0:0:1]/a.png',
        'https://[fe80::1]/a.png',
        'https://[febf:ffff::1]/a.png',
        'https://[fc00::1]/a.png',
        'https://[fd12:3456:789a::1]/a.png',
        'https://[fec0::1]/a.png',
        'https://[feff::1]/a.png',
        'https://[ff02::1]/a.png',
        'https://[::ffff:127.0.0.1]/a.png',
        'https://[::ffff:10.0.0.1]/a.png',
        'https://[::ffff:192.168.1.1]/a.png',
        'https://[::ffff:169.254.169.254]/a.png',
        'https://[::ffff:172.16.0.1]/a.png',
        'https://[::ffff:100.64.0.1]/a.png',
        'https://[::ffff:0.0.0.0]/a.png',
        'https://[::ffff:224.0.0.1]/a.png',
        'https://[0000:0000:0000:0000:0000:ffff:127.0.0.1]/a.png',
        'https://[0000:0000:0000:0000:0000:ffff:7f00:0001]/a.png',
      ]

      for (const url of restrictedUrls) {
        expect(isSafeQuestionImageUrl(url), url).toBe(false)
      }
    })

    it('rejects raw URLs exceeding 500 chars, restricted IPv4, userinfo, non-HTTPS, and traversal', () => {
      const rawOver500ButTrimmed499 = `  https://example.com/${'a'.repeat(475)}.png`
      expect(rawOver500ButTrimmed499.length).toBe(501)
      expect(rawOver500ButTrimmed499.trim().length).toBe(499)
      expect(isSafeQuestionImageUrl(rawOver500ButTrimmed499)).toBe(false)

      expect(isSafeQuestionImageUrl('http://cdn.example.com/a.png')).toBe(false)
      expect(isSafeQuestionImageUrl('https://user@example.com/a.png')).toBe(false)
      expect(isSafeQuestionImageUrl('https://localhost/a.png')).toBe(false)
      expect(isSafeQuestionImageUrl('https://127.0.0.1/a.png')).toBe(false)
      expect(isSafeQuestionImageUrl('https://2130706433/a.png')).toBe(false)
      expect(isSafeQuestionImageUrl('https://0x7f000001/a.png')).toBe(false)
      expect(isSafeQuestionImageUrl('/uploads/questions/../secret.png')).toBe(false)
      expect(isSafeQuestionImageUrl('https://example.com/a\n.png')).toBe(false)
    })
  })

  describe('hasValidScorePrecision', () => {
    it('accepts up to 2 decimal places and rejects 1e-9, 5.000000001, and 3-decimal floats', () => {
      expect(hasValidScorePrecision(0.01)).toBe(true)
      expect(hasValidScorePrecision(0.1)).toBe(true)
      expect(hasValidScorePrecision(0.2)).toBe(true)
      expect(hasValidScorePrecision(0.3)).toBe(true)
      expect(hasValidScorePrecision(3.34)).toBe(true)
      expect(hasValidScorePrecision(9.99)).toBe(true)
      expect(hasValidScorePrecision(10)).toBe(true)

      expect(hasValidScorePrecision(1e-9)).toBe(false)
      expect(hasValidScorePrecision(5.000000001)).toBe(false)
      expect(hasValidScorePrecision(3.333)).toBe(false)
      expect(hasValidScorePrecision(1.005)).toBe(false)
      expect(hasValidScorePrecision(Number.NaN)).toBe(false)
      expect(hasValidScorePrecision(Number.POSITIVE_INFINITY)).toBe(false)
    })
  })
})
