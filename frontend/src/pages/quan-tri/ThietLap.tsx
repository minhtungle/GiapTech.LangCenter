import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, layMaLoi } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { Button, CanhBaoLoi, Card, CardContent, Input, Label, Textarea,
} from '@/components/ui'
import { cn } from '@/lib/utils'
import { BANG_MAU_AO } from '@/components/soDo/loaiSan'

interface ThietLapDto {
  id: string
  maDoi: string
  tenDoi: string
  tenVietTat: string | null
  ngayThanhLap: string | null
  logoUrl: string | null
  anhBiaUrl: string | null
  moTa: string | null
  /** Bộ áo đấu của CLB — mã màu trong BANG_MAU_AO. */
  mauAo: string[]
}

/** FR-06 — thiết lập chung CLB. */
export default function ThietLap() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { capNhatTenDoi } = useAuth()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)
  /** null = chưa chạm, lấy giá trị server. */
  const [mauAo, setMauAo] = useState<string[] | null>(null)

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
      setMauAo(null)
      // Tên đội nằm trong JWT nên token đang cầm vẫn mang tên cũ tới lần làm mới kế tiếp;
      // không đồng bộ thì sidebar hiện tên cũ dù người dùng vừa đổi xong.
      if (form.tenDoi) capNhatTenDoi(form.tenDoi)
      setMaLoi(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  const dangChonMau = mauAo ?? data?.mauAo ?? []

  const bat = (ma: string) =>
    setMauAo(
      dangChonMau.includes(ma)
        ? dangChonMau.filter((m) => m !== ma)
        : [...dangChonMau, ma],
    )

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    // Mọi trường lệnh cập nhật ghi đè đều gửi lại (quy tắc #1) — kể cả logo/ảnh bìa chưa có
    // ô trên form, nếu không mỗi lần lưu sẽ xoá chúng.
    luu.mutate({
      tenDoi: String(fd.get('tenDoi')),
      tenVietTat: (fd.get('tenVietTat') as string) || null,
      ngayThanhLap: (fd.get('ngayThanhLap') as string) || null,
      moTa: (fd.get('moTa') as string) || null,
      logoUrl: data?.logoUrl ?? null,
      anhBiaUrl: data?.anhBiaUrl ?? null,
      mauAo: dangChonMau,
    })
  }

  return (
    <Card className="max-w-2xl">
      <CardContent className="pt-5">
        <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="maDoi">{t('thietLap.maDoi')}</Label>
            <Input id="maDoi" value={data?.maDoi ?? ''} disabled />
            <p className="text-xs text-muted-foreground">{t('thietLap.maDoiKhongDoi')}</p>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenDoi">{t('thietLap.tenDoi')}</Label>
            <Input id="tenDoi" name="tenDoi" defaultValue={data?.tenDoi} required />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenVietTat">{t('thietLap.tenVietTat')}</Label>
            <Input id="tenVietTat" name="tenVietTat" defaultValue={data?.tenVietTat ?? ''} />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ngayThanhLap">{t('thietLap.ngayThanhLap')}</Label>
            <Input
              id="ngayThanhLap"
              name="ngayThanhLap"
              type="date"
              defaultValue={data?.ngayThanhLap ?? ''}
            />
          </div>

          {/*
            Bộ áo đấu — bảng chiến thuật chỉ cho chọn trong bộ này. Khai ở đây một lần thay vì
            mỗi trận chọn lại một màu khác, xem lại lịch sử không nhận ra đội mình mặc gì.
          */}
          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label>{t('thietLap.mauAo')}</Label>
            <div className="flex flex-wrap gap-2">
              {BANG_MAU_AO.map((m) => {
                const daChon = dangChonMau.includes(m.ma)
                return (
                  <button
                    key={m.ma}
                    type="button"
                    onClick={() => bat(m.ma)}
                    aria-pressed={daChon}
                    aria-label={t(`soDo.mau.${m.ma}`)}
                    className={cn(
                      'flex items-center gap-2 rounded-md border px-2 py-1.5 text-xs transition-colors',
                      daChon
                        ? 'border-[hsl(var(--accent))] bg-[hsl(var(--accent))]/10 font-medium'
                        : 'border-border hover:bg-muted',
                    )}
                  >
                    <span
                      style={{ background: m.nen }}
                      className="h-4 w-4 rounded-full border border-white/70 shadow-sm"
                    />
                    {t(`soDo.mau.${m.ma}`)}
                  </button>
                )
              })}
            </div>
            <p className="text-xs text-muted-foreground">
              {dangChonMau.length === 0 ? t('thietLap.mauAoChuaChon') : t('thietLap.mauAoGoiY')}
            </p>
          </div>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="moTa">{t('thietLap.moTa')}</Label>
            <Textarea id="moTa" name="moTa" defaultValue={data?.moTa ?? ''} />
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
            {daLuu && <span className="text-sm text-status-win">Đã lưu</span>}
          </div>
        </form>
      </CardContent>
    </Card>
  )
}
