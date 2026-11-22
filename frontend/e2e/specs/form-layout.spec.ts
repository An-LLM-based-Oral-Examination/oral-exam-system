import { test, expect } from '../fixtures/test'
import { isLiveApiMode } from '../fixtures/env'
import type { Locator } from '@playwright/test'

async function expectNoHorizontalOverflow(dialog: Locator) {
  await expect
    .poll(() =>
      dialog.evaluate((root) => {
        const ancestors: Element[] = []
        for (const form of root.querySelectorAll('form')) {
          let parent = form.parentElement
          while (parent && parent !== root) {
            ancestors.push(parent)
            parent = parent.parentElement
          }
        }
        const containers = [
          root,
          ...ancestors,
          ...root.querySelectorAll('form, section, fieldset, label, details, summary, textarea'),
        ]
        return containers
          .filter(
            (element) => element.clientWidth > 0 && element.scrollWidth > element.clientWidth + 2,
          )
          .map((element) => element.tagName)
      }),
    )
    .toEqual([])
}

for (const viewport of [
  { width: 1280, height: 900 },
  { width: 390, height: 844 },
]) {
  test(`question form wraps long values without horizontal scrolling at ${viewport.width}px`, async ({
    page,
    authSession,
  }) => {
    test.skip(isLiveApiMode(), 'Layout regression uses the mocked question authoring workflow.')
    await page.setViewportSize(viewport)
    await authSession.seedSession('Instructor')
    await page.goto('/lecturer/question-bank')
    await page.getByRole('button', { name: 'Tạo câu hỏi', exact: true }).click()
    await page.getByRole('button', { name: /Soạn câu hỏi thủ công/ }).click()
    const dialog = page.getByRole('dialog', { name: 'Soạn câu hỏi thủ công', exact: true })
    const title = dialog.getByPlaceholder('Tên ngắn gọn giúp nhận diện câu hỏi')
    const initialWidth = await title.evaluate((element) => element.getBoundingClientRect().width)
    await title.fill('a'.repeat(300))
    await dialog
      .getByPlaceholder('Viết đầy đủ yêu cầu mà sinh viên cần trình bày...')
      .fill('a'.repeat(2000))
    await dialog
      .getByPlaceholder('Câu trả lời chuẩn mực dùng làm mốc chấm...')
      .fill('b'.repeat(2000))
    await expectNoHorizontalOverflow(dialog)
    expect(await title.evaluate((element) => element.getBoundingClientRect().width)).toBeCloseTo(
      initialWidth,
      0,
    )

    await dialog.getByRole('button', { name: 'Chọn CLO cho câu hỏi', exact: true }).click()
    await page.getByRole('option', { name: /CLO1/ }).click()
    await dialog.getByRole('button', { name: 'Tiếp tục', exact: true }).click()
    await dialog.getByPlaceholder('Ví dụ: Rubric đánh giá kiến trúc').fill('c'.repeat(300))
    const criterion = dialog.locator('details').filter({ hasText: 'Tiêu chí 1' }).first()
    await criterion.locator('summary').click()
    await criterion.getByPlaceholder('Tên tiêu chí đánh giá').fill('d'.repeat(1000))
    await criterion
      .getByPlaceholder('Mô tả biểu hiện đạt điểm của tiêu chí...')
      .fill('e'.repeat(2000))
    await expectNoHorizontalOverflow(dialog)
    await dialog.getByRole('button', { name: 'Đóng', exact: true }).first().click()
  })
}
