import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, layMaLoi } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { Button, CanhBaoLoi, Card, CardContent, Input, Label } from '@/components/ui'

interface ThietLapDto {
  id: string
  maDoi: string
  tenDoi: string
  tenVietTat: string | null
  ngayThanhLap: string | null
  logoUrl: string | null
  anhBiaUrl: string | null
  moTa: string | null
}

/** FR-06 — thiết lập chung CLB. */
export default function ThietLap() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { capNhatTenDoi } = useAuth()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)

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

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    luu.mutate({
      tenDoi: String(fd.get('tenDoi')),
      tenVietTat: (fd.get('tenVietTat') as string) || null,
      ngayThanhLap: (fd.get('ngayThanhLap') as string) || null,
      moTa: (fd.get('moTa') as string) || null,
      logoUrl: data?.logoUrl ?? null,
      anhBiaUrl: data?.anhBiaUrl ?? null,
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

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="moTa">{t('thietLap.moTa')}</Label>
            <Input id="moTa" name="moTa" defaultValue={data?.moTa ?? ''} />
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
