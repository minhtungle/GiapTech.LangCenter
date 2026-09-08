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
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { CAC_DON_VI, tien, type DonViTien, type SanPhamDto } from './crmTypes'

/**
 * FR-20 — danh mục sản phẩm bán kèm: sách, học cụ, đồng phục (CRM).
 *
 * Bảng riêng chứ không gộp vào `KHOA_HOC` kèm cột `loai`: gộp thì `so_buoi` luôn NULL cho
 * sách, và mọi query khoá học phải nhớ `WHERE loai = ...`. Xem `docs/nghiep-vu/crm.md`.
 */
export default function SanPham() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [locBan, setLocBan] = useState<string | null>(null)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<SanPhamDto | null>(null)
  const [donVi, setDonVi] = useState<DonViTien>('VND')
  const [dangBan, setDangBan] = useState(true)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: kq = trangRong<SanPhamDto>(), isLoading } = useQuery({
    queryKey: ['san-pham', timKiem, locBan, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<SanPhamDto>>('/san-pham', {
        params: {
          timKiem: timKiem || undefined,
          dangBan: locBan === null ? undefined : locBan === 'true',
          trang,
          soDong,
        },
      })).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['san-pham'] })
    // Ô chọn khoá ở màn Doanh thu phải thấy khoá mới ngay.
    void qc.invalidateQueries({ queryKey: ['san-pham-ngan'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        ten: String(fd.get('ten')).trim(),
        ghiChu: String(fd.get('ghiChu') ?? '').trim() || null,
        giaTien: Number(fd.get('giaTien')),
        donViTien: donVi,
        donViTinh: String(fd.get('donViTinh') ?? '').trim() || null,
        dangBan,
      }
      if (dangSua) await api.put(`/san-pham/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/san-pham', than)
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/san-pham/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moSua = (k: SanPhamDto) => {
    setDangSua(k)
    setDonVi(k.donViTien)
    setDangBan(k.dangBan)
    setMaLoi(null)
    setMoForm(true)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-56">
            <Label htmlFor="tim">{t('chung.timKiem')}</Label>
            <Input
              id="tim"
              value={timKiem}
              onChange={(e) => {
                setTimKiem(e.target.value)
                setTrang(1)
              }}
              placeholder={t('sanPham.tenSp')}
            />
          </div>
          <div className="w-44">
            <Label htmlFor="loc-ban">{t('sanPham.trangThaiBan')}</Label>
            <SelectTimKiem
              id="loc-ban"
              luaChon={[
                { giaTri: 'true', nhan: t('sanPham.dangBan') },
                { giaTri: 'false', nhan: t('sanPham.ngungBan') },
              ]}
              giaTri={locBan}
              onDoi={(v) => {
                setLocBan(v)
                setTrang(1)
              }}
              placeholder={t('chung.tatCa')}
            />
          </div>
        </div>

        {coQuyen('SanPham', 'Them') && (
          <Button
            onClick={() => {
              setDangSua(null)
              setDonVi('VND')
              setDangBan(true)
              setMaLoi(null)
              setMoForm(true)
            }}
          >
            <Plus className="h-4 w-4" />
            {t('sanPham.them')}
          </Button>
        )}
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : kq.duLieu.length === 0 ? (
            <TrangTrong thongDiep={t('sanPham.chuaCo')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('sanPham.tenSp')}</Th>
                    <Th className="text-right">{t('sanPham.giaTien')}</Th>
                    <Th>{t('sanPham.donViTinh')}</Th>
                    <Th className="text-right">{t('sanPham.soDonHang')}</Th>
                    <Th className="text-right">{t('sanPham.tongSoLuongBan')}</Th>
                    <Th>{t('sanPham.trangThaiBan')}</Th>
                    <Th>{t('chung.ghiChu')}</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {kq.duLieu.map((k) => (
                    <tr key={k.id} className="hover:bg-muted/40">
                      <Td className="font-medium">{k.ten}</Td>
                      <Td className="text-right">{tien(k.giaTien, k.donViTien)}</Td>
                      <Td className="text-muted-foreground">{k.donViTinh ?? '—'}</Td>
                      <Td className="text-right text-muted-foreground">{k.soDonHang}</Td>
                      <Td className="text-right text-muted-foreground">{k.tongSoLuongBan}</Td>
                      <Td>
                        <Badge variant={k.dangBan ? 'ok' : 'muted'}>
                          {t(k.dangBan ? 'sanPham.dangBan' : 'sanPham.ngungBan')}
                        </Badge>
                      </Td>
                      <Td className="max-w-xs truncate text-muted-foreground" title={k.ghiChu ?? ''}>
                        {k.ghiChu || '—'}
                      </Td>
                      <Td>
                        <div className="flex justify-end">
                          <MenuThaoTac
                            nhanMo={t('chung.thaoTac')}
                            muc={[
                              {
                                nhan: t('chung.sua'),
                                icon: Pencil,
                                an: !coQuyen('SanPham', 'Sua'),
                                onChon: () => moSua(k),
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                ngatNhom: true,
                                // Khoá đã bán không xoá được (FK Restrict) — ẩn nút thay vì
                                // để người dùng bấm rồi nhận lỗi.
                                an: !coQuyen('SanPham', 'Xoa') || k.soDonHang > 0,
                                onChon: () =>
                                  hoi({
                                    tieuDe: t('chung.xacNhanXoa'),
                                    thongDiep: t('sanPham.hoiXoa', { ten: k.ten }),
                                    nhanDongY: t('chung.xoa'),
                                    nguyHiem: true,
                                    onDongY: () => xoa.mutate(k.id),
                                  }),
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
            </>
          )}
        </CardContent>
      </Card>

      <Modal
        mo={moForm}
        onDong={dong}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('sanPham.sua') : t('sanPham.them')}
        moTa={dangSua?.ten}
        rong="md"
      >
        <form
          className="grid gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            hoi({
              tieuDe: t('chung.xacNhanLuu'),
              thongDiep: t('sanPham.hoiLuu'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div>
            <Label htmlFor="ten">{t('sanPham.tenSp')} *</Label>
            <Input id="ten" name="ten" required defaultValue={dangSua?.ten ?? ''} autoFocus />
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <Label htmlFor="giaTien">{t('sanPham.giaTien')} *</Label>
              <Input
                id="giaTien"
                name="giaTien"
                type="number"
                min={0}
                step="0.01"
                required
                defaultValue={dangSua?.giaTien ?? 0}
              />
            </div>
            <div>
              <Label htmlFor="donVi">{t('sanPham.donViTien')} *</Label>
              <SelectTimKiem
                id="donVi"
                luaChon={CAC_DON_VI.map((d) => ({ giaTri: d, nhan: t(`donViTien.${d}`) }))}
                giaTri={donVi}
                onDoi={(v) => setDonVi((v as DonViTien) ?? 'VND')}
                choPhepXoa={false}
              />
            </div>
            <div>
              <Label htmlFor="donViTinh">{t('sanPham.donViTinh')}</Label>
              <Input
                id="donViTinh"
                name="donViTinh"
                placeholder={t('sanPham.donViTinhGoiY')}
                defaultValue={dangSua?.donViTinh ?? ''}
              />
            </div>
          </div>

          <div>
            <Label htmlFor="ghiChu">{t('chung.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" rows={3} defaultValue={dangSua?.ghiChu ?? ''} />
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="h-4 w-4 accent-[hsl(var(--primary))]"
              checked={dangBan}
              onChange={(e) => setDangBan(e.target.checked)}
            />
            {t('sanPham.conBan')}
          </label>
          <p className="text-xs text-muted-foreground">{t('sanPham.ngungBanGoiY')}</p>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={dong}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {hop}
    </div>
  )
}
