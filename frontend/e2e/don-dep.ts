import { request } from '@playwright/test'

/**
 * `globalTeardown` — dọn trung tâm rác sau mỗi lượt chạy E2E (nợ N11).
 *
 * ## Vì sao cần
 *
 * Mỗi test tự tạo một trung tâm riêng (xem `tro-giup.ts` — cố ý, để test không giẫm lên
 * nhau). Hệ quả: mỗi lượt chạy cả bộ để lại ~40 trung tâm `E2E ...` trong DB dev.
 *
 * Đã phải dọn tay **năm lần** (12/09: 212→1, 17/09: 662→1, và ba lần ngày 23/09), và repo có
 * tới 5 script `.sql` dọn rác. Dọn tay không phải cách chữa — đây mới là.
 *
 * ## Thất bại ở đây KHÔNG được làm cả bộ test đỏ
 *
 * Chốt quan trọng nhất của file này, đã ghi sẵn trong `playwright.config.ts` từ trước:
 * "rác chưa dọn" mà bị hiểu thành "có test đỏ" thì lần sau người ta sẽ tắt teardown đi.
 * Nên mọi lỗi ở đây chỉ **in cảnh báo**.
 *
 * ## Cần gì để chạy
 *
 * - `CHO_DON_E2E=true` ở API (mặc định TẮT — endpoint xoá hàng loạt)
 * - `CHU_HE_THONG_MAT_KHAU` để seeder tạo tài khoản chủ
 *
 * Thiếu một trong hai thì teardown in một dòng nói rõ thiếu gì rồi bỏ qua.
 */
export default async function donDep() {
  const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080'
  const matKhauChu = process.env.CHU_HE_THONG_MAT_KHAU
  const usernameChu = process.env.CHU_HE_THONG_USERNAME ?? 'chu'

  if (!matKhauChu) {
    console.warn(
      '[teardown] Bỏ qua dọn tenant E2E: chưa đặt CHU_HE_THONG_MAT_KHAU. ' +
        'Trung tâm rác vẫn còn trong DB — xem nợ N11.',
    )
    return
  }

  const ctx = await request.newContext({ baseURL })

  try {
    const dn = await ctx.post('/api/v1/chu-he-thong/dang-nhap', {
      data: { username: usernameChu, matKhau: matKhauChu },
    })

    if (!dn.ok()) {
      console.warn(
        `[teardown] Không đăng nhập được tài khoản chủ (${dn.status()}). ` +
          'Trung tâm rác vẫn còn — kiểm CHU_HE_THONG_MAT_KHAU và seeder.',
      )
      return
    }

    const { accessToken } = await dn.json()

    const res = await ctx.post('/api/v1/chu-he-thong/don-tenant-e2e', {
      headers: { Authorization: `Bearer ${accessToken}` },
    })

    if (res.status() === 404) {
      console.warn(
        '[teardown] Endpoint dọn đang tắt — đặt CHO_DON_E2E=true khi chạy API. ' +
          'Trung tâm rác vẫn còn.',
      )
      return
    }

    if (!res.ok()) {
      console.warn(`[teardown] Dọn thất bại (${res.status()}). Trung tâm rác vẫn còn.`)
      return
    }

    const { daXoa } = await res.json()
    console.log(`[teardown] Đã dọn ${daXoa} trung tâm E2E.`)
  } catch (e) {
    // Nuốt mọi lỗi: teardown hỏng không được làm cả bộ test đỏ — xem chú thích đầu file.
    console.warn(`[teardown] Lỗi khi dọn tenant E2E, bỏ qua: ${e}`)
  } finally {
    await ctx.dispose()
  }
}
