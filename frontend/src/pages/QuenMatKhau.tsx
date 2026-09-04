import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api, layMaLoi } from '@/lib/api'
import {
  Button, CanhBaoLoi, Card, CardContent, CardDescription, CardHeader, CardTitle, Input, Label,
} from '@/components/ui'

/**
 * FR-02 — quên mật khẩu, hai bước trên cùng một màn hình.
 *
 * Bước 1 luôn báo thành công dù email có tồn tại hay không: backend cũng trả 204 giống
 * hệt nhau, nên giao diện không được để lộ điều backend cố tình giấu.
 */
export default function QuenMatKhau() {
  const { t } = useTranslation()
  const [buoc, setBuoc] = useState<1 | 2>(1)
  const [maTrungTam, setMaTrungTam] = useState('')
  const [email, setEmail] = useState('')
  const [token, setToken] = useState('')
  const [matKhauMoi, setMatKhauMoi] = useState('')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [dangGui, setDangGui] = useState(false)
  const [xong, setXong] = useState(false)

  const guiYeuCau = async (e: React.FormEvent) => {
    e.preventDefault()
    setMaLoi(null)
    setDangGui(true)
    try {
      await api.post('/auth/quen-mat-khau', { maTrungTam, email })
      setBuoc(2)
    } catch (err) {
      setMaLoi(layMaLoi(err))
    } finally {
      setDangGui(false)
    }
  }

  const datLai = async (e: React.FormEvent) => {
    e.preventDefault()
    setMaLoi(null)
    setDangGui(true)
    try {
      await api.post('/auth/dat-lai-mat-khau', { token, matKhauMoi })
      setXong(true)
    } catch (err) {
      setMaLoi(layMaLoi(err))
    } finally {
      setDangGui(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-lg">{t('dangNhap.quenTieuDe')}</CardTitle>
          <CardDescription>
            {buoc === 1 ? t('dangNhap.quenMoTa') : t('dangNhap.daGuiEmail')}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {xong ? (
            <div className="flex flex-col gap-4">
              <p className="text-sm">Đặt lại mật khẩu thành công.</p>
              <Link to="/dang-nhap">
                <Button className="w-full">{t('dangNhap.nut')}</Button>
              </Link>
            </div>
          ) : buoc === 1 ? (
            <form onSubmit={guiYeuCau} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="maTrungTam">{t('dangNhap.maTrungTam')}</Label>
                <Input
                  id="maTrungTam"
                  autoFocus
                  maxLength={7}
                  placeholder="A3K9M2P"
                  className="font-mono uppercase tracking-widest"
                  value={maTrungTam}
                  onChange={(e) => setMaTrungTam(e.target.value.toUpperCase())}
                  required
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="email">{t('dangNhap.email')}</Label>
                <Input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
              </div>

              {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

              <Button type="submit" disabled={dangGui}>
                {dangGui ? t('chung.dangTai') : t('dangNhap.guiYeuCau')}
              </Button>
              <Link
                to="/dang-nhap"
                className="text-center text-sm text-muted-foreground hover:text-foreground"
              >
                {t('chung.quayLai')}
              </Link>
            </form>
          ) : (
            <form onSubmit={datLai} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="token">{t('dangNhap.maDatLai')}</Label>
                <Input
                  id="token"
                  autoFocus
                  value={token}
                  onChange={(e) => setToken(e.target.value)}
                  required
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="mkMoi">{t('dangNhap.matKhauMoi')}</Label>
                <Input
                  id="mkMoi"
                  type="password"
                  minLength={6}
                  value={matKhauMoi}
                  onChange={(e) => setMatKhauMoi(e.target.value)}
                  required
                />
              </div>

              {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

              <Button type="submit" disabled={dangGui}>
                {dangGui ? t('chung.dangTai') : t('dangNhap.datLaiMatKhau')}
              </Button>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
