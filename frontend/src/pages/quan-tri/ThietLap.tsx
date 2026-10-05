import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, layMaLoi } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { Button, CanhBaoLoi, Card, CardContent, Input, Label, Textarea,
} from '@/components/ui'
import { ChonAnh } from '@/components/ui/ChonAnh'
import { useXacNhan } from '@/lib/xacNhan'

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
  duoiTenDangNhap: string | null
  duoiTenDangNhap2: string | null
  duoiTenDangNhap3: string | null
  soTaiKhoan: string | null
  tenNganHang: string | null
  chuTaiKhoan: string | null
  anhQrUrl: string | null
}

type Tab = 'trung-tam' | 'dang-nhap' | 'chuyen-khoan'

/**
 * FR-06 — thiết lập chung của trung tâm, chia ba tab (05/10/2026).
 *
 * ## Ba tab nhưng MỘT form — và tab ẩn KHÔNG unmount
 *
 * Đây là chỗ dễ sai nhất của trang này. Lệnh `PUT /thiet-lap` ghi đè mọi trường nó nhận, và
 * `onSubmit` dựng dữ liệu từ `FormData` của thẻ `<form>`. Nếu tab ẩn bị gỡ khỏi DOM thì
 * `fd.get('soTaiKhoan')` trả `null` khi đang đứng ở tab khác ⇒ `?? ''` biến nó thành chuỗi
 * rỗng ⇒ **lưu tab Trung tâm sẽ xoá sạch thông tin chuyển khoản**.
 *
 * Đó đúng là lỗi 16/08/2026 mặc áo mới (quy tắc #1). Nên ba tab nằm trong cùng một `<form>`,
 * và tab không hiện chỉ bị ẩn bằng `hidden` — input vẫn ở trong DOM, vẫn vào `FormData`.
 *
 * Hệ quả cố ý: **một nút Lưu cho cả ba tab**. Người dùng sửa ở tab nào, bấm Lưu ở đâu cũng
 * lưu tất cả — đúng với cách dữ liệu thực sự được gửi đi, thay vì giả vờ ba tab độc lập.
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
  const { hoi, hop } = useXacNhan()
  const qc = useQueryClient()
  const { capNhatTenTrungTam } = useAuth()
  const [tab, setTab] = useState<Tab>('trung-tam')
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
    const du = {
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
      // Ba đuôi tên đăng nhập. Cùng quy ước: chuỗi rỗng = trung tâm thôi dùng đuôi đó.
      duoiTenDangNhap: (fd.get('duoiTenDangNhap') as string) ?? '',
      duoiTenDangNhap2: (fd.get('duoiTenDangNhap2') as string) ?? '',
      duoiTenDangNhap3: (fd.get('duoiTenDangNhap3') as string) ?? '',
      // Thông tin chuyển khoản. Cùng quy ước: chuỗi rỗng = xoá, không gửi null.
      soTaiKhoan: (fd.get('soTaiKhoan') as string) ?? '',
      tenNganHang: (fd.get('tenNganHang') as string) ?? '',
      chuTaiKhoan: (fd.get('chuTaiKhoan') as string) ?? '',
      // Ảnh QR do ChonAnh tự tải lên và trả khoá — gửi lại để không bị xoá khi lưu form.
      anhQrUrl: anhQr,
    }

    hoi({
      tieuDe: t('chung.xacNhanLuu'),
      thongDiep: t('thietLap.hoiLuu'),
      onDongY: () => luu.mutate(du),
    })
  }

  return (
    // `max-w-5xl` chứ không `max-w-2xl` — xem ghi chú ở đầu component.
    <Card className="max-w-5xl">
      <CardContent className="pt-5">
        {/* Thanh tab nằm NGOÀI <form> về mặt thị giác nhưng trong cùng Card — nó chỉ đổi tab
            nào hiện, không đụng tới dữ liệu. */}
        <div className="mb-4 flex flex-wrap gap-1 rounded-lg border border-border p-1">
          {([
            ['trung-tam', 'thietLap.tabTrungTam'],
            ['dang-nhap', 'thietLap.tabDangNhap'],
            ['chuyen-khoan', 'thietLap.tabChuyenKhoan'],
          ] as const).map(([ma, khoa]) => (
            <button
              key={ma}
              type="button"
              onClick={() => setTab(ma)}
              className={
                'rounded-md px-3 py-1.5 text-sm font-medium transition-colors '
                + (tab === ma
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-muted')
              }
            >
              {t(khoa)}
            </button>
          ))}
        </div>

        <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
          {/* Ẩn bằng CSS, KHÔNG gỡ khỏi DOM — xem ghi chú ở đầu component.

              Dùng class `hidden` chứ không thuộc tính `hidden` của HTML: nhóm đang hiện cần
              `display: contents` để các ô con nhận grid của <form>, mà `display: contents`
              ghi đè `display: none` mà thuộc tính `hidden` đặt ra ⇒ tab ẩn vẫn hiện nguyên.
              Đổi hẳn giá trị `display` theo tab thì không có hai luật tranh nhau. */}
          <div className={tab === 'trung-tam' ? 'contents' : 'hidden'}>
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

          </div>

          <div className={tab === 'dang-nhap' ? 'contents' : 'hidden'}>
          <div className="sm:col-span-2">
            <h3 className="text-sm font-semibold">{t('thietLap.nhomDangNhap')}</h3>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {t('thietLap.nhomDangNhapMoTa')}
            </p>
          </div>

          {/* Ba ô đuôi. Đánh số 1-2-3 chứ không "chính / phụ": người tạo tài khoản chọn ô nào
              thì nối đuôi ô ấy, không ô nào ưu tiên hơn ô nào. */}
          {([
            ['duoiTenDangNhap', data?.duoiTenDangNhap, '@vietgeneducation.edu.vn'],
            ['duoiTenDangNhap2', data?.duoiTenDangNhap2, '@hocvien.vietgen.edu.vn'],
            ['duoiTenDangNhap3', data?.duoiTenDangNhap3, '@ctv.vietgen.edu.vn'],
          ] as const).map(([ten, giaTri, goiY], i) => (
            <div key={ten} className="flex flex-col gap-1.5 sm:col-span-2">
              <Label htmlFor={ten}>
                {t('thietLap.duoiTenDangNhapSo', { so: i + 1 })}
              </Label>
              <Input
                id={ten}
                name={ten}
                maxLength={100}
                placeholder={goiY}
                defaultValue={giaTri ?? ''}
              />
            </div>
          ))}

          <div className="sm:col-span-2">
            <p className="text-xs text-muted-foreground">
              {t('thietLap.duoiTenDangNhapGoiY')}
            </p>
          </div>
          </div>

          <div className={tab === 'chuyen-khoan' ? 'contents' : 'hidden'}>
          {/* Nhóm chuyển khoản. Nói rõ mức riêng tư: số tài khoản không phải thông tin để
              hiện công khai như tên hay logo. */}
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
            {daLuu && <span className="text-sm text-status-ok">{t('chung.daLuu')}</span>}
            <span className="text-xs text-muted-foreground">{t('thietLap.luuCaBaTab')}</span>
          </div>
        </form>
      </CardContent>
      {hop}
    </Card>
  )
}
