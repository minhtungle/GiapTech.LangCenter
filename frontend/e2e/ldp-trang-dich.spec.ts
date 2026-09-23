import { expect, test } from '@playwright/test'
import { dangNhap, taoTrungTam } from './tro-giup'

/**
 * FR-30 — trang đích công khai, luồng thật trên trình duyệt.
 *
 * Kiểm đúng thứ quan trọng nhất: **chưa xuất bản thì khách không xem được**, và xuất bản rồi
 * thì xem được mà KHÔNG cần đăng nhập.
 */
test('soạn nội dung, xuất bản, khách vãng lai xem được và gửi liên hệ', async ({
  page, request,
}) => {
  const tt = await taoTrungTam(request, 'ldp')
  await dangNhap(page, tt)

  // --- Trước khi xuất bản: khách KHÔNG xem được ---
  //
  // Mở trong context riêng để không mang theo phiên đăng nhập — đúng cảnh khách vãng lai.
  const khach = await page.context().browser()!.newContext()
  const trangKhach = await khach.newPage()
  await trangKhach.goto(`/t/${tt.maTrungTam}`)
  await expect(trangKhach.getByText('Không tìm thấy trang')).toBeVisible({ timeout: 15_000 })

  // --- Soạn nội dung ---
  await page.goto('/ldp/noi-dung')
  await expect(page.getByRole('heading', { name: 'Nội dung trang' })).toBeVisible()

  // Khối đầu là Hero — đặt tiêu đề nhận diện được
  const tieuDeHero = `Học tiếng Anh tại ${tt.tenTrungTam}`
  const oTieuDe = page.locator('input[id^="td-"]').first()
  await oTieuDe.fill(tieuDeHero)
  await page.getByRole('button', { name: 'Lưu' }).nth(1).click()

  // --- Xuất bản ---
  await page.getByRole('button', { name: 'Xuất bản', exact: true }).click()
  await expect(page.getByText('Đang hiển thị công khai')).toBeVisible({ timeout: 10_000 })

  // --- Sau khi xuất bản: khách xem được ---
  await trangKhach.goto(`/t/${tt.maTrungTam}`)
  await expect(trangKhach.getByRole('heading', { name: tieuDeHero })).toBeVisible({
    timeout: 15_000,
  })

  // --- Khách gửi form liên hệ ---
  await trangKhach.fill('input[placeholder="Họ và tên"]', 'Khách E2E')
  await trangKhach.fill('input[placeholder="Số điện thoại"]', '0987654321')
  await trangKhach.getByRole('button', { name: 'Gửi thông tin' }).click()
  await expect(trangKhach.getByText('Đã nhận thông tin của bạn')).toBeVisible({
    timeout: 10_000,
  })

  // --- Người phụ trách thấy liên hệ và chuyển sang CRM ---
  await page.goto('/ldp/lien-he')
  await expect(page.getByRole('cell', { name: 'Khách E2E' })).toBeVisible({ timeout: 10_000 })

  await page.getByRole('button', { name: 'Chuyển CRM' }).first().click()
  await expect(page.getByText('Đã chuyển CRM')).toBeVisible({ timeout: 10_000 })

  await khach.close()
})

test('gỡ xuất bản thì khách lại không xem được', async ({ page, request }) => {
  const tt = await taoTrungTam(request, 'ldp-go')
  await dangNhap(page, tt)

  await page.goto('/ldp/noi-dung')
  await page.getByRole('button', { name: 'Xuất bản', exact: true }).click()
  await expect(page.getByText('Đang hiển thị công khai')).toBeVisible({ timeout: 10_000 })

  const khach = await page.context().browser()!.newContext()
  const trangKhach = await khach.newPage()
  await trangKhach.goto(`/t/${tt.maTrungTam}`)
  await expect(trangKhach.getByText('Không tìm thấy trang')).toBeHidden()

  // Gỡ xuất bản — nội dung phải biến khỏi Internet ngay.
  await page.getByRole('button', { name: 'Gỡ xuất bản' }).click()
  await expect(page.getByText('Chưa xuất bản', { exact: false })).toBeVisible({ timeout: 10_000 })

  await trangKhach.goto(`/t/${tt.maTrungTam}`)
  await expect(trangKhach.getByText('Không tìm thấy trang')).toBeVisible({ timeout: 15_000 })

  await khach.close()
})
