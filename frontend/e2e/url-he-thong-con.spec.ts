import { test, expect } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Ba hệ thống con phải có URL cùng khuôn: `/hrm/...`, `/crm/...`, `/lms/...` (10/09/2026).
 *
 * Trước đó route LMS không có tiền tố (`/lop-hoc`, `/hoc-vien`…) nên `Layout.tsx` phải giữ một
 * **danh sách 5 đường hardcode** để suy ra đang ở hệ thống con nào — thêm màn LMS mới mà quên
 * khai thì sidebar hiện sai hệ thống, một lỗi im lặng không có lỗi biên dịch. Nay chỉ còn một
 * bảng tra tiền tố.
 *
 * Phần đáng canh nhất là **redirect giữ `:id` và query**: `<Navigate to>` tĩnh sẽ làm mất
 * chúng, và link tới đúng một lớp/buổi cụ thể sẽ lặng lẽ rơi về danh sách. Đã kiểm bằng đột
 * biến (bỏ `search`/`hash` → test đỏ đúng chỗ).
 */
test.describe('URL ba hệ thống con', () => {
// Test này đi qua ~12 lần điều hướng (5 đường cũ + 2 link có id/query + Back + menu) nên vượt
// ngân sách 30s mặc định. Nới timeout thay vì bỏ bước kiểm — mỗi bước canh một thứ khác nhau.
test.setTimeout(90_000)

test('URL LMS có tiền tố /lms, đường cũ redirect giữ id + query', async ({ page, request }) => {
  await vaoHeThong(page, request, 'url-lms')

  // --- Đường MỚI mở được, sidebar nhận đúng hệ thống LMS.
  await page.goto('/lms/lop-hoc')
  await page.waitForTimeout(1500)
  expect(new URL(page.url()).pathname).toBe('/lms/lop-hoc')
  await expect(page.locator('aside').getByText('LMS — Đào tạo')).toBeVisible()
  await page.screenshot({ path: '/tmp/u1-lms-lop-hoc.png', fullPage: true })

  // --- Đường CŨ → redirect sang /lms/... (bookmark cũ không chết).
  for (const [cu, moi] of [
    ['/lop-hoc', '/lms/lop-hoc'],
    ['/hoc-vien', '/lms/hoc-vien'],
    ['/tai-lieu', '/lms/tai-lieu'],
    ['/hoc-phi', '/lms/hoc-phi'],
    ['/lop-hoc/cho-xep-lop', '/lms/lop-hoc/cho-xep-lop'],
  ]) {
    await page.goto(cu)
    await page.waitForTimeout(1200)
    expect(new URL(page.url()).pathname, `${cu} phải chuyển sang ${moi}`).toBe(moi)
  }

  // --- Redirect phải GIỮ :id và query. Đây là chỗ `<Navigate to>` tĩnh sẽ làm mất.
  const id = '11111111-2222-3333-4444-555555555555'
  await page.goto(`/lop-hoc/${id}?tab=lich`)
  await page.waitForTimeout(1200)
  const u = new URL(page.url())
  expect(u.pathname).toBe(`/lms/lop-hoc/${id}`)
  expect(u.search).toBe('?tab=lich')

  await page.goto(`/buoi-hoc/${id}?tab=diem-danh`)
  await page.waitForTimeout(1200)
  const u2 = new URL(page.url())
  expect(u2.pathname).toBe(`/lms/buoi-hoc/${id}`)
  expect(u2.search).toBe('?tab=diem-danh')

  // --- Back sau redirect không kẹt vòng (nhờ `replace`).
  await page.goto('/lms/hoc-phi')
  await page.waitForTimeout(800)
  await page.goto('/lop-hoc')
  await page.waitForTimeout(1200)
  await page.goBack()
  await page.waitForTimeout(1200)
  expect(new URL(page.url()).pathname, 'Back phải về trang trước, không kẹt vòng redirect')
    .toBe('/lms/hoc-phi')

  // --- Điều hướng trong app: bấm menu LMS ra đường có tiền tố.
  await page.goto('/lms/lop-hoc')
  await page.waitForTimeout(1200)
  await page.locator('aside').getByText('Học phí', { exact: true }).click()
  await page.waitForTimeout(1200)
  expect(new URL(page.url()).pathname).toBe('/lms/hoc-phi')
  await page.screenshot({ path: '/tmp/u2-menu-lms.png', fullPage: true })
})
})
