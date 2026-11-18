// Build tools use repository-controlled globs. Production serves static files through Nginx.
// Remove this exception when a patched picomatch is available, or reassess by the deadline.
export const picomatchException = {
  url: 'https://github.com/advisories/GHSA-fc5q-39xc-vjpf',
  dependency: 'picomatch',
  expiresAt: '2026-12-18T00:00:00Z',
}

const severities = ['info', 'low', 'moderate', 'high', 'critical']

export function assessAudit(report, now = new Date()) {
  if (report?.error || report?.auditReportVersion !== 2 ||
      !report.vulnerabilities || !report.metadata?.vulnerabilities) {
    throw new Error('npm audit did not return a valid vulnerability report.')
  }
  const blocked = new Set()
  const accepted = new Set()

  function advisoriesFor(name, path = new Set()) {
    if (path.has(name)) return []
    const entry = report.vulnerabilities[name]
    if (!entry || !Array.isArray(entry.via) || !severities.includes(entry.severity)) {
      throw new Error(`Invalid audit dependency chain for ${name}.`)
    }
    const nextPath = new Set([...path, name])
    return entry.via.flatMap(cause => {
      if (typeof cause === 'string') return advisoriesFor(cause, nextPath)
      if (!cause || !severities.includes(cause.severity) || typeof cause.url !== 'string') {
        throw new Error(`Invalid advisory for ${name}.`)
      }
      return [cause]
    })
  }

  for (const [name, entry] of Object.entries(report.vulnerabilities)) {
    const advisories = advisoriesFor(name)
    if (severities.indexOf(entry.severity) < severities.indexOf('high')) continue
    const highAdvisories = advisories.filter(advisory => severities.indexOf(advisory.severity) >= severities.indexOf('high'))
    if (!highAdvisories.length) throw new Error(`Unresolved high-severity advisory for ${name}.`)
    for (const advisory of highAdvisories) {
      const temporarilyAccepted = advisory.url === picomatchException.url &&
        advisory.dependency === picomatchException.dependency &&
        advisory.severity === 'high' && now < new Date(picomatchException.expiresAt)
      if (temporarilyAccepted) accepted.add(advisory.url)
      else blocked.add(advisory.url)
    }
  }
  return { blocked: [...blocked], accepted: [...accepted] }
}
