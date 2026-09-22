import { useIsFetching, useIsMutating } from '@tanstack/react-query'
import { useEffect, useState } from 'react'

/**
 * **Thanh tiến trình khi trang đang xử lý** (22/09/2026 — yêu cầu chủ sản phẩm).
 *
 * Một vạch mảnh chạy ở **mép trên màn hình** mỗi khi có request TanStack Query đang bay.
 *
 * ## Vì sao thanh trên đỉnh, không phải spinner che màn
 *
 * Spinner toàn màn khoá người dùng lại và làm mọi thao tác nhanh trông như chậm. Vạch mép trên
 * (kiểu GitHub, YouTube) nói "hệ thống đang làm việc" mà **không chặn** người dùng đọc tiếp
 * hay bấm sang chỗ khác.
 *
 * Nó cũng **không thay** các trạng thái tải sẵn có trong từng màn (bảng rỗng, nút "Đang
 * lưu…"): những thứ đó nói *cái gì* đang tải, còn vạch này chỉ nói *có* đang tải.
 *
 * ## Vì sao có độ TRỄ trước khi hiện
 *
 * Phần lớn request ở LAN xong trong dưới 200ms. Hiện ngay thì người dùng thấy một vệt nhấp
 * nháy ở mọi thao tác — gây khó chịu hơn là trấn an. Chỉ hiện khi request thật sự lâu.
 *
 * ## Vì sao giữ lại một chút sau khi xong
 *
 * Tải xong mà tắt ngay thì vạch biến mất lúc mới chạy được nửa đường, trông như bị hỏng. Chạy
 * nốt tới 100% rồi mới mờ đi.
 */

/** Chờ bao lâu rồi mới hiện — dưới ngưỡng này thì thao tác coi như tức thì. */
const TRE_TRUOC_KHI_HIEN = 250

/** Giữ lại sau khi xong, để vạch chạy nốt tới 100%. */
const GIU_SAU_KHI_XONG = 400

export function DangXuLy() {
  // `useIsFetching` đếm query ĐỌC, `useIsMutating` đếm lệnh GHI. Thiếu cái thứ hai thì lưu
  // biểu mẫu — thao tác chờ lâu nhất và đáng báo nhất — lại không có tín hiệu nào.
  const soQuery = useIsFetching()
  const soMutation = useIsMutating()
  const dangChay = soQuery + soMutation > 0

  const [hien, setHien] = useState(false)

  useEffect(() => {
    if (dangChay) {
      const hen = setTimeout(() => setHien(true), TRE_TRUOC_KHI_HIEN)
      return () => clearTimeout(hen)
    }

    const hen = setTimeout(() => setHien(false), GIU_SAU_KHI_XONG)
    return () => clearTimeout(hen)
  }, [dangChay])

  if (!hien) return null

  return (
    <div
      // `aria-hidden`: đây là trang trí. Người dùng trình đọc màn hình nhận thông báo từ chính
      // nội dung đang đổi, thêm một vùng "đang tải" chỉ làm ồn.
      aria-hidden
      className="pointer-events-none fixed inset-x-0 top-0 z-[100] h-0.5 overflow-hidden bg-transparent"
    >
      <div className="h-full w-full origin-left animate-[chay-ngang_1.4s_ease-in-out_infinite] bg-[hsl(var(--primary))]" />
    </div>
  )
}
