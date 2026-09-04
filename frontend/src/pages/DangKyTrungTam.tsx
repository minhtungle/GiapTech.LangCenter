import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Check, Copy } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { useTinhNang } from '@/lib/tinhNang'
import {
  Button, CanhBaoLoi, Card, CardContent, CardDescription, CardHeader, CardTitle, Input, Label,
} from '@/components/ui'

interface KetQua {
  maTrungTam: string
  tenTrungTam: string
  username: string
  matKhau: string
}

/**
 * Đăng ký trung tâm mới. Mã trung tâm do hệ thống sinh (7 ký tự) — người dùng không tự đặt
 * vì tên trung tâm rất dễ trùng.
 *
 * Màn này hiển thị mã thật to kèm nút sao chép: mã sinh tự động mà người dùng không ghi
 * lại thì họ mất đường vào hệ thống, và không có cách nào tự tra lại.
 */
export default function DangKyTrungTam() {
  const { t } = useTranslation()
  const tinhNang = useTinhNang()
  const [tenTrungTam, setTenTrungTam] = useState('')
  const [ketQua, setKetQua] = useState<KetQua | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [dangGui, setDangGui] = useState(false)
  const [daChep, setDaChep] = useState(false)

  const dangKy = async (e: React.FormEvent) => {
    e.preventDefault()
    setMaLoi(null)
    setDangGui(true)
    try {
      const { data } = await api.post<KetQua>('/dang-ky-trung-tam', { tenTrungTam })
      setKetQua(data)
    } catch (err) {
      setMaLoi(layMaLoi(err))
    } finally {
      setDangGui(false)
    }
  }

  const chepMa = async () => {
    if (!ketQua) return
    await navigator.clipboard.writeText(ketQua.maTrungTam)
    setDaChep(true)
    setTimeout(() => setDaChep(false), 2000)
  }

  // Ẩn link ở trang đăng nhập là chưa đủ: gõ thẳng /dang-ky vẫn mở được form, điền xong mới
  // biết là không dùng được. Chặn ngay ở đây, nói rõ lý do thay vì để 404 thành "lỗi hệ thống".
  if (!tinhNang.dangKyTrungTam) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4">
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle className="text-lg">{t('dangKy.chuaMo')}</CardTitle>
            <CardDescription>{t('dangKy.chuaMoMoTa')}</CardDescription>
          </CardHeader>
          <CardContent>
            <Link to="/dang-nhap" className="text-sm text-primary hover:underline">
              {t('dangKy.veDangNhap')}
            </Link>
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-lg">{t('dangKy.tieuDe')}</CardTitle>
          <CardDescription>
            {ketQua ? t('dangKy.thanhCong') : t('dangKy.moTa')}
          </CardDescription>
        </CardHeader>

        <CardContent>
          {ketQua ? (
            <div className="flex flex-col gap-4">
              <div className="rounded-lg border border-primary/30 bg-primary/5 p-4">
                <p className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {t('dangKy.maTrungTamCuaBan')}
                </p>
                <div className="flex items-center gap-3">
                  <code className="font-mono text-3xl font-bold tracking-[0.2em] text-primary">
                    {ketQua.maTrungTam}
                  </code>
                  <Button variant="outline" size="sm" onClick={chepMa} title={t('dangKy.saoChep')}>
                    {daChep ? (
                      <Check className="h-4 w-4 text-status-win" />
                    ) : (
                      <Copy className="h-4 w-4" />
                    )}
                  </Button>
                </div>
              </div>

              <CanhBaoLoi>{t('dangKy.canhBaoLuuMa')}</CanhBaoLoi>

              <div className="rounded-md border border-border p-3 text-sm">
                <div className="flex justify-between py-0.5">
                  <span className="text-muted-foreground">{t('thietLap.tenTrungTam')}</span>
                  <span className="font-medium">{ketQua.tenTrungTam}</span>
                </div>
                <div className="flex justify-between py-0.5">
                  <span className="text-muted-foreground">{t('dangNhap.username')}</span>
                  <span className="font-mono font-medium">{ketQua.username}</span>
                </div>
                <div className="flex justify-between py-0.5">
                  <span className="text-muted-foreground">{t('dangNhap.matKhau')}</span>
                  <span className="font-mono font-medium">{ketQua.matKhau}</span>
                </div>
              </div>

              <p className="text-xs text-muted-foreground">{t('dangKy.luuYDoiMatKhau')}</p>

              <Link to="/dang-nhap">
                <Button className="w-full">{t('dangNhap.nut')}</Button>
              </Link>
            </div>
          ) : (
            <form onSubmit={dangKy} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="tenTrungTam">{t('thietLap.tenTrungTam')}</Label>
                <Input
                  id="tenTrungTam"
                  autoFocus
                  required
                  maxLength={200}
                  placeholder="VD: Trung tâm Ngoại ngữ Sông Hàn"
                  value={tenTrungTam}
                  onChange={(e) => setTenTrungTam(e.target.value)}
                />
                <p className="text-xs text-muted-foreground">{t('dangKy.giaiThichMa')}</p>
              </div>

              {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

              <Button type="submit" disabled={dangGui || !tenTrungTam.trim()}>
                {dangGui ? t('chung.dangTai') : t('dangKy.nut')}
              </Button>

              <Link
                to="/dang-nhap"
                className="text-center text-sm text-muted-foreground hover:text-foreground"
              >
                {t('chung.quayLai')}
              </Link>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
