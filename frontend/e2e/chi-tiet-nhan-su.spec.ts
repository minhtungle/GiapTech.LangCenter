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
})
