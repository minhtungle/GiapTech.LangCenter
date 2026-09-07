import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckCircle2, Star } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Badge, Button, CanhBaoLoi, Card, CardContent, Label, Textarea, TrangTrong } from '@/components/ui'
import { useXacNhan } from '@/lib/xacNhan'

interface NhanXetDto {
  id: string
  hocVienId: string
  hoTen: string
  noiDung: string
  mucHaiLong: number | null
  thoiDiem: string
  /** true = nhận xét của chính người đang xem — backend tự xác định từ token. */
  cuaToi: boolean
}

interface NhanXetGvDto {
  hoTen: string
  nhanXet: string | null
}

const MUC = [1, 2, 3, 4, 5]

/**
 * Nhận xét quanh một buổi học — HAI chiều, cố ý gộp vào một tab.
 *
 * - Học viên nhận xét về BUỔI (bảng `NHAN_XET_BUOI_HOC`, mỗi người một bản, gửi lại = sửa).
 * - Giáo viên nhận xét về TỪNG HỌC VIÊN (cột `DIEM_DANH.nhan_xet`) — chỉ ĐỌC ở đây, ghi ở
 *   tab Điểm danh nơi đã có sẵn một dòng cho mỗi học viên. Ghi ở hai nơi thì hai form cùng
 *   sửa một cột và người dùng không biết bản nào thắng.
 *
 * Ai thấy nhận xét của ai do BACKEND quyết (`LayNhanXetBuoiHocQuery` lọc theo quyền), không
 * do component này ẩn hiện — ẩn ở frontend thì gọi API trực tiếp vẫn đọc được.
 */
export function NhanXetBuoiHoc({
  buoiHocId,
  toiLaHocVien,
}: {
  buoiHocId: string
  /** Chỉ học viên đang học của lớp gửi được nhận xét về buổi — xem `BuoiHocDto.toiLaHocVien`. */
  toiLaHocVien: boolean
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { hoi, hop } = useXacNhan()
  const [noiDung, setNoiDung] = useState('')
  const [muc, setMuc] = useState<number | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daGui, setDaGui] = useState(false)

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['buoi-hoc', buoiHocId, 'nhan-xet'],
    queryFn: async () =>
      (await api.get<NhanXetDto[]>(`/buoi-hoc/${buoiHocId}/nhan-xet`)).data,
  })

  // Nhận xét của GV lấy từ chính bảng điểm danh — không có endpoint riêng, và cũng không nên
  // có: quyền đọc điểm danh đã đúng là quyền cần để đọc nhận xét kèm nó.
  const { data: diemDanh = [] } = useQuery({
    queryKey: ['buoi-hoc', buoiHocId, 'diem-danh'],
    queryFn: async () =>
      (await api.get<NhanXetGvDto[]>(`/buoi-hoc/${buoiHocId}/diem-danh`)).data,
  })

  // Dùng cờ `cuaToi` của backend chứ không suy từ "danh sách có 1 phần tử": giáo viên đọc
  // được mọi nhận xét, nếu lớp chỉ có một học viên đã gửi thì suy kiểu đó sẽ nạp nhận xét của
  // HỌC VIÊN vào form của GIÁO VIÊN, và bấm Gửi là ghi đè nhầm chủ.
  const cuaToi = ds.find((n) => n.cuaToi) ?? null

  useEffect(() => {
    if (cuaToi) {
      setNoiDung(cuaToi.noiDung)
      setMuc(cuaToi.mucHaiLong)
    }
  }, [cuaToi])

  const gui = useMutation({
    mutationFn: () =>
      api.post(`/buoi-hoc/${buoiHocId}/nhan-xet`, {
        noiDung: noiDung.trim(),
        mucHaiLong: muc,
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['buoi-hoc', buoiHocId, 'nhan-xet'] })
      setMaLoi(null)
      setDaGui(true)
      window.setTimeout(() => setDaGui(false), 2500)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setDaGui(false)
    },
  })

  const nhanXetGv = diemDanh.filter((d) => d.nhanXet)

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      {!toiLaHocVien ? (
        /* Giáo viên / trợ giảng / quản trị: KHÔNG hiện form. Trước đây hiện cho mọi người nên
           giáo viên nhập xong mới nhận `KHONG_THUOC_LOP_NAY` — vô lý với người dạy chính lớp.
           Nói luôn chỗ ghi nhận xét của họ thay vì chỉ ẩn đi. */
        <Card>
          <CardContent className="grid gap-2 pt-6">
            <h3 className="font-semibold">{t('nhanXetBuoi.nhanXetGiaoVien')}</h3>
            <p className="text-sm text-muted-foreground">{t('nhanXetBuoi.giaiThichChoGv')}</p>
          </CardContent>
        </Card>
      ) : (
      <Card>
        <CardContent className="grid gap-3 pt-6">
          <div className="flex items-center justify-between">
            <h3 className="font-semibold">{t('nhanXetBuoi.cuaToi')}</h3>
            {cuaToi && (
              <span className="text-xs text-muted-foreground">
                {new Date(cuaToi.thoiDiem).toLocaleString('vi-VN')}
              </span>
            )}
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="grid gap-1.5">
            <Label>{t('nhanXetBuoi.noiDung')}</Label>
            <Textarea
              rows={4}
              value={noiDung}
              onChange={(e) => setNoiDung(e.target.value)}
              placeholder={t('nhanXetBuoi.noiDungGoiY')}
            />
          </div>

          <div className="grid gap-1.5">
            <Label>{t('nhanXetBuoi.mucHaiLong')}</Label>
            <div className="flex items-center gap-1">
              {MUC.map((m) => (
                <button
                  key={m}
                  type="button"
                  aria-label={`${t('nhanXetBuoi.mucHaiLong')} ${m}`}
                  /* Bấm lại mức đang chọn = bỏ chọn. `MucHaiLong` nullable ở backend nên không
                     ép người dùng phải cho điểm mới gửi được nhận xét. */
                  onClick={() => setMuc((cu) => (cu === m ? null : m))}
                  className="rounded p-1 hover:bg-muted"
                >
                  <Star
                    className={
                      'h-5 w-5 ' +
                      (muc !== null && m <= muc
                        ? 'fill-status-win text-status-win'
                        : 'text-muted-foreground')
                    }
                  />
                </button>
              ))}
              <span className="ml-2 text-xs text-muted-foreground">
                {muc === null ? t('nhanXetBuoi.khongChon') : `${muc}/5`}
              </span>
            </div>
          </div>

          <div className="flex items-center justify-end gap-2">
            {daGui && (
              <span className="mr-auto flex items-center gap-1 text-sm text-status-win">
                <CheckCircle2 className="h-4 w-4" />
                {t('nhanXetBuoi.daGui')}
              </span>
            )}
            <Button
              disabled={gui.isPending || !noiDung.trim()}
              onClick={() =>
                hoi({
                  tieuDe: cuaToi ? t('nhanXetBuoi.capNhat') : t('nhanXetBuoi.gui'),
                  thongDiep: cuaToi
                    ? t('nhanXetBuoi.hoiCapNhat')
                    : t('nhanXetBuoi.hoiGui'),
                  onDongY: () => gui.mutate(),
                })
              }
            >
              {cuaToi ? t('nhanXetBuoi.capNhat') : t('nhanXetBuoi.gui')}
            </Button>
          </div>
        </CardContent>
      </Card>
      )}

      <div className="grid gap-4">
        <Card>
          <CardContent className="grid gap-2 pt-6">
            <h3 className="font-semibold">{t('nhanXetBuoi.nhanXetHocVien')}</h3>
            {isLoading ? (
              <TrangTrong thongDiep={t('chung.dangTai')} />
            ) : ds.length === 0 ? (
              <TrangTrong thongDiep={t('nhanXetBuoi.chuaCo')} />
            ) : (
              <ul className="grid gap-3">
                {ds.map((n) => (
                  <li key={n.id} className="rounded-md border border-border p-3">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{n.hoTen}</span>
                      {n.mucHaiLong !== null && (
                        <span className="flex items-center gap-0.5 text-xs text-muted-foreground">
                          <Star className="h-3 w-3 fill-status-win text-status-win" />
                          {n.mucHaiLong}/5
                        </span>
                      )}
                      {n.cuaToi && <Badge variant="accent">{t('nhanXetBuoi.cuaToi')}</Badge>}
                      <span className="ml-auto text-xs text-muted-foreground">
                        {new Date(n.thoiDiem).toLocaleString('vi-VN')}
                      </span>
                    </div>
                    <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                      {n.noiDung}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardContent className="grid gap-2 pt-6">
            <h3 className="font-semibold">{t('nhanXetBuoi.nhanXetGiaoVien')}</h3>
            <p className="text-xs text-muted-foreground">{t('nhanXetBuoi.ghiONhanXetGv')}</p>
            {nhanXetGv.length === 0 ? (
              <TrangTrong thongDiep={t('nhanXetBuoi.chuaCoNhanXetGv')} />
            ) : (
              <ul className="grid gap-3">
                {nhanXetGv.map((d) => (
                  <li key={d.hoTen} className="rounded-md border border-border p-3">
                    <span className="text-sm font-medium">{d.hoTen}</span>
                    <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                      {d.nhanXet}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      {hop}
    </div>
  )
}
