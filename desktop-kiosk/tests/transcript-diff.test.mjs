import assert from 'node:assert/strict'
import test from 'node:test'
import {
  MAX_TRANSCRIPT_EDIT_RATIO,
  calculateTranscriptEditRatio,
  evaluateTranscriptEdit,
  normalizeTranscript,
} from '../node_modules/.tmp/test-dist/utils/transcript-diff.js'

test('normalizeTranscript applies trim, NFC, whitespace collapse, and lowercase', () => {
  const nfd = 'Ho\u0300a   Bi\u0300nh\t\n  VN'
  assert.equal(normalizeTranscript(nfd), 'hòa bình vn')
  assert.equal(normalizeTranscript('   '), '')
  assert.equal(normalizeTranscript(null), '')
})

test('calculateTranscriptEditRatio matches Backend TranscriptEditRatioCalculator rules', () => {
  assert.equal(MAX_TRANSCRIPT_EDIT_RATIO, 0.35)
  assert.equal(calculateTranscriptEditRatio('', ''), 0)
  assert.equal(calculateTranscriptEditRatio('', 'typed without voice'), 1)
  assert.equal(calculateTranscriptEditRatio('spoken answer', ''), 1)
  assert.equal(calculateTranscriptEditRatio('Clean Architecture', '  clean   architecture '), 0)

  // 35% exact boundary: 100 chars with 35 substitutions -> 0.35 (allowed)
  const base100 = 'a'.repeat(100)
  const edited35 = 'b'.repeat(35) + 'a'.repeat(65)
  const eval35 = evaluateTranscriptEdit(base100, edited35)
  assert.equal(eval35.ratio, 0.35)
  assert.equal(eval35.isExceeded, false)
  assert.equal(eval35.isManuallyEdited, true)

  // 36% -> exceeds 35% cap (blocked)
  const edited36 = 'b'.repeat(36) + 'a'.repeat(64)
  const eval36 = evaluateTranscriptEdit(base100, edited36)
  assert.equal(eval36.ratio, 0.36)
  assert.equal(eval36.isExceeded, true)
})
