import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { apiChu, luuTokenChu } from '@/lib/apiChu'
import { layMaLoi } from '@/lib/api'
import {
  Button, CanhBaoLoi, Card, CardContent, CardDescription, CardHeader, CardTitle, Input, Label,
} from '@/components/ui'

interface FormData {
  username: string
  matKhau: string
}

/** Vài mã lỗi hay gặp, viết thẳng tiếng Việt — xem chú thích ở chỗ dùng. */
const MO_TA_LOI: Record<string, string> = {
  DANG_NHAP_THAT_BAI: 'Sai tên đăng nhập hoặc mật khẩu.',
  TAI_KHOAN_BI_VO_HIEU_HOA: 'Tài khoản đã bị vô hiệu hoá.',
  LOI_HE_THONG: 'Có lỗi xảy ra. Thử lại sau.',
}

/**
 * Đăng nhập **site chủ hệ thống** (ADR-0009).
 *
 * Không có ô mã trung tâm: tài khoản này đứng TRÊN mọi tenant, không thuộc trung tâm nào.
 *
 * Cố ý **không có "quên mật khẩu"**: chỉ có vài tài khoản chủ và chúng mạnh hơn admin của bất
 * kỳ trung tâm nào — mở một đường đặt lại mật khẩu qua email là thêm bề mặt tấn công đổi lấy
 * tiện lợi hiếm khi dùng. Mất mật khẩu thì đặt lại thẳng trong DB.
 */
export default function DangNhapChu() {
  const navigate = useNavigate()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const { register, handleSubmit, formState } = useForm<FormData>({
    defaultValues: { username: '', matKhau: '' },
  })

  const onSubmit = async (data: FormData) => {
    setMaLoi(null)
    try {
      const res = await apiChu.post('/dang-nhap', data)
      luuTokenChu(res.data.accessToken)
      navigate('/chu/trung-tam', { replace: true })
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Quản trị hệ thống</CardTitle>
          <CardDescription>
            Trang dành cho chủ sản phẩm — quản lý các trung tâm.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="username">Tên đăng nhập</Label>
              <Input
                id="username"
                autoFocus
                autoComplete="username"
                {...register('username', { required: true })}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="matKhau">Mật khẩu</Label>
              <Input
                id="matKhau"
                type="password"
                autoComplete="current-password"
                {...register('matKhau', { required: true })}
              />
            </div>

            {/*
              Site chủ KHÔNG đa ngôn ngữ: nó chỉ dành cho chủ sản phẩm, không phải khách hàng.
              Thêm 5 bản dịch cho một màn hai người dùng là chi phí không đổi lại gì.
              Hiện mã lỗi thô khi không có câu tiếng Việt — người đọc là người biết mã đó.
            */}
            {maLoi && <CanhBaoLoi>{MO_TA_LOI[maLoi] ?? maLoi}</CanhBaoLoi>}

            <Button type="submit" disabled={formState.isSubmitting}>
              {formState.isSubmitting ? 'Đang đăng nhập…' : 'Đăng nhập'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
