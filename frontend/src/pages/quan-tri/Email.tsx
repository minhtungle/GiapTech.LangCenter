import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, layMaLoi } from '@/lib/api'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { Button, CanhBaoLoi, Card, CardContent, Input, Label } from '@/components/ui'
import { SoanThao } from '@/components/ui/SoanThao'

/** Khớp `ThietLapEmailDto` — CỐ Ý không có trường mật khẩu, chỉ cờ `coMatKhau`. */
interface ThietLapEmailDto {
  daCauHinh: boolean
  smtpHost: string | null
  smtpPort: number | null
  smtpUser: string | null
  coMatKhau: boolean
  smtpNguoiGui: string | null
  smtpTenNguoiGui: string | null
  khoaMaHoaSanSang: boolean
}

interface MauEmailDto {
  loai: string
  daSoan: boolean
  dangDung: boolean
  tieuDe: string
  noiDungHtml: string
  bien: string[]
  tuGuiDuoc: boolean
}

/**
 * FR-31 — thiết lập gửi email và mẫu nội dung.
 *
 * Hai tab vì hai việc khác nhau và hai quyền khác nhau: `ThietLapEmail` (kết nối SMTP) và
 * `MauEmail` (nội dung thư). Người soạn nội dung không nhất thiết được đụng vào cấu hình
 * máy chủ thư, nên tab nào không có quyền thì không hiện.
 */
export default function Email() {
  const { t } = useTranslation()
  const { coQuyen } = useQuyen()

  const xemThietLap = coQuyen('ThietLapEmail', 'Xem')
  const xemMau = coQuyen('MauEmail', 'Xem')

  const tabs = [
    ...(xemThietLap ? [{ ma: 'thiet-lap' as const, khoa: 'email.tabThietLap' }] : []),
    ...(xemMau ? [{ ma: 'mau' as const, khoa: 'email.tabMau' }] : []),
  ]
  const [tab, setTab] = useState<'thiet-lap' | 'mau'>(tabs[0]?.ma ?? 'thiet-lap')

  if (tabs.length === 0) {
    return <p className="text-sm text-muted-foreground">{t('email.khongCoQuyen')}</p>
  }

  return (
    <div className="max-w-5xl space-y-4">
      <h1 className="text-xl font-semibold">{t('email.tieuDeTrang')}</h1>

      {tabs.length > 1 && (
        <div className="flex gap-1 border-b border-border pb-2">
          {tabs.map((x) => (
            <button
              key={x.ma}
              type="button"
              onClick={() => setTab(x.ma)}
              className={
                'rounded-md px-3 py-1.5 text-sm font-medium transition-colors ' +
                (tab === x.ma
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-muted')
              }
            >
              {t(x.khoa)}
            </button>
          ))}
        </div>
      )}

      {tab === 'thiet-lap' && xemThietLap && <TabThietLap />}
      {tab === 'mau' && xemMau && <TabMau />}
    </div>
  )
}

// ---------- Tab 1: cấu hình SMTP ----------

function TabThietLap() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)
  const [emailThu, setEmailThu] = useState('')
  const [daGuiThu, setDaGuiThu] = useState(false)
  /*
    Tăng để nạp lại giá trị mặc định của form (React remount theo `key`).

    Dùng `key` chứ không chuyển form sang controlled: form này dùng `defaultValue` +
    `FormData`, đổi sang controlled là viết lại toàn bộ và thêm một nguồn trạng thái phải
    đồng bộ với server. Remount đạt đúng mục đích với một dòng.
  */
  const [dienGmail, setDienGmail] = useState(0)

  const duocSua = coQuyen('ThietLapEmail', 'Sua')

  const { data, isLoading } = useQuery({
    queryKey: ['email-thiet-lap'],
    queryFn: async () => (await api.get<ThietLapEmailDto>('/email/thiet-lap')).data,
  })

  const luu = useMutation({
    mutationFn: (than: Record<string, unknown>) => api.put('/email/thiet-lap', than),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['email-thiet-lap'] })
      setMaLoi(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: () => api.delete('/email/thiet-lap'),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['email-thiet-lap'] })
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const guiThu = useMutation({
    mutationFn: (denEmail: string) => api.post('/email/gui-thu', { denEmail }),
    onSuccess: () => {
      setMaLoi(null)
      setDaGuiThu(true)
      setTimeout(() => setDaGuiThu(false), 4000)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    const matKhau = String(fd.get('matKhau') ?? '')

    luu.mutate({
      smtpHost: String(fd.get('smtpHost')),
      smtpPort: Number(fd.get('smtpPort')),
      smtpUser: String(fd.get('smtpUser')),
      // Để TRỐNG = giữ nguyên mật khẩu cũ. Gửi `null` chứ không gửi chuỗi rỗng để ý định
      // rõ ràng ở cả hai đầu.
      matKhau: matKhau === '' ? null : matKhau,
      smtpNguoiGui: String(fd.get('smtpNguoiGui')),
      smtpTenNguoiGui: (fd.get('smtpTenNguoiGui') as string) || null,
    })
  }

  return (
    <div className="space-y-4">
      {!data?.khoaMaHoaSanSang && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm">
          {t('email.chuaCoKhoaMaHoa')}
        </div>
      )}

      {!data?.daCauHinh && (
        <p className="text-sm text-muted-foreground">{t('email.dangDungSmtpChung')}</p>
      )}

      {/*
        Hướng dẫn Gmail đặt NGAY TRÊN form, không giấu trong tài liệu.

        Gmail chặn đăng nhập bằng mật khẩu thường từ 2022 — nhập mật khẩu Gmail vào đây sẽ
        báo "Username and Password not accepted", mà thông báo đó không nói gì về mật khẩu
        ứng dụng. Người dùng sẽ đi đổi mật khẩu Gmail (thứ vốn đúng) thay vì tạo mật khẩu
        ứng dụng. Nói trước ở đây rẻ hơn nhiều so với một buổi hỗ trợ.
      */}
      {duocSua && (
        <div className="rounded-md border border-input bg-muted/40 px-3 py-2 text-sm">
          <p className="font-medium">{t('email.gmailTieuDe')}</p>
          <ol className="ml-4 mt-1 list-decimal space-y-0.5 text-muted-foreground">
            <li>{t('email.gmailB1')}</li>
            <li>{t('email.gmailB2')}</li>
            <li>{t('email.gmailB3')}</li>
          </ol>
          <button
            type="button"
            onClick={() => setDienGmail((n) => n + 1)}
            className="mt-2 rounded border border-input bg-background px-2 py-1 text-xs font-medium hover:bg-muted"
          >
            {t('email.dienSanGmail')}
          </button>
        </div>
      )}

      <Card>
        <CardContent className="pt-6">
          <form key={dienGmail} onSubmit={onSubmit} className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="smtpHost">{t('email.smtpHost')}</Label>
                <Input
                  id="smtpHost" name="smtpHost" required maxLength={200}
                  defaultValue={dienGmail > 0 ? 'smtp.gmail.com' : (data?.smtpHost ?? '')}
                  placeholder="smtp.gmail.com" disabled={!duocSua}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="smtpPort">{t('email.smtpPort')}</Label>
                <Input
                  id="smtpPort" name="smtpPort" type="number" required min={1} max={65535}
                  defaultValue={dienGmail > 0 ? 587 : (data?.smtpPort ?? 587)}
                  disabled={!duocSua}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="smtpUser">{t('email.smtpUser')}</Label>
                <Input
                  id="smtpUser" name="smtpUser" required maxLength={200}
                  defaultValue={data?.smtpUser ?? ''} disabled={!duocSua}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="matKhau">{t('email.matKhau')}</Label>
                <Input
                  id="matKhau" name="matKhau" type="password" maxLength={500}
                  autoComplete="new-password" disabled={!duocSua}
                  placeholder={
                    data?.coMatKhau ? t('email.matKhauDaLuu') : t('email.matKhauChuaCo')
                  }
                />
                <p className="text-xs text-muted-foreground">{t('email.matKhauGhiChu')}</p>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="smtpNguoiGui">{t('email.nguoiGui')}</Label>
                <Input
                  id="smtpNguoiGui" name="smtpNguoiGui" type="email" required maxLength={200}
                  defaultValue={data?.smtpNguoiGui ?? ''} disabled={!duocSua}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="smtpTenNguoiGui">{t('email.tenNguoiGui')}</Label>
                <Input
                  id="smtpTenNguoiGui" name="smtpTenNguoiGui" maxLength={200}
                  defaultValue={data?.smtpTenNguoiGui ?? ''} disabled={!duocSua}
                />
              </div>
            </div>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            {duocSua && (
              <div className="flex flex-wrap items-center gap-2">
                <Button type="submit" disabled={luu.isPending}>
                  {luu.isPending ? t('chung.luu') : t('chung.luu')}
                </Button>
                {daLuu && (
                  <span className="text-sm text-muted-foreground">{t('chung.daLuu')}</span>
                )}
                {data?.daCauHinh && (
                  <Button
                    type="button" variant="outline"
                    onClick={() =>
                      hoi({
                        tieuDe: t('email.xoaCauHinh'),
                        thongDiep: t('email.xoaCauHinhMatGi'),
                        nguyHiem: true,
                        onDongY: () => xoa.mutate(),
                      })
                    }
                  >
                    {t('email.xoaCauHinh')}
                  </Button>
                )}
              </div>
            )}
          </form>
        </CardContent>
      </Card>

      {/*
        Gửi thử là BẮT BUỘC chứ không phải tiện ích: nhập sai cấu hình thì email không đi mà
        lỗi chỉ nằm trong log server — người dùng không có cách nào biết cho tới khi học viên
        phàn nàn.
      */}
      {coQuyen('ThietLapEmail', 'GuiThu') && (
        <Card>
          <CardContent className="space-y-3 pt-6">
            <div>
              <h2 className="text-sm font-semibold">{t('email.guiThuTieuDe')}</h2>
              <p className="text-xs text-muted-foreground">{t('email.guiThuMoTa')}</p>
            </div>
            <div className="flex flex-wrap items-end gap-2">
              <div className="min-w-[16rem] flex-1 space-y-1.5">
                <Label htmlFor="emailThu">{t('email.denDiaChi')}</Label>
                <Input
                  id="emailThu" type="email" value={emailThu}
                  onChange={(e) => setEmailThu(e.target.value)}
                  placeholder="ban@example.com"
                />
              </div>
              <Button
                type="button" variant="outline"
                disabled={guiThu.isPending || emailThu.trim() === ''}
                onClick={() => guiThu.mutate(emailThu.trim())}
              >
                {guiThu.isPending ? t('email.dangGui') : t('email.guiThu')}
              </Button>
              {daGuiThu && (
                <span className="text-sm text-muted-foreground">{t('email.daGuiThu')}</span>
              )}
            </div>
          </CardContent>
        </Card>
      )}
      {hop}
    </div>
  )
}

// ---------- Tab 2: mẫu nội dung ----------

function TabMau() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const [dangChon, setDangChon] = useState<string | null>(null)
  const [tieuDe, setTieuDe] = useState('')
  const [noiDung, setNoiDung] = useState('')
  const [dangDung, setDangDung] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)

  const duocSua = coQuyen('MauEmail', 'Sua')

  const { data: ds, isLoading } = useQuery({
    queryKey: ['email-mau'],
    queryFn: async () => (await api.get<MauEmailDto[]>('/email/mau')).data,
  })

  const mau = ds?.find((m) => m.loai === dangChon) ?? null

  // Chọn mẫu đầu tiên khi tải xong, và nạp nội dung mỗi khi đổi mẫu.
  //
  // Đặt trong `useEffect` chứ không tính trong thân render: gọi `setState` lúc render là vòng
  // lặp vô hạn, còn `useQuery` phải chạy trước mọi nhánh `return` để không phạm luật thứ tự
  // hook — TypeScript không bắt được lỗi này.
  useEffect(() => {
    if (!ds?.length) return
    const chon = ds.find((m) => m.loai === dangChon) ?? ds[0]
    if (chon.loai !== dangChon) setDangChon(chon.loai)
    setTieuDe(chon.tieuDe)
    setNoiDung(chon.noiDungHtml)
    setDangDung(chon.dangDung)
  }, [ds, dangChon])

  const luu = useMutation({
    mutationFn: (m: { loai: string; tieuDe: string; noiDungHtml: string; dangDung: boolean }) =>
      api.put(`/email/mau/${m.loai}`, {
        tieuDe: m.tieuDe,
        noiDungHtml: m.noiDungHtml,
        dangDung: m.dangDung,
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['email-mau'] })
      setMaLoi(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (loai: string) => api.delete(`/email/mau/${loai}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['email-mau'] })
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
  if (!ds?.length || !mau) return null

  /** Chèn biến vào cuối tiêu đề — nội dung thì người dùng tự gõ ở chỗ con trỏ. */
  const chenVaoTieuDe = (bien: string) => setTieuDe((cu) => `${cu}{{${bien}}}`)

  return (
    <div className="space-y-4">
      {/* Danh sách loại mẫu. Luôn đủ mọi loại, kể cả loại chưa soạn. */}
      <div className="flex flex-wrap gap-1.5">
        {ds.map((m) => (
          <button
            key={m.loai}
            type="button"
            onClick={() => setDangChon(m.loai)}
            className={
              'rounded-md border px-3 py-1.5 text-sm transition-colors ' +
              (m.loai === dangChon
                ? 'border-primary bg-primary/10 font-medium'
                : 'border-input hover:bg-muted')
            }
          >
            {t(`email.loai.${m.loai}`)}
            {!m.daSoan && (
              <span className="ml-1.5 text-xs text-muted-foreground">
                {t('email.chuaSoan')}
              </span>
            )}
          </button>
        ))}
      </div>

      {/*
        Mẫu chưa có chỗ gọi tự động thì phải nói rõ. Không có cảnh báo này, trung tâm soạn
        xong rồi ngồi đợi một email không bao giờ được gửi.
      */}
      {!mau.tuGuiDuoc && (
        <div className="rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm">
          {t('email.chuaTuGui')}
        </div>
      )}

      <Card>
        <CardContent className="space-y-4 pt-6">
          <div className="space-y-1.5">
            <Label htmlFor="tieuDeMau">{t('email.tieuDeThu')}</Label>
            <Input
              id="tieuDeMau" value={tieuDe} maxLength={300} disabled={!duocSua}
              onChange={(e) => setTieuDe(e.target.value)}
            />
          </div>

          {/* Danh sách biến dùng được — do backend trả, không hard-code ở đây. */}
          <div className="space-y-1.5">
            <Label>{t('email.bienDungDuoc')}</Label>
            <div className="flex flex-wrap gap-1.5">
              {mau.bien.map((b) => (
                <button
                  key={b}
                  type="button"
                  disabled={!duocSua}
                  onClick={() => chenVaoTieuDe(b)}
                  title={t('email.chenVaoTieuDe')}
                  className="rounded border border-input bg-muted/50 px-2 py-0.5 font-mono text-xs hover:bg-muted disabled:opacity-50"
                >
                  {`{{${b}}}`}
                </button>
              ))}
            </div>
            <p className="text-xs text-muted-foreground">{t('email.bienGhiChu')}</p>
          </div>

          <div className="space-y-1.5">
            <Label>{t('email.noiDungThu')}</Label>
            {duocSua ? (
              <SoanThao giaTri={noiDung} onDoi={setNoiDung} />
            ) : (
              <div
                className="soan-thao rounded-md border border-input px-3 py-2"
                // Chỉ đọc, và nội dung do chính quản trị viên của trung tâm soạn — không
                // phải dữ liệu từ người ngoài.
                dangerouslySetInnerHTML={{ __html: noiDung }}
              />
            )}
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox" checked={dangDung} disabled={!duocSua}
              onChange={(e) => setDangDung(e.target.checked)}
              className="h-4 w-4 rounded border-input"
            />
            {t('email.dangDung')}
          </label>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          {duocSua && (
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button" disabled={luu.isPending}
                onClick={() =>
                  luu.mutate({ loai: mau.loai, tieuDe, noiDungHtml: noiDung, dangDung })
                }
              >
                {luu.isPending ? t('chung.luu') : t('chung.luu')}
              </Button>
              {daLuu && (
                <span className="text-sm text-muted-foreground">{t('chung.daLuu')}</span>
              )}
              {mau.daSoan && coQuyen('MauEmail', 'Xoa') && (
                <Button
                  type="button" variant="outline"
                  onClick={() =>
                    hoi({
                      tieuDe: t('email.veMacDinh'),
                      thongDiep: t('email.veMacDinhMatGi'),
                      nguyHiem: true,
                      onDongY: () => xoa.mutate(mau.loai),
                    })
                  }
                >
                  {t('email.veMacDinh')}
                </Button>
              )}
            </div>
          )}
        </CardContent>
      </Card>
      {hop}
    </div>
  )
}
