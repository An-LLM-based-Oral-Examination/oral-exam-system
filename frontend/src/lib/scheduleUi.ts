/** Defer UI setState out of the synchronous effect body (eslint react-hooks/set-state-in-effect). */
export function scheduleUi(fn: () => void) {
  const id = window.setTimeout(fn, 0)
  return () => window.clearTimeout(id)
}
