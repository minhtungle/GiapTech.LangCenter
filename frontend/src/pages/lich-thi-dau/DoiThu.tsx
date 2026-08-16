import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Pencil, Trash2, Search } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'

interface DoiThuDto {
  id: string
  tenDoi: string
  lienHe: string | null
  ghiChu: string | null
  soTranDaDau: number
}

/** Sổ đối thủ — nền cho FR-09 (lời mời) và FR-10 (trận đấu). */
export default function DoiThu() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [timKiem, setTimKiem] = useState('')
  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<DoiThuDto | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: ketQua, isLoading } = useQuery({
    queryKey: ['doi-thu', timKiem, trang, soDong],
    queryFn: async () =>
      (
        await api.get<KetQuaTrang<DoiThuDto>>('/doi-thu', {
          params: { timKiem: timKiem || undefined, trang, soDong },
        })
      ).data,
  })

  const kq = ketQua ?? trangRong<DoiThuDto>()
  const data = kq.duLieu

  const luu = useMutation({
    mutationFn: async (form: Record<string, unknown>) => {
      if (dangSua) await api.put(`/doi-thu/${dangSua.id}`, { ...form, id: dangSua.id })
      else await api.post('/doi-thu', form)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['doi-thu'] })
      dongForm()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/doi-thu/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['doi-thu'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moThem = () => {
    setDangSua(null)
    setMaLoi(null)
    setMoForm(true)
  }
  const moSua = (d: DoiThuDto) => {
    setDangSua(d)
    setMaLoi(null)
    setMoForm(true)
  }
  const dongForm = () => {
    setMoForm(false)
    setDangSua(null)
    setMaLoi(null)
  }

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    luu.mutate({
      tenDoi: String(fd.get('tenDoi')),
      lienHe: (fd.get('lienHe') as string) || null,
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
            onChange={(e) => {
              setTimKiem(e.target.value)
              setTrang(1)
            }}
          />
        </div>
        <Button onClick={moThem}>
          <Plus className="h-4 w-4" />
          {t('doiThu.themMoi')}
        </Button>
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data.length ? (
        <TrangTrong
          thongDiep={timKiem ? t('chung.khongCoDuLieu') : t('doiThu.chuaCo')}
          hanhDong={
            !timKiem && (
              <Button onClick={moThem}>
                <Plus className="h-4 w-4" />
                {t('doiThu.themMoi')}
              </Button>
            )
          }
        />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('doiThu.tenDoi')}</Th>
              <Th>{t('doiThu.lienHe')}</Th>
              <Th>{t('doiThu.soTran')}</Th>
              <Th>{t('cauThu.ghiChu')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {data.map((d) => (
              <tr key={d.id} className="hover:bg-muted/40">
                <Td className="font-medium">{d.tenDoi}</Td>
                <Td className="text-muted-foreground">{d.lienHe ?? '—'}</Td>
                <Td>
                  <Badge variant={d.soTranDaDau > 0 ? 'accent' : 'muted'}>{d.soTranDaDau}</Badge>
                </Td>
                <Td className="text-muted-foreground">{d.ghiChu ?? '—'}</Td>
                <Td>
                  <div className="flex gap-1">
                    <Button variant="ghost" size="sm" title={t('chung.sua')} onClick={() => moSua(d)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.xoa')}
                      onClick={() => {
                        setMaLoiBang(null)
                        if (confirm(t('chung.xacNhanXoa'))) xoa.mutate(d.id)
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

      {data.length > 0 && (
        <PhanTrang
          trang={kq.trang}
          soDong={kq.soDong}
          tongSoDong={kq.tongSoDong}
          tongSoTrang={kq.tongSoTrang}
          onDoiTrang={setTrang}
          onDoiSoDong={(n) => {
            setSoDong(n)
            setTrang(1)
          }}
        />
      )}

      <Modal
        mo={moForm}
        onDong={dongForm}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('doiThu.suaTieuDe') : t('doiThu.themMoi')}
        moTa={dangSua?.tenDoi}
      >
        <form key={dangSua?.id ?? 'moi'} onSubmit={onSubmit} className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenDoi">{t('doiThu.tenDoi')}</Label>
            <Input id="tenDoi" name="tenDoi" required autoFocus defaultValue={dangSua?.tenDoi} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="lienHe">{t('doiThu.lienHe')}</Label>
            <Input id="lienHe" name="lienHe" defaultValue={dangSua?.lienHe ?? ''} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
            <Input id="ghiChu" name="ghiChu" defaultValue={dangSua?.ghiChu ?? ''} />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button type="button" variant="outline" onClick={dongForm} disabled={luu.isPending}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>
              {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>
    </div>
  )
}
