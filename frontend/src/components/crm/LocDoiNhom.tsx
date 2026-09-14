import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, type KetQuaTrang } from '@/lib/api'
import { Label } from '@/components/ui'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

interface PhongBanNgan {
  id: string
  ten: string
}

interface NguoiDungNgan {
  id: string
  hoTen: string
  phongBanId?: string | null
  loaiNguoiDung?: string
}

/**
 * Bộ lọc **đội nhóm + nhân viên** dùng chung cho màn Khách hàng và Doanh thu.
 *
 * ## Mốc quy doanh số
 *
 * Cả hai màn lọc theo **NGƯỜI MANG KHÁCH VỀ** (`KHACH_HANG.created_by_id`), không phải người
 * nhập đơn — đúng mốc mà màn Thống kê CRM dùng. Nếu mỗi màn chọn một mốc thì cùng một đội sẽ
 * ra hai con số khác nhau ở hai màn, và không ai biết số nào đúng.
 *
 * Khách **tự đăng ký** không có ai phụ trách nên tự nhiên rơi ra khỏi mọi bộ lọc đội/nhân
 * viên — đúng ý: đơn của họ không tính vào doanh số cá nhân của ai.
 *
 * ## Vì sao dùng chung một component
 *
 * Hai màn cần đúng một cặp select, cùng nguồn dữ liệu, cùng ràng buộc (chọn đội thì danh sách
 * nhân viên thu hẹp theo). Viết hai bản là hai bản sẽ trôi khỏi nhau — đúng bài học của
 * `KhungNoiDung`.
 */
export function LocDoiNhom({
  phongBanId,
  nhanVienId,
  onDoiPhongBan,
  onDoiNhanVien,
}: {
  phongBanId: string | null
  nhanVienId: string | null
  onDoiPhongBan: (v: string | null) => void
  onDoiNhanVien: (v: string | null) => void
}) {
  const { t } = useTranslation()

  const { data: phongBan } = useQuery({
    queryKey: ['phong-ban-ngan'],
    queryFn: async () => (await api.get<PhongBanNgan[]>('/phong-ban')).data,
    staleTime: 5 * 60_000,
  })

  /**
   * Danh sách người để chọn "nhân viên".
   *
   * Lấy từ `/nguoi-dung` chứ không `/hoc-vien`: người mang khách về là nhân sự, và endpoint
   * này là đường duy nhất liệt kê đủ mọi loại người dùng.
   *
   * **Bỏ học viên, không khoá cứng một loại.** Tenant thật hiện có 6 nhân viên + 3 giáo viên
   * + 68 học viên; để cả 68 học viên vào select thì danh sách vô dụng. Nhưng lọc cứng
   * `loaiNguoiDung=NhanVien` cũng sai: giáo vụ (`GiaoVien`) hoàn toàn có thể tạo hồ sơ khách,
   * và khi đó tên họ phải chọn được — nếu không, đơn của họ thành không lọc được theo ai.
   */
  const { data: nguoiDung } = useQuery({
    queryKey: ['nguoi-dung-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NguoiDungNgan>>('/nguoi-dung', { params: { soDong: 200 } }))
        .data.duLieu,
    staleTime: 5 * 60_000,
  })

  /**
   * Chọn đội thì danh sách nhân viên thu hẹp theo đội đó.
   *
   * Không có phần này thì người dùng chọn "Kinh doanh Miền Bắc" rồi chọn một nhân viên thuộc
   * đội khác, và kết quả về rỗng mà không có lời giải thích nào trên màn.
   */
  const dsNhanVien = (nguoiDung ?? [])
    .filter((n) => n.loaiNguoiDung !== 'HocVien')
    .filter((n) => !phongBanId || n.phongBanId === phongBanId)
    .map((n) => ({ giaTri: n.id, nhan: n.hoTen }))

  return (
    <>
      <div className="w-48">
        <Label htmlFor="loc-doi">{t('crmLoc.doiNhom')}</Label>
        <SelectTimKiem
          id="loc-doi"
          luaChon={(phongBan ?? []).map((p) => ({ giaTri: p.id, nhan: p.ten }))}
          giaTri={phongBanId}
          onDoi={(v) => {
            onDoiPhongBan(v)
            // Đổi đội thì bỏ nhân viên đang chọn: giữ lại sẽ thành "đội A + người của đội B"
            // → kết quả rỗng, người dùng tưởng không có dữ liệu.
            if (nhanVienId) onDoiNhanVien(null)
          }}
          placeholder={t('chung.tatCa')}
        />
      </div>

      <div className="w-48">
        <Label htmlFor="loc-nv">{t('crmLoc.nhanVien')}</Label>
        <SelectTimKiem
          id="loc-nv"
          luaChon={dsNhanVien}
          giaTri={nhanVienId}
          onDoi={onDoiNhanVien}
          placeholder={t('chung.tatCa')}
        />
      </div>
    </>
  )
}
