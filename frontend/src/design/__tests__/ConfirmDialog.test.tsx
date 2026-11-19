import { useState } from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as bodyScrollLockModule from '../bodyScrollLock'
import { ConfirmDialog } from '../ConfirmDialog'
import { FormDialog } from '../overlays'

describe('ConfirmDialog Escape capture-phase isolation', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    bodyScrollLockModule.resetBodyScrollLock()
  })
  it('prevents Escape from propagating to an outer FormDialog when pending is true', () => {
    const onOuterClose = vi.fn()
    const onCancel = vi.fn()
    const onConfirm = vi.fn()

    render(
      <FormDialog open={true} busy={false} onClose={onOuterClose} title="Outer dialog">
        <ConfirmDialog
          title="Xác nhận xóa"
          message="Hành động đang xử lý."
          confirmLabel="Xác nhận"
          cancelLabel="Hủy"
          pending={true}
          onConfirm={onConfirm}
          onCancel={onCancel}
        />
      </FormDialog>,
    )

    const escapeEvent = new KeyboardEvent('keydown', {
      key: 'Escape',
      cancelable: true,
      bubbles: true,
    })
    window.dispatchEvent(escapeEvent)

    expect(escapeEvent.defaultPrevented).toBe(true)
    expect(onCancel).not.toHaveBeenCalled()
    expect(onConfirm).not.toHaveBeenCalled()
    expect(onOuterClose).not.toHaveBeenCalled()
  })

  it('calls onCancel once and blocks outer FormDialog Escape when pending is false', () => {
    const onOuterClose = vi.fn()
    const onCancel = vi.fn()
    const onConfirm = vi.fn()

    render(
      <FormDialog open={true} busy={false} onClose={onOuterClose} title="Outer dialog">
        <ConfirmDialog
          title="Xác nhận xóa"
          message="Hành động chưa chạy."
          confirmLabel="Xác nhận"
          cancelLabel="Hủy"
          pending={false}
          onConfirm={onConfirm}
          onCancel={onCancel}
        />
      </FormDialog>,
    )

    const escapeEvent = new KeyboardEvent('keydown', {
      key: 'Escape',
      cancelable: true,
      bubbles: true,
    })
    window.dispatchEvent(escapeEvent)

    expect(escapeEvent.defaultPrevented).toBe(true)
    expect(onCancel).toHaveBeenCalledTimes(1)
    expect(onConfirm).not.toHaveBeenCalled()
    expect(onOuterClose).not.toHaveBeenCalled()
  })

  it('handles pending true -> false Escape dismissal via pendingRef without re-locking body scroll', () => {
    const lockSpy = vi.spyOn(bodyScrollLockModule, 'lockBodyScroll')
    const onCancel = vi.fn()
    const onConfirm = vi.fn()

    const { rerender } = render(
      <ConfirmDialog
        title="Xác nhận khóa"
        message="Đang xử lý yêu cầu."
        confirmLabel="Xác nhận"
        cancelLabel="Hủy"
        pending={true}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    )

    expect(lockSpy).toHaveBeenCalledTimes(1)

    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onCancel).not.toHaveBeenCalled()

    rerender(
      <ConfirmDialog
        title="Xác nhận khóa"
        message="Yêu cầu hoàn tất."
        confirmLabel="Xác nhận"
        cancelLabel="Hủy"
        pending={false}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    )

    expect(lockSpy).toHaveBeenCalledTimes(1)

    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onCancel).toHaveBeenCalledTimes(1)
  })

  it('traps Tab and Shift+Tab strictly inside ConfirmDialog when nested over a FormDialog', async () => {
    const user = userEvent.setup()

    render(
      <div>
        <button type="button">Nút nền ngoài</button>
        <FormDialog open={true} onClose={vi.fn()} title="Quản lý CLO" variant="calm">
          <button type="button">Xóa CLO</button>
          <ConfirmDialog
            title="Xác nhận xóa CLO"
            message="Thao tác không thể hoàn tác."
            confirmLabel="Xác nhận"
            cancelLabel="Hủy"
            variant="calm"
            onConfirm={vi.fn()}
            onCancel={vi.fn()}
          />
        </FormDialog>
      </div>,
    )

    const alertDialog = screen.getByRole('alertdialog', { name: 'Xác nhận xóa CLO' })
    const closeBtn = screen.getAllByRole('button', { name: 'Đóng' }).at(-1)!
    const cancelBtn = screen.getByRole('button', { name: 'Hủy' })
    const confirmBtn = screen.getByRole('button', { name: 'Xác nhận' })

    expect(document.activeElement).toBe(confirmBtn)
    expect(alertDialog.contains(document.activeElement)).toBe(true)

    await user.tab()
    expect(document.activeElement).toBe(closeBtn)
    expect(alertDialog.contains(document.activeElement)).toBe(true)

    await user.tab()
    expect(document.activeElement).toBe(cancelBtn)
    expect(alertDialog.contains(document.activeElement)).toBe(true)

    await user.tab()
    expect(document.activeElement).toBe(confirmBtn)
    expect(alertDialog.contains(document.activeElement)).toBe(true)

    await user.tab({ shift: true })
    expect(document.activeElement).toBe(cancelBtn)
    expect(alertDialog.contains(document.activeElement)).toBe(true)

    await user.tab({ shift: true })
    expect(document.activeElement).toBe(closeBtn)
    expect(alertDialog.contains(document.activeElement)).toBe(true)

    await user.tab({ shift: true })
    expect(document.activeElement).toBe(confirmBtn)
    expect(alertDialog.contains(document.activeElement)).toBe(true)
  })

  it('restores focus to the triggering button inside FormDialog when nested ConfirmDialog closes via Escape or Cancel', async () => {
    const user = userEvent.setup()

    function NestedHarness() {
      const [confirmOpen, setConfirmOpen] = useState(false)
      return (
        <FormDialog open={true} onClose={vi.fn()} title="Quản lý CLO" variant="calm">
          <button type="button" onClick={() => setConfirmOpen(true)}>
            Xóa CLO-01
          </button>
          {confirmOpen ? (
            <ConfirmDialog
              title="Xác nhận xóa CLO-01"
              message="Bạn có chắc chắn muốn xóa?"
              confirmLabel="Xóa ngay"
              cancelLabel="Hủy"
              variant="calm"
              onConfirm={() => setConfirmOpen(false)}
              onCancel={() => setConfirmOpen(false)}
            />
          ) : null}
        </FormDialog>
      )
    }

    render(<NestedHarness />)

    const deleteTrigger = screen.getByRole('button', { name: 'Xóa CLO-01' })
    deleteTrigger.focus()
    await user.click(deleteTrigger)

    expect(screen.getByRole('alertdialog', { name: 'Xác nhận xóa CLO-01' })).toBeInTheDocument()

    // Close via Escape
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(document.activeElement).toBe(deleteTrigger)

    // Reopen and close via Cancel button
    await user.click(deleteTrigger)
    expect(screen.getByRole('alertdialog', { name: 'Xác nhận xóa CLO-01' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Hủy' }))
    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(document.activeElement).toBe(deleteTrigger)
  })

  it('focuses the alertdialog container and keeps Tab trapped inside when mounted with pending=true', async () => {
    const user = userEvent.setup()

    render(
      <FormDialog open={true} onClose={vi.fn()} title="Outer dialog">
        <button type="button">Outer button</button>
        <ConfirmDialog
          title="Đang xử lý"
          message="Vui lòng đợi."
          confirmLabel="Xác nhận"
          cancelLabel="Hủy"
          pending={true}
          onConfirm={vi.fn()}
          onCancel={vi.fn()}
        />
      </FormDialog>,
    )

    const alertDialog = screen.getByRole('alertdialog', { name: 'Đang xử lý' })
    expect(document.activeElement).toBe(alertDialog)

    await user.tab()
    expect(document.activeElement).toBe(alertDialog)
    await user.tab({ shift: true })
    expect(document.activeElement).toBe(alertDialog)
  })
})
