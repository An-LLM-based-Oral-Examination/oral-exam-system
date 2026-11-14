/**
 * A result is visible only after the official grade has been published.
 * Earlier ticket states belong to the exam workspace, not the results view.
 */
export function isPublishedOfficialTicket(status: string): boolean {
  const normalizedStatus = status.trim().toUpperCase()
  return normalizedStatus === 'PUBLISHED' || normalizedStatus === 'LOCKED'
}
