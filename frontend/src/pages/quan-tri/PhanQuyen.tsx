import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Th,
} from '@/components/ui'

interface ChucNangDto {
  tenChucNang: string
  hanhDongs: string[]
}
interface QuyenDto {
  id: string
  tenQuyen: string
  moTa: string | null
  soTaiKhoan: number
  chucNangs: ChucNangDto[]
}
interface DanhMucDto {
  chucNangs: string[]
  hanhDongs: string[]
}

/** FR-05 — nhóm quyền với ma trận chức năng × thao tác. */
export default function PhanQuyen() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [dangSua, setDangSua] = useState<QuyenDto | null>(null)
  const [moForm, setMoForm] = useState(false)
  const [tenQuyen, setTenQuyen] = useState('')
  const [oDaChon, setODaChon] = useState<Set<string>>(new Set())
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: danhMuc } = useQuery({
    queryKey: ['quyen-danh-muc'],
    queryFn: async () => (await api.get<DanhMucDto>('/quyen/danh-muc')).data,
  })

  const { data: quyens, isLoading } = useQuery({
    queryKey: ['quyen'],
    queryFn: async () => (await api.get<QuyenDto[]>('/quyen')).data,
  })

  const khoaO = (cn: string, hd: string) => `${cn}:${hd}`

  const moFormVoi = (q: QuyenDto | null) => {
    setDangSua(q)
    setTenQuyen(q?.tenQuyen ?? '')
    setODaChon(
      new Set(
        q?.chucNangs.flatMap((c) => c.hanhDongs.map((h) => khoaO(c.tenChucNang, h))) ?? [],
      ),
    )
    setMoForm(true)
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async () => {
      // Gom các ô đã tick thành ma trận { chức năng → danh sách thao tác }.
      const theoChucNang = new Map<string, string[]>()
      for (const o of oDaChon) {
        const [cn, hd] = o.split(':')
        theoChucNang.set(cn, [...(theoChucNang.get(cn) ?? []), hd])
      }
      const body = {
        tenQuyen,
        moTa: null,
        chucNangs: [...theoChucNang].map(([tenChucNang, hanhDongs]) => ({
          tenChucNang,
          hanhDongs,
        })),
      }
      if (dangSua) await api.put(`/quyen/${dangSua.id}`, { ...body, id: dangSua.id })
      else await api.post('/quyen', body)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['quyen'] })
      setMoForm(false)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/quyen/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['quyen'] }),
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Button onClick={() => moFormVoi(null)}>
          <Plus className="h-4 w-4" />
          {t('quyen.themMoi')}
        </Button>
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {moForm && danhMuc && (
        <Card>
          <CardContent className="flex flex-col gap-4 pt-5">
            <div className="flex max-w-sm flex-col gap-1.5">
              <Label htmlFor="tenQuyen">{t('quyen.tenQuyen')}</Label>
              <Input
                id="tenQuyen"
                value={tenQuyen}
                onChange={(e) => setTenQuyen(e.target.value)}
                autoFocus
              />
            </div>

            {/* Ma trận chức năng × thao tác — ưu tiên desktop (nguyên tắc UI/UX mục 3) */}
            <div>
              <Label>{t('quyen.maTran')}</Label>
              <div className="mt-2">
                <Table>
                  <thead>
                    <tr>
                      <Th>{t('quyen.chucNang')}</Th>
                      {danhMuc.hanhDongs.map((hd) => (
                        <Th key={hd} className="w-20 text-center">
                          {t(`hanhDong.${hd}`, hd)}
                        </Th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {danhMuc.chucNangs.map((cn) => (
                      <tr key={cn} className="hover:bg-muted/40">
                        <Td className="font-medium">{t(`chucNang.${cn}`, cn)}</Td>
                        {danhMuc.hanhDongs.map((hd) => {
                          const khoa = khoaO(cn, hd)
                          return (
                            <Td key={hd} className="text-center">
                              <input
                                type="checkbox"
                                className="h-4 w-4 accent-[hsl(var(--primary))]"
                                checked={oDaChon.has(khoa)}
                                onChange={(e) => {
                                  const moi = new Set(oDaChon)
                                  if (e.target.checked) moi.add(khoa)
                                  else moi.delete(khoa)
                                  setODaChon(moi)
                                }}
                              />
                            </Td>
                          )
                        })}
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </div>
            </div>

            <div className="flex gap-2">
              <Button onClick={() => luu.mutate()} disabled={luu.isPending || !tenQuyen}>
                {t('chung.luu')}
              </Button>
              <Button variant="outline" onClick={() => setMoForm(false)}>
                {t('chung.huy')}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('quyen.tenQuyen')}</Th>
              <Th>{t('quyen.soTaiKhoan')}</Th>
              <Th>{t('quyen.chucNang')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {quyens?.map((q) => (
              <tr key={q.id} className="hover:bg-muted/40">
                <Td className="font-medium">{q.tenQuyen}</Td>
                <Td>
                  <Badge variant={q.soTaiKhoan > 0 ? 'accent' : 'muted'}>{q.soTaiKhoan}</Badge>
                </Td>
                <Td className="text-muted-foreground">{q.chucNangs.length}</Td>
                <Td>
                  <div className="flex gap-1">
                    <Button variant="ghost" size="sm" onClick={() => moFormVoi(q)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        if (confirm(t('chung.xacNhanXoa'))) xoa.mutate(q.id)
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
