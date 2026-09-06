import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Trash2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Button, CanhBaoLoi, Label, Table, Td, Th, TrangTrong } from '@/components/ui'
import { KhungNoiDung } from '@/components/ui/KhungNoiDung'
import { SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import {
  tienVN, ngayVN,
  type LopHocDto, type NguoiDungNgan, type HocVienTrongLop,
} from './lopHocTypes'


/** Bước 3 của wizard, cũng dùng lại làm màn quản lý học viên của lớp. */
export function HocVienCuaLop({
  lop,
  nguoiDungs,
  onDong,
  nhung,
}: {
  lop: LopHocDto
  nguoiDungs: NguoiDungNgan[]
  onDong: () => void
  /** true = đang là tab trong view chi tiết lớp, không bọc Modal. */
  nhung?: boolean
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [chon, setChon] = useState<string[]>([])
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: hocViens = [] } = useQuery({
    queryKey: ['lop-hoc', lop.id, 'hoc-vien'],
    queryFn: async () =>
      (await api.get<HocVienTrongLop[]>(`/lop-hoc/${lop.id}/hoc-vien`)).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['lop-hoc', lop.id, 'hoc-vien'] })
    void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
  }

  const them = useMutation({
    mutationFn: () => api.post(`/lop-hoc/${lop.id}/hoc-vien`, { hocVienIds: chon }),
    onSuccess: () => {
      setChon([])
      setMaLoi(null)
      lamMoi()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const go = useMutation({
    mutationFn: (hocVienId: string) =>
      api.delete(`/lop-hoc/${lop.id}/hoc-vien/${hocVienId}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const daTrongLop = new Set(hocViens.map((h) => h.hocVienId))
  const luaChon = nguoiDungs
    .filter((u) => u.loaiNguoiDung === 'HocVien' && !daTrongLop.has(u.id))
    .map((u) => ({ giaTri: u.id, nhan: u.hoTen, phu: u.email ?? undefined }))

  return (
    <KhungNoiDung nhung={nhung} onDong={onDong} tieuDe={`${t('lopHoc.hocVien')} — ${lop.ten}`}>
      <div className="grid gap-4">
        <div className="flex flex-wrap items-end gap-2">
          <div className="flex min-w-64 flex-1 flex-col gap-1.5">
            <Label htmlFor="themHocVien">{t('lopHoc.themHocVien')}</Label>
            <SelectTimKiemNhieu
              id="themHocVien"
              luaChon={luaChon}
              giaTri={chon}
              onDoi={setChon}
              placeholder={t('lopHoc.timHocVien')}
              placeholderTimKiem={t('lopHoc.timHocVien')}
            />
          </div>
          <Button disabled={chon.length === 0 || them.isPending} onClick={() => them.mutate()}>
            {t('chung.them')}
          </Button>
        </div>

        <p className="text-xs text-muted-foreground">
          {lop.sucChuaToiDa === null
            ? t('lopHoc.daChon', { soLuong: hocViens.length })
            : t('lopHoc.sucChuaConLai', {
                daChon: hocViens.length,
                toiDa: lop.sucChuaToiDa,
              })}
        </p>

        {maLoi && (
          <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
        )}

        {hocViens.length === 0 ? (
          <TrangTrong thongDiep={t('chung.khongCoDuLieu')} />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>{t('taiKhoan.hoTen')}</Th>
                <Th>{t('taiKhoan.email')}</Th>
                <Th>{t('lopHoc.ngayVaoLop')}</Th>
                <Th>{t('lopHoc.hocPhiApDung')}</Th>
                <Th className="w-16" />
              </tr>
            </thead>
            <tbody>
              {hocViens.map((h) => (
                <tr key={h.id} className="hover:bg-muted/40">
                  <Td className="font-medium">{h.hoTen}</Td>
                  <Td className="text-muted-foreground">{h.email ?? '—'}</Td>
                  <Td className="text-muted-foreground">{ngayVN(h.ngayVaoLop)}</Td>
                  <Td className="text-muted-foreground">{tienVN(h.hocPhiApDung)}</Td>
                  <Td>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('lopHoc.goHocVien')}
                      onClick={() => go.mutate(h.hocVienId)}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </div>
    </KhungNoiDung>
  )
}
