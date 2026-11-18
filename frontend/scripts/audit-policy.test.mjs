import { test } from 'node:test'
import assert from 'node:assert/strict'
import { assessAudit, picomatchException } from './audit-policy.mjs'

const knownAdvisory = { ...picomatchException, severity: 'high' }
function report(advisory = knownAdvisory) {
  return {
    auditReportVersion: 2,
    metadata: { vulnerabilities: { high: 2 } },
    vulnerabilities: {
      vite: { severity: 'high', via: ['picomatch'] },
      picomatch: { severity: 'high', via: [advisory] },
    },
  }
}
const beforeExpiry = new Date('2026-11-18T00:00:00Z')

test('accepts only the known advisory and its transitive dependants', () => {
  assert.deepEqual(assessAudit(report(), beforeExpiry), { blocked: [], accepted: [knownAdvisory.url] })
})
test('blocks another high advisory on the same package', () => {
  const advisory = { ...knownAdvisory, url: 'https://github.com/advisories/GHSA-new-issue' }
  assert.deepEqual(assessAudit(report(advisory), beforeExpiry).blocked, [advisory.url])
})
test('blocks an unrelated high advisory alongside the exception', () => {
  const input = report()
  input.vulnerabilities.vite.via.push({ severity: 'high', dependency: 'vite', url: 'https://example.test/other' })
  assert.deepEqual(assessAudit(input, beforeExpiry).blocked, ['https://example.test/other'])
})
test('blocks a critical escalation and the exception after expiry', () => {
  assert.equal(assessAudit(report({ ...knownAdvisory, severity: 'critical' }), beforeExpiry).blocked.length, 1)
  assert.equal(assessAudit(report(), new Date(picomatchException.expiresAt)).blocked.length, 1)
})
test('fails closed for registry errors, missing causes and unresolved high advisories', () => {
  assert.throws(() => assessAudit({ error: { code: 'ENETUNREACH' } }, beforeExpiry))
  const input = report()
  delete input.vulnerabilities.picomatch
  assert.throws(() => assessAudit(input, beforeExpiry))
  assert.throws(() => assessAudit(report({ ...knownAdvisory, severity: 'moderate' }), beforeExpiry))
})
test('allows a clean report', () => {
  assert.deepEqual(assessAudit({ auditReportVersion: 2, metadata: { vulnerabilities: {} }, vulnerabilities: {} }, beforeExpiry), { blocked: [], accepted: [] })
})
