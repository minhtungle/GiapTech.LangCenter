import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { BarChart3, Briefcase, Network, Star, UserCog } from 'lucide-react'
import { useQuyen } from '@/lib/quyen'
import CoCauToChuc from './CoCauToChuc'
import NhanSu from './NhanSu'
import ChucVu from './ChucVu'
import TieuChiDanhGia from './TieuChiDanhGia'
import ThongKeNhanSu from './ThongKeNhanSu'

/**
 * HRM — một trang, ba tab (gộp 16/09/2026).
 *
 * ## Vì sao gộp
 *
 * Ba màn Cơ cấu tổ chức · Hồ sơ nhân sự · Chức vụ **nói về cùng một tập người**: `NGUOI_DUNG`
 * mang cả `phong_ban_id` lẫn `chuc_vu_id`. Nhưng giao diện tách rời nên một việc thường ngày
 * ("ai trong phòng Kinh doanh?", "đổi chức vụ người này") phải đi qua sidebar hai ba lần, và
 * sơ đồ tổ chức hiện sĩ số mà **không có đường nào xem đó là ai**.
 *
 * Gộp lại: sidebar còn một mục, và bấm sĩ số trên sơ đồ là sang tab Nhân sự đã lọc sẵn phòng
 * đó — xem `CoCauToChuc.tsx`.
 *
 * ## Tab lưu ở `?tab=`, không ở state
 *
 * Đúng quy ước dự án (nguyên tắc UI/UX mục 2): gửi link cho đồng nghiệp thì họ mở đúng tab, F5
 * không mất chỗ, nút Back hoạt động như mong đợi. `replace: true` để bấm qua lại giữa ba tab
 * không sinh ba mục lịch sử — Back phải quay về chỗ trước khi vào HRM.
 *
 * ## Ba màn con giữ nguyên là component riêng
 *
 * Không nhồi 800 dòng vào một file: mỗi tab vẫn là màn cũ, gọi nguyên vẹn. Nhờ vậy `/hrm/co-cau`
 * và `/hrm/nhan-su` vẫn chạy được (đường cũ, link đã lưu), và sửa một tab không đụng hai tab kia.
 */

type Tab = 'so-do' | 'nhan-su' | 'chuc-vu' | 'thong-ke' | 'tieu-chi'

const CAC_TAB: { ma: Tab; khoa: string; icon: typeof Network; can: string }[] = [
  { ma: 'so-do', khoa: 'menu.coCauToChuc', icon: Network, can: 'PhongBan' },
  { ma: 'nhan-su', khoa: 'menu.nhanSuNguoiDung', icon: UserCog, can: 'NhanSu' },
  { ma: 'chuc-vu', khoa: 'menu.chucVu', icon: Briefcase, can: 'NhanSu' },
  // FR-29 (16/09/2026) — gác bằng quyền RIÊNG, không phải `NhanSu`: bảng xếp hạng kèm doanh số
  // và điểm chất lượng của từng đồng nghiệp, khác hẳn việc xem một hồ sơ.
  { ma: 'thong-ke', khoa: 'chucNang.ThongKeNhanSu', icon: BarChart3, can: 'ThongKeNhanSu' },
  { ma: 'tieu-chi', khoa: 'chucNang.TieuChiDanhGia', icon: Star, can: 'TieuChiDanhGia' },
]

export default function Hrm() {
  const { t } = useTranslation()
  const [sp, setSp] = useSearchParams()
  const { coQuyen, dangTai } = useQuyen()

  // Trong lúc chưa biết quyền thì hiện đủ tab — ẩn rồi hiện lại làm nhấp nháy mỗi lần tải.
  const tabHienThi = dangTai ? CAC_TAB : CAC_TAB.filter((x) => coQuyen(x.can))

  const tabQuery = sp.get('tab') as Tab | null
  // Gõ thẳng `?tab=chuc-vu` khi không có quyền thì rơi về tab đầu, không phải tab trắng.
  const tab: Tab =
    tabQuery && tabHienThi.some((x) => x.ma === tabQuery)
      ? tabQuery
      : (tabHienThi[0]?.ma ?? 'so-do')

  const doiTab = (x: Tab) => {
    // Giữ `phongBanId` khi chuyển tab: người vừa bấm sĩ số một phòng rồi bấm sang tab khác và
    // quay lại thì bộ lọc vẫn còn — mất nó đi là họ phải chọn lại.
    const moi = new URLSearchParams(sp)
    moi.set('tab', x)
    setSp(moi, { replace: true })
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-1 rounded-lg border border-border p-1">
        {tabHienThi.map((x) => {
          const Icon = x.icon
          return (
            <button
              key={x.ma}
              type="button"
              onClick={() => doiTab(x.ma)}
              className={
                'inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium '
                + 'transition-colors '
                + (tab === x.ma
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-muted')
              }
            >
              <Icon className="h-4 w-4" />
              {t(x.khoa)}
            </button>
          )
        })}
      </div>

      {/*
        Render tab ĐANG XEM, không render cả ba rồi ẩn hai: mỗi màn tự gọi API của nó, nên
        render hết là ba lượt request mỗi lần vào trang, cho hai màn người dùng chưa mở.

        `key` theo tab: đổi tab là dựng lại màn con từ đầu. Cần vì `CoCauToChuc` giữ state cây
        trong thư viện ngoài React — giữ lại qua các lần ẩn/hiện sẽ lệch khỏi dữ liệu.
      */}
      <div key={tab}>
        {tab === 'so-do' && <CoCauToChuc />}
        {tab === 'nhan-su' && <NhanSu />}
        {tab === 'chuc-vu' && <ChucVu />}
        {tab === 'thong-ke' && <ThongKeNhanSu />}
        {tab === 'tieu-chi' && <TieuChiDanhGia />}
      </div>
    </div>
  )
}
