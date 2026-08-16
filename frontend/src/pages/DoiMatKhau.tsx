import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { api, layMaLoi } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import {
  Button, CanhBaoLoi, Card, CardContent, CardDescription, CardHeader, CardTitle, Input, Label,
} from '@/components/ui'

const schema = z
  .object({
    matKhauCu: z.string().min(1),
    matKhauMoi: z.string().min(6, 'MAT_KHAU_QUA_NGAN'),
    xacNhan: z.string().min(1),
  })
  .refine((d) => d.matKhauMoi === d.xacNhan, {
    path: ['xacNhan'],
    message: 'MAT_KHAU_XAC_NHAN_KHONG_KHOP',
  })

type FormData = z.infer<typeof schema>

/** FR-01 — đổi mật khẩu (bắt buộc ở lần đăng nhập đầu). */
export default function DoiMatKhau() {
  const { t } = useTranslation()
  const { danhDauDaDoiMatKhau } = useAuth()
  const navigate = useNavigate()
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { register, handleSubmit, formState } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { matKhauCu: '', matKhauMoi: '', xacNhan: '' },
  })

  const onSubmit = async (data: FormData) => {
    setMaLoi(null)
    try {
      await api.post('/auth/doi-mat-khau', {
        matKhauCu: data.matKhauCu,
        matKhauMoi: data.matKhauMoi,
      })
      danhDauDaDoiMatKhau()
      navigate('/', { replace: true })
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  const loiXacNhan = formState.errors.xacNhan?.message

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-lg">{t('dangNhap.doiMatKhauTieuDe')}</CardTitle>
          <CardDescription>{t('dangNhap.doiMatKhauMoTa')}</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="matKhauCu">{t('dangNhap.matKhauCu')}</Label>
              <Input id="matKhauCu" type="password" autoFocus {...register('matKhauCu')} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="matKhauMoi">{t('dangNhap.matKhauMoi')}</Label>
              <Input id="matKhauMoi" type="password" {...register('matKhauMoi')} />
              {formState.errors.matKhauMoi && (
                <p className="text-xs text-destructive">
                  {t(`loi.${formState.errors.matKhauMoi.message}`)}
                </p>
              )}
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="xacNhan">{t('dangNhap.xacNhanMatKhau')}</Label>
              <Input id="xacNhan" type="password" {...register('xacNhan')} />
              {loiXacNhan && (
                <p className="text-xs text-destructive">Mật khẩu xác nhận không khớp</p>
              )}
            </div>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <Button type="submit" disabled={formState.isSubmitting}>
              {formState.isSubmitting ? t('chung.dangTai') : t('chung.luu')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
