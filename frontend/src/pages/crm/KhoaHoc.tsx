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
import { CAC_DON_VI, tien, type DonViTien, type KhoaHocDto } from './crmTypes'

/**
 * FR-19 — danh mục khoá học bán ra (CRM).
 *
 * Khoá học ≠ Lớp học: khoá là **sản phẩm** bán đi bán lại, lớp là một **lần mở** cụ thể có
 * giáo viên và lịch. Xem `docs/06-nghiep-vu/crm.md`.
 */
export default function KhoaHoc() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [locBan, setLocBan] = useState<string | null>(null)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<KhoaHocDto | null>(null)
  const [donVi, setDonVi] = useState<DonViTien>('VND')
  const [dangBan, setDangBan] = useState(true)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: kq = trangRong<KhoaHocDto>(), isLoading } = useQuery({
    queryKey: ['khoa-hoc', timKiem, locBan, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhoaHocDto>>('/khoa-hoc', {
        params: {
          timKiem: timKiem || undefined,
          dangBan: locBan === null ? undefined : locBan === 'true',
          trang,
          soDong,
        },
      })).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['khoa-hoc'] })
    // Ô chọn khoá ở màn Doanh thu phải thấy khoá mới ngay.
    void qc.invalidateQueries({ queryKey: ['khoa-hoc-ngan'] })
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
        soBuoi: Number(fd.get('soBuoi')),
        dangBan,
      }
      if (dangSua) await api.put(`/khoa-hoc/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/khoa-hoc', than)
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/khoa-hoc/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moSua = (k: KhoaHocDto) => {
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
              placeholder={t('khoaHoc.tenKhoa')}
            />
          </div>
          <div className="w-44">
            <Label htmlFor="loc-ban">{t('khoaHoc.trangThaiBan')}</Label>
            <SelectTimKiem
              id="loc-ban"
              luaChon={[
                { giaTri: 'true', nhan: t('khoaHoc.dangBan') },
                { giaTri: 'false', nhan: t('khoaHoc.ngungBan') },
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

        {coQuyen('KhoaHoc', 'Them') && (
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
            {t('khoaHoc.them')}
          </Button>
        )}
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : kq.duLieu.length === 0 ? (
            <TrangTrong thongDiep={t('khoaHoc.chuaCo')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('khoaHoc.tenKhoa')}</Th>
                    <Th className="text-right">{t('khoaHoc.giaTien')}</Th>
                    <Th className="text-right">{t('khoaHoc.soBuoi')}</Th>
                    <Th className="text-right">{t('khoaHoc.soDangKy')}</Th>
                    <Th>{t('khoaHoc.trangThaiBan')}</Th>
                    <Th>{t('chung.ghiChu')}</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {kq.duLieu.map((k) => (
                    <tr key={k.id} className="hover:bg-muted/40">
                      <Td className="font-medium">{k.ten}</Td>
                      <Td className="text-right">{tien(k.giaTien, k.donViTien)}</Td>
                      <Td className="text-right text-muted-foreground">{k.soBuoi}</Td>
                      <Td className="text-right text-muted-foreground">{k.soDangKy}</Td>
                      <Td>
                        <Badge variant={k.dangBan ? 'ok' : 'muted'}>
                          {t(k.dangBan ? 'khoaHoc.dangBan' : 'khoaHoc.ngungBan')}
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
                                an: !coQuyen('KhoaHoc', 'Sua'),
                                onChon: () => moSua(k),
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                ngatNhom: true,
                                // Khoá đã bán không xoá được (FK Restrict) — ẩn nút thay vì
                                // để người dùng bấm rồi nhận lỗi.
                                an: !coQuyen('KhoaHoc', 'Xoa') || k.soDangKy > 0,
                                onChon: () =>
                                  hoi({
                                    tieuDe: t('chung.xacNhanXoa'),
                                    thongDiep: t('khoaHoc.hoiXoa', { ten: k.ten }),
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
        tieuDe={dangSua ? t('khoaHoc.sua') : t('khoaHoc.them')}
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
              thongDiep: t('khoaHoc.hoiLuu'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div>
            <Label htmlFor="ten">{t('khoaHoc.tenKhoa')} *</Label>
            <Input id="ten" name="ten" required defaultValue={dangSua?.ten ?? ''} autoFocus />
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <Label htmlFor="giaTien">{t('khoaHoc.giaTien')} *</Label>
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
              <Label htmlFor="donVi">{t('khoaHoc.donViTien')} *</Label>
              <SelectTimKiem
                id="donVi"
                luaChon={CAC_DON_VI.map((d) => ({ giaTri: d, nhan: t(`donViTien.${d}`) }))}
                giaTri={donVi}
                onDoi={(v) => setDonVi((v as DonViTien) ?? 'VND')}
                choPhepXoa={false}
              />
            </div>
            <div>
              <Label htmlFor="soBuoi">{t('khoaHoc.soBuoi')} *</Label>
              <Input
                id="soBuoi"
                name="soBuoi"
                type="number"
                min={1}
                max={500}
                required
                defaultValue={dangSua?.soBuoi ?? 40}
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
            {t('khoaHoc.conBan')}
          </label>
          <p className="text-xs text-muted-foreground">{t('khoaHoc.ngungBanGoiY')}</p>

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
