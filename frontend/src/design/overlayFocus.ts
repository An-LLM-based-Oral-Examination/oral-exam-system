const FOCUSABLE_SELECTOR = [
  'button:not([disabled])',
  '[href]',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(', ')

function isFocusableElement(element: Element, container: HTMLElement): element is HTMLElement {
  if (!(element instanceof HTMLElement)) return false
  if (element.hasAttribute('disabled') || element.closest('fieldset[disabled]')) {
    return false
  }
  const tabIndexAttr = element.getAttribute('tabindex')
  if (tabIndexAttr !== null) {
    const parsed = Number.parseInt(tabIndexAttr, 10)
    if (Number.isNaN(parsed) || parsed < 0) return false
  }
  let current: HTMLElement | null = element
  while (current && current !== container) {
    if (
      current.hasAttribute('hidden') ||
      current.hasAttribute('inert') ||
      current.getAttribute('aria-hidden') === 'true'
    ) {
      return false
    }
    if (window.getComputedStyle(current).display === 'none') {
      return false
    }
    current = current.parentElement
  }
  const ownStyle = window.getComputedStyle(element)
  return ownStyle.display !== 'none' && ownStyle.visibility !== 'hidden'
}

export function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll(FOCUSABLE_SELECTOR)).filter((element) =>
    isFocusableElement(element, container),
  )
}

export function isTopmostOverlayModal(element: HTMLElement): boolean {
  const modals = document.querySelectorAll('[role="dialog"], [role="alertdialog"]')
  return modals.length === 0 || modals[modals.length - 1] === element
}

export function trapOverlayTabKey(event: KeyboardEvent, container: HTMLElement) {
  const focusables = getFocusableElements(container)
  if (focusables.length === 0) {
    event.preventDefault()
    container.focus()
    return
  }

  const first = focusables[0]
  const last = focusables[focusables.length - 1]
  const active = document.activeElement

  if (!(active instanceof HTMLElement) || !container.contains(active)) {
    event.preventDefault()
    ;(event.shiftKey ? last : first).focus()
    return
  }

  if (event.shiftKey) {
    if (active === first || active === container) {
      event.preventDefault()
      last.focus()
    }
  } else if (active === last || active === container) {
    event.preventDefault()
    first.focus()
  }
}
