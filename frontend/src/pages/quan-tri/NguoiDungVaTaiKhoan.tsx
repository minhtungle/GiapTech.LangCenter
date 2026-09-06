import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import NguoiDung from './NguoiDung'
import TaiKhoan from './TaiKhoan'

/**
 * FR-03/FR-04 — hai mặt của một con người trong hệ thống.
 *
 * **Người dùng** là hồ sơ con người (họ tên, vai trò, thông tin đặc thù). **Tài khoản** chỉ là
 * cách họ đăng nhập. Tách đôi từ 07/09/2026 vì hai vòng đời khác nhau: người rời trung tâm thì
 * tài khoản bị vô hiệu hoá, nhưng tên họ vẫn phải hiện đúng trong bảng điểm danh và sổ học phí
 * của những năm trước.
 */
export default function NguoiDungVaTaiKhoan() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<'nguoi-dung' | 'tai-khoan'>('nguoi-dung')

  return (
    <div className="space-y-4">
      <div className="flex gap-1 rounded-lg border border-border p-1 sm:w-fit">
        {(['nguoi-dung', 'tai-khoan'] as const).map((x) => (
          <button
            key={x}
            type="button"
            onClick={() => setTab(x)}
            className={
              'flex-1 rounded-md px-4 py-1.5 text-sm font-medium transition-colors sm:flex-none ' +
              (tab === x
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-muted')
            }
          >
            {t(x === 'nguoi-dung' ? 'menu.nguoiDung' : 'menu.taiKhoan')}
          </button>
        ))}
      </div>

      {/* Không dùng `hidden` để giữ cả hai cùng mount: mỗi tab có bộ lọc và trang riêng, ẩn
          hiện sẽ giữ lại state cũ gây khó hiểu khi quay lại. */}
      {tab === 'nguoi-dung' ? <NguoiDung /> : <TaiKhoan />}
    </div>
  )
}
