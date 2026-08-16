import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Plus, Check, X, Trash2 } from 'lucide-react'
import { api, layMaLoi, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

type TrangThaiLoiMoi = 'ChoPhanHoi' | 'DaChapNhan' | 'DaTuChoi'

interface LoiMoiDto {
  id: string
  doiThuId: string
  tenDoiThu: string
  thoiGianDeXuat: string
  trangThai: TrangThaiLoiMoi
  ghiChu: string | null
  tranDauId: string | null
}
interface DoiThuNgan {
  id: string
  tenDoi: string
}

/** FR-09 — lời mời giao hữu từ đối thủ. */
export default function LoiMoi() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [moForm, setMoForm] = useState(false)
  const [doiThuChon, setDoiThuChon] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['loi-moi'],
    queryFn: async () => (await api.get<LoiMoiDto[]>('/loi-moi')).data,
  })
  const { data: doiThus } = useQuery({
    queryKey: ['doi-thu'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<DoiThuNgan>>('/doi-thu', { params: { soDong: 200 } })).data.duLieu,
  })

  const tao = useMutation({
    mutationFn: async (form: Record<string, unknown>) => api.post('/loi-moi', form),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['loi-moi'] })
      setMoForm(false)
      setDoiThuChon(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const chapNhan = useMutation({
    mutationFn: async (id: string) => (await api.post<string>(`/loi-moi/${id}/chap-nhan`)).data,
    onSuccess: (tranDauId) => {
      void qc.invalidateQueries({ queryKey: ['loi-moi'] })
      void qc.invalidateQueries({ queryKey: ['tran-dau'] })
      // Đưa thẳng sang trận vừa sinh: đó là việc người dùng làm tiếp theo.
      navigate(`/lich-thi-dau/${tranDauId}`)
    },
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const tuChoi = useMutation({
    mutationFn: async (id: string) => api.post(`/loi-moi/${id}/tu-choi`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['loi-moi'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/loi-moi/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['loi-moi'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const mauTrangThai: Record<TrangThaiLoiMoi, 'draw' | 'win' | 'lose'> = {
    ChoPhanHoi: 'draw',
    DaChapNhan: 'win',
    DaTuChoi: 'lose',
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Button
          onClick={() => {
            setDoiThuChon(null)
            setMaLoi(null)
            setMoForm(true)
          }}
        >
          <Plus className="h-4 w-4" />
          {t('loiMoi.themMoi')}
        </Button>
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data?.length ? (
        <TrangTrong thongDiep={t('loiMoi.chuaCo')} />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('loiMoi.doiThu')}</Th>
              <Th>{t('loiMoi.thoiGianDeXuat')}</Th>
              <Th>{t('cauThu.ghiChu')}</Th>
              <Th>{t('loiMoi.trangThai')}</Th>
              <Th className="w-32" />
            </tr>
          </thead>
          <tbody>
            {data.map((lm) => (
              <tr key={lm.id} className="hover:bg-muted/40">
                <Td className="font-medium">{lm.tenDoiThu}</Td>
                <Td className="whitespace-nowrap">
                  {new Date(lm.thoiGianDeXuat).toLocaleString('vi-VN', {
                    day: '2-digit', month: '2-digit', year: 'numeric',
                    hour: '2-digit', minute: '2-digit',
                  })}
                </Td>
                <Td className="text-muted-foreground">{lm.ghiChu ?? '—'}</Td>
                <Td>
                  <Badge variant={mauTrangThai[lm.trangThai]}>
                    {t(`loiMoi.tt.${lm.trangThai}`)}
                  </Badge>
                </Td>
                <Td>
                  <div className="flex gap-1">
                    {lm.trangThai === 'ChoPhanHoi' ? (
                      <>
                        <Button
                          variant="ghost"
                          size="sm"
                          title={t('loiMoi.chapNhan')}
                          disabled={chapNhan.isPending}
                          onClick={() => {
                            setMaLoiBang(null)
                            chapNhan.mutate(lm.id)
                          }}
                        >
                          <Check className="h-3.5 w-3.5 text-status-win" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          title={t('loiMoi.tuChoi')}
                          onClick={() => {
                            setMaLoiBang(null)
                            tuChoi.mutate(lm.id)
                          }}
                        >
                          <X className="h-3.5 w-3.5 text-destructive" />
                        </Button>
                      </>
                    ) : lm.tranDauId ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => navigate(`/lich-thi-dau/${lm.tranDauId}`)}
                      >
                        {t('loiMoi.xemTran')}
                      </Button>
                    ) : (
                      <Button
                        variant="ghost"
                        size="sm"
                        title={t('chung.xoa')}
                        onClick={() => {
                          setMaLoiBang(null)
                          if (confirm(t('chung.xacNhanXoa'))) xoa.mutate(lm.id)
                        }}
                      >
                        <Trash2 className="h-3.5 w-3.5 text-destructive" />
                      </Button>
                    )}
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        chanDoiKhiXuLy={tao.isPending}
        tieuDe={t('loiMoi.themMoi')}
      >
        <form
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            tao.mutate({
              doiThuId: doiThuChon,
              thoiGianDeXuat: new Date(String(fd.get('thoiGianDeXuat'))).toISOString(),
              ghiChu: (fd.get('ghiChu') as string) || null,
            })
          }}
          className="flex flex-col gap-4"
        >
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="doiThuId">{t('loiMoi.doiThu')}</Label>
            <SelectTimKiem
              id="doiThuId"
              luaChon={(doiThus ?? []).map((d) => ({ giaTri: d.id, nhan: d.tenDoi }))}
              giaTri={doiThuChon}
              onDoi={setDoiThuChon}
              placeholder={t('tranDau.chuaChonDoiThu')}
              placeholderTimKiem={t('tranDau.timDoiThu')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="thoiGianDeXuat">{t('loiMoi.thoiGianDeXuat')}</Label>
            <Input id="thoiGianDeXuat" name="thoiGianDeXuat" type="datetime-local" required />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
            <Input id="ghiChu" name="ghiChu" placeholder="VD: Sân Hòa Xuân" />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button
              type="button"
              variant="outline"
              onClick={() => setMoForm(false)}
              disabled={tao.isPending}
            >
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={tao.isPending || !doiThuChon}>
              {tao.isPending ? t('chung.dangTai') : t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>
    </div>
  )
}
