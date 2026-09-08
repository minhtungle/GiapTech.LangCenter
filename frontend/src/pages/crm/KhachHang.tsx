import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ExternalLink, Pencil, Plus, Trash2 } from 'lucide-react'
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
import {
  CAC_PHUONG_THUC, tien, type KhachHangDto, type PhuongThucThanhToan,
} from './crmTypes'

interface HocVienNgan {
  id: string
  hoTen: string
}

/**
 * FR-17 — khách hàng (CRM): người quan tâm khoá học, chưa chắc thành học viên.
 *
 * Bảng riêng chứ không dùng `NGUOI_DUNG` — xem `docs/nghiep-vu/crm.md`. Khi khách thật sự vào
 * học thì **nối** bằng `nguoiDungId`, không copy họ tên sang.
 */
export default function KhachHang() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [locMua, setLocMua] = useState<string | null>(null)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<KhachHangDto | null>(null)
  const [phuongThuc, setPhuongThuc] = useState<PhuongThucThanhToan>('ChuyenKhoan')
  const [hocVienId, setHocVienId] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: kq = trangRong<KhachHangDto>(), isLoading } = useQuery({
    queryKey: ['khach-hang', timKiem, locMua, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhachHangDto>>('/khach-hang', {
        params: {
          timKiem: timKiem || undefined,
          daMua: locMua === null ? undefined : locMua === 'true',
          trang,
          soDong,
        },
      })).data,
  })

  /**
   * Danh sách học viên để NỐI khách với hồ sơ học tập.
   *
   * `enabled: coQuyen('TaiKhoan')` — người trực tổng đài chỉ có quyền `KhachHang` sẽ nhận 403
   * ở endpoint này; gọi vô điều kiện thì họ thấy một lỗi mạng không giải thích được.
   */
  const { data: hocViens = [] } = useQuery({
    queryKey: ['hoc-vien-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<HocVienNgan>>('/hoc-vien', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: coQuyen('TaiKhoan'),
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['khach-hang'] })
    void qc.invalidateQueries({ queryKey: ['khach-hang-ngan'] })
    // Tên khách hiện ở màn Doanh thu.
    void qc.invalidateQueries({ queryKey: ['doanh-thu'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        hoTen: String(fd.get('hoTen')).trim(),
        email: String(fd.get('email') ?? '').trim() || null,
        soDienThoai: String(fd.get('soDienThoai') ?? '').trim() || null,
        linkFacebook: String(fd.get('linkFacebook') ?? '').trim() || null,
        ghiChu: String(fd.get('ghiChu') ?? '').trim() || null,
        phuongThucThanhToan: phuongThuc,
        nguoiDungId: hocVienId,
      }
      if (dangSua) await api.put(`/khach-hang/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/khach-hang', than)
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/khach-hang/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moSua = (k: KhachHangDto) => {
    setDangSua(k)
    setPhuongThuc(k.phuongThucThanhToan)
    setHocVienId(k.nguoiDungId)
    setMaLoi(null)
    setMoForm(true)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-64">
            <Label htmlFor="tim">{t('chung.timKiem')}</Label>
            <Input
              id="tim"
              value={timKiem}
              onChange={(e) => {
                setTimKiem(e.target.value)
                setTrang(1)
              }}
              placeholder={t('khachHang.timTheo')}
            />
          </div>
          <div className="w-44">
            <Label htmlFor="loc-mua">{t('khachHang.tinhTrang')}</Label>
            <SelectTimKiem
              id="loc-mua"
              luaChon={[
                { giaTri: 'true', nhan: t('khachHang.daMua') },
                { giaTri: 'false', nhan: t('khachHang.chuaMua') },
              ]}
              giaTri={locMua}
              onDoi={(v) => {
                setLocMua(v)
                setTrang(1)
              }}
              placeholder={t('chung.tatCa')}
            />
          </div>
        </div>

        {coQuyen('KhachHang', 'Them') && (
          <Button
            onClick={() => {
              setDangSua(null)
              setPhuongThuc('ChuyenKhoan')
              setHocVienId(null)
              setMaLoi(null)
              setMoForm(true)
            }}
          >
            <Plus className="h-4 w-4" />
            {t('khachHang.them')}
          </Button>
        )}
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : kq.duLieu.length === 0 ? (
            <TrangTrong thongDiep={t('khachHang.chuaCo')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('khachHang.hoTen')}</Th>
                    <Th>{t('khachHang.lienHe')}</Th>
                    <Th>{t('khachHang.phuongThuc')}</Th>
                    <Th className="text-right">{t('khachHang.soKhoa')}</Th>
                    <Th className="text-right">{t('khachHang.tongMua')}</Th>
                    <Th>{t('khachHang.hocVien')}</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {kq.duLieu.map((k) => (
                    <tr key={k.id} className="hover:bg-muted/40">
                      <Td>
                        <div className="font-medium">{k.hoTen}</div>
                        {k.ghiChu && (
                          <div
                            className="max-w-xs truncate text-xs text-muted-foreground"
                            title={k.ghiChu}
                          >
                            {k.ghiChu}
                          </div>
                        )}
                      </Td>
                      <Td className="text-muted-foreground">
                        <div className="flex flex-col gap-0.5 text-xs">
                          {k.soDienThoai && <span>{k.soDienThoai}</span>}
                          {k.email && <span className="truncate">{k.email}</span>}
                          {k.linkFacebook && (
                            <a
                              href={k.linkFacebook}
                              target="_blank"
                              rel="noreferrer noopener"
                              className="inline-flex items-center gap-1 text-primary hover:underline"
                            >
                              Facebook
                              <ExternalLink className="h-3 w-3" />
                            </a>
                          )}
                          {!k.soDienThoai && !k.email && !k.linkFacebook && '—'}
                        </div>
                      </Td>
                      <Td className="text-muted-foreground">
                        {t(`phuongThucThanhToan.${k.phuongThucThanhToan}`)}
                      </Td>
                      <Td className="text-right">
                        {k.soDangKy > 0 ? (
                          <Badge variant="ok">{k.soDangKy}</Badge>
                        ) : (
                          <span className="text-muted-foreground">0</span>
                        )}
                      </Td>
                      <Td className="text-right">
                        {k.soDangKy > 0 ? tien(k.tongMuaVnd) : '—'}
                      </Td>
                      <Td className="text-muted-foreground">
                        {k.tenHocVien ? (
                          <Badge variant="accent">{k.tenHocVien}</Badge>
                        ) : (
                          t('khachHang.chuaVaoHoc')
                        )}
                      </Td>
                      <Td>
                        <div className="flex justify-end">
                          <MenuThaoTac
                            nhanMo={t('chung.thaoTac')}
                            muc={[
                              {
                                nhan: t('chung.sua'),
                                icon: Pencil,
                                an: !coQuyen('KhachHang', 'Sua'),
                                onChon: () => moSua(k),
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                ngatNhom: true,
                                // Khách đã mua không xoá được — đăng ký là dữ liệu tiền.
                                an: !coQuyen('KhachHang', 'Xoa') || k.soDangKy > 0,
                                onChon: () =>
                                  hoi({
                                    tieuDe: t('chung.xacNhanXoa'),
                                    thongDiep: t('khachHang.hoiXoa', { ten: k.hoTen }),
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
        tieuDe={dangSua ? t('khachHang.sua') : t('khachHang.them')}
        moTa={dangSua?.hoTen}
        rong="md"
      >
        <form
          className="grid gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            hoi({
              tieuDe: t('chung.xacNhanLuu'),
              thongDiep: t('khachHang.hoiLuu'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div>
            <Label htmlFor="hoTen">{t('khachHang.hoTen')} *</Label>
            <Input id="hoTen" name="hoTen" required defaultValue={dangSua?.hoTen ?? ''} autoFocus />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="soDienThoai">{t('khachHang.soDienThoai')}</Label>
              <Input
                id="soDienThoai"
                name="soDienThoai"
                defaultValue={dangSua?.soDienThoai ?? ''}
              />
            </div>
            <div>
              <Label htmlFor="email">{t('khachHang.email')}</Label>
              <Input id="email" name="email" type="email" defaultValue={dangSua?.email ?? ''} />
            </div>
          </div>

          <div>
            <Label htmlFor="linkFacebook">{t('khachHang.linkFacebook')}</Label>
            <Input
              id="linkFacebook"
              name="linkFacebook"
              placeholder="https://facebook.com/..."
              defaultValue={dangSua?.linkFacebook ?? ''}
            />
          </div>

          <div>
            <Label htmlFor="phuongThuc">{t('khachHang.phuongThuc')}</Label>
            <SelectTimKiem
              id="phuongThuc"
              luaChon={CAC_PHUONG_THUC.map((p) => ({
                giaTri: p,
                nhan: t(`phuongThucThanhToan.${p}`),
              }))}
              giaTri={phuongThuc}
              onDoi={(v) => setPhuongThuc((v as PhuongThucThanhToan) ?? 'ChuyenKhoan')}
              choPhepXoa={false}
            />
            <p className="mt-1 text-xs text-muted-foreground">{t('khachHang.phuongThucGoiY')}</p>
          </div>

          {/* Nối với hồ sơ học viên — chỉ hiện khi người dùng đọc được danh sách học viên. */}
          {coQuyen('TaiKhoan') && (
            <div>
              <Label htmlFor="hocVien">{t('khachHang.noiHocVien')}</Label>
              <SelectTimKiem
                id="hocVien"
                luaChon={hocViens.map((h) => ({ giaTri: h.id, nhan: h.hoTen }))}
                giaTri={hocVienId}
                onDoi={setHocVienId}
                placeholder={t('khachHang.chuaVaoHoc')}
              />
              <p className="mt-1 text-xs text-muted-foreground">{t('khachHang.noiHocVienGoiY')}</p>
            </div>
          )}

          <div>
            <Label htmlFor="ghiChu">{t('chung.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" rows={3} defaultValue={dangSua?.ghiChu ?? ''} />
          </div>

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
