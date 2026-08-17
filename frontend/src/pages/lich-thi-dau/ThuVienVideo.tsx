import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { ExternalLink, Search, Video } from 'lucide-react'
import { api, trangRong, type KetQuaTrang } from '@/lib/api'
import { Badge, Button, Input, TrangTrong } from '@/components/ui'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

interface VideoThuVienDto {
  id: string
  tranDauId: string
  ten: string
  url: string
  moTa: string | null
  thoiGianTran: string
  tenDoiThu: string | null
  tySoNha: number | null
  tySoKhach: number | null
}
interface DoiThuNgan {
  id: string
  tenDoi: string
}

/**
 * Thư viện video — xem tổng hợp video của mọi trận.
 *
 * **Không có bảng riêng**: đọc thẳng từ `VIDEO_TRAN`. Giữ bản sao sẽ tạo hai nguồn sự thật —
 * sửa tên video ở màn trận mà thư viện không đổi theo, rồi không biết bên nào đúng. Đọc thẳng
 * thì đồng bộ là hệ quả tự nhiên, không phải việc phải nhớ làm.
 *
 * Muốn sửa/xoá thì vào trận tương ứng — mỗi video thuộc về đúng một trận, sửa ở hai nơi chỉ
 * thêm đường cho dữ liệu lệch.
 */
export default function ThuVienVideo() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [timKiem, setTimKiem] = useState('')
  const [doiThuId, setDoiThuId] = useState<string | null>(null)
  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)

  const { data: doiThus } = useQuery({
    queryKey: ['doi-thu'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<DoiThuNgan>>('/doi-thu', { params: { soDong: 200 } })).data.duLieu,
  })

  const { data: ketQua, isLoading } = useQuery({
    queryKey: ['thu-vien-video', timKiem, doiThuId, trang, soDong],
    queryFn: async () =>
      (
        await api.get<KetQuaTrang<VideoThuVienDto>>('/thu-vien-video', {
          params: {
            timKiem: timKiem || undefined,
            doiThuId: doiThuId ?? undefined,
            trang,
            soDong,
          },
        })
      ).data,
  })

  const kq = ketQua ?? trangRong<VideoThuVienDto>()

  const ngay = (iso: string) =>
    new Date(iso).toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative max-w-xs flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            className="pl-8"
            placeholder={t('video.timKiem')}
            value={timKiem}
            onChange={(e) => {
              setTimKiem(e.target.value)
              setTrang(1)
            }}
          />
        </div>
        <div className="w-56">
          <SelectTimKiem
            id="locDoiThu"
            luaChon={(doiThus ?? []).map((d) => ({ giaTri: d.id, nhan: d.tenDoi }))}
            giaTri={doiThuId}
            onDoi={(v) => {
              setDoiThuId(v)
              setTrang(1)
            }}
            placeholder={t('tranDau.moiDoiThu')}
            placeholderTimKiem={t('tranDau.timDoiThu')}
          />
        </div>
      </div>

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !kq.duLieu.length ? (
        <TrangTrong
          thongDiep={timKiem || doiThuId ? t('chung.khongCoDuLieu') : t('video.thuVienChuaCo')}
        />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {kq.duLieu.map((v) => (
            <div key={v.id} className="flex flex-col gap-2 rounded-lg border border-border p-3">
              <div className="flex items-start justify-between gap-2">
                <p className="flex min-w-0 items-center gap-1.5 font-medium">
                  <Video className="h-4 w-4 shrink-0 text-primary" />
                  <span className="truncate">{v.ten}</span>
                </p>
                {/* rel="noreferrer": link do người dùng nhập, không để trang đích đọc opener. */}
                <a
                  href={v.url}
                  target="_blank"
                  rel="noreferrer"
                  title={t('video.mo')}
                  className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md border border-border hover:bg-muted"
                >
                  <ExternalLink className="h-3.5 w-3.5" />
                </a>
              </div>

              {v.moTa && <p className="text-sm text-muted-foreground">{v.moTa}</p>}

              <div className="mt-auto flex flex-wrap items-center gap-2 pt-1 text-xs">
                <span className="text-muted-foreground">{ngay(v.thoiGianTran)}</span>
                {v.tenDoiThu && <Badge variant="muted">{v.tenDoiThu}</Badge>}
                {(v.tySoNha !== null || v.tySoKhach !== null) && (
                  <span className="font-mono font-semibold">
                    {v.tySoNha ?? 0} – {v.tySoKhach ?? 0}
                  </span>
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  className="ml-auto"
                  onClick={() => navigate(`/lich-thi-dau/${v.tranDauId}`)}
                >
                  {t('video.xemTran')}
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {kq.duLieu.length > 0 && (
        <PhanTrang
          trang={kq.trang}
          soDong={kq.soDong}
          tongSoDong={kq.tongSoDong}
          tongSoTrang={kq.tongSoTrang}
          onDoiTrang={setTrang}
          onDoiSoDong={(n) => {
            setSoDong(n)
            setTrang(1)
          }}
        />
      )}
    </div>
  )
}
