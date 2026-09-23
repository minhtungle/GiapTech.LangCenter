import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { Check, CircleAlert } from 'lucide-react'
import { useAuth } from '@/lib/auth'
import { layMaLoi } from '@/lib/api'
import { useTinhNang } from '@/lib/tinhNang'
import { useTraTenTrungTam } from '@/lib/traTenTrungTam'
import { vietTat } from '@/lib/nhanDienTrungTam'
import { useNhanDienTab } from '@/lib/nhanDienTab'
import { ChonNgonNgu } from '@/components/ChonNgonNgu'
import { docDaNho, luuDaNho, xoaDaNho } from '@/lib/nhoDangNhap'
import { BannerTrungTam } from './BannerTrungTam'
import {
  Button, CanhBaoLoi, Card, CardContent, CardDescription, CardHeader, CardTitle, Input, Label,
} from '@/components/ui'

const schema = z.object({
  maTrungTam: z.string().min(1).transform((v) => v.trim().toUpperCase()),
  username: z.string().min(1),
  matKhau: z.string().min(1),
})

type FormData = z.infer<typeof schema>

/** FR-01 — đăng nhập bằng bộ ba {ID đội, username, mật khẩu}. */
export default function DangNhap() {
  const { t } = useTranslation()
  const tinhNang = useTinhNang()
  const { dangNhap } = useAuth()
  const navigate = useNavigate()
  /*
    Lý do bị đá khỏi phiên trước, do interceptor đặt vào `sessionStorage` (xem `lib/api.ts`).

    Đọc một lần rồi XOÁ: giữ lại thì lần đăng nhập sau vẫn hiện câu "vừa đăng nhập ở nơi khác"
    dù chẳng có gì xảy ra, và người dùng sẽ tưởng bị chiếm tài khoản.
  */
  const [maLoi, setMaLoi] = useState<string | null>(() => {
    try {
      const ly = sessionStorage.getItem('lms_ly_do_thoat')
      if (ly) sessionStorage.removeItem('lms_ly_do_thoat')
      return ly
    } catch {
      return null
    }
  })

  /*
    Thông tin đã nhớ từ lần trước — đọc MỘT lần lúc dựng component (`useState` khởi tạo lười),
    không đọc lại mỗi lần render. Đọc lại sẽ ghi đè thứ người dùng đang gõ dở.

    Ô "nhớ" tích sẵn khi đã từng nhớ: người dùng đã chọn nhớ thì lần sau bỏ tích mới là hành
    động có ý thức, chứ không phải mỗi lần đăng nhập lại phải tích lại.
  */
  const [daNho] = useState(docDaNho)
  const [nhoDangNhap, setNhoDangNhap] = useState(daNho !== null)

  const { register, handleSubmit, formState, watch } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      maTrungTam: daNho?.maTrungTam ?? '',
      username: daNho?.username ?? '',
      // KHÔNG bao giờ điền sẵn mật khẩu — xem `lib/nhoDangNhap.ts`.
      matKhau: '',
    },
  })

  // Tra tên đội ngay khi mã đủ 7 ký tự: gõ sai một chữ mà chỉ biết sau khi điền cả mật khẩu
  // rồi nhận "sai thông tin đăng nhập" thì không phân biệt được là sai mã hay sai mật khẩu.
  const { tenTrungTam, trungTam, duongDanLogo, duongDanAnhBia, dangTra } =
    useTraTenTrungTam(watch('maTrungTam') ?? '')

  /*
    Tab đổi theo mã vừa gõ (22/09/2026).

    `laUrl = true` vì ở màn đăng nhập chưa có token: `duongDanLogo` trỏ endpoint ẩn danh
    `/auth/logo/{ma}` nhận MÃ chứ không nhận khoá ảnh, nên dùng thẳng được.
  */
  useNhanDienTab(trungTam?.tenTrungTam, duongDanLogo, true)

  /*
    Đẩy linh vật lên TRÊN footer của màn này (footer cao ~3.5rem).

    Phải đặt biến lên `:root`, không đặt bằng class trên thẻ bao ngoài: `LinhVat` mount ở
    `App.tsx`, **ngoài cây DOM của trang này**, nên nó không thừa kế được biến khai ở đây.
    Đã thử cách class trước và biến luôn về giá trị mặc định `0px` — nhân vật đè lên dòng bản
    quyền, phát hiện khi chụp màn kiểm tra.
  */
  useEffect(() => {
    document.documentElement.style.setProperty('--linh-vat-day', '3.5rem')
    return () => {
      document.documentElement.style.removeProperty('--linh-vat-day')
    }
  }, [])

  const onSubmit = async (data: FormData) => {
    setMaLoi(null)
    try {
      const { phaiDoiMatKhau } = await dangNhap(data.maTrungTam, data.username, data.matKhau)
      // Ghi nhớ SAU khi đăng nhập thành công: nhớ bộ sai thì lần sau người dùng lại phải xoá
      // tay đúng cái mà hệ thống vừa điền cho họ.
      if (nhoDangNhap) luuDaNho({ maTrungTam: data.maTrungTam, username: data.username })
      else xoaDaNho()
      // Bắt buộc đổi mật khẩu trước khi vào hệ thống (FR-01). Backend cũng chặn ở
      // middleware, nên điều hướng này chỉ để trải nghiệm mượt, không phải lớp bảo vệ.
      navigate(phaiDoiMatKhau ? '/doi-mat-khau' : '/', { replace: true })
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  return (
    /*
      Hai cột: banner trái, form phải (22/09/2026 — *"đưa khung đăng nhập sang phải, bên trái
      để hiển thị 1 khung banner"*).

      `min-h-screen` + `lg:flex-row`: dưới `lg` banner tự ẩn (xem `BannerTrungTam`) và form
      chiếm trọn màn — nhồi cả hai vào màn điện thoại sẽ đẩy form xuống dưới nếp gấp.
    */
    <div className="relative flex min-h-screen flex-col">
      {/* Hàng trên: banner trái + form phải. `flex-1` để nó ăn hết chiều cao còn lại. */}
      <div className="flex flex-1 flex-col lg:flex-row">
      <BannerTrungTam
        trungTam={trungTam}
        duongDanLogo={duongDanLogo}
        duongDanAnhBia={duongDanAnhBia}
      />

      {/* Cột phải: form căn giữa cả hai chiều. */}
      <div className="flex flex-1 items-center justify-center bg-muted/30 px-4 py-10">
        <div className="w-full max-w-sm">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-lg">{t('dangNhap.tieuDe')}</CardTitle>
          <CardDescription>{t('dangNhap.moTa')}</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="maTrungTam">{t('dangNhap.maTrungTam')}</Label>
              <Input
                id="maTrungTam"
                // Đã điền sẵn thì con trỏ nhảy thẳng xuống ô mật khẩu (xem ô mật khẩu bên
                // dưới) — bắt người dùng tự bấm qua hai ô đã có sẵn chữ là vô nghĩa.
                autoFocus={daNho === null}
                autoComplete="organization"
                maxLength={7}
                placeholder="A3K9M2P"
                // Hiển thị hoa ngay khi gõ để khớp với mã được cấp; backend cũng chuẩn hoá
                // nên gõ thường vẫn vào được, đây chỉ là gợi ý trực quan.
                className="font-mono uppercase tracking-widest placeholder:tracking-widest"
                {...register('maTrungTam')}
              />
              {/* Ba trạng thái, mỗi trạng thái một câu: đang tra / tìm thấy tên / không có trung tâm
                  nào. Khi mã chưa đủ 7 ký tự thì giữ nguyên câu gợi ý — hiện "không tìm thấy"
                  lúc người dùng còn đang gõ là báo sai. */}
              {dangTra ? (
                <p className="text-xs text-muted-foreground">{t('dangNhap.dangTraTenTrungTam')}</p>
              ) : trungTam ? (
                /*
                  Thẻ nhận diện trung tâm (22/09/2026) — *"nhập đúng mã trung tâm sẽ load đúng
                  thông tin trung tâm như trong thiết lập"*.

                  Hiện logo THẬT nếu trung tâm đã tải lên, không thì ô chữ cái đầu như sidebar.
                  Không hiện địa chỉ/liên hệ: đây là màn công khai, ai dò trúng mã 7 ký tự cũng
                  đọc được — xem `TenTrungTamTheoMaDto`.
                */
                <div className="flex items-center gap-2.5 rounded-md border border-primary/30 bg-primary/5 px-2.5 py-2">
                  {duongDanLogo ? (
                    <img
                      src={duongDanLogo}
                      alt=""
                      className="h-9 w-9 shrink-0 rounded object-contain"
                      /* Logo hỏng (bị xoá khỏi kho, MinIO chết) thì ẩn hẳn thay vì để icon
                         ảnh vỡ — người dùng vẫn đọc được tên bên cạnh. */
                      onError={(e) => {
                        e.currentTarget.style.display = 'none'
                      }}
                    />
                  ) : (
                    <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded bg-primary text-xs font-semibold text-primary-foreground">
                      {vietTat(trungTam.tenVietTat ?? trungTam.tenTrungTam)}
                    </span>
                  )}
                  <span className="min-w-0">
                    <span className="flex items-center gap-1 text-xs font-medium text-primary">
                      <Check className="h-3.5 w-3.5 shrink-0" />
                      <span className="truncate">{trungTam.tenTrungTam}</span>
                    </span>
                    {trungTam.tenVietTat && (
                      <span className="block truncate text-xs text-muted-foreground">
                        {trungTam.tenVietTat}
                      </span>
                    )}
                  </span>
                </div>
              ) : tenTrungTam === null ? (
                <p className="flex items-center gap-1 text-xs text-destructive">
                  <CircleAlert className="h-3.5 w-3.5 shrink-0" />
                  {t('dangNhap.khongTimThayTrungTam')}
                </p>
              ) : (
                <p className="text-xs text-muted-foreground">{t('dangNhap.maTrungTamGoiY')}</p>
              )}
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="username">{t('dangNhap.username')}</Label>
              <Input id="username" autoComplete="username" {...register('username')} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="matKhau">{t('dangNhap.matKhau')}</Label>
              <Input
                id="matKhau"
                type="password"
                autoFocus={daNho !== null}
                autoComplete="current-password"
                {...register('matKhau')}
              />
            </div>

            {/*
              Nhớ MÃ TRUNG TÂM + TÊN ĐĂNG NHẬP, không nhớ mật khẩu (22/09/2026).

              Bỏ tích thì QUÊN NGAY, không đợi lần đăng nhập thành công kế tiếp: người ở máy
              dùng chung bỏ tích rồi đổi ý không đăng nhập nữa — thông tin của họ phải biến
              mất ngay lúc đó, chứ không nằm lại chờ một sự kiện có thể không bao giờ tới.
            */}
            <label className="flex items-center gap-2 text-sm text-muted-foreground">
              <input
                type="checkbox"
                className="h-4 w-4 accent-[hsl(var(--primary))]"
                checked={nhoDangNhap}
                onChange={(e) => {
                  setNhoDangNhap(e.target.checked)
                  if (!e.target.checked) xoaDaNho()
                }}
              />
              {t('dangNhap.nhoDangNhap')}
            </label>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <Button type="submit" disabled={formState.isSubmitting}>
              {formState.isSubmitting ? t('chung.dangTai') : t('dangNhap.nut')}
            </Button>

            <Link
              to="/quen-mat-khau"
              className="text-center text-sm text-muted-foreground hover:text-foreground"
            >
              {t('dangNhap.quenMatKhau')}
            </Link>

            {/* Chỉ hiện khi API khai là đăng ký đang bật. Nếu máy chủ tắt thì endpoint đó trả
                404, người dùng bấm vào sẽ điền cả form rồi nhận "Đã có lỗi xảy ra". */}
            {tinhNang.dangKyTrungTam && (
              <p className="text-center text-sm text-muted-foreground">
                {t('dangNhap.chuaCoTrungTam')}{' '}
                <Link to="/dang-ky" className="text-primary hover:underline">
                  {t('dangKy.nut')}
                </Link>
              </p>
            )}
          </form>
        </CardContent>
      </Card>
        </div>
      </div>
      </div>

      {/*
          Footer — *"thông tin cũng được thiết lập và 1 dòng bản quyền phần mềm của GiapTex"*.

          Chủ sản phẩm chốt: footer **chỉ có dòng bản quyền**; thông tin trung tâm nằm ở banner
          bên trái (chỉ hiện sau khi gõ đúng mã). Nên người chưa biết mã không đọc được gì về
          trung tâm — cùng lý lẽ với việc endpoint ẩn danh không trả `lienHe`.

          Tên trung tâm chèn vào dòng bản quyền khi đã biết, để footer không phải một dòng
          trơ trọi giữa màn.
        */}
      {/*
        Nút đổi ngôn ngữ ở góc trên phải màn đăng nhập.

        Phải có Ở ĐÂY, không chỉ trong hệ thống: người chưa đăng nhập được mà không đọc được
        tiếng Việt thì không có đường nào khác để đổi.
      */}
      <div className="absolute right-4 top-4 z-10">
        <ChonNgonNgu huong="xuong" />
      </div>

      <footer className="border-t border-border bg-background px-4 py-4 text-center text-xs text-muted-foreground">
          {trungTam
            ? t('dangNhap.banQuyenCoTen', {
                nam: new Date().getFullYear(),
                ten: trungTam.tenTrungTam,
              })
            : t('dangNhap.banQuyen', { nam: new Date().getFullYear() })}
      </footer>
    </div>
  )
}
