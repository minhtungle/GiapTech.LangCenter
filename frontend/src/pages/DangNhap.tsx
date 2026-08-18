import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/lib/auth'
import { layMaLoi } from '@/lib/api'
import { useTinhNang } from '@/lib/tinhNang'
import {
  Button, CanhBaoLoi, Card, CardContent, CardDescription, CardHeader, CardTitle, Input, Label,
} from '@/components/ui'

const schema = z.object({
  maDoi: z.string().min(1).transform((v) => v.trim().toUpperCase()),
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
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { register, handleSubmit, formState } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { maDoi: '', username: '', matKhau: '' },
  })

  const onSubmit = async (data: FormData) => {
    setMaLoi(null)
    try {
      const { phaiDoiMatKhau } = await dangNhap(data.maDoi, data.username, data.matKhau)
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
              <Label htmlFor="maDoi">{t('dangNhap.maDoi')}</Label>
              <Input
                id="maDoi"
                autoFocus
                autoComplete="organization"
                maxLength={7}
                placeholder="A3K9M2P"
                // Hiển thị hoa ngay khi gõ để khớp với mã được cấp; backend cũng chuẩn hoá
                // nên gõ thường vẫn vào được, đây chỉ là gợi ý trực quan.
                className="font-mono uppercase tracking-widest placeholder:tracking-widest"
                {...register('maDoi')}
              />
              <p className="text-xs text-muted-foreground">{t('dangNhap.maDoiGoiY')}</p>
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

            {/* Chỉ hiện khi API khai là đăng ký CLB đang bật. Trên production endpoint đó trả
                404, người dùng bấm vào sẽ điền cả form rồi nhận "Đã có lỗi xảy ra". */}
            {tinhNang.dangKyClb && (
              <p className="text-center text-sm text-muted-foreground">
                {t('dangNhap.chuaCoClb')}{' '}
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
