import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, layMaLoi } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { Button, CanhBaoLoi, Card, CardContent, Input, Label, Textarea,
} from '@/components/ui'
import { ChonAnh } from '@/components/ui/ChonAnh'

interface ThietLapDto {
  id: string
  maTrungTam: string
  tenTrungTam: string
  tenVietTat: string | null
  logoUrl: string | null
  anhBiaUrl: string | null
  moTa: string | null
  diaChi: string | null
  lienHe: string | null
  soTaiKhoan: string | null
  tenNganHang: string | null
  chuTaiKhoan: string | null
  anhQrUrl: string | null
}

/**
 * FR-06 — thiết lập chung của trung tâm.
 *
 * `max-w-5xl` chứ không `max-w-2xl`: đo 21/08 trên màn 1440px thì form chỉ rộng 650px và bỏ
 * trống hoàn toàn nửa phải, nên trang cao 1420px với viewport 800px — phải cuộn hai lần cho
 * một form. Hai khối ảnh (Logo + Ảnh bìa) ĐÃ có `flex-wrap` để nằm cạnh nhau; chúng xếp dọc
 * chỉ vì container quá hẹp.
 *
 * Không để rộng vô hạn: dòng nhập dài quá 3-4 inch làm mắt mất điểm neo khi nhảy từ cuối dòng
 * này sang đầu dòng sau. `5xl` (1024px) là mức còn dễ đọc mà dùng được bề ngang.
 */
export default function ThietLap() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { capNhatTenTrungTam } = useAuth()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)
  const [qrNhap, setQrNhap] = useState<string | null | undefined>(undefined)
  /** null = chưa chạm, lấy giá trị server. Ảnh tải ngay nên state này chỉ để hiện lại. */
  const [logoNhap, setLogoNhap] = useState<string | null | undefined>(undefined)
  const [anhBiaNhap, setAnhBiaNhap] = useState<string | null | undefined>(undefined)

  const { data, isLoading } = useQuery({
    queryKey: ['thiet-lap'],
    queryFn: async () => (await api.get<ThietLapDto>('/thiet-lap')).data,
  })

  const luu = useMutation({
    mutationFn: async (form: Partial<ThietLapDto>) => {
      await api.put('/thiet-lap', form)
      return form
    },
    onSuccess: (form) => {
      void qc.invalidateQueries({ queryKey: ['thiet-lap'] })
      setLogoNhap(undefined)
      setAnhBiaNhap(undefined)
      // Tên trung tâm nằm trong JWT nên token đang cầm vẫn mang tên cũ tới lần làm mới kế
      // tiếp; không đồng bộ thì sidebar hiện tên cũ dù người dùng vừa đổi xong.
      if (form.tenTrungTam) capNhatTenTrungTam(form.tenTrungTam)
      setMaLoi(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  const logo = logoNhap === undefined ? (data?.logoUrl ?? null) : logoNhap
  const anhQr = qrNhap === undefined ? (data?.anhQrUrl ?? null) : qrNhap
  const anhBia = anhBiaNhap === undefined ? (data?.anhBiaUrl ?? null) : anhBiaNhap
  const setLogo = setLogoNhap
  const setAnhBia = setAnhBiaNhap

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    // Mọi trường lệnh cập nhật ghi đè đều gửi lại (quy tắc #1) — kể cả logo/ảnh bìa chưa có
    // ô trên form, nếu không mỗi lần lưu sẽ xoá chúng.
    luu.mutate({
      tenTrungTam: String(fd.get('tenTrungTam')),
      tenVietTat: (fd.get('tenVietTat') as string) || null,
      moTa: (fd.get('moTa') as string) || null,
      logoUrl: logo,
      anhBiaUrl: anhBia,
      // Gửi CHUỖI RỖNG (không phải null) khi người dùng xoá hết ô: backend hiểu null =
      // "client không gửi, giữ nguyên", còn '' = "chủ động xoá". Gửi null ở đây sẽ khiến ô đã
      // xoá lại hiện giá trị cũ sau khi tải lại — trông như không lưu được.
      diaChi: (fd.get('diaChi') as string) ?? '',
      lienHe: (fd.get('lienHe') as string) ?? '',
      // Thông tin chuyển khoản. Cùng quy ước: chuỗi rỗng = xoá, không gửi null.
      soTaiKhoan: (fd.get('soTaiKhoan') as string) ?? '',
      tenNganHang: (fd.get('tenNganHang') as string) ?? '',
      chuTaiKhoan: (fd.get('chuTaiKhoan') as string) ?? '',
      // Ảnh QR do ChonAnh tự tải lên và trả khoá — gửi lại để không bị xoá khi lưu form.
      anhQrUrl: anhQr,
    })
  }

  return (
    // `max-w-5xl` chứ không `max-w-2xl` — xem ghi chú ở đầu component.
    <Card className="max-w-5xl">
      <CardContent className="pt-5">
        <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="maTrungTam">{t('thietLap.maTrungTam')}</Label>
            <Input id="maTrungTam" value={data?.maTrungTam ?? ''} disabled />
            <p className="text-xs text-muted-foreground">{t('thietLap.maTrungTamKhongDoi')}</p>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenTrungTam">{t('thietLap.tenTrungTam')}</Label>
            <Input id="tenTrungTam" name="tenTrungTam" defaultValue={data?.tenTrungTam} required />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenVietTat">{t('thietLap.tenVietTat')}</Label>
            <Input id="tenVietTat" name="tenVietTat" defaultValue={data?.tenVietTat ?? ''} />
          </div>

          <div className="flex flex-wrap gap-6 sm:col-span-2">
            <div>
              <Label className="mb-1.5 block">{t('thietLap.logo')}</Label>
              <ChonAnh
                khoa={logo}
                duongDanTai="/anh/trung-tam/logo"
                duongDanXoa="/anh/trung-tam/logo"
                onXong={(k) => {
                  setLogo(k)
                  void qc.invalidateQueries({ queryKey: ['thiet-lap'] })
                }}
              />
            </div>
            <div>
              <Label className="mb-1.5 block">{t('thietLap.anhBia')}</Label>
              <ChonAnh
                khoa={anhBia}
                duongDanTai="/anh/trung-tam/anh-bia"
                duongDanXoa="/anh/trung-tam/anh-bia"
                onXong={(k) => {
                  setAnhBia(k)
                  void qc.invalidateQueries({ queryKey: ['thiet-lap'] })
                }}
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="diaChi">{t('thietLap.diaChi')}</Label>
            <Input
              id="diaChi"
              name="diaChi"
              defaultValue={data?.diaChi ?? ''}
              placeholder={t('thietLap.diaChiGoiY')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="lienHe">{t('thietLap.lienHe')}</Label>
            <Input
              id="lienHe"
              name="lienHe"
              defaultValue={data?.lienHe ?? ''}
              placeholder={t('thietLap.lienHeGoiY')}
            />
          </div>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="moTa">{t('thietLap.moTa')}</Label>
            <Textarea id="moTa" name="moTa" defaultValue={data?.moTa ?? ''} />
          </div>

          {/* Nhóm chuyển khoản. Tách riêng và nói rõ mức riêng tư: số tài khoản không phải
              thông tin để hiện công khai như tên hay logo — đặt cạnh nhau mà không phân biệt
              thì người dùng tưởng cả hai nhóm cùng mức. */}
          <div className="sm:col-span-2">
            <h3 className="text-sm font-semibold">{t('thietLap.nhomChuyenKhoan')}</h3>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {t('thietLap.nhomChuyenKhoanMoTa')}
            </p>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="soTaiKhoan">{t('thietLap.soTaiKhoan')}</Label>
            <Input
              id="soTaiKhoan"
              name="soTaiKhoan"
              defaultValue={data?.soTaiKhoan ?? ''}
              placeholder={t('thietLap.soTaiKhoanGoiY')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenNganHang">{t('thietLap.tenNganHang')}</Label>
            <Input
              id="tenNganHang"
              name="tenNganHang"
              defaultValue={data?.tenNganHang ?? ''}
              placeholder={t('thietLap.tenNganHangGoiY')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="chuTaiKhoan">{t('thietLap.chuTaiKhoan')}</Label>
            <Input
              id="chuTaiKhoan"
              name="chuTaiKhoan"
              defaultValue={data?.chuTaiKhoan ?? ''}
              placeholder={t('thietLap.chuTaiKhoanGoiY')}
            />
            <p className="text-xs text-muted-foreground">{t('thietLap.chuTaiKhoanLuuY')}</p>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label className="mb-1.5 block">{t('thietLap.anhQr')}</Label>
            <ChonAnh
              khoa={anhQr}
              duongDanTai="/anh/trung-tam/qr-chuyen-khoan"
              duongDanXoa="/anh/trung-tam/qr-chuyen-khoan"
              onXong={(k) => {
                setQrNhap(k)
                void qc.invalidateQueries({ queryKey: ['thiet-lap'] })
              }}
            />
            <p className="text-xs text-muted-foreground">{t('thietLap.anhQrGoiY')}</p>
          </div>

          {maLoi && (
            <div className="sm:col-span-2">
              <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
            </div>
          )}

          <div className="flex items-center gap-3 sm:col-span-2">
            <Button type="submit" disabled={luu.isPending}>
              {t('chung.luu')}
            </Button>
            {daLuu && <span className="text-sm text-status-win">{t('chung.daLuu')}</span>}
          </div>
        </form>
      </CardContent>
    </Card>
  )
}
