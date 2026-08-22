import { request } from '@playwright/test'

/**
 * globalTeardown — xoá các CLB do test E2E sinh ra.
 *
 * Mỗi test tự tạo CLB riêng (xem `tro-giup.ts`: dùng chung thì test này sửa dữ liệu test kia),
 * nên mỗi lần chạy cả bộ để lại ~44 CLB. Chúng **hiện lên trang Cộng đồng của mọi người**, và
 * sau vài lần chạy thì trang đó không còn dùng được để test tay — đo thật 20/08: **242 CLB rác
 * trên 249**, toàn bộ trang đầu là rác, 7 CLB mẫu bị đẩy xuống trang sau.
 *
 * Dọn tay đã làm một lần rồi tích lại ngay sau lần chạy kế tiếp, nên phải tự động.
 *
 * **Không làm cả bộ đỏ nếu dọn thất bại.** Teardown chạy sau khi mọi test đã xong; báo lỗi ở đây
 * chỉ biến "rác chưa dọn" thành "tưởng có test đỏ", che mất kết quả thật. Chỉ in cảnh báo.
 */
export default async function donRac() {
  const base = process.env.E2E_BASE_URL ?? 'http://localhost:8080'

  try {
    const ctx = await request.newContext({ baseURL: base, ignoreHTTPSErrors: true })
    const res = await ctx.post('/api/v1/du-lieu-mau/don-tenant-test')

    if (res.ok()) {
      const { daXoa, conLai } = await res.json()
      console.log(`\n[don-rac] đã xoá ${daXoa} CLB test, còn lại ${conLai} CLB.`)
    } else {
      // 404 = API chạy ở Production (endpoint đóng); 400 = có lời mời bắc sang CLB thật.
      console.warn(
        `\n[don-rac] KHÔNG dọn được (HTTP ${res.status()}). CLB test còn lại trong DB.`,
      )
    }

    await ctx.dispose()
  } catch (e) {
    console.warn(`\n[don-rac] KHÔNG dọn được: ${e instanceof Error ? e.message : e}`)
  }
}
