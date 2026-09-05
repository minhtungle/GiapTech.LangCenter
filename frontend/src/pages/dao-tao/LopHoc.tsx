import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CalendarDays, ClipboardList, Pencil, Plus, Trash2, Users, X } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang, type ThamSoTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import { LichVaDiemDanh } from './LichVaDiemDanh'
import { BaiTapCuaLop } from './BaiTapCuaLop'

type HinhThucHoc = 'ChuaChon' | 'Online' | 'Offline' | 'KetHop'
type TrangThaiLopHoc = 'Nhap' | 'SapKhaiGiang' | 'DangHoc' | 'DaKetThuc' | 'DaHuy'

const CAC_HINH_THUC: HinhThucHoc[] = ['Online', 'Offline', 'KetHop']

interface LopHocDto {
  id: string
  ten: string
  giaoVienChinhId: string
  tenGiaoVienChinh: string
  hinhThuc: HinhThucHoc
  phongHoc: string | null
  linkHoc: string | null
  hocPhi: number | null
  sucChuaToiDa: number | null
  ngayKhaiGiang: string | null
  ngayKetThuc: string | null
  trangThai: TrangThaiLopHoc
  ghiChu: string | null
  troGiangIds: string[]
  tenTroGiangs: string[]
  soHocVien: number
}

interface NguoiDungNgan {
  id: string
  hoTen: string
  email: string | null
  loaiNguoiDung: string
}

interface HocVienTrongLop {
  id: string
  hocVienId: string
  hoTen: string
  email: string | null
  soDienThoai: string | null
  ngayVaoLop: string
  trangThai: string
  hocPhiApDung: number
}

const tienVN = (n: number | null) =>
  n === null ? '—' : n.toLocaleString('vi-VN') + '₫'

const ngayVN = (s: string | null) =>
  s ? new Date(s).toLocaleDateString('vi-VN') : '—'

/** Màu badge theo trạng thái — nháp mờ, đang học nổi, đã huỷ đỏ. */
const mauTrangThai = (tt: TrangThaiLopHoc) =>
  tt === 'DangHoc' ? 'win' : tt === 'DaHuy' ? 'lose' : tt === 'Nhap' ? 'draw' : 'accent'

/** FR-07 — quản lý lớp học. */
export default function LopHoc() {
  const { t } = useTranslation()
  const qc = useQueryClient()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [locTrangThai, setLocTrangThai] = useState<string | null>(null)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<LopHocDto | null>(null)
  const [giaoVienChon, setGiaoVienChon] = useState<string | null>(null)
  const [troGiangChon, setTroGiangChon] = useState<string[]>([])
  const [hinhThuc, setHinhThuc] = useState<HinhThucHoc>('Offline')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const [xoaCho, setXoaCho] = useState<LopHocDto | null>(null)
  const [huyCho, setHuyCho] = useState<LopHocDto | null>(null)
  const [lopXemHocVien, setLopXemHocVien] = useState<LopHocDto | null>(null)
  const [lopXemLich, setLopXemLich] = useState<LopHocDto | null>(null)
  const [lopXemBaiTap, setLopXemBaiTap] = useState<LopHocDto | null>(null)

  const thamSo: ThamSoTrang = { trang, soDong }

  const { data: kq = trangRong<LopHocDto>(), isLoading } = useQuery({
    queryKey: ['lop-hoc', timKiem, locTrangThai, trang, soDong],
    queryFn: async () =>
      (
        await api.get<KetQuaTrang<LopHocDto>>('/lop-hoc', {
          params: { timKiem: timKiem || undefined, trangThai: locTrangThai || undefined, ...thamSo },
        })
      ).data,
  })

  // Danh sách người dùng để chọn giáo viên / trợ giảng / học viên.
  const { data: nguoiDungs } = useQuery({
    queryKey: ['tai-khoan-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NguoiDungNgan>>('/tai-khoan', { params: { soDong: 200 } }))
        .data.duLieu,
  })

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setGiaoVienChon(null)
    setTroGiangChon([])
    setHinhThuc('Offline')
    setMaLoi(null)
  }

  const sauKhiLuu = () => {
    void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
    dong()
  }

  const tao = useMutation({
    mutationFn: (form: Record<string, unknown>) => api.post('/lop-hoc', form),
    onSuccess: sauKhiLuu,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const capNhat = useMutation({
    mutationFn: (form: Record<string, unknown>) =>
      api.put(`/lop-hoc/${dangSua!.id}`, { ...form, id: dangSua!.id }),
    onSuccess: sauKhiLuu,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const hoanTat = useMutation({
    mutationFn: (lop: LopHocDto) =>
      api.post(`/lop-hoc/${lop.id}/hoan-tat`, {
        ngayKhaiGiang: new Date().toISOString(),
      }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['lop-hoc'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const huy = useMutation({
    mutationFn: (id: string) => api.post(`/lop-hoc/${id}/huy`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
      setHuyCho(null)
    },
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/lop-hoc/${id}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
      setXoaCho(null)
    },
    onError: (e) => {
      setMaLoiBang(layMaLoi(e))
      setXoaCho(null)
    },
  })

  const moThem = () => {
    setDangSua(null)
    setGiaoVienChon(null)
    setTroGiangChon([])
    setHinhThuc('Offline')
    setMaLoi(null)
    setMoForm(true)
  }

  const moSua = (l: LopHocDto) => {
    setDangSua(l)
    setGiaoVienChon(l.giaoVienChinhId)
    setTroGiangChon(l.troGiangIds)
    setHinhThuc(l.hinhThuc)
    setMaLoi(null)
    setMoForm(true)
  }

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)

    // Mọi trường lệnh cập nhật ghi đè đều đọc TỪ FORM. Gửi cứng null sẽ xoá dữ liệu người
    // dùng chưa từng đụng tới — đúng lỗi đã xảy ra hai lần trong dự án này.
    const hocPhiTho = (fd.get('hocPhi') as string) || ''
    const sucChuaTho = (fd.get('sucChuaToiDa') as string) || ''

    const form = {
      ten: String(fd.get('ten')),
      giaoVienChinhId: giaoVienChon,
      hinhThuc,
      troGiangIds: troGiangChon,
      // Chuỗi rỗng = chủ động xoá ô; backend hiểu '' là xoá, null là giữ nguyên.
      phongHoc: (fd.get('phongHoc') as string) ?? '',
      linkHoc: (fd.get('linkHoc') as string) ?? '',
      ghiChu: (fd.get('ghiChu') as string) ?? '',
      hocPhi: hocPhiTho === '' ? null : Number(hocPhiTho),
      sucChuaToiDa: sucChuaTho === '' ? null : Number(sucChuaTho),
      // Ô sức chứa để trống khi SỬA nghĩa là "bỏ giới hạn" — null không diễn đạt được điều
      // đó vì null đã mang nghĩa "không gửi".
      boGioiHanSucChua: Boolean(dangSua) && sucChuaTho === '',
    }

    if (!giaoVienChon) {
      setMaLoi('NHAN_SU_KHONG_HOP_LE')
      return
    }

    if (dangSua) capNhat.mutate(form)
    else tao.mutate(form)
  }

  const giaoVienLuaChon = (nguoiDungs ?? [])
    .filter((u) => u.loaiNguoiDung === 'GiaoVien')
    .map((u) => ({ giaTri: u.id, nhan: u.hoTen, phu: u.email ?? undefined }))

  const troGiangLuaChon = (nguoiDungs ?? [])
    .filter((u) => u.loaiNguoiDung === 'TroGiang' || u.loaiNguoiDung === 'GiaoVien')
    .filter((u) => u.id !== giaoVienChon)
    .map((u) => ({ giaTri: u.id, nhan: u.hoTen, phu: u.email ?? undefined }))

  const data = kq.duLieu
  const dangLuu = tao.isPending || capNhat.isPending

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="timKiem">{t('chung.timKiem')}</Label>
          <Input
            id="timKiem"
            value={timKiem}
            onChange={(e) => {
              setTimKiem(e.target.value)
              setTrang(1)
            }}
            placeholder={t('lopHoc.ten')}
            className="w-64"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="locTrangThai">{t('lopHoc.locTrangThai')}</Label>
          <SelectTimKiem
            id="locTrangThai"
            luaChon={(['Nhap', 'SapKhaiGiang', 'DangHoc', 'DaKetThuc', 'DaHuy'] as const).map(
              (tt) => ({ giaTri: tt, nhan: t(`trangThaiLopHoc.${tt}`) }),
            )}
            giaTri={locTrangThai}
            onDoi={(v) => {
              setLocTrangThai(v)
              setTrang(1)
            }}
            placeholder={t('chung.tatCa')}
          />
        </div>

        <Button onClick={moThem} className="ml-auto">
          <Plus className="mr-1.5 h-4 w-4" />
          {t('lopHoc.themMoi')}
        </Button>
      </div>

      {maLoiBang && (
        <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
      )}

      <Card>
        <CardContent className="pt-5">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
          ) : data.length === 0 ? (
            <TrangTrong thongDiep={t('lopHoc.chuaCoLop')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('lopHoc.ten')}</Th>
                    <Th>{t('lopHoc.giaoVienChinh')}</Th>
                    <Th>{t('lopHoc.hinhThuc')}</Th>
                    <Th>{t('lopHoc.soHocVien')}</Th>
                    <Th>{t('lopHoc.hocPhi')}</Th>
                    <Th>{t('lopHoc.ngayKhaiGiang')}</Th>
                    <Th>{t('lopHoc.trangThai')}</Th>
                    <Th className="w-40" />
                  </tr>
                </thead>
                <tbody>
                  {data.map((l) => (
                    <tr key={l.id} className="hover:bg-muted/40">
                      <Td className="font-medium">{l.ten}</Td>
                      <Td className="text-muted-foreground">{l.tenGiaoVienChinh}</Td>
                      <Td className="text-muted-foreground">
                        {t(`hinhThucHoc.${l.hinhThuc}`)}
                      </Td>
                      <Td className="text-muted-foreground">
                        {l.soHocVien}
                        {l.sucChuaToiDa !== null && ` / ${l.sucChuaToiDa}`}
                      </Td>
                      <Td className="text-muted-foreground">{tienVN(l.hocPhi)}</Td>
                      <Td className="text-muted-foreground">{ngayVN(l.ngayKhaiGiang)}</Td>
                      <Td>
                        <Badge variant={mauTrangThai(l.trangThai)}>
                          {t(`trangThaiLopHoc.${l.trangThai}`)}
                        </Badge>
                      </Td>
                      <Td>
                        <div className="flex justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            title={t('buoiHoc.lich')}
                            onClick={() => setLopXemLich(l)}
                          >
                            <CalendarDays className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            title={t('hocLieu.baiTap')}
                            onClick={() => setLopXemBaiTap(l)}
                          >
                            <ClipboardList className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            title={t('lopHoc.hocVien')}
                            onClick={() => setLopXemHocVien(l)}
                          >
                            <Users className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            title={t('chung.sua')}
                            onClick={() => moSua(l)}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          {l.trangThai === 'Nhap' ? (
                            <Button
                              variant="ghost"
                              size="sm"
                              title={t('chung.xoa')}
                              onClick={() => setXoaCho(l)}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          ) : l.trangThai !== 'DaHuy' ? (
                            <Button
                              variant="ghost"
                              size="sm"
                              title={t('lopHoc.huyLop')}
                              onClick={() => setHuyCho(l)}
                            >
                              <X className="h-4 w-4" />
                            </Button>
                          ) : null}
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>

              {/* Lớp nháp cần một đường rõ ràng để công bố, nếu không nó nằm mãi ở nháp. */}
              {data.some((l) => l.trangThai === 'Nhap') && (
                <div className="mt-3 rounded-md border border-dashed border-border p-3">
                  <p className="text-xs text-muted-foreground">{t('lopHoc.nhapGoiY')}</p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {data
                      .filter((l) => l.trangThai === 'Nhap')
                      .map((l) => (
                        <Button
                          key={l.id}
                          size="sm"
                          variant="outline"
                          disabled={hoanTat.isPending}
                          onClick={() => hoanTat.mutate(l)}
                        >
                          {t('lopHoc.hoanTat')}: {l.ten}
                        </Button>
                      ))}
                  </div>
                </div>
              )}

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
        tieuDe={dangSua ? `${t('chung.sua')}: ${dangSua.ten}` : t('lopHoc.themMoi')}
      >
        <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="ten">{t('lopHoc.ten')}</Label>
            <Input id="ten" name="ten" defaultValue={dangSua?.ten ?? ''} required autoFocus />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="giaoVienChinhId">{t('lopHoc.giaoVienChinh')}</Label>
            <SelectTimKiem
              id="giaoVienChinhId"
              luaChon={giaoVienLuaChon}
              giaTri={giaoVienChon}
              onDoi={setGiaoVienChon}
              placeholder={t('lopHoc.chonGiaoVien')}
              placeholderTimKiem={t('lopHoc.timGiaoVien')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="hinhThuc">{t('lopHoc.hinhThuc')}</Label>
            <SelectTimKiem
              id="hinhThuc"
              choPhepXoa={false}
              luaChon={CAC_HINH_THUC.map((h) => ({
                giaTri: h,
                nhan: t(`hinhThucHoc.${h}`),
              }))}
              giaTri={hinhThuc}
              onDoi={(v) => setHinhThuc((v as HinhThucHoc) ?? 'Offline')}
            />
          </div>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="troGiangIds">{t('lopHoc.troGiang')}</Label>
            <SelectTimKiemNhieu
              id="troGiangIds"
              luaChon={troGiangLuaChon}
              giaTri={troGiangChon}
              onDoi={setTroGiangChon}
              placeholder={t('lopHoc.chonTroGiang')}
              placeholderTimKiem={t('lopHoc.timTroGiang')}
            />
          </div>

          {hinhThuc !== 'Online' && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="phongHoc">{t('lopHoc.phongHoc')}</Label>
              <Input id="phongHoc" name="phongHoc" defaultValue={dangSua?.phongHoc ?? ''} />
            </div>
          )}

          {hinhThuc !== 'Offline' && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="linkHoc">{t('lopHoc.linkHoc')}</Label>
              <Input id="linkHoc" name="linkHoc" defaultValue={dangSua?.linkHoc ?? ''} />
            </div>
          )}

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="hocPhi">{t('lopHoc.hocPhi')}</Label>
            <Input
              id="hocPhi"
              name="hocPhi"
              type="number"
              min={0}
              step={1000}
              defaultValue={dangSua?.hocPhi ?? ''}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="sucChuaToiDa">{t('lopHoc.sucChuaToiDa')}</Label>
            <Input
              id="sucChuaToiDa"
              name="sucChuaToiDa"
              type="number"
              min={1}
              defaultValue={dangSua?.sucChuaToiDa ?? ''}
              placeholder={t('lopHoc.khongGioiHan')}
            />
          </div>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="ghiChu">{t('lopHoc.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" defaultValue={dangSua?.ghiChu ?? ''} />
          </div>

          {maLoi && (
            <div className="sm:col-span-2">
              <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
            </div>
          )}

          <div className="flex justify-end gap-2 sm:col-span-2">
            <Button type="button" variant="outline" onClick={dong}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={dangLuu}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {lopXemLich && (
        <LichVaDiemDanh
          lopHocId={lopXemLich.id}
          tenLop={lopXemLich.ten}
          onDong={() => setLopXemLich(null)}
        />
      )}

      {lopXemBaiTap && (
        <BaiTapCuaLop
          lopHocId={lopXemBaiTap.id}
          tenLop={lopXemBaiTap.ten}
          onDong={() => setLopXemBaiTap(null)}
        />
      )}

      {lopXemHocVien && (
        <HocVienCuaLop
          lop={lopXemHocVien}
          nguoiDungs={nguoiDungs ?? []}
          onDong={() => setLopXemHocVien(null)}
        />
      )}

      <HopXacNhan
        mo={xoaCho !== null}
        tieuDe={t('chung.xacNhanXoa')}
        thongDiep={xoaCho ? `${t('chung.xoa')} "${xoaCho.ten}"?` : ''}
        onHuy={() => setXoaCho(null)}
        onDongY={() => xoaCho && xoa.mutate(xoaCho.id)}
      />

      <HopXacNhan
        mo={huyCho !== null}
        tieuDe={t('lopHoc.huyLop')}
        thongDiep={huyCho ? `${huyCho.ten} — ${t('lopHoc.xacNhanHuy')}` : ''}
        onHuy={() => setHuyCho(null)}
        onDongY={() => huyCho && huy.mutate(huyCho.id)}
      />
    </div>
  )
}

/** Bước 3 của wizard, cũng dùng lại làm màn quản lý học viên của lớp. */
function HocVienCuaLop({
  lop,
  nguoiDungs,
  onDong,
}: {
  lop: LopHocDto
  nguoiDungs: NguoiDungNgan[]
  onDong: () => void
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
    <Modal mo onDong={onDong} tieuDe={`${t('lopHoc.hocVien')} — ${lop.ten}`}>
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
    </Modal>
  )
}
