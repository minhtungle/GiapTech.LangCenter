import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import { ChonTep, type TepDto } from '@/components/ui/ChonTep'

type LoaiTaiLieu = 'GiaoTrinh' | 'BaiGiang' | 'ThamKhao' | 'DeThi' | 'Khac'
const CAC_LOAI: LoaiTaiLieu[] = ['GiaoTrinh', 'BaiGiang', 'ThamKhao', 'DeThi', 'Khac']

interface TaiLieuDto {
  id: string
  tieuDe: string
  moTa: string | null
  loai: LoaiTaiLieu
  nguoiTaiLen: string | null
  ngayTao: string
  teps: TepDto[]
  lopHocIds: string[]
  tenLopHocs: string[]
}

interface LopNgan {
  id: string
  ten: string
}

/** FR-13 — tài liệu giảng dạy. */
export default function TaiLieu({ lopHocId }: { lopHocId?: string } = {}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<TaiLieuDto | null>(null)
  const [lopChon, setLopChon] = useState<string[]>([])
  const [loai, setLoai] = useState<LoaiTaiLieu>('GiaoTrinh')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [xoaCho, setXoaCho] = useState<TaiLieuDto | null>(null)

  const { data: kq = trangRong<TaiLieuDto>(), isLoading } = useQuery({
    queryKey: ['tai-lieu', lopHocId ?? null, timKiem, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<TaiLieuDto>>('/tai-lieu', {
        params: { timKiem: timKiem || undefined, lopHocId, trang, soDong },
      })).data,
  })

  const { data: lops } = useQuery({
    queryKey: ['lop-hoc-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<LopNgan>>('/lop-hoc', { params: { soDong: 200 } }))
        .data.duLieu,
  })

  const lamMoi = () => void qc.invalidateQueries({ queryKey: ['tai-lieu'] })

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setLopChon([])
    setLoai('GiaoTrinh')
    setMaLoi(null)
  }

  const tao = useMutation({
    mutationFn: (b: Record<string, unknown>) => api.post<string>('/tai-lieu', b),
    onSuccess: async (res) => {
      lamMoi()
      // Mở lại form ở chế độ sửa để người dùng đính kèm tệp ngay — tạo xong mà chưa có tệp
      // thì tài liệu rỗng, phải mở lại lần nữa mới gắn được.
      const ds = (await api.get<KetQuaTrang<TaiLieuDto>>('/tai-lieu', { params: { soDong: 200 } }))
        .data.duLieu
      const moi = ds.find((x) => x.id === res.data)
      if (moi) {
        setDangSua(moi)
        setLopChon(moi.lopHocIds)
        setLoai(moi.loai)
      } else dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const capNhat = useMutation({
    mutationFn: (b: Record<string, unknown>) =>
      api.put(`/tai-lieu/${dangSua!.id}`, { ...b, id: dangSua!.id }),
    onSuccess: () => { lamMoi(); dong() },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/tai-lieu/${id}`),
    onSuccess: () => { lamMoi(); setXoaCho(null) },
    onError: (e) => { setMaLoi(layMaLoi(e)); setXoaCho(null) },
  })

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    const body = {
      tieuDe: String(fd.get('tieuDe')),
      moTa: (fd.get('moTa') as string) ?? '',
      loai,
      lopHocIds: lopChon,
    }
    const ten = body.tieuDe as string
    hoi({
      tieuDe: dangSua ? t('chung.xacNhanLuu') : t('chung.xacNhanThem'),
      thongDiep: dangSua ? t('chung.hoiLuu', { ten }) : t('chung.hoiThem', { ten }),
      onDongY: () => (dangSua ? capNhat.mutate(body) : tao.mutate(body)),
    })
  }

  const lamMoiDangSua = async () => {
    if (!dangSua) return
    const ds = (await api.get<KetQuaTrang<TaiLieuDto>>('/tai-lieu', { params: { soDong: 200 } }))
      .data.duLieu
    setDangSua(ds.find((x) => x.id === dangSua.id) ?? null)
    lamMoi()
  }

  const data = kq.duLieu

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="timKiem">{t('chung.timKiem')}</Label>
          <Input
            id="timKiem" value={timKiem} className="w-64"
            onChange={(e) => { setTimKiem(e.target.value); setTrang(1) }}
            placeholder={t('hocLieu.tieuDe')}
          />
        </div>
        {coQuyen('TaiLieu', 'Them') && (
        <Button className="ml-auto" onClick={() => { setDangSua(null); setMoForm(true) }}>
          <Plus className="mr-1.5 h-4 w-4" />
          {t('hocLieu.themTaiLieu')}
        </Button>
        )}
      </div>

      {maLoi && !moForm && (
        <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
      )}

      <Card>
        <CardContent className="pt-5">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
          ) : data.length === 0 ? (
            <TrangTrong thongDiep={t('hocLieu.chuaCoTaiLieu')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('hocLieu.tieuDe')}</Th>
                    <Th>{t('hocLieu.loai')}</Th>
                    <Th>{t('hocLieu.ganLop')}</Th>
                    <Th>{t('hocLieu.tep')}</Th>
                    <Th>{t('hocLieu.nguoiTaiLen')}</Th>
                    <Th className="w-24" />
                  </tr>
                </thead>
                <tbody>
                  {data.map((tl) => (
                    <tr key={tl.id} className="hover:bg-muted/40">
                      <Td className="font-medium">{tl.tieuDe}</Td>
                      <Td className="text-muted-foreground">{t(`loaiTaiLieu.${tl.loai}`)}</Td>
                      <Td>
                        {tl.tenLopHocs.length === 0 ? (
                          <Badge variant="accent">{t('hocLieu.lopChung')}</Badge>
                        ) : (
                          <span className="text-muted-foreground">
                            {tl.tenLopHocs.join(', ')}
                          </span>
                        )}
                      </Td>
                      <Td className="text-muted-foreground">{tl.teps.length}</Td>
                      <Td className="text-muted-foreground">{tl.nguoiTaiLen ?? '—'}</Td>
                      <Td>
                        <div className="flex justify-end">
                          <MenuThaoTac
                            nhanMo={t('chung.thaoTac')}
                            muc={[
                              {
                                nhan: t('chung.sua'),
                                icon: Pencil,
                                an: !coQuyen('TaiLieu', 'Sua'),
                                onChon: () => {
                                  setDangSua(tl); setLopChon(tl.lopHocIds); setLoai(tl.loai)
                                  setMoForm(true)
                                },
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                ngatNhom: true,
                                an: !coQuyen('TaiLieu', 'Xoa'),
                                onChon: () => setXoaCho(tl),
                              },
                            ]}
                          />
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>

              <PhanTrang
                trang={kq.trang} soDong={kq.soDong} tongSoDong={kq.tongSoDong}
                tongSoTrang={kq.tongSoTrang} onDoiTrang={setTrang}
                onDoiSoDong={(n) => { setSoDong(n); setTrang(1) }}
              />
            </>
          )}
        </CardContent>
      </Card>

      {(moForm || dangSua) && (
        <Modal
          mo
          onDong={dong}
          tieuDe={dangSua ? `${t('chung.sua')}: ${dangSua.tieuDe}` : t('hocLieu.themTaiLieu')}
        >
          <form onSubmit={onSubmit} className="grid gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="tieuDe">{t('hocLieu.tieuDe')}</Label>
              <Input id="tieuDe" name="tieuDe" defaultValue={dangSua?.tieuDe ?? ''} required />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="loai">{t('hocLieu.loai')}</Label>
              <SelectTimKiem
                id="loai" choPhepXoa={false}
                luaChon={CAC_LOAI.map((l) => ({ giaTri: l, nhan: t(`loaiTaiLieu.${l}`) }))}
                giaTri={loai}
                onDoi={(v) => setLoai((v as LoaiTaiLieu) ?? 'GiaoTrinh')}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="lopHocIds">{t('hocLieu.ganLop')}</Label>
              <SelectTimKiemNhieu
                id="lopHocIds"
                luaChon={(lops ?? []).map((l) => ({ giaTri: l.id, nhan: l.ten }))}
                giaTri={lopChon}
                onDoi={setLopChon}
                placeholder={t('hocLieu.lopChung')}
              />
              <p className="text-xs text-muted-foreground">{t('hocLieu.ganLopGoiY')}</p>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="moTa">{t('hocLieu.moTa')}</Label>
              <Textarea id="moTa" name="moTa" defaultValue={dangSua?.moTa ?? ''} />
            </div>

            {dangSua && (
              <div className="flex flex-col gap-1.5">
                <Label>{t('hocLieu.tep')}</Label>
                <ChonTep
                  loai="TaiLieu" doiTuongId={dangSua.id} teps={dangSua.teps}
                  onDoi={lamMoiDangSua}
                />
              </div>
            )}

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={dong}>
                {dangSua ? t('chung.dong') : t('chung.huy')}
              </Button>
              <Button type="submit" disabled={tao.isPending || capNhat.isPending}>
                {t('chung.luu')}
              </Button>
            </div>
          </form>
        </Modal>
      )}

      {hop}

      <HopXacNhan
        mo={xoaCho !== null}
        tieuDe={t('chung.xacNhanXoa')}
        thongDiep={xoaCho ? `${t('chung.xoa')} "${xoaCho.tieuDe}"?` : ''}
        onHuy={() => setXoaCho(null)}
        onDongY={() => xoaCho && xoa.mutate(xoaCho.id)}
      />
    </div>
  )
}
