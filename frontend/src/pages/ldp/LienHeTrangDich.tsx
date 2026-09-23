import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2, UserPlus } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { useQuyen } from '@/lib/quyen'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, CardHeader, CardTitle, Table, Td, Th,
} from '@/components/ui'
import { HopXacNhan } from '@/components/ui/HopXacNhan'

interface LienHe {
  id: string
  hoTen: string
  soDienThoai: string
  email: string | null
  quanTam: string | null
  loiNhan: string | null
  daXuLy: boolean
  khachHangId: string | null
  ngayGui: string
}

/**
 * FR-30 — liên hệ khách vãng lai để lại qua form trang đích.
 *
 * Bước trung gian trước CRM là có chủ ý: form là endpoint ẩn danh nên nó nhận cả bot và rác.
 * Người phụ trách đọc rồi mới bấm chuyển — xem FR-30.
 */
export default function LienHeTrangDich() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [xoaId, setXoaId] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['ldp', 'lien-he'],
    queryFn: async () => (await api.get<LienHe[]>('/ldp/lien-he')).data,
  })

  const lamMoi = () => qc.invalidateQueries({ queryKey: ['ldp', 'lien-he'] })

  const chuyen = useMutation({
    mutationFn: async (id: string) => api.post(`/ldp/lien-he/${id}/chuyen-crm`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/ldp/lien-he/${id}`),
    onSuccess: () => { setXoaId(null); lamMoi() },
    onError: (e) => { setXoaId(null); setMaLoi(layMaLoi(e)) },
  })

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">{t('menu.lienHeTrangDich')}</h1>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardHeader>
          <CardTitle>{t('ldp.danhSachLienHe')} ({data?.length ?? 0})</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
          ) : data?.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('ldp.chuaCoLienHe')}</p>
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>{t('ldp.hoTen')}</Th>
                  <Th>{t('ldp.soDienThoai')}</Th>
                  <Th>{t('ldp.quanTam')}</Th>
                  <Th>{t('ldp.loiNhan')}</Th>
                  <Th>{t('ldp.ngayGui')}</Th>
                  <Th> </Th>
                </tr>
              </thead>
              <tbody>
                {data?.map((l) => (
                  <tr key={l.id}>
                    <Td>{l.hoTen}</Td>
                    <Td><span className="font-mono">{l.soDienThoai}</span></Td>
                    <Td>{l.quanTam ?? '—'}</Td>
                    <Td className="max-w-xs truncate">{l.loiNhan ?? '—'}</Td>
                    <Td>{new Date(l.ngayGui).toLocaleDateString()}</Td>
                    <Td>
                      <div className="flex items-center gap-1">
                        {l.khachHangId ? (
                          <Badge>{t('ldp.daChuyenCrm')}</Badge>
                        ) : (
                          coQuyen('LienHeLanding', 'ChuyenCrm') && (
                            <Button
                              variant="outline" size="sm"
                              disabled={chuyen.isPending}
                              onClick={() => chuyen.mutate(l.id)}
                            >
                              <UserPlus className="h-4 w-4" />
                              {t('ldp.chuyenCrm')}
                            </Button>
                          )
                        )}
                        {coQuyen('LienHeLanding', 'Xoa') && (
                          <Button
                            variant="ghost" size="sm"
                            title={t('chung.xoa')}
                            onClick={() => setXoaId(l.id)}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        )}
                      </div>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}
        </CardContent>
      </Card>

      <HopXacNhan
        mo={xoaId !== null}
        tieuDe={t('ldp.xacNhanXoaLienHe')}
        thongDiep={t('ldp.xoaLienHeMatGi', {
          ten: data?.find((x) => x.id === xoaId)?.hoTen ?? '',
        })}
        nguyHiem
        onHuy={() => setXoaId(null)}
        onDongY={() => xoaId && xoa.mutate(xoaId)}
      />
    </div>
  )
}
