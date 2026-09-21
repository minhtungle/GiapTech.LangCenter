import { useState } from 'react'
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

  const { register, handleSubmit, formState, watch } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { maTrungTam: '', username: '', matKhau: '' },
  })

  // Tra tên đội ngay khi mã đủ 7 ký tự: gõ sai một chữ mà chỉ biết sau khi điền cả mật khẩu
  // rồi nhận "sai thông tin đăng nhập" thì không phân biệt được là sai mã hay sai mật khẩu.
  const { tenTrungTam, dangTra } = useTraTenTrungTam(watch('maTrungTam') ?? '')

  const onSubmit = async (data: FormData) => {
    setMaLoi(null)
    try {
      const { phaiDoiMatKhau } = await dangNhap(data.maTrungTam, data.username, data.matKhau)
      // Bắt buộc đổi mật khẩu trước khi vào hệ thống (FR-01). Backend cũng chặn ở
      // middleware, nên điều hướng này chỉ để trải nghiệm mượt, không phải lớp bảo vệ.
      navigate(phaiDoiMatKhau ? '/doi-mat-khau' : '/', { replace: true })
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4">
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
                autoFocus
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
              ) : tenTrungTam ? (
                <p className="flex items-center gap-1 text-xs font-medium text-primary">
                  <Check className="h-3.5 w-3.5 shrink-0" />
                  <span className="truncate">{tenTrungTam}</span>
                </p>
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
                autoComplete="current-password"
                {...register('matKhau')}
              />
            </div>

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
  )
}
