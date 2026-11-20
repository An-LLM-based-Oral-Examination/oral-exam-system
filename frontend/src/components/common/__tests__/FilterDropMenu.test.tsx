import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { FilterDropMenu } from '@/components/common/FilterDropMenu'

describe('FilterDropMenu', () => {
  it('renders [role="option"] buttons with min-h-11 touch target and preserves keyboard navigation', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()

    render(
      <FilterDropMenu
        label="Vai trò"
        value=""
        onChange={onChange}
        options={[
          { value: '', label: 'Tất cả vai trò' },
          { value: 'Student', label: 'Sinh viên' },
          { value: 'Admin', label: 'Quản trị viên' },
        ]}
      />,
    )

    const trigger = screen.getByRole('button', { name: 'Vai trò' })
    await user.click(trigger)

    const listbox = await screen.findByRole('listbox', { name: 'Vai trò' })
    expect(listbox).toBeInTheDocument()

    const options = screen.getAllByRole('option')
    expect(options).toHaveLength(3)
    for (const option of options) {
      expect(option).toHaveClass('min-h-11')
    }

    await user.keyboard('{ArrowDown}')
    expect(options[0]).toHaveFocus()

    await user.keyboard('{ArrowDown}')
    expect(options[1]).toHaveFocus()

    await user.click(options[1])
    expect(onChange).toHaveBeenCalledWith('Student')
    expect(screen.queryByRole('listbox', { name: 'Vai trò' })).not.toBeInTheDocument()
  })
})
