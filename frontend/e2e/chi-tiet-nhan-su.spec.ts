import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * View chi tiết hồ sơ nhân sự chia hai tab: **Thông tin chung** và **Tệp hồ sơ** (10/09/2026).
 *
 * Điều đáng canh không phải "tab bấm được" mà là **hai khối loại trừ nhau**: trước khi chia tab
 * cả hai cùng hiện, nên một thay đổi làm mất điều kiện `tab === 'tep'` sẽ trả UI về đúng trạng
 * thái cũ mà không có gì đỏ — `npm run build` vẫn xanh vì đó là JSX hợp lệ.
 *
 * Kiểm cả `?tab=` round-trip qua F5: mã tab nằm trong URL để gửi link được, mà nếu chỉ giữ
 * trong `useState` thì F5 sẽ âm thầm quay về tab đầu.
 */

/**
 * Xác nhận hộp thoại "Đặt tên tệp" — bước MỚI từ 16/09/2026.
 *
 * Trước đó chọn tệp là tải lên luôn. Nay có hộp thoại đặt tên ở giữa (ô nhập đã gợi ý sẵn tên
 * tệp), nên mọi bước tải lên trong bộ test này phải bấm Lưu. Giữ nguyên tên gợi ý để các assert
 * cũ (`hop-dong.pdf`…) vẫn nói đúng chuyện.
 */
async function xacNhanDatTen(page: import('@playwright/test').Page) {
  await expect(page.getByText('Đặt tên tệp')).toBeVisible({ timeout: 10_000 })
  await page.getByRole('button', { name: 'Lưu' }).click()
  // `toBeHidden`, KHÔNG `toHaveCount(0)`: `<Modal>` dựng bằng `<dialog>` nên nó luôn nằm trong
  // DOM, chỉ đóng lại — cùng lý do khiến khối lỗi từng hiện ở hai chỗ.
  await expect(page.getByText('Đặt tên tệp')).toBeHidden({ timeout: 10_000 })
}

test.describe('Chi tiết hồ sơ nhân sự', () => {
  test('hai tab loại trừ nhau, ?tab= sống qua F5, badge đếm tệp', async ({ page, request }) => {
    await vaoHeThong(page, request, 'tab-nhan-su')

    // Tạo một nhân sự để có hàng mở ra xem.
    await page.goto('/hrm/nhan-su')
    await page.waitForTimeout(1200)
    await page.getByRole('button', { name: /Thêm|Tạo/ }).first().click()
    await page.waitForTimeout(600)
    await page.fill('#hoTen', 'Nguyễn Văn Kiểm Tab')
    await page.locator('button[type=submit]').last().click()
    await page.waitForTimeout(600)
    await page.getByRole('button', { name: 'Đồng ý' }).click()
    await page.waitForTimeout(2000)

    await page.getByText('Nguyễn Văn Kiểm Tab').first().click()
    await page.waitForURL(/\/hrm\/nhan-su\/[0-9a-f-]{36}/, { timeout: 10_000 })
    await page.waitForTimeout(1000)

    // --- Tab mặc định: thông tin chung. Khối tệp phải ẨN, không chỉ là "không có trong URL".
    //
    // Dùng `isVisible()` chứ không `count()`: khối ẩn bằng điều kiện JSX thì không có trong DOM,
    // nhưng `count()` từng cho kết quả sai ở chỗ khác (dialog đóng vẫn giữ con trong DOM) nên
    // giữ một khuôn duy nhất cho mọi phép kiểm "có thấy không".
    expect(new URL(page.url()).searchParams.get('tab')).toBe(null)
    await expect(page.getByText('CCCD / CMND')).toBeVisible()
    expect(await page.getByRole('heading', { name: 'Tệp hồ sơ' }).isVisible()).toBe(false)

    // --- Sang tab tệp: mã tab vào URL, khối thông tin ẩn đi.
    await page.getByRole('button', { name: 'Tệp hồ sơ' }).click()
    await page.waitForTimeout(600)
    expect(new URL(page.url()).searchParams.get('tab')).toBe('tep')
    await expect(page.getByRole('heading', { name: 'Tệp hồ sơ' })).toBeVisible()
    expect(await page.getByText('CCCD / CMND').isVisible()).toBe(false)

    // --- Tải một tệp lên: badge đếm hiện trên NHÃN TAB để biết có gì bên đó mà không cần bấm.
    await page.setInputFiles('input[type=file]', {
      name: 'hop-dong.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 test'),
    })
    await xacNhanDatTen(page)
    await page.waitForTimeout(2500)
    await expect(page.getByText('hop-dong.pdf')).toBeVisible()
    await expect(page.getByRole('button', { name: /Tệp hồ sơ\s*1/ })).toBeVisible()

    // --- F5 ở tab tệp: vẫn ở tab tệp (đây là lý do mã tab nằm trong URL).
    await page.reload()
    await page.waitForTimeout(1500)
    expect(new URL(page.url()).searchParams.get('tab')).toBe('tep')
    await expect(page.getByRole('heading', { name: 'Tệp hồ sơ' })).toBeVisible()

    // --- Về tab thông tin: `?tab=` biến mất hẳn thay vì thành `?tab=thong-tin`, để URL của
    // trạng thái mặc định là URL ngắn nhất.
    await page.getByRole('button', { name: 'Thông tin chung' }).click()
    await page.waitForTimeout(600)
    expect(new URL(page.url()).searchParams.get('tab')).toBe(null)
    expect(await page.getByRole('heading', { name: 'Tệp hồ sơ' }).isVisible()).toBe(false)
  })

  /**
   * Xem tệp online + giới hạn định dạng (10/09/2026).
   *
   * Chỗ dễ sai nhất là **iframe phải có nguồn thật**: endpoint cần header `Authorization` nên
   * `<iframe src="/api/...">` sẽ 401 và hiện khung trắng — phải tải blob rồi
   * `createObjectURL`. Test kiểm `src` bắt đầu bằng `blob:` chính là để canh điều đó.
   */
  test('xem PDF trong modal, chặn định dạng không cho phép', async ({ page, request }) => {
    await vaoHeThong(page, request, 'xem-tep')

    await page.goto('/hrm/nhan-su')
    await page.waitForTimeout(1200)
    await page.getByRole('button', { name: /Thêm|Tạo/ }).first().click()
    await page.waitForTimeout(600)
    await page.fill('#hoTen', 'Nguyễn Thị Xem Tệp')
    await page.locator('button[type=submit]').last().click()
    await page.waitForTimeout(600)
    await page.getByRole('button', { name: 'Đồng ý' }).click()
    await page.waitForTimeout(2000)

    await page.getByText('Nguyễn Thị Xem Tệp').first().click()
    await page.waitForURL(/\/hrm\/nhan-su\/[0-9a-f-]{36}/, { timeout: 10_000 })
    await page.getByRole('button', { name: 'Tệp hồ sơ' }).click()
    await page.waitForTimeout(600)

    // Giới hạn phải được NÓI TRƯỚC, không để người dùng chọn xong mới biết.
    await expect(page.getByText(/Chỉ nhận PDF, Word, Excel/)).toBeVisible()

    // --- Định dạng KHÔNG cho phép: ảnh (kho lưu trữ dùng chung vẫn nhận, hồ sơ nhân sự không).
    await page.setInputFiles('input[type=file]', {
      name: 'anh-the.png',
      mimeType: 'image/png',
      buffer: Buffer.from('\x89PNG\r\n\x1a\n fake'),
    })
    await page.waitForTimeout(1200)
    await expect(page.getByText(/chỉ nhận PDF, Word hoặc Excel/i)).toBeVisible()
    // Và tệp KHÔNG được vào danh sách.
    expect(await page.getByText('anh-the.png').isVisible()).toBe(false)

    // --- PDF hợp lệ: vào danh sách, có nút Xem.
    await page.setInputFiles('input[type=file]', {
      name: 'hop-dong-lao-dong.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>'),
    })
    await xacNhanDatTen(page)
    await page.waitForTimeout(2500)
    await expect(page.getByText('hop-dong-lao-dong.pdf')).toBeVisible()

    // --- Mở modal xem: iframe phải trỏ tới blob: (đã tải kèm token), không phải URL API.
    await page.getByRole('button', { name: 'Xem' }).first().click()
    await page.waitForTimeout(1500)

    const iframe = page.locator('iframe')
    await expect(iframe).toBeVisible()

    // GIỚI HẠN CỦA TEST NÀY: chỉ kiểm được `src` là `blob:` (tức đã tải kèm token và không
    // trỏ thẳng vào API). **Không** kiểm được PDF có render ra chữ hay không — Chromium
    // headless không có plugin đọc PDF, mọi `<iframe src="blob:...pdf">` đều báo
    // `net::ERR_ABORTED` và ra khung trắng, kể cả HTML thuần không liên quan tới app (đã
    // thăm dò riêng 10/09). Việc render thật đã xác nhận bằng tay ở chế độ `--headed`.
    expect(await iframe.getAttribute('src')).toMatch(/^blob:/)

    // Tiêu đề modal là tên gốc, không phải khoá GUID.
    await expect(
      page.getByRole('heading', { name: 'hop-dong-lao-dong.pdf' }),
    ).toBeVisible()

    // --- Đóng modal: iframe biến mất (và blob được thu hồi).
    await page.getByRole('button', { name: 'Đóng' }).click()
    await page.waitForTimeout(600)
    expect(await iframe.isVisible()).toBe(false)
  })

  /**
   * Word/Excel **không** có nút Xem: trình duyệt không render được, mở modal ra sẽ trắng trơn.
   * Chỉ có nút Tải về.
   */
  test('Word không có nút Xem, chỉ có Tải về', async ({ page, request }) => {
    await vaoHeThong(page, request, 'tep-word')

    await page.goto('/hrm/nhan-su')
    await page.waitForTimeout(1200)
    await page.getByRole('button', { name: /Thêm|Tạo/ }).first().click()
    await page.waitForTimeout(600)
    await page.fill('#hoTen', 'Trần Văn Word')
    await page.locator('button[type=submit]').last().click()
    await page.waitForTimeout(600)
    await page.getByRole('button', { name: 'Đồng ý' }).click()
    await page.waitForTimeout(2000)

    await page.getByText('Trần Văn Word').first().click()
    await page.waitForURL(/\/hrm\/nhan-su\/[0-9a-f-]{36}/, { timeout: 10_000 })
    await page.getByRole('button', { name: 'Tệp hồ sơ' }).click()
    await page.waitForTimeout(600)

    await page.setInputFiles('input[type=file]', {
      name: 'ly-lich.docx',
      mimeType:
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      buffer: Buffer.from('PK fake docx'),
    })
    await xacNhanDatTen(page)
    await page.waitForTimeout(2500)

    await expect(page.getByText('ly-lich.docx')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Tải về' })).toBeVisible()
    expect(await page.getByRole('button', { name: 'Xem' }).isVisible()).toBe(false)
  })
})
