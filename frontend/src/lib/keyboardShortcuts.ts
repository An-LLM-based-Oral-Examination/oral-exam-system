const SPACE_INTERACTIVE_SELECTOR = [
  'textarea',
  'input',
  'select',
  'button',
  'a',
  'summary',
  '[role="button"]',
  '[role="switch"]',
  '[role="checkbox"]',
  '[role="radio"]',
  '[role="tab"]',
  '[role="menuitem"]',
  '[role="option"]',
  '[role="combobox"]',
  '[contenteditable]:not([contenteditable="false"])',
  '[data-space-ignore="true"]',
].join(', ')

export function shouldIgnoreSpaceShortcut(event: KeyboardEvent): boolean {
  if (event.defaultPrevented || event.repeat) return true
  if (event.code !== 'Space' && event.key !== ' ') return true
  if (event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return true

  const target = event.target
  if (!(target instanceof Element)) return false
  if (target instanceof HTMLElement && target.isContentEditable) return true
  return Boolean(target.closest(SPACE_INTERACTIVE_SELECTOR))
}
