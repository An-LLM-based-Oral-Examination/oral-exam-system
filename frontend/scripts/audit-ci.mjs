import { spawnSync } from 'node:child_process'
import { assessAudit, picomatchException } from './audit-policy.mjs'

try {
  if (!process.env.npm_execpath) throw new Error('Run this check with npm run audit:ci.')
  const result = spawnSync(process.execPath, [process.env.npm_execpath, 'audit', '--json'], {
    encoding: 'utf8',
    timeout: 120_000,
    maxBuffer: 10 * 1024 * 1024,
  })
  if (result.error) throw result.error
  if (result.signal || ![0, 1].includes(result.status)) {
    throw new Error(result.stderr || `npm audit failed with exit code ${result.status}.`)
  }
  const report = JSON.parse(result.stdout)
  const assessment = assessAudit(report)
  console.log('npm audit vulnerability totals:', report.metadata.vulnerabilities)
  for (const advisory of assessment.accepted) {
    console.warn(`Temporary build-tool exception until ${picomatchException.expiresAt}: ${advisory}`)
  }
  for (const advisory of assessment.blocked) console.error(`Blocked vulnerability: ${advisory}`)
  process.exitCode = assessment.blocked.length ? 1 : 0
} catch (error) {
  console.error('Dependency audit could not complete:', error.message)
  process.exitCode = 1
}
