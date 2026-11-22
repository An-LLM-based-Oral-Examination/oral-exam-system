import { MemoryRouter } from 'react-router-dom'
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { DepartmentHeadDashboardPage } from '../DepartmentHeadDashboardPage'

describe('DepartmentHeadDashboardPage', () => {
  it('removes dead /department-head/appeals link and exposes only valid DH routes', () => {
    render(
      <MemoryRouter>
        <DepartmentHeadDashboardPage />
      </MemoryRouter>,
    )

    const links = screen.getAllByRole('link').map((link) => link.getAttribute('href'))
    expect(links).not.toContain('/department-head/appeals')
    expect(links).toEqual(
      expect.arrayContaining([
        '/department-head/question-review',
        '/department-head/exam-structures',
        '/department-head/courses',
      ]),
    )
  })
})
