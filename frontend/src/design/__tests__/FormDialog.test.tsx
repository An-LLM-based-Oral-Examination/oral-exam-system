import { StrictMode, useState } from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as bodyScrollLockModule from '../bodyScrollLock'
import { BareDialog, DialogFooter, FormDialog, SideDrawer } from '../overlays'

describe('FormDialog accessibility, focus management & wrapping', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    bodyScrollLockModule.resetBodyScrollLock()
  })

  it('moves focus into the dialog on open and restores focus to the trigger button on close', async () => {
    const user = userEvent.setup()

    function Harness() {
      const [open, setOpen] = useState(false)
      return (
        <div>
          <button type="button" onClick={() => setOpen(true)}>
            Mở hộp thoại
          </button>
          <FormDialog
            open={open}
            onClose={() => setOpen(false)}
            title="Tạo tài khoản"
            variant="calm"
          >
            <label htmlFor="full-name-input">Họ tên</label>
            <input id="full-name-input" type="text" />
          </FormDialog>
        </div>
      )
    }

    render(<Harness />)

    const trigger = screen.getByRole('button', { name: 'Mở hộp thoại' })
    trigger.focus()
    expect(document.activeElement).toBe(trigger)

    await user.click(trigger)

    const dialog = screen.getByRole('dialog', { name: 'Tạo tài khoản' })
    expect(dialog.contains(document.activeElement)).toBe(true)
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Đóng' }))

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('falls back to focusing the dialog container when no focusable descendants exist', () => {
    render(
      <FormDialog open={true} onClose={vi.fn()} label="Thông tin chỉ đọc">
        <p>Nội dung tĩnh không có nút bấm.</p>
      </FormDialog>,
    )

    const dialog = screen.getByRole('dialog', { name: 'Thông tin chỉ đọc' })
    expect(document.activeElement).toBe(dialog)
  })

  it('traps Tab and Shift+Tab inside the dialog without leaking to background controls', async () => {
    const user = userEvent.setup()

    render(
      <div>
        <button type="button">Nút nền trước</button>
        <FormDialog
          open={true}
          onClose={vi.fn()}
          title="Chỉnh sửa vai trò"
          variant="calm"
          footer={
            <DialogFooter variant="calm">
              <button type="button">Hủy</button>
              <button type="button">Lưu thay đổi</button>
            </DialogFooter>
          }
        >
          <input aria-label="Ghi chú" type="text" />
        </FormDialog>
        <button type="button">Nút nền sau</button>
      </div>,
    )

    const dialog = screen.getByRole('dialog', { name: 'Chỉnh sửa vai trò' })
    const closeBtn = screen.getByRole('button', { name: 'Đóng' })
    const noteInput = screen.getByLabelText('Ghi chú')
    const cancelBtn = screen.getByRole('button', { name: 'Hủy' })
    const saveBtn = screen.getByRole('button', { name: 'Lưu thay đổi' })

    expect(document.activeElement).toBe(closeBtn)

    await user.tab()
    expect(document.activeElement).toBe(noteInput)
    await user.tab()
    expect(document.activeElement).toBe(cancelBtn)
    await user.tab()
    expect(document.activeElement).toBe(saveBtn)

    await user.tab()
    expect(document.activeElement).toBe(closeBtn)
    expect(dialog.contains(document.activeElement)).toBe(true)

    await user.tab({ shift: true })
    expect(document.activeElement).toBe(saveBtn)
    expect(dialog.contains(document.activeElement)).toBe(true)
  })

  it('handles synchronous Escape state via busyRef when busy transitions true -> false', () => {
    const lockSpy = vi.spyOn(bodyScrollLockModule, 'lockBodyScroll')
    const onClose = vi.fn()

    const { rerender } = render(
      <FormDialog open={true} busy={true} onClose={onClose} title="Đang lưu">
        <p>Đang gửi yêu cầu...</p>
      </FormDialog>,
    )

    expect(lockSpy).toHaveBeenCalledTimes(1)

    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onClose).not.toHaveBeenCalled()

    rerender(
      <FormDialog open={true} busy={false} onClose={onClose} title="Đang lưu">
        <p>Đã xong, có thể đóng.</p>
      </FormDialog>,
    )

    expect(lockSpy).toHaveBeenCalledTimes(1)

    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('renders long unbroken eyebrow and title with wrapping classes inside a min-w-0 container', () => {
    const longEyebrow = 'VeryLongEyebrowIdentifier_WithoutSpaces_AuditRecord'
    const longTitle = 'VeryLongEntityNameWithoutSpaces_UserAccountConfiguration'

    render(
      <FormDialog
        open={true}
        onClose={vi.fn()}
        eyebrow={longEyebrow}
        title={longTitle}
        variant="calm"
      >
        <p>Chi tiết</p>
      </FormDialog>,
    )

    const heading = screen.getByRole('heading', { name: longTitle })
    const eyebrow = screen.getByText(longEyebrow)

    expect(heading).toHaveClass('break-words', '[overflow-wrap:anywhere]')
    expect(eyebrow).toHaveClass('break-words', '[overflow-wrap:anywhere]')
    expect(heading.parentElement).toHaveClass('min-w-0')
  })

  it('focuses an autoFocus descendant on open and restores focus to the trigger button on Escape close', async () => {
    const user = userEvent.setup()

    function Harness() {
      const [open, setOpen] = useState(false)
      return (
        <div>
          <button type="button" onClick={() => setOpen(true)}>
            Mở nhập điểm
          </button>
          <FormDialog
            open={open}
            onClose={() => setOpen(false)}
            title="Chấm điểm"
            variant="calm"
          >
            <label htmlFor="autofocus-score-input">Điểm cả bài</label>
            <input id="autofocus-score-input" type="number" autoFocus />
          </FormDialog>
        </div>
      )
    }

    render(<Harness />)

    const trigger = screen.getByRole('button', { name: 'Mở nhập điểm' })
    trigger.focus()
    await user.click(trigger)

    const input = screen.getByLabelText('Điểm cả bài')
    expect(document.activeElement).toBe(input)

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('preserves autoFocus inside StrictMode and restores focus to the trigger on close', async () => {
    const user = userEvent.setup()

    function Harness() {
      const [open, setOpen] = useState(false)
      return (
        <StrictMode>
          <div>
            <button type="button" onClick={() => setOpen(true)}>
              Mở StrictMode dialog
            </button>
            {open ? (
              <FormDialog open={true} onClose={() => setOpen(false)} title="StrictMode Form">
                <label htmlFor="strict-autofocus-input">Tên ca</label>
                <input id="strict-autofocus-input" type="text" autoFocus />
              </FormDialog>
            ) : null}
          </div>
        </StrictMode>
      )
    }

    render(<Harness />)

    const trigger = screen.getByRole('button', { name: 'Mở StrictMode dialog' })
    trigger.focus()
    await user.click(trigger)

    expect(document.activeElement).toBe(screen.getByLabelText('Tên ca'))

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('traps Tab and restores layered focus for SideDrawer with a nested FormDialog, BareDialog, and dual FormDialogs', async () => {
    const user = userEvent.setup()

    function MultiOverlayHarness() {
      const [drawerOpen, setDrawerOpen] = useState(false)
      const [nestedInDrawerOpen, setNestedInDrawerOpen] = useState(false)
      const [bareOpen, setBareOpen] = useState(false)
      const [firstOpen, setFirstOpen] = useState(false)
      const [secondOpen, setSecondOpen] = useState(false)

      return (
        <div>
          <button type="button" onClick={() => setDrawerOpen(true)}>
            Mở Drawer
          </button>
          <button type="button" onClick={() => setBareOpen(true)}>
            Mở BareDialog
          </button>
          <button type="button" onClick={() => setFirstOpen(true)}>
            Mở Form 1
          </button>

          <SideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} title="Chi tiết đề">
            <button type="button" onClick={() => setNestedInDrawerOpen(true)}>
              Duyệt câu hỏi
            </button>
            <FormDialog
              open={nestedInDrawerOpen}
              nested
              onClose={() => setNestedInDrawerOpen(false)}
              title="Xác nhận duyệt"
            >
              <button type="button">Lưu duyệt</button>
            </FormDialog>
          </SideDrawer>

          <BareDialog open={bareOpen} onClose={() => setBareOpen(false)} label="Soạn thảo nhanh">
            <button type="button">Nút Bare A</button>
            <button type="button">Nút Bare B</button>
          </BareDialog>

          <FormDialog open={firstOpen} onClose={() => setFirstOpen(false)} title="Hộp thoại 1">
            <button type="button" onClick={() => setSecondOpen(true)}>
              Mở Form 2
            </button>
            <FormDialog
              open={secondOpen}
              nested
              onClose={() => setSecondOpen(false)}
              title="Hộp thoại 2"
            >
              <button type="button">Nút Form 2</button>
            </FormDialog>
          </FormDialog>
        </div>
      )
    }

    render(<MultiOverlayHarness />)

    // SideDrawer + nested FormDialog (PROBE-4)
    const openDrawer = screen.getByRole('button', { name: 'Mở Drawer' })
    openDrawer.focus()
    await user.click(openDrawer)
    const approveBtn = screen.getByRole('button', { name: 'Duyệt câu hỏi' })
    approveBtn.focus()
    await user.click(approveBtn)

    const nestedDialog = screen.getByRole('dialog', { name: 'Xác nhận duyệt' })
    expect(nestedDialog.contains(document.activeElement)).toBe(true)
    await user.tab()
    expect(nestedDialog.contains(document.activeElement)).toBe(true)
    await user.tab()
    expect(nestedDialog.contains(document.activeElement)).toBe(true)

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: 'Xác nhận duyệt' })).toBeNull()
    expect(document.activeElement).toBe(approveBtn)

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: 'Chi tiết đề' })).toBeNull()
    expect(document.activeElement).toBe(openDrawer)

    // BareDialog (PROBE-5)
    const openBare = screen.getByRole('button', { name: 'Mở BareDialog' })
    openBare.focus()
    await user.click(openBare)
    const bareA = screen.getByRole('button', { name: 'Nút Bare A' })
    const bareB = screen.getByRole('button', { name: 'Nút Bare B' })
    expect(document.activeElement).toBe(bareA)
    await user.tab()
    expect(document.activeElement).toBe(bareB)
    await user.tab()
    expect(document.activeElement).toBe(bareA)
    await user.tab({ shift: true })
    expect(document.activeElement).toBe(bareB)
    await user.keyboard('{Escape}')
    expect(document.activeElement).toBe(openBare)

    // Dual FormDialog (PROBE-6)
    const openFirst = screen.getByRole('button', { name: 'Mở Form 1' })
    openFirst.focus()
    await user.click(openFirst)
    const openSecond = screen.getByRole('button', { name: 'Mở Form 2' })
    openSecond.focus()
    await user.click(openSecond)

    const secondDialog = screen.getByRole('dialog', { name: 'Hộp thoại 2' })
    expect(secondDialog.contains(document.activeElement)).toBe(true)
    await user.tab()
    expect(secondDialog.contains(document.activeElement)).toBe(true)
    await user.tab()
    expect(secondDialog.contains(document.activeElement)).toBe(true)

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: 'Hộp thoại 2' })).toBeNull()
    expect(document.activeElement).toBe(openSecond)

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: 'Hộp thoại 1' })).toBeNull()
    expect(document.activeElement).toBe(openFirst)
  })
})
