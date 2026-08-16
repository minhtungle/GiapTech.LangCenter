import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Search, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'

interface CauThuDto {
  id: string
  hoTen: string
  anhDaiDien: string | null
  ngaySinh: string | null
  ngayThamGia: string | null
  ghiChu: string | null
  coTaiKhoan: boolean
}

/** FR-04 — CRUD hồ sơ cầu thủ. */
export default function CauThu() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [timKiem, setTimKiem] = useState('')
  const [dangSua, setDangSua] = useState<CauThuDto | null>(null)
  const [moForm, setMoForm] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['cau-thu', timKiem],
    queryFn: async () =>
      (await api.get<CauThuDto[]>('/cau-thu', { params: { timKiem: timKiem || undefined } })).data,
  })

  const luu = useMutation({
    mutationFn: async (form: Partial<CauThuDto>) => {
      if (dangSua) await api.put(`/cau-thu/${dangSua.id}`, { ...form, id: dangSua.id })
      else await api.post('/cau-thu', form)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['cau-thu'] })
      setMoForm(false)
      setDangSua(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/cau-thu/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['cau-thu'] }),
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    luu.mutate({
      hoTen: String(fd.get('hoTen')),
      ngaySinh: (fd.get('ngaySinh') as string) || null,
      ngayThamGia: (fd.get('ngayThamGia') as string) || null,
      ghiChu: (fd.get('ghiChu') as string) || null,
    })
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="relative max-w-xs flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            className="pl-8"
            placeholder={t('chung.timKiem')}
            value={timKiem}
            onChange={(e) => setTimKiem(e.target.value)}
          />
        </div>
        <Button
          onClick={() => {
            setDangSua(null)
            setMoForm(true)
            setMaLoi(null)
          }}
        >
          <Plus className="h-4 w-4" />
          {t('cauThu.themMoi')}
        </Button>
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {moForm && (
        <Card>
          <CardContent className="pt-5">
            <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="hoTen">{t('cauThu.hoTen')}</Label>
                <Input id="hoTen" name="hoTen" defaultValue={dangSua?.hoTen} required autoFocus />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="ngaySinh">{t('cauThu.ngaySinh')}</Label>
                <Input
                  id="ngaySinh"
                  name="ngaySinh"
                  type="date"
                  defaultValue={dangSua?.ngaySinh ?? ''}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="ngayThamGia">{t('cauThu.ngayThamGia')}</Label>
                <Input
                  id="ngayThamGia"
                  name="ngayThamGia"
                  type="date"
                  defaultValue={dangSua?.ngayThamGia ?? ''}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
                <Input id="ghiChu" name="ghiChu" defaultValue={dangSua?.ghiChu ?? ''} />
              </div>
              <div className="flex gap-2 sm:col-span-2">
                <Button type="submit" disabled={luu.isPending}>
                  {t('chung.luu')}
                </Button>
                <Button type="button" variant="outline" onClick={() => setMoForm(false)}>
                  {t('chung.huy')}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data?.length ? (
        <TrangTrong
          thongDiep={t('cauThu.chuaCo')}
          hanhDong={
            <Button onClick={() => setMoForm(true)}>
              <Plus className="h-4 w-4" />
              {t('cauThu.themMoi')}
            </Button>
          }
        />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('cauThu.hoTen')}</Th>
              <Th>{t('cauThu.ngaySinh')}</Th>
              <Th>{t('cauThu.ngayThamGia')}</Th>
              <Th>{t('cauThu.coTaiKhoan')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {data.map((c) => (
              <tr key={c.id} className="hover:bg-muted/40">
                <Td className="font-medium">{c.hoTen}</Td>
                <Td className="text-muted-foreground">{c.ngaySinh ?? '—'}</Td>
                <Td className="text-muted-foreground">{c.ngayThamGia ?? '—'}</Td>
                <Td>
                  {c.coTaiKhoan ? (
                    <Badge variant="win">{t('cauThu.coTaiKhoan')}</Badge>
                  ) : (
                    <Badge>{t('taiKhoan.chuaGan')}</Badge>
                  )}
                </Td>
                <Td>
                  <div className="flex gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setDangSua(c)
                        setMoForm(true)
                        setMaLoi(null)
                      }}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        if (confirm(t('chung.xacNhanXoa'))) xoa.mutate(c.id)
                      }}
                    >
                      <Trash2 className="h-3.5 w-3.5 text-destructive" />
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </div>
  )
}
