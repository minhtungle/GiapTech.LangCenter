import { expect, test } from '@playwright/test'
import { layTokenQuaApi, vaoHeThong } from './tro-giup'

/**
 * FR-22 — cây cơ cấu tổ chức.
 *
 * ## Vì sao cần E2E riêng cho màn này
 *
 * Màn này dùng `@headless-tree`, một thư viện **có state nội bộ** ngoài React: nó tự giữ
 * `expandedItems`, `focusedItem`, và cache cấu trúc cây. Mọi lỗi nghiêm trọng của màn đều đến
 * từ chỗ state đó lệch khỏi dữ liệu thật — mà lệch thì thư viện **ném lỗi và sập cả app**,
 * không chỉ sập cây.
 *
 * Người dùng báo ngày 16/09/2026: *"lỗi khi xóa cơ cấu thì màn hình bị trắng tinh không hiện
 * gì, phải reload lại"*. Nguyên nhân: xoá xong → cây tải lại → thư viện vẫn hỏi dữ liệu của id
 * vừa xoá → `dataLoader.getItem` trả `null` → `@headless-tree` 1.7 làm
 * `if (!data) throw` (`core/dist/index.js:1362`) → **màn trắng**.
 *
 * Trước đó màn này **không có E2E nào** — đó là lý do lỗi lọt. Test không kiểm "xoá được
 * không" (backend đã có test) mà kiểm **màn còn sống sau khi xoá**.
 */

/** Màn còn render, không trắng, không hàng trống lọt ra từ node giữ chỗ. */
async function manConSong(page: import('@playwright/test').Page, nhan: string) {
  const x = await page.evaluate(() => ({
    rootRong: !document.getElementById('root')?.innerHTML?.trim(),
    quaIt: document.body.innerText.trim().length < 60,
    hangTrong: [...document.querySelectorAll('[role=treeitem]')].some((e) => !e.textContent?.trim()),
  }))

  expect(x.rootRong, `${nhan}: MÀN TRẮNG — React đã sập`).toBeFalsy()
  expect(x.quaIt, `${nhan}: màn gần như rỗng`).toBeFalsy()
  expect(x.hangTrong, `${nhan}: có hàng trống — node giữ chỗ lọt ra khỏi vòng render`).toBeFalsy()
}

async function xoaPhong(page: import('@playwright/test').Page, ten: string) {
  const hang = page.locator('div').filter({ hasText: new RegExp(`^${ten}`) }).last()
  await hang.locator('button').last().click()
  await page.getByText('Xóa', { exact: true }).first().click()
  await page.getByRole('button', { name: 'Xóa' }).last().click()
  await page.waitForTimeout(1800)
}

test('Xoá phòng ban KHÔNG làm trắng màn hình', async ({ page, request }) => {
  const tt = await vaoHeThong(page, request, 'co-cau-xoa')

  const token = await layTokenQuaApi(page)
  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: than,
    })
    expect(res.ok(), `${duong} → ${res.status()} ${await res.text()}`).toBeTruthy()
    return res.json()
  }

  // Bắt mọi lỗi JS chưa xử lý — đây là tín hiệu thật của "màn trắng".
  const loiJs: string[] = []
  page.on('pageerror', (e) => loiJs.push(e.message))

  const cha = await api('/phong-ban', { ten: 'Khối đào tạo', thuTu: 1 })
  await api('/phong-ban', { ten: 'Bộ môn Anh', phongBanChaId: cha, thuTu: 0 })
  await api('/phong-ban', { ten: 'Bộ môn Đức', phongBanChaId: cha, thuTu: 1 })
  await api('/phong-ban', { ten: 'Phòng lẻ', thuTu: 2 })

  await page.goto('/hrm/co-cau')
  await expect(page.getByText('Bộ môn Anh')).toBeVisible({ timeout: 15000 })

  // --- CA CHÍNH: xoá phòng CON trong nhánh đang mở (ca làm sập app) ---
  await xoaPhong(page, 'Bộ môn Đức')
  await manConSong(page, 'sau khi xoá phòng con')
  await expect(page.getByText('Bộ môn Đức')).toHaveCount(0)
  // Phòng cha và phòng còn lại vẫn hiện — không xoá lây.
  await expect(page.getByText('Bộ môn Anh')).toBeVisible()
  await expect(page.getByText('Khối đào tạo')).toBeVisible()

  // --- Xoá phòng GỐC không có con ---
  await xoaPhong(page, 'Phòng lẻ')
  await manConSong(page, 'sau khi xoá phòng gốc')
  await expect(page.getByText('Phòng lẻ')).toHaveCount(0)

  // --- Xoá nốt phòng con rồi xoá phòng cha: cây rỗng dần tới hết ---
  await xoaPhong(page, 'Bộ môn Anh')
  await manConSong(page, 'sau khi xoá phòng con cuối')
  await xoaPhong(page, 'Khối đào tạo')
  await manConSong(page, 'sau khi xoá phòng cha')

  expect(loiJs, 'có lỗi JS chưa xử lý trong luồng xoá').toEqual([])
})

/**
 * Xoá bị CHẶN phải báo cho người dùng, và cũng không được làm sập màn.
 *
 * Hai điều kiện chặn ở backend: phòng còn cấp dưới, và phòng còn nhân sự. Bấm Xoá mà không có
 * gì xảy ra là trải nghiệm tệ ngang với màn trắng — người dùng không biết mình cần làm gì.
 */
test('Xoá bị chặn thì báo lỗi rõ ràng, màn vẫn sống', async ({ page, request }) => {
  const tt = await vaoHeThong(page, request, 'co-cau-chan')

  const token = await layTokenQuaApi(page)
  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: than,
    })
    expect(res.ok()).toBeTruthy()
    return res.json()
  }

  const loiJs: string[] = []
  page.on('pageerror', (e) => loiJs.push(e.message))

  const cha = await api('/phong-ban', { ten: 'Có cấp dưới', thuTu: 1 })
  await api('/phong-ban', { ten: 'Cấp dưới', phongBanChaId: cha, thuTu: 0 })

  await page.goto('/hrm/co-cau')
  await expect(page.getByText('Có cấp dưới')).toBeVisible({ timeout: 15000 })

  await xoaPhong(page, 'Có cấp dưới')

  // Báo lỗi ĐỌC ĐƯỢC, không phải mã lỗi thô cũng không phải im lặng.
  await expect(page.getByText(/còn phòng cấp dưới/i)).toBeVisible()
  await expect(page.getByText(/PHONG_BAN_/)).toHaveCount(0)   // không lộ mã lỗi thô

  await manConSong(page, 'sau khi xoá bị chặn')
  await expect(page.getByText('Có cấp dưới')).toBeVisible()   // không bị xoá

  expect(loiJs).toEqual([])
})
