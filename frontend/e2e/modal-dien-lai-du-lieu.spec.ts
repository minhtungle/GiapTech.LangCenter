import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Modal sửa phải điền dữ liệu của **đúng dòng đang mở** (11/10/2026).
 *
 * ## Lỗi nó canh
 *
 * `<Modal>` dựng bằng `<dialog>` nên **không bị unmount khi đóng** — chỉ đổi thuộc tính
 * `open`. Mà React chỉ đọc `defaultValue` ở lần dựng ĐẦU TIÊN, nên mọi ô uncontrolled giữ
 * nguyên giá trị của lần mở trước: mở sửa khoá A rồi mở sửa khoá B thì giá tiền, số buổi…
 * vẫn là của A.
 *
 * Chủ sản phẩm báo 11/10/2026: danh sách ghi 7.275.000đ / 15 buổi, modal hiện 0đ / 40 buổi.
 * Đếm ra **15+ màn, 70+ ô `defaultValue`** cùng dính lỗi này.
 *
 * ## Vì sao phải là E2E
 *
 * `tsc`, `npm run build` và vitest đều XANH với lỗi này: JSX hợp lệ, kiểu đúng, không có
 * phép tính nào sai. Nó chỉ lộ ra khi thật sự mở modal hai lần trong một trình duyệt.
 *
 * Sửa bằng `key` đổi theo số lần mở trong chính `Modal` — một chỗ, mọi modal hưởng. Test này
 * canh màn Khoá học làm đại diện; sửa hỏng `Modal` là nó đỏ.
 */
test('mở sửa dòng khác không dùng lại dữ liệu dòng trước', async ({ page, request }) => {
  await vaoHeThong(page, request, 'modal-dien-lai')

  await page.goto('/crm/khoa-hoc')

  // Hai khoá học khác nhau RÕ RỆT ở cả giá lẫn số buổi — giống nhau thì test không phân biệt
  // được "điền đúng" với "giữ nguyên của lần trước".
  const khoa = [
    { ten: 'Khoá Alpha', gia: '1000000', buoi: '10' },
    { ten: 'Khoá Beta', gia: '2000000', buoi: '20' },
  ]

  for (const k of khoa) {
    await page.getByRole('button', { name: /thêm khoá/i }).click()
    await page.fill('#ten', k.ten)
    await page.fill('#giaTien', k.gia)
    await page.fill('#soBuoi', k.buoi)
    await page.getByRole('button', { name: /^lưu$/i }).last().click()
    await page.getByRole('button', { name: /đồng ý/i }).click()
    await expect(page.getByRole('cell', { name: k.ten })).toBeVisible({ timeout: 10_000 })
  }

  // Mở sửa LẦN LƯỢT hai khoá; lần thứ hai là lúc lỗi cũ lộ ra.
  for (const k of khoa) {
    await page.getByRole('row', { name: new RegExp(k.ten) }).getByRole('button').first().click()
    await page.getByText(/^sửa$/i).first().click()

    await expect(page.locator('#ten')).toHaveValue(k.ten)
    await expect(page.locator('#giaTien')).toHaveValue(k.gia)
    await expect(page.locator('#soBuoi')).toHaveValue(k.buoi)

    await page.getByRole('button', { name: /h[uủ][yỷ]/i }).last().click()
  }
})
